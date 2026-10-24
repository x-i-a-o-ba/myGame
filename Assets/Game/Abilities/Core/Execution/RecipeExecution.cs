using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    public sealed partial class AbilityExecution
    {
        private readonly RecipeProgramStore programs=new RecipeProgramStore();
        private readonly Dictionary<(Entity owner,int cast),int> derivedCounts=new Dictionary<(Entity,int),int>();
        private readonly HashSet<int> activeCasts=new HashSet<int>();
        private readonly List<(Entity owner,int cast)> endedCasts=new List<(Entity,int)>();

        /// <summary>表现读取施法当时的几何版本。宿主不得保存返回引用并越过下一次 Step/ReleaseOwner。</summary>
        public BlobAssetReference<AbilityLibrary> GetLibrary(Entity owner,int version)
        {
            var runtime=manager.GetComponentData<AbilityRuntime>(owner);
            if(version==0) return runtime.Library; // 尚未经过首个 Step 的兼容快照。
            return programs.Get(owner,version);
        }
        /// <summary>
        /// 只在非施法动作期间替换相同技能目录；成功接管 replacement，失败时仍由调用者释放。
        /// 冷却与补充计时保留，容量缩小时裁减库存；新蓄层条件会清除旧进度。
        /// </summary>
        public bool ReplaceLibrary(Entity owner,BlobAssetReference<AbilityLibrary> replacement,out string failure)
        {
            failure=null;
            if(!replacement.IsCreated || !manager.Exists(owner) || !manager.HasComponent<AbilityRuntime>(owner))
            { failure="替换技能库或拥有者无效。"; return false; }
            var runtime=manager.GetComponentData<AbilityRuntime>(owner);
            if(!runtime.Library.IsCreated || !manager.HasBuffer<AbilitySlot>(owner))
            { failure="技能拥有者尚未初始化或已经释放。"; return false; }
            if(AbilityCastTiming.IsBusy(runtime,runtime.Time) || runtime.AwaitingRelease)
            { failure="请等待施法前摇和后摇结束后再换装。"; return false; }
            if(!RecipeProgramStore.Compatible(ref runtime.Library.Value,ref replacement.Value,out failure)) return false;
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            // 在接管内存之前核对运行态，保证失败路径不会留下已接管却未提交的替换库。
            for(int i=0;i<slots.Length;i++)
                if(slots[i].Definition<0 || slots[i].Definition>=runtime.Library.Value.Skills.Length)
                { failure="技能槽引用了不存在的定义，无法安全换装。"; return false; }
            int version;
            try { programs.Ensure(owner,runtime.Library); version=programs.Adopt(owner,replacement); }
            catch(InvalidOperationException ex) { failure=ex.Message; return false; }
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];
                ref var old=ref runtime.Library.Value.Skills[slot.Definition];
                ref var next=ref replacement.Value.Skills[slot.Definition];
                // 原先已满库存没有在途充能；扩容后的空位从换装时开始恢复，不能追溯补发。
                if(slot.Charges==old.MaxCharges && next.MaxCharges>old.MaxCharges)
                    slot.RechargeAt=runtime.Time+math.max(.001f,next.RechargeSeconds);
                slot.Charges=math.min(slot.Charges,next.MaxCharges);
                if(old.RequiredHits!=next.RequiredHits) slot.HitStacks=0;
                slots[i]=slot;
            }
            runtime.Library=replacement; runtime.ProgramVersion=version;
            manager.SetComponentData(owner,runtime);
            return true;
        }
        /// <summary>宿主重开会话时先完成 Job，再调用本方法，最后销毁实体并释放原始借用库。</summary>
        public void ReleaseOwner(Entity owner)
        {
            if(manager.Exists(owner))
            {
                if(manager.HasBuffer<ScheduledSkillStage>(owner)) manager.GetBuffer<ScheduledSkillStage>(owner).Clear();
                if(manager.HasBuffer<SkillFlight>(owner)) manager.GetBuffer<SkillFlight>(owner).Clear();
                if(manager.HasBuffer<SkillCue>(owner)) manager.GetBuffer<SkillCue>(owner).Clear();
                if(manager.HasComponent<AbilityRuntime>(owner))
                {
                    var runtime=manager.GetComponentData<AbilityRuntime>(owner);
                    runtime.Library=default; runtime.ProgramVersion=0;
                    manager.SetComponentData(owner,runtime);
                }
            }
            programs.ReleaseOwner(owner);
            retiredHits.Clear();
            foreach(var item in pierced) if(item.Key.owner==owner) retiredHits.Add(item.Key);
            foreach(var key in retiredHits) pierced.Remove(key);
            endedCasts.Clear();
            foreach(var item in derivedCounts) if(item.Key.owner==owner) endedCasts.Add(item.Key);
            foreach(var key in endedCasts) derivedCounts.Remove(key);
        }
        private static ScheduledSkillStage Context(in SkillFlight flight)=>new ScheduledSkillStage {
            Skill=flight.Skill,Stage=flight.Stage,Cast=flight.Cast,ProgramVersion=flight.ProgramVersion,
            Depth=flight.Depth,Level=flight.Level,RootReleasedAt=flight.RootReleasedAt,Direction=flight.Direction,Target=flight.Target };

        private static bool HasTrigger(ref SkillData skill,int parent,RecipeTrigger trigger)
        {
            if(!skill.UsesRecipe) return false;
            for(int i=0;i<skill.Stages.Length;i++)
                if(skill.Stages[i].ParentStage==parent && skill.Stages[i].Trigger==trigger) return true;
            return false;
        }
        private static void Reject(ref AbilityRuntime runtime,in ScheduledSkillStage context,int node,RecipeExecutionFailure reason)
        {
            runtime.RejectedDerivations++; runtime.LastRejectedCast=context.Cast;
            runtime.LastRejectedNode=node; runtime.LastRecipeFailure=reason;
        }
        private static bool Expired(in ScheduledSkillStage context,ref SkillData skill,float now)
            =>skill.UsesRecipe && now>context.RootReleasedAt+skill.MaxLifetime+0.0001f;

        /// <summary>事件只排队，绝不同步调用子阶段。全体查询的目标结算不受派生数量配额裁切。</summary>
        private void Emit(Entity owner,in ScheduledSkillStage context,RecipeTrigger trigger,float3 position,float3 direction,
            Entity target,ref AbilityLibrary library,float now,ref AbilityRuntime runtime)
        {
            ref var skill=ref library.Skills[context.Skill];
            if(!skill.UsesRecipe || !IsAlive(owner)) return;
            var scheduled=manager.GetBuffer<ScheduledSkillStage>(owner);
            float eventAt=context.NextAt;
            for(int i=0;i<skill.Stages.Length;i++)
            {
                ref var child=ref skill.Stages[i];
                if(child.ParentStage!=context.Stage || child.Trigger!=trigger) continue;
                RecipeExecutionFailure error=RecipeExecutionFailure.None;
                derivedCounts.TryGetValue((owner,context.Cast),out int count);
                if(context.Depth>=4) error=RecipeExecutionFailure.DepthLimit;
                else if(count+child.RepeatCount>skill.MaxDerivedExecutions) error=RecipeExecutionFailure.DerivedLimit;
                else if(eventAt+child.Delay+(child.RepeatCount-1)*child.RepeatInterval>context.RootReleasedAt+skill.MaxLifetime+0.0001f)
                    error=RecipeExecutionFailure.LifetimeLimit;
                else if(scheduled.Length>=1024) error=RecipeExecutionFailure.QueueLimit;
                if(error!=RecipeExecutionFailure.None) { Reject(ref runtime,context,child.NodeId,error); continue; }
                derivedCounts[(owner,context.Cast)]=count+child.RepeatCount;
                scheduled.Add(new ScheduledSkillStage { Skill=context.Skill,Stage=i,Cast=context.Cast,
                    ProgramVersion=context.ProgramVersion,Depth=context.Depth+1,Level=context.Level,RootReleasedAt=context.RootReleasedAt,
                    Remaining=child.RepeatCount,NextAt=eventAt+child.Delay,Origin=position,Point=position,Direction=direction,Target=target });
            }
        }
        private void PruneState(Entity owner)
        {
            var liveFlights=manager.GetBuffer<SkillFlight>(owner);
            retiredHits.Clear();
            foreach(var entry in pierced)
            {
                if(entry.Key.owner!=owner) continue;
                bool alive=false;
                for(int i=0;i<liveFlights.Length;i++) if(liveFlights[i].FlightId==entry.Key.id) {alive=true;break;}
                if(!alive) retiredHits.Add(entry.Key);
            }
            foreach(var key in retiredHits) pierced.Remove(key);
            var scheduled=manager.GetBuffer<ScheduledSkillStage>(owner); var flights=manager.GetBuffer<SkillFlight>(owner);
            programs.Prune(owner,scheduled,flights,manager.GetBuffer<SkillCue>(owner));
            activeCasts.Clear();
            for(int i=0;i<scheduled.Length;i++) activeCasts.Add(scheduled[i].Cast);
            for(int i=0;i<flights.Length;i++) activeCasts.Add(flights[i].Cast);
            endedCasts.Clear();
            foreach(var item in derivedCounts) if(item.Key.owner==owner && !activeCasts.Contains(item.Key.cast)) endedCasts.Add(item.Key);
            foreach(var key in endedCasts) derivedCounts.Remove(key);
        }
    }
}
