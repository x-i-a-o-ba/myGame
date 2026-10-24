using System;
using System.Text;
using Roguelike.Abilities;
using Roguelike.Flow;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Combat
{
    public enum ExpeditionPhase { Battle,VictoryNotice,Camp,Victory,Defeat }
    public sealed partial class GameCombatController
    {
        public ExpeditionCatalog expedition;
        public ExpeditionPhase Phase {get;private set;}
        public CampState Camp {get;private set;}
        private float victoryUntil;
        private bool returnConfirmation;
        private bool abyssLightingApplied;
        public RunState Run=>RunState.Current;
        private void PrepareExpedition()
        {
            if(!expedition) return;
            if(Run==null || Run.Hero!=hero)
            {
                var second=Array.Find(expedition.selection.maps,value=>value && value!=map);
                RunState.Begin(hero,map,second);
            }
            inputProfile?.Validate();
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
            Phase=ExpeditionPhase.Battle;Camp=null;Paused=false;returnConfirmation=false;
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
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);unit.Health=unit.MaxHealth;unit.Mana=unit.MaxMana;
            manager.SetComponentData(session.Entity,unit);
            var player=State;player.Health=unit.Health;player.MaximumHealth=unit.MaxHealth;player.Kills=Run.Kills;player.Elapsed=Run.TotalBattleTime;
            manager.SetComponentData(session.Entity,player);
            var director=manager.GetComponentData<SpawnDirectorSettings>(session.Entity);
            manager.SetComponentData(session.Entity,CombatDirectorState.Begin(director,Run.Stage,Run.Seed));
            var settings=manager.GetComponentData<CombatSettings>(session.Entity);
            settings.Lava=map && map.lavaHazards;
            settings.PassiveGoldPerSecond=expedition.passiveGoldPerSecond;
            settings.DamageMultiplier=map?map.enemyDamageMultiplier:1;
            settings.HealthMultiplier=(map?map.enemyHealthMultiplier:1)*(Run.Abyss?1+expedition.abyssHealthBonus:1)*(Run.Stage==2 && Run.DangerousRoute?1+expedition.dangerousHealthBonus:1);
            settings.GoldMultiplier=Run.Stage==2 && Run.DangerousRoute?expedition.dangerousGoldMultiplier:1;
            settings.EliteCount=Run.Stage==2 && Run.DangerousRoute?2:1;
            manager.SetComponentData(session.Entity,settings);
            if(Run.Abyss && !abyssLightingApplied)
            {
                abyssLightingApplied=true;
                RenderSettings.ambientLight*=.58f;gameCamera.backgroundColor=new Color(.025f,.018f,.055f);
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.intensity*=.62f;
            }
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
            if(playerView) playerView.localScale=Vector3.one*traits.BodyScale;
        }
        private void StepExpedition()
        {
            if(!expedition || Run==null || !IsReady) return;
            if(Phase==ExpeditionPhase.Battle && Director.Outcome!=CombatOutcome.Running)
            {
                CaptureRun();Paused=false;
                if(Director.Outcome==CombatOutcome.Defeat) Phase=ExpeditionPhase.Defeat;
                else {Phase=ExpeditionPhase.VictoryNotice;victoryUntil=Time.unscaledTime+expedition.victoryNoticeSeconds;}
            }
            if(Phase==ExpeditionPhase.VictoryNotice && Time.unscaledTime>=victoryUntil)
            {
                if(Run.Stage==3) Phase=ExpeditionPhase.Victory;
                else {Camp=new CampState(Run,expedition,session.Items);Phase=ExpeditionPhase.Camp;}
            }
        }
        private void ExpeditionCommand(UiCommand command)
        {
            switch(command.Id)
            {
                case "expedition.resume":SetPaused(false);returnConfirmation=false;break;
                case "expedition.return":ReturnToSelection();break;
                case "expedition.form":ChooseForm(command.Value);break;
                case "camp.relic":ChooseRelic(command.Value);break;
                case "camp.buy":CampBuy(command.Value);break;
                case "camp.sell":CampSell(command.Value);break;
                case "camp.refresh":CampRefresh();break;
                case "camp.route":ChooseRoute(command.Value==1);break;
                case "camp.depart":Depart();break;
            }
            nextHudUpdate=0;
        }
        public bool ChooseRelic(int option)
        {
            if(Phase!=ExpeditionPhase.Camp || Camp.RelicChosen || option<0 || option>=Camp.RelicOptions.Count) return false;
            Run.Relics.Add(Camp.RelicOptions[option]);Camp.RelicChosen=true;ApplyRelics();CaptureRun();return true;
        }
        public bool CampBuy(int slot)
        {
            if(Phase!=ExpeditionPhase.Camp || !Camp.RelicChosen || slot<0 || slot>=Camp.Stock.Length || Camp.Stock[slot]<0) return false;
            var manager=session.World.EntityManager;var purse=Equipment;
            bool bought=EquipmentLogic.TryBuy(ref purse,manager.GetBuffer<EquipmentSlot>(session.Entity),Camp.Stock[slot]);
            manager.SetComponentData(session.Entity,purse);
            if(bought) {Camp.Stock[slot]=-1;session.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(session.Entity);CaptureRun();}
            else ShowNotice(EquipmentLogic.Describe(purse.LastFailure),3);
            return bought;
        }
        public bool CampSell(int slot)
        {
            if(Phase!=ExpeditionPhase.Camp || !Camp.RelicChosen) return false;
            var manager=session.World.EntityManager;var purse=Equipment;
            bool sold=EquipmentLogic.TrySell(ref purse,manager.GetBuffer<EquipmentSlot>(session.Entity),slot);
            manager.SetComponentData(session.Entity,purse);
            if(sold) {session.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(session.Entity);CaptureRun();}
            return sold;
        }
        public bool CampRefresh()
        {
            if(Phase!=ExpeditionPhase.Camp || !Camp.RelicChosen) return false;
            var purse=Equipment;if(purse.Gold<Camp.RefreshPrice) {ShowNotice("金币不足，无法刷新",2);return false;}
            purse.Gold-=Camp.RefreshPrice;Camp.Refreshes++;
            session.World.EntityManager.SetComponentData(session.Entity,purse);CaptureRun();Camp.Roll(Run,session.Items);return true;
        }
        public bool ChooseRoute(bool dangerous)
        {
            if(Phase!=ExpeditionPhase.Camp || !Camp.RelicChosen || Run.Stage!=1) return false;
            Run.DangerousRoute=dangerous;Camp.RouteChosen=true;return true;
        }
        public bool Depart()
        {
            if(Phase!=ExpeditionPhase.Camp || !Camp.CanDepart) return false;
            CaptureRun();Run.Advance();SceneManager.LoadScene(ResourcePaths.Resolve(Run.Map.sceneId,"scene"));return true;
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
        private void PublishExpedition()
        {
            var ui=ExpeditionUI;if(!ui || !IsReady) return;
            ui.Visible("expedition.pause",Paused && Phase==ExpeditionPhase.Battle);
            ui.Visible("expedition.return.confirm",returnConfirmation);
            ui.Visible("expedition.camp",Phase==ExpeditionPhase.Camp);
            ui.Visible("expedition.result",Phase==ExpeditionPhase.Victory || Phase==ExpeditionPhase.Defeat);
            ui.Visible("expedition.victoryNotice",Phase==ExpeditionPhase.VictoryNotice);
            ui.Text("expedition.result.title",Phase==ExpeditionPhase.Victory?"远征胜利":"远征结束");
            ui.Text("expedition.result.body",$"抵达第 {Director.Stage} 关\n战斗时间 {State.Elapsed/60:0} 分 {State.Elapsed%60:00} 秒\n等级 {Progression.Level}　击杀 {State.Kills:N0}\n本局收入 {Equipment.GoldEarned:0} 金币");
            var stats=new StringBuilder();foreach(var kind in StatText.DisplayOrder) stats.Append(StatText.Name(kind)).Append("  ").Append(StatText.Value(kind,EffectiveStat(kind))).AppendLine();
            ui.Text("expedition.stats.body",stats.ToString());
            if(Run!=null) for(int i=0;i<2;i++)
            {ui.Text("expedition.relic."+i,i<Run.Relics.Count?Run.Relics[i].displayName:"");ui.Texture("expedition.relic.icon."+i,i<Run.Relics.Count?Run.Relics[i].icon:null);}
            if(Phase!=ExpeditionPhase.Camp) return;
            ui.Text("camp.title",$"第 {Run.Stage} 关完成 · 营地");ui.Text("camp.gold",$"金币 {Equipment.Gold:0}");
            ui.Text("camp.hint",!Camp.RelicChosen?"先选择一件遗物":!Camp.RouteChosen?"整备装备，并选择下一关路线":"整备完成后出发 · 下一关回复全部生命与法力");
            ui.Visible("camp.choices",!Camp.RelicChosen);ui.Visible("camp.store",Camp.RelicChosen);ui.Visible("camp.routes",Camp.RelicChosen && Run.Stage==1);
            ui.Text("camp.refresh",$"刷新货架 · {Camp.RefreshPrice} 金");
            ui.Text("camp.route.status",Camp.RouteChosen?(Run.DangerousRoute?"已选凶险路线":"已选稳健路线"):"请选择路线");
            for(int i=0;i<4;i++)
            {
                ui.Visible("camp.relic.visible."+i,i<Camp.RelicOptions.Count);
                if(i<Camp.RelicOptions.Count) ui.Text("camp.relic."+i,Camp.RelicOptions[i].displayName+"\n\n"+Camp.RelicOptions[i].description);
            }
            for(int i=0;i<5;i++)
            {
                int index=Camp.Stock[i];var item=index>=0?session.Items[index]:null;
                ui.Text("camp.stock."+i,item?$"{item.Label}\n{item.price} 金\n\n{item.description}":"已售出");ui.Texture("camp.stock.icon."+i,item?item.icon:null);
            }
            for(int i=0;i<6;i++) {int item=EquippedAt(i);ui.Text("camp.sell."+i,item<0?"空装备位":$"{session.Items[item].Label}\n出售 {equipmentCatalog.SellPrice(session.Items[item])} 金");}
        }
    }
}
