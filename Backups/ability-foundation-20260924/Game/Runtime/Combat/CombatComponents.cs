using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }

    public struct CombatEnemy : IComponentData
    {
        public float AttackCooldown;
        public float SlowRemaining;
        public float SlowMultiplier;
        public float StunRemaining;
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

    /// <summary>Game skill commands are written by input and consumed once by CombatSimulationSystem.</summary>
    public enum CombatAbilityId : byte { Q, W, E, R }

    public struct CombatAbilityCommand : IBufferElementData
    {
        public CombatAbilityId Ability;
        public float3 Target;
        public float3 Direction;
    }

    /// <summary>Game 会话使用的技能数值快照，由四份 ScriptableObject 配置在重开时写入。</summary>
    public struct CombatAbilitySettings : IComponentData
    {
        public int QRequiredHits;
        public float QDuration, QCooldown, QAttackSpeedMultiplier;
        public int WArrowCount;
        public float WRange, WHalfAngleDegrees, WArrowHitRadius, WDamage, WSlowMultiplier, WSlowDuration, WCooldown;
        public float ERange, ETravelSpeed, EAreaRadius, EDamage, EChargeCooldown;
        public int EMaximumCharges;
        public float RRange, RSpeed, RHitRadius, RExplosionRadius, RImpactDamage, RExplosionDamage;
        public float RStunDuration, RSlowMultiplier, RSlowDuration, RCooldown;

        public static CombatAbilitySettings Default => new CombatAbilitySettings
        {
            QRequiredHits = 4, QDuration = 5f, QCooldown = 7f, QAttackSpeedMultiplier = 1.6f,
            WArrowCount = 9, WRange = 22f, WHalfAngleDegrees = 35f, WArrowHitRadius = 1f, WDamage = 30f,
            WSlowMultiplier = 0.65f, WSlowDuration = 2.5f, WCooldown = 5f,
            ERange = 25f, ETravelSpeed = 42f, EAreaRadius = 5f,
            EDamage = 18f, EChargeCooldown = 15f, EMaximumCharges = 2,
            RRange = 150f, RSpeed = 48f, RHitRadius = 1.15f, RExplosionRadius = 7f,
            RImpactDamage = 20f, RExplosionDamage = 16f, RStunDuration = 1.5f,
            RSlowMultiplier = 0.5f, RSlowDuration = 3f, RCooldown = 20f
        };
    }

    /// <summary>Per-run skill state, reset with the CombatOwned session.</summary>
    public struct CombatAbilityState : IComponentData
    {
        public int QStacks;
        public float QBuffRemaining, QCooldownRemaining, WCooldownRemaining;
        public int ECharges;
        public float ERechargeRemaining, RCooldownRemaining;
    }

    /// <summary>E 的飞行落点弹体；抵达目标点后按半径结算一次范围伤害。</summary>
    public struct CombatAreaProjectile : IComponentData
    {
        public float3 Destination;
        public float Speed, Radius, Damage;
    }

    /// <summary>One successful cast notification for the Game presentation layer.</summary>
    public struct CombatAbilityEvent : IBufferElementData
    {
        public CombatAbilityId Ability;
        public float3 Origin, Direction, Target;
        public int Affected;
    }

    /// <summary>R is a straight traveling projectile; its hit and explosion use the Game damage ledger.</summary>
    public struct CombatUltimateProjectile : IComponentData
    {
        public float3 Direction;
        public float Speed, RemainingRange, HitRadius;
    }
}
