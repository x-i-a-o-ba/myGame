using Roguelike.Simulation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Benchmark
{
    public struct BenchmarkAgent : IComponentData
    {
        public float3 Origin;
        public float Phase;
    }

    public struct BenchmarkOwned : IComponentData { }

    public struct BenchmarkSettings : IComponentData
    {
        public bool SyntheticDamage;
    }

    // This system only touches benchmark agents. It is not production enemy AI.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BenchmarkSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BenchmarkSettings>();
            state.RequireForUpdate<BenchmarkAgent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new MoveJob
            {
                Time = (float)SystemAPI.Time.ElapsedTime,
                DeltaTime = SystemAPI.Time.DeltaTime,
                SyntheticDamage = SystemAPI.GetSingleton<BenchmarkSettings>().SyntheticDamage
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct MoveJob : IJobEntity
        {
            public float Time;
            public float DeltaTime;
            public bool SyntheticDamage;

            private void Execute(ref LocalTransform transform, ref Health health,
                in BenchmarkAgent agent, in MoveSpeed speed)
            {
                float phase = agent.Phase + Time * speed.Value;
                transform.Position = agent.Origin + new float3(math.sin(phase), 0, math.cos(phase)) * 0.2f;
                if (SyntheticDamage)
                {
                    // Synthetic write load only: no targeting, hit testing, death, or drops.
                    health.Current -= DeltaTime * 10;
                    if (health.Current <= 0) health.Current = health.Maximum;
                }
            }
        }
    }
}
