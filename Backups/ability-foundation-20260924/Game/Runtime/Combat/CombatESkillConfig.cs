using NaughtyAttributes;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>E 的编辑期数值；只在指定落点造成范围伤害，不包含任何视野效果。</summary>
    [CreateAssetMenu(fileName = "E Frost Impact", menuName = "Roguelike/Game/Skills/E - 冰霜冲击")]
    public sealed class CombatESkillConfig : ScriptableObject
    {
        [Header("E 冰霜冲击（重开后生效）")]
        [Label("目标点最大距离（米）"), Min(0.1f), Tooltip("落点中心距主角的最大平面距离；超距时限制到边界；重开后生效。")]
        public float range = 25f;

        [Label("飞行速度（米/秒）"), Min(0.1f), Tooltip("飞抵目标点后才结算范围伤害；重开后生效。")]
        public float travelSpeed = 42f;

        [Label("范围半径（米）"), Min(0.1f), Tooltip("以实际落点为中心的平面伤害半径；重开后生效。")]
        public float areaRadius = 5f;

        [Label("范围伤害"), Min(0f), Tooltip("落地时对范围内每只存活怪物结算一次；没有视野或减速效果；重开后生效。")]
        public float damage = 18f;

        [Label("每次充能恢复时间（秒）"), Min(0.1f), Tooltip("充能按顺序恢复；重开后生效。")]
        public float chargeCooldown = 15f;

        [Label("最大充能次数"), Min(1), Tooltip("每次成功施放消耗一层充能；重开后生效。")]
        public int maximumCharges = 2;

        /// <summary>将配置校验后写入本局的纯数据快照，不修改此资源。</summary>
        public void Apply(ref CombatAbilitySettings settings)
        {
            settings.ERange = Mathf.Max(0.1f, range);
            settings.ETravelSpeed = Mathf.Max(0.1f, travelSpeed);
            settings.EAreaRadius = Mathf.Max(0.1f, areaRadius);
            settings.EDamage = Mathf.Max(0f, damage);
            settings.EChargeCooldown = Mathf.Max(0.1f, chargeCooldown);
            settings.EMaximumCharges = Mathf.Max(1, maximumCharges);
        }
    }
}
