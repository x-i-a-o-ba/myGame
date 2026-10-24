using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>即时伤害模块；伤害写入统一结算入口，不生成零时长 Buff。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/效果/伤害")]
    public sealed class DamageEffect : EffectDefinition
    {
        [InspectorName("伤害值"), Min(0)] public float amount=10;
        [Min(0), Tooltip("额外加上来源当前攻击 × 此系数。")] public float sourceAttackRatio;
        [Range(0,1), Tooltip("额外加上目标最大生命 × 此系数。")] public float targetMaxHealthRatio;
        public override EffectData Compile() => new EffectData { Kind=EffectKind.Damage, Amount=amount, SourceAttackRatio=sourceAttackRatio,TargetMaxHealthRatio=targetMaxHealthRatio,Target=target, Buff=-1 };
    }
}
