using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Roguelike.Abilities;
using Roguelike.Foundation.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 显式 --game-balance 启用的整局观测。仅发出合法移动、学习、天赋与施法意图；
    /// 不改生命、法力、经验、金币、敌人、波次或战斗时钟。正常游戏不创建此对象。
    /// 输出逐波 CSV 和结局；自动走位不能代替玩家手感验收。
    /// </summary>
    public sealed class GameBalanceProbe : MonoBehaviour
    {
        private GameCombatController controller;
        private EntityManager manager;
        private EntityQuery enemies;
        private World world;
        private string output;
        private readonly StringBuilder rows=new StringBuilder("wave,time,level,spawned,kills,alive,peak_alive,hp,max_hp,min_hp,mana,max_mana,min_mana,mean_mana,mana_regen,casts,skipped,skill_points,q_casts,w_casts,e_casts,r_casts,mana_spent,gold\n");
        private int wave,peak,samples,castsAtStart,lastSerial;
        private float minimumHealth=float.MaxValue,minimumMana=float.MaxValue,manaSum,nextThink;
        private readonly int[] casts=new int[4];
        private int pendingSlot=-1;
        private float manaSpent;
        private float timeScale=1;
        private CombatPlayer lastPlayer;
        private CombatWaveState lastWave;
        private AbilityUnit lastUnit;
        private HeroProgression lastProgression;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--game-balance")<0) return;
            // 构建首场景是主菜单，GameStartupRouter 随后切到战场；观测对象需跨场景保留。
            var go=new GameObject("整局平衡观测（命令行）");
            DontDestroyOnLoad(go); go.AddComponent<GameBalanceProbe>();
        }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--game-output");
            output=index>=0 && index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"WaveBalance");
            Directory.CreateDirectory(output);
            float deadline=Time.realtimeSinceStartup+45;
            while(Time.realtimeSinceStartup<deadline)
            {
                controller=FindFirstObjectByType<GameCombatController>();
                if(controller && controller.IsReady) break;
                yield return null;
            }
            if(!controller || !controller.IsReady) { File.WriteAllText(Path.Combine(output,"result.txt"),"初始化失败"); Application.Quit(2); yield break; }
            world=World.DefaultGameObjectInjectionWorld; manager=world.EntityManager;
            enemies=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            // 默认正常速度。加速仅用于快速诊断；不同步长会影响出手节奏，不能替代正常速度的最终验证。
            int speedArgument=Array.IndexOf(args,"--balance-speed");
            if(speedArgument>=0 && speedArgument+1<args.Length && float.TryParse(args[speedArgument+1],NumberStyles.Float,CultureInfo.InvariantCulture,out float requestedSpeed)) timeScale=Mathf.Clamp(requestedSpeed,1,4);
            Time.timeScale=timeScale;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
            Application.runInBackground=true;
            while(controller.Wave.Outcome==CombatOutcome.Running && controller.State.Elapsed<1200)
            {
                Observe();
                if(controller.State.Elapsed>=nextThink) { Think(); nextThink=controller.State.Elapsed+.2f; }
                yield return null;
            }
            Observe(); WriteRow();
            File.WriteAllText(Path.Combine(output,"waves.csv"),rows.ToString(),Encoding.UTF8);
            string result=$"结局：{controller.Wave.Outcome}\n波次：{controller.Wave.Wave}\n模拟秒：{controller.State.Elapsed:0.0}\n等级：{controller.Progression.Level}\n击杀：{controller.State.Kills}\n跳过敌人：{controller.Wave.Skipped}\n各槽成功施放：{string.Join(",",casts)}\n累计施法耗蓝：{manaSpent:0.0}\n观测方式：{timeScale:0.#} 倍游戏速度；自动环绕走位；合法学习、天赋、施法、用击杀金币到商店购买装备；未改写生命、法力、经验或刷怪状态。";
            File.WriteAllText(Path.Combine(output,"result.txt"),result,Encoding.UTF8);
            Time.timeScale=1;
            Debug.Log(result);
            Application.Quit(controller.Wave.Outcome==CombatOutcome.Victory && controller.Wave.Wave==30 && controller.Wave.Skipped==0?0:1);
        }
        private void Observe()
        {
            var state=controller.State;
            var runtime=manager.GetComponentData<AbilityRuntime>(controller.SessionEntity);
            if(runtime.CastSerial>lastSerial && pendingSlot>=0)
            {
                casts[pendingSlot]+=runtime.CastSerial-lastSerial;
                var slot=manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[pendingSlot];
                manaSpent+=SkillLevels.ManaCost(ref runtime.Library.Value.Skills[slot.Definition],slot.Level);
            }
            lastSerial=runtime.CastSerial; pendingSlot=-1;
            if(controller.Wave.Wave!=wave)
            {
                if(wave>0) WriteRow();
                wave=controller.Wave.Wave; peak=0; samples=0; manaSum=0; minimumHealth=minimumMana=float.MaxValue; castsAtStart=lastSerial;
            }
            var unit=manager.GetComponentData<AbilityUnit>(controller.SessionEntity);
            peak=math.max(peak,state.AliveEnemies); minimumHealth=math.min(minimumHealth,unit.Health); minimumMana=math.min(minimumMana,unit.Mana);
            manaSum+=unit.Mana; samples++;
            lastPlayer=state; lastWave=controller.Wave; lastUnit=unit; lastProgression=controller.Progression;
        }
        private void WriteRow()
        {
            var u=lastUnit; var s=lastPlayer; var w=lastWave;
            rows.AppendLine(string.Join(",",new[]{wave.ToString(),F(s.Elapsed),lastProgression.Level.ToString(),w.SpawnedTotal.ToString(),s.Kills.ToString(),s.AliveEnemies.ToString(),peak.ToString(),F(u.Health),F(u.MaxHealth),F(minimumHealth),F(u.Mana),F(u.MaxMana),F(minimumMana),F(manaSum/math.max(1,samples)),F(AbilityEffects.BaseManaRegen+u.ManaRegenBonus),(lastSerial-castsAtStart).ToString(),w.Skipped.ToString(),lastProgression.SkillPoints.ToString(),casts[0].ToString(),casts[1].ToString(),casts[2].ToString(),casts[3].ToString(),F(manaSpent),F(controller.Equipment.Gold)}));
            File.WriteAllText(Path.Combine(output,"waves.csv"),rows.ToString(),Encoding.UTF8);
        }
        private static string F(float value)=>value.ToString("0.00",CultureInfo.InvariantCulture);
        private void Think()
        {
            var entity=controller.SessionEntity; var state=controller.State;
            var runtime=manager.GetComponentData<AbilityRuntime>(entity);
            var slots=manager.GetBuffer<AbilitySlot>(entity);
            // 三个基础技能均衡加点；终极技能在门槛满足时优先。
            if(controller.Progression.SkillPoints>0)
                foreach(int slot in new[]{3,1,0,2})
                {
                    if(slot!=3 && slots[slot].Level>math.min(slots[0].Level,math.min(slots[1].Level,slots[2].Level))) continue;
                    ref var skill=ref runtime.Library.Value.Skills[slots[slot].Definition];
                    if(HeroProgressionLogic.CanLearn(controller.Progression,ref skill,slots[slot].Level)==ProgressionFailure.None)
                    { controller.LearnSkill(slot); break; }
                }
            if(HeroProgressionLogic.PendingTalent(controller.Progression)>=0) controller.ChooseTalent(0);
            float angle=state.Elapsed*.42f;
            Vector3 destination=new Vector3(math.cos(angle)*18,0,math.sin(angle)*18);
            // 从实际击杀所得金币购买：回复戒指 → 法力宝石 → 吸血獠牙 → 霜语长弓 → 奥术法杖 → 守护铠甲。
            foreach(int id in new[]{6005,6004,6007,6012,6013,6010})
            {
                int item=Array.FindIndex(controller.ShopItems,x=>x && x.configId==id);
                if(item<0) continue;
                var equipment=controller.Equipment;
                var inventory=manager.GetBuffer<EquipmentSlot>(entity);
                if(EquipmentLogic.Owns(inventory,item)) continue;
                if(EquipmentLogic.CanBuy(equipment,inventory,item)==EquipmentFailure.None)
                {
                    if(controller.InShopRange) controller.BuyItem(item);
                    else destination=(Vector3)controller.Shop.Position;
                }
                break;
            }
            controller.SetDestination(destination);
            using var transforms=enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var units=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            float nearest=100000; float3 target=state.Position; int nearby=0;
            for(int i=0;i<transforms.Length;i++)
            {
                if(units[i].Health<=0) continue;
                float d=math.distancesq(state.Position.xz,transforms[i].Position.xz);
                if(d<625) nearby++;
                if(d<nearest) { nearest=d; target=transforms[i].Position; }
            }
            if(nearby==0 || AbilityCastTiming.IsBusy(runtime,runtime.Time)) return;
            var unit=manager.GetComponentData<AbilityUnit>(entity);
            bool bossAlive=manager.Exists(controller.Wave.Boss) && manager.HasComponent<AbilityUnit>(controller.Wave.Boss) && manager.GetComponentData<AbilityUnit>(controller.Wave.Boss).Health>0;
            foreach(int slot in new[]{3,0,1,2})
            {
                var equipped=slots[slot]; ref var skill=ref runtime.Library.Value.Skills[equipped.Definition];
                if(equipped.Level<=0 || equipped.ReadyAt>runtime.Time || (skill.MaxCharges>1 && equipped.Charges<1) || equipped.HitStacks<skill.RequiredHits) continue;
                float cost=SkillLevels.ManaCost(ref skill,equipped.Level);
                if(unit.Mana<cost) continue;
                if(slot!=3 && bossAlive && slots[3].Level>0 && slots[3].ReadyAt<=runtime.Time && unit.Mana-cost<SkillLevels.ManaCost(ref runtime.Library.Value.Skills[slots[3].Definition],slots[3].Level)) continue;
                if(slot==3 && nearby<18 && !bossAlive) continue;
                if(slot==1 && nearby<3) continue;
                if(slot==2)
                {
                    int cluster=0;
                    for(int i=0;i<transforms.Length;i++) if(units[i].Health>0 && math.distancesq(target.xz,transforms[i].Position.xz)<=25) cluster++;
                    if(cluster<4) continue;
                }
                if(slot==0 && (nearest>controller.attackRange*controller.attackRange || nearby<2)) continue;
                float3 point=target;
                if(slot==3 && bossAlive && manager.HasComponent<LocalTransform>(controller.Wave.Boss))
                    point=manager.GetComponentData<LocalTransform>(controller.Wave.Boss).Position;
                float3 direction=math.normalizesafe(point-state.Position,new float3(1,0,0));
                if(skill.Aim==AimMode.Point) point=state.Position+direction*math.min(math.distance(point,state.Position),skill.CastRange);
                manager.GetBuffer<SkillCommand>(entity).Add(new SkillCommand{Slot=slot,Point=point,Direction=direction});
                pendingSlot=slot; return;
            }
        }
        private void OnDestroy() { if(world!=null && world.IsCreated) enemies.Dispose(); }
    }
}
