using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Roguelike.Combat
{
    public struct EnemySpawnStats { public float Health,Damage,Speed,Experience,Gold,Scale; }

    /// <summary>纯节奏决策与批量生成边界。由战斗宿主在 Job 完成、死亡回收之后调用，不依赖英雄、输入或相机对象。</summary>
    public static class CombatSpawnDirector
    {
        public const float NormalScale=0.85f;
        public static float Minutes(in SpawnDirectorSettings settings,int stage,float elapsed)
            =>(settings.StageSeconds*math.max(0,stage-1)+math.max(0,elapsed))/60;

        public static float Population(NativeArray<PopulationPoint> curve,float minute)
        {
            if(curve.Length==0) return 0;
            for(int i=1;i<curve.Length;i++)
                if(minute<curve[i].Minute)
                    return math.lerp(curve[i-1].Alive,curve[i].Alive,math.saturate((minute-curve[i-1].Minute)/(curve[i].Minute-curve[i-1].Minute)));
            return curve[curve.Length-1].Alive;
        }
        public static int Target(in SpawnDirectorSettings settings,in CombatDirectorState state,NativeArray<PopulationPoint> curve)
        {
            float recovery=settings.RecoverySeconds<=0?1:math.lerp(settings.OpeningFraction,1,math.saturate(state.StageElapsed/settings.RecoverySeconds));
            float boss=state.BossSpawned?settings.BossPopulationFraction:1;
            return math.clamp((int)math.round(Population(curve,Minutes(settings,state.Stage,state.StageElapsed))*recovery*boss),0,settings.AliveLimit);
        }
        public static EnemySpawnStats Stats(in CombatDifficultySettings settings,in CombatSettings combat,float minute,int stage,EnemyTier tier)
        {
            // 超长单关不会指数溢出；只钳制强度时间，不提前结束战斗。
            minute=math.clamp(minute,0,60);
            var value=new EnemySpawnStats {Health=combat.EnemyHealth*math.pow(settings.HealthGrowth,minute),Damage=combat.EnemyAttackDamage*math.pow(settings.DamageGrowth,minute),
                Speed=math.min(settings.MaxSpeed,combat.EnemySpeed+settings.SpeedPerMinute*minute),Experience=settings.BaseExperience,Gold=settings.BaseGold,Scale=NormalScale};
            int index=math.clamp(stage-1,0,2);
            if(tier==EnemyTier.Elite)
            {
                value.Health*=settings.EliteHealth; value.Damage*=settings.EliteDamage; value.Scale*=settings.EliteScale;
                value.Experience=settings.EliteExperience; value.Gold=settings.EliteGold;
            }
            else if(tier==EnemyTier.Boss)
            {
                value.Health=settings.BossHealth[index]; value.Damage*=settings.BossDamage; value.Speed*=settings.BossSpeed;
                value.Scale*=settings.BossScale; value.Experience=settings.BossExperience[index]; value.Gold=settings.BossGold[index];
            }
            return value;
        }
        public static CombatEnemyKind LegacyKind(in CombatSettings combat)
            =>new CombatEnemyKind {Prefab=combat.EnemyPrefab,ConfigId=GameCombatController.EnemyConfigId,
                Tiers=EnemyTierMask.Normal|EnemyTierMask.Elite|EnemyTierMask.Boss,Weight=1,Health=1,Damage=1,Speed=1,Experience=1,Gold=1,Scale=1,
                Color=new float4(1),EliteColor=new float4(1),BossColor=new float4(1)};

        public static bool Allows(in CombatEnemyKind kind,EnemyTier tier,float seconds,bool special=false)
            =>kind.Prefab!=Entity.Null && kind.Weight>0 && (special?kind.SpecialElite:(kind.Tiers&EnemySpawnEntry.Mask(tier))!=0 && !kind.SpecialElite)
                && seconds>=kind.FirstSecond && (kind.LastSecond<=0 || seconds<=kind.LastSecond);

        /// <summary>无合格条目返回 -1，绝不退回未解锁或权重为零的怪物。专属精英单独选取。</summary>
        public static int Pick(NativeArray<CombatEnemyKind> kinds,EnemyTier tier,float seconds,ref Random random,bool special=false)
        {
            float total=0;
            for(int i=0;i<kinds.Length;i++) if(Allows(kinds[i],tier,seconds,special)) total+=kinds[i].Weight;
            if(total<=0) return -1;
            float roll=random.NextFloat(total); int last=-1;
            for(int i=0;i<kinds.Length;i++)
            {
                if(!Allows(kinds[i],tier,seconds,special)) continue;
                last=i; roll-=kinds[i].Weight; if(roll<0) return i;
            }
            return last;
        }

        /// <summary>射线到可见矩形外再取环带；靠墙时重抽方向，不把越界位置夹回镜头内。没有合法位置则本次不生成。</summary>
        public static bool TryPosition(in CombatSettings combat,in SpawnDirectorSettings settings,in CombatSpawnView view,float2 center,
            float2 heading,bool tide,ref Random random,out float2 position)
        {
            position=default;
            if(!view.Valid) return false;
            float limit=math.max(1,combat.ArenaHalfSize-3);
            float fallbackAngle=random.NextFloat(-math.PI,math.PI);
            for(int attempt=0;attempt<32;attempt++)
            {
                bool biased=math.lengthsq(heading)>0.01f && (tide || random.NextFloat()<settings.ForwardBias);
                // 潮涌遇到边界时允许换方向，避免永远卡住；正常地形时保持单向扇区。
                if(tide && attempt>=12) biased=false;
                float angle=biased?math.atan2(heading.y,heading.x)+random.NextFloat(-1,1)*(tide?0.35f:1.13f):random.NextFloat(-math.PI,math.PI);
                if(attempt>=16) angle=fallbackAngle+(attempt-16)*(2*math.PI/16);
                float2 direction=new float2(math.cos(angle),math.sin(angle));
                float tx=math.abs(direction.x)<0.00001f?float.MaxValue:((direction.x>0?view.Max.x:view.Min.x)-center.x)/direction.x;
                float tz=math.abs(direction.y)<0.00001f?float.MaxValue:((direction.y>0?view.Max.y:view.Min.y)-center.y)/direction.y;
                float exit=math.max(0,math.min(tx,tz));
                float inner=math.max(combat.SafeSpawnRadius,exit+settings.CameraPadding);
                float radius=math.sqrt(math.lerp(inner*inner,(inner+settings.RingWidth)*(inner+settings.RingWidth),random.NextFloat()));
                float2 candidate=center+direction*radius;
                if(math.any(math.abs(candidate)>limit)) continue;
                float2 paddedMin=view.Min-settings.CameraPadding,paddedMax=view.Max+settings.CameraPadding;
                if(math.all(candidate>=paddedMin) && math.all(candidate<=paddedMax)) continue;
                position=candidate; return true;
            }
            return false;
        }

        public static int Tick(EntityManager manager,in CombatSettings combat,in CombatDifficultySettings difficulty,in SpawnDirectorSettings settings,
            ref CombatDirectorState state,float3 center,int alive,NativeArray<CombatEnemyKind> kinds,NativeArray<PopulationPoint> curve,in CombatSpawnView view)
        {
            if(state.Outcome!=CombatOutcome.Running || state.StageElapsed<state.NextTickAt || !view.Valid) return 0;
            state.NextTickAt=state.StageElapsed+settings.TickSeconds; // 不因卡顿补跑多个批次。
            state.LastBatchSpawned=0;
            int cap=math.min(combat.MaxAlive,settings.AliveLimit),index=math.clamp(state.Stage-1,0,2);
            float minute=Minutes(settings,state.Stage,state.StageElapsed);
            var random=Random.CreateFromIndex(state.Seed++);
            if(!state.TideStarted && state.StageElapsed>=settings.TideAt)
            {
                state.TideStarted=true; state.PendingTide=settings.TideCounts[index]; state.TideNoticeUntil=state.StageElapsed+5;
                float angle=random.NextFloat(-math.PI,math.PI); state.TideDirection=new float2(math.cos(angle),math.sin(angle));
            }
            int room=math.min(settings.BatchBudget,math.max(0,cap-alive));
            if(!state.BossSpawned && state.StageElapsed>=settings.BossAt && room>0)
            {
                int kind=Pick(kinds,EnemyTier.Boss,minute*60,ref random,state.Stage==1);
                if(kind<0) kind=Pick(kinds,EnemyTier.Boss,minute*60,ref random);
                int n=Spawn(manager,combat,difficulty,settings,view,ref state,minute,EnemyTier.Boss,kind,kinds,1,center,view.Heading,false,ref random);
                if(n>0) { state.BossSpawned=true; room-=n; alive+=n; }
            }
            // 首领预留 1 格，首领也不突破上限；普通补充按目标减存活，潮涌是额外一次性预算。
            if(!state.BossSpawned) room=math.min(room,math.max(0,cap-1-alive));
            if(!state.BossSpawned && state.StageElapsed<settings.BossAt && state.StageElapsed>=state.NextEliteAt && room>0)
            {
                bool special=(state.ElitesSpawned+1)%3==0;
                int kind=Pick(kinds,EnemyTier.Elite,minute*60,ref random,special);
                if(kind<0 && special) kind=Pick(kinds,EnemyTier.Elite,minute*60,ref random);
                int n=Spawn(manager,combat,difficulty,settings,view,ref state,minute,EnemyTier.Elite,kind,kinds,math.min(room,math.max(1,combat.EliteCount)),center,view.Heading,false,ref random);
                if(n>0) { state.ElitesSpawned++; state.NextEliteAt=state.StageElapsed+settings.EliteIntervals[index]; room-=n; alive+=n; }
            }
            state.TargetAlive=Target(settings,state,curve);
            // 首领阶段取消未兑现的潮涌，防止与 50% 补充规则冲突。
            if(state.BossSpawned) state.PendingTide=0;
            int requested=state.PendingTide>0?math.min(room,state.PendingTide):math.min(room,math.max(0,state.TargetAlive-alive));
            using(var countStorage=new NativeArray<int>(kinds.Length,Allocator.Temp))
            {
                var counts=countStorage;
                for(int i=0;i<requested;i++) { int kind=Pick(kinds,EnemyTier.Normal,minute*60,ref random); if(kind>=0) counts[kind]++; }
                bool tide=state.PendingTide>0;
                for(int i=0;i<kinds.Length;i++)
                {
                    int n=Spawn(manager,combat,difficulty,settings,view,ref state,minute,EnemyTier.Normal,i,kinds,counts[i],center,
                        tide?state.TideDirection:view.Heading,tide,ref random);
                    if(tide) state.PendingTide-=n;
                }
            }
            state.SpawnedTotal+=state.LastBatchSpawned;
            return state.LastBatchSpawned;
        }

        private static int Spawn(EntityManager manager,in CombatSettings combat,in CombatDifficultySettings difficulty,in SpawnDirectorSettings settings,
            in CombatSpawnView view,ref CombatDirectorState state,float minute,EnemyTier tier,int kindIndex,NativeArray<CombatEnemyKind> kinds,int count,
            float3 center,float2 heading,bool tide,ref Random random)
        {
            if(count<=0 || kindIndex<0) return 0;
            var kind=kinds[kindIndex]; var stats=Stats(difficulty,combat,minute,state.Stage,tier);
            using var positionStorage=new NativeArray<float2>(count,Allocator.Temp);
            var positions=positionStorage;
            int accepted=0;
            for(int i=0;i<count;i++)
                if(TryPosition(combat,settings,view,center.xz,heading,tide,ref random,out var point)) positions[accepted++]=point;
                else state.PositionRejected++;
            if(accepted==0) return 0;
            using var units=new NativeArray<Entity>(accepted,Allocator.Temp);
            manager.Instantiate(kind.Prefab,units);
            for(int i=0;i<accepted;i++)
            {
                Initialize(manager,units[i],combat,kind,kindIndex,stats,tier,state.Stage,positions[i],center,ref random);
                if(tier==EnemyTier.Boss) state.Boss=units[i];
            }
            state.LastBatchSpawned+=accepted;
            return accepted;
        }
        /// <summary>导演与召唤共用初始化：只覆写实例，不更改原型或资源。召唤方额外设置无递归分裂标记。</summary>
        public static void Initialize(EntityManager manager,Entity entity,in CombatSettings combat,in CombatEnemyKind kind,int kindIndex,
            in EnemySpawnStats stats,EnemyTier tier,int stage,float2 point,float3 facingPoint,ref Random random)
        {
            float scale=stats.Scale*kind.Scale,health=math.max(1,stats.Health*(tier==EnemyTier.Boss?1:kind.Health)*(combat.HealthMultiplier>0?combat.HealthMultiplier:1));
            float4 color=tier==EnemyTier.Boss?kind.BossColor:tier==EnemyTier.Elite?kind.EliteColor:kind.Color;
            float3 position=new float3(point.x,.125f+scale*.5f,point.y),facing=facingPoint-position;facing.y=0;
            manager.SetComponentData(entity,LocalTransform.FromPositionRotationScale(position,quaternion.LookRotationSafe(facing,math.up()),scale));
            manager.SetComponentData(entity,new AbilityUnit{Team=1,Kind=(int)tier,Health=health,MaxHealth=health,BaseSpeed=stats.Speed*kind.Speed,Speed=stats.Speed*kind.Speed,
                BaseAttack=stats.Damage*kind.Damage*(combat.DamageMultiplier>0?combat.DamageMultiplier:1),Attack=stats.Damage*kind.Damage*(combat.DamageMultiplier>0?combat.DamageMultiplier:1),AttackSpeedMultiplier=1});
            manager.SetComponentData(entity,new CombatEnemy{ConfigId=kind.ConfigId,Kind=kindIndex,Stage=stage,Tier=tier,
                Experience=stats.Experience*(tier==EnemyTier.Normal?kind.Experience:1),Gold=stats.Gold*(tier==EnemyTier.Normal?kind.Gold:1)*(combat.GoldMultiplier>0?combat.GoldMultiplier:1),
                Reach=math.max(0,(scale-NormalScale)*.5f),LastHealth=health,BaseColor=color});
            if(manager.HasComponent<AutoAttackState>(entity))
                manager.SetComponentData(entity,new AutoAttackState{Cooldown=random.NextFloat()*manager.GetComponentData<AutoAttackData>(entity).BaseAttackTime});
            if(manager.HasComponent<URPMaterialPropertyBaseColor>(entity)) manager.SetComponentData(entity,new URPMaterialPropertyBaseColor{Value=color});
            if(manager.HasComponent<MonsterBrain>(entity))
            {
                var brain=manager.GetComponentData<MonsterBrain>(entity);brain.ReadyAt=new float4(-1);brain.ShieldAt=-1;brain.SummonAt=-1;
                if(tier==EnemyTier.Elite)
                {
                    int first=random.NextInt(6);brain.Affixes=(MonsterAffix)(1<<first);
                    if(stage==3) {int second=(first+random.NextInt(1,6))%6;brain.Affixes|=(MonsterAffix)(1<<second);}
                }
                manager.SetComponentData(entity,brain);
            }
        }
    }
}
