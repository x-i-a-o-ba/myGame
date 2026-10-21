using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    public struct AbilityLibrary { public BlobArray<SkillData> Skills; public BlobArray<BuffData> Buffs; public BlobArray<PassiveData> Passives; }
    public struct SkillData
    {
        public SkillDelivery Delivery; public TargetRelation Targets;
        public float CastRange,Radius,Cooldown,ManaCost,AreaDuration,AreaInterval,LevelPower;
        public BlobArray<EffectData> Effects;
    }
    public struct PassiveData { public PassiveTrigger Trigger; public BlobArray<EffectData> Effects; }
    public struct EffectData { public EffectKind Kind; public float Amount,BuffScale; public int Buff; }
    public struct BuffData
    {
        public float Duration,Interval,PeriodicDamage,SpeedPercent,AttackFlat,Regen;
        public BuffStackRule Stacking; public int MaxStacks; public bool Dispellable;
    }
    public struct AbilityOwned : IComponentData { }
    /// <summary>所有训练单位的可变状态；基础属性和 Buff 聚合结果分离，移除贡献后重算。</summary>
    public struct AbilityUnit : IComponentData
    {
        public int Team,Kind;
        public float Health,MaxHealth,Mana,MaxMana,BaseSpeed,Speed,BaseAttack,Attack,Regen,AttackReadyAt;
        public float3 Destination;
        public uint PassiveMask;
    }
    [InternalBufferCapacity(2)]
    public struct ActiveBuff : IBufferElementData
    {
        public int Definition,Origin,Stacks; public Entity Source;
        public float ExpiresAt,NextTick,Scale;
    }
    [InternalBufferCapacity(3)]
    public struct AbilitySlot : IBufferElementData { public int Definition,Level; public float ReadyAt; }
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
