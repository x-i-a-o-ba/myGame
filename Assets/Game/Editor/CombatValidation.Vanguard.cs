using System;
using Roguelike.Abilities;
using UnityEditor;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        private static void ValidateVanguardAndVolley()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>(AbilityFoundationAssets.CatalogPath);
            var frost=AssetDatabase.LoadAssetAtPath<HeroDefinition>(AbilityFoundationAssets.HeroPath);
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(frost.ResolveSkills(catalog));
                var front=DurableTarget(f,new float3(10,0,0));var behind=DurableTarget(f,new float3(14,0,0));
                var miss=DurableTarget(f,new float3(10,0,1));
                f.Command(owner,1,new float3(20,0,0),new float3(1,0,0));f.Step(owner,0,0);
                var flights=f.Manager.GetBuffer<SkillFlight>(owner);
                Check(flights.Length==7 && f.State(front).Health==1000,"冰箭数量来自配置，发射帧不进行扇形瞬时伤害");
                Check(math.abs(math.dot(flights[0].Direction,flights[6].Direction)-math.cos(math.radians(70)))<.001f,"七支箭均匀覆盖七十度张角");
                f.Step(owner,.3f,.3f);
                Check(f.State(front).Health==940 && f.State(behind).Health==1000 && f.State(miss).Health==1000,"箭只命中路径上的首个敌人，后方遮挡与箭间空隙不受伤");
                Check(f.Manager.GetBuffer<SkillFlight>(owner).Length==6,"命中的那支箭立即销毁，其余箭继续飞行");
                f.Step(owner,1,.7f);Check(f.Manager.GetBuffer<SkillFlight>(owner).Length==0 && f.State(behind).Health==1000,"射程终点清理未命中箭，不再穿透结算");
            }
            var cloned=UnityEngine.Object.Instantiate(frost.skills[1]);
            try
            {
                foreach(int invalid in new[]{0,33})
                {cloned.recipe.nodes[0].projectileCount=invalid;bool bad=false;try{SkillRecipeCompiler.Validate(cloned);}catch(InvalidOperationException){bad=true;}Check(bad,"非法箭数在编译边界被拒绝");}
                cloned.recipe.nodes[0].projectileCount=1;cloned.recipe.nodes[0].spreadDegrees=0;SkillRecipeCompiler.Validate(cloned);
                Check(true,"单箭配置与旧直线弹体兼容");
            } finally {UnityEngine.Object.DestroyImmediate(cloned);}
            var hero=AssetDatabase.LoadAssetAtPath<HeroDefinition>("Assets/GameData/英雄/2002_磐锋卫士.asset");
            Check(hero && hero.skills.Length==4 && hero.visualPrefab && hero.ResolveAttack(38).Delivery==AttackDelivery.Melee,"战士四槽装配、近战和独立外形可用");
            hero.ValidateProgression();var ids=hero.ResolveSkills(catalog);
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(ids);SetMana(f,owner,240);var target=DurableTarget(f,new float3(3,0,0));
                f.Command(owner,0,new float3(6,0,0),new float3(1,0,0));f.Step(owner,0,0);
                Check(f.State(target).Health==1000 && f.State(owner).Mana==240,"战士重劈需要真实普攻命中蓄势");
                for(int i=0;i<3;i++)f.Execution.RecordBasicAttackHit(owner);
                f.Command(owner,0,new float3(6,0,0),new float3(1,0,0));f.Step(owner,.1f,.1f);
                Check(f.State(target).Health==910 && math.abs(f.State(target).Attack-8)<.001f && f.Manager.GetBuffer<AbilitySlot>(owner)[0].HitStacks==0,"重劈伤害与减攻共用效果结算，并消耗三层蓄势");
                var unit=f.State(owner);unit.Health=20;f.Manager.SetComponentData(owner,unit);
                f.Command(owner,1,float3.zero,new float3(1,0,0));f.Step(owner,.2f,.1f);
                Check(f.State(owner).Health==65 && math.abs(f.State(owner).Speed-8)<.001f,"守势即时治疗并承受移动代价");
                f.TickBuffs(owner,1.2f,1);Check(f.State(owner).Health==81,"守势持续恢复生命");
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(ids);SetMana(f,owner,240);var target=DurableTarget(f,new float3(3,0,0));
                f.Command(owner,2,float3.zero,new float3(1,0,0));f.Step(owner,0,0);
                f.Manager.SetComponentData(owner,LocalTransform.FromPosition(new float3(20,0,0)));
                f.Step(owner,3,3);
                Check(f.State(target).Health==850 && f.State(owner).Mana==195,"刃阵固定在施放处，六次各25伤害，离开不搬动阵地且只扣一次蓝");
                f.Step(owner,4,1);Check(f.State(target).Health==850,"刃阵结束后无多余伤害");
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(ids);SetMana(f,owner,240);var target=DurableTarget(f,new float3(10,0,0));
                f.Command(owner,3,new float3(10,0,0),new float3(1,0,0));f.Step(owner,0,0);f.Step(owner,.3f,.3f);
                Check(f.State(target).Health==1000,"落剑飞行途中不提前伤害");
                f.Step(owner,.7f,.4f);Check(f.State(target).Health==760 && f.State(owner).Mana==160,"落剑到达后结算范围伤害及真实耗蓝");
            }
        }
    }
}
