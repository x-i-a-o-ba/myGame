using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>宿主拥有库的生命周期；本组件只引用库，不释放它。</summary>
    public struct AbilityRuntime : IComponentData
    {
        public BlobAssetReference<AbilityLibrary> Library;
        public float Time;
        public int CastSerial;
        public CastFailure LastFailure;
    }
    [InternalBufferCapacity(4)]
    public struct SkillCommand : IBufferElementData
    {
        public int Slot;
        public float3 Point, Direction;
        public Entity Target;
    }
    [InternalBufferCapacity(0)]
    public struct ScheduledSkillStage : IBufferElementData
    {
        public int Skill, Stage, Remaining, Cast;
        public float NextAt;
        public float3 Origin, Point, Direction;
        public Entity Target;
    }
    [InternalBufferCapacity(0)]
    public struct SkillFlight : IBufferElementData
    {
        public int Skill, Stage, Cast;
        public Entity Target;
        public float3 Position, Destination, Direction;
        public float RemainingRange;
    }
    [InternalBufferCapacity(0)]
    public struct SkillCue : IBufferElementData
    {
        public int Skill, Stage;
        public float3 Position, Direction;
    }
}
