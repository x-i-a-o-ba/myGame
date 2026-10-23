using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>显式迁移与只读预览适配。旧阶段保留原值，启用后仅配方参与执行。</summary>
    public static class SkillRecipeAdapter
    {
        public static bool IsRecipe(SkillDefinition skill) => skill && skill.recipe!=null && skill.recipe.enabled;

        public static SkillRecipe FromLegacy(SkillDefinition skill,SkillModuleCatalog catalog)
        {
            if(!skill || !catalog) throw new ArgumentNullException("技能与模块目录不能为空。");
            catalog.Validate();
            if(IsRecipe(skill)) return JsonUtility.FromJson<SkillRecipe>(JsonUtility.ToJson(skill.recipe));
            if(skill.stages==null || skill.stages.Length==0)
                throw new InvalidOperationException("旧独立示例持续区域不能直接转换；请先创建正式组合阶段。");
            var result=new SkillRecipe {enabled=true,catalogVersion=catalog.catalogVersion,nodes=new RecipeNode[skill.stages.Length]};
            float longest=10;
            for(int i=0;i<result.nodes.Length;i++)
            {
                var s=skill.stages[i]; if(s==null) throw new InvalidOperationException("旧阶段为空："+i);
                DeliveryModule delivery=null; QueryModule query=null;
                foreach(var d in catalog.deliveries) if(d.kind==s.delivery) {delivery=d;break;}
                foreach(var q in catalog.queries) if(q.scope==s.scope && q.shape==s.shape && q.targets==s.targets) {query=q;break;}
                if(delivery==null || query==null) throw new InvalidOperationException("模块目录未登记旧阶段使用的投递/查询："+i);
                result.nodes[i]=new RecipeNode {nodeId=i+1,deliveryModuleId=delivery.id,queryModuleId=query.id,
                    delay=s.delay,repeatCount=s.repeatCount,repeatInterval=s.repeatInterval,
                    range=s.range,radius=s.radius,hitRadius=s.hitRadius,halfAngleDegrees=s.halfAngleDegrees,speed=s.speed,
                    projectileCount=s.projectileCount,spreadDegrees=s.spreadDegrees,
                    arcHeight=s.arcHeight,flightSeconds=s.flightSeconds,launchHeight=s.launchHeight,arrivalHeight=s.arrivalHeight,
                    effects=Uses(s.effects,catalog),impactEffects=Uses(s.impactEffects,catalog)};
                float distance=s.delivery==DeliveryKind.PointProjectile?skill.castRange:s.range;
                float travel=s.delivery==DeliveryKind.ParabolicProjectile?s.flightSeconds:s.delivery==DeliveryKind.Instant?0:distance/Math.Max(.001f,s.speed);
                longest=Math.Max(longest,s.delay+(s.repeatCount-1)*s.repeatInterval+travel+.1f);
            }
            result.maxLifetime=longest;
            return result;
        }

        /// <summary>供编辑器范围预览只读获取几何；返回新的阶段对象，不写回旧字段，也不复制效果 SO。</summary>
        public static SkillStage[] GetPreviewStages(SkillDefinition skill)
        { return IsRecipe(skill)?SkillRecipeCompiler.Resolve(skill).Stages:skill.stages??Array.Empty<SkillStage>(); }

        private static RecipeEffectUse[] Uses(EffectDefinition[] source,SkillModuleCatalog catalog)
        {
            if(source==null) return Array.Empty<RecipeEffectUse>();
            var uses=new RecipeEffectUse[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                if(!source[i] || catalog.FindEffect(source[i].configId).definition!=source[i])
                    throw new InvalidOperationException("旧效果没有登记到目录。");
                uses[i]=new RecipeEffectUse {effectId=source[i].configId};
            }
            return uses;
        }
    }
}
