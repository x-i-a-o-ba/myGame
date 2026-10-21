using UnityEngine;

namespace Roguelike.Abilities
{
    // Historical values remain stable for serialized sample assets.
    public enum SkillDelivery { InstantCircle, PersistentCircle, SelfCircle }
    public enum TargetRelation { Enemies, Allies }
    public enum EffectKind { Damage, Heal, ApplyBuff, Dispel }
    public enum BuffStackRule { Refresh, AddAndRefresh }
    public enum PassiveTrigger { WhileLearned, BasicAttackHit }

    public enum AimMode { Self, Point, Direction, Unit }
    public enum TargetScope { Self, Single, Area, All }
    public enum AreaShape { Circle, Cone, Line }
    public enum DeliveryKind { Instant, PointProjectile, LinearProjectile }
    public enum EffectTarget { Selected, Source }
    public enum BuffSourcePolicy { ByDefinition, BySource, Independent }
    public enum AttributeKind { MoveSpeed, AttackDamage, AttackSpeed }
    public enum ModifierOperation { Add, Percent, Multiply }
    [System.Flags]
    public enum ControlFlags { None=0, MovementBlocked=1, AttackBlocked=2, CastBlocked=4 }

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
    [CreateAssetMenu(menuName="Roguelike/Abilities/Skill")]
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

