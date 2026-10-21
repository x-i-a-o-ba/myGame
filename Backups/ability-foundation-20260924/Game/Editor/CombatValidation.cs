using System;
using Roguelike.Combat;
using Roguelike.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    public static class CombatValidation
    {
        [MenuItem("Tools/Roguelike/Validate Combat Logic")]
        public static void Validate()
        {
            using (var test = new Fixture())
            {
                var far = test.Enemy(new float3(8, 0.55f, 0));
                var near = test.Enemy(new float3(5, 0.55f, 0));
                test.Tick(0.01f);
                using var bullets = test.Manager.CreateEntityQuery(typeof(CombatProjectile));
                Check(bullets.CalculateEntityCount() == 1, "Auto attack creates one projectile");
                Check(bullets.GetSingleton<CombatProjectile>().Target == near, "Selects the closest enemy");
                test.Tick(0.1f);
                Check(!test.Manager.Exists(near) && test.Manager.Exists(far), "Projectile damages its own target");
                Check(test.Player.Kills == 1 && test.Player.AliveEnemies == 1 && test.Player.Hits == 1, "Hit/kill counts update once");
            }
            using (var test = new Fixture())
            {
                test.Enemy(new float3(30, 0.55f, 0));
                test.Tick(0.1f);
                Check(test.Player.ShotsFired == 0, "No shooting outside attack range");
                var player = test.Player;
                player.Destination = new float3(0.2f, 0.55f, 0);
                test.Player = player;
                test.Tick(0.1f);
                Check(math.distance(test.Player.Position, player.Destination) < 0.001f, "Movement never overshoots destination");
            }
            using (var test = new Fixture())
            {
                Entity target = test.Enemy(new float3(5, 0.55f, 0));
                test.Tick(0.01f);
                test.Manager.DestroyEntity(target);
                test.Tick(0.1f);
                Check(test.Player.ActiveProjectiles == 0, "Projectile with destroyed target is reclaimed");
            }
            using (var test = new Fixture())
            {
                Entity target = test.Enemy(new float3(3, 0.55f, 0));
                var player = test.Player;
                player.FireCooldown = 999;
                player.ActiveProjectiles = 2;
                test.Player = player;
                test.Bullet(target); test.Bullet(target);
                test.Tick(0.1f);
                Check(test.Player.Kills == 1 && test.Player.ActiveProjectiles == 0, "Multiple bullets do not double-kill");
            }
            using (var test = new Fixture())
            {
                test.Enemy(new float3(1, 0.55f, 0));
                var player = test.Player; player.FireCooldown = 999; test.Player = player;
                test.Tick(0.1f);
                Check(test.Player.Health == 90, "Contact deals damage");
                test.Tick(0.1f);
                Check(test.Player.Health == 90, "Contact damage obeys attack interval");
                player = test.Player; player.Health = 1; test.Player = player;
                for (int i = 0; i < 12; i++) test.Tick(0.1f);
                Check(test.Player.Health == 0, "Player HP clamps to zero");
                player = test.Player; player.Destination += new float3(10, 0, 0); test.Player = player;
                float3 oldPosition = player.Position;
                test.Tick(0.1f);
                Check(math.distancesq(test.Player.Position, oldPosition) == 0 && test.Player.ShotsFired == 0, "Dead player cannot move or shoot");
            }
            using (var test = new Fixture())
            {
                for (int i = 0; i < 10000; i++) test.Enemy(new float3(50 + i % 100, 0.55f, 50 + i / 100), 2);
                test.Tick(0.1f);
                using var enemies = test.Manager.CreateEntityQuery(typeof(CombatEnemy), typeof(LocalTransform));
                Check(enemies.CalculateEntityCount() == 10000, "10k combat enemies update without population loss");
                using var positions = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                foreach (var position in positions) Check(math.all(math.isfinite(position.Position)), "Finite enemy position");
            }
            ValidateAsheAbilities();
            Debug.Log("GAME_COMBAT_LOGIC_VALIDATED: targeting, movement, hits, expiry, cooldown, death, 10k pursuit, Ashe Q/W/E/R.");
        }

        private static void ValidateAsheAbilities()
        {
            // Four real projectile hits unlock Q. A blocked cast must not consume stacks or restart its buff.
            using (var test = new Fixture())
            {
                var tuning = CombatAbilitySettings.Default;
                tuning.QDuration = 0.25f;
                tuning.QCooldown = 0.3f;
                tuning.QAttackSpeedMultiplier = 2f;
                test.EnableAbilities(tuning);
                Entity target = test.Enemy(new float3(3, 0.55f, 0), health: 200);
                var player = test.Player; player.FireCooldown = 999; test.Player = player;
                test.Command(CombatAbilityId.Q, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Abilities.QBuffRemaining == 0 && test.Events.Length == 0, "Q cannot activate before hit stacks");
                player = test.Player; player.ActiveProjectiles = tuning.QRequiredHits; test.Player = player;
                for (int i = 0; i < tuning.QRequiredHits; i++) test.Bullet(target);
                test.Tick(0.1f);
                Check(test.Abilities.QStacks == tuning.QRequiredHits && test.Player.Hits == tuning.QRequiredHits,
                    "Basic projectile hits fill Q stacks");
                test.Command(CombatAbilityId.Q, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Abilities.QStacks == 0 && test.Abilities.QBuffRemaining > 0 &&
                    test.Abilities.QCooldownRemaining > 0 && test.Events.Length == 1,
                    "Q consumes stacks and starts buff and cooldown");
                player = test.Player; player.FireCooldown = 0; test.Player = player;
                test.Tick(0.01f);
                Check(math.abs(test.Player.FireCooldown - 0.06f) < 0.001f,
                    "Q buff shortens the actual basic attack interval");
                player = test.Player; player.FireCooldown = 999; test.Player = player;
                var ability = test.Abilities; ability.QStacks = tuning.QRequiredHits; test.Abilities = ability;
                test.Command(CombatAbilityId.Q, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Events.Length == 1 && test.Abilities.QStacks == tuning.QRequiredHits,
                    "Q cooldown blocks a second cast without consuming stacks");
                for (int i = 0; i < 3; i++) test.Tick(0.1f);
                Check(test.Abilities.QBuffRemaining == 0 && test.Abilities.QCooldownRemaining == 0,
                    "Q buff and cooldown expire independently");
                test.Command(CombatAbilityId.Q, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Events.Length == 2 && test.Abilities.QStacks == 0,
                    "Q can reactivate from retained stacks after cooldown");
            }

            // All three volley rays overlap the near monster; no ray may pierce it or damage it twice.
            using (var test = new Fixture())
            {
                var tuning = CombatAbilitySettings.Default;
                tuning.WArrowCount = 3;
                tuning.WHalfAngleDegrees = 10;
                tuning.WArrowHitRadius = 2;
                test.EnableAbilities(tuning);
                Entity near = test.Enemy(new float3(4, 0.55f, 0), health: 100);
                Entity far = test.Enemy(new float3(8, 0.55f, 0), health: 100);
                var player = test.Player; player.FireCooldown = 999; test.Player = player;
                test.Command(CombatAbilityId.W, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Manager.GetComponentData<Health>(near).Current == 100 - tuning.WDamage &&
                    test.Manager.GetComponentData<Health>(far).Current == 100 &&
                    test.Events.Length == 1 && test.Events[0].Affected == 1,
                    "W hits the first monster once across overlapping rays");
                var slow = test.Manager.GetComponentData<CombatEnemy>(near);
                Check(slow.SlowRemaining > 0 && math.abs(slow.SlowMultiplier - tuning.WSlowMultiplier) < 0.001f,
                    "W applies a slow to the hit monster");
                test.Command(CombatAbilityId.W, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                Check(test.Events.Length == 1 && test.Manager.GetComponentData<Health>(near).Current == 100 - tuning.WDamage,
                    "W cooldown prevents a second volley");
            }

            using (var test = new Fixture())
            {
                var tuning = CombatAbilitySettings.Default;
                tuning.ERange = 20;
                tuning.ETravelSpeed = 10;
                tuning.EAreaRadius = 0.8f;
                tuning.EChargeCooldown = 0.25f;
                test.EnableAbilities(tuning);
                Entity first = test.Enemy(new float3(3, 0.55f, 0), health: 100);
                Entity second = test.Enemy(new float3(5, 0.55f, 0), health: 100);
                var player = test.Player; player.FireCooldown = 999; test.Player = player;
                test.Command(CombatAbilityId.E, new float3(1, 0, 0), new float3(3, 0.55f, 0));
                test.Tick(0.01f);
                using var areas = test.Manager.CreateEntityQuery(typeof(CombatAreaProjectile));
                Check(test.Abilities.ECharges == 1 && areas.CalculateEntityCount() == 1 &&
                    math.abs(areas.GetSingleton<CombatAreaProjectile>().Destination.x - 3) < 0.001f,
                    "E consumes one charge and creates a projectile for the requested point");
                test.Command(CombatAbilityId.E, new float3(1, 0, 0), new float3(5, 0.55f, 0));
                test.Tick(0.01f);
                Check(test.Abilities.ECharges == 0 && areas.CalculateEntityCount() == 2,
                    "E can spend both charges while projectiles travel");
                test.Command(CombatAbilityId.E, new float3(1, 0, 0), new float3(8, 0.55f, 0));
                test.Tick(0.01f);
                Check(areas.CalculateEntityCount() == 2 && test.Abilities.ECharges == 0,
                    "E rejects a cast with zero charges");
                // CombatSimulationSystem caps each frame at 0.1 s; advance four real frames.
                for (int frame = 0; frame < 4; frame++) test.Tick(0.1f);
                Check(test.Manager.GetComponentData<Health>(first).Current == 100 - tuning.EDamage &&
                    test.Manager.GetComponentData<Health>(second).Current == 100 &&
                    test.Events.Length == 1 && test.Events[0].Affected == 1,
                    "E arrival damages only monsters in the target circle");
                Check(test.Abilities.ECharges == 1, "E restores one charge after the recharge interval");
                test.Command(CombatAbilityId.E, new float3(1, 0, 0), new float3(8, 0.55f, 0));
                test.Tick(0.01f);
                Check(test.Abilities.ECharges == 0 && areas.CalculateEntityCount() == 2,
                    "E can cast again using the recharged charge");
            }

            using (var test = new Fixture())
            {
                var tuning = CombatAbilitySettings.Default;
                tuning.RSpeed = 100;
                test.EnableAbilities(tuning);
                Entity first = test.Enemy(new float3(5, 0.55f, 0), health: 100);
                Entity nearby = test.Enemy(new float3(7, 0.55f, 2), health: 100);
                Entity outside = test.Enemy(new float3(13, 0.55f, 0), health: 100);
                var player = test.Player; player.FireCooldown = 999; test.Player = player;
                test.Command(CombatAbilityId.R, new float3(1, 0, 0), default);
                test.Tick(0.01f);
                using var arrows = test.Manager.CreateEntityQuery(typeof(CombatUltimateProjectile));
                Check(arrows.CalculateEntityCount() == 1 && test.Player.ActiveProjectiles == 1,
                    "R creates one straight projectile");
                test.Command(CombatAbilityId.R, new float3(1, 0, 0), default);
                test.Tick(0.1f);
                Check(arrows.CalculateEntityCount() == 0 && test.Player.ActiveProjectiles == 0 &&
                    test.Events.Length == 2 && test.Events[1].Affected == 2,
                    "R explodes on the first monster and cooldown blocks duplicate casting");
                Check(test.Manager.GetComponentData<Health>(first).Current ==
                    100 - tuning.RImpactDamage - tuning.RExplosionDamage &&
                    test.Manager.GetComponentData<Health>(nearby).Current == 100 - tuning.RExplosionDamage &&
                    test.Manager.GetComponentData<Health>(outside).Current == 100,
                    "R impact and explosion damage affect only monsters in the blast");
                var firstStatus = test.Manager.GetComponentData<CombatEnemy>(first);
                var nearbyStatus = test.Manager.GetComponentData<CombatEnemy>(nearby);
                Check(firstStatus.StunRemaining > 0 && nearbyStatus.StunRemaining > 0 &&
                    firstStatus.SlowRemaining > 0 && nearbyStatus.SlowRemaining > 0 &&
                    math.abs(firstStatus.SlowMultiplier - tuning.RSlowMultiplier) < 0.001f,
                    "R applies stun and slow to the blast targets");
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Combat validation: " + message);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly World world = new World("Combat validation");
            private readonly Entity session, prefab;
            private readonly SystemHandle system;
            private double elapsed;
            public EntityManager Manager => world.EntityManager;
            public CombatPlayer Player { get => Manager.GetComponentData<CombatPlayer>(session); set => Manager.SetComponentData(session, value); }
            public CombatAbilityState Abilities
            {
                get => Manager.GetComponentData<CombatAbilityState>(session);
                set => Manager.SetComponentData(session, value);
            }
            public DynamicBuffer<CombatAbilityEvent> Events => Manager.GetBuffer<CombatAbilityEvent>(session);

            public Fixture()
            {
                prefab = Manager.CreateEntity(typeof(CombatProjectile), typeof(LocalTransform), typeof(Prefab));
                session = Manager.CreateEntity(typeof(CombatSettings), typeof(CombatPlayer));
                Manager.SetComponentData(session, new CombatSettings
                {
                    ProjectilePrefab = prefab, PlayerSpeed = 10, AttackRange = 10, AttackInterval = 0.12f,
                    ProjectileDamage = 30, ProjectileSpeed = 100, EnemyAttackRange = 1.25f,
                    EnemyAttackDamage = 10, EnemyAttackInterval = 1, ArenaHalfSize = 125
                });
                Player = new CombatPlayer { Position = new float3(0, 0.55f, 0), Destination = new float3(0, 0.55f, 0), Health = 100, MaximumHealth = 100 };
                system = world.GetOrCreateSystem<CombatSimulationSystem>();
            }

            public void EnableAbilities(CombatAbilitySettings settings)
            {
                Manager.AddComponentData(session, settings);
                Manager.AddComponentData(session, new CombatAbilityState { ECharges = math.max(1, settings.EMaximumCharges) });
                Manager.AddBuffer<CombatAbilityCommand>(session);
                Manager.AddBuffer<CombatAbilityEvent>(session);
            }

            public void Command(CombatAbilityId ability, float3 direction, float3 target) =>
                Manager.GetBuffer<CombatAbilityCommand>(session).Add(new CombatAbilityCommand
                {
                    Ability = ability, Direction = direction, Target = target
                });

            public Entity Enemy(float3 position, float speed = 0, float health = 30)
            {
                var entity = Manager.CreateEntity(typeof(CombatEnemy), typeof(Health), typeof(MoveSpeed), typeof(LocalTransform));
                Manager.SetComponentData(entity, LocalTransform.FromPosition(position));
                Manager.SetComponentData(entity, new Health { Current = health, Maximum = health });
                Manager.SetComponentData(entity, new MoveSpeed { Value = speed });
                Manager.SetComponentData(entity, new CombatEnemy { SlowMultiplier = 1 });
                var player = Player; player.AliveEnemies++; Player = player;
                return entity;
            }

            public void Bullet(Entity target)
            {
                var bullet = Manager.Instantiate(prefab);
                Manager.SetComponentData(bullet, new CombatProjectile { Target = target, Damage = 30, Speed = 100, Lifetime = 5 });
                Manager.SetComponentData(bullet, LocalTransform.FromPosition(new float3(0, 0.55f, 0)));
            }

            public void Tick(float delta)
            {
                elapsed += delta;
                world.SetTime(new Unity.Core.TimeData(elapsed, delta));
                system.Update(world.Unmanaged);
                Manager.CompleteAllTrackedJobs();
            }

            public void Dispose() => world.Dispose();
        }
    }
}
