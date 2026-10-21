using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    public struct AbilityLibrary { public BlobArray<SkillData> Skills; public BlobArray<BuffData> Buffs; public BlobArray<PassiveData> Passives; }
    public struct SkillData
    {
        public AimMode Aim;
        public int MaxCharges,RequiredHits;
        public float RechargeSeconds;
        // Presentation snapshot, compiled with the same lifetime as the geometry (RGBA, each 0..1).
        public float4 IndicatorColor;
        public BlobArray<StageData> Stages;
        // Preserved only for the standalone historical sample scheduler.
        public SkillDelivery Delivery; public TargetRelation Targets;
        public float CastRange,Radius,Cooldown,ManaCost,AreaDuration,AreaInterval,LevelPower;
        public BlobArray<EffectData> Effects;
    }
    public struct PassiveData { public PassiveTrigger Trigger; public BlobArray<EffectData> Effects; }
    /// <summary>不可变的组合阶段。每次重复先投递，再查询，最后提交效果；不存冷却或剩余次数。</summary>
    public struct StageData
    {
        public float Delay,RepeatInterval;
        public int RepeatCount;
        public DeliveryKind Delivery;
        public TargetScope Scope;
        public AreaShape Shape;
        public TargetRelation Targets;
        public float Range,Radius,HitRadius,HalfAngleDegrees,Speed;
        public BlobArray<EffectData> Effects,ImpactEffects;
    }
    public struct EffectData { public EffectKind Kind; public EffectTarget Target; public float Amount,BuffScale; public int Buff; }
    public struct AttributeModifierData { public AttributeKind Attribute; public ModifierOperation Operation; public float Value; }
    public struct BuffData
    {
        public float Duration,Interval,PeriodicDamage,SpeedPercent,AttackFlat,Regen;
        public BuffStackRule Stacking; public int MaxStacks; public bool Dispellable;
        public BlobArray<AttributeModifierData> Modifiers;
        public ControlFlags Controls;
        public BuffSourcePolicy SourcePolicy;
    }
    public struct AbilityOwned : IComponentData { }
    /// <summary>战斗单位的唯一权威属性与状态；基础值与 Buff 聚合值分离。Kind/PassiveMask 保留旧示例兼容。</summary>
    public struct AbilityUnit : IComponentData
    {
        public int Team,Kind;
        public float Health,MaxHealth,Mana,MaxMana,BaseSpeed,Speed,BaseAttack,Attack,Regen,AttackReadyAt;
        public float3 Destination;
        public uint PassiveMask;
        public float AttackSpeedMultiplier;
        public ControlFlags Controls;
    }
    [InternalBufferCapacity(2)]
    public struct ActiveBuff : IBufferElementData
    {
        public int Definition,Origin,Stacks; public Entity Source;
        public float ExpiresAt,NextTick,Scale;
    }
    [InternalBufferCapacity(3)]
    public struct AbilitySlot : IBufferElementData
    {
        public int Definition,Level; public float ReadyAt;
        public int Charges,HitStacks;
        public float RechargeAt;
    }
    public enum CastFailure { None,Dead,Cooldown,Mana,OutOfRange,AreaLimit,Invalid }
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
        public Entity Source,Target; public int Origin,Order; public EffectData Effect; public float Power;
        public int CompareTo(EffectRequest other)
        { int n=Target.Index.CompareTo(other.Target.Index); return n!=0?n:Order.CompareTo(other.Order); }
    }
}
