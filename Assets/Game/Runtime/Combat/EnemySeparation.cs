using System;
using Roguelike.Abilities;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>模拟系统拥有的可复用空间网格。先收集移动后的位置，再并行读取快照做软分离，避免并发读写邻居 Transform。</summary>
    public sealed class EnemySeparation : IDisposable
    {
        public struct Body { public Entity Entity; public float2 Position; public float Radius; }
        private NativeParallelMultiHashMap<int2,Body> grid=new(4096,Allocator.Persistent);
        public void Prepare(int count)
        {
            // 调用方在上帧 Job 完成后进入；不在任务中扩容。
            grid.Clear();
            if(grid.Capacity<count) grid.Capacity=math.ceilpow2(count);
        }
        public JobHandle Schedule(EntityManager manager,EntityQuery query,in SpawnDirectorSettings settings,float delta,float arena,JobHandle dependency)
        {
            if(settings.SeparationSpeed<=0) return dependency;
            var collect=new Collect {Cell=settings.SeparationCell,Grid=grid.AsParallelWriter(),
                Entities=manager.GetEntityTypeHandle(),Units=manager.GetComponentTypeHandle<AbilityUnit>(true),Transforms=manager.GetComponentTypeHandle<LocalTransform>(true)}.ScheduleParallel(query,dependency);
            return new Separate {Cell=settings.SeparationCell,Step=settings.SeparationSpeed*delta,Limit=math.max(1,arena-2),Checks=settings.SeparationChecks,Grid=grid,
                Entities=manager.GetEntityTypeHandle(),Units=manager.GetComponentTypeHandle<AbilityUnit>(true),Transforms=manager.GetComponentTypeHandle<LocalTransform>(false),
                Enemies=manager.GetComponentTypeHandle<CombatEnemy>(true)}
                .ScheduleParallel(query,collect);
        }
        public void Dispose() { if(grid.IsCreated) grid.Dispose(); }
        public static float Radius(float scale,float cell)=>math.clamp(scale*0.4f,0.15f,cell*0.5f);

        /// <summary>完全同点也有反对称的分离方向，不依赖共享随机状态。</summary>
        public static float2 PairPush(Entity self,Entity other,float2 delta,float radius)
        {
            float distance=math.length(delta);
            if(distance>=radius) return float2.zero;
            if(distance>0.0001f) return delta/distance*(1-distance/radius);
            uint hash=math.hash(new uint2((uint)math.min(self.Index,other.Index),(uint)math.max(self.Index,other.Index)));
            float angle=(hash%65536)*(2*math.PI/65536);
            return new float2(math.cos(angle),math.sin(angle))*(self.Index<other.Index?1:-1);
        }
        [BurstCompile]
        private struct Collect : IJobChunk
        {
            public float Cell;
            public NativeParallelMultiHashMap<int2,Body>.ParallelWriter Grid;
            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<AbilityUnit> Units;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> Transforms;
            public void Execute(in ArchetypeChunk chunk,int unfilteredChunkIndex,bool useEnabledMask,in v128 chunkEnabledMask)
            {
                var entities=chunk.GetNativeArray(Entities); var units=chunk.GetNativeArray(ref Units); var transforms=chunk.GetNativeArray(ref Transforms);
                var enumerator=new ChunkEntityEnumerator(useEnabledMask,chunkEnabledMask,chunk.Count);
                while(enumerator.NextEntityIndex(out int i)) CollectBody(entities[i],units[i],transforms[i]);
            }
            private void CollectBody(Entity entity,in AbilityUnit unit,in LocalTransform transform)
            {
                if(unit.Health<=0) return;
                float2 p=transform.Position.xz;
                Grid.Add((int2)math.floor(p/Cell),new Body{Entity=entity,Position=p,Radius=Radius(transform.Scale,Cell)});
            }
        }
        [BurstCompile]
        private struct Separate : IJobChunk
        {
            [ReadOnly] public NativeParallelMultiHashMap<int2,Body> Grid;
            public float Cell,Step,Limit; public int Checks;
            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<AbilityUnit> Units;
            [ReadOnly] public ComponentTypeHandle<CombatEnemy> Enemies;
            public ComponentTypeHandle<LocalTransform> Transforms;
            public void Execute(in ArchetypeChunk chunk,int unfilteredChunkIndex,bool useEnabledMask,in v128 chunkEnabledMask)
            {
                var entities=chunk.GetNativeArray(Entities); var units=chunk.GetNativeArray(ref Units);
                var enemies=chunk.GetNativeArray(ref Enemies); var transforms=chunk.GetNativeArray(ref Transforms);
                var enumerator=new ChunkEntityEnumerator(useEnabledMask,chunkEnabledMask,chunk.Count);
                while(enumerator.NextEntityIndex(out int i))
                {
                    var transform=transforms[i]; SeparateBody(entities[i],ref transform,enemies[i],units[i]); transforms[i]=transform;
                }
            }
            private void SeparateBody(Entity entity,ref LocalTransform transform,in CombatEnemy enemy,in AbilityUnit unit)
            {
                if(unit.Health<=0 || (unit.Controls&ControlFlags.MovementBlocked)!=0) return;
                float2 origin=transform.Position.xz,push=float2.zero;
                int2 cell=(int2)math.floor(origin/Cell);
                float radius=Radius(transform.Scale,Cell); int checkedCount=0;
                // 从自身格开始，并轮换邻格顺序，避免固定方向总先耗尽预算。最多 9 格，每格最多 Checks 次，共享总预算。
                int start=(entity.Index%8);
                for(int k=0;k<9 && checkedCount<Checks;k++)
                {
                    int2 offset=int2.zero;
                    if(k>0)
                    {
                        int n=(start+k-1)%8;
                        offset=n<3?new int2(n-1,-1):n<5?new int2(n==3?-1:1,0):new int2(n-6,1);
                    }
                    if(!Grid.TryGetFirstValue(cell+offset,out var body,out var iterator)) continue;
                    do
                    {
                        checkedCount++;
                        if(body.Entity!=entity) push+=PairPush(entity,body.Entity,origin-body.Position,radius+body.Radius);
                    } while(checkedCount<Checks && Grid.TryGetNextValue(out body,ref iterator));
                }
                float magnitude=math.length(push);
                if(magnitude>0) origin+=push/math.max(1,magnitude)*Step;
                origin=math.clamp(origin,-Limit,Limit);
                transform.Position.x=origin.x; transform.Position.z=origin.y;
            }
        }
    }
}
