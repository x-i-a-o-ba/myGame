using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        /// <summary>读取正式配置验证逐级表、法力提交与实际伤害；不改写配置资源。</summary>
        private static void ValidateFrostArcherData()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>(AbilityFoundationAssets.CatalogPath);
            var hero=AssetDatabase.LoadAssetAtPath<HeroDefinition>(AbilityFoundationAssets.HeroPath);
            Check(hero && catalog && hero.configId==2000 && hero.baseMana==240 && hero.manaPerLevel==12,"寒冰正式装配与法力成长可读取");
            using(var f=new Fixture(catalog))
            {
                var definitions=hero.ResolveSkills(catalog);
                float[][] costs={new[]{30f,35f,40f,45f},new[]{45f,50f,55f,60f},new[]{60f,65f,70f,75f},new[]{100f,125f,150f}};
                float[][] times={new[]{10f,9f,8f,7f},new[]{8f,7f,6f,5f},new[]{14f,13f,12f,11f},new[]{30f,25f,20f}};
                for(int slot=0;slot<4;slot++)
                {
                    ref var skill=ref f.Library.Value.Skills[definitions[slot]];
                    Check(skill.Levels.Length==costs[slot].Length,"正式技能等级数正确");
                    for(int level=1;level<=costs[slot].Length;level++)
                    {
                        var row=SkillLevels.Row(ref skill,level);
                        Check(Mathf.Approximately(SkillLevels.ManaCost(ref skill,level),costs[slot][level-1]) &&
                            Mathf.Approximately((slot==2?skill.RechargeSeconds:skill.Cooldown)*row.CooldownScale,times[slot][level-1]) &&
                            row.RequiredHeroLevel==(slot==3?6+(level-1)*6:1+(level-1)*2),"正式逐级蓝耗、冷却与学习门槛一致");
                    }
                }
                var owner=f.Owner(definitions);
                SetMana(f,owner,240);
                f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0,0);
                Check(f.State(owner).Mana==240 && f.Manager.GetBuffer<ActiveBuff>(owner).Length==0,"Q 未满层不扣蓝");
                for(int i=0;i<4;i++) f.Execution.RecordBasicAttackHit(owner);
                f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                Check(f.State(owner).Mana==210 && Mathf.Approximately(f.State(owner).AttackSpeedMultiplier,1.6f),"Q 满层只扣一次蓝并增加攻速");
                f.TickBuffs(owner,5.2f,0);
                Check(Mathf.Approximately(f.State(owner).AttackSpeedMultiplier,1),"Q 到期恢复攻速");
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(hero.ResolveSkills(catalog));
                var target=DurableTarget(f,new float3(4,0,0));
                SetMana(f,owner,44);
                f.Command(owner,1,new float3(10,0,0),new float3(1,0,0)); f.Step(owner,0,0);
                var slot=f.Manager.GetBuffer<AbilitySlot>(owner)[1];
                Check(f.State(owner).Mana==44 && f.State(target).Health==1000 && slot.ReadyAt==0,"W 蓝量不足不伤害、不扣蓝、不进入冷却");
                SetMana(f,owner,45);
                f.Command(owner,1,new float3(10,0,0),new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                Check(f.State(owner).Mana==0 && f.State(target).Health==940 && Mathf.Approximately(f.State(target).Speed,6.5f),"W 恰好足够蓝量可施放并结算 60 伤害及 35% 减速");
                var slots=f.Manager.GetBuffer<AbilitySlot>(owner); slot=slots[1]; slot.Level=4; slot.ReadyAt=0; slots[1]=slot;
                SetMana(f,owner,60); f.Command(owner,1,new float3(10,0,0),new float3(1,0,0)); f.Step(owner,9,0);
                Check(f.State(target).Health==760 && f.State(owner).Mana==0 && Mathf.Approximately(f.State(target).Speed,6.5f),"W 四级结算 180 伤害，减速不随等级意外放大");
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(hero.ResolveSkills(catalog)); SetMana(f,owner,240);
                var target=DurableTarget(f,new float3(10,0,0));
                f.Command(owner,2,new float3(10,0,0),new float3(1,0,0)); f.Step(owner,0,0); f.Step(owner,1,1);
                Check(f.State(target).Health==910 && f.State(owner).Mana==180 && f.Manager.GetBuffer<AbilitySlot>(owner)[2].Charges==1,"E 落点结算 90 伤害并消耗一次充能与 60 法力");
                f.Command(owner,2,new float3(10,0,0),new float3(1,0,0)); f.Step(owner,1.1f,0.1f); f.Step(owner,2,0.9f);
                Check(f.State(target).Health==820 && f.State(owner).Mana==120 && f.Manager.GetBuffer<AbilitySlot>(owner)[2].Charges==0,"E 第二次充能独立扣蓝");
                f.Step(owner,14.1f,0); Check(f.Manager.GetBuffer<AbilitySlot>(owner)[2].Charges==1,"E 依次恢复充能");
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(hero.ResolveSkills(catalog)); SetMana(f,owner,240);
                var first=DurableTarget(f,new float3(5,0,0)); var nearby=DurableTarget(f,new float3(5,0,3)); var beyond=DurableTarget(f,new float3(20,0,0));
                f.Command(owner,3,new float3(30,0,0),new float3(1,0,0)); f.Step(owner,0,0); f.Step(owner,1,1);
                Check(f.State(first).Health==780 && f.State(nearby).Health==900 && f.State(beyond).Health==1000,"R 首中承受 120+100，附近承受 100，弹体首碰停止");
                Check(f.State(owner).Mana==140 && f.State(first).Controls!=ControlFlags.None && Mathf.Approximately(f.State(nearby).Speed,5),"R 消耗 100 法力并施加眩晕与减速");
            }
        }
        private static void SetMana(Fixture f,Entity owner,float mana)
        {var unit=f.State(owner); unit.Mana=mana; unit.MaxMana=240; f.Manager.SetComponentData(owner,unit);}
        private static Entity DurableTarget(Fixture f,float3 position)
        {var target=f.Unit(position,1); var unit=f.State(target); unit.Health=unit.MaxHealth=1000; f.Manager.SetComponentData(target,unit); return target;}
    }
}
