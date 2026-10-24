using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>名册中的一条出场规则：哪个敌人预制体、在哪些强度时间与档位出现、权重多少。</summary>
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [Tooltip("带 EnemyArchetype 组件的敌人预制体。")] public EnemyArchetype prefab;
        [Min(0), Tooltip("同一档位内的抽取权重；0 表示暂不出场。")] public float weight=1;
        [Min(0), Tooltip("最早出现的强度时间（秒），按累计关卡时间计算。") ] public float firstSecond;
        [Min(0), Tooltip("最后出场的强度时间（秒）；0 表示一直出现。")] public float lastSecond;
        [Tooltip("本图专属精英：每第 3 只精英与第 1 关首领使用此条目。") ] public bool specialElite;
        [Tooltip("允许以哪些档位出现，可多选。")] public EnemyTierMask tiers=EnemyTierMask.Normal;

        public bool Allows(EnemyTier tier,float seconds)=>weight>0 && (tiers&Mask(tier))!=0 && seconds>=firstSecond && (lastSecond<=0 || seconds<=lastSecond);
        public static EnemyTierMask Mask(EnemyTier tier)=>tier==EnemyTier.Boss?EnemyTierMask.Boss:tier==EnemyTier.Elite?EnemyTierMask.Elite:EnemyTierMask.Normal;
    }

    /// <summary>
    /// 敌人名册：列出本局可刷出的敌人种类与出场规则。CombatSpawnDirector 每次补充按档位筛选可出场条目并按权重抽取；
    /// 没有合格条目就不生成；专属精英单独按节奏抽取。只存配置。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/战斗/敌人名册")]
    public sealed class EnemyRoster : ScriptableObject
    {
        public EnemySpawnEntry[] entries=Array.Empty<EnemySpawnEntry>();
        [Header("档位色调（混入敌人材质颜色）")]
        public Color eliteTint=new Color(1f,0.78f,0.28f,1);
        [Range(0,1)] public float eliteTintStrength=0.45f;
        public Color bossTint=new Color(1f,0.35f,0.3f,1);
        [Range(0,1)] public float bossTintStrength=0.15f;

        /// <summary>校验全部条目；返回第一个错误，没有错误时返回 null。不修改资源。</summary>
        public string Validate()
        {
            if(entries==null || entries.Length==0) return "敌人名册没有任何条目。";
            if(entries.Length>64) return "敌人名册最多 64 条。";
            var ids=new Dictionary<int,EnemyArchetype>();
            bool firstNormal=false,elite=false,boss=false;
            for(int i=0;i<entries.Length;i++)
            {
                var entry=entries[i];
                if(entry==null || !entry.prefab) return $"第 {i+1} 条缺少敌人预制体。";
                try { entry.prefab.Compile(out _); }
                catch(InvalidOperationException error) { return $"第 {i+1} 条：{error.Message}"; }
                if(ids.TryGetValue(entry.prefab.configId,out var owner) && owner!=entry.prefab) return $"怪物数字 ID 重复：{entry.prefab.configId}";
                ids[entry.prefab.configId]=entry.prefab;
                if(float.IsNaN(entry.weight) || float.IsInfinity(entry.weight) || entry.weight<0) return $"第 {i+1} 条权重无效。";
                if(entry.firstSecond<0 || float.IsNaN(entry.firstSecond) || float.IsInfinity(entry.firstSecond) || float.IsNaN(entry.lastSecond) || float.IsInfinity(entry.lastSecond) || entry.lastSecond<0 || (entry.lastSecond>0 && entry.lastSecond<entry.firstSecond)) return $"第 {i+1} 条强度时间范围无效。";
                if((entry.tiers&(EnemyTierMask.Normal|EnemyTierMask.Elite|EnemyTierMask.Boss))==0) return $"第 {i+1} 条没有选择任何档位。";
                if(!entry.prefab.ResolveMaterial()) return $"第 {i+1} 条预制体缺少 MeshRenderer 材质。";
                if(entry.prefab.GetComponentInChildren<MeshFilter>(true)==null) return $"第 {i+1} 条预制体缺少 MeshFilter 网格。";
                firstNormal|=entry.Allows(EnemyTier.Normal,0);
                elite|=entry.weight>0 && (entry.tiers&EnemyTierMask.Elite)!=0;
                boss|=entry.weight>0 && (entry.tiers&EnemyTierMask.Boss)!=0;
            }
            if(!firstNormal) return "敌人名册必须至少有一条从开局开始的普通敌人。";
            if(!elite) return "敌人名册必须至少有一条允许精英档位的条目。";
            if(!boss) return "敌人名册必须至少有一条允许首领档位的条目。";
            return null;
        }
    }
}
