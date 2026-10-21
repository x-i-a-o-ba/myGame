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
        // 所有时间均为宿主模拟秒；仅当前动作锁归此组件，已释放的阶段归调度缓冲。
        public float ReleaseAt,ActionReadyAt;
        public int CastSlot;
        public bool AwaitingRelease;
    }
    public enum AbilityCastPhase { [UnityEngine.InspectorName("空闲")] Idle,[UnityEngine.InspectorName("施法前摇")] Windup,[UnityEngine.InspectorName("施法后摇")] Recovery }
    /// <summary>宿主移动、普攻、输入和展示共用的施法时间查询；不修改 Buff 控制状态。</summary>
    public static class AbilityCastTiming
    {
        public static bool IsBusy(in AbilityRuntime runtime,float now)=>now<runtime.ActionReadyAt;
        public static AbilityCastPhase Phase(in AbilityRuntime runtime,float now)
            =>!IsBusy(runtime,now)?AbilityCastPhase.Idle:now<runtime.ReleaseAt?AbilityCastPhase.Windup:AbilityCastPhase.Recovery;
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
        // 所有投递均使用阶段到期的模拟时间，卡顿补发仍保留各次时间；Start 仅抛物线使用。
        public float3 Start;
        public float StartedAt,Elapsed;
    }
    [InternalBufferCapacity(0)]
    public struct SkillCue : IBufferElementData
    {
        public int Skill, Stage;
        public float3 Position, Direction;
    }
}
