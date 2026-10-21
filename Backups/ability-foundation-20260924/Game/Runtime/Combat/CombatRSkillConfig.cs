using NaughtyAttributes;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>R 的编辑期数值；首怪命中后爆炸，所有 Game 怪物类型都可被命中。</summary>
    [CreateAssetMenu(fileName = "R Crystal Arrow", menuName = "Roguelike/Game/Skills/R - 魔法水晶箭")]
    public sealed class CombatRSkillConfig : ScriptableObject
    {
        [Header("R 魔法水晶箭（重开后生效）")]
        [Label("最大飞行距离（米）"), Min(0.1f), Tooltip("箭矢按瞄准方向飞行，命中首只怪物后爆炸；重开后生效。")]
        public float range = 150f;

        [Label("飞行速度（米/秒）"), Min(0.1f), Tooltip("箭矢飞行速度；重开后生效。")]
        public float speed = 48f;

        [Label("命中半径（米）"), Min(0.1f), Tooltip("箭矢路径与怪物中心的平面碰撞容差；重开后生效。")]
        public float hitRadius = 1.15f;

        [Label("爆炸半径（米）"), Min(0.1f), Tooltip("以第一只命中怪物为中心的范围效果半径；重开后生效。")]
        public float explosionRadius = 7f;

        [Label("首怪额外伤害"), Min(0f), Tooltip("第一只命中怪物在范围伤害之外额外承受此值；重开后生效。")]
        public float impactDamage = 20f;

        [Label("范围伤害"), Min(0f), Tooltip("爆炸范围内每只存活怪物承受一次；重开后生效。")]
        public float explosionDamage = 16f;

        [Label("眩晕时长（秒）"), Min(0f), Tooltip("爆炸范围内幸存怪物停止移动与攻击；重开后生效。")]
        public float stunDuration = 1.5f;

        [Label("减速后移速倍率"), Range(0.05f, 1f), Tooltip("0.5 表示怪物移动速度降为原来的 50%；重开后生效。")]
        public float slowMultiplier = 0.5f;

        [Label("减速时长（秒）"), Min(0f), Tooltip("爆炸范围内幸存怪物的减速时长；重开后生效。")]
        public float slowDuration = 3f;

        [Label("冷却时间（秒）"), Min(0f), Tooltip("成功发射时开始计时，即使射空也不返还；重开后生效。")]
        public float cooldown = 20f;

        /// <summary>将配置校验后写入本局的纯数据快照，不修改此资源。</summary>
        public void Apply(ref CombatAbilitySettings settings)
        {
            settings.RRange = Mathf.Max(0.1f, range);
            settings.RSpeed = Mathf.Max(0.1f, speed);
            settings.RHitRadius = Mathf.Max(0.1f, hitRadius);
            settings.RExplosionRadius = Mathf.Max(0.1f, explosionRadius);
            settings.RImpactDamage = Mathf.Max(0f, impactDamage);
            settings.RExplosionDamage = Mathf.Max(0f, explosionDamage);
            settings.RStunDuration = Mathf.Max(0f, stunDuration);
            settings.RSlowMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
            settings.RSlowDuration = Mathf.Max(0f, slowDuration);
            settings.RCooldown = Mathf.Max(0f, cooldown);
        }
    }
}
