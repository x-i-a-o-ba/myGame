using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace Roguelike.Combat
{
    /// <summary>仅显式命令行启用。功能检查跳转导演时钟；压力测试固定种子/负载、补英雄生命法力并自动施法，不代表正常平衡。</summary>
    public sealed class DirectorPlayerProbe : MonoBehaviour
    {
        private GameCombatController controller;
        private EntityManager manager;
        private EntityQuery enemies;
        private bool hasQuery,failed;
        private int passed;
        private string output;
        private readonly List<string> failures=new();
        private readonly WaitForEndOfFrame renderPhase=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if(!Has("--director-smoke") && !Has("--combat-stress")) return;
            var go=new GameObject("刷怪导演验证（命令行）"); DontDestroyOnLoad(go); go.AddComponent<DirectorPlayerProbe>();
        }
        private static bool Has(string key)=>Array.IndexOf(Environment.GetCommandLineArgs(),key)>=0;
        private static string Arg(string key,string fallback)
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key);
            return i>=0 && i+1<args.Length?args[i+1]:fallback;
        }
        private IEnumerator Start()
        {
            output=Arg("--director-output",Path.Combine(Application.persistentDataPath,"Director")); Directory.CreateDirectory(output);
            float deadline=Time.realtimeSinceStartup+30;
            while(Time.realtimeSinceStartup<deadline && (!(controller=FindFirstObjectByType<GameCombatController>()) || !controller.IsReady)) yield return null;
            Check(controller && controller.IsReady,"战场与导演初始化");
            if(!failed)
            {
                manager=World.DefaultGameObjectInjectionWorld.EntityManager;
                enemies=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform)); hasQuery=true;
                Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
                if(Has("--combat-stress")) yield return Stress(); else yield return Smoke();
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{passed=!failed,checks=passed,failures=failures.ToArray()},true));
            Debug.Log($"DIRECTOR_PLAYER_RESULT passed={!failed} checks={passed} output={output}");
            Application.Quit(failed?1:0);
        }
        private void Check(bool condition,string name)
        {
            if(condition) { passed++; Debug.Log("DIRECTOR_PASS "+name); }
            else { failed=true; failures.Add(name); Debug.LogError("DIRECTOR_FAIL "+name); }
        }
        private void KeepHero()
        {
            var unit=manager.GetComponentData<AbilityUnit>(controller.SessionEntity);
            unit.Health=unit.MaxHealth=100000000; unit.Mana=unit.MaxMana=100000;
            manager.SetComponentData(controller.SessionEntity,unit);
        }
        private void Seek(float second)
        {
            KeepHero(); var state=controller.Director; state.StageElapsed=second; state.NextTickAt=0;
            manager.SetComponentData(controller.SessionEntity,state);
        }
        private IEnumerator Smoke()
        {
            yield return new WaitForSeconds(0.6f);
            Check(controller.State.AliveEnemies>0 && controller.State.AliveEnemies<100,"开局按恢复比例补怪");
            var view=manager.GetComponentData<CombatSpawnView>(controller.SessionEntity);
            bool outside=view.Valid;
            using(var positions=enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp))
                foreach(var position in positions) outside&=math.any(position.Position.xz<view.Min)||math.any(position.Position.xz>view.Max);
            Check(outside,"实际相机下开局怪物都在镜头外");
            Time.timeScale=0; yield return null; yield return null;
            float before=controller.Director.StageElapsed;
            yield return new WaitForSecondsRealtime(0.2f);
            Check(Mathf.Approximately(before,controller.Director.StageElapsed),"暂停不推进导演时钟"); Time.timeScale=1;
            Seek(90); yield return null; yield return null;
            Check(controller.Director.ElitesSpawned==1,"90 秒首次精英");
            Seek(150); yield return null; yield return null;
            Check(controller.Director.TideStarted && controller.Director.PendingTide>0,"150 秒潮涌分批开始");
            yield return Capture("tide.png");
            int elites=controller.Director.ElitesSpawned;
            Seek(210); yield return null; yield return null;
            var boss=controller.Director.Boss;
            Check(controller.Director.BossSpawned && manager.Exists(boss),"210 秒关底首领出现");
            if(!manager.Exists(boss)) yield break;
            Check(manager.GetComponentData<AbilityUnit>(boss).MaxHealth==6000,"首关首领固定 6000 生命");
            Check(controller.Director.PendingTide==0 && controller.Director.ElitesSpawned==elites,"首领出现后停止精英并清空潮涌余量");
            float attack=manager.GetComponentData<AbilityUnit>(boss).BaseAttack;
            Seek(270); yield return null; yield return null;
            Check(controller.Director.BossEnraged && Mathf.Approximately(manager.GetComponentData<AbilityUnit>(boss).BaseAttack,attack*2),"270 秒首领攻击翻倍一次");
            Seek(280); yield return null;
            Check(Mathf.Approximately(manager.GetComponentData<AbilityUnit>(boss).BaseAttack,attack*2),"狂暴不会反复翻倍");
            var dead=manager.GetComponentData<AbilityUnit>(boss); dead.Health=0; manager.SetComponentData(boss,dead);
            yield return null; yield return null;
            Check(controller.Director.Outcome==CombatOutcome.Victory,"首领死亡完成本关");
            int total=controller.Director.SpawnedTotal; yield return new WaitForSeconds(0.4f);
            Check(controller.Director.SpawnedTotal==total,"关卡完成后停止生成");
            controller.RestartRun(); yield return null; yield return null; yield return new WaitForSeconds(0.3f);
            Check(controller.Director.Stage==1 && !controller.Director.TideStarted && !controller.Director.BossSpawned && controller.State.AliveEnemies<100,"重开清空导演与敌人运行态");
            dead=manager.GetComponentData<AbilityUnit>(controller.SessionEntity); dead.Health=0; manager.SetComponentData(controller.SessionEntity,dead);
            yield return null; yield return null;
            Check(controller.Director.Outcome==CombatOutcome.Defeat,"英雄死亡记录失败");
            total=controller.Director.SpawnedTotal; yield return new WaitForSeconds(0.3f);
            Check(controller.Director.SpawnedTotal==total,"失败后停止补充");
        }

        private IEnumerator Stress()
        {
            int requested=int.Parse(Arg("--stress-count","4000"),CultureInfo.InvariantCulture);
            requested=Mathf.Clamp(requested,100,4000);
            float duration=float.Parse(Arg("--stress-seconds","60"),CultureInfo.InvariantCulture);
            duration=Mathf.Clamp(duration,5,180);
            var settings=manager.GetComponentData<SpawnDirectorSettings>(controller.SessionEntity);
            settings.BossPopulationFraction=1;
            if(Has("--no-separation")) settings.SeparationSpeed=0;
            manager.SetComponentData(controller.SessionEntity,settings);
            var curve=manager.GetBuffer<PopulationPoint>(controller.SessionEntity); curve.Clear();
            curve.Add(new PopulationPoint{Minute=0,Alive=requested}); curve.Add(new PopulationPoint{Minute=100,Alive=requested});
            var state=CombatDirectorState.Begin(settings,3,20261024); state.StageElapsed=180; state.BossSpawned=true; state.TideStarted=true;
            manager.SetComponentData(controller.SessionEntity,state);
            // 先让真正的导演分批填充，再把初始群体放进镜头以验证渲染负载；采样中不再搬运怪物。
            float deadline=Time.realtimeSinceStartup+45;
            while(enemies.CalculateEntityCount()<requested-20 && Time.realtimeSinceStartup<deadline) { KeepHero(); yield return null; }
            Check(enemies.CalculateEntityCount()>=requested-20,"导演填充目标压力负载");
            using var rendering=new CombatProbeRender(controller.gameCamera,true);
            using(var units=enemies.ToEntityArray(Allocator.Temp))
            {
                int side=Mathf.CeilToInt(Mathf.Sqrt(units.Length));
                for(int i=0;i<units.Length;i++)
                {
                    var t=manager.GetComponentData<LocalTransform>(units[i]);
                    t.Position.x=(i%side-side/2f)*1.1f; t.Position.z=(i/side-side/2f)*1.1f;
                    manager.SetComponentData(units[i],t);
                }
            }
            var slots=manager.GetBuffer<AbilitySlot>(controller.SessionEntity);
            for(int i=0;i<slots.Length;i++) { var slot=slots[i]; slot.Level=i==3?3:4; slots[i]=slot; }
            float warm=Time.realtimeSinceStartup+10;
            while(Time.realtimeSinceStartup<warm) { DriveStress(); yield return renderPhase; rendering.Render(); }
            Check(rendering.Save(Path.Combine(output,"combat-4000.png")),"压力帧包含实际场景和界面像素");
            var frames=new List<float>(20000); var simulations=new List<float>(20000); var gpu=new List<float>(20000);
            var csv=new StringBuilder("frame_ms,simulation_ms,gpu_ms,alive,visible_center,projectiles,gc_bytes\n",2000000);
            long previousAllocation=GC.GetAllocatedBytesForCurrentThread();
            var timings=new FrameTiming[1]; var planes=new Plane[6];
            var simulation=World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CombatSimulationSystem>();
            int minAlive=int.MaxValue,maxAlive=0,minVisible=int.MaxValue,maxVisible=0,visible=0,peakProjectiles=0;
            long maxGc=0,totalGc=0,peakMemory=0; int initialKills=controller.State.Kills; float nextVisibility=0;
            double start=Time.realtimeSinceStartupAsDouble,previousFrame=start;
            while(Time.realtimeSinceStartupAsDouble-start<duration)
            {
                DriveStress(); FrameTimingManager.CaptureFrameTimings();
                yield return renderPhase;
                rendering.Render();
                double completed=Time.realtimeSinceStartupAsDouble;
                float frame=(float)((completed-previousFrame)*1000),sim=(float)simulation.LastSimulationMilliseconds; previousFrame=completed;
                float gpuMs=FrameTimingManager.GetLatestTimings(1,timings)>0?(float)timings[0].gpuFrameTime:0;
                frames.Add(frame); simulations.Add(sim); if(gpuMs>0) gpu.Add(gpuMs);
                int alive=enemies.CalculateEntityCount(); minAlive=Math.Min(minAlive,alive); maxAlive=Math.Max(maxAlive,alive);
                if(Time.realtimeSinceStartup>=nextVisibility)
                {
                    nextVisibility=Time.realtimeSinceStartup+0.5f; visible=0;
                    GeometryUtility.CalculateFrustumPlanes(rendering.Camera,planes);
                    using var positions=enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    foreach(var t in positions)
                        if(GeometryUtility.TestPlanesAABB(planes,new Bounds(t.Position,Vector3.one*t.Scale))) visible++;
                    minVisible=Math.Min(minVisible,visible); maxVisible=Math.Max(maxVisible,visible);
                    peakMemory=Math.Max(peakMemory,Profiler.GetTotalAllocatedMemoryLong());
                }
                int projectiles=controller.State.ActiveProjectiles; peakProjectiles=Math.Max(peakProjectiles,projectiles);
                long allocated=GC.GetAllocatedBytesForCurrentThread(); long bytes=allocated-previousAllocation; previousAllocation=allocated; maxGc=Math.Max(maxGc,bytes); totalGc+=bytes;
                csv.AppendFormat(CultureInfo.InvariantCulture,"{0:F3},{1:F3},{2:F3},{3},{4},{5},{6}\n",frame,sim,gpuMs,alive,visible,projectiles,bytes);
            }
            int kills=controller.State.Kills-initialKills;
            Check(maxAlive<=4000 && maxAlive>=requested-20 && minAlive>=requested*0.8f,"采样期间存活数量保持在目标附近且不超硬上限");
            Check(kills>0 && controller.State.Hits>0 && peakProjectiles>0,"压力期间实际执行攻击、技能、弹体、击杀与补怪");
            var report=new StressReport {unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                os=SystemInfo.operatingSystem,development=Debug.isDebugBuild,width=Screen.width,height=Screen.height,requested=requested,separation=settings.SeparationSpeed>0,
                samples=frames.Count,seconds=duration,minAlive=minAlive,maxAlive=maxAlive,minVisible=minVisible,maxVisible=maxVisible,peakProjectiles=peakProjectiles,kills=kills,
                frameP50=Percentile(frames,.5f),frameP95=Percentile(frames,.95f),frameP99=Percentile(frames,.99f),simP95=Percentile(simulations,.95f),gpuP95=gpu.Count>0?Percentile(gpu,.95f):0,
                gcRecorderValid=maxGc>0,gcMean=maxGc>0?totalGc/Math.Max(1,frames.Count):-1,gcMax=maxGc>0?maxGc:-1,peakUnityBytes=peakMemory,
                load="现有 Game 战斗、模型、导演、敌人攻击、循环英雄技能；补英雄生命/法力/冷却。游戏镜头维持原设置，另用正交尺寸 160 的全场相机逐帧提交 1920x1080 URP 离屏渲染并等待 GPU 单像素读回；帧墙钟包含该同步开销，不是前台呈现帧率。初始群体一次性铺设，之后自由模拟。可见数为全场相机视锥与包围盒相交，非 GPU 遮挡。GC 是主线程分配，含探针记录；计数始终为零时视为计数器不支持，汇总记 -1，不代表零分配。未实现的远征机制不计入。"};
            File.WriteAllText(Path.Combine(output,"stress.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"frames.csv"),csv.ToString());
            Debug.Log($"COMBAT_STRESS_DONE samples={frames.Count} alive={minAlive}-{maxAlive} visible={minVisible}-{maxVisible} frameP95={report.frameP95:F2} simP95={report.simP95:F2} kills={kills}");
        }
        private float nextCast;
        private void DriveStress()
        {
            KeepHero(); float t=Time.realtimeSinceStartup;
            controller.SetDestination(new Vector3(Mathf.Cos(t*0.3f)*10,0,Mathf.Sin(t*0.3f)*10));
            if(t<nextCast) return; nextCast=t+0.7f;
            var runtime=manager.GetComponentData<AbilityRuntime>(controller.SessionEntity);
            if(AbilityCastTiming.IsBusy(runtime,runtime.Time)) return;
            var slots=manager.GetBuffer<AbilitySlot>(controller.SessionEntity); int slotIndex=((int)(t/0.7f))%slots.Length;
            var slot=slots[slotIndex]; slot.ReadyAt=0; slot.Charges=2; slot.HitStacks=100; slots[slotIndex]=slot;
            float3 center=controller.State.Position;
            manager.GetBuffer<SkillCommand>(controller.SessionEntity).Add(new SkillCommand{Slot=slotIndex,Point=center+new float3(6,0,0),Direction=new float3(1,0,0)});
        }
        private static float Percentile(List<float> values,float q) { values.Sort(); return values[Mathf.Clamp(Mathf.CeilToInt(values.Count*q)-1,0,values.Count-1)]; }
        private IEnumerator Capture(string name)
        {
            yield return renderPhase;
            using var rendering=new CombatProbeRender(controller.gameCamera,false);
            Check(rendering.Save(Path.Combine(output,name)),"实际 URP 画面："+name);
        }
        private void OnDestroy() { if(hasQuery && World.DefaultGameObjectInjectionWorld!=null && World.DefaultGameObjectInjectionWorld.IsCreated) enemies.Dispose(); }
        [Serializable] private class Result { public bool passed; public int checks; public string[] failures; }
        [Serializable] private class StressReport
        {
            public string unity,cpu,gpu,api,os,load;
            public bool development,separation,gcRecorderValid;
            public int width,height,requested,samples,minAlive,maxAlive,minVisible,maxVisible,peakProjectiles,kills;
            public float seconds,frameP50,frameP95,frameP99,simP95,gpuP95;
            public long gcMean,gcMax,peakUnityBytes;
        }
    }
}
