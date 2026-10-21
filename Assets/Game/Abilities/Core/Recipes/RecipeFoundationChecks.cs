#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>由主任务统一调用的编辑器编译合同检查；不进入 Play，不加载或保存场景。</summary>
    public static class RecipeFoundationChecks
    {
        public static void Run()
        {
            int count=0;
            using(var f=new Fixture())
            {
                void Check(bool ok,string context) { if(!ok) throw new InvalidOperationException(context); count++; }
                void Reject(Action action,string context)
                { bool rejected=false;try {action();} catch(InvalidOperationException) {rejected=true;} Check(rejected,context); }
                SkillRecipeCompiler.Validate(f.Skill);
                count++;
                var n=f.Skill.recipe.nodes[0];
                n.effects[0].overrideAmount=true; n.effects[0].amount=25;
                using(var blob=f.Catalog.Compile())
                {
                    ref var s=ref blob.Value.Skills[0];
                    Check(s.UsesRecipe && s.ConfigId==1000,"配方未进入核心编译");
                    Check(s.Stages[0].ParentStage==-1,"释放根没有标记为 -1");
                    Check(s.Stages[0].Effects[0].Amount==25,"本次效果覆盖未生效");
                }
                Check(f.Damage.amount==10,"覆盖污染了共享效果");
                n.effects[0].overrideAmount=false;
                using(var blob=f.Catalog.Compile()) Check(blob.Value.Skills[0].Stages[0].Effects[0].Amount==10,"默认值被污染");
                string json=SkillRecipeJson.Export(f.Skill);
                var imported=SkillRecipeJson.Import(json,f.Modules,true);
                try {Check(imported.recipe.nodes[0].effects[0].effectId==4000 && imported.configId==0,"JSON 编号往返失败");}
                finally {UnityEngine.Object.DestroyImmediate(imported);}
                f.Skill.recipe.catalogVersion=9;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"错误目录版本被接受"); f.Skill.recipe.catalogVersion=1;
                f.Skill.recipe.schemaVersion=9;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"错误格式版本被接受"); f.Skill.recipe.schemaVersion=1;
                n.effects[0].effectId=4999;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"缺失效果被接受"); n.effects[0].effectId=4000;
                n.effects[0].overrideAmount=true;n.effects[0].amount=101;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"越界覆盖被接受"); n.effects[0].amount=float.NaN;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"NaN 被接受"); n.effects[0].overrideAmount=false;
                n.effects[0].overrideBuffScale=true;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"错误效果参数被接受"); n.effects[0].overrideBuffScale=false;
                n.deliveryModuleId=10999;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"缺失投递被接受"); n.deliveryModuleId=10000;
                n.parentNodeId=1;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"自循环被接受"); n.parentNodeId=0;
                f.Skill.recipe.nodes=new[]{n,Clone(n)};
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"重复节点 ID 被接受");
                var child=f.Skill.recipe.nodes[1]; child.nodeId=2;child.parentNodeId=1;child.trigger=RecipeTrigger.OnResolved;
                SkillRecipeCompiler.Validate(f.Skill); count++;
                using(var blob=f.Catalog.Compile()) Check(blob.Value.Skills[0].Stages[1].ParentStage==0,"父节点映射错误");
                child.trigger=RecipeTrigger.OnProjectileHit;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"非弹体提供首碰端口");
                child.trigger=RecipeTrigger.OnResolved;child.queryModuleId=10002;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"整组端口被当成单体");
                child.trigger=RecipeTrigger.OnDamage;
                SkillRecipeCompiler.Validate(f.Skill);count++;
                child.trigger=RecipeTrigger.OnKill;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"击杀端口被当成存活单体");
                child.trigger=RecipeTrigger.OnResolved;child.queryModuleId=10001;
                n.effects=Array.Empty<RecipeEffectUse>();
                SkillRecipeCompiler.Validate(f.Skill);count++;
                f.Skill.recipe.nodes=new[]{n};
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"无效果无后续节点被接受");
                n.effects=new[]{new RecipeEffectUse{effectId=4000}};
                n.repeatCount=9;n.repeatInterval=.1f;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill,true),"玩家重复越界被接受"); n.repeatCount=1;
                f.Modules.effects[0].playerAllowed=false;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill,true),"玩家权限被绕过");
                SkillRecipeCompiler.Validate(f.Skill,false);count++;f.Modules.effects[0].playerAllowed=true;
                n.delay=11;
                Reject(()=>SkillRecipeCompiler.Validate(f.Skill),"派生链寿命越界被接受");n.delay=0;
                f.Skill.recipe.enabled=false;
                f.Skill.stages=new[]{new SkillStage{effects=new EffectDefinition[]{f.Damage}}};
                using(var blob=f.Catalog.Compile()) Check(!blob.Value.Skills[0].UsesRecipe && blob.Value.Skills[0].Stages[0].ParentStage==-1,"旧阶段迁移改变根语义");
                f.Skill.recipe=SkillRecipeAdapter.FromLegacy(f.Skill,f.Modules);
                SkillRecipeCompiler.Validate(f.Skill);count++;
                Check(f.Skill.stages[0].effects[0]==f.Damage,"适配器污染旧阶段");
            }
            Debug.Log("RecipeFoundationChecks PASS assertions="+count);
        }

        private static RecipeNode Clone(RecipeNode n) => JsonUtility.FromJson<RecipeNode>(JsonUtility.ToJson(n));
        private sealed class Fixture : IDisposable
        {
            public readonly DamageEffect Damage=ScriptableObject.CreateInstance<DamageEffect>();
            public readonly SkillModuleCatalog Modules=ScriptableObject.CreateInstance<SkillModuleCatalog>();
            public readonly SkillDefinition Skill=ScriptableObject.CreateInstance<SkillDefinition>();
            public readonly SkillCatalog Catalog=ScriptableObject.CreateInstance<SkillCatalog>();
            public Fixture()
            {
                Damage.configId=4000;Damage.stableId="effect.test";Damage.amount=10;
                Modules.deliveries=new[]{new DeliveryModule{id=10000,kind=DeliveryKind.Instant}};
                Modules.queries=new[]{new QueryModule{id=10001},new QueryModule{id=10002,scope=TargetScope.Single}};
                Modules.effects=new[]{new EffectModule{definition=Damage,allowAmountOverride=true,maxAmount=100}};
                Skill.configId=1000;Skill.stableId="skill.test";Skill.displayName="配方检查";Skill.modules=Modules;
                Skill.recipe=new SkillRecipe{enabled=true,nodes=new[]{new RecipeNode{deliveryModuleId=10000,queryModuleId=10001,effects=new[]{new RecipeEffectUse{effectId=4000}}}}};
                Catalog.skills=new[]{Skill};
            }
            public void Dispose()
            { UnityEngine.Object.DestroyImmediate(Catalog);UnityEngine.Object.DestroyImmediate(Skill);UnityEngine.Object.DestroyImmediate(Modules);UnityEngine.Object.DestroyImmediate(Damage); }
        }
    }
}
#endif
