using Roguelike.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    // O(enemies) pursuit each frame and O(enemies) targeting only when the player fires.
    // No per-enemy Rigidbody, NavMeshAgent, or managed Update.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct CombatSimulationSystem : ISystem
    {
        private NativeQueue<float> pendingDamage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            pendingDamage = new NativeQueue<float>(Allocator.Persistent);
            state.RequireForUpdate<CombatSettings>();
            state.RequireForUpdate<CombatPlayer>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (pendingDamage.IsCreated) pendingDamage.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<CombatSettings>();
            var player = SystemAPI.GetSingleton<CombatPlayer>();
            if (player.Health <= 0) return;
            float dt = math.min(SystemAPI.Time.DeltaTime, 0.1f);
            player.Elapsed += dt;

            float3 movement = player.Destination - player.Position;
            movement.y = 0;
            float distance = math.length(movement);
            if (distance > 0.03f)
            {
                player.Facing = movement / distance;
                player.Position += player.Facing * math.min(distance, config.PlayerSpeed * dt);
            }

            state.Dependency = new PursuitJob
            {
                PlayerPosition = player.Position,
                DeltaTime = dt,
                AttackRange = config.EnemyAttackRange,
                AttackInterval = config.EnemyAttackInterval,
                AttackDamage = config.EnemyAttackDamage,
                Damage = pendingDamage.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);
            // A single explicit join: projectile hits and player HP consume this frame's pursuit.
            state.Dependency.Complete();
            while (pendingDamage.TryDequeue(out float damage)) player.Health = math.max(0, player.Health - damage);
            if (player.Health <= 0)
            {
                SystemAPI.SetSingleton(player);
                return;
            }

            var healthLookup = SystemAPI.GetComponentLookup<Health>(false);
            var positionLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            // Projectiles are few; serial damage writes make simultaneous hits deterministic.
            foreach (var (projectileRef, transformRef, entity) in
                SystemAPI.Query<RefRW<CombatProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var projectile = projectileRef.ValueRO;
                projectile.Lifetime -= dt;
                if (projectile.Lifetime <= 0 ||
                    !healthLookup.TryGetComponent(projectile.Target, out var health) || health.Current <= 0 ||
                    !positionLookup.TryGetComponent(projectile.Target, out var target))
                {
                    commands.DestroyEntity(entity);
                    player.ActiveProjectiles--;
                    continue;
                }

                float3 offset = target.Position - transformRef.ValueRO.Position;
                float remaining = math.length(offset);
                float step = projectile.Speed * dt;
                if (remaining <= step + 0.35f)
                {
                    health.Current = math.max(0, health.Current - projectile.Damage);
                    healthLookup[projectile.Target] = health;
                    player.Hits++;
                    if (health.Current <= 0)
                    {
                        commands.DestroyEntity(projectile.Target);
                        player.Kills++;
                        player.AliveEnemies--;
                    }
                    commands.DestroyEntity(entity);
                    player.ActiveProjectiles--;
                }
                else
                {
                    transformRef.ValueRW.Position += offset / remaining * step;
                    projectileRef.ValueRW = projectile;
                }
            }

            player.FireCooldown -= dt;
            if (player.FireCooldown <= 0)
            {
                Entity nearest = Entity.Null;
                float closestSquared = config.AttackRange * config.AttackRange;
                float3 nearestPosition = default;
                foreach (var (transform, health, enemy, entity) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<Health>, RefRO<CombatEnemy>>().WithEntityAccess())
                {
                    if (health.ValueRO.Current <= 0) continue;
                    float squared = math.distancesq(player.Position, transform.ValueRO.Position);
                    if (squared <= closestSquared)
                    {
                        closestSquared = squared;
                        nearest = entity;
                        nearestPosition = transform.ValueRO.Position;
                    }
                }
                // Poll at firing cadence even if no target exists; never scan per enemy pair.
                player.FireCooldown = config.AttackInterval;
                if (nearest != Entity.Null)
                {
                    Entity bullet = commands.Instantiate(config.ProjectilePrefab);
                    commands.SetComponent(bullet, new CombatProjectile
                    {
                        Target = nearest, Damage = config.ProjectileDamage,
                        Speed = config.ProjectileSpeed, Lifetime = 5
                    });
                    commands.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                        player.Position + new float3(0, 0.5f, 0), quaternion.identity, 0.27f));
                    if (distance <= 0.03f)
                        player.Facing = math.normalizesafe(nearestPosition - player.Position, new float3(0, 0, 1));
                    player.ActiveProjectiles++;
                    player.ShotsFired++;
                }
            }
            commands.Playback(state.EntityManager);
            SystemAPI.SetSingleton(player);
        }

        [BurstCompile]
        private partial struct PursuitJob : IJobEntity
        {
            public float3 PlayerPosition;
            public float DeltaTime, AttackRange, AttackInterval, AttackDamage;
            public NativeQueue<float>.ParallelWriter Damage;

            private void Execute(ref LocalTransform transform, ref CombatEnemy enemy, in MoveSpeed speed, in Health health)
            {
                if (health.Current <= 0) return;
                float3 offset = PlayerPosition - transform.Position;
                offset.y = 0;
                float distance = math.length(offset);
                if (distance > AttackRange)
                {
                    float3 direction = offset / distance;
                    transform.Position += direction * math.min(speed.Value * DeltaTime, distance - AttackRange);
                    transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
                }
                enemy.AttackCooldown = math.max(0, enemy.AttackCooldown - DeltaTime);
                // Recheck after moving so attackers do not need an extra frame at the boundary.
                float2 separation = (PlayerPosition - transform.Position).xz;
                if (math.lengthsq(separation) <= (AttackRange + 0.01f) * (AttackRange + 0.01f) && enemy.AttackCooldown <= 0)
                {
                    Damage.Enqueue(AttackDamage);
                    enemy.AttackCooldown = AttackInterval;
                }
            }
        }
    }
}
