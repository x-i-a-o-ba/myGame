using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    /// <summary>怪物宿主：AI 只调度七类模块；形状与伤害复用核心。所有集合有硬预算，生命周期归战斗会话。</summary>
    public sealed class MonsterAbilities
    {
        public const int WarningLimit=64,ZoneLimit=128,ShotLimit=256,DashLimit=32;
        public struct Visual
        {
            public float3 Center,Direction;public AreaShape Shape;public float Radius,Range,HalfAngle,InnerRadius,Progress;
            public float4 Color;public bool Warning;
        }
        struct Cast
        {
            public Entity Source;public MonsterAbilityData Spec;public float3 Center,Direction;
            public float Attack,Health,At;public int ConfigId,Kind,Stage;public bool DeadSource;
        }
        struct Zone {public Cast Cast;public float Began,End,Next;public bool Environment;}
        struct Shot {public Cast Cast;public float3 Position,End;public float Remaining;}
        struct Dash {public Cast Cast;public float Remaining,TrailAt;public bool Hit;}
        readonly EntityManager manager;readonly AbilityExecution core;
        readonly List<Cast> waiting=new List<Cast>(WarningLimit);
        readonly List<Zone> zones=new List<Zone>(ZoneLimit);
        readonly List<Shot> shots=new List<Shot>(ShotLimit);
        readonly List<Dash> dashes=new List<Dash>(DashLimit);
        readonly List<Visual> visuals=new List<Visual>(WarningLimit+ZoneLimit+ShotLimit);
        public IReadOnlyList<Visual> Visuals=>visuals;
        public int ActiveWarnings=>waiting.Count;
        public int ActiveZones=>zones.Count;
        public int ActiveProjectiles=>shots.Count;
        public int CastCount {get;private set;}
        public int DeferredCount {get;private set;}
        float nextThink,nextLava;uint seed=47291;
        Entity hero;CombatSettings settings;CombatDifficultySettings difficulty;CombatDirectorState director;int population;
        public MonsterAbilities(EntityManager manager,AbilityExecution core) {this.manager=manager;this.core=core;}
        public void Clear() {waiting.Clear();zones.Clear();shots.Clear();dashes.Clear();visuals.Clear();nextThink=0;nextLava=0;CastCount=0;DeferredCount=0;}
        bool Alive(Entity e)=>manager.Exists(e)&&manager.HasComponent<AbilityUnit>(e)&&manager.GetComponentData<AbilityUnit>(e).Health>0;
        float3 Position(Entity e)=>manager.GetComponentData<LocalTransform>(e).Position;
        static bool Has(MonsterBrain b,MonsterAffix flag)=>(b.Affixes&flag)!=0;
        Cast Snapshot(Entity e,MonsterAbilityData spec,float now)
        {
            var unit=manager.GetComponentData<AbilityUnit>(e);var enemy=manager.GetComponentData<CombatEnemy>(e);
            float3 point=Position(e),direction=math.normalizesafe(Position(hero)-point,new float3(0,0,1));direction.y=0;direction=math.normalizesafe(direction);
            return new Cast{Source=e,Spec=spec,Center=spec.AtHero?Position(hero):point,Direction=direction,Attack=unit.Attack,Health=unit.MaxHealth,
                At=now+spec.Warning,ConfigId=enemy.ConfigId,Kind=enemy.Kind,Stage=enemy.Stage};
        }
        public void Step(Entity session,EntityQuery enemies,float now,float dt,ref AbilityLibrary library)
        {
            hero=session;settings=manager.GetComponentData<CombatSettings>(hero);difficulty=manager.GetComponentData<CombatDifficultySettings>(hero);
            director=manager.GetComponentData<CombatDirectorState>(hero);population=enemies.CalculateEntityCount();
            if(now>=nextThink)
            {
                nextThink=now+.1f;
                using var entities=enemies.ToEntityArray(Allocator.Temp);
                using var records=enemies.ToComponentDataArray<CombatEnemy>(Allocator.Temp);
                using var states=enemies.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
                // 首领与精英先申请预算，普通怪物超预算时延迟冷却，不产生不可见伤害。
                for(int tier=2;tier>=0;tier--) for(int index=0;index<entities.Length;index++)
                {
                    var record=records[index];if((int)record.Tier!=tier || states[index].Health<=0) continue;
                    Entity e=entities[index];if(!manager.HasComponent<MonsterBrain>(e)) continue;
                    var brain=manager.GetComponentData<MonsterBrain>(e);var unit=states[index];
                    if(brain.Affixes==MonsterAffix.None && brain.ArmoredAboveHalf==0 && (!brain.Library.IsCreated || brain.Library.Value.Abilities.Length==0)) continue;
                    if(brain.Affixes!=MonsterAffix.None || brain.ArmoredAboveHalf>0 || record.Tier==EnemyTier.Boss && record.Stage==3)
                    {Affixes(e,ref brain,ref unit,record,now);manager.SetComponentData(e,unit);}
                    if(brain.Library.IsCreated && brain.BusyUntil<=now && (unit.Controls&ControlFlags.CastBlocked)==0)
                    {
                        ref var modules=ref brain.Library.Value.Abilities;
                        for(int n=0;n<modules.Length;n++)
                        {
                            var spec=modules[n];if(spec.Template==MonsterAbilityTemplate.OnDeath || spec.MinimumStage>record.Stage) continue;
                            if(brain.ReadyAt[n]<0) brain.ReadyAt[n]=now+math.min(3,spec.Cooldown*.35f)+(e.Index%13)*.07f;
                            if(brain.ReadyAt[n]>now || math.distancesq(Position(e).xz,Position(hero).xz)>spec.TriggerRange*spec.TriggerRange) continue;
                            if(Schedule(Snapshot(e,spec,now),tier,now))
                            {brain.ReadyAt[n]=now+math.max(.2f,spec.Cooldown);brain.BusyUntil=now+spec.Warning;break;}
                        }
                    }
                    manager.SetComponentData(e,brain);
                }
            }
            for(int i=waiting.Count-1;i>=0;i--)
            {
                var cast=waiting[i];
                if(!cast.DeadSource && !Alive(cast.Source)) {waiting.RemoveAt(i);continue;}
                if(now<cast.At) continue;
                waiting.RemoveAt(i);Execute(cast,now,ref library);CastCount++;
            }
            StepDashes(now,dt,ref library);StepShots(now,dt,ref library);StepZones(now,ref library);
            if(settings.Lava && now>=nextLava)
            {
                nextLava=now+8;var random=Unity.Mathematics.Random.CreateFromIndex(seed++);
                float2 offset=random.NextFloat2Direction()*random.NextFloat(3,15);
                var spec=new MonsterAbilityData{Template=MonsterAbilityTemplate.GroundZone,Shape=AreaShape.Circle,Radius=2.2f,Range=2.2f,
                    Warning=1.2f,Duration=5,Interval=1,DamageRatio=1,Color=new float4(1,.32f,.04f,.75f),RepeatCount=1};
                var cast=new Cast{Center=Position(hero)+new float3(offset.x,0,offset.y),Direction=new float3(0,0,1),Spec=spec,Attack=15+director.Stage*10,
                    DeadSource=true,At=now+spec.Warning,ConfigId=-1};Schedule(cast,2,now);
            }
            UpdateVisuals(now);
        }
        void Affixes(Entity e,ref MonsterBrain brain,ref AbilityUnit unit,CombatEnemy record,float now)
        {
            bool fortified=Has(brain,MonsterAffix.Fortified);
            unit.SlowImmune=fortified;unit.DisplacementImmune=fortified;
            unit.DamageReduction=math.max(fortified?.3f:0,unit.Health>unit.MaxHealth*.5f?brain.ArmoredAboveHalf:0);
            if(!brain.Enraged && unit.Health<unit.MaxHealth*.5f && (Has(brain,MonsterAffix.Rage) || record.Tier==EnemyTier.Boss && record.Stage==3))
            {brain.Enraged=true;unit.BaseSpeed*=1.5f;unit.Speed*=1.5f;unit.AttackSpeedBonus+=.5f;unit.AttackSpeedMultiplier+=.5f;}
            if(Has(brain,MonsterAffix.Shield) && brain.ShieldAt<=now) {unit.Shield=unit.MaxHealth*.2f;brain.ShieldAt=now+10;}
            if(Has(brain,MonsterAffix.Summon))
            {
                if(brain.SummonAt<0) brain.SummonAt=now+8;
                if(brain.SummonAt<=now)
                {
                    var spec=new MonsterAbilityData{Template=MonsterAbilityTemplate.Summon,SummonId=brain.SummonKind,SummonCount=4,Warning=.8f,Radius=2,Range=2,
                        Color=new float4(.8f,.15f,1,.6f),RepeatCount=1};
                    if(Schedule(Snapshot(e,spec,now),1,now)) brain.SummonAt=now+8;
                }
            }
        }
        bool Schedule(Cast cast,int tier,float now)
        {
            int count=math.max(1,cast.Spec.RepeatCount),limit=tier==0?WarningLimit-16:WarningLimit;
            int dash=dashes.Count,shot=shots.Count,zone=zones.Count;
            foreach(var pending in waiting)
            {
                if(pending.Spec.Template==MonsterAbilityTemplate.Dash) dash++;
                if(pending.Spec.Template==MonsterAbilityTemplate.ProjectileBarrage) shot+=pending.Spec.ProjectileCount;
                if(pending.Spec.Template==MonsterAbilityTemplate.GroundZone || pending.Spec.Template==MonsterAbilityTemplate.Aura) zone++;
            }
            if(waiting.Count+count>limit || cast.Spec.Template==MonsterAbilityTemplate.Dash && dash+count>DashLimit ||
                cast.Spec.Template==MonsterAbilityTemplate.ProjectileBarrage && shot+count*cast.Spec.ProjectileCount>ShotLimit ||
                (cast.Spec.Template==MonsterAbilityTemplate.GroundZone || cast.Spec.Template==MonsterAbilityTemplate.Aura) && zone+count>ZoneLimit)
            {DeferredCount++;return false;}
            for(int n=0;n<count;n++)
            {
                var copy=cast;copy.At+=n*cast.Spec.RepeatInterval;
                if(count>1 && cast.Spec.RepeatOffset>0)
                {
                    float angle=n*math.PI*2/count;
                    copy.Center+=new float3(math.cos(angle),0,math.sin(angle))*cast.Spec.RepeatOffset;
                    if(cast.Spec.Shape==AreaShape.Line) copy.Direction=math.mul(quaternion.RotateY((n-(count-1)*.5f)*.35f),copy.Direction);
                }
                waiting.Add(copy);
            }
            return true;
        }
        void Execute(Cast cast,float now,ref AbilityLibrary library)
        {
            var spec=cast.Spec;
            switch(spec.Template)
            {
                case MonsterAbilityTemplate.TelegraphArea:Hit(cast,now,ref library);break;
                case MonsterAbilityTemplate.Dash:
                    if(dashes.Count<DashLimit && Alive(cast.Source))
                    {
                        dashes.Add(new Dash{Cast=cast,Remaining=spec.Range});
                        var brain=manager.GetComponentData<MonsterBrain>(cast.Source);brain.BusyUntil=now+spec.Range/spec.Speed;manager.SetComponentData(cast.Source,brain);
                    }
                    break;
                case MonsterAbilityTemplate.ProjectileBarrage:
                    for(int n=0;n<spec.ProjectileCount && shots.Count<ShotLimit;n++)
                    {
                        var shot=cast;float angle=spec.ProjectileCount<=1?0:(n/(float)(spec.Spread>=359.99f?spec.ProjectileCount:spec.ProjectileCount-1)-.5f)*math.radians(spec.Spread);
                        shot.Direction=math.mul(quaternion.RotateY(angle),cast.Direction);
                        float3 origin=Alive(cast.Source)?Position(cast.Source):cast.Center;
                        shots.Add(new Shot{Cast=shot,Position=origin,End=spec.AtPoint?cast.Center:origin+shot.Direction*spec.Range,Remaining=spec.Range});
                    }
                    break;
                case MonsterAbilityTemplate.Summon:Summon(cast,spec.SummonCount,spec.SummonId,spec.SummonHealthRatio);break;
                case MonsterAbilityTemplate.Aura:
                case MonsterAbilityTemplate.GroundZone:AddZone(cast,now,cast.ConfigId==-1);break;
                case MonsterAbilityTemplate.OnDeath:DeathAction(cast,now,ref library);break;
            }
            if(spec.TrailSeconds>0 && spec.Template==MonsterAbilityTemplate.TelegraphArea) Trail(cast,cast.Center,now);
            if(spec.Sacrifice && Alive(cast.Source))
            {
                var unit=manager.GetComponentData<AbilityUnit>(cast.Source);unit.Health=0;manager.SetComponentData(cast.Source,unit);
                var record=manager.GetComponentData<CombatEnemy>(cast.Source);record.SelfDestructed=true;manager.SetComponentData(cast.Source,record);
            }
        }
        void Hit(Cast cast,float now,ref AbilityLibrary library,float scale=1,bool environment=false)
        {
            var spec=cast.Spec;
            for(int team=environment?0:1;team<=1;team++)
            {
                using var targets=core.QueryTeam(team,cast.Center,spec.Radius,spec.Shape,cast.Direction,spec.HalfAngle,
                    relation:spec.HealMaxHealth>0?TargetRelation.Allies:TargetRelation.Enemies,range:spec.Range,innerRadius:spec.InnerRadius);
                foreach(var target in targets)
                {
                    if(spec.HealMaxHealth>0) core.Apply(cast.Source,target,-2,new EffectData{Kind=EffectKind.Heal,TargetMaxHealthRatio=spec.HealMaxHealth*scale},ref library,now);
                    else
                    {
                        core.Apply(cast.Source,target,-2,new EffectData{Kind=EffectKind.Damage,Amount=cast.Attack*spec.DamageRatio*scale},ref library,now);
                        if(spec.HasStatus) core.Apply(cast.Source,target,-2,spec.Status,ref library,now);
                        if(spec.Knockback>0 && manager.Exists(cast.Source)) core.Apply(cast.Source,target,-2,new EffectData{Kind=EffectKind.Knockback,Amount=spec.Knockback},ref library,now);
                    }
                }
            }
        }
        void AddZone(Cast cast,float now,bool environment=false)
        {if(zones.Count<ZoneLimit) zones.Add(new Zone{Cast=cast,Began=now,End=now+cast.Spec.Duration,Next=now,Environment=environment});}
        void Trail(Cast cast,float3 point,float now)
        {
            cast.Center=point;cast.Spec.Shape=AreaShape.Circle;cast.Spec.Radius=math.min(2.5f,cast.Spec.Radius);
            cast.Spec.Duration=cast.Spec.TrailSeconds;cast.Spec.DamageRatio=cast.Spec.TrailDamageRatio;cast.Spec.HealMaxHealth=0;cast.Spec.Interval=1;
            cast.Spec.TrailSeconds=0;cast.Spec.Sacrifice=false;AddZone(cast,now);
        }
        void StepZones(float now,ref AbilityLibrary library)
        {
            for(int i=zones.Count-1;i>=0;i--)
            {
                var zone=zones[i];if(now>=zone.End) {zones.RemoveAt(i);continue;}
                if(zone.Cast.Spec.Template==MonsterAbilityTemplate.Aura)
                {if(!Alive(zone.Cast.Source)) {zones.RemoveAt(i);continue;}zone.Cast.Center=Position(zone.Cast.Source);}
                if(now>=zone.Next)
                {
                    var cast=zone.Cast;
                    if(cast.Spec.ExpandingRing) {cast.Spec.Radius=math.max(.5f,cast.Spec.Radius*math.saturate((now-zone.Began)/cast.Spec.Duration));cast.Spec.InnerRadius=math.max(0,cast.Spec.Radius-1.2f);}
                    Hit(cast,now,ref library,cast.Spec.Interval,zone.Environment);zone.Next=now+cast.Spec.Interval;
                }
                zones[i]=zone;
            }
        }
        void StepDashes(float now,float dt,ref AbilityLibrary library)
        {
            for(int i=dashes.Count-1;i>=0;i--)
            {
                var dash=dashes[i];var cast=dash.Cast;
                if(!Alive(cast.Source) || dash.Remaining<=0 || (manager.GetComponentData<AbilityUnit>(cast.Source).Controls&ControlFlags.MovementBlocked)!=0)
                {dashes.RemoveAt(i);continue;}
                var transform=manager.GetComponentData<LocalTransform>(cast.Source);float3 start=transform.Position;
                float distance=math.min(dash.Remaining,cast.Spec.Speed*dt);transform.Position+=cast.Direction*distance;
                transform.Position.xz=math.clamp(transform.Position.xz,-settings.ArenaHalfSize+2,settings.ArenaHalfSize-2);
                transform.Rotation=quaternion.LookRotationSafe(cast.Direction,math.up());manager.SetComponentData(cast.Source,transform);dash.Remaining-=distance;
                if(!dash.Hit && SkillTargeting.FirstContact(start,transform.Position,Position(hero),cast.Spec.Radius+.4f,out _))
                {var hit=cast;hit.Center=Position(hero);hit.Spec.Shape=AreaShape.Circle;Hit(hit,now,ref library);dash.Hit=true;}
                if(cast.Spec.TrailSeconds>0 && now>=dash.TrailAt) {Trail(cast,transform.Position,now);dash.TrailAt=now+.35f;}
                dashes[i]=dash;
            }
        }
        void StepShots(float now,float dt,ref AbilityLibrary library)
        {
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];float3 before=shot.Position;
                float distance=math.min(shot.Cast.Spec.Speed*dt,math.distance(before.xz,shot.End.xz));
                shot.Position+=math.normalizesafe(shot.End-before)*distance;shot.Remaining-=distance;
                bool arrived=math.distancesq(shot.Position.xz,shot.End.xz)<.05f;
                bool contact=!shot.Cast.Spec.AtPoint && SkillTargeting.FirstContact(before,shot.Position,Position(hero),.65f,out _);
                if(arrived || contact || shot.Remaining<=0)
                {
                    if(contact || shot.Cast.Spec.AtPoint)
                    {var hit=shot.Cast;hit.Center=shot.Position;hit.Spec.Shape=AreaShape.Circle;Hit(hit,now,ref library);if(hit.Spec.TrailSeconds>0) Trail(hit,hit.Center,now);}
                    shots.RemoveAt(i);
                }
                else shots[i]=shot;
            }
        }
        public void OnDeath(Entity e,float now,ref AbilityLibrary library)
        {
            if(!manager.HasComponent<MonsterBrain>(e)) return;
            var brain=manager.GetComponentData<MonsterBrain>(e);if(brain.DeathHandled) return;
            brain.DeathHandled=true;manager.SetComponentData(e,brain);
            if(brain.Library.IsCreated)
                for(int n=0;n<brain.Library.Value.Abilities.Length;n++)
                {
                    var spec=brain.Library.Value.Abilities[n];if(spec.Template!=MonsterAbilityTemplate.OnDeath || brain.Summoned && spec.DeathAction==MonsterDeathAction.Split) continue;
                    var cast=Snapshot(e,spec,now);cast.DeadSource=true;
                    if(spec.Warning>0) Schedule(cast,1,now);else DeathAction(cast,now,ref library);
                }
            if(!brain.Summoned && Has(brain,MonsterAffix.Split)) Summon(Snapshot(e,default,now),3,manager.GetComponentData<CombatEnemy>(e).ConfigId,.2f);
            if(Has(brain,MonsterAffix.Elemental) && brain.ElementIsFire)
            {
                var spec=new MonsterAbilityData{Template=MonsterAbilityTemplate.GroundZone,Radius=2.5f,Range=2.5f,Duration=3,Interval=1,DamageRatio=.5f,Color=new float4(1,.2f,0,.7f)};
                AddZone(Snapshot(e,spec,now),now);
            }
        }
        void DeathAction(Cast cast,float now,ref AbilityLibrary library)
        {
            switch(cast.Spec.DeathAction)
            {
                case MonsterDeathAction.GroundZone:AddZone(cast,now);break;
                case MonsterDeathAction.Split:Summon(cast,cast.Spec.SummonCount,cast.Spec.SummonId,cast.Spec.SummonHealthRatio);break;
                case MonsterDeathAction.Explosion:Hit(cast,now,ref library);break;
                case MonsterDeathAction.Chain:
                    Hit(cast,now,ref library);
                    using(var nearby=core.QueryTeam(1,cast.Center,cast.Spec.Radius,AreaShape.Circle,cast.Direction,relation:TargetRelation.Allies,limit:16))
                        foreach(var target in nearby) if(manager.HasComponent<CombatEnemy>(target) && manager.GetComponentData<CombatEnemy>(target).ConfigId==cast.ConfigId)
                        {var unit=manager.GetComponentData<AbilityUnit>(target);unit.Health=0;manager.SetComponentData(target,unit);}
                    break;
            }
        }
        void Summon(Cast cast,int count,int configId,float healthRatio)
        {
            var kinds=manager.GetBuffer<CombatEnemyKind>(hero);int index=-1;
            for(int n=0;n<kinds.Length;n++)
                if(configId>0?kinds[n].ConfigId==configId:!kinds[n].SpecialElite && (kinds[n].Tiers&EnemyTierMask.Normal)!=0 && kinds[n].FirstSecond==0) {index=n;break;}
            if(index<0) return;
            var kind=kinds[index];count=math.min(count,math.max(0,settings.MaxAlive-population-(director.BossSpawned?0:1)));
            var random=Unity.Mathematics.Random.CreateFromIndex(seed++);
            var stats=CombatSpawnDirector.Stats(difficulty,settings,(270*(director.Stage-1)+director.StageElapsed)/60,director.Stage,EnemyTier.Normal);
            for(int n=0;n<count;n++)
            {
                var entity=manager.Instantiate(kind.Prefab);float2 point=cast.Center.xz+random.NextFloat2Direction()*random.NextFloat(1,3);
                point=math.clamp(point,-settings.ArenaHalfSize+2,settings.ArenaHalfSize-2);
                CombatSpawnDirector.Initialize(manager,entity,settings,kind,index,stats,EnemyTier.Normal,director.Stage,point,Position(hero),ref random);
                var brain=manager.GetComponentData<MonsterBrain>(entity);brain.Summoned=true;manager.SetComponentData(entity,brain);
                if(healthRatio>0) {var unit=manager.GetComponentData<AbilityUnit>(entity);unit.Health=unit.MaxHealth=math.max(1,cast.Health*healthRatio);manager.SetComponentData(entity,unit);var transform=manager.GetComponentData<LocalTransform>(entity);transform.Scale*=.55f;manager.SetComponentData(entity,transform);}
                population++;
            }
        }
        static Visual View(Cast c,bool warning,float progress)
            =>new Visual{Center=c.Center,Direction=c.Direction,Shape=c.Spec.Shape,Radius=c.Spec.Radius,Range=c.Spec.Range,HalfAngle=c.Spec.HalfAngle,InnerRadius=c.Spec.InnerRadius,Color=c.Spec.Color,Warning=warning,Progress=progress};
        void UpdateVisuals(float now)
        {
            visuals.Clear();
            foreach(var c in waiting) visuals.Add(View(c,true,math.saturate(1-(c.At-now)/math.max(.05f,c.Spec.Warning))));
            foreach(var z in zones)
            {
                var v=View(z.Cast,false,(now-z.Began)/math.max(.05f,z.End-z.Began));
                if(z.Cast.Spec.ExpandingRing) {v.Radius=math.max(.5f,v.Radius*v.Progress);v.InnerRadius=math.max(0,v.Radius-1.2f);}visuals.Add(v);
            }
            foreach(var s in shots) {var v=View(s.Cast,false,1);v.Center=s.Position;v.Shape=AreaShape.Circle;v.Radius=.25f;visuals.Add(v);}
        }
    }
}
