using Roguelike.Abilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>
    /// 战斗宿主：WASD 移动、自动普攻、模块化怪物技能、死亡奖励和持续刷怪。
    /// 属性归 HeroStatsLogic；技能、普攻、装备及 Buff 共用效果结算。营地交易由流程控制器处理。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class CombatSimulationSystem : SystemBase
    {
        /// <summary>自动索敌目标超出索敌距离后仍继续追击的额外牵引距离（米）。</summary>
        public const float LeashExtra=4;
        /// <summary>敌人体型半径 = 缩放 × 该系数（米），用于攻击距离与点选。</summary>
        public const float BodyRadius=0.5f;
        private const float AcquireRetry=0.1f,HostileHitRadius=0.6f,FlashSeconds=0.12f,MoveFacing=0.5f;
        /// <summary>同屏敌方弹体上限；超出时忽略新的出手，避免弹体数量失控。</summary>
        public const int MaxHostileProjectiles=2500;
        private NativeQueue<PendingCombatDamage> pendingDamage;
        private NativeQueue<HostileShot> pendingShots;
        private AbilityExecution abilities;
        public MonsterAbilities Monsters {get;private set;}
        private EnemySeparation separation;
        private EntityQuery enemies, projectiles, hostileProjectiles;
        public bool TryReplaceLibrary(Entity owner,BlobAssetReference<AbilityLibrary> replacement,out string failure)
        {
            Dependency.Complete(); EntityManager.CompleteAllTrackedJobs();
            return abilities.ReplaceLibrary(owner,replacement,out failure);
        }
        public BlobAssetReference<AbilityLibrary> GetLibrary(Entity owner,int version)=>abilities.GetLibrary(owner,version);
        public void ReleaseOwner(Entity owner)
        { Dependency.Complete(); abilities.ReleaseOwner(owner); basicPierced.Clear(); Monsters.Clear(); }
        protected override void OnCreate()
        {
            pendingDamage=new NativeQueue<PendingCombatDamage>(Allocator.Persistent);
            pendingShots=new NativeQueue<HostileShot>(Allocator.Persistent);
            enemies=GetEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            projectiles=GetEntityQuery(typeof(CombatProjectile),typeof(LocalTransform));
            hostileProjectiles=GetEntityQuery(typeof(HostileProjectile),typeof(LocalTransform));
            abilities=new AbilityExecution(EntityManager);
            Monsters=new MonsterAbilities(EntityManager,abilities);
            separation=new EnemySeparation();
            RequireForUpdate<CombatSettings>();
            RequireForUpdate<AbilityRuntime>();
        }
        protected override void OnDestroy()
        { Dependency.Complete(); abilities.Dispose(); separation.Dispose(); pendingDamage.Dispose(); pendingShots.Dispose(); }

        public double LastSimulationMilliseconds {get; private set;}
        protected override void OnUpdate()
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            try { StepCombat(); }
            finally { LastSimulationMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency; }
        }
        private void StepCombat()
        {
            var manager=EntityManager;
            Entity session=SystemAPI.GetSingletonEntity<CombatSettings>();
            var config=manager.GetComponentData<CombatSettings>(session);
            var steering=manager.HasComponent<HeroSteering>(session)?manager.GetComponentData<HeroSteering>(session):default;
            if(steering.Paused) return;
            var player=manager.GetComponentData<CombatPlayer>(session);
            var runtime=manager.GetComponentData<AbilityRuntime>(session);
            var waveState=manager.GetComponentData<CombatDirectorState>(session);
            var director=manager.GetComponentData<SpawnDirectorSettings>(session);
            // 胜利后冻结战斗：不推进时间、不刷怪，由远征流程进入营地或结算。
            if(waveState.Outcome==CombatOutcome.Victory) { ClearCommands(session); return; }
            float dt=math.min(SystemAPI.Time.DeltaTime,0.1f);
            if(dt<=0) return;
            float now=runtime.Time+dt;
            Dependency=new TickBuffs {Library=runtime.Library,Now=now,Delta=dt}.ScheduleParallel(Dependency);
            Dependency.Complete();
            var hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0) { Die(session,ref player,ref waveState,now,dt); return; }
            ProcessProgression(session,ref runtime.Library.Value);
            hero=manager.GetComponentData<AbilityUnit>(session);
            player.Elapsed+=dt;
            waveState.StageElapsed+=dt;
            if(waveState.BossSpawned && !waveState.BossEnraged && waveState.StageElapsed>=director.EnrageAt && Alive(waveState.Boss))
            {
                var boss=manager.GetComponentData<AbilityUnit>(waveState.Boss); boss.BaseAttack*=2; boss.Attack*=2;
                manager.SetComponentData(waveState.Boss,boss); waveState.BossEnraged=true;
            }
            // 世界方向移动与自动攻击节奏；动作锁归技能持有者，施法前后摇期间不移动、不普攻。
            var attack=manager.GetComponentData<AutoAttackState>(session);
            var attackData=manager.GetComponentData<AutoAttackData>(session);
            bool released=StepSteering(ref player,ref attack,attackData,hero,steering,config.ArenaHalfSize,AbilityCastTiming.IsBusy(runtime,now),dt);
            Entity releaseTarget=attack.Target;
            manager.SetComponentData(session,attack);
            manager.SetComponentData(session,LocalTransform.FromPosition(player.Position));
            separation.Prepare(enemies.CalculateEntityCount());
            var hostileCommands=new EntityCommandBuffer(Allocator.TempJob);
            Dependency=new EnemyJob {PlayerPosition=player.Position,DeltaTime=dt,Damage=pendingDamage.AsParallelWriter(),
                Shots=pendingShots.AsParallelWriter(),Now=now}.ScheduleParallel(Dependency);
            Dependency=separation.Schedule(manager,enemies,director,dt,config.ArenaHalfSize,Dependency);
            Dependency=new HostileProjectileJob {Target=player.Position,DeltaTime=dt,HitRadius=HostileHitRadius,
                Damage=pendingDamage.AsParallelWriter(),Commands=hostileCommands.AsParallelWriter()}.ScheduleParallel(Dependency);
            // 此处是所有并行单位写入与串行效果写入的明确交接点。
            Dependency.Complete();
            hostileCommands.Playback(manager); hostileCommands.Dispose();
            SpawnHostileShots(config,player.Position);
            ref var library=ref runtime.Library.Value;
            while(pendingDamage.TryDequeue(out var damage))
            {
                float reflection=manager.GetComponentData<AbilityUnit>(session).Reflect;
                var outcome=abilities.Apply(damage.Source,session,-1,new EffectData{Kind=EffectKind.Damage,Amount=damage.Amount},ref library,now);
                if(manager.Exists(damage.Source) && manager.HasComponent<MonsterBrain>(damage.Source))
                {
                    var brain=manager.GetComponentData<MonsterBrain>(damage.Source);
                    if((brain.Affixes&MonsterAffix.Elemental)!=0 && !brain.ElementIsFire && brain.ElementEffect.Kind==EffectKind.ApplyBuff)
                        abilities.Apply(damage.Source,session,-2,brain.ElementEffect,ref library,now);
                }
                if(damage.Melee && reflection>0 && outcome.ActualDamage>0)
                    abilities.Apply(session,damage.Source,-1,new EffectData{Kind=EffectKind.Damage,Amount=outcome.ActualDamage*reflection},ref library,now);
            }
            hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0) { Die(session,ref player,ref waveState,now,dt); return; }
            abilities.RebuildIndex();
            Monsters.Step(session,enemies,now,dt,ref library);
            player.Position=manager.GetComponentData<LocalTransform>(session).Position;
            if(manager.GetComponentData<AbilityUnit>(session).Health<=0) {Die(session,ref player,ref waveState,now,dt);return;}
            using var destroy=new EntityCommandBuffer(Allocator.Temp);
            using(var bullets=projectiles.ToEntityArray(Allocator.Temp))
                for(int i=0;i<bullets.Length;i++)
                {
                    Entity bullet=bullets[i]; var projectile=manager.GetComponentData<CombatProjectile>(bullet);
                    var transform=manager.GetComponentData<LocalTransform>(bullet);
                    projectile.Lifetime-=dt;
                    if(projectile.Piercing) {StepPiercingBasic(session,bullet,ref projectile,ref transform,ref library,now,dt,ref player,destroy);continue;}
                    if(projectile.Lifetime<=0 || !Alive(projectile.Target))
                    { destroy.DestroyEntity(bullet); continue; }
                    var target=manager.GetComponentData<LocalTransform>(projectile.Target);
                    float3 offset=target.Position-transform.Position; float remaining=math.length(offset);
                    if(remaining<=projectile.Speed*dt+0.35f)
                    {
                        HeroAttackHit(session,projectile.Target,projectile.Damage,projectile.CountsAsBasicAttackHit,ref library,now,ref player);
                        destroy.DestroyEntity(bullet);
                    }
                    else
                    {
                        transform.Position+=offset/remaining*projectile.Speed*dt;
                        manager.SetComponentData(bullet,transform); manager.SetComponentData(bullet,projectile);
                    }
                }
            abilities.Step(session,now,dt);
            ProcessCastEvents(session,ref library,now);
            hero=manager.GetComponentData<AbilityUnit>(session);
            runtime=manager.GetComponentData<AbilityRuntime>(session);
            // 自动攻击出手：弹道攻击发射追踪弹体，近战攻击立即结算；目标已在本帧死亡时本次攻击落空。
            if(released && hero.Health>0 && Alive(releaseTarget))
            {
                player.ShotsFired++;
                float damage=hero.Attack;
                if(attackData.Delivery==AttackDelivery.Projectile)
                {
                    Entity bullet=destroy.Instantiate(config.ProjectilePrefab);
                    destroy.SetComponent(bullet,new CombatProjectile {Target=releaseTarget,Damage=damage,Speed=attackData.ProjectileSpeed,Lifetime=5,
                        CountsAsBasicAttackHit=attackData.CountsAsBasicAttackHit,Piercing=hero.PierceBasics,RemainingRange=attackData.Range,
                        Direction=math.normalizesafe(manager.GetComponentData<LocalTransform>(releaseTarget).Position-player.Position)});
                    destroy.SetComponent(bullet,LocalTransform.FromPositionRotationScale(player.Position+new float3(0,0.5f,0),quaternion.identity,0.27f));
                }
                else HeroAttackHit(session,releaseTarget,damage,attackData.CountsAsBasicAttackHit,ref runtime.Library.Value,runtime.Time,ref player);
                ReleaseSecondaryAttacks(session,releaseTarget,hero,attackData,config,ref library,now,ref player,destroy);
            }
            float experience=0,gold=config.PassiveGoldPerSecond*dt; int killsBefore=player.Kills; bool victory=false;
            var traits=manager.HasComponent<AbilityTraits>(session)?manager.GetComponentData<AbilityTraits>(session):AbilityTraits.Identity;
            var waves=manager.GetComponentData<CombatDifficultySettings>(session);
            using(var units=enemies.ToEntityArray(Allocator.Temp))
            using(var stats=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp))
            using(var records=enemies.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
                for(int i=0;i<units.Length;i++)
                {
                    if(stats[i].Health>0) continue;
                    var enemy=records[i];
                    Monsters.OnDeath(units[i],now,ref library);
                    var feedback=manager.GetBuffer<AbilityFeedback>(session);
                    if(feedback.Length<256) feedback.Add(new AbilityFeedback{Target=units[i],Position=manager.GetComponentData<LocalTransform>(units[i]).Position,Killed=true});
                    // 自爆死亡不给经验和金币、不计击杀，也不会触发首领胜利。
                    if(!enemy.SelfDestructed)
                    {
                        experience+=enemy.Experience; gold+=enemy.Gold*(enemy.Tier==EnemyTier.Normal?1:traits.EliteGoldMultiplier); player.Kills++;
                        if(enemy.Tier==EnemyTier.Boss && units[i]==waveState.Boss) victory=true;
                    }
                    destroy.DestroyEntity(units[i]);
                }
            destroy.Playback(manager);
            // 经验、金币与升级在 ECB 回放之后串行结算；升级后统一重算属性（成长直接相加），再叠加 Buff。
            if(gold>0 && manager.HasComponent<HeroEquipment>(session))
            {
                var purse=manager.GetComponentData<HeroEquipment>(session);
                EquipmentLogic.GainGold(ref purse,gold); manager.SetComponentData(session,purse);
            }
            var progression=manager.GetComponentData<HeroProgression>(session);
            HeroProgressionLogic.GainExperience(ref progression,experience);
            int gained=HeroProgressionLogic.ApplyPendingLevels(ref progression);
            manager.SetComponentData(session,progression);
            if(gained>0) RebuildHeroStats(session,ref runtime.Library.Value);
            hero=manager.GetComponentData<AbilityUnit>(session);
            if(player.Kills>killsBefore && traits.KillHealRatio>0)
                abilities.Apply(session,session,-1,new EffectData{Kind=EffectKind.Heal,Amount=(player.Kills-killsBefore)*hero.MaxHealth*traits.KillHealRatio},ref library,now);
            if(victory)
            {
                waveState.Outcome=CombatOutcome.Victory;Monsters.Clear();
                manager.DestroyEntity(enemies);manager.DestroyEntity(projectiles);manager.DestroyEntity(hostileProjectiles);basicPierced.Clear();
                manager.GetBuffer<SkillFlight>(session).Clear();manager.GetBuffer<ScheduledSkillStage>(session).Clear();
            }
            else if(waveState.StageElapsed>=waveState.NextTickAt)
            {
                using var kinds=manager.GetBuffer<CombatEnemyKind>(session).ToNativeArray(Allocator.Temp);
                using var curve=manager.GetBuffer<PopulationPoint>(session).ToNativeArray(Allocator.Temp);
                CombatSpawnDirector.Tick(manager,config,waves,director,ref waveState,player.Position,enemies.CalculateEntityCount(),kinds,curve,
                    manager.GetComponentData<CombatSpawnView>(session));
            }
            manager.SetComponentData(session,waveState);
            player.AliveEnemies=enemies.CalculateEntityCount();
            player.ActiveProjectiles=projectiles.CalculateEntityCount()+hostileProjectiles.CalculateEntityCount()+manager.GetBuffer<SkillFlight>(session).Length;
            player.Health=hero.Health; player.MaximumHealth=hero.MaxHealth;
            manager.SetComponentData(session,player);
        }
        private void Die(Entity session,ref CombatPlayer player,ref CombatDirectorState waveState,float now,float dt)
        {
            abilities.Step(session,now,dt);Monsters.Clear();
            EntityManager.DestroyEntity(projectiles); EntityManager.DestroyEntity(hostileProjectiles);
            while(pendingShots.TryDequeue(out _)) { }
            player.ActiveProjectiles=0; player.Health=0;
            EntityManager.SetComponentData(session,player); Defeat(session,ref waveState);
        }
        private bool Alive(Entity entity)
            =>entity!=Entity.Null && EntityManager.Exists(entity) && EntityManager.HasComponent<AbilityUnit>(entity) &&
              EntityManager.GetComponentData<AbilityUnit>(entity).Health>0;
        /// <summary>读取存活敌人的位置、平面距离与体型半径；目标已死亡或被销毁时返回 false。</summary>
        private bool TryTarget(Entity entity,float3 from,out float3 position,out float distance,out float radius)
        {
            position=default; distance=0; radius=0;
            if(!Alive(entity) || !EntityManager.HasComponent<CombatEnemy>(entity) || !EntityManager.HasComponent<LocalTransform>(entity)) return false;
            var transform=EntityManager.GetComponentData<LocalTransform>(entity);
            position=transform.Position; float3 offset=position-from; offset.y=0;
            distance=math.length(offset); radius=BodyRadius*transform.Scale;
            return true;
        }
        /// <summary>索敌：返回体表距离不超过 radius 的最近存活敌人。批量复制组件，避免逐实体查询。</summary>
        private Entity Nearest(float3 from,float radius)
        {
            Entity best=Entity.Null; float closest=radius;
            using var units=enemies.ToEntityArray(Allocator.Temp);
            using var transforms=enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var stats=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            for(int i=0;i<units.Length;i++)
            {
                if(stats[i].Health<=0) continue;
                float distance=math.distance(from.xz,transforms[i].Position.xz)-BodyRadius*transforms[i].Scale;
                if(distance<=closest) { closest=distance; best=units[i]; }
            }
            return best;
        }
        /// <summary>
        /// Dota 2 式英雄步进：读取移动意图并检查目标有效性，独立推进自动索敌与攻击节奏。
        /// 返回 true 表示本帧攻击出手，目标为 attack.Target。
        /// </summary>
        // 移动与攻击独立：输入为世界平面向量，不追击目标，不等待转身。
        private bool StepSteering(ref CombatPlayer player,ref AutoAttackState attack,in AutoAttackData data,
            in AbilityUnit hero,in HeroSteering input,float arena,bool busy,float dt)
        {
            float3 movement=math.normalizesafe(new float3(input.Direction.x,0,input.Direction.y));
            if(!busy && (hero.Controls&ControlFlags.MovementBlocked)==0)
            {
                player.Position+=movement*hero.Speed*dt;
                player.Position.xz=math.clamp(player.Position.xz,new float2(-arena+2),new float2(arena-2));
            }
            player.Destination=player.Position;
            attack.AcquireDelay-=dt;
            if(!TryTarget(attack.Target,player.Position,out var targetPosition,out var distance,out var radius) || distance>data.Range+radius)
                attack.Target=Entity.Null;
            if(attack.AcquireDelay<=0)
            {
                attack.Target=Nearest(player.Position,data.Range); attack.AcquireDelay=AcquireRetry;
            }
            bool target=TryTarget(attack.Target,player.Position,out targetPosition,out distance,out radius);
            if(target) player.Facing=math.normalizesafe(targetPosition-player.Position,player.Facing);
            else if(math.lengthsq(movement)>0) player.Facing=movement;
            bool engage=!busy && target && distance<=data.Range+radius && (hero.Controls&ControlFlags.AttackBlocked)==0;
            return AutoAttackLogic.Step(ref attack,data,hero.AttackSpeedMultiplier,engage,dt)==AutoAttackEvent.Released;
        }
        private void SpawnHostileShots(in CombatSettings config,float3 target)
        {
            int active=hostileProjectiles.CalculateEntityCount();
            using var spawn=new EntityCommandBuffer(Allocator.Temp);
            while(pendingShots.TryDequeue(out var shot))
            {
                if(config.HostileProjectilePrefab==Entity.Null || active>=MaxHostileProjectiles) continue;
                active++;
                Entity projectile=spawn.Instantiate(config.HostileProjectilePrefab);
                spawn.SetComponent(projectile,new HostileProjectile {Source=shot.Source,Direction=math.normalizesafe(new float3(target.x-shot.Origin.x,0,target.z-shot.Origin.z)),Damage=shot.Damage,Speed=shot.Speed,Lifetime=4});
                spawn.SetComponent(projectile,LocalTransform.FromPositionRotationScale(shot.Origin,quaternion.identity,0.32f));
            }
            spawn.Playback(EntityManager);
        }
        private void Defeat(Entity session,ref CombatDirectorState state)
        {
            ClearCommands(session);
            if(state.Outcome!=CombatOutcome.Running) return;
            state.Outcome=CombatOutcome.Defeat; EntityManager.SetComponentData(session,state);
        }
        /// <summary>胜负已定或英雄倒下时丢弃尚未处理的学习指令。</summary>
        private void ClearCommands(Entity session)
        {
            EntityManager.GetBuffer<ProgressionCommand>(session).Clear();
        }
        /// <summary>串行处理学习技能，结果与失败原因写回 HeroProgression。</summary>
        private bool ProcessProgression(Entity session,ref AbilityLibrary library)
        {
            var commands=EntityManager.GetBuffer<ProgressionCommand>(session);
            if(commands.Length==0) return false;
            var progression=EntityManager.GetComponentData<HeroProgression>(session);
            bool talent=false;
            for(int i=0;i<commands.Length;i++)
            {
                var command=commands[i]; progression.CommandSerial++;
                if(command.Kind==ProgressionCommandKind.LearnSkill)
                    HeroProgressionLogic.TryLearn(ref progression,EntityManager.GetBuffer<AbilitySlot>(session),command.Value,ref library);

            }
            EntityManager.GetBuffer<ProgressionCommand>(session).Clear();
            EntityManager.SetComponentData(session,progression);
            return talent;
        }
        public void RefreshHero(Entity session)
        {
            Dependency.Complete();
            var runtime=EntityManager.GetComponentData<AbilityRuntime>(session);
            RebuildHeroStats(session,ref runtime.Library.Value);
        }
        private void RebuildHeroStats(Entity session,ref AbilityLibrary library)
        {
            var manager=EntityManager;
            if(!manager.HasComponent<HeroStats>(session)) return;
            var stats=manager.GetComponentData<HeroStats>(session);
            var progression=manager.GetComponentData<HeroProgression>(session);
            var unit=manager.GetComponentData<AbilityUnit>(session);
            var attack=manager.GetComponentData<AutoAttackData>(session);
            var equipment=default(StatSheet);
            if(manager.HasComponent<HeroEquipment>(session))
                equipment=EquipmentLogic.Sheet(manager.GetBuffer<EquipmentSlot>(session),manager.GetComponentData<HeroEquipment>(session));
            HeroStatsLogic.Rebuild(ref stats,equipment,ref progression,ref unit,ref attack,manager.HasComponent<AbilityTraits>(session)?manager.GetComponentData<AbilityTraits>(session):default);
            AbilityEffects.Recalculate(ref unit,manager.GetBuffer<ActiveBuff>(session),ref library);
            manager.SetComponentData(session,stats); manager.SetComponentData(session,progression);
            manager.SetComponentData(session,unit); manager.SetComponentData(session,attack);
        }
        /// <summary>
        /// 命中效果的作用对象为“施法者”时作用于英雄自身；目标已死亡时其余效果自动跳过。
        /// </summary>
        private void HeroAttackHit(Entity session,Entity target,float damage,bool countsAsHit,ref AbilityLibrary library,float now,ref CombatPlayer player)
        {
            var manager=EntityManager;
            var outcome=abilities.Apply(session,target,-1,new EffectData{Kind=EffectKind.Damage,Amount=damage},ref library,now);
            if(manager.HasComponent<HeroStats>(session))
            {
                float heal=HeroStatsLogic.LifeStealHeal(manager.GetComponentData<HeroStats>(session).Total,outcome.ActualDamage);
                if(heal>0) abilities.Apply(session,session,-1,new EffectData{Kind=EffectKind.Heal,Amount=heal},ref library,now);
            }
            if(manager.HasComponent<HeroEquipment>(session))
            {
                var equipment=manager.GetComponentData<HeroEquipment>(session);
                if(equipment.Library.IsCreated)
                {
                    ref var items=ref equipment.Library.Value.Items;
                    var slots=manager.GetBuffer<EquipmentSlot>(session);
                    for(int i=0;i<slots.Length;i++)
                    {
                        int item=slots[i].Item;
                        if(item<0 || item>=items.Length) continue;
                        ref var hits=ref items[item].OnHit;
                        for(int e=0;e<hits.Length;e++)
                        {
                            var effect=hits[e];
                            abilities.Apply(session,effect.Target==EffectTarget.Source?session:target,EquipmentLogic.OnHitOrigin+item,effect,ref library,now);
                        }
                    }
                }
            }
            BasicSplash(session,manager.GetComponentData<LocalTransform>(target).Position,manager.GetComponentData<AbilityUnit>(session),ref library,now,target);
            if(countsAsHit) {abilities.RecordBasicAttackHit(session,target);EquipmentTriggerEvent(session,target,EquipmentTrigger.BasicHit,player.Hits,ref library,now);}
            player.Hits++;
        }
        [BurstCompile, WithAll(typeof(CombatOwned))]
        private partial struct TickBuffs : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<AbilityLibrary> Library;
            public float Now,Delta;
            private void Execute(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs)
            { AbilityEffects.Tick(ref unit,buffs,ref Library.Value,Now,Delta); }
        }
        /// <summary>敌人行为任务：按 EnemyBehavior 移动、冲锋、保持距离或自爆，并用自动攻击模块决定出手时机。</summary>
        [BurstCompile]
        private partial struct EnemyJob : IJobEntity
        {
            public float3 PlayerPosition;
            public float DeltaTime,Now;
            public NativeQueue<PendingCombatDamage>.ParallelWriter Damage;
            public NativeQueue<HostileShot>.ParallelWriter Shots;
            private void Execute(Entity entity,ref LocalTransform transform,ref CombatEnemy enemy,ref AutoAttackState attack,ref AbilityUnit unit,
                ref URPMaterialPropertyBaseColor color,in AutoAttackData data,in EnemyBehaviorData behavior,in MonsterBrain brain)
            {
                if(unit.Health<=0) return;
                // 受击闪白：生命下降时短暂混入白色。
                if(unit.Health<enemy.LastHealth-0.001f) enemy.Flash=FlashSeconds;
                enemy.LastHealth=unit.Health;
                enemy.Flash=math.max(0,enemy.Flash-DeltaTime);
                color.Value=math.lerp(enemy.BaseColor,new float4(1,1,1,enemy.BaseColor.w),enemy.Flash/FlashSeconds*0.75f);
                if(brain.BusyUntil>Now) {AutoAttackLogic.Interrupt(ref attack);return;}
                float3 offset=PlayerPosition-transform.Position; offset.y=0;
                float distance=math.length(offset);
                float3 direction=distance>0.0001f?offset/distance:new float3(0,0,1);
                bool canMove=(unit.Controls&ControlFlags.MovementBlocked)==0;
                bool canAttack=(unit.Controls&ControlFlags.AttackBlocked)==0;
                float reach=data.Range+enemy.Reach;
                float step=unit.Speed*DeltaTime;
                bool engage;
                if(behavior.Behavior==EnemyBehavior.Ranged)
                {
                    // 远程：停在偏好距离射击，英雄贴近时后退。
                    float preferred=math.max(0.5f,data.Range*behavior.PreferredDistance)+enemy.Reach;
                    if(canMove && attack.Phase!=AttackPhase.Windup)
                    {
                        if(distance>preferred) transform.Position+=direction*math.min(distance-preferred,step);
                        else if(distance<behavior.RetreatDistance) transform.Position-=direction*math.min(behavior.RetreatDistance-distance,step*0.7f);
                    }
                    engage=canAttack && distance<=reach+(attack.Phase==AttackPhase.Windup?data.RangeBuffer:0);
                }
                else
                {
                    // 近战、冲锋（未冲锋时）与自爆：追到攻击距离；前摇中站定。
                    if(canMove && distance>reach && attack.Phase!=AttackPhase.Windup) transform.Position+=direction*math.min(distance-reach,step);
                    engage=canAttack && distance<=reach+(attack.Phase==AttackPhase.Windup?data.RangeBuffer:0.05f);
                }
                if(distance>0.0001f) transform.Rotation=quaternion.LookRotationSafe(direction,math.up());
                if(AutoAttackLogic.Step(ref attack,data,unit.AttackSpeedMultiplier,engage,DeltaTime)!=AutoAttackEvent.Released) return;
                if(data.Delivery==AttackDelivery.Projectile)
                    Shots.Enqueue(new HostileShot {Source=entity,Origin=transform.Position+new float3(0,0.4f*transform.Scale,0),Damage=unit.Attack,Speed=data.ProjectileSpeed});
                else Damage.Enqueue(new PendingCombatDamage{Source=entity,Amount=unit.Attack,Melee=true});
            }
        }
        /// <summary>敌方弹体任务：沿出手方向飞行并连续检测英雄碰撞，命中后把伤害写入待结算队列并销毁。</summary>
        [BurstCompile]
        private partial struct HostileProjectileJob : IJobEntity
        {
            public float3 Target;
            public float DeltaTime,HitRadius;
            public NativeQueue<PendingCombatDamage>.ParallelWriter Damage;
            public EntityCommandBuffer.ParallelWriter Commands;
            private void Execute(Entity entity,[ChunkIndexInQuery] int sortKey,ref LocalTransform transform,ref HostileProjectile shot)
            {
                shot.Lifetime-=DeltaTime;
                if(shot.Lifetime<=0) {Commands.DestroyEntity(sortKey,entity);return;}
                float3 end=transform.Position+shot.Direction*shot.Speed*DeltaTime;
                if(SkillTargeting.FirstContact(transform.Position,end,Target,HitRadius,out _))
                {Damage.Enqueue(new PendingCombatDamage{Source=shot.Source,Amount=shot.Damage});Commands.DestroyEntity(sortKey,entity);return;}
                transform.Position=end;
            }
        }
    }
}
