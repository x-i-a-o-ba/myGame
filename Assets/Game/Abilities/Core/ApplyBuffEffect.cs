using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>引用共享 Buff 的应用模块，倍率属于组合而非 Buff 运行状态。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/效果/施加 Buff")]
    public sealed class ApplyBuffEffect : EffectDefinition
    {
        [InspectorName("Buff 定义")] public BuffDefinition buff;
        [InspectorName("强度倍率"), Min(0.01f)] public float buffScale=1;
        public override BuffDefinition ReferencedBuff => buff;
        public override EffectData Compile() => new EffectData { Kind=EffectKind.ApplyBuff, Buff=-1, BuffScale=buffScale, Target=target };
    }
}
