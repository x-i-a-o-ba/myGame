using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>独立演示的组合根：拥有原生资源，连接来源、查询、场景显示与 UI。</summary>
    public sealed class FogDemoSession:MonoBehaviour
    {
        [Header("地图/负载（启动时读取）")]
        [Range(32,256)] public int gridSize=128;
        [Min(0.5f)] public float cellSize=1;
        [Range(1,20000)] public int targetCount=10000;
        [Range(1,30)] public float refreshRate=10;
        [Tooltip("开启未探索黑雾；关闭时地图初始已知，但敌人仍受当前视野限制。")] public bool unexploredFog=true;
        [Header("场景引用")]
        public Camera gameCamera;
        public FogDemoHud hud;
        public VisionSource blueScout,redScout,detector;
        public VisionSource[] wardSlots;
        public Mesh targetMesh;
        public Material blueMaterial,redMaterial;
        [Header("昼夜")]
        public bool night,cycle;
        [Min(2)] public float phaseSeconds=30;
        public int Observer { get; private set; }
        public bool Omniscient { get; private set; }
        public FogSolver Solver { get; private set; }
        public FogTargetBatch Batch { get; private set; }
        public double SolveMs { get; private set; }
        public double QueryMs { get; private set; }
        public double UploadMs { get; private set; }
        public int SourceCount { get; private set; }
        private NativeList<FogSourceData> sourceData;
        private FogDemoPresentation presentation;
        private VisionSource[] sources;
        private Renderer[][] sourceRenderers;
        private FogObstacle[] trees;
        private FogHeightRegion[] regions;
        private float nextTick,cycleTime;
        private int selected=-1,placement=-1;
        private Vector3 blueStart,redStart;
        private bool[] treeInitial;
        private FogDemoInput input;
        private readonly Stopwatch timer=new Stopwatch();
        public float2 Minimum => new float2(-gridSize*cellSize*0.5f);

        private void Start()
        {
            RefreshSources();
            trees=GetComponentsInChildren<FogObstacle>(true);
            treeInitial=new bool[trees.Length];
            for(int i=0;i<trees.Length;i++) treeInitial[i]=trees[i].blocking;
            regions=GetComponentsInChildren<FogHeightRegion>(true);
            Solver=new FogSolver(gridSize);
            sourceData=new NativeList<FogSourceData>(sources.Length+16,Allocator.Persistent);
            Batch=new FogTargetBatch(targetCount,targetMesh,blueMaterial,redMaterial);
            presentation=new FogDemoPresentation(this);
            blueStart=blueScout.transform.position; redStart=redScout.transform.position;
            hud.Command+=Execute;
            input=new FogDemoInput(this);
            ResetSession();
        }
        /// <summary>运行时增删视野预制体后显式调用。仅重建低数量来源缓存，不遍历万级敌人；对象须位于本会话根节点下。</summary>
        public void RefreshSources()
        {
            sources=GetComponentsInChildren<VisionSource>(true);
            sourceRenderers=new Renderer[sources.Length][];
            for(int i=0;i<sources.Length;i++)
            {
                sourceRenderers[i]=sources[i].GetComponentsInChildren<Renderer>(true);
                if(sources[i].expiresAt==0 && sources[i].lifetime>0)
                    sources[i].expiresAt=Time.time+sources[i].lifetime;
            }
        }
        public int Index(Vector3 position)
        {
            int2 cell=(int2)math.floor((new float2(position.x,position.z)-Minimum)/cellSize);
            return math.all(cell>=0)&&math.all(cell<gridSize)?cell.y*gridSize+cell.x:-1;
        }
        private Vector3 Snap(Vector3 position)
        {
            float half=gridSize*cellSize*0.5f;
            position.x=Mathf.Clamp(position.x,-half+cellSize,half-cellSize);
            position.z=Mathf.Clamp(position.z,-half+cellSize,half-cellSize);
            int index=Index(position); position.y=Solver.Heights[index]*3;
            return position;
        }
        private void BuildMap()
        {
            for(int i=0;i<Solver.Heights.Length;i++)
            {
                var p=Minimum+new float2(i%gridSize+0.5f,i/gridSize+0.5f)*cellSize;
                byte height=0;
                foreach(var region in regions)
                    if(region.isActiveAndEnabled && Mathf.Abs(p.x-region.transform.position.x)<=region.size.x*0.5f && Mathf.Abs(p.y-region.transform.position.z)<=region.size.y*0.5f)
                        height=(byte)Mathf.Max(height,region.level);
                Solver.Heights[i]=height; Solver.Blockers[i]=0;
            }
            foreach(var tree in trees)
            {
                if(!tree.blocking) continue;
                var p=tree.transform.position; int2 center=(int2)math.floor((new float2(p.x,p.z)-Minimum)/cellSize);
                int radius=Mathf.CeilToInt(tree.radius/cellSize);
                for(int y=Mathf.Max(0,center.y-radius);y<=Mathf.Min(gridSize-1,center.y+radius);y++)
                    for(int x=Mathf.Max(0,center.x-radius);x<=Mathf.Min(gridSize-1,center.x+radius);x++)
                        if(math.lengthsq(new int2(x,y)-center)<=radius*radius) Solver.Blockers[y*gridSize+x]=1;
            }
        }
        public void ResetSession()
        {
            placement=-1; selected=-1; night=false; cycle=false; cycleTime=0; Observer=0; Omniscient=false;
            for(int i=0;i<trees.Length;i++) { trees[i].blocking=treeInitial[i]; trees[i].rememberedBlue=trees[i].rememberedRed=treeInitial[i]; }
            foreach(var slot in wardSlots) slot.gameObject.SetActive(false);
            blueScout.transform.position=blueStart; redScout.transform.position=redStart;
            blueScout.flying=redScout.flying=false; blueScout.providesVision=redScout.providesVision=true;
            detector.providesVision=false;
            BuildMap(); Solver.Reset(!unexploredFog);
            var random=new Unity.Mathematics.Random(90222);
            for(int i=0;i<Batch.Targets.Length;i++)
            {
                var p=new Vector3(random.NextFloat(-58,58),0,random.NextFloat(-58,58));
                if(i==0) p=new Vector3(-20,0,-8);
                p=Snap(p); p.y+=0.4f;
                Batch.Targets[i]=new FogTargetData{position=p,team=1,invisible=i==0};
                Batch.Memory[i*2]=Batch.Memory[i*2+1]=default;
            }
            foreach(var source in sources) if(source) source.expiresAt=source.lifetime>0?Time.time+source.lifetime:0;
            Tick();
        }
        public void Tick()
        {
            if(Solver==null) return;
            timer.Restart(); BuildMap(); sourceData.Clear();
            foreach(var source in sources)
            {
                if(!source || !source.isActiveAndEnabled || !source.providesVision || (source.expiresAt>0 && Time.time>=source.expiresAt)) continue;
                var p=source.transform.position;
                sourceData.Add(new FogSourceData{cell=(int2)math.floor((new float2(p.x,p.z)-Minimum)/cellSize),
                    radius=Mathf.Clamp(Mathf.FloorToInt((night?source.nightRadius:source.dayRadius)/cellSize),0,gridSize),
                    detectionRadius=Mathf.Clamp(Mathf.FloorToInt(source.trueSightRadius/cellSize),0,gridSize),
                    team=Mathf.Clamp(source.team,0,1),flying=source.flying});
            }
            SourceCount=sourceData.Length;
            Solver.Schedule(sourceData.AsArray()).Complete(); SolveMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart(); Batch.Schedule(Solver,cellSize,Minimum,Time.time).Complete(); QueryMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart(); Present(); UploadMs=timer.Elapsed.TotalMilliseconds;
            nextTick=Time.time+1/refreshRate;
        }
        /// <summary>性能探针复用实际求解/查询/上传路径；输入为批量来源，不实例化虚构的敌人对象。</summary>
        public void BenchmarkSnapshot(NativeArray<FogSourceData> batchSources)
        {
            SourceCount=batchSources.Length;
            timer.Restart(); BuildMap(); Solver.Schedule(batchSources).Complete(); SolveMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart(); Batch.Schedule(Solver,cellSize,Minimum,Time.time).Complete(); QueryMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart(); Present(); UploadMs=timer.Elapsed.TotalMilliseconds;
        }
        private void Present() => presentation.Render(sources,sourceRenderers,trees,selected,placement);
        public void Execute(FogCommand command)
        {
            var scout=Observer==0?blueScout:redScout;
            switch(command)
            {
                case FogCommand.Team: Observer=1-Observer; selected=-1; break;
                case FogCommand.Night: night=!night; cycle=false; break;
                case FogCommand.Cycle: cycle=!cycle; cycleTime=0; break;
                case FogCommand.Flying: scout.flying=!scout.flying; break;
                case FogCommand.EnemySource: redScout.providesVision=!redScout.providesVision; break;
                case FogCommand.TrueSight: detector.team=Observer; detector.transform.position=scout.transform.position; detector.providesVision=!detector.providesVision; break;
                case FogCommand.Invisible: var t=Batch.Targets[0]; t.invisible=!t.invisible; Batch.Targets[0]=t; break;
                case FogCommand.Trees: foreach(var tree in trees) tree.blocking=!tree.blocking; break;
                case FogCommand.ObserverWard: placement=0; break;
                case FogCommand.SentryWard: placement=1; break;
                case FogCommand.Pulse: placement=2; break;
                case FogCommand.RemoveWards: foreach(var slot in wardSlots) if(slot.team==Observer) slot.gameObject.SetActive(false); break;
                case FogCommand.Exploration: unexploredFog=!unexploredFog; Solver.Reset(!unexploredFog); break;
                case FogCommand.Reset: ResetSession(); return;
                case FogCommand.Overview: gameCamera.orthographicSize=gameCamera.orthographicSize<60?78:30; gameCamera.transform.position=new Vector3(0,90,-65); break;
                case FogCommand.Omniscient: Omniscient=!Omniscient; break;
            }
            Tick();
        }
        public void PlaceWard(Vector3 point,int type)
        {
            foreach(var slot in wardSlots)
            {
                if(slot.gameObject.activeSelf && (slot.expiresAt==0 || Time.time<slot.expiresAt)) continue;
                slot.gameObject.SetActive(true); slot.team=Observer; slot.providesVision=true; slot.invisible=type!=2;
                slot.transform.position=Snap(point); slot.dayRadius=slot.nightRadius=type==1?0:14;
                slot.trueSightRadius=type==1?14:0; slot.flying=type==2;
                slot.lifetime=type==2?3:30; slot.expiresAt=Time.time+slot.lifetime;
                placement=-1; Tick(); return;
            }
            hud.selection.text="演示眼槽已满，请清除本阵营眼。";
        }
        private void Update()
        {
            if(Solver==null) return;
            if(cycle) { cycleTime+=Time.deltaTime; if(cycleTime>=phaseSeconds) { cycleTime=0; night=!night; nextTick=0; } }
            input.Tick();
            if(Time.time>=nextTick) Tick();
            Batch.Draw(Observer,Omniscient);
        }
        public void CancelPlacement() => placement=-1;
        /// <summary>演示用瞬移；后续战斗适配器应更新真实来源位置，不在视野模块实现寻路。</summary>
        public void MoveObserver(Vector3 point)
        {
            (Observer==0?blueScout:redScout).transform.position=Snap(point); nextTick=0;
        }
        public void ClickGround(Vector3 point)
        {
            if(placement>=0) PlaceWard(point,placement);
            else { selected=Batch.Pick(point,Observer); presentation.RefreshSelection(selected); }
        }
        /// <summary>先命中最高可见的配置平台表面，再回落到地面，避免俯视相机点高地时产生水平偏移。</summary>
        public bool TryGroundPoint(Ray ray,out Vector3 point)
        {
            point=default;
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float nearest)) return false;
            point=ray.GetPoint(nearest);
            foreach(var region in regions)
            {
                if(!region.isActiveAndEnabled) continue;
                if(!new Plane(Vector3.up,Vector3.up*(region.level*3)).Raycast(ray,out float distance) || distance>=nearest) continue;
                var candidate=ray.GetPoint(distance);
                if(Mathf.Abs(candidate.x-region.transform.position.x)>region.size.x*0.5f || Mathf.Abs(candidate.z-region.transform.position.z)>region.size.y*0.5f) continue;
                point=candidate; nearest=distance;
            }
            point=Snap(point); return true;
        }
        private void OnDestroy()
        {
            if(hud) hud.Command-=Execute;
            Batch?.Dispose(); Solver?.Dispose();
            if(sourceData.IsCreated) sourceData.Dispose();
            presentation?.Dispose();
        }
    }
}
