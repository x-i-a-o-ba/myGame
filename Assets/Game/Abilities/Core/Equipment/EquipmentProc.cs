using System;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Abilities
{
    public enum EquipmentTrigger
    {
        [InspectorName("主普攻命中")] BasicHit,
        [InspectorName("主动技能施放成功")] AbilityCast
    }
    /// <summary>装备触发器的有限查询与效果装配；派生伤害不再派生装备事件。</summary>
    [Serializable]
    public sealed class EquipmentProc
    {
        public EquipmentTrigger trigger;
        [Range(0,1)] public float chance=1;
        [Min(.1f)] public float radius=8;
        [Range(1,32)] public int maxTargets=6;
        public bool chain,includePrimary=true;
        public EffectDefinition[] effects=Array.Empty<EffectDefinition>();
    }
    public struct EquipmentProcData
    {
        public EquipmentTrigger Trigger;
        public float Chance,Radius;
        public int MaxTargets;
        public bool Chain,IncludePrimary;
        public BlobArray<EffectData> Effects;
    }
}
