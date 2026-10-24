using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Roguelike.Abilities;
using Roguelike.Flow;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>只发出合法操作的三关观测：不补生命、法力、经验、金币，不改敌人和导演时钟。自动操作不代表玩家手感。</summary>
    public sealed class GameBalanceProbe:MonoBehaviour
    {
        GameCombatController game;EntityManager manager;EntityQuery enemies;bool queryCreated,finished;
        string output;float nextThink,nextSample,deadline,speed=1;int casts,stageSeen;
        readonly List<string> events=new List<string>();
        readonly StringBuilder rows=new StringBuilder("stage,seconds,level,kills,alive,health,mana,gold,casts\n");
        static string Arg(string key,string fallback) {var a=Environment.GetCommandLineArgs();int n=Array.IndexOf(a,key);return n>=0 && n+1<a.Length?a[n+1]:fallback;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--game-balance")<0) return;
            var go=new GameObject("三关合法操作观测");DontDestroyOnLoad(go);go.AddComponent<GameBalanceProbe>();
        }
        IEnumerator Start()
        {
            output=Arg("--game-output",Path.Combine(Application.persistentDataPath,"ExpeditionBalance"));Directory.CreateDirectory(output);
            speed=Mathf.Clamp(float.Parse(Arg("--balance-speed","1"),CultureInfo.InvariantCulture),1,4);Time.timeScale=speed;
            deadline=Time.realtimeSinceStartup+2400/speed;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            while(!(game=FindFirstObjectByType<GameCombatController>()) || !game.IsReady) {if(Time.realtimeSinceStartup>deadline) {Finish("初始化超时");yield break;}yield return null;}
            manager=World.DefaultGameObjectInjectionWorld.EntityManager;enemies=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));queryCreated=true;
            if(Arg("--balance-hero","archer")=="warrior")
            {
                var selected=game.expedition.selection.heroes[1];RunState.Clear();GameSelection.Clear();
                RunState.Begin(selected,game.map,Array.Find(game.expedition.selection.maps,x=>x!=game.map),20261024);game.SelectHero(selected);
            }
            while(!finished && Time.realtimeSinceStartup<deadline)
            {
                if(!game || !game.IsReady) {game=FindFirstObjectByType<GameCombatController>();yield return null;continue;}
                game.ExternalMovementInput=true;
                if(stageSeen!=game.Run.Stage) {stageSeen=game.Run.Stage;events.Add("进入第 "+stageSeen+" 关，等级 "+game.Progression.Level);}
                if(game.Phase==ExpeditionPhase.Victory || game.Phase==ExpeditionPhase.Defeat) {Finish(game.Phase.ToString());yield break;}
                if(game.Phase==ExpeditionPhase.Camp)
                {
                    if(!game.Camp.RelicChosen) game.ChooseRelic(0);
                    for(int n=0;n<game.Camp.Stock.Length;n++) game.CampBuy(n);
                    if(game.Run.Stage==1) game.ChooseRoute(false);
                    game.Depart();yield return null;continue;
                }
                if(game.Phase==ExpeditionPhase.Battle && Time.unscaledTime>=nextThink) {Think();nextThink=Time.unscaledTime+.12f;}
                if(Time.unscaledTime>=nextSample)
                {
                    nextSample=Time.unscaledTime+2;var u=manager.GetComponentData<AbilityUnit>(game.SessionEntity);var r=manager.GetComponentData<AbilityRuntime>(game.SessionEntity);casts=r.CastSerial;
                    rows.AppendLine(FormattableString.Invariant($"{game.Run.Stage},{game.State.Elapsed:F2},{game.Progression.Level},{game.State.Kills},{game.State.AliveEnemies},{u.Health:F1},{u.Mana:F1},{game.Equipment.Gold:F1},{casts}"));
                    File.WriteAllText(Path.Combine(output,"timeline.csv"),rows.ToString());
                }
                yield return null;
            }
            Finish("观测超时");
        }
        void Think()
        {
            while(game.PendingFormSlot>=0) game.ChooseForm(0);
            var entity=game.SessionEntity;var runtime=manager.GetComponentData<AbilityRuntime>(entity);var slots=manager.GetBuffer<AbilitySlot>(entity);
            if(game.Progression.SkillPoints>0)
                foreach(int n in game.LiveAttack.Delivery==AttackDelivery.Melee?new[]{3,0,2,1}:new[]{3,1,2,0})
                {
                    if(n!=3 && slots[n].Level>math.min(slots[0].Level,math.min(slots[1].Level,slots[2].Level))) continue;
                    if(HeroProgressionLogic.CanLearn(game.Progression,ref runtime.Library.Value.Skills[slots[n].Definition],slots[n].Level)==ProgressionFailure.None) {game.LearnSkill(n);break;}
                }
            float3 position=game.State.Position,target=position+new float3(8,0,0);float nearest=float.MaxValue;int nearby=0;
            using var transforms=enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);using var units=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            for(int i=0;i<transforms.Length;i++)
            {
                if(units[i].Health<=0) continue;float d=math.distancesq(position.xz,transforms[i].Position.xz);
                if(d<225) nearby++;if(d<nearest) {nearest=d;target=transforms[i].Position;}
            }
            var boss=game.Director.Boss;bool bossAlive=manager.Exists(boss) && manager.HasComponent<AbilityUnit>(boss) && manager.GetComponentData<AbilityUnit>(boss).Health>0;
            if(bossAlive) target=manager.GetComponentData<LocalTransform>(boss).Position;
            float2 offset=target.xz-position.xz;float distance=math.length(offset);float2 toward=math.normalizesafe(offset);
            float desired=game.LiveAttack.Delivery==AttackDelivery.Melee?2.7f:game.LiveAttack.Range*.7f;
            var simulation=World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CombatSimulationSystem>();
            float2 direction=toward;float best=float.MaxValue;
            bool ranged=game.LiveAttack.Delivery==AttackDelivery.Projectile;
            // 在合法移动方向中选择两秒后的安全位置，避免一直退到场地边缘。
            float arena=game.arenaHalfSize-5;
            for(int candidate=0;candidate<24;candidate++)
            {
                float angle=candidate*math.PI*2/24;float2 move=new float2(math.cos(angle),math.sin(angle));
                float2 future=position.xz+move*(ranged?9:5);
                float edge=math.max(0,math.cmax(math.abs(future))-arena);
                float score=edge*edge*30+math.abs(math.distance(future,target.xz)-desired)*(bossAlive?1.3f:.35f);
                for(int i=0;i<transforms.Length;i++)
                {
                    if(units[i].Health<=0 || math.distancesq(position.xz,transforms[i].Position.xz)>900) continue;
                    float distanceFuture=math.distance(future,transforms[i].Position.xz);
                    float danger=math.max(0,(ranged?6:1.6f)-distanceFuture);
                    score+=danger*danger*(ranged?3:6);
                }
                foreach(var warning in simulation.Monsters.Visuals)
                {
                    float3 predicted=new float3(future.x,0,future.y);
                    if(SkillTargeting.Contains(warning.Shape,warning.Radius+1,warning.Range,warning.HalfAngle,warning.InnerRadius,warning.Center,warning.Direction,predicted)) score+=50;
                }
                if(score<best) {best=score;direction=move;}
            }
            game.SetMovement((Vector2)direction);
            if(nearby==0 && !bossAlive || AbilityCastTiming.IsBusy(runtime,runtime.Time)) return;
            var owner=manager.GetComponentData<AbilityUnit>(entity);
            foreach(int n in new[]{3,0,1,2})
            {
                var slot=slots[n];ref var skill=ref runtime.Library.Value.Skills[slot.Definition];
                if(skill.AutoTriggerEveryHits>0 || slot.Level<1 || slot.ReadyAt>runtime.Time || slot.Charges<=0 || owner.Mana<SkillLevels.ManaCost(ref skill,slot.Level)) continue;
                if(n==3 && nearby<8 && !bossAlive) continue;
                if(n==1 && game.LiveAttack.Delivery==AttackDelivery.Melee && owner.Health>owner.MaxHealth*.85f && nearby<15) continue;
                if(skill.Aim!=AimMode.Self && distance>skill.CastRange+1) continue;
                if(skill.Aim==AimMode.Self && game.LiveAttack.Delivery==AttackDelivery.Melee && distance>7) continue;
                game.QuickCast(n,target);break;
            }
        }
        void Finish(string outcome)
        {
            if(finished) return;finished=true;Time.timeScale=1;
            var result=new Result{outcome=outcome,speed=speed,hero=game?game.hero.displayName:"",stage=stageSeen,
                seconds=game?game.State.Elapsed:0,level=game?game.Progression.Level:0,kills=game?game.State.Kills:0,events=events.ToArray()};
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));File.WriteAllText(Path.Combine(output,"timeline.csv"),rows.ToString());
            Debug.Log("EXPEDITION_BALANCE_RESULT "+JsonUtility.ToJson(result));Application.Quit(outcome=="Victory"?0:1);
        }
        void OnDestroy() {if(queryCreated && World.DefaultGameObjectInjectionWorld!=null && World.DefaultGameObjectInjectionWorld.IsCreated) enemies.Dispose();}
        [Serializable] class Result {public string outcome,hero;public int stage,level,kills;public float speed,seconds;public string[] events;}
    }
}
