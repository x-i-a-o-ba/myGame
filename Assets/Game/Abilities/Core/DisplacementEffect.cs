using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>由执行器移动目标 Transform；控制免疫抵抗位移，拉拽保留配置的接触距离。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/效果/拉拽与击退")]
    public sealed class DisplacementEffect : EffectDefinition
    {
        public bool pull=true;
        [Min(0),Tooltip("最大移动距离（米）；拉拽不会越过施法者。")] public float distance=10;
        [Min(0),Tooltip("拉拽后的最小中心距离（米），不会把目标堆到来源脚下。")] public float stopDistance=1;
        public override EffectData Compile()
        {
            if(!float.IsFinite(distance) || distance<0 || !float.IsFinite(stopDistance) || stopDistance<0) throw new System.InvalidOperationException("位移距离和拉拽保留距离必须有限且非负。");
            return new EffectData{Kind=pull?EffectKind.Pull:EffectKind.Knockback,Amount=distance,StopDistance=stopDistance,Target=target,Buff=-1};
        }
    }
}
