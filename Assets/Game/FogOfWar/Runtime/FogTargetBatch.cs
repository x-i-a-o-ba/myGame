using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.FogOfWar
{
    public struct FogTargetData { public float3 position; public int team; public bool invisible; }
    public struct FogTargetMemory { public float3 position; public float time; public bool known; }

    /// <summary>万级目标使用连续数据和 Burst 查询，不创建万级 GameObject 或视野来源。</summary>
    public sealed class FogTargetBatch : IDisposable
    {
        public NativeArray<FogTargetData> Targets;
        public NativeArray<byte> Visibility;
        public NativeArray<FogTargetMemory> Memory;
        private readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        private readonly Mesh mesh;
        private readonly Material blue,red;
        public int VisibleCount { get; private set; }
        public FogTargetBatch(int count,Mesh mesh,Material blue,Material red)
        {
            this.mesh=mesh; this.blue=blue; this.red=red;
            Targets=new NativeArray<FogTargetData>(count,Allocator.Persistent);
            Visibility=new NativeArray<byte>(count,Allocator.Persistent);
            Memory=new NativeArray<FogTargetMemory>(count*2,Allocator.Persistent);
        }
        public JobHandle Schedule(FogSolver solver,float cellSize,float2 minimum,float time)
            => new QueryJob { targets=Targets,results=Visibility,memory=Memory,visible=solver.Visible,detection=solver.Detection,
                size=solver.Size,cellSize=cellSize,minimum=minimum,time=time }.Schedule(Targets.Length,128);
        public void Draw(int observer,bool omniscient)
        {
            VisibleCount=0;
            for(int team=0;team<2;team++)
            {
                int n=0;
                for(int i=0;i<Targets.Length;i++)
                {
                    var target=Targets[i];
                    if(target.team!=team || (!omniscient && (Visibility[i]&(1<<observer))==0)) continue;
                    matrices[n++]=Matrix4x4.TRS(target.position,Quaternion.identity,Vector3.one*0.48f);
                    VisibleCount++;
                    if(n==matrices.Length) { Draw(n,team); n=0; }
                }
                if(n>0) Draw(n,team);
            }
        }
        private void Draw(int count,int team) => Graphics.DrawMeshInstanced(mesh,0,team==0?blue:red,matrices,count,null,UnityEngine.Rendering.ShadowCastingMode.Off,false);
        public int Pick(Vector3 point,int observer)
        {
            int selected=-1; float best=1.1f;
            for(int i=0;i<Targets.Length;i++)
            {
                if((Visibility[i]&(1<<observer))==0) continue;
                var d=(float3)point-Targets[i].position; d.y=0;
                float distance=math.lengthsq(d);
                if(distance<best) { best=distance; selected=i; }
            }
            return selected;
        }
        public void Dispose() { Targets.Dispose(); Visibility.Dispose(); Memory.Dispose(); }
        [BurstCompile]
        private struct QueryJob:IJobParallelFor
        {
            [ReadOnly] public NativeArray<FogTargetData> targets;
            [ReadOnly] public NativeArray<uint> visible,detection;
            public NativeArray<byte> results;
            // 每个索引只写自己对应的两阵营记录，写入区域不重叠。
            [NativeDisableParallelForRestriction] public NativeArray<FogTargetMemory> memory;
            public int size; public float cellSize,time; public float2 minimum;
            public void Execute(int i)
            {
                var t=targets[i]; int2 cell=(int2)math.floor((t.position.xz-minimum)/cellSize);
                bool inside=math.all(cell>=0)&&math.all(cell<size);
                int index=inside?cell.y*size+cell.x:0; byte mask=0;
                for(int team=0;team<2;team++)
                {
                    uint bit=1u<<team;
                    bool canSee=t.team==team || (inside && (visible[index]&bit)!=0 && (!t.invisible || (detection[index]&bit)!=0));
                    if(canSee) { mask|=(byte)bit; memory[i*2+team]=new FogTargetMemory{position=t.position,time=time,known=true}; }
                }
                results[i]=mask;
            }
        }
    }
}
