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
            using(var render=new CombatProbeRender(game.gameCamera,false)) Check(render.Save(Path.Combine(output,name+".png")),"渲染 "+name);
        }
        void Supply()
        {
            var unit=manager.GetComponentData<AbilityUnit>(game.SessionEntity);unit.MaxHealth=unit.Health=1000000;unit.MaxMana=unit.Mana=100000;
            manager.SetComponentData(game.SessionEntity,unit);
        }
        float deadline;bool finished;
        void Update() {if(!finished && deadline>0 && Time.realtimeSinceStartup>deadline) {failures.Add("流程验证超时");Finish();}}
        IEnumerator Start()
        {
            output=Arg("--expedition-output",Arg("--game-output",Path.Combine(Application.persistentDataPath,"Expedition")));Directory.CreateDirectory(output);
            deadline=Time.realtimeSinceStartup+180;Application.runInBackground=true;yield return Ready();
            if(!game || !game.IsReady) {Finish();yield break;}
            if(Arg("--expedition-hero","archer")=="warrior")
            {
                var selected=game.expedition.selection.heroes[1];RunState.Clear();GameSelection.Clear();
                RunState.Begin(selected,game.map,Array.Find(game.expedition.selection.maps,x=>x!=game.map));game.SelectHero(selected);
            }
            game.Run.Seed=20261024;
            Supply();game.ExternalMovementInput=true;
            var startPosition=game.State.Position;game.SetMovement(Vector2.right);yield return new WaitForSeconds(.3f);game.SetMovement(Vector2.zero);
            Check(game.State.Position.x>startPosition.x+1,"方向输入驱动英雄移动且不自动追击");
            yield return new WaitForSeconds(.2f);
            Check(game.Progression.Level==1 && game.Progression.SkillPoints==1,"一级一个技能点开局");
            game.LearnSkill(0);yield return null;yield return null;
            Check(manager.GetBuffer<AbilitySlot>(game.SessionEntity)[0].Level==1,"学习技能消耗权威技能点");
            game.SetPaused(true);float before=game.Director.StageElapsed;yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Approximately(before,game.Director.StageElapsed),"暂停冻结战斗计时");yield return Capture("pause");game.SetPaused(false);
            var progression=game.Progression;HeroProgressionLogic.GainExperience(ref progression,20020);HeroProgressionLogic.ApplyPendingLevels(ref progression);
            manager.SetComponentData(game.SessionEntity,progression);
            for(int i=0;i<4;i++) for(int level=0;level<(i==3?3:4);level++) {game.LearnSkill(i);yield return null;yield return null;}
            manager.World.GetExistingSystemManaged<CombatSimulationSystem>().RefreshHero(game.SessionEntity);
            Check(game.Progression.Level==15 && game.Progression.SkillPoints==0,"十五点可完整学习四个技能");
            yield return Capture("form-choice");
            for(int i=0;i<4;i++) Check(game.ChooseForm(i%2),"选择技能形态 "+i);
            Supply();yield return Capture("battle");
            for(int stage=1;stage<=3;stage++)
            {
                Supply();var director=game.Director;director.StageElapsed=211;director.NextTickAt=0;manager.SetComponentData(game.SessionEntity,director);
                float wait=Time.realtimeSinceStartup+5;
                while(Time.realtimeSinceStartup<wait && !game.Director.BossSpawned) yield return null;
                Check(game.Director.BossSpawned,"第 "+stage+" 关首领生成");
                if(!game.Director.BossSpawned) break;
                var boss=manager.GetComponentData<AbilityUnit>(game.Director.Boss);boss.Health=0;manager.SetComponentData(game.Director.Boss,boss);
                yield return new WaitForSecondsRealtime(2.4f);
                if(stage==3) {Check(game.Phase==ExpeditionPhase.Victory,"击败第三关首领完成远征");yield return Capture("victory");break;}
                Check(game.Phase==ExpeditionPhase.Camp,"第 "+stage+" 次营地");
                Check(!game.Depart() && !game.CampBuy(0),"选择遗物前不能交易或出发");
                Check(game.Camp.RelicOptions.Count==(stage==2?4:3),"营地遗物选项数量");yield return Capture("camp-relic-"+stage);
                Check(game.ChooseRelic(0),"选择遗物");
                // 六件最高价装备加多次刷新也必须有余款，避免随机货架把流程测试变成金币不足测试。
                var purse=game.Equipment;purse.Gold=100000;manager.SetComponentData(game.SessionEntity,purse);
                if(stage==2 && EquipmentLogic.FreeSlot(manager.GetBuffer<EquipmentSlot>(game.SessionEntity))<0) game.CampSell(0);
                Check(game.CampBuy(0),"购买货架装备");float gold=game.Equipment.Gold;
                Check(game.CampRefresh() && Mathf.Approximately(game.Equipment.Gold,gold-50) && game.Camp.RefreshPrice==100,"刷新扣款与递增费用");
                if(stage==1)
                {
                    for(int repeat=0;repeat<8 && EquipmentLogic.FreeSlot(manager.GetBuffer<EquipmentSlot>(game.SessionEntity))>=0;repeat++)
                    { for(int slot=0;slot<5;slot++) game.CampBuy(slot);if(EquipmentLogic.FreeSlot(manager.GetBuffer<EquipmentSlot>(game.SessionEntity))>=0) game.CampRefresh(); }
                    Check(EquipmentLogic.FreeSlot(manager.GetBuffer<EquipmentSlot>(game.SessionEntity))<0,"营地可购买至六格装满");
                    game.CampRefresh();float fullGold=game.Equipment.Gold;int stock=game.Camp.Stock[0];
                    Check(!game.CampBuy(0) && game.Equipment.Gold==fullGold && game.Camp.Stock[0]==stock,"满背包交易失败不扣款不消耗货架");
                    int item=game.EquippedAt(0);float refund=game.Equipment.Library.Value.Items[item].SellPrice;
                    Check(game.CampSell(0) && game.EquippedAt(0)<0 && Mathf.Approximately(game.Equipment.Gold,fullGold+refund),"营地出售返还五成价格");
                    Check(game.CampBuy(0) && game.EquippedAt(0)>=0,"出售后新装备填入首个空格");
                    Check(game.ChooseRoute(true),"选择凶险路线");
                }
                yield return Capture("camp-store-"+stage);var old=game;
                Check(game.Depart(),"从营地出发");
                while(old) yield return null;
                yield return Ready();Supply();
                Check(game.Director.Stage==stage+1 && game.Progression.Level==15,"关卡和等级跨场景保留");
                Check(manager.GetBuffer<AbilitySlot>(game.SessionEntity)[0].FormChoice==1 && game.EquippedAt(0)>=0,"形态与装备跨场景保留");
                Check(game.Run.Relics.Count==stage,"遗物跨场景保留");
                if(stage==2) Check(game.EnemyKinds.Length==15 && game.Run.Abyss,"深渊混合两图普通怪及专属精英");
            }
            var saved=game.Run;var selectedHero=game.hero;var first=saved.FirstMap;var second=saved.SecondMap;
            game.ReturnToSelection();yield return null;yield return null;
            Check(RunState.Current==null && GameSelection.Hero==null,"胜利返回选择清空局内状态");
            RunState.Begin(selectedHero,first,second);GameSelection.Set(selectedHero,first);
            UnityEngine.SceneManagement.SceneManager.LoadScene(Roguelike.Foundation.ResourcePaths.Resolve(first.sceneId,"scene"));
            yield return Ready();
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
