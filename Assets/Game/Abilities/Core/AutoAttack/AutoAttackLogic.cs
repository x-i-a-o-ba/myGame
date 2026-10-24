using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>由 AutoAttackDefinition 编译的只读攻击配置；会话期间不变。</summary>
    public struct AutoAttackData : IComponentData
    {
        public AttackDelivery Delivery;
        public int SplitTargets,CleaveTargets; public float SplitDamageRatio,CleaveRadius,CleaveHalfAngle,CleaveDamageRatio;
        public float Range,AcquisitionRange,BaseAttackTime,AttackPoint,Backswing,ProjectileSpeed,RangeBuffer;
        public bool AutoAcquire,CountsAsBasicAttackHit;
    }

    /// <summary>攻击动作阶段：待机可开始新攻击；前摇可被取消；后摇可被新指令打断。</summary>
    public enum AttackPhase { Idle, Windup, Backswing }

    /// <summary>单位自己的自动攻击运行状态。Target 仅英雄使用；敌人的目标固定为英雄。</summary>
    public struct AutoAttackState : IComponentData
    {
        public Entity Target;
        public AttackPhase Phase;
        /// <summary>当前阶段已经过的时间（秒）。</summary>
        public float Timer;
        /// <summary>距离下一次可以开始攻击的剩余时间（秒），从出手时刻起算。</summary>
        public float Cooldown;
        /// <summary>索敌节流计时；没找到目标时短暂等待再扫描。</summary>
        public float AcquireDelay;
    }

    /// <summary>一次步进产生的攻击事件。Released 表示本帧出手，调用方负责结算伤害或发射弹体。</summary>
    public enum AutoAttackEvent { None, Started, Released, Cancelled }

    /// <summary>Dota 2 式攻击节奏的纯函数，英雄（主线程）与敌人（Burst 并行任务）共用。</summary>
    public static class AutoAttackLogic
    {
        /// <summary>前摇期间允许偏离的朝向（弧度余弦），约 11.5°，对应 Dota 2 开始攻击的朝向要求。</summary>
        public const float FacingTolerance=0.98f;
        public static float Interval(in AutoAttackData data,float attackSpeed)=>data.BaseAttackTime/math.max(0.01f,attackSpeed);
        public static float Point(in AutoAttackData data,float attackSpeed)=>data.AttackPoint/math.max(0.01f,attackSpeed);
        public static float BackswingTime(in AutoAttackData data,float attackSpeed)=>data.Backswing/math.max(0.01f,attackSpeed);
        /// <summary>每秒攻击次数，用于界面显示。</summary>
        public static float AttacksPerSecond(in AutoAttackData data,float attackSpeed)=>1/math.max(0.0001f,Interval(data,attackSpeed));

        /// <summary>
        /// 推进一帧。engage 表示本帧仍然要攻击当前目标（在距离内、未被禁止、朝向满足）。
        /// 前摇中 engage 变为 false 会取消攻击且不进入冷却；后摇中会立即结束后摇。
        /// </summary>
        public static AutoAttackEvent Step(ref AutoAttackState state,in AutoAttackData data,float attackSpeed,bool engage,float deltaTime)
        {
            // 本帧中冷却结束后剩余的时间，计入新攻击的前摇，避免帧率越低攻速越慢。
            float spare=math.clamp(deltaTime-state.Cooldown,0,deltaTime);
            state.Cooldown=math.max(0,state.Cooldown-deltaTime);
            if(state.Phase==AttackPhase.Windup)
            {
                if(!engage) { state.Phase=AttackPhase.Idle; state.Timer=0; return AutoAttackEvent.Cancelled; }
                state.Timer+=deltaTime;
                return state.Timer>=Point(data,attackSpeed)?Release(ref state,data,attackSpeed):AutoAttackEvent.None;
            }
            if(state.Phase==AttackPhase.Backswing)
            {
                state.Timer+=deltaTime;
                // 后摇被新指令或目标丢失打断；攻速足够高时冷却先于后摇结束，直接开始下一次攻击。
                if(!engage || state.Timer>=BackswingTime(data,attackSpeed)) { state.Phase=AttackPhase.Idle; state.Timer=0; }
                else if(state.Cooldown>0) return AutoAttackEvent.None;
            }
            if(!engage || state.Cooldown>0) return AutoAttackEvent.None;
            state.Phase=AttackPhase.Windup; state.Timer=spare;
            return state.Timer>=Point(data,attackSpeed)?Release(ref state,data,attackSpeed):AutoAttackEvent.Started;
        }

        /// <summary>取消当前前摇/后摇（停止指令、施法打断）；已开始的冷却保留。</summary>
        public static void Interrupt(ref AutoAttackState state)
        {
            if(state.Phase!=AttackPhase.Idle) { state.Phase=AttackPhase.Idle; state.Timer=0; }
        }

        private static AutoAttackEvent Release(ref AutoAttackState state,in AutoAttackData data,float attackSpeed)
        {
            // 超出前摇的部分已经属于下一次攻击的间隔。
            float overflow=math.max(0,state.Timer-Point(data,attackSpeed));
            state.Phase=AttackPhase.Backswing; state.Timer=overflow;
            state.Cooldown=math.max(0,Interval(data,attackSpeed)-Point(data,attackSpeed)-overflow);
            return AutoAttackEvent.Released;
        }
    }
}
