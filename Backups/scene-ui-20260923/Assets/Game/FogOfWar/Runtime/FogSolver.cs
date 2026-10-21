using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Roguelike.FogOfWar
{
    public struct FogSourceData
    {
        public int2 cell;
        public int radius, detectionRadius, team;
        public bool flying;
    }

    /// <summary>会话拥有的原生网格。逻辑与显示解耦；重算并集避免移除来源擦掉重叠视野。</summary>
    public sealed class FogSolver : IDisposable
    {
        public readonly int Size;
        public NativeArray<byte> Heights, Blockers;
        public NativeArray<uint> Visible, Detection, Explored;
        private NativeArray<Scan> stack;
        public FogSolver(int size)
        {
            Size = size;
            Heights = new NativeArray<byte>(size*size, Allocator.Persistent);
            Blockers = new NativeArray<byte>(size*size, Allocator.Persistent);
            Visible = new NativeArray<uint>(size*size, Allocator.Persistent);
            Detection = new NativeArray<uint>(size*size, Allocator.Persistent);
            Explored = new NativeArray<uint>(size*size, Allocator.Persistent);
            // 每个阴影分支由扫描到的遮挡格触发；整图格数提供保守工作栈容量。
            stack = new NativeArray<Scan>(size*size*8, Allocator.Persistent);
        }
        public JobHandle Schedule(NativeArray<FogSourceData> sources) => new SolveJob {
            size=Size, heights=Heights, blockers=Blockers, visible=Visible,
            detection=Detection, explored=Explored, sources=sources, stack=stack
        }.Schedule();
        public bool CanSee(int index,int team,bool invisible)
        {
            if(index<0 || index>=Visible.Length) return false;
            uint bit=1u<<team;
            return (Visible[index]&bit)!=0 && (!invisible || (Detection[index]&bit)!=0);
        }
        public void Reset(bool terrainKnown)
        {
            for(int i=0;i<Visible.Length;i++) { Visible[i]=0; Detection[i]=0; Explored[i]=terrainKnown?3u:0u; }
        }
        public void Dispose()
        {
            Heights.Dispose(); Blockers.Dispose(); Visible.Dispose(); Detection.Dispose(); Explored.Dispose(); stack.Dispose();
        }
        private struct Scan { public int depth; public float start,end; }

        [BurstCompile]
        private struct SolveJob : IJob
        {
            public int size;
            [ReadOnly] public NativeArray<byte> heights,blockers;
            [ReadOnly] public NativeArray<FogSourceData> sources;
            public NativeArray<uint> visible,detection,explored;
            public NativeArray<Scan> stack;
            public void Execute()
            {
                for(int i=0;i<visible.Length;i++) { visible[i]=0; detection[i]=0; }
                for(int i=0;i<sources.Length;i++)
                {
                    var source=sources[i];
                    if(!Inside(source.cell.x,source.cell.y)) continue;
                    uint bit=1u<<source.team;
                    if(source.detectionRadius>0) Circle(source.cell,source.detectionRadius,bit,true);
                    if(source.radius<=0) continue;
                    if(source.flying) { Circle(source.cell,source.radius,bit,false); continue; }
                    visible[source.cell.y*size+source.cell.x]|=bit;
                    for(int octant=0;octant<8;octant++) Cast(source,bit,octant);
                }
                for(int i=0;i<visible.Length;i++) explored[i]|=visible[i];
            }
            private bool Inside(int x,int y) => x>=0 && y>=0 && x<size && y<size;
            private void Circle(int2 center,int radius,uint bit,bool detect)
            {
                for(int y=math.max(0,center.y-radius);y<=math.min(size-1,center.y+radius);y++)
                    for(int x=math.max(0,center.x-radius);x<=math.min(size-1,center.x+radius);x++)
                        if(math.lengthsq(new int2(x,y)-center)<=radius*radius)
                        { int n=y*size+x; if(detect) detection[n]|=bit; else visible[n]|=bit; }
            }
            private int2 Transform(int dx,int dy,int octant)
            {
                switch(octant) {
                    case 0:return new int2(dx,dy); case 1:return new int2(dy,dx);
                    case 2:return new int2(-dy,dx); case 3:return new int2(-dx,dy);
                    case 4:return new int2(-dx,-dy); case 5:return new int2(-dy,-dx);
                    case 6:return new int2(dy,-dx); default:return new int2(dx,-dy);
                }
            }
            private void Cast(FogSourceData source,uint bit,int octant)
            {
                int count=1; stack[0]=new Scan{depth=1,start=1,end=0};
                int eye=heights[source.cell.y*size+source.cell.x];
                while(count>0)
                {
                    var row=stack[--count]; float start=row.start, nextStart=start;
                    if(start<row.end) continue;
                    bool blocked=false;
                    for(int distance=row.depth;distance<=source.radius && !blocked;distance++)
                    {
                        int dy=-distance;
                        for(int dx=-distance;dx<=0;dx++)
                        {
                            float left=(dx-0.5f)/(dy+0.5f), right=(dx+0.5f)/(dy-0.5f);
                            if(start<right) continue;
                            if(row.end>left) break;
                            int2 p=source.cell+Transform(dx,dy,octant);
                            bool inside=Inside(p.x,p.y); int n=inside?p.y*size+p.x:0;
                            bool high=inside && heights[n]>eye;
                            bool opaque=!inside || high || blockers[n]!=0;
                            // 树的近侧格可见，但更高平台自身不因作为遮挡格被揭示。
                            if(inside && !high && dx*dx+dy*dy<=source.radius*source.radius) visible[n]|=bit;
                            if(blocked)
                            {
                                if(opaque) nextStart=right;
                                else { blocked=false; start=nextStart; }
                            }
                            else if(opaque && distance<source.radius)
                            {
                                blocked=true;
                                stack[count++]=new Scan{depth=distance+1,start=start,end=left};
                                nextStart=right;
                            }
                        }
                    }
                }
            }
        }
    }
}
