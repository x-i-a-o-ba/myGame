using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Abilities
{
    [BurstCompile]
    public partial struct BuffAndMovementJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<AbilityLibrary> Library;
        public float Now,Delta; public float3 HeroPosition; public bool MoveEnemies;
        private void Execute(ref AbilityUnit unit,ref LocalTransform transform,DynamicBuffer<ActiveBuff> buffs)
        {
            AbilityEffects.Tick(ref unit,buffs,ref Library.Value,Now,Delta);
            if(unit.Health<=0) { transform.Scale=0; return; }
            if(unit.Kind==1)
            {
                if(!MoveEnemies) return;
                unit.Destination=HeroPosition;
                if(math.distancesq(transform.Position.xz,HeroPosition.xz)<9) return;
            }
            if(unit.Kind==2 && math.distancesq(transform.Position.xz,unit.Destination.xz)<0.3f)
                unit.Destination.x=-unit.Destination.x;
            float3 difference=unit.Destination-transform.Position; difference.y=0;
            float distance=math.length(difference);
            transform.Position+=math.normalizesafe(difference)*math.min(distance,unit.Speed*Delta);
        }
    }
    [BurstCompile]
    public partial struct BuildAbilityGridJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2,SpatialEntry>.ParallelWriter Grid;
        public const float CellSize=8;
        private void Execute(Entity entity,in AbilityUnit unit,in LocalTransform transform)
        {
            if(unit.Health<=0) return;
            Grid.Add((int2)math.floor(transform.Position.xz/CellSize),new SpatialEntry{Entity=entity,Position=transform.Position,Team=unit.Team});
        }
    }
    /// <summary>空间粗筛 + 圆形精确查询；只产生效果请求，不并发修改目标属性。</summary>
    [BurstCompile]
    public struct QueryAbilityAreasJob : IJob
    {
        [ReadOnly] public NativeParallelMultiHashMap<int2,SpatialEntry> Grid;
        [ReadOnly] public NativeArray<AreaPulse> Pulses;
        [ReadOnly] public BlobAssetReference<AbilityLibrary> Library;
        public NativeList<EffectRequest> Results;
        public void Execute()
        {
            for(int p=0;p<Pulses.Length;p++)
            {
                var pulse=Pulses[p]; ref var skill=ref Library.Value.Skills[pulse.Skill];
                int2 lo=(int2)math.floor((pulse.Center.xz-pulse.Radius)/BuildAbilityGridJob.CellSize);
                int2 hi=(int2)math.floor((pulse.Center.xz+pulse.Radius)/BuildAbilityGridJob.CellSize);
                for(int y=lo.y;y<=hi.y;y++) for(int x=lo.x;x<=hi.x;x++)
                {
                    if(!Grid.TryGetFirstValue(new int2(x,y),out var entry,out var iterator)) continue;
                    do
                    {
                        bool team=skill.Targets==TargetRelation.Allies?entry.Team==pulse.Team:entry.Team!=pulse.Team;
                        if(!team || math.distancesq(entry.Position.xz,pulse.Center.xz)>pulse.Radius*pulse.Radius) continue;
                        for(int e=0;e<skill.Effects.Length;e++)
                            Results.Add(new EffectRequest{Source=pulse.Source,Target=entry.Entity,Origin=pulse.Skill,
                                Effect=skill.Effects[e],Power=pulse.Power,Order=Results.Length});
                    } while(Grid.TryGetNextValue(out entry,ref iterator));
                }
            }
        }
    }
    [BurstCompile]
    public struct ApplyAbilityEffectsJob : IJob
    {
        public NativeArray<EffectRequest> Requests;
        [ReadOnly] public BlobAssetReference<AbilityLibrary> Library;
        public ComponentLookup<AbilityUnit> Units;
        public BufferLookup<ActiveBuff> Buffs;
        public float Now;
        public void Execute()
        {
            // 单结算 Job 拥有所有目标写权限。按目标和原始事件次序排序，避免同一 Buff Buffer 的数据竞争。
            Requests.Sort();
            for(int i=0;i<Requests.Length;i++)
            {
                var r=Requests[i]; if(!Units.TryGetComponent(r.Target,out var target) || !Buffs.HasBuffer(r.Target)) continue;
                AbilityEffects.Apply(ref target,Buffs[r.Target],ref Library.Value,r,Now);
                Units[r.Target]=target;
            }
        }
    }
    [BurstCompile]
    public partial struct AbilitySummaryJob : IJobEntity
    {
        public NativeArray<int> Counts;
        private void Execute(in AbilityUnit unit,in DynamicBuffer<ActiveBuff> buffs)
        { if(unit.Team==1 && unit.Health>0) Counts[0]++; Counts[1]+=buffs.Length; }
    }
}
