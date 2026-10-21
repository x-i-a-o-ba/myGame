using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>即时治疗模块，不复活已经死亡的目标。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/效果/治疗")]
    public sealed class HealEffect : EffectDefinition
    {
        [InspectorName("治疗值"), Min(0)] public float amount=10;
        public override EffectData Compile() => new EffectData { Kind=EffectKind.Heal, Amount=amount, Target=target, Buff=-1 };
    }
}
