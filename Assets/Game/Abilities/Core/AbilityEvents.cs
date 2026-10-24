using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>成功提交的施法快照。可选宿主缓冲，供装备触发器消费；绝不反向驱动命中。</summary>
    [InternalBufferCapacity(4)]
    public struct AbilityCastEvent : IBufferElementData
    {
        public int Slot,Skill,Serial;
        public float3 Position;
        public bool Automatic;
    }
    /// <summary>有界表现快照；达到预算时丢弃表现，权威效果照常结算。</summary>
    [InternalBufferCapacity(0)]
    public struct AbilityFeedback : IBufferElementData
    {
        public Entity Target;
        public float3 Position;
        public float Damage;
        public int Origin,TargetKind;
        public bool Killed;
    }
    [InternalBufferCapacity(0)]
    public struct AbilityLink : IBufferElementData
    {
        public float3 From,To;
        public float4 Color;
    }
}
