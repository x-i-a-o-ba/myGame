using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }
    /// <summary>敌人等级档位：决定刷怪倍率；最终波首领被击杀时判定胜利。</summary>
    public enum EnemyTier { Normal,Elite,Boss }
    public struct CombatEnemy : IComponentData
    {
        public int ConfigId; public float AttackCooldown;
        /// <summary>击杀奖励经验；Reach 为体型带来的额外攻击距离（米）。</summary>
        public float Experience,Reach;
        public EnemyTier Tier; public int Wave;
    }
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
        // 波次刷怪使用的敌人原型与基础数值；原型实体带 Prefab 标记，随会话销毁。
        public Entity EnemyPrefab;
        public float EnemyHealth, EnemySpeed, SafeSpawnRadius;
        public int MaxAlive;
    }
    /// <summary>由 CombatDifficultyDefinition 生成的难度曲线快照；会话期间只读。</summary>
    public struct CombatWaveSettings : IComponentData
    {
        public float WaveInterval, FirstWaveAt, SpawnRingWidth;
        public int BaseCount, MaxPerWave; public float CountGrowth;
        public float HealthGrowth, DamageGrowth, SpeedPerWave, MaxSpeed;
        public float BaseExperience, ExperienceGrowth;
        public int EliteEvery, EliteBaseCount, EliteWavesPerExtra;
        public float EliteHealth, EliteDamage, EliteScale, EliteExperience;
        public int BossEvery;
        public float BossHealth, BossDamage, BossScale, BossExperience, BossSpeed;
        public int FinalWave;
    }
    public enum CombatOutcome { Running, Victory, Defeat }
    /// <summary>本局波次运行状态；Wave 为已刷出的最后一波。</summary>
    public struct CombatWaveState : IComponentData
    {
        public int Wave, LastWaveSpawned, SpawnedTotal, Skipped;
        public float NextWaveAt;
        public CombatOutcome Outcome;
        public uint Seed;
        public bool FinalBossSpawned;
    }
    /// <summary>Game移动意图、战斗计数与展示快照。Health来自AbilityUnit，不是伤害写入入口。</summary>
    public struct CombatPlayer : IComponentData
    {
        public float3 Position, Destination, Facing;
        public float Health, MaximumHealth, FireCooldown, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles, ShotsFired, Hits;
    }
}
