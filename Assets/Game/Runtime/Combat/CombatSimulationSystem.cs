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
    /// Game战斗宿主：Dota 2 式英雄指令（移动/攻击目标/攻击移动/待命/停止/拾取/丢弃）、自动攻击被动模块、多种敌人行为、死亡计数、
    /// 经验升级、金币、商店范围内的装备买卖、装备换位 / 丢弃 / 拾取、波次刷怪。英雄属性由 HeroStatsLogic 统一合成；技能投递和Buff共用Abilities核心，不按技能或英雄身份分支。
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
        /// <summary>同屏敌方弹体上限；超出时远程攻击直接结算伤害，避免弹体数量失控。</summary>
        public const int MaxHostileProjectiles=2500;
        private NativeQueue<float> pendingDamage;
        private NativeQueue<HostileShot> pendingShots;
        private AbilityExecution abilities;
        private EntityQuery enemies, projectiles, hostileProjectiles;
        public bool TryReplaceLibrary(Entity owner,BlobAssetReference<AbilityLibrary> replacement,out string failure)
        {
            Dependency.Complete(); EntityManager.CompleteAllTrackedJobs();
            return abilities.ReplaceLibrary(owner,replacement,out failure);
        }
        public BlobAssetReference<AbilityLibrary> GetLibrary(Entity owner,int version)=>abilities.GetLibrary(owner,version);
        public void ReleaseOwner(Entity owner)
        { Dependency.Complete(); abilities.ReleaseOwner(owner); }
        protected override void OnCreate()
        {
            pendingDamage=new NativeQueue<float>(Allocator.Persistent);
            pendingShots=new NativeQueue<HostileShot>(Allocator.Persistent);
            enemies=GetEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            projectiles=GetEntityQuery(typeof(CombatProjectile),typeof(LocalTransform));
            hostileProjectiles=GetEntityQuery(typeof(HostileProjectile),typeof(LocalTransform));
            abilities=new AbilityExecution(EntityManager);
            RequireForUpdate<CombatSettings>();
            RequireForUpdate<AbilityRuntime>();
        }
        protected override void OnDestroy()
        { Dependency.Complete(); abilities.Dispose(); pendingDamage.Dispose(); pendingShots.Dispose(); }

        protected override void OnUpdate()
        {
            var manager=EntityManager;
            Entity session=SystemAPI.GetSingletonEntity<CombatSettings>();
            var config=manager.GetComponentData<CombatSettings>(session);
            var player=manager.GetComponentData<CombatPlayer>(session);
            var runtime=manager.GetComponentData<AbilityRuntime>(session);
            var waveState=manager.GetComponentData<CombatWaveState>(session);
            // 胜利后冻结整局：不推进时间、不刷怪，等待玩家重开。
            if(waveState.Outcome==CombatOutcome.Victory) { ClearCommands(session); return; }
            float dt=math.min(SystemAPI.Time.DeltaTime,0.1f);
            float now=runtime.Time+dt;
            Dependency=new TickBuffs {Library=runtime.Library,Now=now,Delta=dt}.ScheduleParallel(Dependency);
            Dependency.Complete();
            var hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0) { Die(session,ref player,ref waveState,now,dt); return; }
            // 拾取 / 丢弃指令进入距离后转成装备指令，与学习技能、天赋、装备买卖一起串行结算；等级、天赋或装备变化后统一重算属性。
            var itemOrder=manager.GetComponentData<HeroOrder>(session);
            if(StepItemOrder(session,ref itemOrder,player.Position)) manager.SetComponentData(session,itemOrder);
            bool rebuild=ProcessProgression(session,ref runtime.Library.Value);
            rebuild|=ProcessEquipment(session,player.Position);
            if(rebuild) RebuildHeroStats(session,ref runtime.Library.Value);
            hero=manager.GetComponentData<AbilityUnit>(session);
            player.Elapsed+=dt;
            // 英雄指令、转身、移动与自动攻击节奏；动作锁归技能持有者，施法前后摇期间不移动、不普攻。
            var order=manager.GetComponentData<HeroOrder>(session);
            var attack=manager.GetComponentData<AutoAttackState>(session);
            var attackData=manager.GetComponentData<AutoAttackData>(session);
            bool released=StepHero(ref player,ref order,ref attack,attackData,hero,config,AbilityCastTiming.IsBusy(runtime,runtime.Time),dt);
            Entity releaseTarget=attack.Target;
            manager.SetComponentData(session,order); manager.SetComponentData(session,attack);
            manager.SetComponentData(session,LocalTransform.FromPosition(player.Position));
            var hostileCommands=new EntityCommandBuffer(Allocator.TempJob);
            Dependency=new EnemyJob {PlayerPosition=player.Position,DeltaTime=dt,Damage=pendingDamage.AsParallelWriter(),
                Shots=pendingShots.AsParallelWriter()}.ScheduleParallel(Dependency);
            Dependency=new HostileProjectileJob {Target=player.Position,DeltaTime=dt,HitRadius=HostileHitRadius,
                Damage=pendingDamage.AsParallelWriter(),Commands=hostileCommands.AsParallelWriter()}.ScheduleParallel(Dependency);
            // 此处是所有并行单位写入与串行效果写入的明确交接点。
            Dependency.Complete();
            hostileCommands.Playback(manager); hostileCommands.Dispose();
            SpawnHostileShots(config);
            ref var library=ref runtime.Library.Value;
            while(pendingDamage.TryDequeue(out float damage))
                abilities.Apply(Entity.Null,session,-1,new EffectData{Kind=EffectKind.Damage,Amount=damage},ref library,now);
            hero=manager.GetComponentData<AbilityUnit>(session);
            if(hero.Health<=0) { Die(session,ref player,ref waveState,now,dt); return; }
            abilities.RebuildIndex();
            using var destroy=new EntityCommandBuffer(Allocator.Temp);
            using(var bullets=projectiles.ToEntityArray(Allocator.Temp))
                for(int i=0;i<bullets.Length;i++)
                {
                    Entity bullet=bullets[i]; var projectile=manager.GetComponentData<CombatProjectile>(bullet);
                    var transform=manager.GetComponentData<LocalTransform>(bullet);
                    projectile.Lifetime-=dt;
                    if(projectile.Lifetime<=0 || !Alive(projectile.Target))
                    { destroy.DestroyEntity(bullet); continue; }
                    var target=manager.GetComponentData<LocalTransform>(projectile.Target);
                    float3 offset=target.Position-transform.Position; float remaining=math.length(offset);
                    if(remaining<=projectile.Speed*dt+0.35f)
                    {
                        HeroAttackHit(session,projectile.Target,projectile.Damage,projectile.Critical,projectile.CountsAsBasicAttackHit,ref library,now,ref player);
                        destroy.DestroyEntity(bullet);
                    }
                    else
                    {
                        transform.Position+=offset/remaining*projectile.Speed*dt;
                        manager.SetComponentData(bullet,transform); manager.SetComponentData(bullet,projectile);
                    }
                }
            abilities.Step(session,now,dt);
            hero=manager.GetComponentData<AbilityUnit>(session);
            runtime=manager.GetComponentData<AbilityRuntime>(session);
            // 自动攻击出手：弹道攻击发射追踪弹体，近战攻击立即结算；目标已在本帧死亡时本次攻击落空。
            // 暴击在出手时按会话种子与出手序号确定性判定，弹体携带已乘暴击倍率的伤害。
            if(released && hero.Health>0 && Alive(releaseTarget))
            {
                player.ShotsFired++;
                float damage=hero.Attack; bool critical=false;
                if(manager.HasComponent<HeroStats>(session))
                    damage*=HeroStatsLogic.CriticalMultiplier(manager.GetComponentData<HeroStats>(session).Total,waveState.Seed,player.ShotsFired,out critical);
                if(attackData.Delivery==AttackDelivery.Projectile)
                {
                    Entity bullet=destroy.Instantiate(config.ProjectilePrefab);
                    destroy.SetComponent(bullet,new CombatProjectile {Target=releaseTarget,Damage=damage,Speed=attackData.ProjectileSpeed,Lifetime=5,
                        CountsAsBasicAttackHit=attackData.CountsAsBasicAttackHit,Critical=critical});
                    destroy.SetComponent(bullet,LocalTransform.FromPositionRotationScale(player.Position+new float3(0,0.5f,0),quaternion.identity,0.27f));
                }
                else HeroAttackHit(session,releaseTarget,damage,critical,attackData.CountsAsBasicAttackHit,ref runtime.Library.Value,runtime.Time,ref player);
            }
            float experience=0,gold=0; bool victory=false;
            var waves=manager.GetComponentData<CombatWaveSettings>(session);
            using(var units=enemies.ToEntityArray(Allocator.Temp))
            using(var stats=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp))
            using(var records=enemies.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
                for(int i=0;i<units.Length;i++)
                {
                    if(stats[i].Health>0) continue;
                    var enemy=records[i];
                    // 自爆死亡不给经验和金币、不计击杀，也不会触发首领胜利。
                    if(!enemy.SelfDestructed)
                    {
                        experience+=enemy.Experience; gold+=enemy.Gold; player.Kills++;
                        if(enemy.Tier==EnemyTier.Boss && waves.FinalWave>0 && enemy.Wave==waves.FinalWave) victory=true;
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
            if(victory) waveState.Outcome=CombatOutcome.Victory;
            else if(CombatWaveDirector.ShouldSpawn(waves,waveState,player.Elapsed))
            {
                // 每帧最多刷一波，避免卡顿后集中补刷造成尖峰。实例化会使缓冲区失效，先复制种类表。
                waveState.Wave++; waveState.NextWaveAt+=waves.WaveInterval;
                using(var kinds=manager.GetBuffer<CombatEnemyKind>(session).ToNativeArray(Allocator.Temp))
                    CombatWaveDirector.SpawnWave(manager,config,waves,ref waveState,player.Position,enemies.CalculateEntityCount(),kinds);
            }
            manager.SetComponentData(session,waveState);
            player.AliveEnemies=enemies.CalculateEntityCount();
            player.ActiveProjectiles=projectiles.CalculateEntityCount()+hostileProjectiles.CalculateEntityCount()+manager.GetBuffer<SkillFlight>(session).Length;
            player.Health=hero.Health; player.MaximumHealth=hero.MaxHealth;
            manager.SetComponentData(session,player);
        }
        private void Die(Entity session,ref CombatPlayer player,ref CombatWaveState waveState,float now,float dt)
        {
            abilities.Step(session,now,dt);
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
        /// Dota 2 式英雄步进：处理新指令、目标有效性与牵引、自动索敌，再交给 <see cref="StepMotion"/> 推进转身、移动与攻击节奏。
        /// 返回 true 表示本帧攻击出手，目标为 attack.Target。
        /// </summary>
        private bool StepHero(ref CombatPlayer player,ref HeroOrder order,ref AutoAttackState attack,in AutoAttackData data,in AbilityUnit hero,
            in CombatSettings config,bool busy,float dt)
        {
            bool strafe=config.AttackWhileMoving;
            if(order.Serial!=player.HandledOrder)
            {
                player.HandledOrder=order.Serial;
                Entity ordered=order.Kind==HeroOrderKind.AttackTarget?order.Target:Entity.Null;
                // 任何新指令都会打断攻击前摇与后摇（冷却保留）；重复右键同一目标不打断正在进行的攻击。
                // 移动中攻击开启时，移动/攻击移动（含拾取、丢弃）只更换目的地：保留当前目标与进行中的攻击，按住右键拖动也能持续出手。
                bool keep=(order.Kind==HeroOrderKind.AttackTarget && ordered==attack.Target) ||
                          (strafe && (MoveLike(order.Kind) || order.Kind==HeroOrderKind.AttackMove));
                if(!keep) { AutoAttackLogic.Interrupt(ref attack); attack.Target=ordered; }
                attack.AcquireDelay=0;
            }
            float3 position=player.Position;
            // 移动中攻击：移动指令（含走去拾取、丢弃）只攻击攻击距离内的敌人（含前摇缓冲），不追击、不停下。
            bool moveOrder=MoveLike(order.Kind);
            bool moveFires=strafe && moveOrder;
            bool local=order.Kind==HeroOrderKind.Hold || moveFires;
            Entity target=moveOrder && !moveFires?Entity.Null:attack.Target;
            float3 targetPosition=default; float targetDistance=0,targetRadius=0;
            if(target!=Entity.Null && !TryTarget(target,position,out targetPosition,out targetDistance,out targetRadius)) target=Entity.Null;
            if(target==Entity.Null && order.Kind==HeroOrderKind.AttackTarget) { order.Kind=HeroOrderKind.Idle; order.Target=Entity.Null; }
            // 自动索敌得到的目标有牵引距离：超出后放弃；待命只攻击攻击距离内的目标，移动中攻击离开攻击距离即换目标。
            if(target!=Entity.Null && order.Kind!=HeroOrderKind.AttackTarget)
            {
                float leash=order.Kind==HeroOrderKind.Hold?data.Range+targetRadius+data.RangeBuffer:
                    moveFires?data.Range+targetRadius+(attack.Phase==AttackPhase.Windup?data.RangeBuffer:0):data.AcquisitionRange+LeashExtra;
                if(targetDistance>leash) target=Entity.Null;
            }
            attack.AcquireDelay=math.max(0,attack.AcquireDelay-dt);
            bool acquires=data.AutoAcquire && (order.Kind==HeroOrderKind.Idle || order.Kind==HeroOrderKind.AttackMove || order.Kind==HeroOrderKind.Hold || moveFires);
            if(target==Entity.Null && acquires && attack.AcquireDelay<=0)
            {
                target=Nearest(position,local?data.Range:data.AcquisitionRange);
                if(target==Entity.Null || !TryTarget(target,position,out targetPosition,out targetDistance,out targetRadius))
                { target=Entity.Null; attack.AcquireDelay=AcquireRetry; }
            }
            attack.Target=target;
            bool moving=moveOrder || order.Kind==HeroOrderKind.AttackMove;
            // 有目标时：攻击移动/攻击目标/待机先处理目标（追击或站定攻击）；移动中攻击则继续走向目的地。
            bool travel=moving && (target==Entity.Null || moveFires);
            if(travel && math.distance(position.xz,order.Point.xz)<=0.05f) { order.Kind=HeroOrderKind.Idle; travel=false; moving=false; }
            player.Destination=moving?order.Point:position;
            var motion=new HeroMotion{HasTarget=target!=Entity.Null,TargetPosition=targetPosition,TargetRadius=targetRadius,
                Chase=target!=Entity.Null && !local,Travel=travel,Destination=order.Point};
            return StepMotion(ref player,ref attack,data,hero,motion,strafe,config.TurnRate,config.ArenaHalfSize,busy,dt)==AutoAttackEvent.Released;
        }
        /// <summary>移动、拾取与丢弃指令途中的行为相同：走向 Point，不追击。</summary>
        public static bool MoveLike(HeroOrderKind kind)
            =>kind==HeroOrderKind.Move || kind==HeroOrderKind.PickUp || kind==HeroOrderKind.DropItem;
        /// <summary>
        /// 纯函数：按动作意图推进一帧转身、攻击节奏与位移（英雄专用，编辑器校验直接调用）。
        /// 传统模式（strafe 为假）：前摇中站定，移动前需大致转向，移动会打断后摇。
        /// 移动中攻击（strafe 为真）：前摇不定身；有目标时朝向目标、沿移动方向位移，移动不等待转身。
        /// </summary>
        public static AutoAttackEvent StepMotion(ref CombatPlayer player,ref AutoAttackState attack,in AutoAttackData data,in AbilityUnit hero,in HeroMotion motion,
            bool strafe,float turnRate,float arenaHalfSize,bool busy,float dt)
        {
            float3 position=player.Position;
            float3 desired=math.normalizesafe(new float3(player.Facing.x,0,player.Facing.z),new float3(0,0,1));
            float3 heading=desired,goal=position; bool wantMove=false,inRange=false; float stopDistance=0;
            if(motion.HasTarget)
            {
                float3 toTarget=motion.TargetPosition-position; toTarget.y=0;
                float distance=math.length(toTarget);
                float reach=data.Range+motion.TargetRadius;
                inRange=distance<=reach+(attack.Phase==AttackPhase.Windup?data.RangeBuffer:0);
                if(distance>0.000001f) desired=toTarget/distance;
                if(!inRange && motion.Chase) { goal=motion.TargetPosition; wantMove=true; stopDistance=reach*0.95f; heading=desired; }
            }
            if(!wantMove && motion.Travel)
            {
                float3 offset=motion.Destination-position; offset.y=0;
                float length=math.length(offset);
                if(length>0.05f)
                {
                    goal=motion.Destination; wantMove=true; heading=offset/length;
                    if(!motion.HasTarget) desired=heading;
                }
            }
            if(!busy) player.Facing=Turn(player.Facing,desired,turnRate*dt);
            float facing=math.dot(player.Facing,desired);
            bool attackAllowed=!busy && (hero.Controls&ControlFlags.AttackBlocked)==0;
            // 传统模式下移动会打断后摇（engage 为假）；移动中攻击时边走边打，后摇照常结束。
            bool engage=motion.HasTarget && inRange && attackAllowed && (strafe || !wantMove) &&
                        (attack.Phase==AttackPhase.Windup || facing>=AutoAttackLogic.FacingTolerance);
            var result=AutoAttackLogic.Step(ref attack,data,hero.AttackSpeedMultiplier,engage,dt);
            bool canMove=!busy && (hero.Controls&ControlFlags.MovementBlocked)==0 && (strafe || attack.Phase!=AttackPhase.Windup);
            bool aligned=strafe || math.dot(player.Facing,heading)>=MoveFacing;
            if(wantMove && canMove && aligned)
            {
                float3 offset=goal-position; offset.y=0;
                float travel=math.min(math.length(offset)-stopDistance,hero.Speed*dt);
                if(travel>0)
                {
                    float limit=math.max(1,arenaHalfSize-2);
                    position+=heading*travel;
                    position.x=math.clamp(position.x,-limit,limit); position.z=math.clamp(position.z,-limit,limit);
                    player.Position=position;
                }
            }
            return result;
        }
        /// <summary>按最大角速度把朝向转向目标方向（绕 Y 轴）；maxRadians 为 0 时瞬间转向。</summary>
        public static float3 Turn(float3 current,float3 desired,float maxRadians)
        {
            current.y=0; desired.y=0;
            current=math.normalizesafe(current,new float3(0,0,1));
            desired=math.normalizesafe(desired,current);
            if(maxRadians<=0 || math.dot(current,desired)>=math.cos(maxRadians)) return desired;
            float3 left=math.mul(quaternion.RotateY(maxRadians),current);
            float3 right=math.mul(quaternion.RotateY(-maxRadians),current);
            return math.dot(left,desired)>=math.dot(right,desired)?left:right;
        }
        /// <summary>把敌人远程出手请求实例化为敌方弹体；超过上限时直接结算伤害。</summary>
        private void SpawnHostileShots(in CombatSettings config)
        {
            int active=hostileProjectiles.CalculateEntityCount();
            using var spawn=new EntityCommandBuffer(Allocator.Temp);
            while(pendingShots.TryDequeue(out var shot))
            {
                if(config.HostileProjectilePrefab==Entity.Null || active>=MaxHostileProjectiles) { pendingDamage.Enqueue(shot.Damage); continue; }
                active++;
                Entity projectile=spawn.Instantiate(config.HostileProjectilePrefab);
                spawn.SetComponent(projectile,new HostileProjectile {Damage=shot.Damage,Speed=shot.Speed,Lifetime=4});
                spawn.SetComponent(projectile,LocalTransform.FromPositionRotationScale(shot.Origin,quaternion.identity,0.32f));
            }
            spawn.Playback(EntityManager);
        }
        private void Defeat(Entity session,ref CombatWaveState state)
        {
            ClearCommands(session);
            if(state.Outcome!=CombatOutcome.Running) return;
            state.Outcome=CombatOutcome.Defeat; EntityManager.SetComponentData(session,state);
        }
        /// <summary>胜负已定或英雄倒下时丢弃尚未处理的成长与装备指令（地上的装备保留到重开）。</summary>
        private void ClearCommands(Entity session)
        {
            EntityManager.GetBuffer<ProgressionCommand>(session).Clear();
            if(EntityManager.HasBuffer<EquipmentCommand>(session)) EntityManager.GetBuffer<EquipmentCommand>(session).Clear();
        }
        /// <summary>串行处理输入层提交的学习技能与天赋选择；结果与失败原因写回 HeroProgression。返回是否选择了天赋（需要重算属性）。</summary>
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
                else if(command.Kind==ProgressionCommandKind.ChooseTalent)
                    talent|=HeroProgressionLogic.TryChooseTalent(ref progression,command.Value);
            }
            EntityManager.GetBuffer<ProgressionCommand>(session).Clear();
            EntityManager.SetComponentData(session,progression);
            return talent;
        }
        /// <summary>拾取 / 丢弃指令推进（见 <see cref="AdvanceItemOrder"/>）；会话缺少装备数据时转为待机。返回指令是否被修改。</summary>
        private bool StepItemOrder(Entity session,ref HeroOrder order,float3 position)
        {
            if(order.Kind!=HeroOrderKind.PickUp && order.Kind!=HeroOrderKind.DropItem) return false;
            var manager=EntityManager;
            if(!manager.HasComponent<HeroEquipment>(session) || !manager.HasBuffer<EquipmentCommand>(session) || !manager.HasBuffer<GroundItem>(session))
            { order.Kind=HeroOrderKind.Idle; return true; }
            return AdvanceItemOrder(ref order,manager.GetComponentData<HeroEquipment>(session),manager.GetBuffer<GroundItem>(session),
                manager.GetBuffer<EquipmentCommand>(session),position);
        }
        /// <summary>
        /// 纯函数（编辑器校验直接调用）：拾取 / 丢弃指令（Dota 2 式）在英雄进入拾取 / 丢弃距离后写入一条装备指令并转为待机，
        /// 同一帧由 <see cref="ApplyEquipmentCommand"/> 结算；拾取时目标点跟随地上的装备，装备已不存在时直接转为待机。
        /// 其他指令不处理。返回指令是否被修改。
        /// </summary>
        public static bool AdvanceItemOrder(ref HeroOrder order,in HeroEquipment equipment,DynamicBuffer<GroundItem> ground,DynamicBuffer<EquipmentCommand> commands,float3 position)
        {
            if(order.Kind!=HeroOrderKind.PickUp && order.Kind!=HeroOrderKind.DropItem) return false;
            if(!equipment.Library.IsCreated) { order.Kind=HeroOrderKind.Idle; return true; }
            var command=new EquipmentCommand{Value=order.Value};
            if(order.Kind==HeroOrderKind.PickUp)
            {
                int index=EquipmentLogic.FindGround(ground,order.Value);
                if(index<0) { order.Kind=HeroOrderKind.Idle; return true; }
                float3 item=ground[index].Position;
                order.Point=new float3(item.x,order.Point.y,item.z);
                if(!EquipmentLogic.Within(position,item,equipment.Library.Value.PickupRange)) return true;
                command.Kind=EquipmentCommandKind.PickUp;
            }
            else
            {
                if(!EquipmentLogic.Within(position,order.Point,equipment.Library.Value.DropRange)) return false;
                command.Kind=EquipmentCommandKind.Drop; command.Argument=order.Item; command.Point=order.Point;
            }
            commands.Add(command);
            order.Kind=HeroOrderKind.Idle; order.Point=new float3(position.x,order.Point.y,position.z);
            return true;
        }
        /// <summary>
        /// 串行处理装备指令（逐条见 <see cref="ApplyEquipmentCommand"/>），结果与失败原因写回 HeroEquipment。返回装备栏是否变化（需要重算属性）。
        /// </summary>
        private bool ProcessEquipment(Entity session,float3 position)
        {
            var manager=EntityManager;
            if(!manager.HasBuffer<EquipmentCommand>(session) || !manager.HasComponent<HeroEquipment>(session)) return false;
            var commands=manager.GetBuffer<EquipmentCommand>(session);
            if(commands.Length==0) return false;
            using var pending=commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            var equipment=manager.GetComponentData<HeroEquipment>(session);
            var slots=manager.GetBuffer<EquipmentSlot>(session);
            var shop=manager.HasComponent<ShopSite>(session)?manager.GetComponentData<ShopSite>(session):default;
            bool hasGround=manager.HasBuffer<GroundItem>(session);
            var ground=hasGround?manager.GetBuffer<GroundItem>(session):default;
            bool changed=false;
            for(int i=0;i<pending.Length;i++)
            {
                var command=pending[i];
                if(!hasGround && (command.Kind==EquipmentCommandKind.Drop || command.Kind==EquipmentCommandKind.PickUp)) continue;
                changed|=ApplyEquipmentCommand(ref equipment,slots,ground,shop,command,position);
            }
            manager.SetComponentData(session,equipment);
            return changed;
        }
        /// <summary>
        /// 纯函数（编辑器校验直接调用）：结算一条装备指令。购买与出售要求英雄在商店范围内（<see cref="ShopSite"/>），否则记为 NotInShop；
        /// 换位、丢弃与拾取随时可用（丢弃 / 拾取另有距离限制）。返回装备栏是否变化。
        /// </summary>
        public static bool ApplyEquipmentCommand(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,DynamicBuffer<GroundItem> ground,in ShopSite shop,
            in EquipmentCommand command,float3 position)
        {
            switch(command.Kind)
            {
                case EquipmentCommandKind.Buy:
                    if(EquipmentLogic.InShop(shop,position)) return EquipmentLogic.TryBuy(ref equipment,slots,command.Value);
                    EquipmentLogic.Reject(ref equipment,command.Kind,command.Value,EquipmentFailure.NotInShop);
                    return false;
                case EquipmentCommandKind.Sell:
                    if(EquipmentLogic.InShop(shop,position)) return EquipmentLogic.TrySell(ref equipment,slots,command.Value);
                    EquipmentLogic.Reject(ref equipment,command.Kind,command.Value>=0 && command.Value<slots.Length?slots[command.Value].Item:-1,EquipmentFailure.NotInShop);
                    return false;
                case EquipmentCommandKind.Move: return EquipmentLogic.TryMove(ref equipment,slots,command.Value,command.Argument);
                case EquipmentCommandKind.Drop: return EquipmentLogic.TryDrop(ref equipment,slots,ground,command.Value,command.Argument,command.Point,position);
                case EquipmentCommandKind.PickUp: return EquipmentLogic.TryPickUp(ref equipment,slots,ground,command.Value,position);
                default: return false;
            }
        }
        /// <summary>等级、天赋或装备变化后统一重算英雄属性：HeroStatsLogic 合成基础值与攻击距离，再叠加 Buff。</summary>
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
            HeroStatsLogic.Rebuild(ref stats,equipment,ref progression,ref unit,ref attack);
            AbilityEffects.Recalculate(ref unit,manager.GetBuffer<ActiveBuff>(session),ref library);
            manager.SetComponentData(session,stats); manager.SetComponentData(session,progression);
            manager.SetComponentData(session,unit); manager.SetComponentData(session,attack);
        }
        /// <summary>
        /// 英雄普攻命中：结算伤害（已含暴击倍率），按实际伤害吸血，再依次施加装备栏中装备的普攻命中效果，最后记录普攻命中事件。
        /// 命中效果的作用对象为“施法者”时作用于英雄自身；目标已死亡时其余效果自动跳过。
        /// </summary>
        private void HeroAttackHit(Entity session,Entity target,float damage,bool critical,bool countsAsHit,ref AbilityLibrary library,float now,ref CombatPlayer player)
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
            if(countsAsHit) abilities.RecordBasicAttackHit(session);
            player.Hits++; if(critical) player.CriticalHits++;
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
            public float DeltaTime;
            public NativeQueue<float>.ParallelWriter Damage;
            public NativeQueue<HostileShot>.ParallelWriter Shots;
            private void Execute(ref LocalTransform transform,ref CombatEnemy enemy,ref AutoAttackState attack,ref AbilityUnit unit,
                ref URPMaterialPropertyBaseColor color,in AutoAttackData data,in EnemyBehaviorData behavior)
            {
                if(unit.Health<=0) return;
                // 受击闪白：生命下降时短暂混入白色。
                if(unit.Health<enemy.LastHealth-0.001f) enemy.Flash=FlashSeconds;
                enemy.LastHealth=unit.Health;
                enemy.Flash=math.max(0,enemy.Flash-DeltaTime);
                color.Value=math.lerp(enemy.BaseColor,new float4(1,1,1,enemy.BaseColor.w),enemy.Flash/FlashSeconds*0.75f);
                float3 offset=PlayerPosition-transform.Position; offset.y=0;
                float distance=math.length(offset);
                float3 direction=distance>0.0001f?offset/distance:new float3(0,0,1);
                bool canMove=(unit.Controls&ControlFlags.MovementBlocked)==0;
                bool canAttack=(unit.Controls&ControlFlags.AttackBlocked)==0;
                float reach=data.Range+enemy.Reach;
                float step=unit.Speed*DeltaTime;
                if(behavior.Behavior==EnemyBehavior.Charger)
                {
                    enemy.ChargeReadyIn=math.max(0,enemy.ChargeReadyIn-DeltaTime);
                    if(enemy.ChargeTimer>0)
                    {
                        // 冲锋中沿锁定方向直线突进，撞到英雄造成一次伤害；被定身或眩晕打断。
                        enemy.ChargeTimer-=DeltaTime;
                        if(!canMove) enemy.ChargeTimer=0;
                        else transform.Position+=enemy.ChargeDirection*unit.Speed*behavior.ChargeSpeedMultiplier*DeltaTime;
                        if(!enemy.ChargeHit && distance<=reach+0.3f)
                        {
                            enemy.ChargeHit=true; enemy.ChargeTimer=0;
                            if(canAttack) Damage.Enqueue(unit.Attack*behavior.ChargeDamageMultiplier);
                        }
                        transform.Rotation=quaternion.LookRotationSafe(enemy.ChargeDirection,math.up());
                        AutoAttackLogic.Step(ref attack,data,unit.AttackSpeedMultiplier,false,DeltaTime);
                        return;
                    }
                    if(canMove && enemy.ChargeReadyIn<=0 && distance<=behavior.ChargeTriggerRange && distance>reach+0.5f)
                    {
                        enemy.ChargeDirection=direction; enemy.ChargeTimer=behavior.ChargeDuration; enemy.ChargeHit=false;
                        enemy.ChargeReadyIn=behavior.ChargeCooldown+behavior.ChargeDuration;
                        AutoAttackLogic.Interrupt(ref attack);
                        transform.Rotation=quaternion.LookRotationSafe(direction,math.up());
                        return;
                    }
                }
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
                if(behavior.Behavior==EnemyBehavior.Exploder)
                {
                    // 自爆：爆炸半径内的英雄受到伤害；自身死亡，不给经验、不计击杀。
                    if(distance<=behavior.ExplosionRadius+enemy.Reach) Damage.Enqueue(unit.Attack*behavior.ExplosionDamageMultiplier);
                    enemy.SelfDestructed=true; unit.Health=0;
                }
                else if(data.Delivery==AttackDelivery.Projectile)
                    Shots.Enqueue(new HostileShot {Origin=transform.Position+new float3(0,0.4f*transform.Scale,0),Damage=unit.Attack,Speed=data.ProjectileSpeed});
                else Damage.Enqueue(unit.Attack);
            }
        }
        /// <summary>敌方弹体任务：追踪英雄位置，命中后把伤害写入待结算队列并销毁。</summary>
        [BurstCompile]
        private partial struct HostileProjectileJob : IJobEntity
        {
            public float3 Target;
            public float DeltaTime,HitRadius;
            public NativeQueue<float>.ParallelWriter Damage;
            public EntityCommandBuffer.ParallelWriter Commands;
            private void Execute(Entity entity,[ChunkIndexInQuery] int sortKey,ref LocalTransform transform,ref HostileProjectile shot)
            {
                shot.Lifetime-=DeltaTime;
                float3 offset=Target-transform.Position;
                float remaining=math.length(offset),travel=shot.Speed*DeltaTime;
                if(remaining<=travel+HitRadius) { Damage.Enqueue(shot.Damage); Commands.DestroyEntity(sortKey,entity); return; }
                if(shot.Lifetime<=0) { Commands.DestroyEntity(sortKey,entity); return; }
                transform.Position+=offset/remaining*travel;
            }
        }
    }
}
