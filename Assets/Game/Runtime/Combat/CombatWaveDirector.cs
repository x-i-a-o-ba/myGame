using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>某一波某一档敌人的实际数值。</summary>
    public struct WaveTierStats
    {
        public int Count; public float Health,Damage,Speed,Experience,Scale;
    }

    /// <summary>按难度曲线在玩家周围刷怪。只在主线程、所有 Job 完成后调用；不读取英雄或技能身份。</summary>
    public static class CombatWaveDirector
    {
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
            var stats=new WaveTierStats {Health=health,Damage=damage,Speed=speed,Experience=experience,Scale=NormalScale};
            switch(tier)
            {
                case EnemyTier.Normal:
                    stats.Count=math.min(settings.MaxPerWave,(int)math.round(settings.BaseCount*(1+settings.CountGrowth*n)));
                    break;
                case EnemyTier.Elite:
                    stats.Count=IsEliteWave(settings,wave)?settings.EliteBaseCount+wave/settings.EliteWavesPerExtra:0;
                    stats.Health*=settings.EliteHealth; stats.Damage*=settings.EliteDamage;
                    stats.Experience*=settings.EliteExperience; stats.Scale*=settings.EliteScale;
                    break;
                case EnemyTier.Boss:
                    stats.Count=IsBossWave(settings,wave)?1:0;
                    stats.Health*=settings.BossHealth; stats.Damage*=settings.BossDamage;
                    stats.Experience*=settings.BossExperience; stats.Scale*=settings.BossScale; stats.Speed*=settings.BossSpeed;
                    break;
            }
            return stats;
        }

        /// <summary>
        /// 刷出 state.Wave 对应的一波。首领总会刷出；精英和普通敌人受同屏上限限制，被截掉的数量记入 Skipped。
        /// 返回本波实际刷出的数量。
        /// </summary>
        public static int SpawnWave(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,ref CombatWaveState state,float3 center,int alive)
        {
            var random=Random.CreateFromIndex(state.Seed++);
            int room=math.max(0,combat.MaxAlive-alive),spawned=0;
            var boss=Stats(settings,combat,state.Wave,EnemyTier.Boss);
            spawned+=Spawn(manager,combat,settings,state.Wave,EnemyTier.Boss,boss,boss.Count,center,ref random);
            if(boss.Count>0 && settings.FinalWave>0 && state.Wave==settings.FinalWave) state.FinalBossSpawned=true;
            room=math.max(0,room-boss.Count);
            var elite=Stats(settings,combat,state.Wave,EnemyTier.Elite);
            int elites=math.min(room,elite.Count);
            spawned+=Spawn(manager,combat,settings,state.Wave,EnemyTier.Elite,elite,elites,center,ref random);
            room-=elites;
            var normal=Stats(settings,combat,state.Wave,EnemyTier.Normal);
            int normals=math.min(room,normal.Count);
            spawned+=Spawn(manager,combat,settings,state.Wave,EnemyTier.Normal,normal,normals,center,ref random);
            state.Skipped+=elite.Count-elites+normal.Count-normals;
            state.SpawnedTotal+=spawned; state.LastWaveSpawned=spawned;
            return spawned;
        }

        private static int Spawn(EntityManager manager,in CombatSettings combat,in CombatWaveSettings settings,int wave,EnemyTier tier,
            in WaveTierStats stats,int count,float3 center,ref Random random)
        {
            if(count<=0 || combat.EnemyPrefab==Entity.Null) return 0;
            float inner=math.max(3,combat.SafeSpawnRadius),outer=inner+settings.SpawnRingWidth,limit=math.max(4,combat.ArenaHalfSize-2);
            using(var units=new NativeArray<Entity>(count,Allocator.Temp))
            {
                manager.Instantiate(combat.EnemyPrefab,units);
                for(int i=0;i<count;i++)
                {
                    float angle=random.NextFloat(0,2*math.PI);
                    float radius=math.sqrt(math.lerp(inner*inner,outer*outer,random.NextFloat()));
                    float2 planar=math.clamp(center.xz+new float2(math.cos(angle),math.sin(angle))*radius,-limit,limit);
                    float height=0.125f+stats.Scale*0.5f;
                    manager.SetComponentData(units[i],LocalTransform.FromPositionRotationScale(new float3(planar.x,height,planar.y),quaternion.identity,stats.Scale));
                    manager.SetComponentData(units[i],new AbilityUnit {Team=1,Health=stats.Health,MaxHealth=stats.Health,
                        BaseSpeed=stats.Speed,Speed=stats.Speed,BaseAttack=stats.Damage,Attack=stats.Damage,AttackSpeedMultiplier=1});
                    manager.SetComponentData(units[i],new CombatEnemy {ConfigId=GameCombatController.EnemyConfigId,
                        AttackCooldown=random.NextFloat()*combat.EnemyAttackInterval,Experience=stats.Experience,
                        Reach=math.max(0,(stats.Scale-NormalScale)*0.5f),Tier=tier,Wave=wave});
                }
            }
            return count;
        }
    }
}
