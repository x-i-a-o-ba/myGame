using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }
    public struct CombatEnemy : IComponentData { public float AttackCooldown; }
    public struct CombatProjectile : IComponentData
    {
        public Entity Target;
        public float Damage, Speed, Lifetime;
    }
    public struct CombatSettings : IComponentData
    {
        public Entity ProjectilePrefab;
        public float PlayerSpeed, AttackRange, AttackInterval, ProjectileSpeed, ProjectileDamage;
        public float EnemyAttackRange, EnemyAttackDamage, EnemyAttackInterval, ArenaHalfSize;
    }
    /// <summary>Game移动意图、战斗计数与展示快照。Health来自AbilityUnit，不是伤害写入入口。</summary>
    public struct CombatPlayer : IComponentData
    {
        public float3 Position, Destination, Facing;
        public float Health, MaximumHealth, FireCooldown, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles, ShotsFired, Hits;
    }
}
