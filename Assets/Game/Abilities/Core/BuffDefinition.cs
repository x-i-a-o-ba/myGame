using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>属性修饰项；按 (基础值 + Add) × (1 + Percent) × Multiply 统一聚合。</summary>
    [System.Serializable]
    public struct AttributeModifier
    {
        [InspectorName("属性")] public AttributeKind attribute;
        [InspectorName("运算")] public ModifierOperation operation;
        [InspectorName("数值"), Tooltip("Percent: 0.2 为 +20%；Multiply: 1 为不变。")] public float value;
    }

    /// <summary>持续状态共享定义。叠层、来源合并、属性修饰与控制贡献均编译为只读数据。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/Buff 定义")]
    public sealed class BuffDefinition : ScriptableObject
    {
        [Range(0,1), Tooltip("增加减伤比例，0.2 为 20%，最终上限 70%。")] public float damageReduction;
        [Range(0,1), Tooltip("每秒回复最大生命比例，0.02 为 2%。")] public float regenMaxHealth;
        [Range(0,3), Tooltip("额外承受伤害比例，0.3 为多受 30%。")] public float damageTaken;
        [Range(0,1), Tooltip("近战实际伤害反射比例；反射不再次反射。")] public float reflect;
        [Min(0)] public int extraTargets;
        public bool controlImmune,pierceBasics;
        [Min(0)] public float attackSplashRadius,attackSplashRatio;

        [Tooltip("Buff 数字 ID：5000–5999。正式模块必须登记；0 仅供旧配置兼容。")]
        public int configId;
        [InspectorName("稳定 ID")] public string stableId;
        [InspectorName("显示名称")] public string displayName;
        [InspectorName("持续时间（秒）"), Min(0), Tooltip("0 为永久，须显式移除或清理会话。")] public float duration=4;
        [InspectorName("周期（秒）"), Min(0.05f)] public float interval=1;
        [InspectorName("每次周期伤害"), Min(0)] public float periodicDamage;
        [Min(0),Tooltip("每跳额外伤害 = 施加时来源攻击 × 系数。")] public float periodicAttackRatio;
        [InspectorName("叠层策略")] public BuffStackRule stacking;
        [InspectorName("最大层数"), Range(1,20)] public int maxStacks=1;
        [InspectorName("可驱散")] public bool dispellable=true;
        [InspectorName("来源合并策略"), Tooltip("BySource 按 Buff + 施法者 + 来源技能合并；Independent 每次独立计时。")]
        public BuffSourcePolicy sourcePolicy=BuffSourcePolicy.BySource;
        [InspectorName("属性修饰")] public AttributeModifier[] modifiers=System.Array.Empty<AttributeModifier>();
        [InspectorName("控制限制")] public ControlFlags controls;
        [Header("旧独立示例兼容（会与修饰数组一起累计）")]
        [Range(-0.9f,3)] public float moveSpeedPercent;
        public float attackDamageFlat;
        [Min(0)] public float healthRegen;
    }
}
