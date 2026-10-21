using NaughtyAttributes;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>W 的编辑期数值；扇形指示器与 ECS 结算应使用同一会话快照。</summary>
    [CreateAssetMenu(fileName = "W Volley", menuName = "Roguelike/Game/Skills/W - 万箭齐发")]
    public sealed class CombatWSkillConfig : ScriptableObject
    {
        [Header("W 万箭齐发（重开后生效）")]
        [Label("箭矢数量"), Range(1, 21), Tooltip("扇形内均匀发出的箭矢条数；重开后生效。")]
        public int arrowCount = 9;

        [Label("射程（米）"), Min(0.1f), Tooltip("从主角向瞄准方向延伸的最远距离；重开后生效。")]
        public float range = 22f;

        [Label("扇形半角（度）"), Range(1f, 89f), Tooltip("左右各展开此角度；重开后生效。")]
        public float halfAngleDegrees = 35f;

        [Label("单箭命中半径（米）"), Min(0.05f), Tooltip("每条箭矢与怪物中心的平面碰撞容差；重开后生效。")]
        public float arrowHitRadius = 1f;

        [Label("命中伤害"), Min(0f), Tooltip("每只怪物每次 W 至多受到一次伤害；当前 Game 场景原值为 30；重开后生效。")]
        public float damage = 30f;

        [Label("减速后移速倍率"), Range(0.05f, 1f), Tooltip("0.65 表示怪物移动速度降为原来的 65%；重开后生效。")]
        public float slowMultiplier = 0.65f;

        [Label("减速时长（秒）"), Min(0f), Tooltip("仅存活的命中怪物可观察到减速；重开后生效。")]
        public float slowDuration = 2.5f;

        [Label("冷却时间（秒）"), Min(0f), Tooltip("成功施放时开始计时；重开后生效。")]
        public float cooldown = 5f;

        /// <summary>将配置校验后写入本局的纯数据快照，不修改此资源。</summary>
        public void Apply(ref CombatAbilitySettings settings)
        {
            settings.WArrowCount = Mathf.Clamp(arrowCount, 1, 21);
            settings.WRange = Mathf.Max(0.1f, range);
            settings.WHalfAngleDegrees = Mathf.Clamp(halfAngleDegrees, 1f, 89f);
            settings.WArrowHitRadius = Mathf.Max(0.05f, arrowHitRadius);
            settings.WDamage = Mathf.Max(0f, damage);
            settings.WSlowMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
            settings.WSlowDuration = Mathf.Max(0f, slowDuration);
            settings.WCooldown = Mathf.Max(0f, cooldown);
        }
    }
}
