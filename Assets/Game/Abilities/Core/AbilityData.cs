using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    public struct AbilityLibrary { public BlobArray<SkillData> Skills; public BlobArray<BuffData> Buffs; public BlobArray<PassiveData> Passives; }
    public struct SkillData
    {
        public int AutoTriggerEveryHits;
        public int ConfigId;
        public AimMode Aim;
        public int MaxCharges,RequiredHits;
        public float RechargeSeconds;
        public float CastTime,RecoveryTime;
        public bool UsesRecipe;
        public int MaxDerivedExecutions;
        public float MaxLifetime;
        // Presentation snapshot, compiled with the same lifetime as the geometry (RGBA, each 0..1).
        public float4 IndicatorColor;
        public BlobArray<StageData> Stages;
        // 等级表由 SkillLevelGrowth 编译；为空时按 1 级处理。运行中的当前等级归 AbilitySlot。
        public int MaxLevel;
        public BlobArray<SkillLevelData> Levels;
        // Preserved only for the standalone historical sample scheduler.
        public SkillDelivery Delivery; public TargetRelation Targets;
        public float CastRange,Radius,Cooldown,ManaCost,AreaDuration,AreaInterval,LevelPower;
        public BlobArray<EffectData> Effects;
    }
    public struct PassiveData { public PassiveTrigger Trigger; public BlobArray<EffectData> Effects; }
    /// <summary>不可变的组合阶段。每次重复先投递，再查询，最后提交效果；不存冷却或剩余次数。</summary>
    public struct StageData
    {
        public bool Pierce,FollowOwner,SeekDense; public float InnerRadius,RepeatAdvance,ForwardOffset;
        public int ParentStage,NodeId;
        public RecipeTrigger Trigger;
        public float Delay,RepeatInterval;
        public int RepeatCount;
        public DeliveryKind Delivery;
        public TargetScope Scope;
        public AreaShape Shape;
        public TargetRelation Targets;
        public float Range,Radius,HitRadius,HalfAngleDegrees,Speed;
        public int ProjectileCount;
        public float SpreadDegrees;
        public float ArcHeight,FlightSeconds,LaunchHeight,ArrivalHeight;
        public BlobArray<EffectData> Effects,ImpactEffects;
    }
    public struct EffectData { public EffectKind Kind; public EffectTarget Target; public float Amount,BuffScale,StopDistance; public float SourceAttackRatio,TargetMaxHealthRatio; public int Buff; }
    public struct AttributeModifierData { public AttributeKind Attribute; public ModifierOperation Operation; public float Value; }
    public struct BuffData
    {
        public float DamageReduction,RegenMaxHealth,DamageTaken,Reflect,AttackSplashRadius,AttackSplashRatio;
        public int ExtraTargets; public bool ControlImmune,PierceBasics;
        public int ConfigId;
        public float Duration,Interval,PeriodicDamage,PeriodicAttackRatio,SpeedPercent,AttackFlat,Regen;
        public BuffStackRule Stacking; public int MaxStacks; public bool Dispellable;
        public BlobArray<AttributeModifierData> Modifiers;
        public ControlFlags Controls;
        public BuffSourcePolicy SourcePolicy;
    }
    public struct AbilityOwned : IComponentData { }
    /// <summary>战斗单位的唯一权威属性与状态；基础值与 Buff 聚合值分离。Kind/PassiveMask 保留旧示例兼容。</summary>
    public struct AbilityUnit : IComponentData
    {
        public bool SlowImmune,DisplacementImmune;
        public float BuffDamageReduction,RegenMaxHealth,DamageTaken,Reflect,Shield;
        public float AttackSplashRadius,AttackSplashRatio; public int ExtraTargets; public bool ControlImmune,PierceBasics;
        public int Team,Kind;
        public float Health,MaxHealth,Mana,MaxMana,BaseSpeed,Speed,BaseAttack,Attack,Regen,AttackReadyAt;
        public float3 Destination;
        public uint PassiveMask;
        public float AttackSpeedMultiplier;
        public ControlFlags Controls;
        // 英雄成长写入的基础值，默认 0 时与旧行为一致：攻速基础为 1+AttackSpeedBonus，回复为 BaseRegen+Buff。
        public float BaseRegen,AttackSpeedBonus;
        // 技能伤害增强（0.3=+30%）与冷却缩减（0.2=-20%），执行器按来源读取。英雄由 HeroStatsLogic 从属性百分点换算写入。
        public float SkillAmplify,CooldownReduction;
        // 额外法力回复（点/秒，叠加在基础每秒 3 点上）与受到伤害的减免比例（0.2=-20%，上限 0.8）；默认 0 与旧行为一致。
        public float ManaRegenBonus,DamageReduction;
    }
    [InternalBufferCapacity(2)]
    public struct ActiveBuff : IBufferElementData
    {
        public int Definition,Origin,Stacks; public Entity Source;
        public float ExpiresAt,NextTick,Scale,SourceAttack; public int ExtraTargets;
    }
    /// <summary>持有者的技能槽运行状态。Level 为 0 表示尚未学习，执行器拒绝施放并返回 NotLearned。</summary>
    [InternalBufferCapacity(3)]
    public struct AbilitySlot : IBufferElementData
    {
        public int Definition,Level,FormChoice; public float ReadyAt;
        public int Charges,HitStacks;
        public float RechargeAt;
    }
    public enum CastFailure { None,Dead,Cooldown,Mana,OutOfRange,AreaLimit,Invalid,Busy,Interrupted,NotLearned }
    /// <summary>演示会话组件使系统只在独立场景生效，不接管既有 Combat 单位。</summary>
    public struct AbilitySession : IComponentData
    {
        public BlobAssetReference<AbilityLibrary> Library; public Entity Hero;
        public float Time,AttackRange,AttackInterval; public bool AutoAttack,MoveEnemies,ClampRange;
        public int CastSerial,LastSkill; public CastFailure LastFailure; public float3 LastPoint;
        public int AttackSerial; public float3 LastAttackPoint;
        public int AliveEnemies,ActiveBuffs; public double StepMs;
    }
    [InternalBufferCapacity(4)]
    public struct CastRequest : IBufferElementData { public Entity Source; public int Slot; public float3 Point; }
    [InternalBufferCapacity(8)]
    public struct ActiveArea : IBufferElementData
    {
        public Entity Source; public int Skill,Team; public float3 Center;
        public float Radius,Power,ExpiresAt,NextPulse;
    }
    public struct AreaPulse
    {
        public Entity Source; public int Skill,Team; public float3 Center; public float Radius,Power;
    }
    public struct SpatialEntry { public Entity Entity; public float3 Position; public int Team; }
    public struct EffectRequest : System.IComparable<EffectRequest>
    {
        public Entity Source,Target; public int Origin,Order; public EffectData Effect; public float Power,SourceAttack,DurationScale; public int ExtraTargets;
        public int CompareTo(EffectRequest other)
        { int n=Target.Index.CompareTo(other.Target.Index); return n!=0?n:Order.CompareTo(other.Order); }
    }
}
