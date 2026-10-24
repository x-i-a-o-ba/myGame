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
        public float3 ChargeDirection; public float ChargeTimer,ChargeReadyIn; public bool ChargeHit;
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
        public float ChargeTriggerRange,ChargeSpeedMultiplier,ChargeDuration,ChargeCooldown,ChargeDamageMultiplier;
        public float ExplosionRadius,ExplosionDamageMultiplier;
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
        public bool CountsAsBasicAttackHit, Critical;
    }
    /// <summary>敌方弹道：追踪英雄位置，由 Burst 任务移动并把伤害写入待结算队列。</summary>
    public struct HostileProjectile : IComponentData
    {
        public float Damage, Speed, Lifetime;
    }
    /// <summary>敌人远程攻击出手时写入的发射请求，主线程统一实例化弹体。</summary>
    public struct HostileShot
    {
        public float3 Origin; public float Damage, Speed;
    }
    public struct CombatSettings : IComponentData
    {
        public Entity ProjectilePrefab, HostileProjectilePrefab;
        /// <summary>英雄转身速度（弧度/秒），0 表示瞬间转身。</summary>
        public float PlayerSpeed, TurnRate;
        /// <summary>移动中攻击：移动指令不打断攻击并自动攻击攻击距离内的敌人，前摇不定身，朝向目标的同时沿移动方向位移。</summary>
        public bool AttackWhileMoving;
        public float EnemyAttackRange, EnemyAttackDamage, EnemyAttackInterval, ArenaHalfSize;
        // 未配置敌人名册时使用的单一敌人原型与基础数值；原型实体带 Prefab 标记，随会话销毁。
        public Entity EnemyPrefab;
        public float EnemyHealth, EnemySpeed, SafeSpawnRadius;
        public int MaxAlive;
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
    /// <summary>Dota 2 式英雄指令。输入层只写意图，模拟系统决定移动、索敌与攻击。</summary>
    public enum HeroOrderKind
    {
        /// <summary>待机：原地不动，自动攻击索敌距离内的敌人并在牵引距离内追击。</summary>
        Idle,
        /// <summary>移动：走到目标点，到达后转为待机。英雄开启“移动中攻击”时途中自动攻击攻击距离内的敌人且不停下，否则途中不攻击。</summary>
        Move,
        /// <summary>攻击指定目标：追击并攻击直到目标死亡，然后转为待机。</summary>
        AttackTarget,
        /// <summary>攻击移动：走向目标点，途中自动攻击索敌距离内的敌人，击杀后继续前进。</summary>
        AttackMove,
        /// <summary>原地待命：不移动，只攻击攻击距离内的敌人。</summary>
        Hold,
        /// <summary>拾取（右键地上的装备）：走向 Value 编号的地上装备，进入拾取距离后拾取并转为待机；途中行为与移动相同。</summary>
        PickUp,
        /// <summary>丢弃（拖动装备到地面）：走向落点 Point，进入丢弃距离后把第 Value 格的装备（下令时为 Item）放到地上并转为待机；途中行为与移动相同。</summary>
        DropItem
    }
    public struct HeroOrder : IComponentData
    {
        public HeroOrderKind Kind; public float3 Point; public Entity Target;
        /// <summary>每次输入层下达新指令时递增，用于取消前摇（移动中攻击开启时移动类指令不取消）。</summary>
        public int Serial;
        /// <summary>拾取：地上装备编号；丢弃：装备栏格。</summary>
        public int Value;
        /// <summary>丢弃：下令时该格的装备下标（期间换位时按装备查找）。</summary>
        public int Item;
    }
    /// <summary>一帧英雄动作意图：由模拟系统按指令与索敌生成，交给纯函数 CombatSimulationSystem.StepMotion 推进。</summary>
    public struct HeroMotion
    {
        public bool HasTarget; public float3 TargetPosition; public float TargetRadius;
        /// <summary>目标在攻击距离外时是否追击；待命与移动中攻击不追击。</summary>
        public bool Chase;
        /// <summary>是否走向 Destination；移动中攻击时可与攻击目标同时生效。</summary>
        public bool Travel; public float3 Destination;
    }
    /// <summary>Game移动意图、战斗计数与展示快照。Health来自AbilityUnit，不是伤害写入入口。</summary>
    public struct CombatPlayer : IComponentData
    {
        public float3 Position, Destination, Facing;
        public float Health, MaximumHealth, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles, ShotsFired, Hits, CriticalHits;
        /// <summary>模拟系统最后处理的指令序号；与 HeroOrder.Serial 不同表示有新指令。</summary>
        public int HandledOrder;
    }
}
