using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>装配时将数字配方解析为旧核心也使用的阶段；不持有运行状态，不修改任何定义。</summary>
    public static class SkillRecipeCompiler
    {
        public sealed class Plan
        {
            public SkillStage[] Stages;
            public int[] Parents;
            public RecipeNode[] Nodes;
        }

        /// <summary>开发者与玩家入口使用同一个完整库编译器。成功后立即释放验证用 Blob。</summary>
        public static void Validate(SkillDefinition skill,bool forPlayer=false)
        {
            if(!skill || !skill.modules || !SkillRecipeAdapter.IsRecipe(skill)) throw new InvalidOperationException("请先启用配方并绑定组合模块目录。");
            var catalog=ScriptableObject.CreateInstance<SkillCatalog>();
            try
            {
                catalog.skills=new[]{skill}; catalog.buffs=skill.modules.buffs;
                var blob=catalog.Compile(forPlayer); blob.Dispose();
            }
            finally { if(Application.isPlaying) UnityEngine.Object.Destroy(catalog); else UnityEngine.Object.DestroyImmediate(catalog); }
        }

        public static Plan Resolve(SkillDefinition skill,bool forPlayer=false)
        {
            if(!skill || skill.recipe==null || !skill.recipe.enabled || !skill.modules)
                throw new InvalidOperationException("配方未启用或缺少模块目录。");
            var recipe=skill.recipe; var catalog=skill.modules; catalog.Validate();
            if(recipe.schemaVersion!=SkillRecipe.CurrentSchemaVersion || recipe.catalogVersion!=catalog.catalogVersion)
                throw new InvalidOperationException("配方格式或模块目录版本不匹配，不能静默迁移。");
            if(recipe.revision<1 || recipe.recipeId<1 || string.IsNullOrWhiteSpace(recipe.authorNamespace) || recipe.authorNamespace.Length>80)
                throw new InvalidOperationException("配方修订/数字编号必须为正数，并指定有效作者命名空间（最多80字）。");
            int limit=forPlayer?16:32;
            if(recipe.nodes==null || recipe.nodes.Length<1 || recipe.nodes.Length>limit)
                throw new InvalidOperationException("配方步骤数量必须为 1–"+limit+"。");
            if(recipe.maxDerivedExecutions<1 || recipe.maxDerivedExecutions>(forPlayer?32:128))
                throw new InvalidOperationException("派生执行额度必须为 1–"+(forPlayer?32:128)+"；额度不限制一次查询的目标数。");
            Bounded(recipe.maxLifetime,0.001f,forPlayer?10:60,"派生链寿命（秒）");
            if(forPlayer)
            {
                Bounded(skill.castRange,0,150,"玩家施法距离"); Bounded(skill.cooldown,.1f,300,"玩家冷却");
                Bounded(skill.castTime,0,10,"玩家前摇"); Bounded(skill.recoveryTime,0,10,"玩家后摇");
                Bounded(skill.manaCost,0,10000,"玩家资源消耗");
                if(skill.requiredHits!=0 || skill.maxCharges>8) throw new InvalidOperationException("玩家配方暂不允许普攻蓄层条件，最多8次充能。");
            }
            var result=new Plan {Stages=new SkillStage[recipe.nodes.Length],Parents=new int[recipe.nodes.Length],Nodes=recipe.nodes};
            var indices=new Dictionary<int,int>(); var depth=new int[recipe.nodes.Length]; var lifetime=new float[recipe.nodes.Length];
            int rootCount=0;
            for(int i=0;i<recipe.nodes.Length;i++)
            {
                var n=recipe.nodes[i]; string c="配方节点 "+(n?.nodeId??0);
                if(n==null || n.nodeId<=0 || !indices.TryAdd(n.nodeId,i)) throw new InvalidOperationException(c+"：编号必须正数且唯一。");
                SkillModuleCatalog.Defined(n.trigger,c+" 触发事件");
                int parent=-1;
                if(n.parentNodeId==0)
                { if(n.trigger!=RecipeTrigger.OnRelease) throw new InvalidOperationException(c+"：根步骤必须使用施法释放事件。"); rootCount++; }
                else
                {
                    if(!indices.TryGetValue(n.parentNodeId,out parent) || parent>=i)
                        throw new InvalidOperationException(c+"：父步骤必须存在且位于本步骤之前，禁止回连和循环。");
                    if(n.trigger==RecipeTrigger.OnRelease) throw new InvalidOperationException(c+"：子步骤不能使用施法释放事件。");
                    depth[i]=depth[parent]+1;
                    if(depth[i]>4) throw new InvalidOperationException(c+"：派生深度最多4层。");
                }
                result.Parents[i]=parent;
                var d=catalog.FindDelivery(n.deliveryModuleId); var q=catalog.FindQuery(n.queryModuleId);
                if(forPlayer && (!d.playerAllowed || !q.playerAllowed)) throw new InvalidOperationException(c+"：模块尚未向玩家开放。");
                Bounded(n.delay,0,recipe.maxLifetime,c+" 延迟"); Bounded(n.repeatInterval,0,recipe.maxLifetime,c+" 重复间隔");
                if(n.repeatCount<1 || n.repeatCount>(forPlayer?8:32)) throw new InvalidOperationException(c+"：重复次数越界。");
                Bounded(n.range,0,Math.Min(d.maxRange,q.maxRange),c+" 长度"); Bounded(n.radius,0,q.maxRadius,c+" 半径");
                Bounded(n.hitRadius,0,q.maxRadius,c+" 碰撞半径"); Bounded(n.speed,0,d.maxSpeed,c+" 速度");
                Bounded(n.halfAngleDegrees,.001f,180,c+" 扇形半角");
                Bounded(n.arcHeight,0,d.maxArcHeight,c+" 弧线高度"); Bounded(n.flightSeconds,.001f,d.maxFlightSeconds,c+" 飞行时长");
                Bounded(n.launchHeight,0,100,c+" 发射高度"); Bounded(n.arrivalHeight,0,100,c+" 到达高度");
                if(q.scope==TargetScope.Single && d.kind!=DeliveryKind.LinearProjectile)
                {
                    bool hasUnit=parent<0 ? skill.aim==AimMode.Unit : n.trigger==RecipeTrigger.OnProjectileHit || n.trigger==RecipeTrigger.OnDamage;
                    if(!hasUnit) throw new InvalidOperationException(c+"：单体查询缺少存活单位端口，击杀/整组完成事件只提供位置。");
                }
                if(parent>=0)
                {
                    var p=result.Stages[parent];
                    if(n.trigger==RecipeTrigger.OnProjectileHit && p.delivery!=DeliveryKind.LinearProjectile)
                        throw new InvalidOperationException(c+"：父步骤不是首碰弹体，不能提供首碰事件。");
                    if((n.trigger==RecipeTrigger.OnDamage || n.trigger==RecipeTrigger.OnKill) && !HasDamage(p))
                        throw new InvalidOperationException(c+"：父步骤没有伤害模块，不能提供伤害/击杀事件。");
                }
                float distance=d.kind==DeliveryKind.PointProjectile && parent<0?skill.castRange:n.range;
                float travel=d.kind==DeliveryKind.ParabolicProjectile?n.flightSeconds:
                    d.kind==DeliveryKind.Instant?0:distance/Math.Max(.001f,n.speed);
                lifetime[i]=(parent<0?0:lifetime[parent])+n.delay+(n.repeatCount-1)*n.repeatInterval+travel;
                if(lifetime[i]>recipe.maxLifetime) throw new InvalidOperationException(c+"：延迟、重复和飞行路径超过配方寿命。");
                result.Stages[i]=new SkillStage {delay=n.delay,repeatCount=n.repeatCount,repeatInterval=n.repeatInterval,
                    delivery=d.kind,scope=q.scope,shape=q.shape,targets=q.targets,
                    range=n.range,radius=n.radius,hitRadius=n.hitRadius,halfAngleDegrees=n.halfAngleDegrees,speed=n.speed,
                    projectileCount=n.projectileCount,spreadDegrees=n.spreadDegrees,
                    arcHeight=n.arcHeight,flightSeconds=n.flightSeconds,launchHeight=n.launchHeight,arrivalHeight=n.arrivalHeight,
                    effects=ResolveEffects(catalog,n.effects,forPlayer,c),impactEffects=ResolveEffects(catalog,n.impactEffects,forPlayer,c)};
            }
            if(forPlayer && rootCount!=1) throw new InvalidOperationException("玩家配方必须恰好有一个释放根步骤。");
            return result;
        }

        public static EffectData ApplyOverride(EffectData value,RecipeEffectUse use)
        {
            if(use.overrideAmount) value.Amount=use.amount;
            if(use.overrideBuffScale) throw new InvalidOperationException("当前版本不支持 Buff 强度覆盖。");
            return value;
        }

        private static EffectDefinition[] ResolveEffects(SkillModuleCatalog catalog,RecipeEffectUse[] uses,bool player,string context)
        {
            if(uses==null || uses.Length>64) throw new InvalidOperationException(context+"：效果数组不能为null，最多64项。");
            var definitions=new EffectDefinition[uses.Length];
            for(int i=0;i<uses.Length;i++)
            {
                var use=uses[i]; if(use==null) throw new InvalidOperationException(context+"：效果使用不能为空。");
                var module=catalog.FindEffect(use.effectId);
                if(player && !module.playerAllowed) throw new InvalidOperationException(context+"：效果未向玩家开放 "+use.effectId);
                if(use.overrideAmount)
                {
                    if(!module.allowAmountOverride) throw new InvalidOperationException(context+"：效果不允许数值覆盖 "+use.effectId);
                    Bounded(use.amount,module.minAmount,module.maxAmount,context+" 效果数值");
                }
                if(use.overrideBuffScale)
                {
                    throw new InvalidOperationException(context+"：当前版本尚不支持 Buff 规格独立合并；请引用固定强度的 Buff 模块，不能覆盖强度。");
                }
                definitions[i]=module.definition;
            }
            return definitions;
        }
        private static bool HasDamage(SkillStage stage)
        {
            foreach(var e in stage.effects) if(e.Compile().Kind==EffectKind.Damage) return true;
            foreach(var e in stage.impactEffects) if(e.Compile().Kind==EffectKind.Damage) return true;
            return false;
        }
        private static void Bounded(float value,float min,float max,string context)
        { if(float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max) throw new InvalidOperationException(context+" 必须在 "+min+"–"+max+" 之间。"); }
    }
}
