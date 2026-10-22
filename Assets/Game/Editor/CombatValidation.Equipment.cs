using System;
using System.Collections.Generic;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>
    /// 属性与装备：装备库校验与编译、金币与买卖、唯一与满格、出售返还、属性直接相加（含上限与当前生命同步）、
    /// 暴击 / 吸血 / 伤害减免 / 法力回复、装备与属性提示、BottomHUD 与商店面板绑定、项目装备库与编号登记。
    /// </summary>
    public static partial class CombatValidation
    {
        private const string EquipmentCatalogPath="Assets/Game/Equipment/装备库.asset";
        private const string ShopPanelPath="Assets/Resources/UI/Panels/UI.Game.Shop.prefab";

        [Serializable] public sealed class ItemLedger { public int schemaVersion; public List<ItemLedgerEntry> entries; }
        [Serializable] public sealed class ItemLedgerEntry { public int id; public string category,assetKey,stableId,path,name; public bool retired; }

        private static void ValidateEquipment()
        {
            using(var assets=new Resources())
            {
                var slow=assets.Buff("buff.validation_item_slow",2); slow.displayName="验证命中减速";
                slow.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.MoveSpeed,operation=ModifierOperation.Percent,value=-0.3f}};
                var applySlow=assets.New<ApplyBuffEffect>(); applySlow.buff=slow;
                var skills=assets.Catalog(new[]{assets.Skill("item_host",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{assets.Damage(1)}})},new[]{slow});
                EquipmentDefinition Item(int id,string key,int price,bool unique,params StatModifier[] stats)
                {
                    var item=assets.New<EquipmentDefinition>(); item.configId=id; item.stableId=key; item.displayName=key; item.price=price; item.unique=unique; item.stats=stats;
                    return item;
                }
                var sword=Item(6901,"item.validation_sword",350,false,new StatModifier(StatKind.AttackDamage,12),new StatModifier(StatKind.AttackDamage,3));
                var boots=Item(6902,"item.validation_boots",455,true,new StatModifier(StatKind.MoveSpeed,1.5f));
                var bow=Item(6903,"item.validation_bow",900,true,new StatModifier(StatKind.AttackSpeed,20)); bow.onHitEffects=new EffectDefinition[]{applySlow};
                bow.quality=EquipmentQuality.Rare; bow.description="验证说明";
                var catalog=assets.New<EquipmentCatalog>(); catalog.items=new[]{sword,boots,bow}; catalog.slotCount=3; catalog.startingGold=1000; catalog.sellRatio=0.5f;
                Check(catalog.Validate()==null,"Valid equipment catalog passes");
                Check(catalog.SellPrice(sword)==175 && catalog.SellPrice(boots)==227 && catalog.SellPrice(null)==0,"Sell price refunds the sell ratio of the price, rounded down");
                Check(catalog.IndexOf(6903)==2 && catalog.IndexOf(6999)<0,"Catalog finds items by numeric id");
                var library=catalog.Compile(skills);
                var abilities=skills.Compile();
                try
                {
                    ref var items=ref library.Value.Items;
                    Check(library.Value.SlotCount==3 && library.Value.StartingGold==1000 && items.Length==3 && items[0].Stats.AttackDamage==15 && items[0].SellPrice==175 &&
                          items[2].Unique && items[2].OnHit.Length==1 && items[2].OnHit[0].Kind==EffectKind.ApplyBuff && items[2].OnHit[0].Buff==0,
                        "Compiled equipment merges same-kind stats and resolves on-hit buffs against the skill catalog");
                    Check(sword.stats.Length==2 && sword.stats[0].value==12 && bow.price==900,"Compiling equipment does not mutate the source definitions");
                    using(var world=new World("EquipmentValidation"))
                    {
                        var manager=world.EntityManager;
                        var entity=manager.CreateEntity(typeof(EquipmentSlot),typeof(ActiveBuff));
                        var slots=manager.GetBuffer<EquipmentSlot>(entity);
                        var equipment=EquipmentLogic.Create(library);
                        EquipmentLogic.InitializeSlots(slots,equipment);
                        Check(equipment.Gold==1000 && equipment.LastItem==-1 && slots.Length==3 && EquipmentLogic.FreeSlot(slots)==0,"New run starts with the catalog's gold and empty slots");
                        Check(EquipmentLogic.TryBuy(ref equipment,slots,0) && equipment.Gold==650 && slots[0].Item==0 && equipment.ChangeSerial==1 && equipment.LastFailure==EquipmentFailure.None,
                            "Buying spends gold and fills the first empty slot");
                        Check(EquipmentLogic.TryBuy(ref equipment,slots,0) && slots[1].Item==0 && equipment.Gold==300,"Non-unique items can be bought more than once");
                        Check(EquipmentLogic.Sheet(slots,equipment).AttackDamage==30,"Equipment stats add directly across slots");
                        Check(!EquipmentLogic.TryBuy(ref equipment,slots,2) && equipment.LastFailure==EquipmentFailure.NotEnoughGold && equipment.Gold==300 && slots[2].Item==-1 &&
                              equipment.CommandSerial==3 && equipment.ChangeSerial==2,"Buying without enough gold fails without side effects");
                        EquipmentLogic.GainGold(ref equipment,float.NaN); EquipmentLogic.GainGold(ref equipment,-5); EquipmentLogic.GainGold(ref equipment,float.PositiveInfinity);
                        Check(equipment.Gold==300 && equipment.GoldEarned==0,"Invalid gold gains are ignored");
                        EquipmentLogic.GainGold(ref equipment,160.5f);
                        Check(Mathf.Approximately(equipment.Gold,460.5f) && Mathf.Approximately(equipment.GoldEarned,160.5f),"Kills add gold and track gold earned");
                        Check(EquipmentLogic.TryBuy(ref equipment,slots,1) && slots[2].Item==1 && Mathf.Approximately(equipment.Gold,5.5f),"Unique item can be bought once");
                        EquipmentLogic.GainGold(ref equipment,5000);
                        Check(!EquipmentLogic.TryBuy(ref equipment,slots,1) && equipment.LastFailure==EquipmentFailure.AlreadyOwned,"Unique item cannot be bought twice");
                        Check(!EquipmentLogic.TryBuy(ref equipment,slots,2) && equipment.LastFailure==EquipmentFailure.InventoryFull,"Full inventory rejects purchases");
                        Check(!EquipmentLogic.TryBuy(ref equipment,slots,7) && equipment.LastFailure==EquipmentFailure.InvalidItem && !EquipmentLogic.TryBuy(ref equipment,slots,-1),
                            "Unknown item index is rejected");
                        float gold=equipment.Gold;
                        Check(EquipmentLogic.TrySell(ref equipment,slots,1) && slots[1].Item==-1 && Mathf.Approximately(equipment.Gold,gold+175) && equipment.LastItem==0 &&
                              equipment.LastCommand==EquipmentCommandKind.Sell,"Selling clears the slot and refunds the sell price");
                        Check(!EquipmentLogic.TrySell(ref equipment,slots,1) && equipment.LastFailure==EquipmentFailure.EmptySlot &&
                              !EquipmentLogic.TrySell(ref equipment,slots,9) && equipment.LastFailure==EquipmentFailure.InvalidSlot,"Selling an empty or missing slot is rejected");
                        Check(EquipmentLogic.TryBuy(ref equipment,slots,2) && slots[1].Item==2 && EquipmentLogic.Owns(slots,2),"Freed slot is reused by the next purchase");
                        var sheet=EquipmentLogic.Sheet(slots,equipment);
                        Check(sheet.AttackDamage==15 && sheet.MoveSpeed==1.5f && sheet.AttackSpeed==20,"Equipment sheet follows the current slots");

                        // 装备属性与英雄属性直接相加；百分点在写入单位时换成执行器使用的小数。
                        var stats=HeroStatsLogic.Create(new StatSheet{MaxHealth=300,AttackDamage=30,MoveSpeed=10,MaxMana=100,ManaRegen=AbilityEffects.BaseManaRegen,
                            AttackRange=20,CriticalDamage=HeroStatsLogic.DefaultCriticalDamage},default,25);
                        var progression=new HeroProgression{Level=1,ExperienceMultiplier=1};
                        var unit=new AbilityUnit{AttackSpeedMultiplier=1}; var attack=new AutoAttackData{Range=20,AcquisitionRange=25};
                        HeroStatsLogic.Rebuild(ref stats,sheet,ref progression,ref unit,ref attack);
                        unit.Health=unit.MaxHealth; unit.Mana=unit.MaxMana;
                        Check(unit.BaseAttack==45 && unit.BaseSpeed==11.5f && Mathf.Approximately(unit.AttackSpeedBonus,0.2f) && stats.Equipment.AttackDamage==15 &&
                              stats.Total.AttackDamage==45 && stats.Version==1,"Equipment stats add directly to hero stats");
                        unit.Health=150; unit.Mana=40;
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{MaxHealth=200,MaxMana=50},ref progression,ref unit,ref attack);
                        Check(unit.MaxHealth==500 && unit.Health==350 && unit.MaxMana==150 && unit.Mana==90,"Max health and mana bonuses also raise current values");
                        HeroStatsLogic.Rebuild(ref stats,default,ref progression,ref unit,ref attack);
                        Check(unit.MaxHealth==300 && unit.Health==300 && unit.MaxMana==100 && unit.Mana==90,"Losing a max health bonus clamps current health without killing");
                        unit.Health=1;
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{MaxHealth=-1000},ref progression,ref unit,ref attack);
                        Check(unit.MaxHealth==1 && unit.Health==1,"Negative max health totals keep the hero alive at 1");
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{AttackRange=3},ref progression,ref unit,ref attack);
                        Check(attack.Range==23 && attack.AcquisitionRange==28,"Attack range bonus extends attack and acquisition range");
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{AttackSpeed=-500,SkillAmplify=30},ref progression,ref unit,ref attack);
                        Check(Mathf.Approximately(unit.AttackSpeedBonus,-0.8f) && Mathf.Approximately(unit.SkillAmplify,0.3f) && attack.Range==20,
                            "Attack speed bottoms out at -80% and skill amplify converts percentage points");
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{ExperienceGain=-200},ref progression,ref unit,ref attack);
                        Check(progression.ExperienceMultiplier==0,"Experience multiplier never goes negative");

                        // 伤害减免在统一伤害入口生效，上限 80%。
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{DamageReduction=25},ref progression,ref unit,ref attack);
                        var buffs=manager.GetBuffer<ActiveBuff>(entity);
                        var target=new AbilityUnit{Health=100,MaxHealth=100,DamageReduction=unit.DamageReduction};
                        var outcome=AbilityEffects.Damage(ref target,buffs,ref abilities.Value,40);
                        Check(Mathf.Approximately(unit.DamageReduction,0.25f) && target.Health==70 && outcome.ActualDamage==30 && outcome.RequestedDamage==40,
                            "Damage reduction lowers damage taken (40 x 75%)");
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{DamageReduction=95},ref progression,ref unit,ref attack);
                        target=new AbilityUnit{Health=100,MaxHealth=100,DamageReduction=0.95f};
                        AbilityEffects.Damage(ref target,buffs,ref abilities.Value,100);
                        Check(Mathf.Approximately(unit.DamageReduction,0.8f) && Mathf.Approximately(target.Health,80),"Damage reduction is capped at 80%");

                        // 法力回复：基础 3 点/秒 + 加成。
                        HeroStatsLogic.Rebuild(ref stats,new StatSheet{ManaRegen=1.5f},ref progression,ref unit,ref attack);
                        unit.Mana=0; unit.Health=unit.MaxHealth;
                        AbilityEffects.Tick(ref unit,buffs,ref abilities.Value,1,2);
                        Check(Mathf.Approximately(unit.ManaRegenBonus,1.5f) && Mathf.Approximately(stats.Total.ManaRegen,4.5f) && Mathf.Approximately(unit.Mana,9),
                            "Mana regeneration adds on top of the base 3 per second (2 s x 4.5)");

                        // 暴击：同一种子与出手序号结果固定；几率与连续暴击符合独立判定。
                        var critical=new StatSheet{CriticalChance=25,CriticalDamage=150};
                        int crits=0,pairs=0; bool stable=true,previous=false;
                        for(int i=1;i<=4000;i++)
                        {
                            float multiplier=HeroStatsLogic.CriticalMultiplier(critical,7,i,out bool crit);
                            stable&=HeroStatsLogic.CriticalMultiplier(critical,7,i,out bool again)==multiplier && again==crit && (crit?multiplier==1.5f:multiplier==1);
                            if(crit) { crits++; if(previous) pairs++; }
                            previous=crit;
                        }
                        Check(stable && crits>850 && crits<1150,"Critical strikes are deterministic per seed and attack and match the 25% chance");
                        Check(pairs>120,"Critical strikes are independent between consecutive attacks (back-to-back crits happen)");
                        bool otherSeed=false;
                        for(int i=1;i<=64 && !otherSeed;i++)
                            otherSeed|=HeroStatsLogic.CriticalMultiplier(critical,7,i,out bool _)!=HeroStatsLogic.CriticalMultiplier(critical,8,i,out bool _);
                        Check(otherSeed,"A different session seed gives a different critical sequence");
                        Check(HeroStatsLogic.CriticalMultiplier(new StatSheet{CriticalChance=0,CriticalDamage=300},7,1,out bool never)==1 && !never &&
                              HeroStatsLogic.CriticalMultiplier(new StatSheet{CriticalChance=100,CriticalDamage=200},7,1,out bool always)==2 && always &&
                              HeroStatsLogic.CriticalMultiplier(new StatSheet{CriticalChance=100,CriticalDamage=50},7,1,out bool _)==1,
                            "Zero chance never crits; 100% always crits; critical damage below 100% never lowers damage");
                        Check(Mathf.Approximately(HeroStatsLogic.LifeStealHeal(new StatSheet{LifeSteal=18},50),9) && HeroStatsLogic.LifeStealHeal(new StatSheet{LifeSteal=18},-5)==0 &&
                              HeroStatsLogic.LifeStealHeal(new StatSheet{LifeSteal=-10},50)==0,"Life steal heals a share of the actual damage dealt");

                        // 装备与属性提示。
                        var view=new AbilityTooltipView();
                        var context=new ItemTooltipContext{Definition=bow,Buffs=new[]{slow},Gold=100,FromShop=true,BuyFailure=EquipmentFailure.NotEnoughGold};
                        AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[2],context,view);
                        Check(view.Visible && view.NoLevel && view.Name=="item.validation_bow" && view.Kind.Contains("稀有") && view.Kind.Contains("900 金币") && view.Description=="验证说明",
                            "Item tooltip shows name, quality, price and description");
                        Check(view.Properties.Contains("+20% 攻击速度") && view.Values.Contains("普攻命中") && view.Values.Contains("验证命中减速"),"Item tooltip lists stats and on-hit effects");
                        Check(view.Notes.Contains("唯一") && view.Notes.Contains("出售返还 450 金币") && view.Status.Contains("还差 800 金币"),"Item tooltip explains unique, refund and missing gold");
                        context.FromShop=false; context.ShopOpen=true;
                        AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[2],context,view);
                        Check(view.Status.Contains("右键出售"),"Equipped item tooltip explains right-click selling");
                        var shown=HeroStatsLogic.Create(new StatSheet{AttackDamage=30,CriticalDamage=150},new StatSheet{AttackDamage=3},20);
                        var shownLevel=new HeroProgression{Level=3,ExperienceMultiplier=1};
                        var shownUnit=new AbilityUnit{AttackSpeedMultiplier=1,Attack=51}; var shownAttack=new AutoAttackData{BaseAttackTime=1};
                        HeroStatsLogic.Rebuild(ref shown,new StatSheet{AttackDamage=15,CooldownReduction=90},ref shownLevel,ref shownUnit,ref shownAttack);
                        AbilityTooltipBuilder.BuildStats(shown,shownUnit,shownAttack,3,"验证英雄",null,view);
                        Check(view.Kind.Contains("验证英雄") && view.Kind.Contains("等级 3") && view.Values.Contains("成长 +6") && view.Values.Contains("装备 +15") &&
                              view.Values.Contains("上限 80%"),"Stats tooltip shows totals with growth, equipment and cap notes");
                    }
                }
                finally { library.Dispose(); abilities.Dispose(); }

                sword.configId=42; Check(catalog.Validate()!=null,"Equipment id outside 6000-6999 is rejected"); sword.configId=6901;
                boots.configId=6901; Check(catalog.Validate()!=null,"Duplicate equipment ids are rejected"); boots.configId=6902;
                boots.stableId=sword.stableId; Check(catalog.Validate()!=null,"Duplicate equipment stable ids are rejected"); boots.stableId="item.validation_boots";
                sword.stats=new[]{new StatModifier((StatKind)99,1)}; Check(catalog.Validate()!=null,"Unknown equipment stat is rejected");
                sword.stats=new[]{new StatModifier(StatKind.AttackDamage,float.NaN)}; Check(catalog.Validate()!=null,"NaN equipment stat is rejected");
                sword.stats=Array.Empty<StatModifier>(); Check(catalog.Validate()!=null,"Equipment without stats or on-hit effects is rejected");
                sword.stats=new[]{new StatModifier(StatKind.AttackDamage,12)};
                sword.price=-1; Check(catalog.Validate()!=null,"Negative price is rejected"); sword.price=350;
                catalog.items=new[]{sword,sword}; Check(catalog.Validate()!=null,"Same equipment listed twice is rejected");
                catalog.items=new[]{sword,null}; Check(catalog.Validate()!=null,"Empty catalog entry is rejected"); catalog.items=new[]{sword,boots,bow};
                catalog.slotCount=7; Check(catalog.Validate()!=null,"More than six slots is rejected"); catalog.slotCount=3;
                catalog.sellRatio=1.5f; Check(catalog.Validate()!=null,"Sell ratio above 1 is rejected"); catalog.sellRatio=0.5f;
                bow.onHitEffects=new EffectDefinition[]{null}; Check(catalog.Validate()!=null,"Null on-hit effect is rejected");
                var unregistered=assets.New<ApplyBuffEffect>(); unregistered.buff=assets.Buff("buff.validation_unregistered",1);
                bow.onHitEffects=new EffectDefinition[]{unregistered};
                Check(catalog.Validate()==null && Rejects(()=>{ using(var bad=catalog.Compile(skills)) { } }),"On-hit effect with an unregistered buff is rejected at compile time");
                bow.onHitEffects=new EffectDefinition[]{applySlow};
                Check(Rejects(()=>{ using(var bad=catalog.Compile(null)) { } }),"On-hit effects require a skill catalog to resolve buffs");
            }
            ValidateEquipmentHud();
            ValidateProjectEquipment();
        }

        /// <summary>BottomHUD 的属性栏、金币、装备栏与商店按钮，以及商店面板的绑定、按钮与悬停源。</summary>
        private static void ValidateEquipmentHud()
        {
            Check(CombatHud.IsHoverValue(CombatHud.EquipmentHoverBase) && CombatHud.IsHoverValue(CombatHud.EquipmentHoverBase+CombatHud.EquipmentSlots-1) &&
                  CombatHud.IsHoverValue(CombatHud.StatsHover) && CombatHud.IsHoverValue(CombatHud.ShopHoverBase+CombatHud.ShopCards-1) &&
                  !CombatHud.IsHoverValue(CombatHud.PassiveSlot+1) && !CombatHud.IsHoverValue(CombatHud.EquipmentHoverBase+CombatHud.EquipmentSlots) &&
                  !CombatHud.IsHoverValue(CombatHud.ShopHoverBase+CombatHud.ShopCards),"Hover values for equipment, stats and shop cards do not overlap");
            var hud=AssetDatabase.LoadAssetAtPath<GameObject>(BottomHudPath);
            var panel=hud?hud.GetComponent<UIPanel>():null;
            Check(panel,"Bottom HUD prefab has a UI panel");
            string missing=Missing(panel.texts,b=>b.key,b=>b.target,CombatHud.StatsKeys.Texts)
                ??Missing(panel.texts,b=>b.key,b=>b.target,CombatHud.EquipmentKeys.Texts)
                ??Missing(panel.images,b=>b.key,b=>b.target,CombatHud.EquipmentKeys.Images)
                ??Missing(panel.toggles,b=>b.key,b=>b.target,CombatHud.EquipmentKeys.Toggles)
                ??Missing(panel.toggles,b=>b.key,b=>b.target,new[]{CombatHud.ShopKeys.Open});
            Check(missing==null,"Bottom HUD binds stats, gold, equipment slots and the shop button: "+missing);
            int hovers=0,sells=0; bool stats=false,shop=false;
            foreach(var hover in hud.GetComponentsInChildren<UIHoverAction>(true))
            {
                if(hover.command!=CombatHud.HoverCommand || !Raycast(hover.gameObject)) continue;
                int slot=hover.value-CombatHud.EquipmentHoverBase;
                if(slot>=0 && slot<CombatHud.EquipmentSlots) hovers|=1<<slot;
                stats|=hover.value==CombatHud.StatsHover;
            }
            foreach(var pointer in hud.GetComponentsInChildren<UIPointerAction>(true))
                if(pointer.command==CombatHud.SellCommand && pointer.button==PointerEventData.InputButton.Right && pointer.value>=0 && pointer.value<CombatHud.EquipmentSlots &&
                   Raycast(pointer.gameObject)) sells|=1<<pointer.value;
            foreach(var action in hud.GetComponentsInChildren<UIAction>(true)) shop|=action.command==CombatHud.ShopCommand && action.GetComponent<Button>();
            int all=(1<<CombatHud.EquipmentSlots)-1;
            Check(hovers==all && sells==all,"Every equipment slot has a hover source and a right-click sell source with raycast targets");
            Check(stats && shop,"Bottom HUD has a stats hover source and a shop button");

            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ShopPanelPath);
            var shopPanel=prefab?prefab.GetComponent<UIPanel>():null;
            Check(shopPanel && shopPanel.panelId==CombatHud.ShopPanel && shopPanel.blocksPointer && !prefab.activeSelf,
                "Shop panel prefab matches its id, blocks the pointer and starts hidden");
            missing=Missing(shopPanel.texts,b=>b.key,b=>b.target,CombatHud.ShopKeys.Texts)
                ??Missing(shopPanel.images,b=>b.key,b=>b.target,CombatHud.ShopKeys.Images)
                ??Missing(shopPanel.toggles,b=>b.key,b=>b.target,CombatHud.ShopKeys.Toggles);
            Check(missing==null,"Shop panel binds every key the HUD publishes: "+missing);
            int buys=0,cards=0,pages=0; bool close=false;
            foreach(var action in prefab.GetComponentsInChildren<UIAction>(true))
            {
                if(!action.GetComponent<Button>()) continue;
                if(action.command==CombatHud.BuyCommand && action.value>=0 && action.value<CombatHud.ShopCards) buys|=1<<action.value;
                if(action.command==CombatHud.PageCommand && (action.value==0 || action.value==1)) pages|=1<<action.value;
                close|=action.command==CombatHud.ShopCommand;
            }
            foreach(var hover in prefab.GetComponentsInChildren<UIHoverAction>(true))
            {
                int card=hover.value-CombatHud.ShopHoverBase;
                if(hover.command==CombatHud.HoverCommand && card>=0 && card<CombatHud.ShopCards && Raycast(hover.gameObject)) cards|=1<<card;
            }
            int every=(1<<CombatHud.ShopCards)-1;
            Check(buys==every && cards==every,"Every shop card has a buy button and a hover source");
            Check(pages==3 && close,"Shop panel has previous / next page buttons and a close button");
            Check(ResourcePaths.Resolve(CombatHud.ShopPanel)=="UI/Panels/UI.Game.Shop","Resource catalog registers the shop panel");
        }

        /// <summary>项目装备库：配置合法、覆盖全部品质、图标与说明齐全、能用项目技能库编译、场景已引用、编号已登记。</summary>
        private static void ValidateProjectEquipment()
        {
            var project=AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(EquipmentCatalogPath);
            Check(project && project.Validate()==null && project.items.Length>=18 && project.slotCount==CombatHud.EquipmentSlots,
                "Project equipment catalog is valid with at least 18 items and six slots");
            string incomplete=null; var qualities=new HashSet<EquipmentQuality>(); bool onHit=false,unique=false;
            foreach(var item in project.items)
            {
                if(incomplete==null && (!item.icon || string.IsNullOrWhiteSpace(item.displayName) || string.IsNullOrWhiteSpace(item.description) || item.configId==0))
                    incomplete=item.name;
                qualities.Add(item.quality); unique|=item.unique; onHit|=item.onHitEffects!=null && item.onHitEffects.Length>0;
            }
            Check(incomplete==null,"Every project item has an id, a name, an icon and a description: "+incomplete);
            Check(qualities.Count==Enum.GetValues(typeof(EquipmentQuality)).Length && unique && onHit,"Project items cover every quality and include unique and on-hit items");
            var skills=AssetDatabase.LoadAssetAtPath<SkillCatalog>(AbilityFoundationAssets.CatalogPath);
            var library=project.Compile(skills);
            try
            {
                bool resolved=true;
                for(int i=0;i<library.Value.Items.Length;i++)
                    for(int e=0;e<library.Value.Items[i].OnHit.Length;e++)
                    {
                        var effect=library.Value.Items[i].OnHit[e];
                        resolved&=effect.Kind!=EffectKind.ApplyBuff || (effect.Buff>=0 && effect.Buff<skills.buffs.Length);
                    }
                Check(library.Value.Items.Length==project.items.Length && library.Value.StartingGold==project.startingGold && resolved,
                    "Project equipment compiles against the game skill catalog");
            }
            finally { library.Dispose(); }
            string guid=AssetDatabase.AssetPathToGUID(EquipmentCatalogPath);
            Check(!string.IsNullOrEmpty(guid) && File.ReadAllText("Assets/Scenes/Game.unity").Contains("equipmentCatalog: {fileID: 11400000, guid: "+guid+", type: 2}"),
                "Game scene assigns the project equipment catalog");
            var ledger=JsonUtility.FromJson<ItemLedger>(File.ReadAllText("Docs/游戏配置/编号登记.json"));
            string unregistered=ledger!=null && ledger.entries!=null?null:"编号登记无法读取";
            if(unregistered==null)
                foreach(var item in project.items)
                {
                    bool found=false;
                    if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item,out string itemGuid,out long localId))
                    {
                        string key=itemGuid+":"+localId;
                        foreach(var entry in ledger.entries)
                            if(entry.category=="item" && entry.id==item.configId && entry.assetKey==key && entry.stableId==item.stableId && !entry.retired) { found=true; break; }
                    }
                    if(!found) { unregistered=item.name; break; }
                }
            Check(unregistered==null,"Every project item is registered in the numeric id ledger: "+unregistered);
        }

        private static bool Raycast(GameObject root)
        {
            foreach(var graphic in root.GetComponentsInChildren<Graphic>(true)) if(graphic.raycastTarget) return true;
            return false;
        }
    }
}
