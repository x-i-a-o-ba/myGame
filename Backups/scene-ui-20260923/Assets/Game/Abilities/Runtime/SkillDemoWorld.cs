using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Abilities
{
    /// <summary>演示启动/退出适配器。只创建 AbilityOwned 实体；静态场景对象由场景管理。可不传网格用于规则测试。</summary>
    public sealed class SkillDemoWorld : IDisposable
    {
        public readonly World World;
        public Entity Session { get; private set; }
        public Entity Hero => State.Hero;
        public EntityManager Manager => World.EntityManager;
        public AbilitySession State => Manager.GetComponentData<AbilitySession>(Session);
        public readonly SkillCatalog Catalog;
        private BlobAssetReference<AbilityLibrary> library;
        private EntityQuery owned;
        public SkillDemoWorld(World world,SkillCatalog catalog)
        {
            World=world; Catalog=catalog;
            using(var existing=world.EntityManager.CreateEntityQuery(typeof(AbilitySession)))
                if(!existing.IsEmptyIgnoreFilter) throw new InvalidOperationException("同一 World 只允许一个技能演示会话。");
            library=catalog.Compile();
            owned=Manager.CreateEntityQuery(new EntityQueryDesc { All=new[]{ComponentType.ReadOnly<AbilityOwned>()},
                Options=EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities });
        }
        public void Reset(int count,Mesh mesh=null,Material enemyMaterial=null,Material allyMaterial=null)
        {
            Manager.CompleteAllTrackedJobs(); Manager.DestroyEntity(owned);
            var hero=CreateUnit(new float3(0,0.55f,0),0,0);
            var unit=Manager.GetComponentData<AbilityUnit>(hero);
            unit.BaseSpeed=unit.Speed=8; unit.BaseAttack=unit.Attack=18; unit.Health=unit.MaxHealth=300;
            var slots=Manager.AddBuffer<AbilitySlot>(hero);
            for(int i=0;i<library.Value.Skills.Length;i++) slots.Add(new AbilitySlot{Definition=i,Level=1});
            var buffs=Manager.GetBuffer<ActiveBuff>(hero);
            for(int i=0;i<library.Value.Passives.Length;i++) AbilityEffects.SetPassive(hero,i,true,ref unit,buffs,ref library.Value,0);
            Manager.SetComponentData(hero,unit);
            var prefab=CreateUnit(float3.zero,1,1);
            if(mesh && enemyMaterial) AddRendering(prefab,mesh,enemyMaterial);
            Manager.AddComponent<Prefab>(prefab);
            using(var entities=new NativeArray<Entity>(math.max(1,count),Allocator.Temp))
            {
                Manager.Instantiate(prefab,entities);
                for(int i=0;i<entities.Length;i++)
                {
                    float radius=math.sqrt(math.lerp(36,55*55,(i+0.5f)/entities.Length)); float angle=i*2.39996323f;
                    var position=new float3(math.cos(angle)*radius,0.55f,math.sin(angle)*radius);
                    Manager.SetComponentData(entities[i],LocalTransform.FromPositionRotationScale(position,quaternion.identity,0.6f));
                    var data=Manager.GetComponentData<AbilityUnit>(entities[i]); data.Destination=position; Manager.SetComponentData(entities[i],data);
                }
            }
            Manager.DestroyEntity(prefab);
            for(int i=0;i<2;i++)
            {
                var ally=CreateUnit(new float3(-3,0.55f,3+i*2),0,2);
                var data=Manager.GetComponentData<AbilityUnit>(ally); data.Destination=new float3(4,0.55f,3+i*2); Manager.SetComponentData(ally,data);
                if(mesh && allyMaterial) AddRendering(ally,mesh,allyMaterial);
            }
            Session=Manager.CreateEntity(typeof(AbilityOwned),typeof(AbilitySession));
            Manager.SetComponentData(Session,new AbilitySession{Library=library,Hero=hero,AttackRange=16,AttackInterval=0.65f,
                AutoAttack=true,ClampRange=true,AliveEnemies=count});
            Manager.AddBuffer<CastRequest>(Session); Manager.AddBuffer<ActiveArea>(Session);
        }
        public Entity CreateUnit(float3 position,int team,int kind)
        {
            var entity=Manager.CreateEntity(typeof(AbilityOwned),typeof(AbilityUnit),typeof(LocalTransform),typeof(LocalToWorld));
            Manager.SetComponentData(entity,LocalTransform.FromPositionRotationScale(position,quaternion.identity,0.7f));
            Manager.SetComponentData(entity,new AbilityUnit{Team=team,Kind=kind,Health=120,MaxHealth=120,Mana=100,MaxMana=100,
                BaseSpeed=2,Speed=2,BaseAttack=18,Attack=18,Destination=position});
            Manager.AddBuffer<ActiveBuff>(entity); return entity;
        }
        private void AddRendering(Entity entity,Mesh mesh,Material material)
        {
            RenderMeshUtility.AddComponents(entity,Manager,new RenderMeshDescription(ShadowCastingMode.Off,false),
                new RenderMeshArray(new[]{material},new[]{mesh}),MaterialMeshInfo.FromRenderMeshArrayIndices(0,0));
        }
        public void Cast(int slot,float3 point,Entity source=default)
        { Manager.GetBuffer<CastRequest>(Session).Add(new CastRequest{Source=source==Entity.Null?Hero:source,Slot=slot,Point=point}); }
        public void SetPassive(int index,bool enabled)
        {
            if(index<0 || index>=library.Value.Passives.Length) return;
            var unit=Manager.GetComponentData<AbilityUnit>(Hero);
            AbilityEffects.SetPassive(Hero,index,enabled,ref unit,Manager.GetBuffer<ActiveBuff>(Hero),ref library.Value,State.Time);
            Manager.SetComponentData(Hero,unit);
        }
        public void Dispel(Entity target)
        {
            if(!Manager.Exists(target) || !Manager.HasComponent<AbilityUnit>(target)) return;
            var unit=Manager.GetComponentData<AbilityUnit>(target);
            AbilityEffects.Dispel(ref unit,Manager.GetBuffer<ActiveBuff>(target),ref library.Value); Manager.SetComponentData(target,unit);
        }
        public void Upgrade(int slot)
        {
            var slots=Manager.GetBuffer<AbilitySlot>(Hero); if(slot<0 || slot>=slots.Length) return;
            var value=slots[slot]; value.Level=math.min(5,value.Level+1); slots[slot]=value;
        }
        public void Dispose()
        {
            if(World.IsCreated) { Manager.CompleteAllTrackedJobs(); Manager.DestroyEntity(owned); owned.Dispose(); }
            if(library.IsCreated) library.Dispose();
        }
    }
}
