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
    /// <summary>拥有Game实体、技能/成长配置Blob与重开生命周期。场景视图归场景所有。</summary>
    public sealed class GameCombatSession : IDisposable
    {
        public Entity Entity {get; private set;}
        public World World {get;}
        private BlobAssetReference<AbilityLibrary> originalLibrary;
        private BlobAssetReference<ProgressionLibrary> progressionLibrary;
        public BlobAssetReference<AbilityLibrary> Library => World!=null && World.IsCreated && World.EntityManager.Exists(Entity) && World.EntityManager.HasComponent<AbilityRuntime>(Entity)
            ? World.EntityManager.GetComponentData<AbilityRuntime>(Entity).Library : originalLibrary;
        public string[] SkillNames {get; private set;}
        private SkillDefinition[] activeSkills;
        private BuffDefinition[] activeBuffs;
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
        public void Reset(GameCombatController config,HeroProgressionRules rules,CombatDifficultyDefinition difficulty,Mesh enemyMesh,Mesh projectileMesh)
        {
            int[] loadout=config.hero.ResolveSkills(config.skillCatalog);
            if(loadout.Length>4) throw new InvalidOperationException("当前Game输入/HUD支持4个槽，英雄槽数不能超过4。");
            if(!rules || !difficulty) throw new InvalidOperationException("Game缺少经验规则或难度曲线。");
            var waves=difficulty.ToSettings();
            var nextProgression=HeroProgressionLogic.Compile(rules,config.hero);
            BlobAssetReference<AbilityLibrary> nextLibrary;
            try { nextLibrary=config.skillCatalog.Compile(); }
            catch { nextProgression.Dispose(); throw; }
            for(int i=0;i<loadout.Length;i++)
                if(nextLibrary.Value.Skills[loadout[i]].Stages.Length==0)
                { nextLibrary.Dispose(); nextProgression.Dispose(); throw new InvalidOperationException("Game英雄必须使用组合阶段配置，旧示例配置需显式迁移。"); }
            Clear(); originalLibrary=nextLibrary; progressionLibrary=nextProgression;
            activeSkills=new SkillDefinition[config.skillCatalog.skills.Length];
            for(int i=0;i<activeSkills.Length;i++) activeSkills[i]=UnityEngine.Object.Instantiate(config.skillCatalog.skills[i]);
            activeBuffs=config.skillCatalog.buffs;
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
            // 敌人原型保留到重开：CombatWaveDirector 按难度曲线逐波实例化，enemyCount 只作同屏存活上限。
            int maxAlive=Mathf.Clamp(config.enemyCount,1,30000);
            float safe=Mathf.Clamp(config.safeSpawnRadius,3,config.arenaHalfSize*0.5f);
            Entity=manager.CreateEntity(typeof(CombatOwned),typeof(CombatPlayer),typeof(CombatSettings),typeof(AbilityRuntime),typeof(AbilityUnit),typeof(LocalTransform));
            manager.AddBuffer<ActiveBuff>(Entity); manager.AddBuffer<SkillCommand>(Entity); manager.AddBuffer<ScheduledSkillStage>(Entity);
            manager.AddBuffer<SkillFlight>(Entity); manager.AddBuffer<SkillCue>(Entity); manager.AddBuffer<ProgressionCommand>(Entity);
            var slots=manager.AddBuffer<AbilitySlot>(Entity);
            // Dota 2 式开局：所有技能 0 级（未学习），英雄 1 级并持有初始技能点。
            foreach(int index in loadout) slots.Add(new AbilitySlot{Definition=index,Level=0,Charges=originalLibrary.Value.Skills[index].MaxCharges});
            manager.SetComponentData(Entity,new AbilityRuntime{Library=originalLibrary});
            var hero=config.hero;
            manager.SetComponentData(Entity,new AbilityUnit {Team=0,Health=hero.health,MaxHealth=hero.health,Mana=hero.baseMana,MaxMana=hero.baseMana,
                BaseSpeed=hero.speed,Speed=hero.speed,BaseAttack=hero.attackDamage,Attack=hero.attackDamage,AttackSpeedMultiplier=1,
                BaseRegen=hero.baseHealthRegen,Regen=hero.baseHealthRegen});
            manager.AddComponentData(Entity,HeroProgressionLogic.Create(progressionLibrary,rules.startingSkillPoints));
            manager.AddComponentData(Entity,waves);
            manager.AddComponentData(Entity,new CombatWaveState {NextWaveAt=waves.FirstWaveAt,Outcome=CombatOutcome.Running,
                Seed=(uint)Environment.TickCount|1u});
            float3 origin=new float3(0,0.55f,0);
            manager.SetComponentData(Entity,LocalTransform.FromPosition(origin));
            manager.SetComponentData(Entity,new CombatPlayer{Position=origin,Destination=origin,Facing=new float3(0,0,1),Health=hero.health,MaximumHealth=hero.health});
            manager.SetComponentData(Entity,new CombatSettings{ProjectilePrefab=bullet,PlayerSpeed=hero.speed,AttackRange=hero.range,
                AttackInterval=hero.attackInterval,ProjectileSpeed=config.projectileSpeed,ProjectileDamage=hero.attackDamage,
                EnemyAttackRange=config.enemyAttackRange,EnemyAttackDamage=config.enemyAttackDamage,EnemyAttackInterval=config.enemyAttackInterval,ArenaHalfSize=config.arenaHalfSize,
                EnemyPrefab=enemy,EnemyHealth=config.enemyHealth,EnemySpeed=config.enemySpeed,SafeSpawnRadius=safe,MaxAlive=maxAlive});
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
        /// <summary>玩家配方事务入口：先验证并编译整个候选库，成功后替换；不会写入项目资源。</summary>
        public bool TryApplySkillRecipe(int slot,string json,out string failure)
        {
            failure=null; SkillDefinition candidate=null; SkillCatalog catalog=null;
            BlobAssetReference<AbilityLibrary> replacement=default;
            try
            {
                if(!World.EntityManager.Exists(Entity)) throw new InvalidOperationException("战斗会话尚未准备好。");
                var slots=World.EntityManager.GetBuffer<AbilitySlot>(Entity);
                if(slot<0 || slot>=slots.Length) throw new InvalidOperationException("技能槽不存在。");
                int index=slots[slot].Definition;
                candidate=SkillRecipeJson.Import(json,activeSkills[index].modules,true);
                candidate.configId=activeSkills[index].configId; candidate.stableId=activeSkills[index].stableId;
                // 配方 JSON 只描述组合步骤；等级成长沿用原技能，避免换装后丢失学习门槛。
                candidate.growth=activeSkills[index].growth?.Clone()??new SkillLevelGrowth();
                catalog=ScriptableObject.CreateInstance<SkillCatalog>();
                catalog.skills=(SkillDefinition[])activeSkills.Clone(); catalog.skills[index]=candidate; catalog.buffs=activeBuffs;
                replacement=catalog.Compile();
                if(!World.GetExistingSystemManaged<CombatSimulationSystem>().TryReplaceLibrary(Entity,replacement,out failure)) return false;
                replacement=default;
                UnityEngine.Object.Destroy(activeSkills[index]); activeSkills[index]=candidate; candidate=null;
                for(int i=0;i<slots.Length;i++) SkillNames[i]=activeSkills[slots[i].Definition].displayName;
                return true;
            }
            catch(Exception error) { failure=error.Message; return false; }
            finally
            {
                if(replacement.IsCreated) replacement.Dispose();
                if(candidate) UnityEngine.Object.Destroy(candidate);
                if(catalog) UnityEngine.Object.Destroy(catalog);
            }
        }
        private void Clear()
        {
            World.EntityManager.CompleteAllTrackedJobs();
            if(Entity!=Entity.Null) World.GetExistingSystemManaged<CombatSimulationSystem>()?.ReleaseOwner(Entity);
            World.EntityManager.DestroyEntity(owned);
            if(originalLibrary.IsCreated) originalLibrary.Dispose();
            if(progressionLibrary.IsCreated) progressionLibrary.Dispose();
            if(activeSkills!=null) foreach(var skill in activeSkills) if(skill) UnityEngine.Object.Destroy(skill);
            activeSkills=null; activeBuffs=null;
            originalLibrary=default; progressionLibrary=default; Entity=Entity.Null;
        }
        public void Dispose()
        {
            if(World!=null && World.IsCreated) { Clear(); owned.Dispose(); }
            else
            {
                if(originalLibrary.IsCreated) originalLibrary.Dispose();
                if(progressionLibrary.IsCreated) progressionLibrary.Dispose();
                if(activeSkills!=null) foreach(var skill in activeSkills) if(skill) UnityEngine.Object.Destroy(skill);
                activeSkills=null; activeBuffs=null;
            }
        }
    }
}
