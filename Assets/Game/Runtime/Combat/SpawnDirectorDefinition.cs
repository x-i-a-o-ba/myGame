using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    [Serializable]
    public struct PopulationKey
    {
        public float minute;
        public int alive;
        public PopulationKey(float minute,int alive) { this.minute=minute; this.alive=alive; }
    }
    /// <summary>导演配置不含局内进度。会话复制成 ECS 快照，暂停、重开和跨场景都不会修改资源。</summary>
    [CreateAssetMenu(menuName="Roguelike/战斗/刷怪导演")]
    public sealed class SpawnDirectorDefinition : ScriptableObject
    {
        public PopulationKey[] population={new(0,60),new(1,150),new(3,500),new(4.5f,800),new(6,1200),new(9,2200),new(12,3500),new(13.5f,4000)};
        [Min(0.05f)] public float tickSeconds=0.25f;
        [Range(1,4000)] public int aliveLimit=4000;
        [Min(1), Tooltip("每次补充最多生成的实体数，包含潮涌/精英/首领。未生成的潮涌留到后续批次。")] public int batchBudget=128;
        [Min(1)] public float stageSeconds=270;
        [Range(0,1)] public float openingFraction=0.4f;
        [Min(0)] public float recoverySeconds=60;
        [Min(0)] public float bossAt=210;
        [Range(0,1)] public float bossPopulationFraction=0.5f;
        [Min(0)] public float enrageAt=270;
        [Min(0)] public float tideAt=150;
        public Vector3Int tideCounts=new(300,800,1500);
        public Vector3 eliteFirstAt=new(90,30,20);
        public Vector3 eliteIntervals=new(60,30,20);
        [Min(1)] public float ringWidth=20;
        [Min(1), Tooltip("镜头地面投影外侧的留白（米），防止模型边缘突然入画。")] public float cameraPadding=3;
        [Range(0,1)] public float forwardBias=0.7f;
        [Min(0.5f)] public float separationCell=1.5f;
        [Min(0), Tooltip("软分离最大位移速度（米/秒）；0 可用于对照测量。")] public float separationSpeed=2.5f;
        [Range(8,128), Tooltip("每个单位每帧最多检查的邻居；限制重叠极密时的开销。")] public int separationChecks=48;

        public SpawnDirectorSettings Compile()
        {
            if(population==null || population.Length<2 || population.Length>32 || population[0].minute!=0)
                throw new InvalidOperationException("目标曲线须从 0 分钟开始，包含 2–32 个节点。");
            float previous=-1;
            foreach(var key in population)
            {
                if(!Finite(key.minute) || key.minute<=previous || key.alive<0 || key.alive>4000)
                    throw new InvalidOperationException("目标曲线时间必须递增，数量范围为 0–4000。");
                previous=key.minute;
            }
            float[] values={tickSeconds,stageSeconds,openingFraction,recoverySeconds,bossAt,bossPopulationFraction,enrageAt,tideAt,
                eliteFirstAt.x,eliteFirstAt.y,eliteFirstAt.z,eliteIntervals.x,eliteIntervals.y,eliteIntervals.z,
                ringWidth,cameraPadding,forwardBias,separationCell,separationSpeed};
            foreach(float value in values) if(!Finite(value) || value<0) throw new InvalidOperationException("导演参数必须是有限非负数。");
            if(tickSeconds<0.05f || stageSeconds<1 || aliveLimit<1 || aliveLimit>4000 || batchBudget<1 || batchBudget>aliveLimit ||
                openingFraction>1 || bossPopulationFraction>1 || forwardBias>1 || enrageAt<bossAt || tideAt>=bossAt ||
                eliteIntervals.x<tickSeconds || eliteIntervals.y<tickSeconds || eliteIntervals.z<tickSeconds ||
                tideCounts.x<0 || tideCounts.y<0 || tideCounts.z<0 || ringWidth<1 || cameraPadding<1 || separationCell<0.5f ||
                separationChecks<8 || separationChecks>128)
                throw new InvalidOperationException("导演节奏、数量、刷怪环或分离参数超出有效范围。");
            return new SpawnDirectorSettings {TickSeconds=tickSeconds,AliveLimit=aliveLimit,BatchBudget=batchBudget,StageSeconds=stageSeconds,
                OpeningFraction=openingFraction,RecoverySeconds=recoverySeconds,BossAt=bossAt,BossPopulationFraction=bossPopulationFraction,
                EnrageAt=enrageAt,TideAt=tideAt,TideCounts=new int3(tideCounts.x,tideCounts.y,tideCounts.z),EliteFirstAt=eliteFirstAt,
                EliteIntervals=eliteIntervals,RingWidth=ringWidth,CameraPadding=cameraPadding,ForwardBias=forwardBias,
                SeparationCell=separationCell,SeparationSpeed=separationSpeed,SeparationChecks=separationChecks};
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
    public struct SpawnDirectorSettings : IComponentData
    {
        public float TickSeconds,StageSeconds,OpeningFraction,RecoverySeconds,BossAt,BossPopulationFraction,EnrageAt,TideAt;
        public int AliveLimit,BatchBudget,SeparationChecks;
        public int3 TideCounts;
        public float3 EliteFirstAt,EliteIntervals;
        public float RingWidth,CameraPadding,ForwardBias,SeparationCell,SeparationSpeed;
    }
    [InternalBufferCapacity(8)]
    public struct PopulationPoint : IBufferElementData { public float Minute; public int Alive; }
    /// <summary>相机桥提供的地面可见矩形与实际移动方向；模拟不依赖 Camera 或 UI。</summary>
    public struct CombatSpawnView : IComponentData
    {
        public float2 Min,Max,Heading;
        public bool Valid;
    }
}
