using System;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Game 关卡难度曲线，只保存配置。每隔固定秒数刷一波，数量、生命、伤害、移速和经验按波次增长；
    /// 定期出现精英与首领，击杀最终波首领获胜。运行状态归会话实体的 CombatWaveState。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/成长/关卡难度曲线")]
    public sealed class CombatDifficultyDefinition : ScriptableObject
    {
        [Header("节奏")]
        [Min(1), Tooltip("两波之间的间隔（秒）。")] public float waveInterval=20;
        [Min(0), Tooltip("第一波出现的时间（秒），0 表示开局立即出现。")] public float firstWaveAt=0;
        [Min(0), Tooltip("最终波编号：这一波刷出最终首领，击杀即胜利；0 表示无尽模式。")] public int finalWave=30;
        [Min(1), Tooltip("刷怪圆环宽度（米）：敌人出现在玩家周围“安全半径”到“安全半径+宽度”之间。")] public float spawnRingWidth=25;
        [Header("普通敌人")]
        [Min(0), Tooltip("第 1 波普通敌人数量。")] public int baseCount=80;
        [Min(0), Tooltip("每波数量线性增长比例：数量 = 基础 ×（1 + 比例 ×（波次 - 1））。")] public float countGrowthPerWave=0.15f;
        [Min(1), Tooltip("单波普通敌人上限。")] public int maxPerWave=2000;
        [Min(1), Tooltip("每波生命倍率（复利）：生命 = 基础 × 倍率^(波次 - 1)。")] public float healthGrowth=1.07f;
        [Min(1), Tooltip("每波伤害倍率（复利）。")] public float damageGrowth=1.05f;
        [Min(0), Tooltip("每波增加的移动速度（米/秒）。")] public float speedPerWave=0.03f;
        [Min(0), Tooltip("普通敌人移动速度上限（米/秒）。")] public float maxSpeed=6;
        [Min(0), Tooltip("第 1 波普通敌人的击杀经验。")] public float baseExperience=3;
        [Min(1), Tooltip("每波经验倍率（复利）。")] public float experienceGrowth=1.02f;
        [Header("精英")]
        [Min(0), Tooltip("每隔几波出现精英；0 表示不出现。")] public int eliteEvery=5;
        [Min(0), Tooltip("精英基础数量。")] public int eliteBaseCount=3;
        [Min(1), Tooltip("每隔几波精英数量 +1。")] public int eliteWavesPerExtra=5;
        [Min(1), Tooltip("精英相对同波普通敌人的生命倍率。")] public float eliteHealth=10;
        [Min(1), Tooltip("精英伤害倍率。")] public float eliteDamage=2.5f;
        [Min(0.1f), Tooltip("精英体型倍率。")] public float eliteScale=1.6f;
        [Min(0), Tooltip("精英经验倍率。")] public float eliteExperience=12;
        [Header("首领")]
        [Min(0), Tooltip("每隔几波出现首领；最终波总会出现首领。0 表示只在最终波出现。")] public int bossEvery=10;
        [Min(1), Tooltip("首领生命倍率。")] public float bossHealth=60;
        [Min(1), Tooltip("首领伤害倍率。")] public float bossDamage=5;
        [Min(0.1f), Tooltip("首领体型倍率。")] public float bossScale=2.6f;
        [Min(0), Tooltip("首领经验倍率。")] public float bossExperience=80;
        [Min(0), Tooltip("首领移动速度倍率。")] public float bossSpeed=0.8f;

        /// <summary>校验并生成 ECS 快照。数值非法时抛出 InvalidOperationException。</summary>
        public CombatWaveSettings ToSettings()
        {
            float[] values={waveInterval,firstWaveAt,spawnRingWidth,countGrowthPerWave,healthGrowth,damageGrowth,speedPerWave,maxSpeed,
                baseExperience,experienceGrowth,eliteHealth,eliteDamage,eliteScale,eliteExperience,bossHealth,bossDamage,bossScale,bossExperience,bossSpeed};
            foreach(float value in values)
                if(float.IsNaN(value) || float.IsInfinity(value) || value<0) throw new InvalidOperationException("难度曲线数值必须是有限的非负数："+name);
            if(waveInterval<1 || spawnRingWidth<1) throw new InvalidOperationException("波次间隔和刷怪圆环宽度至少为 1："+name);
            if(healthGrowth<1 || damageGrowth<1 || experienceGrowth<1) throw new InvalidOperationException("逐波倍率不能小于 1，难度必须随时间上升："+name);
            if(baseCount<0 || maxPerWave<1 || finalWave<0 || eliteEvery<0 || eliteBaseCount<0 || eliteWavesPerExtra<1 || bossEvery<0)
                throw new InvalidOperationException("难度曲线整数参数超出范围："+name);
            return new CombatWaveSettings {WaveInterval=waveInterval,FirstWaveAt=firstWaveAt,SpawnRingWidth=spawnRingWidth,
                BaseCount=baseCount,CountGrowth=countGrowthPerWave,MaxPerWave=maxPerWave,
                HealthGrowth=healthGrowth,DamageGrowth=damageGrowth,SpeedPerWave=speedPerWave,MaxSpeed=maxSpeed,
                BaseExperience=baseExperience,ExperienceGrowth=experienceGrowth,
                EliteEvery=eliteEvery,EliteBaseCount=eliteBaseCount,EliteWavesPerExtra=eliteWavesPerExtra,
                EliteHealth=eliteHealth,EliteDamage=eliteDamage,EliteScale=eliteScale,EliteExperience=eliteExperience,
                BossEvery=bossEvery,BossHealth=bossHealth,BossDamage=bossDamage,BossScale=bossScale,BossExperience=bossExperience,BossSpeed=bossSpeed,
                FinalWave=finalWave};
        }
    }
}
