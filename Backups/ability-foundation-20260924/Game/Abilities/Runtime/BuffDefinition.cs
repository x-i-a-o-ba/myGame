using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>持续状态定义。首版按 Buff、来源实体和来源技能分组，共享到期时间。</summary>
    [CreateAssetMenu(menuName="Roguelike/Abilities/Buff")]
    public sealed class BuffDefinition : ScriptableObject
    {
        public string stableId,displayName;
        [Min(0), Tooltip("秒；0 为永久，移除被动/驱散/会话退出时撤销。")] public float duration=4;
        [Min(0.05f)] public float interval=1;
        [Min(0)] public float periodicDamage;
        [Range(-0.9f,3), Tooltip("加算移速百分比，例如 -0.35 = 减速 35%。")] public float moveSpeedPercent;
        public float attackDamageFlat;
        [Min(0)] public float healthRegen;
        public BuffStackRule stacking;
        [Range(1,20)] public int maxStacks=1;
        [Tooltip("常驻被动设为 false，须通过移除被动撤销。")] public bool dispellable=true;
    }
}
