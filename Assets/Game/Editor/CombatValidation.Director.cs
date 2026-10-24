using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        private static void ValidateDirector()
        {
            using var assets=new Resources();
            var definition=assets.New<SpawnDirectorDefinition>(); var difficulty=assets.New<CombatDifficultyDefinition>();
            var settings=definition.Compile(); var numeric=difficulty.ToSettings();
            var combat=new CombatSettings {EnemyHealth=30,EnemyAttackDamage=6,EnemySpeed=3.5f,EnemyAttackInterval=1,ArenaHalfSize=125,SafeSpawnRadius=30,MaxAlive=4000};
            using var storage_curve=new NativeArray<PopulationPoint>(definition.population.Length,Allocator.Temp); var curve=storage_curve;
            for(int i=0;i<curve.Length;i++) curve[i]=new PopulationPoint{Minute=definition.population[i].minute,Alive=definition.population[i].alive};
            var state=CombatDirectorState.Begin(settings,1,7);
            Check(CombatSpawnDirector.Target(settings,state,curve)==24,"开局目标为 60 的 40%，不是整波生成");
            state.StageElapsed=60;
            Check(CombatSpawnDirector.Target(settings,state,curve)==150,"60 秒恢复到完整目标");
            Check(Mathf.Approximately(CombatSpawnDirector.Population(curve,2),325),"目标曲线在节点间线性插值");
            Check(CombatSpawnDirector.Population(curve,50)==4000,"曲线尾部保持 4000，不无限增长");
            state=CombatDirectorState.Begin(settings,3,7); state.StageElapsed=270;
            Check(CombatSpawnDirector.Target(settings,state,curve)==4000,"第 3 关强度时间包含前两关");
            state.BossSpawned=true;
            Check(CombatSpawnDirector.Target(settings,state,curve)==2000,"首领阶段补充目标减半，不强制删除存量");
            var basic=CombatSpawnDirector.Stats(numeric,combat,4.5f,1,EnemyTier.Normal);
            Check(Mathf.Approximately(basic.Health,30*Mathf.Pow(1.15f,4.5f)) && Mathf.Approximately(basic.Speed,3.86f),"怪物生命与移速按分钟增长");
            var boss=CombatSpawnDirector.Stats(numeric,combat,9,2,EnemyTier.Boss);
            Check(boss.Health==25000 && boss.Gold==800,"第 2 关首领生命与奖励使用绝对值");
            definition.population[1]=new PopulationKey(0,150);
            Check(Rejects(()=>definition.Compile()),"拒绝重复时间节点");
            definition.population[1]=new PopulationKey(1,150); definition.aliveLimit=4001;
            Check(Rejects(()=>definition.Compile()),"拒绝未经验收的超过 4000 上限");
            difficulty.healthGrowth=0.9f;
            Check(Rejects(()=>difficulty.ToSettings()),"拒绝随时间反向衰减的难度倍率");

            var view=new CombatSpawnView{Valid=true,Min=new float2(-30,-20),Max=new float2(30,20),Heading=new float2(1,0)};
            var random=Unity.Mathematics.Random.CreateFromIndex(9);
            int front=0; bool legal=true;
            for(int i=0;i<512;i++)
            {
                bool found=CombatSpawnDirector.TryPosition(combat,settings,view,float2.zero,view.Heading,false,ref random,out var p);
                legal&=found && math.all(math.abs(p)<=122) && !(math.all(p>=view.Min-settings.CameraPadding)&&math.all(p<=view.Max+settings.CameraPadding));
                if(p.x>0) front++;
            }
            Check(legal,"512 次生成位置全部在镜头外且在地图内");
            Check(front>350,"实际移动方向获得较高生成权重");
            var edge=view; edge.Min+=new float2(110,110); edge.Max+=new float2(110,110);
            bool edgeLegal=true;
            for(int i=0;i<128;i++)
            {
                bool found=CombatSpawnDirector.TryPosition(combat,settings,edge,new float2(110),new float2(1,1),false,ref random,out var p);
                edgeLegal&=found && math.all(math.abs(p)<=122) && !(math.all(p>=edge.Min-3)&&math.all(p<=edge.Max+3));
            }
            Check(edgeLegal,"地图角落重抽可用方向，不夹回镜头内");
            var full=view; full.Min=new float2(-200); full.Max=new float2(200);
            Check(!CombatSpawnDirector.TryPosition(combat,settings,full,float2.zero,float2.zero,false,ref random,out _),"镜头覆盖全地图时拒绝突兀地在视野内刷怪");

            using var world=new World("刷怪导演验证"); var manager=world.EntityManager;
            var prefab=manager.CreateEntity(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform),typeof(Prefab));
            combat.EnemyPrefab=prefab;
            using var storage_kinds=new NativeArray<CombatEnemyKind>(2,Allocator.Temp); var kinds=storage_kinds;
            kinds[0]=CombatSpawnDirector.LegacyKind(combat);
            var special=kinds[0]; special.SpecialElite=true; special.ConfigId=3901; kinds[1]=special;
            var blocked=kinds[0]; blocked.Weight=0; kinds[0]=blocked;
            Check(CombatSpawnDirector.Pick(kinds,EnemyTier.Normal,0,ref random)==-1,"权重零与专属精英都不会被当成普通怪兜底生成");
            kinds[0]=CombatSpawnDirector.LegacyKind(combat);
            using var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            state=CombatDirectorState.Begin(settings,1,7);
            int Tick()=>CombatSpawnDirector.Tick(manager,combat,numeric,settings,ref state,float3.zero,query.CalculateEntityCount(),kinds,curve,view);
            Check(Tick()==24 && query.CalculateEntityCount()==24,"导演首批生成满足恢复比例的目标数量");
            Check(Tick()==0,"同一计时窗口不会重复补充");
            state.StageElapsed=0.25f;
            Check(Tick()<=1,"存量达到目标时没有重复整批生成");
            manager.DestroyEntity(query); state.StageElapsed=90;
            Tick();
            Check(state.ElitesSpawned==1 && state.LastBatchSpawned<=128,"首次精英到时出现且计入每批预算");
            state.StageElapsed=150; Tick();
            Check(state.ElitesSpawned==2 && state.TideStarted && state.PendingTide>0 && state.PendingTide<300,"潮涌分批兑现，精英时钟独立运行");
            int pending=state.PendingTide; state.StageElapsed+=0.25f; Tick();
            Check(state.PendingTide<pending,"潮涌余量在后续批次继续生成");
            state.ElitesSpawned=2; state.NextEliteAt=151; state.StageElapsed=151; Tick();
            bool specialFound=false;
            using(var records=query.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
                foreach(var record in records) specialFound|=record.ConfigId==3901 && record.Tier==EnemyTier.Elite;
            Check(specialFound,"每第 3 只精英使用本图专属种类");
            state.StageElapsed=210; Tick();
            Check(state.BossSpawned && state.Boss!=Entity.Null && state.PendingTide==0,"首领到时出现并结束剩余潮涌");
            Check(manager.GetComponentData<CombatEnemy>(state.Boss).ConfigId==3901,"第 1 关首领复用本图专属精英原型");
            var firstBoss=state.Boss; int elites=state.ElitesSpawned;
            state.StageElapsed=300; Tick();
            Check(state.Boss==firstBoss && state.ElitesSpawned==elites,"首领只生成一次，首领阶段不再生成精英");
            state.Outcome=CombatOutcome.Defeat; state.StageElapsed=400;
            Check(Tick()==0,"失败后不再补怪");
            manager.DestroyEntity(query); combat.MaxAlive=5;
            state=CombatDirectorState.Begin(settings,1,7); Tick();
            Check(query.CalculateEntityCount()==4,"普通补充为首领预留一格");
            state.StageElapsed=210; Tick();
            Check(query.CalculateEntityCount()==5 && state.BossSpawned,"首领不突破存活硬上限");
            state.StageElapsed=211;
            Check(Tick()==0,"硬上限满时不继续生成");

            manager.DestroyEntity(query);
            var a=manager.Instantiate(prefab); var b=manager.Instantiate(prefab);
            manager.SetComponentData(a,LocalTransform.FromPosition(float3.zero)); manager.SetComponentData(b,LocalTransform.FromPosition(float3.zero));
            manager.SetComponentData(a,new AbilityUnit{Health=1}); manager.SetComponentData(b,new AbilityUnit{Health=1});
            float2 push=EnemySeparation.PairPush(a,b,float2.zero,1);
            Check(math.lengthsq(push)>0.9f && math.lengthsq(push+EnemySeparation.PairPush(b,a,float2.zero,1))<0.0001f,"完全重叠时分离力非零且成对反向");
            using(var separation=new EnemySeparation())
            {
                separation.Prepare(2); separation.Schedule(manager,query,settings,0.1f,125,default).Complete();
                float3 pa=manager.GetComponentData<LocalTransform>(a).Position,pb=manager.GetComponentData<LocalTransform>(b).Position;
                Check(math.distance(pa,pb)>0.4f && math.length(pa)<=0.251f,"Burst 分离任务推开同点单位且遵守速度上限");
                var frozen=manager.GetComponentData<AbilityUnit>(a); frozen.Controls=ControlFlags.MovementBlocked; manager.SetComponentData(a,frozen);
                separation.Prepare(2); separation.Schedule(manager,query,settings,0.1f,125,default).Complete();
                Check(math.distancesq(pa,manager.GetComponentData<LocalTransform>(a).Position)<0.00001f,"定身单位不会被软分离自行移动");
            }
        }
    }
}
