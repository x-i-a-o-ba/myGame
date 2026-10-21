using NaughtyAttributes;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>Q 的编辑期数值；Game 重开时复制到 ECS 会话，资源本身不保存层数或冷却。</summary>
    [CreateAssetMenu(fileName = "Q Frost Focus", menuName = "Roguelike/Game/Skills/Q - 冰霜专注")]
    public sealed class CombatQSkillConfig : ScriptableObject
    {
        [Header("Q 冰霜专注（重开后生效）")]
        [Label("蓄满所需普攻命中次数"), Min(1), Tooltip("成功命中活着的怪物才蓄一层；重开后生效。")]
        public int requiredHits = 4;

        [Label("攻速强化时长（秒）"), Min(0.1f), Tooltip("满层施放后持续提高普攻速度；重开后生效。")]
        public float duration = 5f;

        [Label("冷却时间（秒）"), Min(0f), Tooltip("成功施放时开始计时；重开后生效。")]
        public float cooldown = 7f;

        [Label("普攻速度倍率"), Min(1f), Tooltip("只增加攻速，不增加箭矢数量；重开后生效。")]
        public float attackSpeedMultiplier = 1.6f;

        /// <summary>将配置校验后写入本局的纯数据快照，不修改此资源。</summary>
        public void Apply(ref CombatAbilitySettings settings)
        {
            settings.QRequiredHits = Mathf.Max(1, requiredHits);
            settings.QDuration = Mathf.Max(0.1f, duration);
            settings.QCooldown = Mathf.Max(0f, cooldown);
            settings.QAttackSpeedMultiplier = Mathf.Max(1f, attackSpeedMultiplier);
        }
    }
}
