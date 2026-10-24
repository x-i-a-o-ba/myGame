using System;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>随远征累计强度时间（分钟）增长的只读数值；刷怪节奏归刷怪导演资源。</summary>
    [CreateAssetMenu(menuName="Roguelike/成长/关卡难度曲线")]
    public sealed class CombatDifficultyDefinition : ScriptableObject
    {
        [Min(1), Tooltip("t=0 的杂兵生命（点），重开生效。")] public float baseEnemyHealth=30;
        [Min(0), Tooltip("t=0 的杂兵攻击力（点），重开生效。")] public float baseEnemyDamage=6;
        [Min(0), Tooltip("t=0 的杂兵移速（米/秒），重开生效。")] public float baseEnemySpeed=3.5f;
        [Min(1), Tooltip("每分钟复利生命倍率：基础生命 × 倍率^t。")] public float healthGrowth=1.15f;
        [Min(1), Tooltip("每分钟复利攻击倍率：基础攻击 × 倍率^t。")] public float damageGrowth=1.07f;
        [Min(0), Tooltip("每分钟增加的移速（米/秒）。")] public float speedPerMinute=0.08f;
        [Min(0), Tooltip("杂兵基准移速上限（米/秒），再乘种类倍率。")] public float maxSpeed=4.7f;
        [Min(0), Tooltip("杂兵击杀经验，种类再乘倍率。")] public float baseExperience=1;
        [Min(0), Tooltip("杂兵击杀金币，种类再乘倍率。")] public float baseGold=0.5f;
        [Min(1)] public float eliteHealth=15;
        [Min(1)] public float eliteDamage=2;
        [Min(0.1f)] public float eliteScale=1.5f;
        [Min(0)] public float eliteExperience=30;
        [Min(0)] public float eliteGold=30;
        [Tooltip("第 1/2/3 关首领的绝对生命（点），不乘种类倍率。")] public Vector3 bossHealth=new Vector3(6000,25000,100000);
        [Min(1)] public float bossDamage=5;
        [Min(0.1f)] public float bossScale=2.6f;
        [Tooltip("第 1/2/3 关首领的绝对击杀经验。")] public Vector3 bossExperience=new Vector3(150,300,0);
        [Tooltip("第 1/2/3 关首领的绝对金币奖励。")] public Vector3 bossGold=new Vector3(500,800,0);
        [Min(0)] public float bossSpeed=0.8f;

        public CombatDifficultySettings ToSettings()
        {
            float[] values={baseEnemyHealth,baseEnemyDamage,baseEnemySpeed,healthGrowth,damageGrowth,speedPerMinute,maxSpeed,
                baseExperience,baseGold,eliteHealth,eliteDamage,eliteScale,eliteExperience,eliteGold,
                bossHealth.x,bossHealth.y,bossHealth.z,bossDamage,bossScale,bossExperience.x,bossExperience.y,bossExperience.z,
                bossGold.x,bossGold.y,bossGold.z,bossSpeed};
            foreach(float value in values)
                if(float.IsNaN(value) || float.IsInfinity(value) || value<0) throw new InvalidOperationException("难度数值必须是有限非负数："+name);
            if(baseEnemyHealth<1 || healthGrowth<1 || damageGrowth<1 || maxSpeed<baseEnemySpeed ||
                bossHealth.x<1 || bossHealth.y<1 || bossHealth.z<1 || eliteHealth<1 || eliteDamage<1 || eliteScale<0.1f || bossScale<0.1f)
                throw new InvalidOperationException("生命、增长倍率、体型或移速上限无效："+name);
            return new CombatDifficultySettings {HealthGrowth=healthGrowth,DamageGrowth=damageGrowth,SpeedPerMinute=speedPerMinute,MaxSpeed=maxSpeed,
                BaseExperience=baseExperience,BaseGold=baseGold,EliteHealth=eliteHealth,EliteDamage=eliteDamage,EliteScale=eliteScale,
                EliteExperience=eliteExperience,EliteGold=eliteGold,BossHealth=bossHealth,BossDamage=bossDamage,BossScale=bossScale,
                BossExperience=bossExperience,BossGold=bossGold,BossSpeed=bossSpeed};
        }
    }
}
