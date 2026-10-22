using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>天赋树一侧的单个选项。</summary>
    [Serializable]
    public struct TalentOption
    {
        [Tooltip("界面显示文字；留空时按属性和数值自动生成。")] public string displayName;
        [Tooltip("被修改的英雄属性；与装备共用同一套属性，直接相加。")] public StatKind stat;
        [Tooltip("增加的数值；百分比类属性填写百分点（25 = +25%）。")] public float value;

        public TalentOption(string displayName,StatKind stat,float value)
        { this.displayName=displayName; this.stat=stat; this.value=value; }

        public string Label=>string.IsNullOrWhiteSpace(displayName)?Describe(stat,value):displayName;

        public static string Describe(StatKind stat,float value)=>StatText.Format(stat,value);
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
            new HeroTalentTier(10,new TalentOption("+20 攻击力",StatKind.AttackDamage,20),new TalentOption("+25% 经验获取",StatKind.ExperienceGain,25)),
            new HeroTalentTier(15,new TalentOption("+25% 攻击速度",StatKind.AttackSpeed,25),new TalentOption("+250 最大生命",StatKind.MaxHealth,250)),
            new HeroTalentTier(20,new TalentOption("+30% 技能伤害",StatKind.SkillAmplify,30),new TalentOption("-20% 技能冷却",StatKind.CooldownReduction,20)),
            new HeroTalentTier(25,new TalentOption("+60 攻击力",StatKind.AttackDamage,60),new TalentOption("+10 生命回复",StatKind.HealthRegen,10))
        };
    }
}
