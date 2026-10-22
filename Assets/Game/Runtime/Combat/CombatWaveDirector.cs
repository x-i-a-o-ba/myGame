using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>某一波某一档敌人的实际数值。</summary>
    public struct WaveTierStats
    {
        public int Count; public float Health,Damage,Speed,Experience,Gold,Scale;
    }

    /// <summary>按难度曲线在玩家周围刷怪。只在主线程、所有 Job 完成后调用；不读取英雄或技能身份。</summary>
    public static class CombatWaveDirector
    {
        /// <summary>有限关卡到最终波停止新增敌人，继续等待最终首领死亡；FinalWave 为 0 才是无尽。</summary>
        public static bool ShouldSpawn(in CombatWaveSettings settings,in CombatWaveState state,float elapsed)
            =>state.Outcome==CombatOutcome.Running && elapsed>=state.NextWaveAt && (settings.FinalWave<=0 || state.Wave<settings.FinalWave);
        public const float NormalScale=0.85f;

        public static bool IsBossWave(in CombatWaveSettings settings,int wave)
            =>wave>0 && ((settings.FinalWave>0 && wave==settings.FinalWave) || (settings.BossEvery>0 && wave%settings.BossEvery==0));
        public static bool IsEliteWave(in CombatWaveSettings settings,int wave)
            =>wave>0 && settings.EliteEvery>0 && wave%settings.EliteEvery==0;

        /// <summary>计算一波中某一档敌人的数量与数值；纯函数，便于验证。</summary>
        public static WaveTierStats Stats(in CombatWaveSettings settings,in CombatSettings combat,int wave,EnemyTier tier)
        {
            int n=math.max(0,wave-1);
            float health=combat.EnemyHealth*math.pow(settings.HealthGrowth,n);
            float damage=combat.EnemyAttackDamage*math.pow(settings.DamageGrowth,n);
            float speed=math.min(math.max(settings.MaxSpeed,combat.EnemySpeed),combat.EnemySpeed+settings.SpeedPerWave*n);
            float experience=settings.BaseExperience*math.pow(settings.ExperienceGrowth,n);
            float gold=settings.BaseGold*math.pow(math.max(1,settings.GoldGrowth),n);
            var stats=new WaveTierStats {Health=health,Damage=damage,Speed=speed,Experience=experience,Gold=gold,Scale=NormalScale};
            switch(tier)
            {
                case EnemyTier.Normal:
                    stats.Count=math.min(settings.MaxPerWave,(int)math.round(settings.BaseCount*(1+settings.CountGrowth*n)));
                    break;
                case EnemyTier.Elite:
                    stats.Count=IsEliteWave(settings,wave)?settings.EliteBaseCount+wave/settings.EliteWavesPerExtra:0;
                    stats.Health*=settings.EliteHealth; stats.Damage*=settings.EliteDamage;
                    stats.Experience*=settings.EliteExperience; stats.Gold*=settings.EliteGold; stats.Scale*=settings.EliteScale;
                    break;
                case EnemyTier.Boss:
                    stats.Count=IsBossWave(settings,wave)?1:0;
                    stats.Health*=settings.BossHealth; stats.Damage*=settings.BossDamage;
                    stats.Experience*=settings.BossExperience; stats.Gold*=settings.BossGold; stats.Scale*=settings.BossScale; stats.Speed*=settings.BossSpeed;
                    break;
            }
            return stats;
        }

        /// <summary>未配置敌人名册时的单一种类：使用 CombatSettings.EnemyPrefab，允许所有档位，倍率为 1。</summary>
        public static CombatEnemyKind LegacyKind(in CombatSettings combat)
            =>new CombatEnemyKind {Prefab=combat.EnemyPrefab,ConfigId=GameCombatController.EnemyConfigId,
                Tiers=EnemyTierMask.Normal|EnemyTierMask.Elite|EnemyTierMask.Boss,FirstWave=1,Weight=1,
                Health=1,Damage=1,Speed=1,Experience=1,Gold=1,Scale=1,Color=new float4(1),EliteColor=new float4(1),BossColor=new float4(1)};

        public static bool Allows(in CombatEnemyKind kind,EnemyTier tier,int wave)
            =>kind.Prefab!=Entity.Null && kind.Weight>0 && (kind.Tiers&EnemySpawnEntry.Mask(tier))!=0 && wave>=kind.FirstWave && (kind.LastWave<=0 || wave<=kind.LastWave);

        /// <summary>按权重抽取一个可出场种类。没有可出场条目时依次退回：同档位全部条目、当前波可出场的普通条目、第一个条目。</summary>
        public static int Pick(NativeArray<CombatEnemyKind> kinds,EnemyTier tier,int wave,ref Random random)
        {
            float total=0;
            for(int i=0;i<kinds.Length;i++) if(Allows(kinds[i],tier,wave)) total+=kinds[i].Weight;
            if(total>0)
            {
                float roll=random.NextFloat(total);
                for(int i=0;i<kinds.Length;i++)
                {
                    if(!Allows(kinds[i],tier,wave)) continue;
                    roll-=kinds[i].Weight;
                    if(roll<0) return i;
                }
                for(int i=kinds.Length-1;i>=0;i--) if(Allows(kinds[i],tier,wave)) return i;
            }
            for(int i=0;i<kinds.Length;i++)
                if(kinds[i].Prefab!=Entity.Null && (kinds[i].Tiers&EnemySpawnEntry.Mask(tier))!=0) return i;
            if(tier!=EnemyTier.Normal) return Pick(kinds,EnemyTier.Normal,wave,ref random);
            return kinds.Length>0?0:-1;
        }

        /// <summary>未配置敌人名册时的兼容入口：只用 CombatSettings.EnemyPrefab 一个种类。</summary>
        public static int SpawnWave(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,ref CombatWaveState state,float3 center,int alive)
        {
            var kinds=new NativeArray<CombatEnemyKind>(1,Allocator.Temp);
            try
            {
                kinds[0]=LegacyKind(combat);
                return SpawnWave(manager,combat,settings,ref state,center,alive,kinds);
            }
            finally { kinds.Dispose(); }
        }

        /// <summary>
        /// 刷出 state.Wave 对应的一波。首领总会刷出；精英和普通敌人受同屏上限限制，被截掉的数量记入 Skipped。
        /// 每只敌人按档位从名册种类中按权重抽取。返回本波实际刷出的数量。kinds 必须是调用方持有的副本（实例化会使缓冲区失效）。
        /// </summary>
        public static int SpawnWave(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,ref CombatWaveState state,float3 center,int alive,
            NativeArray<CombatEnemyKind> kinds)
        {
            var random=Random.CreateFromIndex(state.Seed++);
            int room=math.max(0,combat.MaxAlive-alive),spawned=0;
            if(kinds.Length==0) return 0;
            using(var counts=new NativeArray<int>(kinds.Length,Allocator.Temp))
            {
                var boss=Stats(settings,combat,state.Wave,EnemyTier.Boss);
                for(int i=0;i<boss.Count;i++)
                {
                    int kind=Pick(kinds,EnemyTier.Boss,state.Wave,ref random);
                    if(kind<0) break;
                    int count=Spawn(manager,combat,settings,state.Wave,EnemyTier.Boss,boss,kind,kinds[kind],1,center,ref random,out Entity last);
                    spawned+=count; if(count>0) state.Boss=last;
                }
                if(boss.Count>0 && settings.FinalWave>0 && state.Wave==settings.FinalWave) state.FinalBossSpawned=true;
                room=math.max(0,room-boss.Count);
                var elite=Stats(settings,combat,state.Wave,EnemyTier.Elite);
                int elites=math.min(room,elite.Count);
                spawned+=SpawnTier(manager,combat,settings,state.Wave,EnemyTier.Elite,elite,elites,kinds,counts,center,ref random);
                room-=elites;
                var normal=Stats(settings,combat,state.Wave,EnemyTier.Normal);
                int normals=math.min(room,normal.Count);
                spawned+=SpawnTier(manager,combat,settings,state.Wave,EnemyTier.Normal,normal,normals,kinds,counts,center,ref random);
                state.Skipped+=elite.Count-elites+normal.Count-normals;
            }
            state.SpawnedTotal+=spawned; state.LastWaveSpawned=spawned;
            return spawned;
        }

        private static int SpawnTier(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,int wave,EnemyTier tier,in WaveTierStats stats,int total,
            NativeArray<CombatEnemyKind> kinds,NativeArray<int> counts,float3 center,ref Random random)
        {
            if(total<=0) return 0;
            for(int i=0;i<counts.Length;i++) counts[i]=0;
            for(int i=0;i<total;i++)
            {
                int kind=Pick(kinds,tier,wave,ref random);
                if(kind>=0) counts[kind]=counts[kind]+1;
            }
            int spawned=0;
            for(int i=0;i<kinds.Length;i++)
                spawned+=Spawn(manager,combat,settings,wave,tier,stats,i,kinds[i],counts[i],center,ref random,out _);
            return spawned;
        }

        private static int Spawn(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,int wave,EnemyTier tier,
            in WaveTierStats stats,int kindIndex,in CombatEnemyKind kind,int count,float3 center,ref Random random,out Entity last)
        {
            last=Entity.Null;
            if(count<=0 || kind.Prefab==Entity.Null) return 0;
            float inner=math.max(3,combat.SafeSpawnRadius),outer=inner+settings.SpawnRingWidth,limit=math.max(4,combat.ArenaHalfSize-2);
            float scale=stats.Scale*kind.Scale,health=math.max(1,stats.Health*kind.Health),damage=stats.Damage*kind.Damage;
            float speed=stats.Speed*kind.Speed,experience=stats.Experience*kind.Experience,gold=math.max(0,stats.Gold*kind.Gold);
            float4 color=tier==EnemyTier.Boss?kind.BossColor:tier==EnemyTier.Elite?kind.EliteColor:kind.Color;
            bool hasAttack=manager.HasComponent<AutoAttackState>(kind.Prefab) && manager.HasComponent<AutoAttackData>(kind.Prefab);
            bool hasColor=manager.HasComponent<URPMaterialPropertyBaseColor>(kind.Prefab);
            float interval=hasAttack?manager.GetComponentData<AutoAttackData>(kind.Prefab).BaseAttackTime:combat.EnemyAttackInterval;
            using(var units=new NativeArray<Entity>(count,Allocator.Temp))
            {
                manager.Instantiate(kind.Prefab,units);
                for(int i=0;i<count;i++)
                {
                    float angle=random.NextFloat(0,2*math.PI);
                    float radius=math.sqrt(math.lerp(inner*inner,outer*outer,random.NextFloat()));
                    float2 planar=math.clamp(center.xz+new float2(math.cos(angle),math.sin(angle))*radius,-limit,limit);
                    float height=0.125f+scale*0.5f;
                    float3 position=new float3(planar.x,height,planar.y);
                    float3 facing=center-position; facing.y=0;
                    var rotation=quaternion.LookRotationSafe(math.normalizesafe(facing,new float3(0,0,1)),math.up());
                    manager.SetComponentData(units[i],LocalTransform.FromPositionRotationScale(position,rotation,scale));
                    manager.SetComponentData(units[i],new AbilityUnit {Team=1,Health=health,MaxHealth=health,
                        BaseSpeed=speed,Speed=speed,BaseAttack=damage,Attack=damage,AttackSpeedMultiplier=1});
                    manager.SetComponentData(units[i],new CombatEnemy {ConfigId=kind.ConfigId,Kind=kindIndex,Experience=experience,Gold=gold,
                        Reach=math.max(0,(scale-NormalScale)*0.5f),Tier=tier,Wave=wave,LastHealth=health,BaseColor=color,
                        ChargeReadyIn=random.NextFloat()*1.5f});
                    if(hasAttack) manager.SetComponentData(units[i],new AutoAttackState {Cooldown=random.NextFloat()*interval});
                    if(hasColor) manager.SetComponentData(units[i],new URPMaterialPropertyBaseColor {Value=color});
                }
                last=units[count-1];
            }
            return count;
        }
    }
}
