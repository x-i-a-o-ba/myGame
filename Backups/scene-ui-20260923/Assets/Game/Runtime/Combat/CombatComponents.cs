using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }

    public struct CombatEnemy : IComponentData
    {
        public float AttackCooldown;
    }

    public struct CombatProjectile : IComponentData
    {
        public Entity Target;
        public float Damage;
        public float Speed;
        public float Lifetime;
    }

    public struct CombatSettings : IComponentData
    {
        public Entity ProjectilePrefab;
        public float PlayerSpeed;
        public float AttackRange;
        public float AttackInterval;
        public float ProjectileSpeed;
        public float ProjectileDamage;
        public float EnemyAttackRange;
        public float EnemyAttackDamage;
        public float EnemyAttackInterval;
        public float ArenaHalfSize;
    }

    public struct CombatPlayer : IComponentData
    {
        public float3 Position;
        public float3 Destination;
        public float3 Facing;
        public float Health;
        public float MaximumHealth;
        public float FireCooldown;
        public float Elapsed;
        public int AliveEnemies;
        public int Kills;
        public int ActiveProjectiles;
        public int ShotsFired;
        public int Hits;
    }
}
