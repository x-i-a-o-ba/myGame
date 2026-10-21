using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>移除目标身上标记为可驱散的状态；保留不可驱散贡献。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/效果/驱散")]
    public sealed class DispelEffect : EffectDefinition
    {
        public override EffectData Compile() => new EffectData { Kind=EffectKind.Dispel, Target=target, Buff=-1 };
    }
}
