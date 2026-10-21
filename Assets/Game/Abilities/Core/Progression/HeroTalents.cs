using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>天赋可修改的英雄属性。每个值都由 HeroProgressionLogic.ApplyTalent 写入 AbilityUnit 或 HeroProgression。</summary>
    public enum TalentStat
    {
        [InspectorName("最大生命（点）")] MaxHealth,
        [InspectorName("攻击力（点）")] AttackDamage,
        [InspectorName("攻击速度（小数，0.2 = +20%）")] AttackSpeed,
        [InspectorName("移动速度（米/秒）")] MoveSpeed,
        [InspectorName("生命回复（点/秒）")] HealthRegen,
        [InspectorName("技能伤害增强（小数，0.3 = +30%）")] SkillAmplify,
        [InspectorName("技能冷却缩减（小数，0.2 = -20%）")] CooldownReduction,
        [InspectorName("经验获取（小数，0.25 = +25%）")] ExperienceGain
    }

    /// <summary>天赋树一侧的单个选项。</summary>
    [Serializable]
    public struct TalentOption
    {
        [Tooltip("界面显示文字；留空时按属性和数值自动生成。")] public string displayName;
        [Tooltip("被修改的英雄属性。")] public TalentStat stat;
        [Tooltip("增加的数值；百分比类属性填写小数。")] public float value;

        public TalentOption(string displayName,TalentStat stat,float value)
        { this.displayName=displayName; this.stat=stat; this.value=value; }

        public string Label=>string.IsNullOrWhiteSpace(displayName)?Describe(stat,value):displayName;

        public static string Describe(TalentStat stat,float value)
        {
            switch(stat)
            {
                case TalentStat.MaxHealth: return $"+{value:0} 最大生命";
                case TalentStat.AttackDamage: return $"+{value:0} 攻击力";
                case TalentStat.AttackSpeed: return $"+{value*100:0}% 攻击速度";
                case TalentStat.MoveSpeed: return $"+{value:0.#} 移动速度";
                case TalentStat.HealthRegen: return $"+{value:0.#} 生命回复";
                case TalentStat.SkillAmplify: return $"+{value*100:0}% 技能伤害";
                case TalentStat.CooldownReduction: return $"-{value*100:0}% 技能冷却";
                case TalentStat.ExperienceGain: return $"+{value*100:0}% 经验获取";
            }
            return stat+" "+value;
        }
    }

    /// <summary>Dota 2 式天赋层：到达指定英雄等级后二选一，选择不消耗技能点。</summary>
    [Serializable]
    public sealed class HeroTalentTier
    {
        [Min(1), Tooltip("解锁这一层天赋的英雄等级。")] public int heroLevel=10;
        [Tooltip("左侧选项（按 1 选择）。")] public TalentOption left;
        [Tooltip("右侧选项（按 2 选择）。")] public TalentOption right;

        public HeroTalentTier() { }
        public HeroTalentTier(int heroLevel,TalentOption left,TalentOption right)
        { this.heroLevel=heroLevel; this.left=left; this.right=right; }

        /// <summary>寒冰射手默认天赋树：10/15/20/25 级四层。</summary>
        public static HeroTalentTier[] Defaults()=>new[]
        {
            new HeroTalentTier(10,new TalentOption("+20 攻击力",TalentStat.AttackDamage,20),new TalentOption("+25% 经验获取",TalentStat.ExperienceGain,0.25f)),
            new HeroTalentTier(15,new TalentOption("+25% 攻击速度",TalentStat.AttackSpeed,0.25f),new TalentOption("+250 最大生命",TalentStat.MaxHealth,250)),
            new HeroTalentTier(20,new TalentOption("+30% 技能伤害",TalentStat.SkillAmplify,0.3f),new TalentOption("-20% 技能冷却",TalentStat.CooldownReduction,0.2f)),
            new HeroTalentTier(25,new TalentOption("+60 攻击力",TalentStat.AttackDamage,60),new TalentOption("+10 生命回复",TalentStat.HealthRegen,10))
        };
    }
}
