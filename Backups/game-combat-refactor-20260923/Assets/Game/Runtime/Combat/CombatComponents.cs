using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }

    public struct CombatEnemy : IComponentData
    {
        public float AttackCooldown;
        // Updated by the Game fog adapter. Hidden enemies keep simulating, but cannot be auto-targeted.
        public byte VisibleToPlayer;
        // Optional hidden-state input for normal vision + true-sight intersection (0 = not invisible).
        public byte Invisible;
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

    /// <summary>Per-run skill tuning. Values are project defaults, not official champion balance values.</summary>
    [System.Serializable]
    public struct CombatAbilitySettings : IComponentData
    {
        public int QRequiredHits;
        public float QDuration, QCooldown, QAttackSpeedMultiplier;
        public int WArrowCount;
        public float WRange, WHalfAngleDegrees, WArrowHitRadius, WDamage, WSlowMultiplier, WSlowDuration, WCooldown;
        public float ERange, ETravelSpeed, EVisionRadius, ERevealDuration, EChargeCooldown;
        public int EMaximumCharges;
        public float RRange, RSpeed, RHitRadius, RExplosionRadius, RImpactDamage, RExplosionDamage;
        public float RStunDuration, RSlowMultiplier, RSlowDuration, RCooldown;

        public static CombatAbilitySettings Default => new CombatAbilitySettings
        {
            QRequiredHits = 4, QDuration = 5f, QCooldown = 7f, QAttackSpeedMultiplier = 1.6f,
            WArrowCount = 9, WRange = 22f, WHalfAngleDegrees = 35f, WArrowHitRadius = 1f, WDamage = 12f,
            WSlowMultiplier = 0.65f, WSlowDuration = 2.5f, WCooldown = 5f,
            ERange = 110f, ETravelSpeed = 42f, EVisionRadius = 16f,
            ERevealDuration = 5f, EChargeCooldown = 15f, EMaximumCharges = 2,
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

    /// <summary>An active E scout. Fog reads Position/Radius as a temporary flying friendly vision source.</summary>
    public struct CombatVisionFlare : IBufferElementData
    {
        public float3 Position, Destination;
        public float Radius, Speed, RevealRemaining;
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
