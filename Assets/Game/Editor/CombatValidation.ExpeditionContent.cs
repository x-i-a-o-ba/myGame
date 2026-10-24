using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Flow;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        [Serializable] public sealed class ItemLedger {public int schemaVersion;public List<ItemLedgerEntry> entries;}
        [Serializable] public sealed class ItemLedgerEntry {public int id;public string category,assetKey,stableId,path,name;public bool retired;}
        private static bool Rejects(Action action) {try {action();} catch(InvalidOperationException) {return true;}return false;}

        private static void ValidateExpeditionContent(SkillCatalog catalog)
        {
            var temporary=ScriptableObject.CreateInstance<SkillCatalog>();temporary.buffs=catalog.buffs;
            try
            {
                foreach(var skill in catalog.skills)
                foreach(var variant in new[]{skill,skill.forms[0],skill.forms[1]})
                {
                    temporary.skills=new[]{variant};using var f=new Fixture(temporary);
                    var owner=f.Owner(new[]{0});Strengthen(f,owner);
                    var targets=new List<Entity>();
                    for(int i=0;i<12;i++) {var target=f.Unit(new float3((i%3-1)*1.5f,0,3+i/3*1.5f),1);Strengthen(f,target);targets.Add(target);}
                    var slots=f.Manager.GetBuffer<AbilitySlot>(owner);var slot=slots[0];slot.Level=SkillLevels.MaxLevel(ref f.Library.Value.Skills[0]);slots[0]=slot;
                    if(variant.autoTriggerEveryHits>0) for(int i=0;i<variant.autoTriggerEveryHits;i++) f.Execution.RecordBasicAttackHit(owner,targets[0]);
                    else f.Command(owner,0,new float3(0,0,4),new float3(0,0,1));
                    float damage=0;bool buff=false;
                    for(int tick=0;tick<100;tick++)
                    {
                        f.Step(owner,tick*.05f,.05f);f.TickBuffs(owner,tick*.05f,.05f);
                        buff|=f.Manager.GetBuffer<ActiveBuff>(owner).Length>0;
                    }
                    foreach(var target in targets) damage+=10000-f.State(target).Health;
                    Check(damage>0 || buff,"正式技能与形态实际执行："+variant.displayName);
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(temporary);}

            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(new[]{0});var enemy=f.Unit(new float3(0,0,10),1);
                f.Execution.Apply(owner,enemy,0,new EffectData{Kind=EffectKind.Pull,Amount=20,StopDistance=2.5f},ref f.Library.Value,0);
                Check(math.abs(f.Manager.GetComponentData<Unity.Transforms.LocalTransform>(enemy).Position.z-2.5f)<.001f,"拉拽保留接触距离，不越过来源");
                var state=f.State(enemy);state.DisplacementImmune=true;f.Manager.SetComponentData(enemy,state);
                f.Execution.Apply(owner,enemy,0,new EffectData{Kind=EffectKind.Knockback,Amount=10},ref f.Library.Value,0);
                Check(math.abs(f.Manager.GetComponentData<Unity.Transforms.LocalTransform>(enemy).Position.z-2.5f)<.001f,"位移免疫抵抗共享击退");
            }
            var equipment=AssetDatabase.LoadAssetAtPath<EquipmentCatalog>("Assets/GameData/目录/装备库.asset");
            Check(equipment.items.Length==16 && equipment.Validate()==null,"十六件装备有效且只有三种品质");
            using(var blob=equipment.Compile(catalog))
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(new[]{0});var slots=f.Manager.AddBuffer<EquipmentSlot>(owner);
                var purse=EquipmentLogic.Create(blob);purse.Gold=100000;EquipmentLogic.InitializeSlots(slots,purse);
                int boots=equipment.IndexOf(6003),sword=equipment.IndexOf(6000);
                Check(EquipmentLogic.TryBuy(ref purse,slots,boots) && !EquipmentLogic.TryBuy(ref purse,slots,boots),"唯一疾风靴不能重复购买");
                for(int i=1;i<6;i++) EquipmentLogic.TryBuy(ref purse,slots,sword);
                float gold=purse.Gold;
                Check(!EquipmentLogic.TryBuy(ref purse,slots,sword) && purse.Gold==gold,"满栏购买原子失败");
                Check(EquipmentLogic.TrySell(ref purse,slots,1) && purse.Gold==gold+250,"出售铁剑返还二百五十金币");
                var stats=HeroStatsLogic.Create(new StatSheet{MaxHealth=100,MaxMana=100,CooldownReduction=90,DamageReduction=90},default,10,10);
                var progress=new HeroProgression{Level=1};var unit=f.State(owner);var attack=default(AutoAttackData);
                HeroStatsLogic.Rebuild(ref stats,default,ref progress,ref unit,ref attack);
                Check(unit.CooldownReduction==.5f && unit.DamageReduction==.7f,"基础冷却和减伤上限生效");
                var traits=AbilityTraits.Identity;traits.CooldownCap=.6f;traits.BasicRangeMultiplier=1.2f;
                HeroStatsLogic.Rebuild(ref stats,default,ref progress,ref unit,ref attack,traits);
                HeroStatsLogic.Rebuild(ref stats,default,ref progress,ref unit,ref attack,traits);
                Check(unit.CooldownReduction==.6f && attack.Range==12,"遗物提高冷却上限且重复重算不累乘射程");
                Check(HeroStatsLogic.LifeStealHeal(new StatSheet{LifeSteal=12},50)==6,"吸血使用实际伤害");
            }
            var expedition=AssetDatabase.LoadAssetAtPath<ExpeditionCatalog>("Assets/GameData/目录/远征配置.asset");
            try
            {
                var run=RunState.Begin(expedition.selection.heroes[0],expedition.selection.maps[0],expedition.selection.maps[1],1947);
                run.EquipmentIds[0]=6003;
                var camp=new CampState(run,expedition,equipment.items);
                bool uniqueExcluded=true;for(int i=0;i<100;i++)
                {camp.Roll(run,equipment.items);foreach(int item in camp.Stock) uniqueExcluded&=equipment.items[item].configId!=6003;}
                Check(uniqueExcluded,"连续百次刷新排除已持有唯一装备");
                run.Relics.Add(camp.RelicOptions[0]);run.Stage=2;run.DangerousRoute=true;
                camp=new CampState(run,expedition,equipment.items);
                Check(camp.RelicOptions.Count==4 && !camp.RelicOptions.Contains(run.Relics[0]),"凶险营地四选一且不重复遗物");
            }
            finally {RunState.Clear();GameSelection.Clear();}
        }
    }
}
