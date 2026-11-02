using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Flow;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Combat
{
    /// <summary>
    /// 远征阶段。Map 为路线图选择；Battle 为战斗 / 精英 / 首领遭遇；Shop、Event、Rest 为对应节点界面；
    /// Reward 为精英或首领战后的遗物三选一。
    /// </summary>
    public enum ExpeditionPhase { Battle,VictoryNotice,Map,Shop,Reward,Event,Rest,Victory,Defeat }
    public sealed partial class GameCombatController
    {
        public ExpeditionCatalog expedition;
        public ExpeditionPhase Phase {get;private set;}
        /// <summary>当前商店或战利品；其他阶段为空。</summary>
        public CampState Camp {get;private set;}
        /// <summary>关闭后按旧的整关规则开局（定时首领），供导演与压力探针使用；正常游戏始终使用路线图。</summary>
        public bool RouteMapEnabled {get;set;}=true;
        private float victoryUntil;
        private bool returnConfirmation;
        private ExpeditionEvent currentEvent;
        private string eventResult;
        private bool eventDone;
        private RouteMapView routeView;
        public RunState Run=>RunState.Current;
        public RouteMap Route=>Run!=null?Run.Route:null;
        /// <summary>当前事件（营火时为空）；选择结果写在 <see cref="EventResult"/>。</summary>
        public ExpeditionEvent CurrentEvent=>currentEvent;
        public string EventResult=>eventResult;
        public bool EventDone=>eventDone;
        private RouteMapSettings RouteSettings=>expedition && expedition.route!=null?expedition.route:new RouteMapSettings();
        private void PrepareExpedition()
        {
            if(!expedition) return;
            if(Run==null || Run.Hero!=hero)
            {
                var second=Array.Find(expedition.selection.maps,value=>value && value!=map);
                RunState.Begin(hero,map,second);
            }
            Run.EnsureRoute(RouteSettings);
            inputProfile?.Validate();
            routeView=hud.GetComponentInChildren<RouteMapView>(true);
            ExpeditionUI.Events.Command+=ExpeditionCommand;
        }
        private void CaptureRun()
        {
            if(Run==null || !IsReady) return;
            var manager=session.World.EntityManager;var progression=Progression;var equipment=Equipment;
            Run.Level=progression.Level;Run.Experience=progression.Experience;Run.SkillPoints=progression.SkillPoints;
            Run.Gold=equipment.Gold;Run.GoldEarned=equipment.GoldEarned;Run.Kills=State.Kills;Run.TotalBattleTime=State.Elapsed;
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            for(int i=0;i<4;i++) {Run.SkillLevels[i]=slots[i].Level;Run.Forms[i]=slots[i].FormChoice;}
            for(int i=0;i<6;i++) {int item=EquippedAt(i);Run.EquipmentIds[i]=item>=0?session.Items[item].configId:-1;}
        }
        private void RestoreRun()
        {
            Phase=ExpeditionPhase.Battle;Camp=null;Paused=false;returnConfirmation=false;currentEvent=null;eventDone=false;eventResult=null;
            ClearSelection();
            if(Run==null || Run.Hero!=hero || !expedition) return;
            var manager=session.World.EntityManager;
            var progression=Progression;progression.Level=Run.Level;progression.Experience=Run.Experience;progression.SkillPoints=Run.SkillPoints;
            manager.SetComponentData(session.Entity,progression);
            var equipment=Equipment;equipment.Gold=Run.Gold;equipment.GoldEarned=Run.GoldEarned;
            manager.SetComponentData(session.Entity,equipment);
            var equipped=manager.GetBuffer<EquipmentSlot>(session.Entity);
            for(int i=0;i<6;i++) equipped[i]=new EquipmentSlot{Item=Run.EquipmentIds[i]<0?-1:equipmentCatalog.IndexOf(Run.EquipmentIds[i])};
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            for(int i=0;i<4;i++)
            {
                var slot=slots[i];slot.Level=Run.SkillLevels[i];slot.FormChoice=Run.Forms[i];slots[i]=slot;
                if(slot.FormChoice>0 && !session.TryApplyForm(i,hero.skills[i].forms[slot.FormChoice-1],out var error)) throw new InvalidOperationException(error);
            }
            ApplyRelics();
            // 进入新的一幕（换场景）时回复全部生命与法力；同一幕内生命由战斗会话保留。
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);unit.Health=unit.MaxHealth;unit.Mana=unit.MaxMana;
            manager.SetComponentData(session.Entity,unit);
            var player=State;player.Health=unit.Health;player.MaximumHealth=unit.MaxHealth;player.Kills=Run.Kills;player.Elapsed=Run.TotalBattleTime;
            manager.SetComponentData(session.Entity,player);
            var settings=manager.GetComponentData<CombatSettings>(session.Entity);
            settings.Lava=map && map.lavaHazards;
            settings.PassiveGoldPerSecond=expedition.passiveGoldPerSecond;
            settings.DamageMultiplier=map?map.enemyDamageMultiplier:1;
            settings.HealthMultiplier=map?map.enemyHealthMultiplier:1;
            settings.GoldMultiplier=1;settings.EliteCount=1;
            manager.SetComponentData(session.Entity,settings);
            var director=manager.GetComponentData<SpawnDirectorSettings>(session.Entity);
            if(!RouteMapEnabled)
            {
                manager.SetComponentData(session.Entity,CombatDirectorState.Begin(director,Run.Act,Run.Seed));
                return;
            }
            Run.EnsureRoute(RouteSettings);
            OpenMap();
        }
        private void ApplyRelics()
        {
            if(Run==null) return;
            var traits=AbilityTraits.Identity;
            foreach(var relic in Run.Relics)
            {
                relic.Validate();traits.ExtraBasicTargets+=relic.extraBasicTargets;
                traits.FreeCooldownChance=1-(1-traits.FreeCooldownChance)*(1-relic.echoChance);
                traits.KillHealRatio+=relic.killHealMaxHealth;traits.ControlDurationMultiplier*=relic.controlDurationMultiplier;
                traits.BodyScale*=relic.bodyScale;traits.MaxHealthMultiplier*=relic.maxHealthMultiplier;traits.BasicRangeMultiplier*=relic.basicRangeMultiplier;
                traits.ExtraCooldownReduction+=relic.cooldownReduction/100;traits.CooldownCap=math.max(traits.CooldownCap,relic.cooldownCap/100);
                traits.EliteGoldMultiplier*=relic.eliteGoldMultiplier;
                if(relic.skillHitBuff) {traits.HasSkillHitEffect=true;traits.SkillHitEffect=skillCatalog.CompileExternalEffect(relic.skillHitBuff,relic.displayName);}
            }
            session.World.EntityManager.SetComponentData(session.Entity,traits);
            session.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(session.Entity);
            SyncPlayerHealth();
            if(playerView) playerView.localScale=Vector3.one*traits.BodyScale;
        }
        // ---------- 阶段推进 ----------
        private void StepExpedition()
        {
            if(!expedition || Run==null || !IsReady) return;
            var outcome=Director.Outcome;
            if(Phase==ExpeditionPhase.Battle && outcome!=CombatOutcome.Running && outcome!=CombatOutcome.Idle)
            {
                CaptureRun();Paused=false;ClearSelection();
                if(outcome==CombatOutcome.Defeat) Phase=ExpeditionPhase.Defeat;
                else {Phase=ExpeditionPhase.VictoryNotice;victoryUntil=Time.unscaledTime+expedition.victoryNoticeSeconds;}
            }
            if(Phase==ExpeditionPhase.VictoryNotice && Time.unscaledTime>=victoryUntil) FinishBattle();
        }
        /// <summary>战斗胜利后的结算：普通战斗给金币回到路线图，精英与首领进入遗物三选一。</summary>
        private void FinishBattle()
        {
            var node=Route!=null?Route.CurrentNode:null;
            if(!RouteMapEnabled || node==null) {Phase=ExpeditionPhase.Victory;return;}
            Route.Cleared=true;
            var settings=RouteSettings;
            if(node.Kind==RouteNodeKind.Combat)
            {
                GiveGold(settings.combatGold);ShowNotice($"战斗胜利 · 额外获得 {settings.combatGold:0} 金币",3);CaptureRun();OpenMap();return;
            }
            if(node.Kind==RouteNodeKind.Elite) {GiveGold(settings.eliteGold);ShowNotice($"精英战胜利 · 额外获得 {settings.eliteGold:0} 金币",3);}
            else AdjustHealth(1);
            CaptureRun();
            Camp=new CampState(Run,expedition,session.Items,CampMode.Reward);
            if(Camp.RelicChosen) AfterReward();
            else Phase=ExpeditionPhase.Reward;
        }
        private void AfterReward()
        {
            Camp=null;
            var node=Route!=null?Route.CurrentNode:null;
            if(node!=null && node.Kind==RouteNodeKind.Boss)
            {
                if(Run.IsFinalAct) {Phase=ExpeditionPhase.Victory;return;}
                DepartAct();return;
            }
            OpenMap();
        }
        private void OpenMap()
        {
            Phase=ExpeditionPhase.Map;Camp=null;currentEvent=null;eventDone=false;eventResult=null;Paused=false;ClearSelection();
            if(IsReady)
            {
                var manager=session.World.EntityManager;
                manager.SetComponentData(session.Entity,CombatDirectorState.Idle(Run!=null?Run.Act:1,Run!=null?Run.Seed:1));
            }
            PublishRoute();
        }
        /// <summary>进入路线图上一个可达节点（界面槽位值：层 × 4 + 列，首领为 99）。</summary>
        public bool EnterNode(int slot)
        {
            var route=Route;
            if(Phase!=ExpeditionPhase.Map || route==null) return false;
            int id=route.FromSlot(slot);
            if(!route.CanEnter(id)) return false;
            var node=route.Nodes[id];
            route.Current=id;route.Cleared=false;node.Visited=true;
            switch(node.Kind)
            {
                case RouteNodeKind.Combat: case RouteNodeKind.Elite: case RouteNodeKind.Boss:
                    StartEncounter(node);break;
                case RouteNodeKind.Shop:
                    Camp=new CampState(Run,expedition,session.Items,CampMode.Shop);Phase=ExpeditionPhase.Shop;break;
                case RouteNodeKind.Event:
                    StartEvent();break;
                default:
                    currentEvent=null;eventDone=false;eventResult=null;Phase=ExpeditionPhase.Rest;break;
            }
            PublishRoute();nextHudUpdate=0;return true;
        }
        /// <summary>界面中可达节点的槽位值，供探针与自动操作使用。</summary>
        public List<int> ReachableSlots()
        {
            var result=new List<int>(4);var route=Route;
            if(route==null || Phase!=ExpeditionPhase.Map) return result;
            foreach(var node in route.Nodes) if(route.CanEnter(node.Id)) result.Add(RouteMap.Slot(node));
            return result;
        }
        private void StartEncounter(RouteNode node)
        {
            var manager=session.World.EntityManager;var settings=RouteSettings;
            var director=manager.GetComponentData<SpawnDirectorSettings>(session.Entity);
            float minute=settings.Minute(Run.Act,node.Floor);
            var kind=node.Kind==RouteNodeKind.Elite?EncounterKind.Elite:node.Kind==RouteNodeKind.Boss?EncounterKind.Boss:EncounterKind.Kills;
            int goal=kind==EncounterKind.Kills?settings.KillGoal(minute):0;
            manager.SetComponentData(session.Entity,CombatDirectorState.BeginEncounter(director,Run.Act,kind,minute,goal,settings.Elites(Run.Act),Run.Seed++));
            var combat=manager.GetComponentData<CombatSettings>(session.Entity);
            combat.HealthMultiplier=(map?map.enemyHealthMultiplier:1)*(1+Run.NextFightHealthBonus);Run.NextFightHealthBonus=0;
            manager.SetComponentData(session.Entity,combat);
            // 每场战斗开始补满法力；生命在同一幕内延续。
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);unit.Mana=unit.MaxMana;manager.SetComponentData(session.Entity,unit);
            Phase=ExpeditionPhase.Battle;Paused=false;returnConfirmation=false;
            string title=node.Kind==RouteNodeKind.Boss?"首领战":node.Kind==RouteNodeKind.Elite?$"精英战 · 击败 {settings.Elites(Run.Act)} 只精英":$"战斗 · 击杀 {goal} 只敌人";
            ShowNotice(title,3);
        }
        private void LeaveNode()
        {
            if(Route!=null) Route.Cleared=true;
            CaptureRun();OpenMap();
        }
        private void DepartAct()
        {
            CaptureRun();Run.Advance();SceneManager.LoadScene(ResourcePaths.Resolve(Run.Map.sceneId,"scene"));
        }
        private void ExpeditionCommand(UiCommand command)
        {
            switch(command.Id)
            {
                case "expedition.stats":ToggleStats();break;
                case "expedition.pause":SetPaused(true);break;
                case "expedition.resume":SetPaused(false);returnConfirmation=false;break;
                case "expedition.return":ReturnToSelection();break;
                case "expedition.form":ChooseForm(command.Value);break;
                case "map.node":EnterNode(command.Value);break;
                case "camp.relic":ChooseRelic(command.Value);break;
                case "camp.buy":CampBuy(command.Value);break;
                case "camp.sell":CampSell(command.Value);break;
                case "camp.refresh":CampRefresh();break;
                case "camp.depart":Depart();break;
                case "event.choice":ChooseEventOption(command.Value);break;
                case "event.continue":ContinueEvent();break;
                case "target.close":ClearSelection();break;
            }
            nextHudUpdate=0;
        }
        // ---------- 战利品与商店 ----------
        public bool ChooseRelic(int option)
        {
            if(Phase!=ExpeditionPhase.Reward || Camp==null || Camp.RelicChosen || option<0 || option>=Camp.RelicOptions.Count) return false;
            Run.Relics.Add(Camp.RelicOptions[option]);Camp.RelicChosen=true;ApplyRelics();CaptureRun();
            ShowNotice("获得遗物："+Camp.RelicOptions[option].displayName,3);AfterReward();return true;
        }
        public bool CampBuy(int slot)
        {
            if(Phase!=ExpeditionPhase.Shop || Camp==null || slot<0 || slot>=Camp.Stock.Length || Camp.Stock[slot]<0) return false;
            var manager=session.World.EntityManager;var purse=Equipment;
            bool bought=EquipmentLogic.TryBuy(ref purse,manager.GetBuffer<EquipmentSlot>(session.Entity),Camp.Stock[slot]);
            manager.SetComponentData(session.Entity,purse);
            if(bought) {Camp.Stock[slot]=-1;RefreshHeroStats();CaptureRun();}
            else ShowNotice(EquipmentLogic.Describe(purse.LastFailure),3);
            return bought;
        }
        public bool CampSell(int slot)
        {
            if(Phase!=ExpeditionPhase.Shop) return false;
            var manager=session.World.EntityManager;var purse=Equipment;
            bool sold=EquipmentLogic.TrySell(ref purse,manager.GetBuffer<EquipmentSlot>(session.Entity),slot);
            manager.SetComponentData(session.Entity,purse);
            if(sold) {RefreshHeroStats();CaptureRun();}
            return sold;
        }
        public bool CampRefresh()
        {
            if(Phase!=ExpeditionPhase.Shop || Camp==null) return false;
            var purse=Equipment;if(purse.Gold<Camp.RefreshPrice) {ShowNotice("金币不足，无法刷新",2);return false;}
            purse.Gold-=Camp.RefreshPrice;Camp.Refreshes++;
            session.World.EntityManager.SetComponentData(session.Entity,purse);CaptureRun();Camp.Roll(Run,session.Items);return true;
        }
        /// <summary>离开商店回到路线图。</summary>
        public bool Depart()
        {
            if(Phase!=ExpeditionPhase.Shop) return false;
            LeaveNode();return true;
        }
        // ---------- 事件与营火 ----------
        private void StartEvent()
        {
            var events=expedition.events;
            currentEvent=null;eventDone=false;eventResult=null;
            if(events!=null && events.Length>0)
            {
                var pool=new List<ExpeditionEvent>();
                foreach(var item in events) if(item!=null && item.choices!=null && item.choices.Length>0 && !Run.SeenEvents.Contains(item.id)) pool.Add(item);
                if(pool.Count==0)
                {
                    Run.SeenEvents.Clear();
                    foreach(var item in events) if(item!=null && item.choices!=null && item.choices.Length>0) pool.Add(item);
                }
                if(pool.Count>0)
                {
                    var random=Unity.Mathematics.Random.CreateFromIndex(Run.Seed++);
                    currentEvent=pool[random.NextInt(pool.Count)];Run.SeenEvents.Add(currentEvent.id);
                }
            }
            // 没有可用事件时按营火处理，避免节点卡死。
            Phase=currentEvent!=null?ExpeditionPhase.Event:ExpeditionPhase.Rest;
        }
        /// <summary>营火的两个选项文字（休息 / 训练），与实际结算使用同一组数值。</summary>
        private string RestChoice(int index)
        {
            var settings=RouteSettings;
            if(index==0)
            {
                var unit=session.World.EntityManager.GetComponentData<AbilityUnit>(session.Entity);
                return $"休息：回复 {settings.restHeal*100:0}% 最大生命（+{unit.MaxHealth*settings.restHeal:0}）";
            }
            float experience=RestExperience();
            return experience>0?$"训练：获得 {experience:0} 经验":"训练：已满级，无法获得经验";
        }
        private float RestExperience()
        {
            float2 progress=HeroProgressionLogic.LevelProgress(Progression);
            return progress.y>0?progress.y*RouteSettings.restTraining:0;
        }
        public bool ChooseEventOption(int index)
        {
            if(eventDone || index<0) return false;
            if(Phase==ExpeditionPhase.Rest)
            {
                if(index>1) return false;
                if(index==0) {AdjustHealth(RouteSettings.restHeal);eventResult="你在篝火旁好好休息了一晚，伤势好转。";}
                else
                {
                    float experience=RestExperience();
                    if(experience<=0) {ShowNotice("已满级，无法训练",2);return false;}
                    GainExperience(experience);eventResult=$"你借着火光反复练习，获得 {experience:0} 经验。";
                }
                eventDone=true;CaptureRun();return true;
            }
            if(Phase!=ExpeditionPhase.Event || currentEvent==null || index>=currentEvent.choices.Length) return false;
            var choice=currentEvent.choices[index];
            var manager=session.World.EntityManager;var purse=Equipment;
            if(choice.goldCost>0)
            {
                if(purse.Gold+0.0001f<choice.goldCost) {ShowNotice("金币不足",2);return false;}
                purse.Gold-=choice.goldCost;manager.SetComponentData(session.Entity,purse);
            }
            var random=Unity.Mathematics.Random.CreateFromIndex(Run.Seed++);
            bool success=choice.chance>=1 || random.NextFloat()<choice.chance;
            var outcome=success?choice.success:choice.failure;
            ApplyOutcome(outcome);
            eventResult=outcome!=null && !string.IsNullOrWhiteSpace(outcome.text)?outcome.text:"什么也没有发生。";
            eventDone=true;CaptureRun();return true;
        }
        public bool ContinueEvent()
        {
            if((Phase!=ExpeditionPhase.Event && Phase!=ExpeditionPhase.Rest) || !eventDone) return false;
            LeaveNode();return true;
        }
        private void ApplyOutcome(EventOutcome outcome)
        {
            if(outcome==null) return;
            if(outcome.gold>0) GiveGold(outcome.gold);
            else if(outcome.gold<0)
            {
                var purse=Equipment;purse.Gold=Mathf.Max(0,purse.Gold+outcome.gold);session.World.EntityManager.SetComponentData(session.Entity,purse);
            }
            if(Mathf.Abs(outcome.health)>0.0001f) AdjustHealth(outcome.health);
            if(outcome.experience>0) GainExperience(outcome.experience);
            if(outcome.relic) GrantRandomRelic();
            if(outcome.item) GrantRandomItem(outcome.itemQuality);
            if(outcome.nextFightHealth>0) Run.NextFightHealthBonus+=outcome.nextFightHealth;
        }
        // ---------- 共享结算辅助 ----------
        private void GiveGold(float amount)
        {
            if(!(amount>0)) return;
            var purse=Equipment;EquipmentLogic.GainGold(ref purse,amount);session.World.EntityManager.SetComponentData(session.Entity,purse);
        }
        /// <summary>按最大生命比例回复（正）或损失（负）；损失最多降到 1 点，不会致死。</summary>
        private void AdjustHealth(float fraction)
        {
            var manager=session.World.EntityManager;
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);
            unit.Health=Mathf.Clamp(unit.Health+unit.MaxHealth*fraction,1,unit.MaxHealth);
            manager.SetComponentData(session.Entity,unit);SyncPlayerHealth();
        }
        private void GainExperience(float amount)
        {
            var manager=session.World.EntityManager;var progression=Progression;
            HeroProgressionLogic.GainExperience(ref progression,amount);
            int gained=HeroProgressionLogic.ApplyPendingLevels(ref progression);
            manager.SetComponentData(session.Entity,progression);
            if(gained>0) RefreshHeroStats();
        }
        private void RefreshHeroStats()
        {
            session.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(session.Entity);SyncPlayerHealth();
        }
        /// <summary>界面期间模拟冻结，手动把生命同步到展示用的 CombatPlayer。</summary>
        private void SyncPlayerHealth()
        {
            if(!IsReady) return;
            var manager=session.World.EntityManager;var unit=manager.GetComponentData<AbilityUnit>(session.Entity);
            var player=State;player.Health=unit.Health;player.MaximumHealth=unit.MaxHealth;manager.SetComponentData(session.Entity,player);
        }
        private void GrantRandomRelic()
        {
            var pool=new List<RelicDefinition>();
            foreach(var relic in expedition.relics) if(relic && !Run.Relics.Contains(relic)) pool.Add(relic);
            if(pool.Count==0) {GiveGold(150);ShowNotice("遗物已全部获得，改为获得 150 金币",3);return;}
            var random=Unity.Mathematics.Random.CreateFromIndex(Run.Seed++);
            var chosen=pool[random.NextInt(pool.Count)];Run.Relics.Add(chosen);ApplyRelics();ShowNotice("获得遗物："+chosen.displayName,3);
        }
        private void GrantRandomItem(EquipmentQuality quality)
        {
            var items=session.Items;var manager=session.World.EntityManager;var slots=manager.GetBuffer<EquipmentSlot>(session.Entity);
            var pool=new List<int>();
            for(int i=0;i<items.Length;i++)
                if(items[i] && items[i].quality==quality && !(items[i].unique && EquipmentLogic.Owns(slots,i))) pool.Add(i);
            if(pool.Count==0) return;
            var random=Unity.Mathematics.Random.CreateFromIndex(Run.Seed++);
            int item=pool[random.NextInt(pool.Count)];
            var purse=Equipment;float price=items[item].price;
            if(EquipmentLogic.FreeSlot(slots)<0)
            {
                EquipmentLogic.GainGold(ref purse,price);manager.SetComponentData(session.Entity,purse);
                ShowNotice($"装备栏已满，{items[item].Label} 折算为 {price:0} 金币",3);return;
            }
            // 先补足售价再走共享购买规则，确保唯一性与格位逻辑一致。
            purse.Gold+=price;
            bool bought=EquipmentLogic.TryBuy(ref purse,slots,item);
            if(!bought) purse.Gold-=price;
            manager.SetComponentData(session.Entity,purse);
            if(bought) {RefreshHeroStats();ShowNotice("获得装备："+items[item].Label,3);}
        }
        public void ReturnToSelection()
        {
            if(Phase!=ExpeditionPhase.Victory && Phase!=ExpeditionPhase.Defeat && !returnConfirmation)
            {returnConfirmation=true;ExpeditionUI.Visible("expedition.return.confirm",true);return;}
            RunState.Clear();GameSelection.Clear();SceneManager.LoadScene(ResourcePaths.Resolve("Scene.SelectScene","scene"));
        }
        private float EffectiveStat(StatKind kind)
        {
            var unit=session.World.EntityManager.GetComponentData<AbilityUnit>(session.Entity);
            switch(kind)
            {
                case StatKind.MaxHealth:return unit.MaxHealth;case StatKind.MaxMana:return unit.MaxMana;
                case StatKind.AttackDamage:return unit.Attack;case StatKind.AttackSpeed:return (unit.AttackSpeedMultiplier-1)*100;
                case StatKind.MoveSpeed:return unit.Speed;case StatKind.HealthRegen:return unit.Regen;
                case StatKind.ManaRegen:return unit.ManaRegenBonus+AbilityEffects.BaseManaRegen;
                case StatKind.CooldownReduction:return unit.CooldownReduction*100;
                case StatKind.DamageReduction:return math.min(.7f,unit.DamageReduction+unit.BuffDamageReduction)*100;
                case StatKind.SkillAmplify:return unit.SkillAmplify*100;default:return Stats.Total[kind];
            }
        }
        // ---------- 界面发布 ----------
        /// <summary>当前节点的标题，供顶栏显示，例如“第 1 幕 · 第 3 层 · 精英”。</summary>
        public string StageTitle
        {
            get
            {
                var node=Route!=null?Route.CurrentNode:null;
                if(Run==null || node==null || !RouteMapEnabled) return $"第 {Director.Stage} 关 · 持续战斗";
                return $"第 {Run.Act} 幕 · 第 {node.Floor+1} 层 · {RouteMap.Label(node.Kind)}";
            }
        }
        /// <summary>顶栏的遭遇进度：击杀数、精英数或首领状态。</summary>
        private string EncounterStatus(in CombatDirectorState wave,in SpawnDirectorSettings settings)
        {
            if(wave.Encounter==EncounterKind.Kills) return $"击杀 {Mathf.Min(wave.EncounterKills,wave.KillGoal)} / {wave.KillGoal}  ·  存活目标 {wave.TargetAlive}";
            if(wave.Encounter==EncounterKind.Elite) return $"精英 {Mathf.Min(wave.ElitesKilled,wave.ElitesRequired)} / {wave.ElitesRequired}";
            if(wave.BossEnraged) return "首领狂暴 · 伤害翻倍";
            if(wave.BossSpawned) return "击败首领完成本幕";
            if(wave.Encounter==EncounterKind.Boss) return "首领即将出现";
            if(wave.StageElapsed<wave.TideNoticeUntil) return "潮涌来袭！";
            return $"目标 {wave.TargetAlive}  ·  首领 {Mathf.Max(0,settings.BossAt-wave.StageElapsed):0} 秒";
        }
        private void PublishRoute()
        {
            var ui=ExpeditionUI;var route=Route;
            if(!ui || route==null) return;
            for(int floor=0;floor<RouteMap.MaxFloors-1;floor++)
                for(int lane=0;lane<RouteMap.Lanes;lane++) PublishNode(ui,route,floor*RouteMap.Lanes+lane);
            PublishNode(ui,route,RouteMap.BossSlot);
            ui.Text("map.title",$"第 {Run.Act} 幕 · {(Run.Map?Run.Map.Label:"")}");
            if(routeView) routeView.Draw(route);
        }
        private static void PublishNode(UIManager ui,RouteMap route,int slot)
        {
            int id=route.FromSlot(slot);string key="map.node."+slot;
            ui.Visible(key+".visible",id>=0);
            if(id<0) return;
            var node=route.Nodes[id];
            ui.Text(key+".glyph",RouteMap.Glyph(node.Kind));ui.Text(key+".label",RouteMap.Label(node.Kind));
            ui.Visible(key+".reachable",route.CanEnter(id));
            ui.Visible(key+".visited",node.Visited && id!=route.Current);
            ui.Visible(key+".current",id==route.Current);
        }
        private void PublishExpedition()
        {
            var ui=ExpeditionUI;if(!ui || !IsReady) return;
            bool battle=Phase==ExpeditionPhase.Battle;
            ui.Visible("expedition.pause",Paused && battle);
            ui.Visible("expedition.return.confirm",returnConfirmation);
            ui.Visible("expedition.camp",Phase==ExpeditionPhase.Shop || Phase==ExpeditionPhase.Reward);
            ui.Visible("expedition.map",Phase==ExpeditionPhase.Map);
            ui.Visible("expedition.event",Phase==ExpeditionPhase.Event || Phase==ExpeditionPhase.Rest);
            ui.Visible("expedition.result",Phase==ExpeditionPhase.Victory || Phase==ExpeditionPhase.Defeat);
            ui.Visible("expedition.victoryNotice",Phase==ExpeditionPhase.VictoryNotice);
            ui.Text("expedition.result.title",Phase==ExpeditionPhase.Victory?"远征胜利":"远征结束");
            int elapsedSeconds=Mathf.FloorToInt(State.Elapsed);
            var current=Route!=null?Route.CurrentNode:null;
            string reached=Run!=null?$"抵达第 {Run.Act} 幕"+(current!=null?$" 第 {current.Floor+1} 层":""):$"抵达第 {Director.Stage} 关";
            ui.Text("expedition.result.body",$"{reached}\n战斗时间 {elapsedSeconds/60} 分 {elapsedSeconds%60:00} 秒\n等级 {Progression.Level}　击杀 {State.Kills:N0}\n本局收入 {Equipment.GoldEarned:0} 金币");
            ui.Texture("expedition.hero.icon",hero.portrait);ui.Text("expedition.hero.name",hero.displayName);
            for(int i=0;i<StatText.DisplayOrder.Length;i++)
            {
                var kind=StatText.DisplayOrder[i];ui.Text("expedition.stat."+i+".name",StatText.Name(kind));
                ui.Text("expedition.stat."+i+".value",StatText.Value(kind,EffectiveStat(kind)));
            }
            if(Run!=null)
            {
                for(int i=0;i<RelicSlots;i++)
                {
                    bool has=i<Run.Relics.Count;
                    ui.Text("expedition.relic."+i,has?Run.Relics[i].displayName:"");ui.Texture("expedition.relic.icon."+i,has?Run.Relics[i].icon:null);
                }
                ui.Text("expedition.relic.count",Run.Relics.Count>RelicSlots?$"+{Run.Relics.Count-RelicSlots}":Run.Relics.Count==0?"尚未获得":"");
            }
            if(Phase==ExpeditionPhase.Map)
            {
                var unit=session.World.EntityManager.GetComponentData<AbilityUnit>(session.Entity);
                ui.Text("map.hint",$"选择下一个节点  ·  生命 {Mathf.CeilToInt(unit.Health)} / {Mathf.CeilToInt(unit.MaxHealth)}  ·  金币 {Equipment.Gold:0}  ·  遗物 {Run.Relics.Count}");
            }
            if(Phase==ExpeditionPhase.Event || Phase==ExpeditionPhase.Rest) PublishEvent(ui);
            if(Camp==null || (Phase!=ExpeditionPhase.Shop && Phase!=ExpeditionPhase.Reward)) return;
            bool reward=Phase==ExpeditionPhase.Reward;
            bool boss=current!=null && current.Kind==RouteNodeKind.Boss;
            ui.Text("camp.title",reward?(boss?$"第 {Run.Act} 幕首领战利品":"精英战利品"):$"商店 · 第 {Run.Act} 幕");
            ui.Text("camp.gold",$"金币 {Equipment.Gold:0}");
            ui.Text("camp.hint",reward?"选择一件遗物，效果立即生效并持续整局":"购买装备立即生效 · 点击已有装备出售 · 离开后回到路线图");
            ui.Visible("camp.choices",reward && !Camp.RelicChosen);ui.Visible("camp.store",!reward);
            ui.Text("camp.refresh",$"刷新货架 · {Camp.RefreshPrice} 金");
            ui.Text("camp.depart.label","离开商店");
            for(int i=0;i<4;i++)
            {
                ui.Visible("camp.relic.visible."+i,i<Camp.RelicOptions.Count);
                if(i<Camp.RelicOptions.Count)
                {
                    var relic=Camp.RelicOptions[i];ui.Text("camp.relic.title."+i,relic.displayName);
                    ui.Text("camp.relic.description."+i,relic.description);ui.Texture("camp.relic.icon."+i,relic.icon);
                }
            }
            for(int i=0;i<5;i++)
            {
                int index=Camp.Stock[i];var item=index>=0?session.Items[index]:null;
                ui.Text("camp.stock.title."+i,item?item.Label:"已售出");
                ui.Text("camp.stock.description."+i,item?item.description:"");
                ui.Text("camp.stock.price."+i,item?$"{item.price} 金":"");ui.Texture("camp.stock.icon."+i,item?item.icon:null);
            }
            for(int i=0;i<6;i++)
            {
                int item=EquippedAt(i);ui.Text("camp.sell."+i,item<0?"空装备位":$"{session.Items[item].Label}\n出售 {equipmentCatalog.SellPrice(session.Items[item])} 金");
                ui.Texture("camp.sell.icon."+i,item<0?null:session.Items[item].icon);
            }
        }
        private void PublishEvent(UIManager ui)
        {
            bool rest=Phase==ExpeditionPhase.Rest;
            ui.Text("event.title",rest?"营火":currentEvent!=null?currentEvent.title:"");
            ui.Text("event.body",rest?"跳动的火光驱散了寒意。在继续前进之前，你可以休整片刻，或者抓紧时间磨练技艺。":currentEvent!=null?currentEvent.body:"");
            float gold=Equipment.Gold;
            for(int i=0;i<ExpeditionEvent.MaxChoices;i++)
            {
                string label=null;bool affordable=true;
                if(rest) {if(i<2) label=RestChoice(i);}
                else if(currentEvent!=null && i<currentEvent.choices.Length)
                {
                    var choice=currentEvent.choices[i];label=choice.label;
                    if(choice.goldCost>0 && gold+0.0001f<choice.goldCost) {affordable=false;label+="（金币不足）";}
                    if(choice.chance<1) label+=$"（成功率 {choice.chance*100:0}%）";
                }
                ui.Visible("event.choice."+i+".visible",!eventDone && label!=null);
                ui.Text("event.choice."+i,label==null?"":affordable?label:$"<color=#8A939C>{label}</color>");
            }
            ui.Visible("event.done",eventDone);
            ui.Text("event.result",eventDone?eventResult:"");
        }
    }
}
