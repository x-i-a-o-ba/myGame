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
        private EntityQuery enemies;
        private EntityQuery ultimates;

        public void OnCreate(ref SystemState state)
        {
            pendingDamage = new NativeQueue<float>(Allocator.Persistent);
            enemies = state.GetEntityQuery(ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<Health>(), ComponentType.ReadWrite<CombatEnemy>());
            ultimates = state.GetEntityQuery(ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<CombatUltimateProjectile>());
            state.RequireForUpdate<CombatSettings>();
            state.RequireForUpdate<CombatPlayer>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (pendingDamage.IsCreated) pendingDamage.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<CombatSettings>();
            var player = SystemAPI.GetSingleton<CombatPlayer>();
            var manager = state.EntityManager;
            Entity session = SystemAPI.GetSingletonEntity<CombatSettings>();
            bool hasAbilities = manager.HasComponent<CombatAbilitySettings>(session) &&
                manager.HasComponent<CombatAbilityState>(session) &&
                manager.HasBuffer<CombatAbilityCommand>(session) &&
                manager.HasBuffer<CombatVisionFlare>(session) &&
                manager.HasBuffer<CombatAbilityEvent>(session);
            var abilitySettings = hasAbilities ? manager.GetComponentData<CombatAbilitySettings>(session) : default;
            var abilityState = hasAbilities ? manager.GetComponentData<CombatAbilityState>(session) : default;
            if (player.Health <= 0)
            {
                if (hasAbilities) StopAbilitiesOnDeath(manager, session, abilitySettings, ref player);
                SystemAPI.SetSingleton(player);
                return;
            }
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
                if (hasAbilities) StopAbilitiesOnDeath(manager, session, abilitySettings, ref player);
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
                    if (hasAbilities)
                        abilityState.QStacks = math.min(math.max(1, abilitySettings.QRequiredHits), abilityState.QStacks + 1);
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

            if (hasAbilities)
            {
                TickAbilities(ref abilityState, abilitySettings, manager.GetBuffer<CombatVisionFlare>(session), dt);
                UpdateUltimateProjectiles(ref state, session, ref player, abilitySettings, commands, dt);
                ProcessAbilityCommands(ref state, session, config, ref player, abilitySettings,
                    ref abilityState, commands);
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
                    if (health.ValueRO.Current <= 0 || (hasAbilities && enemy.ValueRO.VisibleToPlayer == 0)) continue;
                    float squared = math.distancesq(player.Position, transform.ValueRO.Position);
                    if (squared <= closestSquared)
                    {
                        closestSquared = squared;
                        nearest = entity;
                        nearestPosition = transform.ValueRO.Position;
                    }
                }
                // Poll at firing cadence even if no target exists; never scan per enemy pair.
                float attackSpeed = hasAbilities && abilityState.QBuffRemaining > 0
                    ? math.max(1f, abilitySettings.QAttackSpeedMultiplier) : 1f;
                player.FireCooldown = config.AttackInterval / attackSpeed;
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
            commands.Playback(manager);
            if (hasAbilities) manager.SetComponentData(session, abilityState);
            SystemAPI.SetSingleton(player);
        }

        private void StopAbilitiesOnDeath(EntityManager manager, Entity session,
            in CombatAbilitySettings settings, ref CombatPlayer player)
        {
            // A dead run no longer advances the simulation. Clear transient vision/skills now so
            // a stopped E flare or R arrow cannot remain visible indefinitely behind the overlay.
            manager.GetBuffer<CombatAbilityCommand>(session).Clear();
            manager.GetBuffer<CombatVisionFlare>(session).Clear();
            manager.GetBuffer<CombatAbilityEvent>(session).Clear();
            manager.SetComponentData(session, new CombatAbilityState
            {
                ECharges = math.max(1, settings.EMaximumCharges)
            });
            int activeArrows = ultimates.CalculateEntityCount();
            if (activeArrows > 0)
            {
                manager.DestroyEntity(ultimates);
                player.ActiveProjectiles = math.max(0, player.ActiveProjectiles - activeArrows);
            }
        }

        private static void TickAbilities(ref CombatAbilityState ability, in CombatAbilitySettings settings,
            DynamicBuffer<CombatVisionFlare> flares, float dt)
        {
            ability.QBuffRemaining = math.max(0, ability.QBuffRemaining - dt);
            ability.QCooldownRemaining = math.max(0, ability.QCooldownRemaining - dt);
            ability.WCooldownRemaining = math.max(0, ability.WCooldownRemaining - dt);
            ability.RCooldownRemaining = math.max(0, ability.RCooldownRemaining - dt);

            int maximumCharges = math.max(1, settings.EMaximumCharges);
            ability.ECharges = math.clamp(ability.ECharges, 0, maximumCharges);
            if (ability.ECharges < maximumCharges)
            {
                ability.ERechargeRemaining -= dt;
                float recharge = math.max(0.1f, settings.EChargeCooldown);
                while (ability.ERechargeRemaining <= 0 && ability.ECharges < maximumCharges)
                {
                    ability.ECharges++;
                    ability.ERechargeRemaining += recharge;
                }
                if (ability.ECharges == maximumCharges) ability.ERechargeRemaining = 0;
            }

            for (int i = 0; i < flares.Length;)
            {
                var flare = flares[i];
                float3 travel = flare.Destination - flare.Position;
                travel.y = 0;
                float distance = math.length(travel);
                if (distance > 0.01f)
                {
                    float step = math.max(0.1f, flare.Speed) * dt;
                    flare.Position = step >= distance ? flare.Destination : flare.Position + travel / distance * step;
                }
                else flare.RevealRemaining -= dt;

                if (flare.RevealRemaining <= 0)
                    flares.RemoveAtSwapBack(i);
                else { flares[i] = flare; i++; }
            }
        }

        private void ProcessAbilityCommands(ref SystemState state, Entity session, in CombatSettings combat,
            ref CombatPlayer player, in CombatAbilitySettings settings, ref CombatAbilityState ability,
            EntityCommandBuffer changes)
        {
            var manager = state.EntityManager;
            var requests = manager.GetBuffer<CombatAbilityCommand>(session);
            var flares = manager.GetBuffer<CombatVisionFlare>(session);
            var events = manager.GetBuffer<CombatAbilityEvent>(session);
            for (int i = 0; i < requests.Length; i++)
            {
                CombatAbilityCommand request = requests[i];
                float3 direction = AimDirection(player.Position, player.Facing, request);
                switch (request.Ability)
                {
                    case CombatAbilityId.Q:
                        if (ability.QStacks < math.max(1, settings.QRequiredHits) || ability.QCooldownRemaining > 0) break;
                        ability.QStacks = 0;
                        ability.QBuffRemaining = math.max(0.1f, settings.QDuration);
                        ability.QCooldownRemaining = math.max(0, settings.QCooldown);
                        events.Add(new CombatAbilityEvent { Ability = CombatAbilityId.Q,
                            Origin = player.Position, Direction = direction, Target = player.Position });
                        break;
                    case CombatAbilityId.W:
                        if (ability.WCooldownRemaining > 0) break;
                        ability.WCooldownRemaining = math.max(0, settings.WCooldown);
                        int hits = CastVolley(manager, player.Position, direction, settings, ref player, changes);
                        events.Add(new CombatAbilityEvent { Ability = CombatAbilityId.W,
                            Origin = player.Position, Direction = direction,
                            Target = player.Position + direction * math.max(0.1f, settings.WRange), Affected = hits });
                        break;
                    case CombatAbilityId.E:
                        if (ability.ECharges <= 0) break;
                        float3 destination = ScoutDestination(player.Position, direction, request.Target,
                            math.max(0.1f, settings.ERange), combat.ArenaHalfSize);
                        ability.ECharges--;
                        if (ability.ERechargeRemaining <= 0)
                            ability.ERechargeRemaining = math.max(0.1f, settings.EChargeCooldown);
                        flares.Add(new CombatVisionFlare { Position = player.Position, Destination = destination,
                            Speed = math.max(0.1f, settings.ETravelSpeed), Radius = math.max(0.1f, settings.EVisionRadius),
                            RevealRemaining = math.max(0.1f, settings.ERevealDuration) });
                        events.Add(new CombatAbilityEvent { Ability = CombatAbilityId.E,
                            Origin = player.Position, Direction = direction, Target = destination });
                        break;
                    case CombatAbilityId.R:
                        if (ability.RCooldownRemaining > 0) break;
                        ability.RCooldownRemaining = math.max(0, settings.RCooldown);
                        Entity arrow = changes.Instantiate(combat.ProjectilePrefab);
                        // The visual prefab is shared, but R must not enter the homing basic-attack query.
                        changes.RemoveComponent<CombatProjectile>(arrow);
                        changes.AddComponent(arrow, new CombatUltimateProjectile
                        {
                            Direction = direction, Speed = math.max(0.1f, settings.RSpeed),
                            RemainingRange = math.max(0.1f, settings.RRange),
                            HitRadius = math.max(0.1f, settings.RHitRadius)
                        });
                        changes.SetComponent(arrow, LocalTransform.FromPositionRotationScale(
                            player.Position + new float3(0, 0.5f, 0), quaternion.LookRotationSafe(direction, math.up()), 0.65f));
                        player.ActiveProjectiles++;
                        events.Add(new CombatAbilityEvent { Ability = CombatAbilityId.R,
                            Origin = player.Position, Direction = direction,
                            Target = player.Position + direction * math.max(0.1f, settings.RRange) });
                        break;
                }
            }
            requests.Clear();
        }

        private static float3 AimDirection(float3 origin, float3 facing, in CombatAbilityCommand request)
        {
            float3 direction = request.Direction;
            direction.y = 0;
            if (math.lengthsq(direction) < 0.0001f)
            {
                direction = request.Target - origin;
                direction.y = 0;
            }
            facing.y = 0;
            return math.normalizesafe(direction, math.normalizesafe(facing, new float3(0, 0, 1)));
        }

        private static float3 ScoutDestination(float3 origin, float3 direction, float3 requested,
            float range, float arenaHalfSize)
        {
            requested.y = origin.y;
            float3 delta = requested - origin;
            delta.y = 0;
            float distance = math.length(delta);
            float3 destination = distance < 0.01f ? origin + direction * range
                : origin + direction * math.min(distance, range);
            float edge = math.max(1, arenaHalfSize - 2);
            destination.x = math.clamp(destination.x, -edge, edge);
            destination.z = math.clamp(destination.z, -edge, edge);
            return destination;
        }

        private int CastVolley(EntityManager manager, float3 origin, float3 direction,
            in CombatAbilitySettings settings, ref CombatPlayer player, EntityCommandBuffer changes)
        {
            int affected = 0;
            int arrowCount = math.clamp(settings.WArrowCount, 1, 32);
            float range = math.max(0.1f, settings.WRange);
            float radius = math.max(0.05f, settings.WArrowHitRadius);
            float halfAngle = math.radians(math.clamp(settings.WHalfAngleDegrees, 1, 89));
            using var targets = enemies.ToEntityArray(Allocator.Temp);
            var firstHits = new NativeArray<Entity>(arrowCount, Allocator.Temp);
            try
            {
                // Determine all first collisions before applying damage, so one arrow cannot pass through
                // a target killed by a different arrow during this same cast.
                for (int arrow = 0; arrow < arrowCount; arrow++)
                {
                    float angle = arrowCount == 1 ? 0 : math.lerp(-halfAngle, halfAngle, (float)arrow / (arrowCount - 1));
                    float sine = math.sin(angle), cosine = math.cos(angle);
                    float3 ray = new float3(direction.x * cosine - direction.z * sine, 0,
                        direction.x * sine + direction.z * cosine);
                    float3 end = origin + ray * range;
                    Entity first = Entity.Null;
                    float closest = 2f;
                    for (int i = 0; i < targets.Length; i++)
                    {
                        Entity target = targets[i];
                        if (manager.GetComponentData<Health>(target).Current <= 0) continue;
                        float3 position = manager.GetComponentData<LocalTransform>(target).Position;
                        float t = SegmentHitParameter(origin.xz, end.xz, position.xz, radius);
                        if (t < closest) { closest = t; first = target; }
                    }
                    firstHits[arrow] = first;
                }
                for (int arrow = 0; arrow < arrowCount; arrow++)
                {
                    Entity target = firstHits[arrow];
                    if (target == Entity.Null) continue;
                    bool alreadyStruck = false;
                    for (int previous = 0; previous < arrow; previous++)
                        if (firstHits[previous] == target) { alreadyStruck = true; break; }
                    if (alreadyStruck) continue;
                    if (ApplyEnemyHit(manager, target, settings.WDamage, settings.WSlowMultiplier,
                        settings.WSlowDuration, 0, ref player, changes)) affected++;
                }
                return affected;
            }
            finally
            {
                firstHits.Dispose();
            }
        }

        private void UpdateUltimateProjectiles(ref SystemState state, Entity session, ref CombatPlayer player,
            in CombatAbilitySettings settings, EntityCommandBuffer changes, float dt)
        {
            var manager = state.EntityManager;
            using var arrows = ultimates.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < arrows.Length; i++)
            {
                Entity arrowEntity = arrows[i];
                var arrow = manager.GetComponentData<CombatUltimateProjectile>(arrowEntity);
                var transform = manager.GetComponentData<LocalTransform>(arrowEntity);
                float step = math.min(math.max(0.1f, arrow.Speed) * dt, arrow.RemainingRange);
                if (step <= 0)
                {
                    changes.DestroyEntity(arrowEntity);
                    player.ActiveProjectiles = math.max(0, player.ActiveProjectiles - 1);
                    continue;
                }
                float3 from = transform.Position;
                float3 to = from + arrow.Direction * step;
                Entity first = Entity.Null;
                float firstT = 2f;
                using var targets = enemies.ToEntityArray(Allocator.Temp);
                for (int n = 0; n < targets.Length; n++)
                {
                    Entity candidate = targets[n];
                    if (manager.GetComponentData<Health>(candidate).Current <= 0) continue;
                    float3 position = manager.GetComponentData<LocalTransform>(candidate).Position;
                    float t = SegmentHitParameter(from.xz, to.xz, position.xz, arrow.HitRadius);
                    if (t < firstT) { firstT = t; first = candidate; }
                }
                if (first != Entity.Null)
                {
                    float3 impact = manager.GetComponentData<LocalTransform>(first).Position;
                    float radius = math.max(0.1f, settings.RExplosionRadius);
                    float radiusSquared = radius * radius;
                    int affected = 0;
                    for (int n = 0; n < targets.Length; n++)
                    {
                        Entity candidate = targets[n];
                        if (manager.GetComponentData<Health>(candidate).Current <= 0) continue;
                        float3 offset = manager.GetComponentData<LocalTransform>(candidate).Position - impact;
                        offset.y = 0;
                        if (math.lengthsq(offset) > radiusSquared) continue;
                        float damage = settings.RExplosionDamage +
                            (candidate == first ? settings.RImpactDamage : 0);
                        if (ApplyEnemyHit(manager, candidate, damage, settings.RSlowMultiplier,
                            settings.RSlowDuration, settings.RStunDuration, ref player, changes)) affected++;
                    }
                    manager.GetBuffer<CombatAbilityEvent>(session).Add(
                        new CombatAbilityEvent { Ability = CombatAbilityId.R, Origin = impact,
                            Direction = arrow.Direction, Target = impact, Affected = affected });
                    changes.DestroyEntity(arrowEntity);
                    player.ActiveProjectiles = math.max(0, player.ActiveProjectiles - 1);
                }
                else if (step >= arrow.RemainingRange - 0.0001f)
                {
                    changes.DestroyEntity(arrowEntity);
                    player.ActiveProjectiles = math.max(0, player.ActiveProjectiles - 1);
                }
                else
                {
                    transform.Position = to;
                    arrow.RemainingRange -= step;
                    manager.SetComponentData(arrowEntity, transform);
                    manager.SetComponentData(arrowEntity, arrow);
                }
            }
        }

        private static float SegmentHitParameter(float2 from, float2 to, float2 point, float radius)
        {
            float2 path = to - from;
            float squaredLength = math.lengthsq(path);
            float t = squaredLength > 0.000001f ? math.clamp(math.dot(point - from, path) / squaredLength, 0, 1) : 0;
            return math.distancesq(point, from + path * t) <= radius * radius ? t : 2f;
        }

        private static bool ApplyEnemyHit(EntityManager manager, Entity entity, float damage,
            float slowMultiplier, float slowDuration, float stunDuration,
            ref CombatPlayer player, EntityCommandBuffer changes)
        {
            var health = manager.GetComponentData<Health>(entity);
            if (health.Current <= 0) return false;
            if (damage > 0)
            {
                health.Current = math.max(0, health.Current - damage);
                manager.SetComponentData(entity, health);
                player.Hits++;
            }
            if (health.Current <= 0)
            {
                // Marking Health dead immediately makes every later hit skip this entity before ECB playback.
                changes.DestroyEntity(entity);
                player.Kills++;
                player.AliveEnemies--;
                return true;
            }
            if (slowDuration > 0 || stunDuration > 0)
            {
                var enemy = manager.GetComponentData<CombatEnemy>(entity);
                if (slowDuration > 0 && slowMultiplier < 1)
                {
                    float clamped = math.clamp(slowMultiplier, 0.05f, 1f);
                    enemy.SlowMultiplier = enemy.SlowRemaining > 0 ? math.min(enemy.SlowMultiplier, clamped) : clamped;
                    enemy.SlowRemaining = math.max(enemy.SlowRemaining, slowDuration);
                }
                enemy.StunRemaining = math.max(enemy.StunRemaining, stunDuration);
                manager.SetComponentData(entity, enemy);
            }
            return true;
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
                enemy.StunRemaining = math.max(0, enemy.StunRemaining - DeltaTime);
                enemy.SlowRemaining = math.max(0, enemy.SlowRemaining - DeltaTime);
                if (enemy.SlowRemaining <= 0) enemy.SlowMultiplier = 1;
                enemy.AttackCooldown = math.max(0, enemy.AttackCooldown - DeltaTime);
                if (enemy.StunRemaining > 0) return;
                float3 offset = PlayerPosition - transform.Position;
                offset.y = 0;
                float distance = math.length(offset);
                if (distance > AttackRange)
                {
                    float3 direction = offset / distance;
                    float multiplier = enemy.SlowRemaining > 0 ? math.clamp(enemy.SlowMultiplier, 0.05f, 1f) : 1f;
                    transform.Position += direction * math.min(speed.Value * multiplier * DeltaTime, distance - AttackRange);
                    transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
                }
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
