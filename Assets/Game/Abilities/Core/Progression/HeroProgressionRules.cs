using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 英雄经验与等级规则，只保存配置。默认值取自 Dota 2 当前经验表（30 级累计 63900 经验），
    /// 每级获得 1 个技能点；天赋独立选择，不消耗技能点。运行中的等级与经验归 HeroProgression 组件。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/成长/经验与等级规则")]
    public sealed class HeroProgressionRules : ScriptableObject
    {
        public const int MaximumLevel=100;
        [Range(1,MaximumLevel), Tooltip("英雄最高等级。Dota 2 默认 30。")]
        public int maxLevel=15;
        [Tooltip("第 1 项是 1→2 级所需经验，第 2 项是 2→3 级所需经验，依此类推；数量至少为最高等级 - 1。")]
        public int[] experienceToNextLevel=ExpeditionExperienceTable();
        [Min(0), Tooltip("开局时拥有的技能点。Dota 2 英雄 1 级有 1 点。")]
        public int startingSkillPoints=1;
        [Min(0), Tooltip("每升 1 级获得的技能点。")]
        public int skillPointsPerLevel=1;

        public static int[] ExpeditionExperienceTable()=>new[]{15,45,80,130,190,280,480,800,1200,1800,2600,3600,4300,4500};

        /// <summary>旧版 30 级数据，只供历史内容迁移。</summary>
        public static int[] DotaExperienceTable()=>new[]
        {
            240,400,520,600,680,760,800,900,1000,1100,
            1200,1300,1400,1500,1600,1700,1800,1900,2000,2200,
            2400,2600,2800,3000,4000,5000,6000,7000,7500
        };

        /// <summary>返回到达每一级所需的累计经验，索引 0 对应 1 级（0 经验）。配置非法时抛出异常。</summary>
        public int[] CumulativeExperience()
        {
            if(maxLevel<1 || maxLevel>MaximumLevel) throw new InvalidOperationException("最高等级必须在 1–"+MaximumLevel+"："+name);
            if(startingSkillPoints<0 || skillPointsPerLevel<0 || startingSkillPoints>1000 || skillPointsPerLevel>100)
                throw new InvalidOperationException("技能点配置超出范围："+name);
            if(experienceToNextLevel==null || experienceToNextLevel.Length<maxLevel-1)
                throw new InvalidOperationException("经验表数量不足，至少需要 "+(maxLevel-1)+" 项："+name);
            var result=new int[maxLevel];
            long total=0;
            for(int level=1;level<maxLevel;level++)
            {
                int step=experienceToNextLevel[level-1];
                if(step<=0) throw new InvalidOperationException("每级所需经验必须大于 0："+name+" / "+level+"→"+(level+1));
                total+=step;
                if(total>10000000) throw new InvalidOperationException("累计经验超过 10000000："+name);
                result[level]=(int)total;
            }
            return result;
        }
    }
}
