using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 通用施法提交、有限阶段调度与投递。宿主在移动 Job 完成后串行调用，独占属性/Buff写权限。
    /// 不解释英雄、键位或技能名称。空间索引与临时列表由此对象创建和释放。
    /// </summary>
    public sealed partial class AbilityExecution : IDisposable
    {
        private const float CellSize=8;
        private NativeParallelMultiHashMap<int2,Entity> grid;
        private NativeList<Entity> candidates;
        private readonly EntityQuery targets;
        private readonly EntityManager manager;

        public AbilityExecution(EntityManager manager)
        {
            this.manager=manager;
            targets=manager.CreateEntityQuery(typeof(AbilityUnit),typeof(LocalTransform));
            grid=new NativeParallelMultiHashMap<int2,Entity>(128,Allocator.Persistent);
            candidates=new NativeList<Entity>(128,Allocator.Persistent);
        }
        public void RebuildIndex()
        {
            grid.Clear();
            using var entities=targets.ToEntityArray(Allocator.Temp);
            if(grid.Capacity<entities.Length) grid.Capacity=math.max(128,entities.Length);
            for(int i=0;i<entities.Length;i++)
            {
                var entity=entities[i];
                if(manager.GetComponentData<AbilityUnit>(entity).Health<=0) continue;
                float3 point=manager.GetComponentData<LocalTransform>(entity).Position;
                grid.Add((int2)math.floor(point.xz/CellSize),entity);
            }
        }
        public void Step(Entity owner,float now,float dt)
        {
            var runtime=manager.GetComponentData<AbilityRuntime>(owner);
            runtime.Time=now;
            runtime.ProgramVersion=programs.Ensure(owner,runtime.Library);
            ref var library=ref runtime.Library.Value;
            var unit=manager.GetComponentData<AbilityUnit>(owner);
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i]; ref var skill=ref library.Skills[slot.Definition];
                // 充能恢复使用当前等级与冷却缩减；已排定的下一次恢复时间不回溯修改。
                float recharge=math.max(0.001f,skill.RechargeSeconds*SkillLevels.CooldownScale(ref skill,slot.Level,unit));
                while(slot.Charges<skill.MaxCharges && slot.RechargeAt<=now)
                {
                    slot.Charges++;
                    slot.RechargeAt+=recharge;
                }
                slots[i]=slot;
            }
            var commands=manager.GetBuffer<SkillCommand>(owner);
            if(unit.Health<=0)
            {
                commands.Clear(); manager.GetBuffer<ScheduledSkillStage>(owner).Clear();
                manager.GetBuffer<SkillFlight>(owner).Clear();
                ClearCastTiming(ref runtime,now); runtime.LastFailure=CastFailure.Dead;
                manager.SetComponentData(owner,runtime); PruneState(owner); return;
            }
            // 在处理新命令前推进动作。上一帧尚未释放且本帧已经禁止施法，控制先于释放生效。
            // 没有控制事件的亚帧时间戳时，跨越释放点也采用此固定优先级。
            if(runtime.AwaitingRelease)
            {
                if((unit.Controls&ControlFlags.CastBlocked)!=0)
                {
                    var waiting=manager.GetBuffer<ScheduledSkillStage>(owner);
                    for(int i=waiting.Length-1;i>=0;i--)
                        if(waiting[i].Cast==runtime.CastSerial) waiting.RemoveAt(i);
                    ClearCastTiming(ref runtime,now); runtime.LastFailure=CastFailure.Interrupted;
                }
                else if(now>=runtime.ReleaseAt) runtime.AwaitingRelease=false;
            }
            for(int i=0;i<commands.Length;i++)
                Submit(owner,commands[i],ref runtime);
            commands.Clear();
            var scheduled=manager.GetBuffer<ScheduledSkillStage>(owner);
            // 跨阶段按到期时间排序；时间相同保留提交与阶段次序。补发有限次重复不丢段。
            while(true)
            {
                if(!IsAlive(owner)) { scheduled.Clear(); break; }
                int i=-1; float earliest=float.MaxValue;
                for(int s=0;s<scheduled.Length;s++)
                    if(scheduled[s].NextAt<=now && scheduled[s].NextAt<earliest) { i=s; earliest=scheduled[s].NextAt; }
                if(i<0) break;
                var pending=scheduled[i];
                var version=programs.Get(owner,pending.ProgramVersion);
                ref var plan=ref version.Value;
                ref var skill=ref plan.Skills[pending.Skill]; ref var stage=ref skill.Stages[pending.Stage];
                // 子阶段可扩充同一缓冲；必须先提交当前排期的消耗，再调用阶段，防止索引/扩容失效。
                if(Expired(pending,ref skill,pending.NextAt))
                { Reject(ref runtime,pending,stage.NodeId,RecipeExecutionFailure.LifetimeLimit); scheduled.RemoveAt(i); continue; }
                var next=pending; next.Remaining--; next.NextAt+=stage.RepeatInterval;
                if(next.Remaining==0) scheduled.RemoveAt(i); else scheduled[i]=next;
                BeginStage(owner,pending,ref plan,now,ref runtime);
            }
            var flights=manager.GetBuffer<SkillFlight>(owner);
            if(!IsAlive(owner)) flights.Clear();
            for(int i=0;i<flights.Length;)
            {
                if(!IsAlive(owner)) { flights.Clear(); break; }
                var flight=flights[i]; var context=Context(flight);
                var version=programs.Get(owner,flight.ProgramVersion); ref var plan=ref version.Value;
                ref var flightSkill=ref plan.Skills[flight.Skill]; ref var stage=ref flightSkill.Stages[flight.Stage];
                // 用原定完成时间检查寿命。Step 可能跨过到期点，已经接受的到达不能仅因晚处理一帧而丢失。
                context.NextAt=stage.Delivery==DeliveryKind.ParabolicProjectile ? flight.StartedAt+stage.FlightSeconds :
                    flight.StartedAt+flight.Elapsed+flight.RemainingRange/math.max(.001f,stage.Speed);
                if(Expired(context,ref flightSkill,context.NextAt))
                { Reject(ref runtime,context,stage.NodeId,RecipeExecutionFailure.LifetimeLimit); flights.RemoveAt(i); continue; }
                if(stage.Delivery==DeliveryKind.ParabolicProjectile)
                {
                    // 使用模拟时钟与原定发射时间，延迟/重复跨越大 dt 时各自补发，不会额外多飞一帧。
                    flight.Elapsed=math.min(stage.FlightSeconds,math.max(flight.Elapsed,math.max(0,now-flight.StartedAt)));
                    flight.Position=SkillTrajectory.Evaluate(flight.Start,flight.Destination,stage.ArcHeight,flight.Elapsed/stage.FlightSeconds);
                    if(flight.Elapsed>=stage.FlightSeconds)
                    {
                        Resolve(owner,context,flight.Destination,flight.Direction,flight.Target,ref plan,now,ref runtime);
                        flights.RemoveAt(i);
                    }
                    else { flights[i]=flight; i++; }
                    continue;
                }
                float distance=stage.Delivery==DeliveryKind.PointProjectile
                    ?math.distance(flight.Position,flight.Destination):flight.RemainingRange;
                // 新发射弹体只推进释放后的时间，不能把本帧前摇消耗的 dt 算作飞行。
                float elapsed=math.max(0,now-flight.StartedAt);
                float flightDt=math.max(0,elapsed-flight.Elapsed);
                flight.Elapsed=elapsed;
                float step=math.min(distance,stage.Speed*flightDt);
                float3 end=flight.Position+flight.Direction*step;
                bool finished=false;
                if(stage.Delivery==DeliveryKind.LinearProjectile)
                {
                    Entity hit=FirstOnSegment(owner,flight.Position,end,stage.HitRadius,stage.Targets,out float fraction);
                    if(hit!=Entity.Null)
                    {
                        context.NextAt=flight.StartedAt+elapsed-flightDt+step*fraction/math.max(.001f,stage.Speed);
                        end=math.lerp(flight.Position,end,fraction);
                        Emit(owner,context,RecipeTrigger.OnProjectileHit,end,flight.Direction,hit,ref plan,now,ref runtime);
                        ApplyList(owner,hit,context,ref stage.ImpactEffects,ref plan,now,ref runtime);
                        Resolve(owner,context,end,flight.Direction,hit,ref plan,now,ref runtime);
                        finished=true;
                    }
                }
                else if(distance<=step+0.0001f)
                {
                    Resolve(owner,context,flight.Destination,flight.Direction,flight.Target,ref plan,now,ref runtime);
                    finished=true;
                }
                flight.Position=end; flight.RemainingRange-=step;
                if(finished || flight.RemainingRange<=0.0001f) flights.RemoveAt(i);
                else { flights[i]=flight; i++; }
            }
            if(!IsAlive(owner)) { ClearCastTiming(ref runtime,now); runtime.LastFailure=CastFailure.Dead; }
            manager.SetComponentData(owner,runtime);
            PruneState(owner);
        }
        private static void ClearCastTiming(ref AbilityRuntime runtime,float now)
        {
            runtime.ReleaseAt=now; runtime.ActionReadyAt=now;
            runtime.AwaitingRelease=false; runtime.CastSlot=-1;
        }
        private void Submit(Entity owner,SkillCommand command,ref AbilityRuntime runtime)
        {
            runtime.LastFailure=CastFailure.Invalid;
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            if(command.Slot<0 || command.Slot>=slots.Length) return;
            var slot=slots[command.Slot]; ref var skill=ref runtime.Library.Value.Skills[slot.Definition];
            var unit=manager.GetComponentData<AbilityUnit>(owner);
            if(unit.Health<=0) { runtime.LastFailure=CastFailure.Dead; return; }
            if(slot.Level<=0) { runtime.LastFailure=CastFailure.NotLearned; return; }
            if(AbilityCastTiming.IsBusy(runtime,runtime.Time)) { runtime.LastFailure=CastFailure.Busy; return; }
            if((unit.Controls&ControlFlags.CastBlocked)!=0) return;
            if(slot.ReadyAt>runtime.Time || slot.Charges<=0) { runtime.LastFailure=CastFailure.Cooldown; return; }
            if(slot.HitStacks<skill.RequiredHits) return;
            float manaCost=SkillLevels.ManaCost(ref skill,slot.Level);
            float cooldownScale=SkillLevels.CooldownScale(ref skill,slot.Level,unit);
            if(unit.Mana<manaCost) { runtime.LastFailure=CastFailure.Mana; return; }
            if(!math.all(math.isfinite(command.Point)) || !math.all(math.isfinite(command.Direction))) return;
            var origin=manager.GetComponentData<LocalTransform>(owner).Position;
            var point=SkillTargeting.ClampPoint(origin,command.Point,skill.CastRange);
            var direction=math.normalizesafe(command.Direction,new float3(0,0,1));
            if(skill.Aim==AimMode.Self) point=origin;
            if(skill.Aim==AimMode.Unit)
            {
                if(!IsAlive(command.Target) || math.distance(origin,manager.GetComponentData<LocalTransform>(command.Target).Position)>skill.CastRange)
                    return;
                point=manager.GetComponentData<LocalTransform>(command.Target).Position;
            }
            var stages=manager.GetBuffer<ScheduledSkillStage>(owner);
            if(stages.Length+skill.Stages.Length>1024) { runtime.LastFailure=CastFailure.AreaLimit; return; }
            float releaseAt=runtime.Time+skill.CastTime;
            float readyAt=releaseAt+skill.RecoveryTime;
            if(!math.isfinite(releaseAt) || !math.isfinite(readyAt)) return;
            // 所有校验通过后才提交，资源与冷却只支付一次。
            unit.Mana-=manaCost;
            slot.ReadyAt=runtime.Time+skill.Cooldown*cooldownScale;
            if(slot.Charges==skill.MaxCharges) slot.RechargeAt=runtime.Time+skill.RechargeSeconds*cooldownScale;
            slot.Charges--; slot.HitStacks=0;
            slots[command.Slot]=slot; manager.SetComponentData(owner,unit);
            runtime.CastSerial++; runtime.LastFailure=CastFailure.None;
            runtime.ReleaseAt=releaseAt; runtime.ActionReadyAt=readyAt;
            runtime.CastSlot=command.Slot; runtime.AwaitingRelease=skill.CastTime>0;
            for(int i=0;i<skill.Stages.Length;i++)
            {
                ref var stage=ref skill.Stages[i];
                if(skill.UsesRecipe && stage.ParentStage>=0) continue;
                stages.Add(new ScheduledSkillStage {Skill=slot.Definition,Stage=i,Remaining=stage.RepeatCount,
                    NextAt=releaseAt+stage.Delay,Cast=runtime.CastSerial,ProgramVersion=runtime.ProgramVersion,RootReleasedAt=releaseAt,Level=slot.Level,
                    Origin=origin,Point=point,Direction=direction,Target=command.Target});
            }
        }
        private void BeginStage(Entity owner,ScheduledSkillStage pending,ref AbilityLibrary library,float now,ref AbilityRuntime runtime)
        {
            ref var skill=ref library.Skills[pending.Skill]; ref var stage=ref skill.Stages[pending.Stage];
            float3 center=skill.Aim==AimMode.Point || skill.Aim==AimMode.Unit ?pending.Point:pending.Origin;
            Emit(owner,pending,RecipeTrigger.OnRelease,pending.Origin,pending.Direction,pending.Target,ref library,now,ref runtime);
            if(stage.Delivery==DeliveryKind.Instant)
            { Resolve(owner,pending,center,pending.Direction,pending.Target,ref library,now,ref runtime); return; }
            if(stage.Delivery==DeliveryKind.ParabolicProjectile)
            {
                float3 start=pending.Origin+new float3(0,stage.LaunchHeight,0);
                float3 end=pending.Point+new float3(0,stage.ArrivalHeight,0);
                float3 planar=pending.Point-pending.Origin; planar.y=0;
                float3 fallback=pending.Direction; fallback.y=0;
                manager.GetBuffer<SkillFlight>(owner).Add(new SkillFlight {Skill=pending.Skill,Stage=pending.Stage,Cast=pending.Cast,
                    ProgramVersion=pending.ProgramVersion,Depth=pending.Depth,Level=pending.Level,RootReleasedAt=pending.RootReleasedAt,
                    Target=pending.Target,Start=start,Position=start,Destination=end,
                    Direction=math.normalizesafe(planar,math.normalizesafe(fallback,new float3(0,0,1))),StartedAt=pending.NextAt,Elapsed=0});
                return;
            }
            float3 destination=stage.Delivery==DeliveryKind.PointProjectile?pending.Point:pending.Origin+pending.Direction*stage.Range;
            float distance=math.distance(pending.Origin,destination);
            if(distance<0.0001f)
            { Resolve(owner,pending,destination,pending.Direction,pending.Target,ref library,now,ref runtime); return; }
            manager.GetBuffer<SkillFlight>(owner).Add(new SkillFlight {Skill=pending.Skill,Stage=pending.Stage,Cast=pending.Cast,
                ProgramVersion=pending.ProgramVersion,Depth=pending.Depth,Level=pending.Level,RootReleasedAt=pending.RootReleasedAt,
                Target=pending.Target,Position=pending.Origin,Destination=destination,Direction=(destination-pending.Origin)/distance,RemainingRange=distance,
                StartedAt=pending.NextAt,Elapsed=0});
        }
        private void Resolve(Entity owner,in ScheduledSkillStage context,float3 center,float3 direction,Entity selected,
            ref AbilityLibrary library,float now,ref AbilityRuntime runtime)
        {
            if(!IsAlive(owner)) return;
            ref var skill=ref library.Skills[context.Skill]; ref var stage=ref skill.Stages[context.Stage];
            bool damageEvents=HasTrigger(ref skill,context.Stage,RecipeTrigger.OnDamage);
            bool killEvents=HasTrigger(ref skill,context.Stage,RecipeTrigger.OnKill);
            if(stage.Scope==TargetScope.Self) { candidates.Clear(); candidates.Add(owner); }
            else if(stage.Scope==TargetScope.Single)
            { candidates.Clear(); if(IsAlive(selected) && Matches(owner,selected,stage.Targets)) candidates.Add(selected); }
            else
            {
                Collect(center,stage.Shape==AreaShape.Circle?stage.Radius:stage.Range+stage.Radius,stage.Scope==TargetScope.All);
            }
            // 效果按配置顺序执行；Source效果仅一次，Selected效果按稳定目标顺序分发。
            for(int e=0;e<stage.Effects.Length && IsAlive(owner);e++)
            {
                var effect=stage.Effects[e];
                if(effect.Target==EffectTarget.Source)
                { ApplyWithEvents(owner,owner,context,effect,damageEvents,killEvents,ref library,now,ref runtime); continue; }
                for(int i=0;i<candidates.Length;i++)
                {
                    Entity target=candidates[i];
                    if(!IsAlive(target)) continue;
                    if(stage.Scope!=TargetScope.Self && !Matches(owner,target,stage.Targets)) continue;
                    if(stage.Scope==TargetScope.Area && !SkillTargeting.Contains(ref stage,center,direction,manager.GetComponentData<LocalTransform>(target).Position)) continue;
                    ApplyWithEvents(owner,target,context,effect,damageEvents,killEvents,ref library,now,ref runtime);
                }
            }
            Emit(owner,context,RecipeTrigger.OnResolved,center,direction,selected,ref library,now,ref runtime);
            manager.GetBuffer<SkillCue>(owner).Add(new SkillCue{Skill=context.Skill,Stage=context.Stage,ProgramVersion=context.ProgramVersion,Position=center,Direction=direction});
        }
        private void ApplyList(Entity owner,Entity selected,in ScheduledSkillStage context,ref BlobArray<EffectData> effects,
            ref AbilityLibrary library,float now,ref AbilityRuntime runtime)
        {
            ref var skill=ref library.Skills[context.Skill];
            bool damageEvents=HasTrigger(ref skill,context.Stage,RecipeTrigger.OnDamage);
            bool killEvents=HasTrigger(ref skill,context.Stage,RecipeTrigger.OnKill);
            for(int e=0;e<effects.Length && IsAlive(owner);e++)
            {
                var effect=effects[e];
                ApplyWithEvents(owner,effect.Target==EffectTarget.Source?owner:selected,context,effect,damageEvents,killEvents,ref library,now,ref runtime);
            }
        }
        private void ApplyWithEvents(Entity source,Entity target,in ScheduledSkillStage context,EffectData effect,
            bool damageEvents,bool killEvents,ref AbilityLibrary library,float now,ref AbilityRuntime runtime)
        {
            // 没有监听分支时不采集每个目标的位置，也不进入派生扫描。
            float3 position=default;
            if((damageEvents || killEvents) && IsAlive(target)) position=manager.GetComponentData<LocalTransform>(target).Position;
            var outcome=Apply(source,target,context.Skill,effect,ref library,now,SkillPower(source,ref library.Skills[context.Skill],context.Level,effect));
            if(damageEvents && outcome.ActualDamage>0)
                Emit(source,context,RecipeTrigger.OnDamage,position,context.Direction,target,ref library,now,ref runtime);
            if(killEvents && outcome.Killed)
                Emit(source,context,RecipeTrigger.OnKill,position,context.Direction,target,ref library,now,ref runtime);
        }
        /// <summary>技能等级与来源技能增强换算成效果强度：伤害乘等级倍率×(1+技能增强)，治疗只乘等级倍率，Buff 乘 Buff 倍率。</summary>
        private float SkillPower(Entity source,ref SkillData skill,int level,in EffectData effect)
        {
            var row=SkillLevels.Row(ref skill,level);
            switch(effect.Kind)
            {
                case EffectKind.Damage:
                    float amplify=manager.Exists(source) && manager.HasComponent<AbilityUnit>(source)?manager.GetComponentData<AbilityUnit>(source).SkillAmplify:0;
                    return row.EffectScale*(1+math.max(0,amplify));
                case EffectKind.Heal: return row.EffectScale;
                case EffectKind.ApplyBuff: return row.BuffScale;
                default: return 1;
            }
        }
        /// <summary>普攻、敌人攻击等非技能来源使用强度 1。</summary>
        public EffectOutcome Apply(Entity source,Entity target,int origin,EffectData effect,ref AbilityLibrary library,float now)
            =>Apply(source,target,origin,effect,ref library,now,1);
        public EffectOutcome Apply(Entity source,Entity target,int origin,EffectData effect,ref AbilityLibrary library,float now,float power)
        {
            if(!IsAlive(target)) return default;
            var unit=manager.GetComponentData<AbilityUnit>(target);
            var outcome=AbilityEffects.Apply(ref unit,manager.GetBuffer<ActiveBuff>(target),ref library,
                new EffectRequest{Source=source,Target=target,Origin=origin,Effect=effect,Power=math.max(0,power)},now);
            manager.SetComponentData(target,unit);
            return outcome;
        }
        public void RecordBasicAttackHit(Entity owner)
        {
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            var runtime=manager.GetComponentData<AbilityRuntime>(owner);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i]; if(slot.Level<=0) continue; // 未学习的技能不积累普攻层数。
                int maximum=runtime.Library.Value.Skills[slot.Definition].RequiredHits;
                slot.HitStacks=math.min(maximum,slot.HitStacks+1); slots[i]=slot;
            }
        }
        private Entity FirstOnSegment(Entity owner,float3 start,float3 end,float radius,TargetRelation relation,out float fraction)
        {
            Collect((start+end)*0.5f,math.distance(start,end)*0.5f+radius,false);
            fraction=float.MaxValue; Entity hit=Entity.Null;
            for(int i=0;i<candidates.Length;i++)
            {
                Entity target=candidates[i]; if(target==owner || !IsAlive(target) || !Matches(owner,target,relation)) continue;
                if(SkillTargeting.FirstContact(start,end,manager.GetComponentData<LocalTransform>(target).Position,radius,out float along) && along<fraction)
                { fraction=along; hit=target; }
            }
            return hit;
        }
        private bool IsAlive(Entity entity)=>manager.Exists(entity) && manager.HasComponent<AbilityUnit>(entity) &&
            manager.HasComponent<LocalTransform>(entity) && manager.GetComponentData<AbilityUnit>(entity).Health>0;
        private bool Matches(Entity owner,Entity target,TargetRelation relation)
        {
            bool same=manager.GetComponentData<AbilityUnit>(owner).Team==manager.GetComponentData<AbilityUnit>(target).Team;
            return relation==TargetRelation.Allies?same:!same;
        }
        private void Collect(float3 center,float radius,bool all)
        {
            candidates.Clear();
            if(all)
            {
                using var entities=targets.ToEntityArray(Allocator.Temp);
                candidates.AddRange(entities);
            }
            else
            {
                int2 lo=(int2)math.floor((center.xz-radius)/CellSize), hi=(int2)math.floor((center.xz+radius)/CellSize);
                for(int z=lo.y;z<=hi.y;z++) for(int x=lo.x;x<=hi.x;x++)
                {
                    if(!grid.TryGetFirstValue(new int2(x,z),out Entity entity,out var iterator)) continue;
                    do { candidates.Add(entity); } while(grid.TryGetNextValue(out entity,ref iterator));
                }
            }
            candidates.AsArray().Sort(new EntityOrder());
        }
        private struct EntityOrder : IComparer<Entity>
        { public int Compare(Entity x,Entity y) { int c=x.Index.CompareTo(y.Index); return c!=0?c:x.Version.CompareTo(y.Version); } }
        public void Dispose() { programs.Dispose(); derivedCounts.Clear(); grid.Dispose(); candidates.Dispose(); targets.Dispose(); }
    }
}

