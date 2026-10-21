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
    public sealed class AbilityExecution : IDisposable
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
            ref var library=ref runtime.Library.Value;
            var unit=manager.GetComponentData<AbilityUnit>(owner);
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i]; ref var skill=ref library.Skills[slot.Definition];
                while(slot.Charges<skill.MaxCharges && slot.RechargeAt<=now)
                {
                    slot.Charges++;
                    slot.RechargeAt+=math.max(0.001f,skill.RechargeSeconds);
                }
                slots[i]=slot;
            }
            var commands=manager.GetBuffer<SkillCommand>(owner);
            if(unit.Health<=0)
            {
                commands.Clear(); manager.GetBuffer<ScheduledSkillStage>(owner).Clear();
                manager.GetBuffer<SkillFlight>(owner).Clear();
                manager.SetComponentData(owner,runtime); return;
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
                var pending=scheduled[i]; ref var stage=ref library.Skills[pending.Skill].Stages[pending.Stage];
                BeginStage(owner,pending,ref library,now);
                pending.Remaining--; pending.NextAt+=stage.RepeatInterval;
                if(pending.Remaining==0) scheduled.RemoveAt(i);
                else scheduled[i]=pending;
            }
            var flights=manager.GetBuffer<SkillFlight>(owner);
            if(!IsAlive(owner)) flights.Clear();
            for(int i=0;i<flights.Length;)
            {
                if(!IsAlive(owner)) { flights.Clear(); break; }
                var flight=flights[i]; ref var stage=ref library.Skills[flight.Skill].Stages[flight.Stage];
                float distance=stage.Delivery==DeliveryKind.PointProjectile
                    ?math.distance(flight.Position,flight.Destination):flight.RemainingRange;
                float step=math.min(distance,stage.Speed*dt);
                float3 end=flight.Position+flight.Direction*step;
                bool finished=false;
                if(stage.Delivery==DeliveryKind.LinearProjectile)
                {
                    Entity hit=FirstOnSegment(owner,flight.Position,end,stage.HitRadius,stage.Targets,out float fraction);
                    if(hit!=Entity.Null)
                    {
                        end=math.lerp(flight.Position,end,fraction);
                        ApplyList(owner,hit,flight.Skill,ref stage.ImpactEffects,ref library,now);
                        Resolve(owner,flight.Skill,flight.Stage,end,flight.Direction,hit,ref library,now);
                        finished=true;
                    }
                }
                else if(distance<=step+0.0001f)
                {
                    Resolve(owner,flight.Skill,flight.Stage,flight.Destination,flight.Direction,flight.Target,ref library,now);
                    finished=true;
                }
                flight.Position=end; flight.RemainingRange-=step;
                if(finished || flight.RemainingRange<=0.0001f) flights.RemoveAt(i);
                else { flights[i]=flight; i++; }
            }
            manager.SetComponentData(owner,runtime);
        }
        private void Submit(Entity owner,SkillCommand command,ref AbilityRuntime runtime)
        {
            runtime.LastFailure=CastFailure.Invalid;
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            if(command.Slot<0 || command.Slot>=slots.Length) return;
            var slot=slots[command.Slot]; ref var skill=ref runtime.Library.Value.Skills[slot.Definition];
            var unit=manager.GetComponentData<AbilityUnit>(owner);
            if(unit.Health<=0) { runtime.LastFailure=CastFailure.Dead; return; }
            if((unit.Controls&ControlFlags.CastBlocked)!=0) return;
            if(slot.ReadyAt>runtime.Time || slot.Charges<=0) { runtime.LastFailure=CastFailure.Cooldown; return; }
            if(slot.HitStacks<skill.RequiredHits) return;
            if(unit.Mana<skill.ManaCost) { runtime.LastFailure=CastFailure.Mana; return; }
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
            // 所有校验通过后才提交，资源与冷却只支付一次。
            unit.Mana-=skill.ManaCost;
            slot.ReadyAt=runtime.Time+skill.Cooldown;
            if(slot.Charges==skill.MaxCharges) slot.RechargeAt=runtime.Time+skill.RechargeSeconds;
            slot.Charges--; slot.HitStacks=0;
            slots[command.Slot]=slot; manager.SetComponentData(owner,unit);
            runtime.CastSerial++; runtime.LastFailure=CastFailure.None;
            for(int i=0;i<skill.Stages.Length;i++)
            {
                ref var stage=ref skill.Stages[i];
                stages.Add(new ScheduledSkillStage {Skill=slot.Definition,Stage=i,Remaining=stage.RepeatCount,
                    NextAt=runtime.Time+stage.Delay,Cast=runtime.CastSerial,Origin=origin,Point=point,Direction=direction,Target=command.Target});
            }
        }
        private void BeginStage(Entity owner,ScheduledSkillStage pending,ref AbilityLibrary library,float now)
        {
            ref var skill=ref library.Skills[pending.Skill]; ref var stage=ref skill.Stages[pending.Stage];
            float3 center=skill.Aim==AimMode.Point || skill.Aim==AimMode.Unit ?pending.Point:pending.Origin;
            if(stage.Delivery==DeliveryKind.Instant)
            { Resolve(owner,pending.Skill,pending.Stage,center,pending.Direction,pending.Target,ref library,now); return; }
            float3 destination=stage.Delivery==DeliveryKind.PointProjectile?pending.Point:pending.Origin+pending.Direction*stage.Range;
            float distance=math.distance(pending.Origin,destination);
            if(distance<0.0001f)
            { Resolve(owner,pending.Skill,pending.Stage,destination,pending.Direction,pending.Target,ref library,now); return; }
            manager.GetBuffer<SkillFlight>(owner).Add(new SkillFlight {Skill=pending.Skill,Stage=pending.Stage,Cast=pending.Cast,
                Target=pending.Target,Position=pending.Origin,Destination=destination,Direction=(destination-pending.Origin)/distance,RemainingRange=distance});
        }
        private void Resolve(Entity owner,int skillIndex,int stageIndex,float3 center,float3 direction,Entity selected,ref AbilityLibrary library,float now)
        {
            if(!IsAlive(owner)) return;
            ref var stage=ref library.Skills[skillIndex].Stages[stageIndex];
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
                { Apply(owner,owner,skillIndex,effect,ref library,now); continue; }
                for(int i=0;i<candidates.Length;i++)
                {
                    Entity target=candidates[i];
                    if(!IsAlive(target)) continue;
                    if(stage.Scope!=TargetScope.Self && !Matches(owner,target,stage.Targets)) continue;
                    if(stage.Scope==TargetScope.Area && !SkillTargeting.Contains(ref stage,center,direction,manager.GetComponentData<LocalTransform>(target).Position)) continue;
                    Apply(owner,target,skillIndex,effect,ref library,now);
                }
            }
            manager.GetBuffer<SkillCue>(owner).Add(new SkillCue{Skill=skillIndex,Stage=stageIndex,Position=center,Direction=direction});
        }
        private void ApplyList(Entity owner,Entity selected,int origin,ref BlobArray<EffectData> effects,ref AbilityLibrary library,float now,bool includeSource=true)
        {
            for(int e=0;e<effects.Length && IsAlive(owner);e++)
            {
                var effect=effects[e]; if(!includeSource && effect.Target==EffectTarget.Source) continue;
                Apply(owner,effect.Target==EffectTarget.Source?owner:selected,origin,effect,ref library,now);
            }
        }
        public void Apply(Entity source,Entity target,int origin,EffectData effect,ref AbilityLibrary library,float now)
        {
            if(!IsAlive(target)) return;
            var unit=manager.GetComponentData<AbilityUnit>(target);
            AbilityEffects.Apply(ref unit,manager.GetBuffer<ActiveBuff>(target),ref library,
                new EffectRequest{Source=source,Target=target,Origin=origin,Effect=effect,Power=1},now);
            manager.SetComponentData(target,unit);
        }
        public void RecordBasicAttackHit(Entity owner)
        {
            var slots=manager.GetBuffer<AbilitySlot>(owner);
            var runtime=manager.GetComponentData<AbilityRuntime>(owner);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i]; int maximum=runtime.Library.Value.Skills[slot.Definition].RequiredHits;
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
        public void Dispose() { grid.Dispose(); candidates.Dispose(); targets.Dispose(); }
    }
}

