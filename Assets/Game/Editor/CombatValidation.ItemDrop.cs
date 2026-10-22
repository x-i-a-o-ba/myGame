using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>
    /// 装备丢弃、拾取、换位与商店位置（Dota 2 式）：丢弃 / 拾取距离校验与编译、换位与交换、丢弃到地面与按装备核对、拾取的空格 / 唯一 / 距离规则、
    /// 拾取与丢弃指令的推进（走到距离内再结算）、商店范围对买卖的限制、地上装备与装备栏提示、BottomHUD 拖动与放置组件、商店面板放置目标与场景商店。
    /// </summary>
    public static partial class CombatValidation
    {
        private static void ValidateItemDrop()
        {
            using(var assets=new Resources())
            {
                var skills=assets.Catalog(new[]{assets.Skill("item_drop_host",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{assets.Damage(1)}})});
                EquipmentDefinition Item(int id,string key,int price,bool unique,StatModifier stat)
                {
                    var item=assets.New<EquipmentDefinition>(); item.configId=id; item.stableId=key; item.displayName=key; item.price=price; item.unique=unique; item.stats=new[]{stat};
                    return item;
                }
                var sword=Item(6911,"item.validation_drop_sword",350,false,new StatModifier(StatKind.AttackDamage,12));
                var boots=Item(6912,"item.validation_drop_boots",455,true,new StatModifier(StatKind.MoveSpeed,1.5f));
                var catalog=assets.New<EquipmentCatalog>(); catalog.items=new[]{sword,boots}; catalog.slotCount=3; catalog.startingGold=5000; catalog.sellRatio=0.5f;
                Check(catalog.Validate()==null && Mathf.Approximately(catalog.dropRange,3.3f) && Mathf.Approximately(catalog.pickupRange,5),
                    "Drop and pick-up ranges default to Dota 2's 100 / 150 units (3.3 / 5 m)");
                catalog.dropRange=0.1f; Check(catalog.Validate()!=null,"Drop range below the minimum is rejected"); catalog.dropRange=3.3f;
                catalog.pickupRange=float.NaN; Check(catalog.Validate()!=null,"NaN pick-up range is rejected");
                catalog.pickupRange=EquipmentCatalog.MaximumRange+1; Check(catalog.Validate()!=null,"Pick-up range above the maximum is rejected"); catalog.pickupRange=5;
                var library=catalog.Compile(skills);
                try
                {
                    Check(Mathf.Approximately(library.Value.DropRange,3.3f) && Mathf.Approximately(library.Value.PickupRange,5),"Compiled library carries the drop and pick-up ranges");
                    using(var world=new World("ItemDropValidation"))
                    {
                        var manager=world.EntityManager;
                        var entity=manager.CreateEntity(typeof(EquipmentSlot),typeof(GroundItem),typeof(EquipmentCommand));
                        var slots=manager.GetBuffer<EquipmentSlot>(entity);
                        var ground=manager.GetBuffer<GroundItem>(entity);
                        var equipment=EquipmentLogic.Create(library);
                        EquipmentLogic.InitializeSlots(slots,equipment);
                        EquipmentLogic.TryBuy(ref equipment,slots,0); EquipmentLogic.TryBuy(ref equipment,slots,1);
                        Check(slots[0].Item==0 && slots[1].Item==1 && slots[2].Item==-1 && equipment.NextGroundId==0,"Two items bought into the first two slots");

                        // 换位：拖到空格时移过去，拖到有装备的格时两格交换；属性合计不变。
                        int changes=equipment.ChangeSerial;
                        Check(EquipmentLogic.TryMove(ref equipment,slots,0,2) && slots[0].Item==-1 && slots[2].Item==0 && equipment.ChangeSerial==changes+1 &&
                              equipment.LastCommand==EquipmentCommandKind.Move && equipment.LastItem==0,"Moving an item to an empty slot moves it");
                        Check(EquipmentLogic.TryMove(ref equipment,slots,2,1) && slots[1].Item==0 && slots[2].Item==1,"Moving onto an occupied slot swaps the two items");
                        var sheet=EquipmentLogic.Sheet(slots,equipment);
                        Check(sheet.AttackDamage==12 && sheet.MoveSpeed==1.5f,"Rearranging slots does not change the equipment stats");
                        Check(!EquipmentLogic.TryMove(ref equipment,slots,0,1) && equipment.LastFailure==EquipmentFailure.EmptySlot &&
                              !EquipmentLogic.TryMove(ref equipment,slots,1,7) && equipment.LastFailure==EquipmentFailure.InvalidSlot,"Moving from an empty or to a missing slot is rejected");
                        changes=equipment.ChangeSerial;
                        Check(!EquipmentLogic.TryMove(ref equipment,slots,1,1) && equipment.LastFailure==EquipmentFailure.None && equipment.ChangeSerial==changes,
                            "Dropping an item back on its own slot changes nothing");

                        // 丢弃：英雄在丢弃距离内才把装备放到落点；期间装备被换位时按装备查找。
                        float3 hero=float3.zero;
                        Check(!EquipmentLogic.TryDrop(ref equipment,slots,ground,1,0,new float3(10,0,0),hero) && equipment.LastFailure==EquipmentFailure.TooFar &&
                              ground.Length==0 && slots[1].Item==0,"Dropping beyond the drop range is rejected without side effects");
                        Check(EquipmentLogic.TryDrop(ref equipment,slots,ground,1,0,new float3(2,0.7f,1),hero) && ground.Length==1 && ground[0].Id==1 && ground[0].Item==0 &&
                              math.all(ground[0].Position==new float3(2,0,1)) && slots[1].Item==-1 && equipment.LastGround==1,"Dropping puts the item on the ground at the drop point");
                        Check(EquipmentLogic.Sheet(slots,equipment).AttackDamage==0,"A dropped item no longer adds its stats");
                        EquipmentLogic.TryMove(ref equipment,slots,2,0);
                        Check(EquipmentLogic.TryDrop(ref equipment,slots,ground,2,1,new float3(-1,0,0),hero) && slots[0].Item==-1 && ground.Length==2 && ground[1].Id==2 && ground[1].Item==1,
                            "A drop order follows the item when it was moved to another slot meanwhile");
                        Check(!EquipmentLogic.TryDrop(ref equipment,slots,ground,0,1,new float3(1,0,0),hero) && equipment.LastFailure==EquipmentFailure.EmptySlot && ground.Length==2,
                            "Dropping an item that is no longer equipped is rejected");

                        // 拾取：地上装备存在、装备栏有空格、唯一装备未拥有，且英雄在拾取距离内。
                        Check(EquipmentLogic.CanPickUp(equipment,slots,ground,1,new float3(30,0,0),false)==EquipmentFailure.None &&
                              EquipmentLogic.CanPickUp(equipment,slots,ground,1,new float3(30,0,0),true)==EquipmentFailure.TooFar &&
                              EquipmentLogic.CanPickUp(equipment,slots,ground,9,hero,false)==EquipmentFailure.GroundItemMissing,"Pick-up checks range only when asked and reports missing items");
                        Check(!EquipmentLogic.TryPickUp(ref equipment,slots,ground,1,new float3(30,0,0)) && equipment.LastFailure==EquipmentFailure.TooFar && ground.Length==2,
                            "Picking up from too far away is rejected");
                        Check(EquipmentLogic.TryPickUp(ref equipment,slots,ground,1,new float3(2,0,5.9f)) && slots[0].Item==0 && ground.Length==1 && ground[0].Id==2 &&
                              equipment.LastCommand==EquipmentCommandKind.PickUp && equipment.LastItem==0 && equipment.LastGround==1,"Picking up fills the first empty slot and removes the ground item");
                        Check(!EquipmentLogic.TryPickUp(ref equipment,slots,ground,1,hero) && equipment.LastFailure==EquipmentFailure.GroundItemMissing,"An item can only be picked up once");
                        EquipmentLogic.TryBuy(ref equipment,slots,1);
                        Check(EquipmentLogic.CanPickUp(equipment,slots,ground,2,hero,false)==EquipmentFailure.AlreadyOwned,"A second copy of a unique item cannot be picked up");
                        EquipmentLogic.TryBuy(ref equipment,slots,0);
                        Check(EquipmentLogic.FreeSlot(slots)<0,"Inventory is full");
                        EquipmentLogic.TryDrop(ref equipment,slots,ground,2,0,new float3(0,0,1),hero);
                        EquipmentLogic.TryBuy(ref equipment,slots,0);
                        Check(ground.Length==2 && ground[1].Id==3 && EquipmentLogic.CanPickUp(equipment,slots,ground,3,hero,true)==EquipmentFailure.InventoryFull,
                            "A full inventory cannot pick up items; ground ids keep counting up");

                        // 拾取 / 丢弃指令：走到距离内才写入装备指令并转为待机；拾取的目标点跟随地上的装备。
                        Check(CombatSimulationSystem.MoveLike(HeroOrderKind.Move) && CombatSimulationSystem.MoveLike(HeroOrderKind.PickUp) && CombatSimulationSystem.MoveLike(HeroOrderKind.DropItem) &&
                              !CombatSimulationSystem.MoveLike(HeroOrderKind.AttackMove) && !CombatSimulationSystem.MoveLike(HeroOrderKind.Hold) && !CombatSimulationSystem.MoveLike(HeroOrderKind.Idle),
                            "Pick-up and drop orders walk like move orders");
                        var commands=manager.GetBuffer<EquipmentCommand>(entity);
                        var order=new HeroOrder{Kind=HeroOrderKind.PickUp,Value=3,Point=new float3(50,0.55f,50)};
                        Check(CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,new float3(8,0.55f,1)) && order.Kind==HeroOrderKind.PickUp &&
                              math.all(order.Point==new float3(0,0.55f,1)) && commands.Length==0,"A pick-up order walks toward the ground item until within range");
                        Check(CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,new float3(4,0.55f,1)) && order.Kind==HeroOrderKind.Idle && commands.Length==1 &&
                              commands[0].Kind==EquipmentCommandKind.PickUp && commands[0].Value==3 && math.all(order.Point==new float3(4,0.55f,1)),
                            "Within pick-up range the order becomes a pick-up command and the hero stops");
                        commands.Clear();
                        order=new HeroOrder{Kind=HeroOrderKind.PickUp,Value=77};
                        Check(CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,hero) && order.Kind==HeroOrderKind.Idle && commands.Length==0,
                            "A pick-up order for a vanished item stops");
                        order=new HeroOrder{Kind=HeroOrderKind.DropItem,Value=1,Item=1,Point=new float3(10,0.55f,0)};
                        Check(!CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,hero) && order.Kind==HeroOrderKind.DropItem && commands.Length==0,
                            "A drop order walks toward the drop point until within range");
                        Check(CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,new float3(7,0.55f,0)) && order.Kind==HeroOrderKind.Idle && commands.Length==1 &&
                              commands[0].Kind==EquipmentCommandKind.Drop && commands[0].Value==1 && commands[0].Argument==1 && math.all(commands[0].Point==new float3(10,0.55f,0)),
                            "Within drop range the order becomes a drop command carrying the slot, item and drop point");
                        commands.Clear();
                        order=new HeroOrder{Kind=HeroOrderKind.Move,Point=new float3(3,0,3)};
                        Check(!CombatSimulationSystem.AdvanceItemOrder(ref order,equipment,ground,commands,hero) && order.Kind==HeroOrderKind.Move && commands.Length==0,
                            "Other orders are not touched by the item order step");

                        // 商店位置：只有在商店范围内才能购买与出售；换位、丢弃、拾取不受限制。
                        var shop=new ShopSite{Position=new float3(0,0,14),Radius=8,Limited=true};
                        Check(!EquipmentLogic.InShop(shop,hero) && EquipmentLogic.InShop(shop,new float3(0,0.55f,6.5f)) && EquipmentLogic.InShop(shop,new float3(5.6f,0,19.6f)) &&
                              EquipmentLogic.InShop(new ShopSite{Limited=false},new float3(100,0,100)),"Shop range is a flat circle; an unlimited shop works anywhere");
                        Check(EquipmentLogic.Within(hero,new float3(3.3f,5,0),3.3f) && !EquipmentLogic.Within(hero,new float3(3.4f,0,0),3.3f),"Range checks are flat with a small slack");
                        int sold=slots[0].Item; float gold=equipment.Gold; int serial=equipment.CommandSerial;
                        Check(!CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.Sell,Value=0},hero) &&
                              equipment.LastFailure==EquipmentFailure.NotInShop && equipment.LastItem==sold && slots[0].Item==sold && equipment.Gold==gold && equipment.CommandSerial==serial+1,
                            "Selling outside the shop range is rejected without side effects");
                        float3 inside=new float3(0,0.55f,10);
                        Check(CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.Sell,Value=0},inside) &&
                              slots[0].Item==-1 && equipment.Gold>gold,"Selling inside the shop range refunds gold");
                        gold=equipment.Gold;
                        Check(!CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.Buy,Value=0},hero) &&
                              equipment.LastFailure==EquipmentFailure.NotInShop && equipment.Gold==gold && slots[0].Item==-1,"Buying outside the shop range is rejected without side effects");
                        Check(CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.Buy,Value=0},inside) &&
                              slots[0].Item==0 && equipment.Gold<gold,"Buying inside the shop range works");
                        Check(CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.Move,Value=0,Argument=1},hero) &&
                              slots[0].Item==1 && slots[1].Item==0,"Rearranging works away from the shop");
                        Check(CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,
                                  new EquipmentCommand{Kind=EquipmentCommandKind.Drop,Value=1,Argument=0,Point=new float3(1,0,1)},hero) && slots[1].Item==-1 && ground.Length==3 &&
                              CombatSimulationSystem.ApplyEquipmentCommand(ref equipment,slots,ground,shop,new EquipmentCommand{Kind=EquipmentCommandKind.PickUp,Value=4},hero) &&
                              slots[1].Item==0 && ground.Length==2,"Dropping and picking up work away from the shop");
                        Check(EquipmentLogic.Describe(EquipmentFailure.NotInShop).Contains("商店") && EquipmentLogic.Describe(EquipmentFailure.TooFar).Length>0 &&
                              EquipmentLogic.Describe(EquipmentFailure.GroundItemMissing).Contains("地上"),"New failures have Chinese descriptions");

                        // 地上的装备与装备栏的提示。
                        var view=new AbilityTooltipView();
                        var abilities=skills.Compile();
                        try
                        {
                            ref var items=ref library.Value.Items;
                            var context=new ItemTooltipContext{Definition=sword,Gold=equipment.Gold,FromGround=true,Distance=4.24f};
                            AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[0],context,view);
                            Check(view.Visible && view.Status.Contains("右键拾取") && view.Status.Contains("4.2 米") && view.Status.Contains("Esc"),
                                "Ground item tooltip explains right-click pick-up, distance and how to close it");
                            context.PickingUp=true; AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[0],context,view);
                            Check(view.Status.Contains("正在走过去拾取"),"Ground item tooltip shows that the hero is walking to pick it up");
                            context.PickingUp=false; context.PickupFailure=EquipmentFailure.InventoryFull; AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[0],context,view);
                            Check(view.Status.Contains("装备栏已满") && view.Status.Contains("丢弃"),"Ground item tooltip explains a full inventory");
                            context.PickupFailure=EquipmentFailure.AlreadyOwned; AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[1],context,view);
                            Check(view.Status.Contains("已拥有"),"Ground item tooltip explains an owned unique item");
                            context=new ItemTooltipContext{Definition=sword,Gold=equipment.Gold};
                            AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[0],context,view);
                            Check(view.Status.Contains("商店范围") && view.Status.Contains("拖到地面丢弃") && view.Status.Contains("换位"),
                                "Equipped item tooltip explains where to sell and how to drop or rearrange");
                            context.ShopOpen=true; AbilityTooltipBuilder.BuildItem(ref abilities.Value,ref items[0],context,view);
                            Check(view.Status.Contains("右键出售") && view.Status.Contains("拖到商店") && view.Status.Contains("175 金币"),"Equipped item tooltip explains selling while the shop is open");
                        }
                        finally { abilities.Dispose(); }
                    }
                }
                finally { library.Dispose(); }
            }
            var host=new GameObject("Shop Validation");
            try
            {
                var site=host.AddComponent<GameShop>();
                host.transform.position=new Vector3(3,0,-4);
                site.radius=100; bool high=Mathf.Approximately(site.Radius,GameShop.MaximumRadius);
                site.radius=0.5f; bool low=Mathf.Approximately(site.Radius,GameShop.MinimumRadius);
                Check(high && low && site.Position==new Vector3(3,0,-4),"Shop radius is clamped and the position follows the transform");
            }
            finally { Object.DestroyImmediate(host); }
            ValidateItemDropHud();
        }

        /// <summary>BottomHUD 装备格的拖动源与放置目标、商店按钮的距离提示、商店面板的放置目标，以及 Game 场景的商店引用。</summary>
        private static void ValidateItemDropHud()
        {
            var hud=AssetDatabase.LoadAssetAtPath<GameObject>(BottomHudPath);
            var panel=hud?hud.GetComponent<UIPanel>():null;
            Check(panel,"Bottom HUD prefab has a UI panel");
            if(!panel) return;
            string missing=Missing(panel.toggles,b=>b.key,b=>b.target,new[]{CombatHud.ShopKeys.Far})??Missing(panel.texts,b=>b.key,b=>b.target,new[]{CombatHud.ShopKeys.Distance});
            Check(missing==null,"Shop button shows when the hero is out of the shop range and how far it is: "+missing);
            int drags=0,drops=0;
            foreach(var drag in hud.GetComponentsInChildren<UIDragAction>(true))
            {
                int slot=drag.value;
                if(drag.beginCommand!=CombatHud.DragCommand || drag.endCommand!=CombatHud.DragEndCommand || slot<0 || slot>=CombatHud.EquipmentSlots) continue;
                bool filled=false,icon=false;
                foreach(var binding in panel.toggles)
                    filled|=binding.key==CombatHud.EquipmentKeys.Filled[slot] && binding.target==drag.requireActive;
                foreach(var binding in panel.images)
                    icon|=binding.key==CombatHud.EquipmentKeys.Icons[slot] && binding.target==drag.ghost;
                if(filled && icon && drag.dimmed>0 && drag.dimmed<1 && Raycast(drag.gameObject)) drags|=1<<slot;
            }
            foreach(var drop in hud.GetComponentsInChildren<UIDropAction>(true))
                if(drop.command==CombatHud.DropOnCommand && drop.value>=0 && drop.value<CombatHud.EquipmentSlots && Raycast(drop.gameObject) &&
                   drop.GetComponent<UIDragAction>()) drops|=1<<drop.value;
            int all=(1<<CombatHud.EquipmentSlots)-1;
            Check(drags==all,"Every equipment slot is a drag source that needs an item and drags a copy of its icon");
            Check(drops==all,"Every equipment slot is also a drop target for rearranging");
            var shop=AssetDatabase.LoadAssetAtPath<GameObject>(ShopPanelPath);
            var shopDrop=shop?shop.GetComponent<UIDropAction>():null;
            Check(shopDrop && shopDrop.command==CombatHud.ShopDropCommand && Raycast(shop),"Shop panel accepts dragged equipment for selling");

            string scene=File.ReadAllText("Assets/Scenes/Game.unity");
            string script=AssetDatabase.AssetPathToGUID("Assets/Game/Runtime/Combat/GameShop.cs");
            var reference=Regex.Match(scene,@"\n  shop: \{fileID: (\d+)\}");
            var component=reference.Success && reference.Groups[1].Value!="0"?Regex.Match(scene,@"--- !u!114 &"+reference.Groups[1].Value+@"\n(?:(?!--- ).*\n)*"):Match.Empty;
            var radius=component.Success?Regex.Match(component.Value,@"\n  radius: ([0-9.]+)"):Match.Empty;
            float value=radius.Success?float.Parse(radius.Groups[1].Value,CultureInfo.InvariantCulture):0;
            Check(!string.IsNullOrEmpty(script) && component.Success && component.Value.Contains("guid: "+script) && value>=GameShop.MinimumRadius && value<=GameShop.MaximumRadius,
                "Game scene places a GameShop with a valid radius and assigns it to the combat controller");
        }
    }
}
