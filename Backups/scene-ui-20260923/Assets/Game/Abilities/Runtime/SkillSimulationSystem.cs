using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Abilities
{
    /// <summary>技能流水线调度：时间/Buff → 空间网格 → 校验提交 → 查询 → 统一效果结算。仅处理 AbilitySession。</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class SkillSimulationSystem : SystemBase
    {
        private NativeParallelMultiHashMap<int2,SpatialEntry> grid;
        private NativeList<AreaPulse> pulses;
        private NativeList<EffectRequest> effects;
        private NativeArray<int> counts;
        private EntityQuery units;
        private readonly Stopwatch stopwatch=new Stopwatch();
        protected override void OnCreate()
        {
            RequireForUpdate<AbilitySession>();
            grid=new NativeParallelMultiHashMap<int2,SpatialEntry>(32768,Allocator.Persistent);
            pulses=new NativeList<AreaPulse>(64,Allocator.Persistent);
            effects=new NativeList<EffectRequest>(4096,Allocator.Persistent);
            counts=new NativeArray<int>(2,Allocator.Persistent);
            units=GetEntityQuery(typeof(AbilityUnit),typeof(LocalTransform),typeof(ActiveBuff));
        }
        protected override void OnDestroy()
        {
            Dependency.Complete(); grid.Dispose(); pulses.Dispose(); effects.Dispose(); counts.Dispose();
        }
        protected override void OnUpdate()
        {
            stopwatch.Restart();
            Dependency.Complete();
            var sessionEntity=SystemAPI.GetSingletonEntity<AbilitySession>();
            var session=EntityManager.GetComponentData<AbilitySession>(sessionEntity);
            float dt=SystemAPI.Time.DeltaTime; session.Time+=dt;
            if(!EntityManager.Exists(session.Hero)) return;
            var heroPosition=EntityManager.GetComponentData<LocalTransform>(session.Hero).Position;
            Dependency=new BuffAndMovementJob{Library=session.Library,Now=session.Time,Delta=dt,HeroPosition=heroPosition,MoveEnemies=session.MoveEnemies}.ScheduleParallel(Dependency);
            Dependency.Complete();
            grid.Clear();
            if(grid.Capacity<units.CalculateEntityCount()) grid.Capacity=units.CalculateEntityCount()*2;
            Dependency=new BuildAbilityGridJob{Grid=grid.AsParallelWriter()}.ScheduleParallel(Dependency);
            Dependency.Complete();
            pulses.Clear(); effects.Clear();
            ProcessCasts(sessionEntity,ref session);
            ProcessAreas(sessionEntity,ref session);
            Dependency=new QueryAbilityAreasJob{Grid=grid,Pulses=pulses.AsArray(),Library=session.Library,Results=effects}.Schedule(Dependency);
            Dependency.Complete();
            ProcessBasicAttack(ref session);
            if(effects.Length>0)
            {
                Dependency=new ApplyAbilityEffectsJob{Requests=effects.AsArray(),Library=session.Library,
                    Units=GetComponentLookup<AbilityUnit>(),Buffs=GetBufferLookup<ActiveBuff>(),Now=session.Time}.Schedule(Dependency);
                Dependency.Complete();
            }
            counts[0]=counts[1]=0;
            Dependency=new AbilitySummaryJob{Counts=counts}.Schedule(Dependency); Dependency.Complete();
            session.AliveEnemies=counts[0]; session.ActiveBuffs=counts[1]; session.StepMs=stopwatch.Elapsed.TotalMilliseconds;
            EntityManager.SetComponentData(sessionEntity,session);
        }
        private void ProcessCasts(Entity entity,ref AbilitySession session)
        {
            var commands=EntityManager.GetBuffer<CastRequest>(entity);
            var areas=EntityManager.GetBuffer<ActiveArea>(entity);
            for(int i=areas.Length-1;i>=0;i--) if(session.Time>=areas[i].ExpiresAt) areas.RemoveAt(i);
            for(int i=0;i<commands.Length;i++)
            {
                var request=commands[i]; session.CastSerial++; session.LastSkill=request.Slot; session.LastFailure=CastFailure.Invalid;
                if(!EntityManager.Exists(request.Source) || !EntityManager.HasBuffer<AbilitySlot>(request.Source)) continue;
                var slots=EntityManager.GetBuffer<AbilitySlot>(request.Source);
                if(request.Slot<0 || request.Slot>=slots.Length) continue;
                var slot=slots[request.Slot]; ref var skill=ref session.Library.Value.Skills[slot.Definition];
                var unit=EntityManager.GetComponentData<AbilityUnit>(request.Source);
                var origin=EntityManager.GetComponentData<LocalTransform>(request.Source).Position;
                var point=skill.Delivery==SkillDelivery.SelfCircle?origin:AbilityGeometry.Resolve(origin,request.Point,skill.CastRange,session.ClampRange,out _);
                AbilityGeometry.Resolve(origin,request.Point,skill.CastRange,session.ClampRange,out bool valid);
                var failure=unit.Health<=0?CastFailure.Dead:slot.ReadyAt>session.Time?CastFailure.Cooldown:
                    unit.Mana<skill.ManaCost?CastFailure.Mana:!valid && skill.Delivery!=SkillDelivery.SelfCircle?CastFailure.OutOfRange:
                    skill.Delivery==SkillDelivery.PersistentCircle && areas.Length>=8?CastFailure.AreaLimit:CastFailure.None;
                session.LastFailure=failure;
                if(failure!=CastFailure.None) continue;
                // 所有校验通过才原子提交冷却和消耗；瞄准取消不会入队，也不会扣费。
                slot.ReadyAt=session.Time+skill.Cooldown; slots[request.Slot]=slot;
                unit.Mana-=skill.ManaCost; EntityManager.SetComponentData(request.Source,unit);
                float radius=AbilityGeometry.Radius(ref skill,slot.Level), power=AbilityGeometry.Power(ref skill,slot.Level);
                session.LastPoint=point;
                if(skill.Delivery==SkillDelivery.PersistentCircle)
                    areas.Add(new ActiveArea{Source=request.Source,Skill=slot.Definition,Team=unit.Team,Center=point,Radius=radius,
                        Power=power,ExpiresAt=session.Time+skill.AreaDuration,NextPulse=session.Time});
                else pulses.Add(new AreaPulse{Source=request.Source,Skill=slot.Definition,Team=unit.Team,Center=point,Radius=radius,Power=power});
            }
            commands.Clear();
        }
        private void ProcessAreas(Entity entity,ref AbilitySession session)
        {
            var areas=EntityManager.GetBuffer<ActiveArea>(entity);
            for(int i=areas.Length-1;i>=0;i--)
            {
                var area=areas[i];
                if(session.Time>=area.ExpiresAt) { areas.RemoveAt(i); continue; }
                if(session.Time<area.NextPulse) continue;
                // 区域用于刷新状态，错过的查询不反复补发，避免帧率降低时人为叠加多次进入效果。
                pulses.Add(new AreaPulse{Source=area.Source,Skill=area.Skill,Team=area.Team,Center=area.Center,Radius=area.Radius,Power=area.Power});
                area.NextPulse=session.Time+session.Library.Value.Skills[area.Skill].AreaInterval; areas[i]=area;
            }
        }
        private void ProcessBasicAttack(ref AbilitySession session)
        {
            if(!session.AutoAttack) return;
            var hero=EntityManager.GetComponentData<AbilityUnit>(session.Hero);
            if(hero.Health<=0 || hero.AttackReadyAt>session.Time) return;
            var position=EntityManager.GetComponentData<LocalTransform>(session.Hero).Position;
            var target=FindClosest(position,session.AttackRange,hero.Team,true);
            if(target==Entity.Null) return;
            hero.AttackReadyAt=session.Time+session.AttackInterval; EntityManager.SetComponentData(session.Hero,hero);
            effects.Add(new EffectRequest{Source=session.Hero,Target=target,Effect=new EffectData{Kind=EffectKind.Damage,Amount=hero.Attack},
                Power=1,Origin=-1,Order=effects.Length});
            // 只有真实普攻命中入口派发这些效果；ApplyAbilityEffectsJob 不递归发出命中事件。
            for(int i=0;i<session.Library.Value.Passives.Length;i++)
            {
                ref var passive=ref session.Library.Value.Passives[i];
                if((hero.PassiveMask&(1u<<i))==0 || passive.Trigger!=PassiveTrigger.BasicAttackHit) continue;
                for(int e=0;e<passive.Effects.Length;e++)
                    effects.Add(new EffectRequest{Source=session.Hero,Target=target,Origin=1000+i,Power=1,Effect=passive.Effects[e],Order=effects.Length});
            }
            session.AttackSerial++; session.LastAttackPoint=EntityManager.GetComponentData<LocalTransform>(target).Position;
        }
        /// <summary>完成本帧网格 Job 后的低频选择/普攻查询，距离相等时按 Entity.Index 选择。</summary>
        public Entity FindClosest(float3 point,float radius,int team,bool enemiesOnly)
        {
            Dependency.Complete(); Entity found=Entity.Null; float best=radius*radius;
            int2 lo=(int2)math.floor((point.xz-radius)/BuildAbilityGridJob.CellSize), hi=(int2)math.floor((point.xz+radius)/BuildAbilityGridJob.CellSize);
            for(int y=lo.y;y<=hi.y;y++) for(int x=lo.x;x<=hi.x;x++)
            {
                if(!grid.TryGetFirstValue(new int2(x,y),out var entry,out var iterator)) continue;
                do
                {
                    if(enemiesOnly && entry.Team==team) continue;
                    float distance=math.distancesq(point.xz,entry.Position.xz);
                    if(distance<best || (distance==best && (found==Entity.Null || entry.Entity.Index<found.Index))) { found=entry.Entity; best=distance; }
                } while(grid.TryGetNextValue(out entry,ref iterator));
            }
            return found;
        }
    }
}
