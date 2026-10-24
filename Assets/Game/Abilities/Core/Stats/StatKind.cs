using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 英雄属性。所有来源（基础、等级成长、天赋、装备）都直接相加，不做力量/敏捷/智力式换算；
    /// 百分比类属性填写百分点：25 表示 +25%。序号 0–7 与旧天赋属性一致，已有资源无需迁移枚举值。
    /// </summary>
    public enum StatKind
    {
        [InspectorName("最大生命（点）")] MaxHealth,
        [InspectorName("攻击力（点）")] AttackDamage,
        [InspectorName("攻击速度（%，25 = +25%）")] AttackSpeed,
        [InspectorName("移动速度（米/秒）")] MoveSpeed,
        [InspectorName("生命回复（点/秒）")] HealthRegen,
        [InspectorName("技能强度（%，30 = +30%）")] SkillAmplify,
        [InspectorName("冷却缩减（%，20 = -20% 冷却，上限 50（遗物可到 60））")] CooldownReduction,
        [InspectorName("已停用：经验获取（%，25 = +25%）")] ExperienceGain,
        [InspectorName("最大法力（点）")] MaxMana,
        [InspectorName("法力回复（点/秒）")] ManaRegen,
        [InspectorName("已停用：攻击距离（米）")] AttackRange,
        [InspectorName("伤害减免（%，上限 70）")] DamageReduction,
        [InspectorName("普攻吸血（%）")] LifeSteal,
        [InspectorName("已停用：暴击几率（%，普攻）")] CriticalChance,
        [InspectorName("已停用：暴击伤害（%，暴击时的伤害倍率）")] CriticalDamage
    }

    /// <summary>一条属性加成：直接加到对应属性上。装备、天赋和英雄额外基础属性共用。</summary>
    [Serializable]
    public struct StatModifier
    {
        [Tooltip("被修改的属性。")] public StatKind stat;
        [Tooltip("直接相加的数值；百分比类属性填写百分点（25 = +25%），可以为负数。")] public float value;
        public StatModifier(StatKind stat,float value) { this.stat=stat; this.value=value; }
        public override string ToString()=>StatText.Format(stat,value);
    }

    /// <summary>属性的中文名称、单位与格式化。只用于界面与文档，不参与结算。</summary>
    public static class StatText
    {
        /// <summary>完整属性提示的显示顺序（生存 → 攻击 → 技能 → 其他）。</summary>
        public static readonly StatKind[] DisplayOrder=
        {
            StatKind.MaxHealth,StatKind.HealthRegen,StatKind.MaxMana,StatKind.ManaRegen,
            StatKind.AttackDamage,StatKind.AttackSpeed,StatKind.LifeSteal,
            StatKind.SkillAmplify,StatKind.CooldownReduction,StatKind.DamageReduction,StatKind.MoveSpeed
        };

        public static bool IsCurrent(StatKind stat)=>System.Array.IndexOf(DisplayOrder,stat)>=0;

        public static string Name(StatKind stat)
        {
            switch(stat)
            {
                case StatKind.MaxHealth: return "最大生命";
                case StatKind.AttackDamage: return "攻击力";
                case StatKind.AttackSpeed: return "攻击速度";
                case StatKind.MoveSpeed: return "移动速度";
                case StatKind.HealthRegen: return "生命回复";
                case StatKind.SkillAmplify: return "技能强度";
                case StatKind.CooldownReduction: return "冷却缩减";
                case StatKind.ExperienceGain: return "经验获取";
                case StatKind.MaxMana: return "最大法力";
                case StatKind.ManaRegen: return "法力回复";
                case StatKind.AttackRange: return "攻击距离";
                case StatKind.DamageReduction: return "伤害减免";
                case StatKind.LifeSteal: return "普攻吸血";
                case StatKind.CriticalChance: return "暴击几率";
                case StatKind.CriticalDamage: return "暴击伤害";
            }
            return stat.ToString();
        }

        /// <summary>百分比类属性（数值为百分点）。</summary>
        public static bool IsPercent(StatKind stat)
            =>stat==StatKind.AttackSpeed || stat==StatKind.SkillAmplify || stat==StatKind.CooldownReduction || stat==StatKind.ExperienceGain ||
              stat==StatKind.DamageReduction || stat==StatKind.LifeSteal || stat==StatKind.CriticalChance || stat==StatKind.CriticalDamage;

        /// <summary>数值本身（不带符号）：百分比加 %，移动速度与攻击距离带“米”单位的由调用方决定。</summary>
        public static string Value(StatKind stat,float value)=>Number(value)+(IsPercent(stat)?"%":"");

        /// <summary>加成文字，例如“+25% 攻击速度”“+20 攻击力”“-10 移动速度”。</summary>
        public static string Format(StatKind stat,float value)=>(value<0?"-":"+")+Value(stat,Mathf.Abs(value))+" "+Name(stat);

        public static string Number(float value)=>value.ToString(Mathf.Abs(value-Mathf.Round(value))<0.005f?"0":"0.##");
    }
}
