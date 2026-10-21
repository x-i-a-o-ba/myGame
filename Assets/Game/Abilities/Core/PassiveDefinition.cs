using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>被动只定义触发入口，共用效果执行；周期伤害不会产生普攻命中事件。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/旧示例被动")]
    public sealed class PassiveDefinition : ScriptableObject
    {
        public string stableId,displayName;
        public PassiveTrigger trigger;
        public EffectEntry[] effects;
    }
}
