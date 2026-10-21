using System;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>拥有Game实体、配置Blob与重开生命周期。场景视图归场景所有。</summary>
    public sealed class GameCombatSession : IDisposable
    {
        public Entity Entity {get; private set;}
        public World World {get;}
        public BlobAssetReference<AbilityLibrary> Library {get; private set;}
        public string[] SkillNames {get; private set;}
        private readonly EntityQuery owned;
        public GameCombatSession(World world)
        {
            World=world;
            using(var existing=world.EntityManager.CreateEntityQuery(typeof(CombatSettings)))
                if(!existing.IsEmptyIgnoreFilter) throw new InvalidOperationException("同一World仅允许一个Game会话。");
            using(var sample=world.EntityManager.CreateEntityQuery(typeof(AbilitySession)))
                if(!sample.IsEmptyIgnoreFilter) throw new InvalidOperationException("Game不能和旧技能示例宿主同时运行。");
            owned=world.EntityManager.CreateEntityQuery(new EntityQueryDesc {All=new[]{ComponentType.ReadOnly<CombatOwned>()},
                Options=EntityQueryOptions.IncludePrefab|EntityQueryOptions.IncludeDisabledEntities});
        }
        public void Reset(GameCombatController config,Mesh enemyMesh,Mesh projectileMesh)
        {
            int[] loadout=config.hero.ResolveSkills(config.skillCatalog);
            if(loadout.Length>4) throw new InvalidOperationException("当前Game输入/HUD支持4个槽，英雄槽数不能超过4。");
            var nextLibrary=config.skillCatalog.Compile();
            for(int i=0;i<loadout.Length;i++)
                if(nextLibrary.Value.Skills[loadout[i]].Stages.Length==0)
                { nextLibrary.Dispose(); throw new InvalidOperationException("Game英雄必须使用组合阶段配置，旧示例配置需显式迁移。"); }
            Clear(); Library=nextLibrary;
            SkillNames=new string[loadout.Length];
            for(int i=0;i<loadout.Length;i++) SkillNames[i]=config.skillCatalog.skills[loadout[i]].displayName;
            var manager=World.EntityManager;
            Entity bullet=Rendered(projectileMesh,config.projectileMaterial);
            manager.AddComponent<CombatProjectile>(bullet); manager.AddComponent<Prefab>(bullet);
            Entity enemy=Rendered(enemyMesh,config.enemyMaterial);
            manager.AddComponent<CombatEnemy>(enemy); manager.AddBuffer<ActiveBuff>(enemy);
            manager.AddComponentData(enemy,new AbilityUnit {Team=1,Health=config.enemyHealth,MaxHealth=config.enemyHealth,
                BaseSpeed=config.enemySpeed,Speed=config.enemySpeed,BaseAttack=config.enemyAttackDamage,Attack=config.enemyAttackDamage,AttackSpeedMultiplier=1});
            manager.AddComponent<Prefab>(enemy);
            int count=Mathf.Clamp(config.enemyCount,1,30000);
            float safe=Mathf.Clamp(config.safeSpawnRadius,3,config.arenaHalfSize*0.5f),outer=config.arenaHalfSize-8;
            using(var units=new NativeArray<Entity>(count,Allocator.Temp))
            {
                manager.Instantiate(enemy,units);
                for(int i=0;i<count;i++)
                {
                    float radius=math.sqrt(math.lerp(safe*safe,outer*outer,(i+0.5f)/count)),angle=i*2.39996323f;
                    manager.SetComponentData(units[i],LocalTransform.FromPositionRotationScale(new float3(math.cos(angle)*radius,0.55f,math.sin(angle)*radius),quaternion.identity,0.85f));
                    manager.SetComponentData(units[i],new CombatEnemy{AttackCooldown=(i%17)/17f*config.enemyAttackInterval});
                }
            }
            manager.DestroyEntity(enemy);
            Entity=manager.CreateEntity(typeof(CombatOwned),typeof(CombatPlayer),typeof(CombatSettings),typeof(AbilityRuntime),typeof(AbilityUnit),typeof(LocalTransform));
            manager.AddBuffer<ActiveBuff>(Entity); manager.AddBuffer<SkillCommand>(Entity); manager.AddBuffer<ScheduledSkillStage>(Entity);
            manager.AddBuffer<SkillFlight>(Entity); manager.AddBuffer<SkillCue>(Entity);
            var slots=manager.AddBuffer<AbilitySlot>(Entity);
            foreach(int index in loadout) slots.Add(new AbilitySlot{Definition=index,Level=1,Charges=Library.Value.Skills[index].MaxCharges});
            manager.SetComponentData(Entity,new AbilityRuntime{Library=Library});
            var hero=config.hero;
            manager.SetComponentData(Entity,new AbilityUnit {Team=0,Health=hero.health,MaxHealth=hero.health,Mana=100,MaxMana=100,
                BaseSpeed=hero.speed,Speed=hero.speed,BaseAttack=hero.attackDamage,Attack=hero.attackDamage,AttackSpeedMultiplier=1});
            float3 origin=new float3(0,0.55f,0);
            manager.SetComponentData(Entity,LocalTransform.FromPosition(origin));
            manager.SetComponentData(Entity,new CombatPlayer{Position=origin,Destination=origin,Facing=new float3(0,0,1),Health=hero.health,MaximumHealth=hero.health,AliveEnemies=count});
            manager.SetComponentData(Entity,new CombatSettings{ProjectilePrefab=bullet,PlayerSpeed=hero.speed,AttackRange=hero.range,
                AttackInterval=hero.attackInterval,ProjectileSpeed=config.projectileSpeed,ProjectileDamage=hero.attackDamage,
                EnemyAttackRange=config.enemyAttackRange,EnemyAttackDamage=config.enemyAttackDamage,EnemyAttackInterval=config.enemyAttackInterval,ArenaHalfSize=config.arenaHalfSize});
        }
        private Entity Rendered(Mesh mesh,Material material)
        {
            var manager=World.EntityManager;
            Entity entity=manager.CreateEntity(typeof(CombatOwned),typeof(LocalTransform),typeof(LocalToWorld));
            manager.SetComponentData(entity,LocalTransform.Identity);
            RenderMeshUtility.AddComponents(entity,manager,new RenderMeshDescription(ShadowCastingMode.Off,false),
                new RenderMeshArray(new[]{material},new[]{mesh}),MaterialMeshInfo.FromRenderMeshArrayIndices(0,0));
            return entity;
        }
        private void Clear()
        {
            World.EntityManager.CompleteAllTrackedJobs();
            World.EntityManager.DestroyEntity(owned);
            if(Library.IsCreated) Library.Dispose();
            Library=default; Entity=Entity.Null;
        }
        public void Dispose()
        {
            if(World!=null && World.IsCreated) { Clear(); owned.Dispose(); }
            else if(Library.IsCreated) Library.Dispose();
        }
    }
}
