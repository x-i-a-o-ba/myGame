using System;
using Unity.Collections;
using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>消费完整视野快照，负责纹理、标记与安全的 HUD 展示；拥有并释放自己创建的显示资源。</summary>
    public sealed class FogDemoPresentation : IDisposable
    {
        private readonly FogDemoSession session;
        private NativeArray<Color32> pixels,mapPixels;
        private readonly Texture2D fogTexture,minimapTexture;
        private FogSolver Solver => session.Solver;
        private FogTargetBatch Batch => session.Batch;
        private FogDemoHud hud => session.hud;
        private int Observer => session.Observer;
        private bool Omniscient => session.Omniscient;
        private bool night => session.night;
        private int SourceCount => session.SourceCount;
        private int targetCount => session.targetCount;
        private float refreshRate => session.refreshRate;
        private double SolveMs => session.SolveMs;
        private double QueryMs => session.QueryMs;
        private int Index(Vector3 point) => session.Index(point);
        public FogDemoPresentation(FogDemoSession session)
        {
            this.session=session;
            int size=session.gridSize;
            pixels=new NativeArray<Color32>(size*size,Allocator.Persistent);
            mapPixels=new NativeArray<Color32>(size*size,Allocator.Persistent);
            fogTexture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            minimapTexture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            hud.minimap.texture=minimapTexture;
            Shader.SetGlobalTexture("_DemoFogMap",fogTexture);
            Shader.SetGlobalVector("_DemoFogBounds",new Vector4(session.Minimum.x,session.Minimum.y,size*session.cellSize,size*session.cellSize));
        }
        public void Render(VisionSource[] sources,Renderer[][] sourceRenderers,FogObstacle[] trees,int selected,int placement)
        {
            uint bit=1u<<Observer;
            for(int i=0;i<pixels.Length;i++)
            {
                bool visible=Omniscient || (Solver.Visible[i]&bit)!=0;
                bool known=Omniscient || (Solver.Explored[i]&bit)!=0;
                pixels[i]=new Color32(visible?(byte)255:(byte)0,known?(byte)255:(byte)0,0,255);
                byte shade=visible?(byte)100:known?(byte)32:(byte)3;
                mapPixels[i]=new Color32(shade,shade,shade,255);
            }
            for(int i=0;i<Batch.Targets.Length;i++)
            {
                if(!Omniscient && (Batch.Visibility[i]&bit)==0) continue;
                int index=Index(Batch.Targets[i].position);
                if(index>=0) mapPixels[index]=Batch.Targets[i].team==0?new Color32(50,200,255,255):new Color32(255,80,60,255);
            }
            for(int i=0;i<sources.Length;i++)
            {
                var source=sources[i]; if(!source) continue;
                int index=Index(source.transform.position);
                bool alive=(source!=session.detector || source.providesVision) && source.isActiveAndEnabled && (source.expiresAt==0 || Time.time<source.expiresAt);
                bool visible=alive && (Omniscient || source.team==Observer || Solver.CanSee(index,Observer,source.invisible));
                foreach(var renderer in sourceRenderers[i]) if(renderer) renderer.enabled=visible;
                if(visible && index>=0) mapPixels[index]=source.team==0?new Color32(30,230,255,255):new Color32(255,60,40,255);
            }
            foreach(var tree in trees)
            {
                int index=Index(tree.transform.position);
                if(index<0) continue;
                if((Solver.Visible[index]&1)!=0) tree.rememberedBlue=tree.blocking;
                if((Solver.Visible[index]&2)!=0) tree.rememberedRed=tree.blocking;
                tree.visual.enabled=Omniscient?tree.blocking:((Solver.Explored[index]&bit)!=0 && (Observer==0?tree.rememberedBlue:tree.rememberedRed));
            }
            fogTexture.SetPixelData(pixels,0); fogTexture.Apply(false);
            minimapTexture.SetPixelData(mapPixels,0); minimapTexture.Apply(false);
            Shader.SetGlobalFloat("_DemoFogOmniscient",Omniscient?1:0);
            hud.status.text=$"{(Omniscient?"调试全知模式":"阵营视角")} · {(Observer==0?"蓝方":"红方")} · {(night?"夜晚":"白天")}\n来源 {SourceCount}  /  显隐目标 {targetCount:N0}（调试负载）\n网格+来源 {SolveMs:F2}ms  查询 {QueryMs:F2}ms\n右键移动本阵营观察者；左键选择可见目标\nWASD 平移 / 滚轮缩放 · 每 {1000/refreshRate:F0}ms 更新";
            if(placement>=0) hud.status.text+="\n点击地面放置眼 / Esc 取消";
            RefreshSelection(selected);
        }
        public void RefreshSelection(int selected)
        {
            if(selected<0) { hud.selection.text="未选择目标（隐藏目标不能被点击选中）"; return; }
            if((Batch.Visibility[selected]&(1<<Observer))!=0) hud.selection.text=$"可见目标 #{selected}  位置 {Batch.Targets[selected].position.x:F1}, {Batch.Targets[selected].position.z:F1}";
            else
            {
                var memory=Batch.Memory[selected*2+Observer];
                hud.selection.text=memory.known?$"目标已消失；最后可见 {memory.position.x:F1}, {memory.position.z:F1}\n这是旧信息，时间 {memory.time:F1}s":"未知目标";
            }
        }
        public void Dispose()
        {
            if(pixels.IsCreated) pixels.Dispose();
            if(mapPixels.IsCreated) mapPixels.Dispose();
            if(hud && hud.minimap) hud.minimap.texture=null;
            UnityEngine.Object.Destroy(fogTexture);
            UnityEngine.Object.Destroy(minimapTexture);
            Shader.SetGlobalTexture("_DemoFogMap",Texture2D.whiteTexture);
            Shader.SetGlobalFloat("_DemoFogOmniscient",0);
        }
    }
}
