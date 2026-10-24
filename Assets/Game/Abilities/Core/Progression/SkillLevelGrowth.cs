using System;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>技能等级解锁节奏。普通/终极两档按 Dota 2 默认门槛生成；自定义档读取逐级表。</summary>
    public enum SkillLevelTrack
    {
        [InspectorName("普通技能（4 级，英雄 1/3/5/7 级可学）")] Basic,
        [InspectorName("终极技能（3 级，英雄 6/10/14 级可学）")] Ultimate,
        [InspectorName("自定义逐级表")] Custom
    }

    /// <summary>一个技能等级的学习门槛与倍率。倍率都相对 1 级配置数值，1 表示不变。</summary>
    [Serializable]
    public sealed class SkillLevelRow
    {
        [Min(1), Tooltip("学习到这一级所需的最低英雄等级。")] public int requiredHeroLevel=1;
        [Min(0.01f)] public float radiusScale=1;
        [Min(0)] public int projectileBonus;
        [Min(0)] public float spreadBonus;
        [Min(0)] public int extraTargets;
        [Min(0), Tooltip("伤害与治疗倍率；1 表示与配置数值相同。")] public float effectScale=1;
        [Min(0), Tooltip("Buff 强度倍率：乘到修饰值、周期伤害和回复上，不改变持续时间与控制类型。")] public float buffScale=1;
        [Min(0.05f), Tooltip("冷却与充能恢复时间倍率；0.8 表示缩短 20%。")] public float cooldownScale=1;
        [Min(0), Tooltip("资源消耗倍率。")] public float manaCostScale=1;

        public SkillLevelRow Clone()=>(SkillLevelRow)MemberwiseClone();
    }

    /// <summary>
    /// 技能定义上的等级成长配置，只保存规则；某个英雄当前学到几级由 AbilitySlot.Level 保存。
    /// 1 级永远对应资源里的原始数值，因此旧资源和旧验证在 1 级下行为不变。
    /// </summary>
    [Serializable]
    public sealed class SkillLevelGrowth
    {
        public const int MaximumLevels=10;
        public static readonly int[] BasicGates={1,3,5,7};
        public static readonly int[] UltimateGates={6,10,14};

        [Tooltip("普通技能 4 级（英雄 1/3/5/7 级可学），终极技能 3 级（英雄 6/10/14 级可学），或使用自定义逐级表。")]
        public SkillLevelTrack track=SkillLevelTrack.Basic;
        [Min(0), Tooltip("每升 1 级增加的伤害/治疗倍率；0.9 表示 4 级为 1 级的 3.7 倍。")]
        public float effectPerLevel=0.9f;
        [Min(0), Tooltip("每升 1 级增加的 Buff 强度倍率；0.15 表示 4 级 Buff 修饰值为 1 级的 1.45 倍。")]
        public float buffPerLevel=0.15f;
        [Range(0,0.3f), Tooltip("每升 1 级缩短的冷却/充能比例；0.08 表示 4 级冷却为 1 级的 76%。")]
        public float cooldownReductionPerLevel=0.08f;
        [Min(0), Tooltip("每升 1 级增加的资源消耗比例。")]
        public float manaCostPerLevel=0.1f;
        [Tooltip("仅“自定义逐级表”使用；第 1 行对应技能 1 级。")]
        public SkillLevelRow[] customLevels=Array.Empty<SkillLevelRow>();

        /// <summary>生成逐级表并校验。失败抛出 InvalidOperationException，不返回半成品。</summary>
        public SkillLevelRow[] Resolve(string context)
        {
            if(!Enum.IsDefined(typeof(SkillLevelTrack),track)) throw new InvalidOperationException("未知技能成长类型："+context+" / "+track);
            if(track==SkillLevelTrack.Custom)
            {
                if(customLevels==null || customLevels.Length<1 || customLevels.Length>MaximumLevels)
                    throw new InvalidOperationException("自定义技能等级表必须有 1–"+MaximumLevels+" 行："+context);
                var rows=new SkillLevelRow[customLevels.Length];
                for(int i=0;i<rows.Length;i++)
                {
                    var row=customLevels[i];
                    if(row==null) throw new InvalidOperationException("自定义技能等级表存在空行："+context+" / 第 "+(i+1)+" 级");
                    Validate(row,context+" / 第 "+(i+1)+" 级");
                    if(i>0 && row.requiredHeroLevel<rows[i-1].requiredHeroLevel)
                        throw new InvalidOperationException("技能等级门槛必须逐级不降："+context+" / 第 "+(i+1)+" 级");
                    rows[i]=row.Clone();
                }
                return rows;
            }
            NonNegative(effectPerLevel,"伤害成长",context); NonNegative(buffPerLevel,"Buff 成长",context);
            NonNegative(cooldownReductionPerLevel,"冷却成长",context); NonNegative(manaCostPerLevel,"消耗成长",context);
            var gates=track==SkillLevelTrack.Ultimate?UltimateGates:BasicGates;
            if(1-cooldownReductionPerLevel*(gates.Length-1)<0.05f)
                throw new InvalidOperationException("冷却成长过大，最高等级冷却倍率低于 0.05："+context);
            var result=new SkillLevelRow[gates.Length];
            for(int i=0;i<gates.Length;i++)
                result[i]=new SkillLevelRow {requiredHeroLevel=gates[i],effectScale=1+effectPerLevel*i,buffScale=1+buffPerLevel*i,
                    cooldownScale=1-cooldownReductionPerLevel*i,manaCostScale=1+manaCostPerLevel*i};
            return result;
        }

        public SkillLevelGrowth Clone()
        {
            var copy=(SkillLevelGrowth)MemberwiseClone();
            copy.customLevels=new SkillLevelRow[customLevels?.Length??0];
            for(int i=0;i<copy.customLevels.Length;i++) copy.customLevels[i]=customLevels[i]?.Clone();
            return copy;
        }

        private static void Validate(SkillLevelRow row,string context)
        {
            if(row.requiredHeroLevel<1 || row.requiredHeroLevel>HeroProgressionRules.MaximumLevel)
                throw new InvalidOperationException("技能等级门槛必须在 1–"+HeroProgressionRules.MaximumLevel+"："+context);
            NonNegative(row.radiusScale,"范围倍率",context); NonNegative(row.spreadBonus,"散射增量",context);
            if(row.radiusScale<=0 || row.projectileBonus<0 || row.projectileBonus>31 || row.extraTargets<0 || row.extraTargets>32) throw new InvalidOperationException("逐级范围或目标数量非法："+context);
            NonNegative(row.effectScale,"伤害倍率",context); NonNegative(row.buffScale,"Buff 倍率",context);
            NonNegative(row.manaCostScale,"消耗倍率",context); NonNegative(row.cooldownScale,"冷却倍率",context);
            if(row.cooldownScale<0.05f) throw new InvalidOperationException("冷却倍率不能低于 0.05："+context);
        }
        private static void NonNegative(float value,string field,string context)
        {
            if(float.IsNaN(value) || float.IsInfinity(value) || value<0)
                throw new InvalidOperationException(field+"必须是有限的非负数："+context);
        }
    }

    /// <summary>编译后的单级数据，存放在 SkillData.Levels。</summary>
    public struct SkillLevelData
    {
        public int RequiredHeroLevel;
        public float RadiusScale,SpreadBonus; public int ProjectileBonus,ExtraTargets;
        public float EffectScale,BuffScale,CooldownScale,ManaCostScale;
    }

    /// <summary>执行器、HUD 与学习规则共用的等级查询；没有等级表的旧库按 1 级处理。</summary>
    public static class SkillLevels
    {
        /// <summary>技能冷却缩减的上限，避免天赋叠加后冷却归零。</summary>
        public const float MaximumCooldownReduction=0.6f;
        public static SkillLevelData Identity=>new SkillLevelData {RequiredHeroLevel=1,RadiusScale=1,EffectScale=1,BuffScale=1,CooldownScale=1,ManaCostScale=1};

        public static int MaxLevel(ref SkillData skill)=>math.max(1,skill.Levels.Length);

        /// <summary>等级被限制在 1..最高级；0 级（未学习）只用于学习规则，不会进入效果结算。</summary>
        public static SkillLevelData Row(ref SkillData skill,int level)
        {
            if(skill.Levels.Length==0) return Identity;
            return skill.Levels[math.clamp(level,1,skill.Levels.Length)-1];
        }
        /// <summary>学习下一级所需英雄等级；已满级返回 int.MaxValue。</summary>
        public static int RequiredHeroLevel(ref SkillData skill,int nextLevel)
        {
            if(nextLevel>MaxLevel(ref skill)) return int.MaxValue;
            return skill.Levels.Length==0?1:skill.Levels[math.max(1,nextLevel)-1].RequiredHeroLevel;
        }
        public static float ManaCost(ref SkillData skill,int level)=>skill.ManaCost*Row(ref skill,level).ManaCostScale;
        public static float CooldownScale(ref SkillData skill,int level,in AbilityUnit owner)
            =>Row(ref skill,level).CooldownScale*(1-math.clamp(owner.CooldownReduction,0,MaximumCooldownReduction));
    }
}
