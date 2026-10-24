using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    public struct CombatOwned : IComponentData { }
    /// <summary>敌人等级档位：决定刷怪倍率；本关首领被击杀时判定胜利。</summary>
    public enum EnemyTier { Normal,Elite,Boss }
    /// <summary>敌人名册条目允许出现的档位，可多选。</summary>
    [Flags] public enum EnemyTierMask
    {
        [InspectorName("普通")] Normal=1,
        [InspectorName("精英")] Elite=2,
        [InspectorName("首领")] Boss=4
    }
    /// <summary>敌人的运行状态与击杀奖励。攻击节奏在 AutoAttackState，行为配置在 EnemyBehaviorData。</summary>
    public struct CombatEnemy : IComponentData
    {
        /// <summary>数字配置 ID（3000–3999）与本局名册中的种类下标。</summary>
        public int ConfigId,Kind;
        /// <summary>击杀奖励经验与金币；Reach 为体型带来的额外攻击距离（米）。</summary>
        public float Experience,Gold,Reach;
        public EnemyTier Tier; public int Stage;
        /// <summary>冲锋运行状态：方向、剩余冲锋时间、冲锋冷却与本次冲锋是否已经撞到英雄。</summary>
        /// <summary>自爆死亡不给经验和金币、不计击杀。</summary>
        public bool SelfDestructed;
        /// <summary>受击闪白：上一帧生命、剩余闪白时间与本档位的基础颜色。</summary>
        public float LastHealth,Flash; public float4 BaseColor;
    }
    /// <summary>敌人种类的只读行为配置，由 EnemyArchetype 预制体编译，刷怪时随原型复制。</summary>
    public struct EnemyBehaviorData : IComponentData
    {
        public EnemyBehavior Behavior;
        /// <summary>远程：停在攻击距离 × PreferredDistance 处；英雄比 RetreatDistance 更近时后退。</summary>
        public float PreferredDistance,RetreatDistance;
    }
    /// <summary>会话内一个可刷出的敌人种类：原型实体、出场规则与数值倍率。归会话实体所有。</summary>
    [InternalBufferCapacity(0)]
    public struct CombatEnemyKind : IBufferElementData
    {
        public Entity Prefab; public int ConfigId;
        public EnemyTierMask Tiers; public float FirstSecond,LastSecond,Weight; public bool SpecialElite;
        public float Health,Damage,Speed,Experience,Gold,Scale;
        /// <summary>普通、精英、首领三档的基础颜色（已混入档位色调）。</summary>
        public float4 Color,EliteColor,BossColor;
    }
    /// <summary>英雄弹道普攻：追踪目标实体，命中后由宿主结算伤害、吸血、装备命中效果与普攻命中事件。Damage 已包含暴击倍率。</summary>
    public struct CombatProjectile : IComponentData
    {
        public Entity Target;
        public float Damage, Speed, Lifetime;
        public bool CountsAsBasicAttackHit, Piercing;
        public float3 Direction; public float RemainingRange;
    }
    /// <summary>敌方弹道：出手时锁定方向，由 Burst 任务移动并把伤害写入待结算队列。</summary>
    public struct HostileProjectile : IComponentData
    {
        public Entity Source;public float3 Direction;
        public float Damage, Speed, Lifetime;
    }
    /// <summary>敌人远程攻击出手时写入的发射请求，主线程统一实例化弹体。</summary>
    public struct PendingCombatDamage {public Entity Source; public float Amount; public bool Melee;}
    public struct HostileShot
    {
        public Entity Source; public float3 Origin; public float Damage, Speed;
    }
    public struct CombatSettings : IComponentData
    {
        public Entity ProjectilePrefab, HostileProjectilePrefab;
        /// <summary>英雄转身速度（弧度/秒），0 表示瞬间转身。</summary>
        public float PlayerSpeed;
        /// <summary>移动中攻击：移动指令不打断攻击并自动攻击攻击距离内的敌人，前摇不定身，朝向目标的同时沿移动方向位移。</summary>
        public bool AttackWhileMoving;
        public float EnemyAttackRange, EnemyAttackDamage, EnemyAttackInterval, ArenaHalfSize;
        // 未配置敌人名册时使用的单一敌人原型与基础数值；原型实体带 Prefab 标记，随会话销毁。
        public Entity EnemyPrefab;
        public float EnemyHealth, EnemySpeed, SafeSpawnRadius;
        public int MaxAlive,EliteCount;public bool Lava;
        public float PassiveGoldPerSecond,HealthMultiplier,GoldMultiplier,DamageMultiplier;
    }
    /// <summary>只读的分钟难度快照。</summary>
    public struct CombatDifficultySettings : IComponentData
    {
        public float HealthGrowth,DamageGrowth,SpeedPerMinute,MaxSpeed,BaseExperience,BaseGold;
        public float EliteHealth,EliteDamage,EliteScale,EliteExperience,EliteGold;
        public float3 BossHealth,BossExperience,BossGold;
        public float BossDamage,BossScale,BossSpeed;
    }
    public enum CombatOutcome { Running, Victory, Defeat }
    /// <summary>会话拥有的导演进度；Stage 从 1 开始，本关时钟不含营地。第六步接入跨关前，一次会话是一关。</summary>
    public struct CombatDirectorState : IComponentData
    {
        public int Stage,LastBatchSpawned,SpawnedTotal,PositionRejected,TargetAlive,PendingTide,ElitesSpawned;
        public float NextTickAt,NextEliteAt,StageElapsed,TideNoticeUntil;
        public float2 TideDirection;
        public CombatOutcome Outcome;
        public uint Seed;
        public bool BossSpawned,TideStarted,BossEnraged;
        public Entity Boss;
        public static CombatDirectorState Begin(in SpawnDirectorSettings settings,int stage,uint seed)
            =>new CombatDirectorState {Stage=math.clamp(stage,1,3),NextEliteAt=settings.EliteFirstAt[math.clamp(stage-1,0,2)],Seed=seed==0?1:seed};
    }
    public struct CombatPlayer : IComponentData
    {
        public float3 Position, Destination, Facing;
        public float Health, MaximumHealth, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles, ShotsFired, Hits;
    }
}
