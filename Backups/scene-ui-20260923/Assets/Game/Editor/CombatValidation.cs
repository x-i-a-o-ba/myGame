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
            Debug.Log("GAME_COMBAT_LOGIC_VALIDATED: targeting, movement, hits, expiry, cooldown, death, 10k pursuit.");
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

            public Entity Enemy(float3 position, float speed = 0)
            {
                var entity = Manager.CreateEntity(typeof(CombatEnemy), typeof(Health), typeof(MoveSpeed), typeof(LocalTransform));
                Manager.SetComponentData(entity, LocalTransform.FromPosition(position));
                Manager.SetComponentData(entity, new Health { Current = 30, Maximum = 30 });
                Manager.SetComponentData(entity, new MoveSpeed { Value = speed });
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
