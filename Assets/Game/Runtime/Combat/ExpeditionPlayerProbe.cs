using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Flow;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>显式启用的加速流程验收；跳转首领时钟、补给生命金币，结果不代表正常难度通关。</summary>
    public sealed class ExpeditionPlayerProbe : MonoBehaviour
    {
        GameCombatController game;EntityManager manager;string output;int checks;readonly List<string> failures=new List<string>();
        static bool Has(string key)=>Array.IndexOf(Environment.GetCommandLineArgs(),key)>=0;
        static string Arg(string key,string fallback) {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if(!Has("--expedition-smoke") && !Has("--game-smoke")) return;
            var root=new GameObject("远征流程验证");DontDestroyOnLoad(root);root.AddComponent<ExpeditionPlayerProbe>();
        }
        void Check(bool condition,string name)
        {
            if(condition) {checks++;Debug.Log("EXPEDITION_PASS "+name);}
            else {failures.Add(name);Debug.LogError("EXPEDITION_FAIL "+name);}
        }
        IEnumerator Ready()
        {
            float limit=Time.realtimeSinceStartup+45;
            while(Time.realtimeSinceStartup<limit && (!(game=FindFirstObjectByType<GameCombatController>()) || !game.IsReady)) yield return null;
            Check(game && game.IsReady,"战场就绪");manager=World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        IEnumerator Capture(string name)
        {
            // 直接调用玩法 API 后，等待限频 HUD 发布本帧状态，再验证真实像素。
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            using(var render=new CombatProbeRender(game.gameCamera,false,Screen.width,Screen.height)) Check(render.Save(Path.Combine(output,name+".png")),"渲染 "+name);
        }
        void Supply()
        {
            var unit=manager.GetComponentData<AbilityUnit>(game.SessionEntity);unit.MaxHealth=unit.Health=1000000;unit.MaxMana=unit.Mana=100000;
            manager.SetComponentData(game.SessionEntity,unit);
        }
        float deadline;bool finished;
        void Update() {if(!finished && deadline>0 && Time.realtimeSinceStartup>deadline) {failures.Add("流程验证超时");Finish();}}
        readonly HashSet<string> captured=new HashSet<string>();
        IEnumerator CaptureOnce(string name) {if(captured.Add(name)) yield return Capture(name);}
        /// <summary>直接把本场遭遇推到完成条件：击杀数 / 精英数达标，或等首领出现后清空其生命。</summary>
        IEnumerator WinBattle()
        {
            Supply();var director=game.Director;
            if(director.Encounter==EncounterKind.Kills) director.EncounterKills=director.KillGoal;
            else if(director.Encounter==EncounterKind.Elite) director.ElitesKilled=director.ElitesRequired;
            manager.SetComponentData(game.SessionEntity,director);
            if(director.Encounter==EncounterKind.Boss)
            {
                float spawn=Time.realtimeSinceStartup+8;
                while(Time.realtimeSinceStartup<spawn && !game.Director.BossSpawned) yield return null;
                Check(game.Director.BossSpawned,"第 "+game.Run.Act+" 幕首领生成");
                if(game.Director.BossSpawned) {yield return CaptureOnce("boss-"+game.Run.Act);var boss=manager.GetComponentData<AbilityUnit>(game.Director.Boss);boss.Health=0;manager.SetComponentData(game.Director.Boss,boss);}
            }
            float wait=Time.realtimeSinceStartup+6;
            while(Time.realtimeSinceStartup<wait && game && (game.Phase==ExpeditionPhase.Battle || game.Phase==ExpeditionPhase.VictoryNotice)) yield return null;
        }
        IEnumerator Start()
        {
            output=Arg("--expedition-output",Arg("--game-output",Path.Combine(Application.persistentDataPath,"Expedition")));Directory.CreateDirectory(output);
            deadline=Time.realtimeSinceStartup+300;Application.runInBackground=true;yield return Ready();
            if(!game || !game.IsReady) {Finish();yield break;}
            if(Arg("--expedition-hero","archer")=="warrior")
            {
                var selected=game.expedition.selection.heroes[1];RunState.Clear();GameSelection.Clear();
                RunState.Begin(selected,game.map,Array.Find(game.expedition.selection.maps,x=>x!=game.map));game.SelectHero(selected);
            }
            game.Run.Seed=20261024;
            Check(game.Phase==ExpeditionPhase.Map && game.ReachableSlots().Count>=2,"开局显示路线图，第 0 层至少两个入口");
            yield return Capture("route-map");
            Check(game.EnterNode(game.ReachableSlots()[0]) && game.Phase==ExpeditionPhase.Battle,"进入第 0 层战斗");
            Check(game.Director.Encounter==EncounterKind.Kills && game.Director.KillGoal>0,"普通战斗使用击杀目标");
            if(Has("--hud-review"))
            {
                game.SetPaused(true);yield return Capture("hud-opening-paused");game.SetPaused(false);
                yield return Capture("hud-opening");game.ToggleStats();yield return Capture("hero-stats");game.ToggleStats();
            }
            Supply();game.ExternalMovementInput=true;
            var startPosition=game.State.Position;game.SetMovement(Vector2.right);yield return new WaitForSeconds(.3f);game.SetMovement(Vector2.zero);
            Check(game.State.Position.x>startPosition.x+1,"方向输入驱动英雄移动且不自动追击");
            yield return new WaitForSeconds(.2f);
            Check(game.Progression.Level==1 && game.Progression.SkillPoints==1,"一级一个技能点开局");
            game.LearnSkill(0);yield return null;yield return null;
            Check(manager.GetBuffer<AbilitySlot>(game.SessionEntity)[0].Level==1,"学习技能消耗权威技能点");
            if(Has("--hud-review")) {game.CastImmediately(0);yield return Capture("skill-cooldown");}
            game.SetPaused(true);float before=game.Director.StageElapsed;yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Approximately(before,game.Director.StageElapsed),"暂停冻结战斗计时");yield return Capture("pause");game.SetPaused(false);
            var progression=game.Progression;HeroProgressionLogic.GainExperience(ref progression,20020);HeroProgressionLogic.ApplyPendingLevels(ref progression);
            manager.SetComponentData(game.SessionEntity,progression);
            for(int i=0;i<4;i++) for(int level=0;level<(i==3?3:4);level++) {game.LearnSkill(i);yield return null;yield return null;}
            manager.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(game.SessionEntity);
            Check(game.Progression.Level==15 && game.Progression.SkillPoints==0,"十五点可完整学习四个技能");
            yield return Capture("form-choice");
            for(int i=0;i<4;i++) Check(game.ChooseForm(i%2),"选择技能形态 "+i);
            Supply();yield return new WaitForSeconds(.6f);
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit)))
            using(var enemies=query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                Check(enemies.Length>0 && game.SelectEnemy(enemies[0]),"左键选中敌人显示单位面板");
            }
            yield return Capture("target-info");
            game.ClearSelection();
            var seen=new HashSet<RouteNodeKind>();bool shopChecked=false;int act=1;
            for(int guard=0;guard<80 && !finished;guard++)
            {
                if(!game) {yield return Ready();continue;}
                var phase=game.Phase;
                if(phase==ExpeditionPhase.Battle) {yield return WinBattle();continue;}
                if(phase==ExpeditionPhase.VictoryNotice) {yield return null;continue;}
                if(phase==ExpeditionPhase.Victory) {Check(act==RunState.FinalAct,"击败第二幕首领完成远征");yield return Capture("victory");break;}
                if(phase==ExpeditionPhase.Defeat) {Check(false,"流程中意外失败");break;}
                if(phase==ExpeditionPhase.Map)
                {
                    yield return CaptureOnce("route-map-"+game.Run.Act+"-"+(game.Route.CurrentFloor+1));
                    var slots=game.ReachableSlots();
                    Check(slots.Count>0,"路线图始终有可前往的节点");if(slots.Count==0) break;
                    int choice=slots[0];
                    foreach(int slot in slots) {var kind=game.Route.Nodes[game.Route.FromSlot(slot)].Kind;if(!seen.Contains(kind)) {choice=slot;break;}}
                    seen.Add(game.Route.Nodes[game.Route.FromSlot(choice)].Kind);
                    Check(game.EnterNode(choice),"进入路线图节点");continue;
                }
                if(phase==ExpeditionPhase.Reward)
                {
                    bool boss=game.Route.CurrentNode.Kind==RouteNodeKind.Boss;
                    Check(game.Camp.RelicOptions.Count>0 && game.Camp.RelicOptions.Count<=CampState.RelicChoices,"战利品提供遗物三选一");
                    yield return CaptureOnce(boss?"reward-boss-"+act:"reward-elite");
                    int relics=game.Run.Relics.Count;var old=game;
                    Check(game.ChooseRelic(0) && game.Run.Relics.Count==relics+1,"选择遗物");
                    if(boss && act<RunState.FinalAct)
                    {
                        while(old) yield return null;
                        yield return Ready();Supply();act++;
                        Check(game.Run.Act==act && game.Phase==ExpeditionPhase.Map && game.Progression.Level==15,"第二幕路线图与等级跨场景保留");
                        Check(manager.GetBuffer<AbilitySlot>(game.SessionEntity)[0].FormChoice==1 && game.Run.Relics.Count==relics+1,"形态与遗物跨场景保留");
                        seen.Clear();
                    }
                    continue;
                }
                if(phase==ExpeditionPhase.Shop)
                {
                    if(!shopChecked)
                    {
                        shopChecked=true;
                        var purse=game.Equipment;purse.Gold=100000;manager.SetComponentData(game.SessionEntity,purse);
                        int stocked=Array.FindIndex(game.Camp.Stock,item=>item>=0);
                        Check(stocked>=0 && game.CampBuy(stocked),"商店购买装备");float gold=game.Equipment.Gold;
                        Check(game.CampRefresh() && Mathf.Approximately(game.Equipment.Gold,gold-50) && game.Camp.RefreshPrice==100,"刷新扣款与递增费用");
                        int slot=-1;for(int i=0;i<6;i++) if(game.EquippedAt(i)>=0) {slot=i;break;}
                        if(slot>=0)
                        {
                            float before2=game.Equipment.Gold;float refund=game.Equipment.Library.Value.Items[game.EquippedAt(slot)].SellPrice;
                            Check(game.CampSell(slot) && Mathf.Approximately(game.Equipment.Gold,before2+refund),"商店出售返还五成价格");
                        }
                        yield return Capture("shop");
                    }
                    Check(game.Depart() && game.Phase==ExpeditionPhase.Map,"离开商店回到路线图");continue;
                }
                if(phase==ExpeditionPhase.Event || phase==ExpeditionPhase.Rest)
                {
                    var purse=game.Equipment;purse.Gold=Mathf.Max(purse.Gold,1000);manager.SetComponentData(game.SessionEntity,purse);
                    yield return CaptureOnce(phase==ExpeditionPhase.Rest?"rest":"event");
                    Check(game.ChooseEventOption(0) && game.EventDone && !string.IsNullOrEmpty(game.EventResult),(phase==ExpeditionPhase.Rest?"营火":"事件")+"选项结算并显示结果");
                    yield return CaptureOnce(phase==ExpeditionPhase.Rest?"rest-result":"event-result");
                    Check(game.ContinueEvent() && game.Phase==ExpeditionPhase.Map,"事件结束回到路线图");continue;
                }
                yield return null;
            }
            Check(seen.Count>=3,"流程至少经历三种节点");
            if(!game) yield return Ready();
            var saved=game.Run;var selectedHero=game.hero;var first=saved.FirstMap;var second=saved.SecondMap;
            game.ReturnToSelection();yield return null;yield return null;
            Check(RunState.Current==null && GameSelection.Hero==null,"胜利返回选择清空局内状态");
            RunState.Begin(selectedHero,first,second);GameSelection.Set(selectedHero,first);
            UnityEngine.SceneManagement.SceneManager.LoadScene(Roguelike.Foundation.ResourcePaths.Resolve(first.sceneId,"scene"));
            yield return Ready();
            Check(game.EnterNode(game.ReachableSlots()[0]),"新一局进入第一场战斗");
            var dead=manager.GetComponentData<AbilityUnit>(game.SessionEntity);dead.Health=0;manager.SetComponentData(game.SessionEntity,dead);
            yield return new WaitForSecondsRealtime(.3f);Check(game.Phase==ExpeditionPhase.Defeat,"生命归零进入失败结算");yield return Capture("defeat");
            game.ReturnToSelection();yield return null;yield return null;
            Check(RunState.Current==null && !FindFirstObjectByType<GameCombatController>(),"失败返回选择释放战斗会话");
            Finish();
        }
        void Finish()
        {
            if(finished) return;finished=true;
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{passed=failures.Count==0,checks=checks,failures=failures.ToArray()},true));
            Debug.Log("EXPEDITION_PLAYER_RESULT passed="+(failures.Count==0)+" checks="+checks);Application.Quit(failures.Count==0?0:1);
        }
        [Serializable] class Result {public bool passed;public int checks;public string[] failures;}
    }
}
