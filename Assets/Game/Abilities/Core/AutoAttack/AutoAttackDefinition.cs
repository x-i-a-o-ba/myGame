using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>自动攻击的结算方式。</summary>
    public enum AttackDelivery
    {
        [InspectorName("近战（前摇结束立即命中）")] Melee,
        [InspectorName("弹道（前摇结束发射追踪弹体）")] Projectile
    }

    /// <summary>
    /// 自动攻击被动模块：Dota 2 式攻击前摇、后摇、基础攻击间隔、攻击距离与索敌距离。只存配置；
    /// 运行状态（目标、阶段、冷却）归持有单位的 AutoAttackState。英雄与敌人共用同一模块与同一步进逻辑。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/技能/自动攻击（被动模块）")]
    public sealed class AutoAttackDefinition : ScriptableObject
    {
        [Tooltip("内部兼容键，例如 attack.frost_arrow。")] public string stableId;
        [Tooltip("界面显示名称。")] public string displayName;
        [Tooltip("HUD 被动栏图标。")] public Texture2D icon;
        [TextArea(2,4), Tooltip("界面说明文字。")] public string description;
        [Tooltip("近战在前摇结束时立即结算；弹道在前摇结束时发射追踪弹体，命中后结算。")] public AttackDelivery delivery=AttackDelivery.Projectile;
        [Min(0.1f), Tooltip("攻击距离（米），从单位中心计算，另加目标体型。")] public float range=16;
        [Min(0), Tooltip("索敌距离（米）：待机、攻击移动时自动寻找此距离内最近的敌人。小于攻击距离时按攻击距离处理。")] public float acquisitionRange=20;
        [Min(0.01f), Tooltip("基础攻击间隔（秒），即 Dota 2 的 BAT；实际间隔 = 基础间隔 / 攻速倍率。")] public float baseAttackTime=1.7f;
        [Min(0), Tooltip("攻击前摇（秒）：开始攻击到出手的时间，随攻速缩放；前摇期间失去目标会取消本次攻击且不进入冷却；英雄关闭“移动中攻击”时移动也会取消。")] public float attackPoint=0.3f;
        [Min(0), Tooltip("攻击后摇（秒）：出手后的收招时间，随攻速缩放；任意移动或新指令可立即取消。")] public float backswing=0.5f;
        [Min(0), Tooltip("弹道速度（米/秒）；仅弹道攻击使用。")] public float projectileSpeed=30;
        [Min(0), Tooltip("前摇期间允许目标离开攻击距离的缓冲（米），对应 Dota 2 的攻击距离缓冲。")] public float rangeBuffer=1.5f;
        [Tooltip("待机或攻击移动时自动攻击索敌范围内的敌人。关闭后只攻击被指定的目标。")] public bool autoAcquire=true;
        [Tooltip("命中时触发“普攻命中”事件，用于需要普攻叠层的技能（例如冰霜专注）。")] public bool countsAsBasicAttackHit=true;

        /// <summary>校验并生成 ECS 快照；不修改资源。</summary>
        public AutoAttackData Compile()
        {
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId)
                throw new InvalidOperationException("自动攻击模块缺少内部兼容键或含首尾空格："+name);
            if(!Enum.IsDefined(typeof(AttackDelivery),delivery)) throw new InvalidOperationException("未知的自动攻击结算方式："+name);
            float[] values={range,acquisitionRange,baseAttackTime,attackPoint,backswing,projectileSpeed,rangeBuffer};
            foreach(float value in values)
                if(float.IsNaN(value) || float.IsInfinity(value) || value<0) throw new InvalidOperationException("自动攻击数值必须是有限的非负数："+name);
            if(range<0.1f || baseAttackTime<0.01f) throw new InvalidOperationException("自动攻击距离至少 0.1 米、基础间隔至少 0.01 秒："+name);
            if(attackPoint>baseAttackTime) throw new InvalidOperationException("攻击前摇不能超过基础攻击间隔："+name);
            if(delivery==AttackDelivery.Projectile && projectileSpeed<1) throw new InvalidOperationException("弹道攻击的弹道速度至少为 1 米/秒："+name);
            return new AutoAttackData {Delivery=delivery,Range=range,AcquisitionRange=Mathf.Max(range,acquisitionRange),BaseAttackTime=baseAttackTime,
                AttackPoint=attackPoint,Backswing=backswing,ProjectileSpeed=projectileSpeed,RangeBuffer=rangeBuffer,
                AutoAcquire=autoAcquire,CountsAsBasicAttackHit=countsAsBasicAttackHit};
        }

        /// <summary>界面显示名称：未填写时回退到资源名。</summary>
        public string Label=>string.IsNullOrWhiteSpace(displayName)?name:displayName;
    }
}
