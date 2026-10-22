using System;
using System.Collections;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Foundation.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>显式命令行启用的Player集成检查；正常游戏不执行。</summary>
    public sealed class GameCombatProbe : MonoBehaviour
    {
        private bool failed;
        private string output;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--game-smoke")<0) { enabled=false; yield break; }
            output=Path.Combine(Application.persistentDataPath,"GameFoundation");
            int index=Array.IndexOf(args,"--game-output"); if(index>=0 && index+1<args.Length) output=args[index+1];
            Directory.CreateDirectory(output);
            var controller=GetComponent<GameCombatController>();
            float deadline=Time.realtimeSinceStartup+20;
            while(!controller.IsReady && Time.realtimeSinceStartup<deadline) yield return null;
            Check(controller.IsReady,"Game initializes composed hero");
            if(failed) { Finish(controller); yield break; }
            var manager=World.DefaultGameObjectInjectionWorld.EntityManager;
            var originalHero=controller.hero;
            var indicator=UnityEngine.Object.FindFirstObjectByType<GameAbilityIndicator>();
            Check(indicator && indicator.HasBindings,"Authored indicator references");
            yield return null; yield return null;
            Check(controller.Wave.Wave==1 && controller.State.AliveEnemies>0 && controller.State.AliveEnemies<=controller.enemyCount,"First difficulty wave spawns within alive cap");
            Check(controller.Progression.Level==1 && controller.Progression.SkillPoints==1,"Hero starts at level 1 with one skill point");
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[0].Level==0,"Skills start unlearned");
            Click(1);
            Check(!indicator.IsAiming,"Unlearned skill cannot enter aim");
            // 直接写入权威累计经验，模拟一段击杀进度；升级、技能点与属性成长由模拟系统结算。
            var progression=manager.GetComponentData<HeroProgression>(controller.SessionEntity);
            float maxHealthBefore=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).MaxHealth;
            progression.Experience=progression.Library.Value.CumulativeExperience[6];
            manager.SetComponentData(controller.SessionEntity,progression);
            yield return null; yield return null;
            Check(controller.Progression.Level==7 && controller.Progression.SkillPoints==7,"Experience table levels hero to 7 and grants skill points");
            Check(manager.GetComponentData<AbilityUnit>(controller.SessionEntity).MaxHealth>maxHealthBefore,"Level growth raises authoritative max health");
            yield return WaitForAction("combat.learnSkill",3);
            for(int i=0;i<4;i++) ClickLearn(i);
            yield return null; yield return null;
            var learned=manager.GetBuffer<AbilitySlot>(controller.SessionEntity);
            Check(learned[0].Level==1 && learned[1].Level==1 && learned[2].Level==1 && learned[3].Level==1 && controller.Progression.SkillPoints==3,"HUD learn buttons learn every slot once");
            controller.LearnSkill(3); yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[3].Level==1 && controller.Progression.LastFailure==ProgressionFailure.HeroLevelTooLow,"Ultimate level 2 waits for hero level 12");
            Check(!controller.TryMoveFromScreen(new Vector2(Screen.width*0.5f,5)),"HUD blocks movement");
            Check(controller.EnemyKinds.Length>1,"Enemy roster provides several enemy kinds");
            // 攻击移动：走向远处并自动攻击途中索敌距离内的敌人（Dota 2 的 A + 左键地面）。
            controller.IssueAttackMove(new Vector3(controller.safeSpawnRadius+12,0,0));
            yield return null;
            Check(controller.Order.Kind==HeroOrderKind.AttackMove,"Attack-move order is recorded");
            yield return new WaitForSeconds(3);
            Check(controller.State.ShotsFired>0 && controller.State.Hits>0,"Auto attack module fires through the shared effect pipeline");
            yield return OrderChecks(controller,manager);
            var slots=manager.GetBuffer<AbilitySlot>(controller.SessionEntity);
            var q=slots[0]; q.HitStacks=originalHero.skills[0].requiredHits; slots[0]=q;
            Click(0);
            yield return null; yield return null;
            Check(manager.GetComponentData<AbilityUnit>(controller.SessionEntity).AttackSpeedMultiplier>1,"Self buff modifies attack speed");
            Click(1);
            Check(indicator.IsAiming && indicator.ActiveSlot==1,"HUD slot enters configured aim");
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.right*12),"Confirm cone ability");
            yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[1].ReadyAt>0,"Successful cast commits cooldown");
            Click(2);
            indicator.UpdateAim((Vector3)controller.State.Position,(Vector3)controller.State.Position+Vector3.forward*10,true,controller.arenaHalfSize);
            yield return Capture("point-indicator.png",()=>indicator.UpdateAim((Vector3)controller.State.Position,(Vector3)controller.State.Position+Vector3.forward*10,true,controller.arenaHalfSize));
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.forward*10),"Point ability accepts target");
            yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[2].Charges==1,"Point projectile consumes one charge");
            Click(3);
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.right*50),"Linear projectile accepts direction");
            yield return new WaitForSeconds(0.2f);
            Check(manager.GetComponentData<AbilityRuntime>(controller.SessionEntity).CastSerial>=4,"All configured examples commit through one executor");
            progression=manager.GetComponentData<HeroProgression>(controller.SessionEntity);
            progression.Experience=progression.Library.Value.CumulativeExperience[9];
            manager.SetComponentData(controller.SessionEntity,progression);
            yield return null; yield return null;
            Check(controller.Progression.Level==10 && HeroProgressionLogic.PendingTalent(controller.Progression)==0,"Level 10 unlocks first talent tier");
            float attackBefore=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack;
            yield return WaitForAction("combat.talent",0);
            ClickTalent(0); yield return null; yield return null;
            Check(HeroProgressionLogic.PendingTalent(controller.Progression)<0 && manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack>attackBefore,"Talent button applies left option");
            // 把下一波时间提前到当前，验证计时触发与逐波数值，而不是让探针等待完整波次间隔。
            var waveState=manager.GetComponentData<CombatWaveState>(controller.SessionEntity);
            int spawnedBefore=waveState.SpawnedTotal; waveState.NextWaveAt=0;
            manager.SetComponentData(controller.SessionEntity,waveState);
            yield return null; yield return null;
            Check(controller.Wave.Wave==2 && controller.Wave.SpawnedTotal>spawnedBefore,"Next difficulty wave spawns when its timer elapses");
            Check(DistinctEnemyKinds(manager)>1,"Waves mix several enemy kinds from the roster");
            // 换序的英雄证明输入槽不是技能身份；克隆仅供探针，不修改项目资源。
            var alternate=Instantiate(originalHero);
            alternate.stableId="probe.reordered"; alternate.skills=(SkillDefinition[])originalHero.skills.Clone();
            (alternate.skills[0],alternate.skills[2])=(alternate.skills[2],alternate.skills[0]);
            controller.SelectHero(alternate); yield return null;
            controller.LearnSkill(0); yield return null; yield return null;
            Click(0);
            Check(indicator.IsAiming && controller.SkillAt(0).aim==AimMode.Point,"Different hero loadout changes Q aim without simulation branches");
            indicator.Cancel(); controller.SelectHero(originalHero); Destroy(alternate); yield return null;
            Check(controller.State.Kills==0 && manager.GetBuffer<SkillFlight>(controller.SessionEntity).Length==0,"Restart clears combat and flights");
            Check(controller.Progression.Level==1 && manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[0].Level==0,"Restart resets level and learned skills");
            Check(controller.EquippedAt(0)==-1 && controller.Stats.Equipment.AttackDamage==0 &&
                  NearGold(controller.Equipment.Gold-controller.Equipment.GoldEarned,controller.equipmentCatalog?controller.equipmentCatalog.startingGold:0),
                "Restart clears equipment and restores starting gold");
            var unit=manager.GetComponentData<AbilityUnit>(controller.SessionEntity); unit.Health=0; manager.SetComponentData(controller.SessionEntity,unit);
            yield return null; yield return null;
            Check(controller.State.Health==0,"Death snapshot comes from authoritative unit");
            Check(controller.Wave.Outcome==CombatOutcome.Defeat,"Hero death records defeat outcome");
            controller.RestartRun(); yield return null;
            Check(controller.State.Health==originalHero.health,"Restart restores configured hero");
            yield return Capture("foundation-game.png");
            Finish(controller);
        }
        /// <summary>Dota 2 式指令：右键敌人攻击、S 停止、H 待命、HUD 指令按钮与鼠标点选敌人。</summary>
        private IEnumerator OrderChecks(GameCombatController controller,EntityManager manager)
        {
            Entity target=NearestEnemy(controller,manager);
            Check(target!=Entity.Null,"Enemies remain near the hero after attack-move");
            if(target==Entity.Null) yield break;
            var screen=controller.gameCamera.WorldToScreenPoint(manager.GetComponentData<Unity.Transforms.LocalTransform>(target).Position);
            Check(controller.PickEnemy(screen)!=Entity.Null,"Mouse ray picks the enemy under the cursor");
            float healthBefore=manager.GetComponentData<AbilityUnit>(target).Health;
            controller.IssueAttack(target); yield return null;
            Check((controller.Order.Kind==HeroOrderKind.AttackTarget && controller.Order.Target==target) || !manager.Exists(target),"Right-clicking an enemy issues an attack order");
            float until=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<until && manager.Exists(target) && manager.GetComponentData<AbilityUnit>(target).Health>=healthBefore) yield return null;
            Check(!manager.Exists(target) || manager.GetComponentData<AbilityUnit>(target).Health<healthBefore,"Attack order damages the chosen target");
            controller.Hold(); yield return null; yield return null;
            var held=controller.State.Position;
            yield return new WaitForSeconds(0.5f);
            Check(controller.Order.Kind==HeroOrderKind.Hold && math.distance(held.xz,controller.State.Position.xz)<0.01f,"Hold position keeps the hero in place");
            yield return WaitForAction("combat.order",1);
            ClickAction("combat.order",1); yield return null;
            Check(controller.Order.Kind==HeroOrderKind.Idle,"HUD stop button issues stop");
            ClickAction("combat.order",0); yield return null;
            Check(controller.AttackArmed,"HUD attack button arms attack targeting");
            ClickAction("combat.order",0); yield return null;
            Check(!controller.AttackArmed,"Second attack button press cancels attack targeting");
            yield return TargetInfoChecks(controller,manager);
            yield return TooltipChecks(controller);
            yield return MoveAttackChecks(controller,manager);
            yield return EquipmentChecks(controller,manager);
        }
        /// <summary>商店与装备：HUD 按钮打开商店，商店卡片购买，装备属性直接相加，打开商店后右键装备格出售并返还金币。</summary>
        private IEnumerator EquipmentChecks(GameCombatController controller,EntityManager manager)
        {
            var items=controller.ShopItems;
            int sword=-1;
            for(int i=0;i<items.Length && i<CombatHud.ShopCards;i++) if(items[i] && items[i].stableId=="item.iron_sword") { sword=i; break; }
            Check(items.Length>=CombatHud.ShopCards && sword>=0,"Shop lists the project equipment catalog on its first page");
            if(sword<0) yield break;
            // 直接给足金币，验证购买链路而不是等待击杀；期间击杀获得的金币用 GoldEarned 扣除。
            var purse=manager.GetComponentData<HeroEquipment>(controller.SessionEntity); purse.Gold=5000; manager.SetComponentData(controller.SessionEntity,purse);
            float earned=purse.GoldEarned;
            yield return WaitForAction(CombatHud.ShopCommand,0);
            ClickAction(CombatHud.ShopCommand,0);
            yield return WaitForAction(CombatHud.BuyCommand,sword);
            Check(controller.ShopOpen,"HUD shop button opens the shop");
            int level=controller.Progression.Level;
            float attackBefore=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack;
            ClickAction(CombatHud.BuyCommand,sword); yield return null; yield return null;
            var equipment=controller.Equipment;
            Check(controller.EquippedAt(0)==sword && NearGold(equipment.Gold,5000-items[sword].price+equipment.GoldEarned-earned),
                "Shop card buys the item into the first slot and spends gold");
            float attackAfter=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack;
            Check(controller.Stats.Equipment.AttackDamage==12 && Mathf.Approximately(attackAfter,controller.Stats.Total.AttackDamage) &&
                  (controller.Progression.Level!=level || Mathf.Approximately(attackAfter,attackBefore+12)),"Equipment attack adds directly to hero attack");
            controller.BuyItem(sword); yield return null; yield return null;
            Check(controller.EquippedAt(1)==sword && controller.Stats.Equipment.AttackDamage==24,"Non-unique items can be bought twice and stack");
            equipment=controller.Equipment;
            float gold=equipment.Gold; earned=equipment.GoldEarned;
            UIPointerAction sell=null;
            foreach(var action in UnityEngine.Object.FindObjectsByType<UIPointerAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(action.command==CombatHud.SellCommand && action.value==1) sell=action;
            Check(sell,"Equipment slot has a right-click sell source");
            if(sell) sell.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {button=UnityEngine.EventSystems.PointerEventData.InputButton.Right});
            yield return null; yield return null;
            equipment=controller.Equipment;
            Check(controller.EquippedAt(1)==-1 && NearGold(equipment.Gold,gold+controller.equipmentCatalog.SellPrice(items[sword])+equipment.GoldEarned-earned),
                "Right-clicking an equipped item in the open shop sells it for the refund price");
            Check(controller.Stats.Equipment.AttackDamage==12,"Selling removes the item's stats");
            yield return Capture("shop.png");
            controller.ToggleShop(); yield return null;
            Check(!controller.ShopOpen,"Shop closes again");
            controller.SellItem(0); yield return null; yield return null;
            Check(controller.EquippedAt(0)==sword,"Selling requires the shop to be open");
        }
        /// <summary>悬停技能 / 被动图标：显示 Dota 2 式详细提示（名称、逐级数值），位于图标上方；指针离开后隐藏。</summary>
        private IEnumerator TooltipChecks(GameCombatController controller)
        {
            var ui=UnityEngine.Object.FindFirstObjectByType<UIManager>();
            if(!ui) yield break;
            UIHoverAction skill=null,passive=null;
            foreach(var hover in UnityEngine.Object.FindObjectsByType<UIHoverAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                if(hover.command!=CombatHud.HoverCommand) continue;
                if(hover.value==1) skill=hover; else if(hover.value==CombatHud.PassiveSlot) passive=hover;
            }
            Check(skill && passive,"Bottom HUD has hover sources for skills and the passive");
            if(!skill || !passive) yield break;
            skill.OnPointerEnter(null);
            float until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until && !ui.IsVisible(CombatHud.TooltipPanel)) yield return null;
            yield return null;
            Check(ui.IsVisible(CombatHud.TooltipPanel),"Hovering a skill icon shows the ability tooltip");
            if(ui.IsVisible(CombatHud.TooltipPanel))
            {
                var definition=controller.SkillAt(1);
                string expected=definition?(string.IsNullOrWhiteSpace(definition.displayName)?definition.name:definition.displayName):null;
                Check(expected!=null && PanelText(ui,CombatHud.TooltipPanel,CombatHud.TooltipKeys.Name)==expected,"Skill tooltip shows the hovered skill name");
                Check(!string.IsNullOrEmpty(PanelText(ui,CombatHud.TooltipPanel,CombatHud.TooltipKeys.Values)) &&
                      !string.IsNullOrEmpty(PanelText(ui,CombatHud.TooltipPanel,CombatHud.TooltipKeys.Cooldown)),"Skill tooltip lists per-level values and cooldown");
                var tip=(RectTransform)ui.FindPanel(CombatHud.TooltipPanel).transform; var source=(RectTransform)skill.transform;
                var a=new Vector3[4]; var b=new Vector3[4]; tip.GetWorldCorners(a); source.GetWorldCorners(b);
                Check(a[0].y>=b[1].y-1 && a[0].x<=b[2].x && a[2].x>=b[1].x,"Tooltip sits above the hovered icon");
                yield return Capture("ability-tooltip.png");
            }
            skill.OnPointerExit(null); yield return null;
            Check(!ui.IsVisible(CombatHud.TooltipPanel),"Leaving the icon hides the tooltip");
            passive.OnPointerEnter(null);
            until=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<until && !ui.IsVisible(CombatHud.TooltipPanel)) yield return null;
            yield return null;
            string kind=PanelText(ui,CombatHud.TooltipPanel,CombatHud.TooltipKeys.Kind);
            Check(ui.IsVisible(CombatHud.TooltipPanel) && kind!=null && kind.Contains("被动"),"Hovering the passive shows the auto attack tooltip");
            passive.OnPointerExit(null); yield return null;
            Check(!ui.IsVisible(CombatHud.TooltipPanel),"Leaving the passive hides the tooltip");
        }
        /// <summary>移动中攻击：攻击距离内有敌人时右键移动，英雄一边走向目的地一边出手（不停下、不追击）。</summary>
        private IEnumerator MoveAttackChecks(GameCombatController controller,EntityManager manager)
        {
            Check(controller.hero && controller.hero.attackWhileMoving,"Smoke hero enables attack while moving");
            if(!controller.hero || !controller.hero.attackWhileMoving) yield break;
            Entity template=NearestEnemy(controller,manager,true);
            Check(template!=Entity.Null,"Move-attack check has a live enemy template");
            if(template==Entity.Null) yield break;
            // 普攻会在敌人进入近距离前将其击杀，等待随机目标会让用例静默跳过。
            // 仅在显式冒烟模式复制一个静止耐久目标，完成后销毁；不修改配置或正常游戏规则。
            Entity target=manager.Instantiate(template);
            try
            {
                float3 start=controller.State.Position;
                float3 forward=math.normalizesafe(new float3(controller.State.Facing.x,0,controller.State.Facing.z),new float3(0,0,1));
                var transform=manager.GetComponentData<Unity.Transforms.LocalTransform>(target);
                transform.Position=start+forward*math.min(8,controller.attackRange*0.5f);
                manager.SetComponentData(target,transform);
                var unit=manager.GetComponentData<AbilityUnit>(target);
                unit.Health=unit.MaxHealth=10000; unit.BaseSpeed=unit.Speed=0; unit.BaseAttack=unit.Attack=0;
                unit.Controls=ControlFlags.None;
                manager.GetBuffer<ActiveBuff>(target).Clear();
                manager.SetComponentData(target,unit);
                float3 destination=start+new float3(-forward.z,0,forward.x)*6;
                float limit=math.max(1,controller.arenaHalfSize-3);
                destination.x=math.clamp(destination.x,-limit,limit); destination.z=math.clamp(destination.z,-limit,limit);
                int shots=controller.State.ShotsFired;
                controller.SetDestination((Vector3)destination); yield return null;
                Check(controller.Order.Kind==HeroOrderKind.Move || controller.Order.Kind==HeroOrderKind.Idle,"Right-click ground issues a move order");
                bool firedWhileMoving=false; float until=Time.realtimeSinceStartup+1.5f;
                while(Time.realtimeSinceStartup<until && !firedWhileMoving)
                {
                    var state=controller.State;
                    if(state.ShotsFired>shots && math.distance(state.Position.xz,start.xz)>0.1f && math.distance(state.Position.xz,destination.xz)>0.3f) firedWhileMoving=true;
                    shots=math.max(shots,state.ShotsFired);
                    yield return null;
                }
                Check(firedWhileMoving,"Hero attacks enemies in range while walking to the destination");
            }
            finally { if(manager.Exists(target)) manager.DestroyEntity(target); }
            controller.Hold(); yield return null;
        }
        /// <summary>左键选中敌人：左上角目标信息面板显示种类名称，× 按钮取消选中并隐藏面板。</summary>
        private IEnumerator TargetInfoChecks(GameCombatController controller,EntityManager manager)
        {
            var ui=UnityEngine.Object.FindFirstObjectByType<UIManager>();
            Check(ui,"Scene has a UI manager for the target panel");
            if(!ui) yield break;
            // 选中的敌人可能在面板异步加载期间被英雄击杀；此时换一个敌人重试。
            Entity target=Entity.Null; bool shown=false;
            for(int attempt=0;attempt<4 && !shown;attempt++)
            {
                target=NearestEnemy(controller,manager,true);
                if(target==Entity.Null || !controller.SelectEnemy(target)) { yield return null; continue; }
                float until=Time.realtimeSinceStartup+5;
                while(Time.realtimeSinceStartup<until && controller.Selected==target && !ui.IsVisible(CombatHud.TargetPanel)) yield return null;
                yield return null;
                shown=controller.Selected==target && ui.IsVisible(CombatHud.TargetPanel);
            }
            Check(shown,"Left-click selection shows the target info panel");
            if(!shown) yield break;
            int kind=manager.GetComponentData<CombatEnemy>(target).Kind;
            string expected=kind>=0 && kind<controller.EnemyKinds.Length?controller.EnemyKinds[kind]:null;
            Check(expected!=null && PanelText(ui,CombatHud.TargetPanel,CombatHud.TargetKeys.Name)==expected,"Target panel shows the selected enemy kind name");
            Check(!string.IsNullOrEmpty(PanelText(ui,CombatHud.TargetPanel,CombatHud.TargetKeys.HealthText)),"Target panel shows target health");
            yield return Capture("target-info.png");
            ClickAction(CombatHud.TargetKeys.CloseCommand,0); yield return null;
            Check(controller.Selected==Entity.Null,"Target panel close button clears the selection");
            yield return null; yield return null;
            Check(!ui.IsVisible(CombatHud.TargetPanel),"Clearing the selection hides the target panel");
        }
        private static string PanelText(UIManager ui,string panelId,string key)
        {
            var panel=ui.FindPanel(panelId);
            if(!panel || panel.texts==null) return null;
            foreach(var binding in panel.texts) if(binding.key==key && binding.target) return binding.target.text;
            return null;
        }
        private static Entity NearestEnemy(GameCombatController controller,EntityManager manager,bool aliveOnly=false)
        {
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(Unity.Transforms.LocalTransform),typeof(AbilityUnit)))
            using(var entities=query.ToEntityArray(Allocator.Temp))
            using(var transforms=query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp))
            using(var units=query.ToComponentDataArray<AbilityUnit>(Allocator.Temp))
            {
                Entity best=Entity.Null; float bestDistance=float.MaxValue; float2 hero=controller.State.Position.xz;
                for(int i=0;i<entities.Length;i++)
                {
                    if(aliveOnly && units[i].Health<=0) continue;
                    float distance=math.distancesq(transforms[i].Position.xz,hero);
                    if(distance<bestDistance) { bestDistance=distance; best=entities[i]; }
                }
                return best;
            }
        }
        private static int DistinctEnemyKinds(EntityManager manager)
        {
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy)))
            using(var enemies=query.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
            {
                var ids=new System.Collections.Generic.HashSet<int>();
                foreach(var enemy in enemies) ids.Add(enemy.ConfigId);
                return ids.Count;
            }
        }
        /// <summary>面板由 UIManager 异步加载，并在 HUD 下一次刷新时显示；按钮可见前等待，最多 5 秒。</summary>
        private IEnumerator WaitForAction(string command,int value)
        {
            float until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until)
            {
                foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if(action.command==command && action.value==value) yield break;
                yield return null;
            }
        }
        private void ClickLearn(int slot)=>ClickAction("combat.learnSkill",slot);
        private void ClickTalent(int option)=>ClickAction("combat.talent",option);
        private void ClickAction(string command,int value)
        {
            foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(action.command==command && action.value==value)
                { action.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return; }
            Check(false,"Missing visible authored button "+command+" "+value);
        }
        private void Click(int slot)
        {
            foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(action.command=="combat.ability" && action.value==slot)
                { action.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return; }
            Check(false,"Missing authored skill button "+slot);
        }
        /// <summary>金币比较：击杀金币是小数，金币与累计金币分别累加会有浮点舍入差，按 0.05 容差比较。</summary>
        private static bool NearGold(float actual,float expected)=>Mathf.Abs(actual-expected)<0.05f;
        private void Check(bool condition,string message)
        { if(!condition) { failed=true; Debug.LogError("GAME_CHECK_FAILED: "+message); } else Debug.Log("GAME_CHECK_PASSED: "+message); }
        private IEnumerator Capture(string name, Action beforeRender = null)
        {
            yield return null;
            beforeRender?.Invoke();
            var demo = GetComponent<GameCombatController>();
            var camera = demo.gameCamera;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                // Hidden/minimized windows can have a black backbuffer. Request an actual URP
                // render including the same HUD into a texture, independent of window visibility.
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                Color first = texture.GetPixel(0, 0);
                bool varied = false;
                for (int y = 30; y < 1080; y += 97)
                    for (int x = 20; x < 1920; x += 113)
                        if (((Vector4)(texture.GetPixel(x, y) - first)).sqrMagnitude > 0.01f) varied = true;
                Check(varied, "Offscreen render contains scene/UI pixels: " + name);
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlaneDistance;
                target.Release();
                Destroy(target);
                Destroy(texture);
            }
        }

        private void Finish(GameCombatController controller)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Report{passed=!failed,enemies=controller.IsReady?controller.State.AliveEnemies:0,
                wave=controller.IsReady?controller.Wave.Wave:0,level=controller.IsReady?controller.Progression.Level:0},true));
            Debug.Log(failed?"GAME_SMOKE_FAILED":"GAME_SMOKE_PASSED"); Application.Quit(failed?1:0);
        }
        [Serializable] private class Report { public bool passed; public int enemies,wave,level; }
    }
}


