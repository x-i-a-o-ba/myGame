using System;
using Roguelike.Abilities;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>遗物是共享战斗贡献的配置，归局内状态持有；没有按名称或 ID 分支的结算。</summary>
    [CreateAssetMenu(menuName="Roguelike/远征/遗物")]
    public sealed class RelicDefinition : ScriptableObject
    {
        public string stableId,displayName;
        [TextArea] public string description;
        public Texture2D icon;
        [Min(0)] public int extraBasicTargets;
        [Range(0,1)] public float echoChance,killHealMaxHealth;
        public ApplyBuffEffect skillHitBuff;
        [Min(1)] public float controlDurationMultiplier=1,bodyScale=1,maxHealthMultiplier=1,basicRangeMultiplier=1,eliteGoldMultiplier=1;
        [Range(0,60)] public float cooldownReduction;
        [Range(50,60)] public float cooldownCap=50;
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(displayName)) throw new InvalidOperationException("遗物缺少稳定编号或名称。");
            foreach(float value in new[]{echoChance,killHealMaxHealth,controlDurationMultiplier,bodyScale,maxHealthMultiplier,basicRangeMultiplier,eliteGoldMultiplier,cooldownReduction,cooldownCap})
                if(!float.IsFinite(value) || value<0) throw new InvalidOperationException("遗物数值必须有限且非负："+displayName);
            if(extraBasicTargets<0 || extraBasicTargets>32 || echoChance>1 || killHealMaxHealth>1 || cooldownCap<50 || cooldownCap>60)
                throw new InvalidOperationException("遗物目标数、概率或冷却上限越界："+displayName);
        }
    }
}
