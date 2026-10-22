using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>显式一次性迁移。由隔离 Editor 执行，保留原资源 GUID 与旧阶段；无自动启动或菜单副作用。</summary>
    public static class RecipeAssetMigration
    {
        public const string CatalogPath="Assets/GameData/目录/组合模块目录.asset";
        public static T[] Assets<T>() where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath).Where(path=>
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT")) ||
                File.Exists(Path.Combine(Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT"),path)))
            .OrderBy(path=>path,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x=>x).ToArray();
        public static void Run()
        {
            var effects=Assets<EffectDefinition>(); var buffs=Assets<BuffDefinition>();
            foreach(var effect in effects) if(string.IsNullOrWhiteSpace(effect.stableId))
            { effect.stableId="effect."+Guid.NewGuid().ToString("N"); EditorUtility.SetDirty(effect); }
            foreach(var buff in buffs) if(string.IsNullOrWhiteSpace(buff.stableId))
            { buff.stableId="buff."+Guid.NewGuid().ToString("N"); EditorUtility.SetDirty(buff); }
            NumericConfigIds.AssignMissing();
            var modules=AssetDatabase.LoadAssetAtPath<SkillModuleCatalog>(CatalogPath);
            if(!modules)
            {
                modules=ScriptableObject.CreateInstance<SkillModuleCatalog>();
                modules.deliveries=new[] {
                    new DeliveryModule{id=10000,displayName="立即结算",kind=DeliveryKind.Instant},
                    new DeliveryModule{id=10001,displayName="飞向目标点",kind=DeliveryKind.PointProjectile},
                    new DeliveryModule{id=10002,displayName="直线首碰弹体",kind=DeliveryKind.LinearProjectile},
                    new DeliveryModule{id=10003,displayName="三维抛物线",kind=DeliveryKind.ParabolicProjectile}};
                var queries=new List<QueryModule>(); int id=10100;
                foreach(TargetRelation relation in Enum.GetValues(typeof(TargetRelation)))
                {
                    string side=relation==TargetRelation.Enemies?"敌方":"友方";
                    queries.Add(new QueryModule{id=id++,displayName="自身（"+side+"关系）",scope=TargetScope.Self,targets=relation});
                    queries.Add(new QueryModule{id=id++,displayName=side+"单体",scope=TargetScope.Single,targets=relation});
                    queries.Add(new QueryModule{id=id++,displayName=side+"全体",scope=TargetScope.All,targets=relation});
                    queries.Add(new QueryModule{id=id++,displayName=side+"圆形范围",scope=TargetScope.Area,shape=AreaShape.Circle,targets=relation});
                    queries.Add(new QueryModule{id=id++,displayName=side+"扇形范围",scope=TargetScope.Area,shape=AreaShape.Cone,targets=relation});
                    queries.Add(new QueryModule{id=id++,displayName=side+"直线范围",scope=TargetScope.Area,shape=AreaShape.Line,targets=relation});
                }
                modules.queries=queries.ToArray(); modules.buffs=buffs;
                modules.effects=effects.Select(effect=>new EffectModule {definition=effect,
                    allowAmountOverride=effect is DamageEffect || effect is HealEffect,minAmount=0,maxAmount=1000,
                    allowBuffScaleOverride=false}).ToArray();
                modules.Validate(); AssetDatabase.CreateAsset(modules,CatalogPath);
            }
            // 重复执行仅补充新登记项，不覆盖策划修改过的参数、权限及历史模块身份。
            var entries=new List<EffectModule>(modules.effects);
            foreach(var effect in effects) if(!entries.Any(entry=>entry.definition==effect))
                entries.Add(new EffectModule {definition=effect,allowAmountOverride=effect is DamageEffect || effect is HealEffect,maxAmount=1000});
            modules.effects=entries.ToArray(); EditorUtility.SetDirty(modules); modules.Validate();
            var pending=new List<(SkillDefinition source,SkillDefinition candidate)>();
            try
            {
                foreach(var skill in Assets<SkillDefinition>())
                {
                    if(SkillRecipeAdapter.IsRecipe(skill) || skill.stages==null || skill.stages.Length==0) continue;
                    var candidate=UnityEngine.Object.Instantiate(skill);
                    pending.Add((skill,candidate)); candidate.modules=modules;
                    candidate.recipe=SkillRecipeAdapter.FromLegacy(skill,modules);
                    candidate.recipe.recipeId=skill.configId;
                    candidate.recipe.authorNamespace="official";
                    SkillRecipeCompiler.Validate(candidate);
                    var converted=SkillRecipeAdapter.GetPreviewStages(candidate);
                    for(int i=0;i<skill.stages.Length;i++)
                        if(JsonUtility.ToJson(skill.stages[i])!=JsonUtility.ToJson(converted[i]))
                            throw new InvalidOperationException("迁移改变阶段配置："+skill.name+" / "+i);
                }
                foreach(var pair in pending)
                { pair.source.modules=modules; pair.source.recipe=pair.candidate.recipe; EditorUtility.SetDirty(pair.source); }
                AssetDatabase.SaveAssets(); NumericConfigIds.ValidateAll();
                Debug.Log($"RECIPE_MIGRATION_OK: skills={pending.Count}, effects={effects.Length}, buffs={buffs.Length}; GUID preserved; legacy stages retained.");
            }
            finally { foreach(var pair in pending) UnityEngine.Object.DestroyImmediate(pair.candidate); }
        }
    }
}
