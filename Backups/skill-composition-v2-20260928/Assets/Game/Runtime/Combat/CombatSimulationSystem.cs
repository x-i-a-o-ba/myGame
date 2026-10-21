using Roguelike.Abilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>Game战斗宿主：移动/普攻/死亡计数。技能投递和Buff共用Abilities核心，不按技能身份分支。</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class CombatSimulationSystem : SystemBase
    {
        private NativeQueue<float> pendingDamage;
        private AbilityExecution abilities;
        private EntityQuery enemies, projectiles;
        protected override void OnCreate()
        {
            pendingDamage=new NativeQueue<float>(Allocator.Persistent);
            enemies=GetEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            projectiles=GetEntityQuery(typeof(CombatProjectile),typeof(LocalTransform));
            abilities=new AbilityExecution(EntityManager);
            RequireForUpdate<CombatSettings>();
            RequireForUpdate<AbilityRuntime>();
        }
        protected override void OnDestroy()
        { Dependency.Complete(); abilities.Dispose(); pendingDamage.Dispose(); }

        protected override void OnUpdate()
        {
            var manager=EntityManager;
            Entity session=SystemAPI.GetSingletonEntity<CombatSettings>();
            var config=manager.GetComponentData<CombatSettings>(session);
            var player=manager.GetComponentData<CombatPlayer>(session);
            var runtime=manager.GetComponentData<AbilityRuntime>(session);
            float dt=math.min(SystemAPI.Time.DeltaTime,0.1f);
            float now=runtime.Time+dt;
            Dependency=new TickBuffs {Library=runtime.Library,Now=now,Delta=dt}.ScheduleParallel(Dependency);
            Dependency.Complete();
            var hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0)
            {
                abilities.Step(session,now,dt);
                manager.DestroyEntity(projectiles);
                player.ActiveProjectiles=0; player.Health=0;
                manager.SetComponentData(session,player); return;
            }
            player.Elapsed+=dt;
            float3 movement=player.Destination-player.Position; movement.y=0;
            float distance=math.length(movement);
            // 动作锁归技能持有者，不写入 Buff 聚合的 Controls，避免每帧重算相互覆盖。
            if(distance>0.03f && (hero.Controls&ControlFlags.MovementBlocked)==0 && !AbilityCastTiming.IsBusy(runtime,runtime.Time))
            {
                player.Facing=movement/distance;
                player.Position+=player.Facing*math.min(distance,hero.Speed*dt);
            }
            manager.SetComponentData(session,LocalTransform.FromPosition(player.Position));
            Dependency=new PursuitJob {PlayerPosition=player.Position,DeltaTime=dt,
                AttackRange=config.EnemyAttackRange,AttackInterval=config.EnemyAttackInterval,
                AttackDamage=config.EnemyAttackDamage,Damage=pendingDamage.AsParallelWriter()}.ScheduleParallel(Dependency);
            // 此处是所有并行单位写入与串行效果写入的明确交接点。
            Dependency.Complete();
            ref var library=ref runtime.Library.Value;
            while(pendingDamage.TryDequeue(out float damage))
                abilities.Apply(Entity.Null,session,-1,new EffectData{Kind=EffectKind.Damage,Amount=damage},ref library,now);
            hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0)
            {
                abilities.Step(session,now,dt); manager.DestroyEntity(projectiles);
                player.ActiveProjectiles=0; player.Health=0; manager.SetComponentData(session,player); return;
            }
            abilities.RebuildIndex();
            using var destroy=new EntityCommandBuffer(Allocator.Temp);
            using(var bullets=projectiles.ToEntityArray(Allocator.Temp))
                for(int i=0;i<bullets.Length;i++)
                {
                    Entity bullet=bullets[i]; var projectile=manager.GetComponentData<CombatProjectile>(bullet);
                    var transform=manager.GetComponentData<LocalTransform>(bullet);
                    projectile.Lifetime-=dt;
                    if(projectile.Lifetime<=0 || !manager.Exists(projectile.Target) || !manager.HasComponent<AbilityUnit>(projectile.Target) ||
                        manager.GetComponentData<AbilityUnit>(projectile.Target).Health<=0)
                    { destroy.DestroyEntity(bullet); continue; }
                    var target=manager.GetComponentData<LocalTransform>(projectile.Target);
                    float3 offset=target.Position-transform.Position; float remaining=math.length(offset);
                    if(remaining<=projectile.Speed*dt+0.35f)
                    {
                        abilities.Apply(session,projectile.Target,-1,new EffectData{Kind=EffectKind.Damage,Amount=projectile.Damage},ref library,now);
                        abilities.RecordBasicAttackHit(session); player.Hits++;
                        destroy.DestroyEntity(bullet);
                    }
                    else
                    {
                        transform.Position+=offset/remaining*projectile.Speed*dt;
                        manager.SetComponentData(bullet,transform); manager.SetComponentData(bullet,projectile);
                    }
                }
            abilities.Step(session,now,dt);
            hero=manager.GetComponentData<AbilityUnit>(session);
            runtime=manager.GetComponentData<AbilityRuntime>(session);
            player.FireCooldown-=dt;
            if(hero.Health>0 && player.FireCooldown<=0 && (hero.Controls&ControlFlags.AttackBlocked)==0 && !AbilityCastTiming.IsBusy(runtime,now))
            {
                Entity nearest=Entity.Null; float closest=config.AttackRange*config.AttackRange;
                using var units=enemies.ToEntityArray(Allocator.Temp);
                for(int i=0;i<units.Length;i++)
                {
                    var enemy=units[i]; if(manager.GetComponentData<AbilityUnit>(enemy).Health<=0) continue;
                    float squared=math.distancesq(player.Position,manager.GetComponentData<LocalTransform>(enemy).Position);
                    if(squared<closest) { closest=squared; nearest=enemy; }
                }
                player.FireCooldown=config.AttackInterval/math.max(0.01f,hero.AttackSpeedMultiplier);
                if(nearest!=Entity.Null)
                {
                    Entity bullet=destroy.Instantiate(config.ProjectilePrefab);
                    destroy.SetComponent(bullet,new CombatProjectile {Target=nearest,Damage=hero.Attack,Speed=config.ProjectileSpeed,Lifetime=5});
                    destroy.SetComponent(bullet,LocalTransform.FromPositionRotationScale(player.Position+new float3(0,0.5f,0),quaternion.identity,0.27f));
                    player.ShotsFired++;
                }
            }
            using(var units=enemies.ToEntityArray(Allocator.Temp))
                for(int i=0;i<units.Length;i++)
                    if(manager.GetComponentData<AbilityUnit>(units[i]).Health<=0)
                    { destroy.DestroyEntity(units[i]); player.Kills++; }
            destroy.Playback(manager);
            player.AliveEnemies=enemies.CalculateEntityCount();
            player.ActiveProjectiles=projectiles.CalculateEntityCount()+manager.GetBuffer<SkillFlight>(session).Length;
            player.Health=hero.Health; player.MaximumHealth=hero.MaxHealth;
            manager.SetComponentData(session,player);
        }
        [BurstCompile, WithAll(typeof(CombatOwned))]
        private partial struct TickBuffs : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<AbilityLibrary> Library;
            public float Now,Delta;
            private void Execute(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs)
            { AbilityEffects.Tick(ref unit,buffs,ref Library.Value,Now,Delta); }
        }
        [BurstCompile]
        private partial struct PursuitJob : IJobEntity
        {
            public float3 PlayerPosition;
            public float DeltaTime,AttackRange,AttackInterval,AttackDamage;
            public NativeQueue<float>.ParallelWriter Damage;
            private void Execute(ref LocalTransform transform,ref CombatEnemy enemy,in AbilityUnit unit)
            {
                if(unit.Health<=0) return;
                float3 offset=PlayerPosition-transform.Position; offset.y=0;
                float distance=math.length(offset);
                if(distance>AttackRange && (unit.Controls&ControlFlags.MovementBlocked)==0)
                    transform.Position+=math.normalizesafe(offset)*math.min(distance-AttackRange,unit.Speed*DeltaTime);
                if((unit.Controls&ControlFlags.AttackBlocked)!=0) return;
                enemy.AttackCooldown-=DeltaTime;
                if(distance<=AttackRange+0.05f && enemy.AttackCooldown<=0)
                { Damage.Enqueue(unit.Attack); enemy.AttackCooldown=AttackInterval/math.max(0.01f,unit.AttackSpeedMultiplier); }
            }
        }
    }
}
