using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>自动攻击被动模块、Dota 式转身与敌人名册的校验。</summary>
    public static partial class CombatValidation
    {
        private const string RosterPath="Assets/GameData/目录/敌人名册.asset";
        private const string HeroPath="Assets/GameData/英雄/2000_寒冰射手.asset";

        private static void ValidateAutoAttack()
        {
            // 时间步长与节奏都取二进制可精确表示的值，避免浮点累加误差。
            var data=new AutoAttackData{Delivery=AttackDelivery.Melee,Range=2,AcquisitionRange=10,BaseAttackTime=1,AttackPoint=0.25f,Backswing=0.5f,AutoAcquire=true};
            var state=new AutoAttackState();
            Check(AutoAttackLogic.Step(ref state,data,1,true,0.125f)==AutoAttackEvent.Started && state.Phase==AttackPhase.Windup,"Auto attack starts with an attack point windup");
            Check(AutoAttackLogic.Step(ref state,data,1,true,0.125f)==AutoAttackEvent.Released && state.Phase==AttackPhase.Backswing &&
                Mathf.Approximately(state.Cooldown,0.75f),"Attack releases after the attack point and starts the remaining interval");
            Check(Releases(data,1,4,0.125f)==4 && Releases(data,2,4,0.125f)==8,"Base attack time sets attacks per second and scales with attack speed");
            Check(Releases(data,1,4,1/30f)==4,"Attack rhythm is frame-rate independent");
            Check(Mathf.Approximately(AutoAttackLogic.AttacksPerSecond(data,2),2) && Mathf.Approximately(AutoAttackLogic.Point(data,2),0.125f),
                "Attack speed divides interval and attack point");
            state=new AutoAttackState();
            AutoAttackLogic.Step(ref state,data,1,true,0.125f);
            Check(AutoAttackLogic.Step(ref state,data,1,false,0.125f)==AutoAttackEvent.Cancelled && state.Phase==AttackPhase.Idle && state.Cooldown==0,
                "Moving during the windup cancels the attack without cooldown (attack animation cancel)");
            Check(AutoAttackLogic.Step(ref state,data,1,true,0.125f)==AutoAttackEvent.Started,"Cancelled attack can restart immediately");
            AutoAttackLogic.Step(ref state,data,1,true,0.125f);
            Check(AutoAttackLogic.Step(ref state,data,1,false,0.125f)==AutoAttackEvent.None && state.Phase==AttackPhase.Idle && state.Cooldown>0,
                "Orders cancel the backswing but keep the attack cooldown");
            AutoAttackLogic.Step(ref state,data,1,true,0.125f);
            Check(state.Phase==AttackPhase.Idle,"Cancelled backswing does not reset the attack cooldown");
            float3 turned=CombatSimulationSystem.Turn(new float3(0,0,1),new float3(1,0,0),math.radians(45));
            Check(math.abs(math.dot(turned,new float3(1,0,0))-math.cos(math.radians(45)))<1e-4f,"Turn rate limits how far the hero rotates per frame");
            Check(math.all(math.abs(CombatSimulationSystem.Turn(new float3(0,0,1),new float3(1,0,0),0)-new float3(1,0,0))<1e-5f),"Turn rate 0 turns instantly");
            using(var assets=new Resources())
            {
                var module=assets.New<AutoAttackDefinition>(); module.stableId="attack.validation"; module.range=4; module.acquisitionRange=1;
                Check(module.Compile().AcquisitionRange==4,"Acquisition range never drops below attack range");
                module.attackPoint=module.baseAttackTime+1;
                Check(Rejects(()=>module.Compile()),"Attack point longer than base attack time is rejected");
                module.attackPoint=0.3f; module.delivery=AttackDelivery.Projectile; module.projectileSpeed=0;
                Check(Rejects(()=>module.Compile()),"Projectile attack without projectile speed is rejected");
                module.delivery=(AttackDelivery)9; module.projectileSpeed=10;
                Check(Rejects(()=>module.Compile()),"Unknown attack delivery is rejected");
            }
            var hero=AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroPath);
            Check(hero && hero.basicAttack && hero.basicAttack.countsAsBasicAttackHit && hero.ResolveAttack(30).Delivery==AttackDelivery.Projectile,
                "Hero basic attack is an auto attack module that feeds basic-attack-hit skills");
        }

        private static int Releases(in AutoAttackData data,float attackSpeed,float seconds,float deltaTime)
        {
            var state=new AutoAttackState(); int released=0;
            int steps=Mathf.RoundToInt(seconds/deltaTime);
            for(int i=0;i<steps;i++) if(AutoAttackLogic.Step(ref state,data,attackSpeed,true,deltaTime)==AutoAttackEvent.Released) released++;
            return released;
        }

        private static bool Rejects(Action action)
        {
            try { action(); } catch(InvalidOperationException) { return true; }
            return false;
        }

        private static void ValidateEnemyRoster()
        {
            var objects=new List<UnityEngine.Object>();
            using(var assets=new Resources())
            try
            {
                var melee=assets.New<AutoAttackDefinition>(); melee.stableId="attack.melee"; melee.delivery=AttackDelivery.Melee; melee.range=1.25f; melee.baseAttackTime=1;
                var bolt=assets.New<AutoAttackDefinition>(); bolt.stableId="attack.bolt"; bolt.delivery=AttackDelivery.Projectile; bolt.range=12; bolt.projectileSpeed=14;
                EnemyArchetype Archetype(int id,EnemyBehavior behavior,AutoAttackDefinition attack)
                {
                    var root=new GameObject("validation enemy "+id); objects.Add(root);
                    var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(root.transform,false);
                    var archetype=root.AddComponent<EnemyArchetype>();
                    archetype.configId=id; archetype.stableId="enemy.validation_"+id; archetype.behavior=behavior; archetype.attack=attack;
                    return archetype;
                }
                var ghoul=Archetype(3901,EnemyBehavior.Melee,melee);
                var archer=Archetype(3902,EnemyBehavior.Ranged,bolt);
                var boss=Archetype(3903,EnemyBehavior.Melee,melee);
                var roster=assets.New<EnemyRoster>();
                roster.entries=new[]{
                    new EnemySpawnEntry{prefab=ghoul,weight=3,firstSecond=0,tiers=EnemyTierMask.Normal|EnemyTierMask.Elite},
                    new EnemySpawnEntry{prefab=archer,weight=1,firstSecond=3,tiers=EnemyTierMask.Normal},
                    new EnemySpawnEntry{prefab=boss,weight=1,firstSecond=0,tiers=EnemyTierMask.Boss}};
                Check(roster.Validate()==null,"Valid roster with normal, elite and boss entries passes");
                var inspector=UnityEditor.Editor.CreateEditor(roster);
                try { Check(inspector is EnemyRosterChineseInspector,"Enemy roster uses the Chinese inspector"); }
                finally { UnityEngine.Object.DestroyImmediate(inspector); }
                Check(roster.entries[1].Allows(EnemyTier.Normal,3) && !roster.entries[1].Allows(EnemyTier.Normal,2) && !roster.entries[1].Allows(EnemyTier.Elite,3),
                    "Spawn entry respects first wave and tier mask");
                archer.attack=melee;
                Check(roster.Validate()!=null,"Ranged enemy with a melee auto attack is rejected");
                archer.attack=bolt; archer.configId=3901;
                Check(roster.Validate()!=null,"Duplicate monster ids are rejected");
                archer.configId=42;
                Check(roster.Validate()!=null,"Monster id outside 3000-3999 is rejected");
                archer.configId=3902; archer.behavior=(EnemyBehavior)9;
                Check(roster.Validate()!=null,"Unknown enemy behavior is rejected");
                archer.behavior=EnemyBehavior.Ranged;
                roster.entries[2].tiers=EnemyTierMask.Normal;
                Check(roster.Validate()!=null,"Roster without a boss entry is rejected");
                roster.entries[2].tiers=EnemyTierMask.Boss;
                var mesh=ghoul.BuildMesh(out bool created);
                try { Check(mesh && created && mesh.vertexCount==24,"Prefab child meshes merge into one ECS mesh"); }
                finally { if(created) UnityEngine.Object.DestroyImmediate(mesh); }
                ValidateKindSpawning();
            }
            finally { foreach(var item in objects) if(item) UnityEngine.Object.DestroyImmediate(item); }
            var project=AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
            Check(project && project.Validate()==null,"Project enemy roster is valid");
            var ids=new HashSet<int>(); var behaviors=new HashSet<EnemyBehavior>();
            foreach(var entry in project.entries) { ids.Add(entry.prefab.configId); behaviors.Add(entry.prefab.behavior); }
            Check(ids.Count>=8 && behaviors.Count==4,"Project roster provides at least 8 enemy kinds covering every behavior");
        }

        private static void ValidateKindSpawning()
        {
            using(var world=new World("EnemyKindValidation"))
            {
                var kinds=new NativeArray<CombatEnemyKind>(3,Allocator.Temp);
                try
                {
                    var manager=world.EntityManager;
                    Entity MakePrefab()
                    {
                        var prefab=manager.CreateEntity(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform),typeof(AutoAttackState),typeof(AutoAttackData),typeof(Prefab));
                        manager.SetComponentData(prefab,new AutoAttackData{BaseAttackTime=1,Range=1,AcquisitionRange=1});
                        manager.AddBuffer<ActiveBuff>(prefab);
                        return prefab;
                    }
                    kinds[0]=new CombatEnemyKind{Prefab=MakePrefab(),ConfigId=3901,Tiers=EnemyTierMask.Normal|EnemyTierMask.Elite,FirstSecond=0,Weight=1,Health=1,Damage=1,Speed=1,Experience=1,Scale=1};
                    kinds[1]=new CombatEnemyKind{Prefab=MakePrefab(),ConfigId=3902,Tiers=EnemyTierMask.Normal,FirstSecond=3,Weight=1,Health=0.5f,Damage=1,Speed=2,Experience=1,Scale=1};
                    kinds[2]=new CombatEnemyKind{Prefab=MakePrefab(),ConfigId=3903,Tiers=EnemyTierMask.Boss,FirstSecond=0,Weight=1,Health=1,Damage=1,Speed=1,Experience=1,Scale=1.5f};
                    var random=Unity.Mathematics.Random.CreateFromIndex(11);
                    bool earlyOnlyFirst=true,laterBoth=false,laterSecond=false;
                    for(int i=0;i<64;i++)
                    {
                        earlyOnlyFirst&=CombatSpawnDirector.Pick(kinds,EnemyTier.Normal,1,ref random)==0;
                        int later=CombatSpawnDirector.Pick(kinds,EnemyTier.Normal,3,ref random);
                        laterSecond|=later==1; laterBoth|=later==0;
                    }
                    Check(earlyOnlyFirst && laterBoth && laterSecond,"Weighted pick only offers kinds whose first wave has arrived");
                    Check(CombatSpawnDirector.Pick(kinds,EnemyTier.Boss,10,ref random)==2 && CombatSpawnDirector.Pick(kinds,EnemyTier.Elite,5,ref random)==0,
                        "Boss and elite tiers pick kinds that allow the tier");
                    var difficulty=ScriptableObject.CreateInstance<CombatDifficultyDefinition>();
                    try
                    {
                        var waves=difficulty.ToSettings();
                        var combat=new CombatSettings{EnemyHealth=30,EnemySpeed=2,EnemyAttackDamage=4,EnemyAttackInterval=1,ArenaHalfSize=125,SafeSpawnRadius=30,MaxAlive=3000};
                        var directorAsset=ScriptableObject.CreateInstance<SpawnDirectorDefinition>();
                        var director=directorAsset.Compile();
                        UnityEngine.Object.DestroyImmediate(directorAsset);
                        var state=CombatDirectorState.Begin(director,2,3); state.StageElapsed=210;
                        using var storage_curve=new NativeArray<PopulationPoint>(2,Allocator.Temp); var curve=storage_curve;
                        curve[0]=new PopulationPoint{Minute=0,Alive=200}; curve[1]=new PopulationPoint{Minute=14,Alive=200};
                        var view=new CombatSpawnView{Valid=true,Min=new float2(-20),Max=new float2(20)};
                        int spawned=CombatSpawnDirector.Tick(manager,combat,waves,director,ref state,float3.zero,0,kinds,curve,view);
                        var normal=CombatSpawnDirector.Stats(waves,combat,CombatSpawnDirector.Minutes(director,2,210),2,EnemyTier.Normal);
                        using(var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit)))
                        using(var enemies=query.ToEntityArray(Allocator.Temp))
                        {
                            var seen=new HashSet<int>(); bool scaled=true,bossKind=false;
                            foreach(var enemy in enemies)
                            {
                                var data=manager.GetComponentData<CombatEnemy>(enemy); seen.Add(data.ConfigId);
                                var unit=manager.GetComponentData<AbilityUnit>(enemy);
                                if(data.Tier==EnemyTier.Normal && data.ConfigId==3902) scaled&=Mathf.Approximately(unit.MaxHealth,math.max(1,normal.Health*0.5f)) && Mathf.Approximately(unit.Speed,normal.Speed*2);
                                if(data.Tier==EnemyTier.Boss) bossKind|=data.ConfigId==3903 && state.Boss==enemy;
                            }
                            Check(spawned==enemies.Length && seen.Count==3,"Director batch spawns several eligible enemy kinds");
                            Check(scaled,"Enemy kind multiplies time-scaled health and speed");
                            Check(bossKind,"Director spawns the boss kind and tracks it for the HUD");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(difficulty); }
                }
                finally { kinds.Dispose(); }
            }
        }
    }
}
