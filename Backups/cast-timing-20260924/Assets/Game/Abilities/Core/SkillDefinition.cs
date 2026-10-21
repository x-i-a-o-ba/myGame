using UnityEngine;

namespace Roguelike.Abilities
{
    // Historical values remain stable for serialized sample assets.
    public enum SkillDelivery { [InspectorName("即时圆形")] InstantCircle, [InspectorName("持续圆形")] PersistentCircle, [InspectorName("自身圆形")] SelfCircle }
    public enum TargetRelation { [InspectorName("敌方")] Enemies, [InspectorName("友方")] Allies }
    public enum EffectKind { [InspectorName("伤害")] Damage, [InspectorName("治疗")] Heal, [InspectorName("施加 Buff")] ApplyBuff, [InspectorName("驱散")] Dispel }
    public enum BuffStackRule { [InspectorName("刷新时长")] Refresh, [InspectorName("加层并刷新")] AddAndRefresh }
    public enum PassiveTrigger { [InspectorName("学会后常驻")] WhileLearned, [InspectorName("普攻命中")] BasicAttackHit }

    public enum AimMode { [InspectorName("自身")] Self, [InspectorName("目标点")] Point, [InspectorName("方向")] Direction, [InspectorName("单位")] Unit }
    public enum TargetScope { [InspectorName("自身")] Self, [InspectorName("单体")] Single, [InspectorName("范围")] Area, [InspectorName("全体")] All }
    public enum AreaShape { [InspectorName("圆形")] Circle, [InspectorName("扇形")] Cone, [InspectorName("直线")] Line }
    public enum DeliveryKind { [InspectorName("立即结算")] Instant, [InspectorName("飞向目标点")] PointProjectile, [InspectorName("直线弹体")] LinearProjectile, [InspectorName("三维抛物线投递")] ParabolicProjectile }
    public enum EffectTarget { [InspectorName("选中目标")] Selected, [InspectorName("施法者")] Source }
    public enum BuffSourcePolicy { [InspectorName("按定义合并")] ByDefinition, [InspectorName("按来源合并")] BySource, [InspectorName("每次独立")] Independent }
    public enum AttributeKind { [InspectorName("移动速度")] MoveSpeed, [InspectorName("攻击力")] AttackDamage, [InspectorName("攻击速度")] AttackSpeed }
    public enum ModifierOperation { [InspectorName("固定加成")] Add, [InspectorName("百分比加成")] Percent, [InspectorName("乘法系数")] Multiply }
    [System.Flags]
    public enum ControlFlags { [InspectorName("无")] None=0, [InspectorName("禁止移动")] MovementBlocked=1, [InspectorName("禁止普攻")] AttackBlocked=2, [InspectorName("禁止施法")] CastBlocked=4 }

    /// <summary>一段独立的投递、目标查询与效果组合；次数和间隔描述多段技能。</summary>
    [System.Serializable]
    public sealed class SkillStage
    {
        [InspectorName("开始延迟（秒）"), Min(0)] public float delay;
        [InspectorName("执行次数"), Range(1,32)] public int repeatCount=1;
        [InspectorName("重复间隔（秒）"), Min(0)] public float repeatInterval;
        [InspectorName("投递方式")] public DeliveryKind delivery;
        [InspectorName("目标范围")] public TargetScope scope=TargetScope.Area;
        [InspectorName("范围形状")] public AreaShape shape;
        [InspectorName("目标关系")] public TargetRelation targets;
        [InspectorName("几何/投递长度（米）"), Min(0)] public float range=18;
        [InspectorName("半径/线宽半径（米）"), Min(0)] public float radius=5;
        [InspectorName("直线弹体碰撞半径（米）"), Min(0)] public float hitRadius=0.5f;
        [InspectorName("扇形半角（度）"), Range(0,180)] public float halfAngleDegrees=35;
        [InspectorName("弹体速度（米/秒）"), Min(0)] public float speed=40;
        [Min(0), Tooltip("抛物线相对起终点连线的最高附加高度，Unity 世界单位；仅三维抛物线投递使用。")] public float arcHeight=3;
        [Min(0.001f), Tooltip("完整飞行时间（秒）；独立于距离，不模拟物理重力。")] public float flightSeconds=1;
        [Min(0), Tooltip("相对施法起点的发射高度，Unity 世界单位。")] public float launchHeight=1;
        [Min(0), Tooltip("相对目标点的落点高度，Unity 世界单位；结算区域仍按 XZ 平面筛选。")] public float arrivalHeight;
        [InspectorName("选定范围内的效果")] public EffectDefinition[] effects=System.Array.Empty<EffectDefinition>();
        [InspectorName("首个碰撞目标的效果"), Tooltip("仅直线弹体使用；先结算碰撞目标，再结算范围效果。")]
        public EffectDefinition[] impactEffects=System.Array.Empty<EffectDefinition>();
    }

    /// <summary>旧独立示例的扁平效果配置。正式技能使用 EffectDefinition 模块，不新增具体英雄字段。</summary>
    [System.Serializable]
    public struct EffectEntry
    {
        public EffectKind kind;
        [Min(0)] public float amount;
        public BuffDefinition buff;
        [Min(0.01f)] public float buffScale;
    }

    /// <summary>共享技能定义；仅保存编辑配置。等级、冷却、充能和蓄层属于持有者运行状态。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/技能定义")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [InspectorName("稳定 ID")] public string stableId;
        [InspectorName("显示名称")] public string displayName;
        [Header("组合技能")]
        [InspectorName("瞄准方式")] public AimMode aim=AimMode.Point;
        [InspectorName("阶段"), Tooltip("非空时使用正式组合模型；空数组仅用于旧独立示例兼容。")]
        public SkillStage[] stages=System.Array.Empty<SkillStage>();
        [InspectorName("最大充能"), Range(1,32)] public int maxCharges=1;
        [InspectorName("充能恢复（秒；0 使用冷却）"), Min(0)] public float rechargeSeconds;
        [InspectorName("所需普攻命中层数"), Min(0)] public int requiredHits;
        [Header("共有参数")]
        [InspectorName("施法距离（米）"), Min(0)] public float castRange=18;
        [InspectorName("冷却（秒）"), Min(0)] public float cooldown=4;
        [InspectorName("资源消耗"), Min(0)] public float manaCost=15;
        [Range(0,1), Tooltip("旧独立示例的每级效果增幅；当前 Game 组合技能固定一级，局内升级尚未接入。")] public float levelPower=0.25f;
        [InspectorName("指示器颜色")] public Color indicatorColor=Color.cyan;
        [Header("旧独立示例兼容（仅 stages 为空时使用）")]
        public SkillDelivery delivery;
        public TargetRelation targets;
        [Min(0.1f)] public float radius=5;
        [Min(0.1f)] public float areaDuration=5;
        [Min(0.05f)] public float areaInterval=0.25f;
        public EffectEntry[] effects;
    }
}

