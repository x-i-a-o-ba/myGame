using System;
using System.Collections.Generic;
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
    /// <summary>拥有Game实体、技能/成长/装备配置Blob与重开生命周期（含地上的装备与商店位置）。场景视图归场景所有。</summary>
    public sealed class GameCombatSession : IDisposable
    {
        public Entity Entity {get; private set;}
        public World World {get;}
        private BlobAssetReference<AbilityLibrary> originalLibrary;
        private BlobAssetReference<ProgressionLibrary> progressionLibrary;
        private BlobAssetReference<EquipmentLibrary> equipmentLibrary;
        private readonly List<BlobAssetReference<MonsterAbilityLibrary>> monsterLibraries=new List<BlobAssetReference<MonsterAbilityLibrary>>();
        /// <summary>本局商店出售的装备定义（只读展示用），下标与 EquipmentSlot.Item / 装备库 Blob 一致。</summary>
        public EquipmentDefinition[] Items {get; private set;}=Array.Empty<EquipmentDefinition>();
        /// <summary>本局出售返还比例（展示用；各装备的权威出售价已编译进装备库）。</summary>
        public float SellRatio {get; private set;}=0.5f;
        public BlobAssetReference<AbilityLibrary> Library => World!=null && World.IsCreated && World.EntityManager.Exists(Entity) && World.EntityManager.HasComponent<AbilityRuntime>(Entity)
            ? World.EntityManager.GetComponentData<AbilityRuntime>(Entity).Library : originalLibrary;
        public string[] SkillNames {get; private set;}
        /// <summary>本局敌人种类显示名，下标与 CombatEnemy.Kind 一致。</summary>
        public string[] EnemyNames {get; private set;}=Array.Empty<string>();
        /// <summary>本局敌人种类预制体（只读配置），下标与 <see cref="EnemyNames"/> 对齐；兼容原型对应 null。</summary>
        public EnemyArchetype[] EnemyArchetypes {get; private set;}=Array.Empty<EnemyArchetype>();
        /// <summary>本局 Buff 定义，下标与 ActiveBuff.Definition 一致（展示用）。</summary>
        public BuffDefinition[] Buffs=>activeBuffs??Array.Empty<BuffDefinition>();
        /// <summary>本局英雄自动攻击快照（展示用；权威值在会话实体的 AutoAttackData）。</summary>
        public AutoAttackData HeroAttack {get; private set;}
        /// <summary>本局技能定义副本（下标 = 技能库索引 = AbilitySlot.Definition）；配方换装后返回新定义。只读展示用，越界返回 null。</summary>
        public SkillDefinition ActiveSkill(int definition)=>activeSkills!=null && definition>=0 && definition<activeSkills.Length?activeSkills[definition]:null;
        // 由预制体子网格合并出的网格归会话对象所有：重开时复用（渲染系统可能仍持有上一局的注册），会话销毁时释放。
        private readonly Dictionary<EnemyArchetype,Mesh> meshCache=new Dictionary<EnemyArchetype,Mesh>();
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
        /// <summary>编译好的名册条目：配置校验在清空旧会话之前完成，失败时旧会话保持不变。</summary>
        private struct RosterPlan
        {
            public EnemySpawnEntry Entry; public EnemyArchetype Archetype; public Material Material;
            public EnemyBehaviorData Behavior; public AutoAttackData Attack; public EnemyRoster Roster; public MonsterAbilityData[] Modules; public EffectData Element;
        }
        public void Reset(GameCombatController config,HeroProgressionRules rules,CombatDifficultyDefinition difficulty,Mesh enemyMesh,Mesh projectileMesh)
        {
            int[] loadout=config.hero.ResolveSkills(config.skillCatalog);
            if(loadout.Length>4) throw new InvalidOperationException("当前Game输入/HUD支持4个槽，英雄槽数不能超过4。");
            if(!rules || !difficulty) throw new InvalidOperationException("Game缺少经验规则或难度曲线。");
            var waves=difficulty.ToSettings();
            if(!config.spawnDirector) throw new InvalidOperationException("Game 缺少刷怪导演配置。");
            var director=config.spawnDirector.Compile();
            var heroAttack=config.hero.ResolveAttack(config.projectileSpeed);
            var roster=PlanRoster(config.ActiveRoster,config.skillCatalog);
            bool abyss=config.Run!=null && config.Run.Abyss;
            if(abyss)
            {
                var other=config.Run.FirstMap==config.map?config.Run.SecondMap:config.Run.FirstMap;
                foreach(var extra in PlanRoster(other.enemyRoster,config.skillCatalog))
                    if(extra.Entry.specialElite || (extra.Entry.tiers&EnemyTierMask.Normal)!=0) roster.Add(extra);
            }
            var nextProgression=HeroProgressionLogic.Compile(rules,config.hero);
            BlobAssetReference<AbilityLibrary> nextLibrary;
            try { nextLibrary=config.skillCatalog.Compile(); }
            catch { nextProgression.Dispose(); throw; }
            BlobAssetReference<EquipmentLibrary> nextEquipment;
            try { nextEquipment=CompileEquipment(config.equipmentCatalog,config.skillCatalog); }
            catch { nextLibrary.Dispose(); nextProgression.Dispose(); throw; }
            for(int i=0;i<loadout.Length;i++)
                if(nextLibrary.Value.Skills[loadout[i]].Stages.Length==0)
                {
                    nextEquipment.Dispose(); nextLibrary.Dispose(); nextProgression.Dispose();
                    throw new InvalidOperationException("Game英雄必须使用组合阶段配置，旧示例配置需显式迁移。");
                }
            Clear(); originalLibrary=nextLibrary; progressionLibrary=nextProgression; equipmentLibrary=nextEquipment; HeroAttack=heroAttack;
            Items=config.equipmentCatalog && config.equipmentCatalog.items!=null?(EquipmentDefinition[])config.equipmentCatalog.items.Clone():Array.Empty<EquipmentDefinition>();
            SellRatio=config.equipmentCatalog?Mathf.Clamp01(config.equipmentCatalog.sellRatio):0.5f;
            activeSkills=new SkillDefinition[config.skillCatalog.skills.Length];
            for(int i=0;i<activeSkills.Length;i++) activeSkills[i]=UnityEngine.Object.Instantiate(config.skillCatalog.skills[i]);
            activeBuffs=config.skillCatalog.buffs;
            SkillNames=new string[loadout.Length];
            for(int i=0;i<loadout.Length;i++) SkillNames[i]=config.skillCatalog.skills[loadout[i]].displayName;
            var manager=World.EntityManager;
            Entity bullet=Rendered(projectileMesh,config.projectileMaterial);
            manager.AddComponent<CombatProjectile>(bullet); manager.AddComponent<Prefab>(bullet);
            Entity hostile=Rendered(projectileMesh,config.enemyProjectileMaterial?config.enemyProjectileMaterial:config.enemyMaterial);
            manager.AddComponent<HostileProjectile>(hostile); manager.AddComponent<Prefab>(hostile);
            // 未配置名册时的兼容原型：场景里的统一敌人网格、材质与近战数值。名册为空时它就是唯一种类。
            var legacyAttack=new AutoAttackData {Delivery=AttackDelivery.Melee,Range=config.enemyAttackRange,AcquisitionRange=config.enemyAttackRange,
                BaseAttackTime=config.enemyAttackInterval,RangeBuffer=0.5f,AutoAcquire=true};
            float4 legacyColor=LinearColor(config.enemyMaterial);
            Entity enemy=EnemyPrototype(enemyMesh,config.enemyMaterial,legacyAttack,new EnemyBehaviorData{Behavior=EnemyBehavior.Melee},legacyColor);
            var kinds=new List<CombatEnemyKind>();
            var names=new List<string>();
            var archetypes=new List<EnemyArchetype>();
            var prototypes=new Dictionary<EnemyArchetype,Entity>();
            foreach(var plan in roster)
            {
                if(!prototypes.TryGetValue(plan.Archetype,out Entity prototype))
                {
                    if(!meshCache.TryGetValue(plan.Archetype,out var mesh) || !mesh)
                    {
                        mesh=plan.Archetype.BuildMesh(out bool created);
                        if(created) meshCache[plan.Archetype]=mesh;
                    }
                    if(!mesh) { Debug.LogWarning("敌人预制体没有可用网格，改用场景统一敌人网格："+plan.Archetype.name,plan.Archetype); mesh=enemyMesh; }
                    prototype=EnemyPrototype(mesh,plan.Material,plan.Attack,plan.Behavior,LinearColor(plan.Material));
                    using(var builder=new BlobBuilder(Allocator.Temp))
                    {
                        ref var root=ref builder.ConstructRoot<MonsterAbilityLibrary>();
                        var rows=builder.Allocate(ref root.Abilities,plan.Modules.Length);
                        for(int n=0;n<plan.Modules.Length;n++) rows[n]=plan.Modules[n];
                        var blob=builder.CreateBlobAssetReference<MonsterAbilityLibrary>(Allocator.Persistent);monsterLibraries.Add(blob);
                        manager.SetComponentData(prototype,new MonsterBrain{Library=blob,SummonKind=Array.Find(plan.Roster.entries,e=>!e.specialElite && e.firstSecond==0 && (e.tiers&EnemyTierMask.Normal)!=0)?.prefab.configId??0,ArmoredAboveHalf=plan.Archetype.armoredAboveHalf,ElementIsFire=plan.Archetype.fireElement,ElementEffect=plan.Element});
                    }
                    prototypes.Add(plan.Archetype,prototype);
                }
                var archetype=plan.Archetype; var entry=plan.Entry; var rosterAsset=plan.Roster;
                Color baseColor=MaterialColor(plan.Material);
                kinds.Add(new CombatEnemyKind {Prefab=prototype,ConfigId=archetype.configId,Tiers=entry.tiers,FirstSecond=entry.firstSecond,LastSecond=entry.lastSecond,SpecialElite=entry.specialElite,
                    Weight=entry.weight*(abyss && (entry.tiers&EnemyTierMask.Normal)!=0?.5f:1),Health=archetype.healthMultiplier,Damage=archetype.damageMultiplier,Speed=archetype.speedMultiplier,
                    Experience=archetype.experienceMultiplier,Gold=archetype.goldMultiplier,Scale=archetype.bodyScale,Color=Linear(baseColor),
                    EliteColor=Linear(Color.Lerp(baseColor,rosterAsset.eliteTint,rosterAsset.eliteTintStrength)),
                    BossColor=Linear(Color.Lerp(baseColor,rosterAsset.bossTint,rosterAsset.bossTintStrength))});
                names.Add(archetype.Label); archetypes.Add(archetype);
            }
            if(kinds.Count==0)
            {
                kinds.Add(new CombatEnemyKind {Prefab=enemy,ConfigId=GameCombatController.EnemyConfigId,Tiers=EnemyTierMask.Normal|EnemyTierMask.Elite|EnemyTierMask.Boss,
                    FirstSecond=0,Weight=1,Health=1,Damage=1,Speed=1,Experience=1,Gold=1,Scale=1,Color=legacyColor,EliteColor=legacyColor,BossColor=legacyColor});
                names.Add("普通怪物"); archetypes.Add(null);
            }
            EnemyNames=names.ToArray(); EnemyArchetypes=archetypes.ToArray();
            // 敌人原型保留到重开：CombatSpawnDirector 按目标存活数持续补充。上限统一来自导演配置。
            int maxAlive=director.AliveLimit;
            float safe=Mathf.Clamp(config.safeSpawnRadius,3,config.arenaHalfSize*0.5f);
            Entity=manager.CreateEntity(typeof(CombatOwned),typeof(CombatPlayer),typeof(CombatSettings),typeof(AbilityRuntime),typeof(AbilityUnit),typeof(LocalTransform));
            manager.AddBuffer<ActiveBuff>(Entity); manager.AddBuffer<SkillCommand>(Entity); manager.AddBuffer<ScheduledSkillStage>(Entity);
            manager.AddBuffer<AbilityLink>(Entity);manager.AddBuffer<AbilityCastEvent>(Entity);manager.AddBuffer<AbilityFeedback>(Entity);
            manager.AddBuffer<SkillFlight>(Entity); manager.AddBuffer<SkillCue>(Entity); manager.AddBuffer<ProgressionCommand>(Entity);
            manager.AddComponentData(Entity,heroAttack); manager.AddComponentData(Entity,new AutoAttackState());
            manager.AddComponentData(Entity,new HeroSteering());
            manager.AddComponentData(Entity,AbilityTraits.Identity);
            var kindBuffer=manager.AddBuffer<CombatEnemyKind>(Entity);
            foreach(var kind in kinds) kindBuffer.Add(kind);
            var slots=manager.AddBuffer<AbilitySlot>(Entity);
            // Dota 2 式开局：所有技能 0 级（未学习），英雄 1 级并持有初始技能点。
            foreach(int index in loadout) slots.Add(new AbilitySlot{Definition=index,Level=0,Charges=originalLibrary.Value.Skills[index].MaxCharges});
            manager.SetComponentData(Entity,new AbilityRuntime{Library=originalLibrary});
            var hero=config.hero;
            // 英雄属性统一由 HeroStatsLogic 合成：基础 + 成长 ×（等级 - 1）+ 天赋 + 装备，开局满生命、满法力。
            var progression=HeroProgressionLogic.Create(progressionLibrary,rules.startingSkillPoints);
            var heroStats=HeroStatsLogic.Create(hero.BaseSheet(heroAttack),progressionLibrary.Value.Growth,heroAttack.AcquisitionRange,heroAttack.Range);
            var heroUnit=new AbilityUnit {Team=0,AttackSpeedMultiplier=1};
            var liveAttack=heroAttack;
            HeroStatsLogic.Rebuild(ref heroStats,default,ref progression,ref heroUnit,ref liveAttack);
            heroUnit.Health=heroUnit.MaxHealth; heroUnit.Mana=heroUnit.MaxMana;
            AbilityEffects.Recalculate(ref heroUnit,manager.GetBuffer<ActiveBuff>(Entity),ref originalLibrary.Value);
            manager.SetComponentData(Entity,heroUnit);
            manager.SetComponentData(Entity,liveAttack);
            manager.AddComponentData(Entity,progression);
            manager.AddComponentData(Entity,heroStats);
            var equipment=EquipmentLogic.Create(equipmentLibrary);
            manager.AddComponentData(Entity,equipment);
            EquipmentLogic.InitializeSlots(manager.AddBuffer<EquipmentSlot>(Entity),equipment);
            manager.AddComponentData(Entity,waves);
            manager.AddComponentData(Entity,director);
            manager.AddComponentData(Entity,CombatDirectorState.Begin(director,1,(uint)Environment.TickCount|1u));
            manager.AddComponentData(Entity,new CombatSpawnView());
            var population=manager.AddBuffer<PopulationPoint>(Entity);
            foreach(var key in config.spawnDirector.population) population.Add(new PopulationPoint{Minute=key.minute,Alive=key.alive});
            float3 origin=new float3(0,0.55f,0);
            manager.SetComponentData(Entity,LocalTransform.FromPosition(origin));
            manager.SetComponentData(Entity,new CombatPlayer{Position=origin,Destination=origin,Facing=new float3(0,0,1),Health=heroUnit.Health,MaximumHealth=heroUnit.MaxHealth});
            manager.SetComponentData(Entity,new CombatSettings{ProjectilePrefab=bullet,HostileProjectilePrefab=hostile,PlayerSpeed=hero.speed,
                EnemyAttackRange=config.enemyAttackRange,EnemyAttackDamage=difficulty.baseEnemyDamage,EnemyAttackInterval=config.enemyAttackInterval,ArenaHalfSize=config.arenaHalfSize,
                EnemyPrefab=enemy,EnemyHealth=difficulty.baseEnemyHealth,EnemySpeed=difficulty.baseEnemySpeed,SafeSpawnRadius=safe,MaxAlive=maxAlive});
        }
        /// <summary>编译装备库；未配置时使用空装备库（商店为空，装备栏 6 格）。调用方唯一拥有返回值。</summary>
        private static BlobAssetReference<EquipmentLibrary> CompileEquipment(EquipmentCatalog catalog,SkillCatalog skills)
        {
            if(catalog) return catalog.Compile(skills);
            var empty=ScriptableObject.CreateInstance<EquipmentCatalog>();
            try { empty.name="空装备库"; return empty.Compile(skills); }
            finally { if(Application.isPlaying) UnityEngine.Object.Destroy(empty); else UnityEngine.Object.DestroyImmediate(empty); }
        }
        /// <summary>校验名册全部条目并生成快照；名册为空引用时返回空列表（使用兼容原型）。</summary>
        private static List<RosterPlan> PlanRoster(EnemyRoster roster,SkillCatalog skills)
        {
            var result=new List<RosterPlan>();
            if(!roster) return result;
            string failure=roster.Validate();
            if(failure!=null) throw new InvalidOperationException("敌人名册无效："+failure);
            foreach(var entry in roster.entries)
            {
                var behavior=entry.prefab.Compile(out var attack);
                var modules=entry.prefab.abilities??Array.Empty<MonsterAbilityDefinition>();
                if(modules.Length>4) throw new InvalidOperationException("单个怪物最多装配四个技能模块。");
                var compiled=new MonsterAbilityData[modules.Length];
                for(int i=0;i<modules.Length;i++) compiled[i]=modules[i]?modules[i].Compile(skills):throw new InvalidOperationException("怪物技能引用为空。");
                result.Add(new RosterPlan{Entry=entry,Archetype=entry.prefab,Material=entry.prefab.ResolveMaterial(),Behavior=behavior,Attack=attack,Roster=roster,Modules=compiled,
                    Element=entry.prefab.elementalSlow?skills.CompileExternalEffect(entry.prefab.elementalSlow,entry.prefab.name):default});
            }
            return result;
        }
        private Entity EnemyPrototype(Mesh mesh,Material material,in AutoAttackData attack,in EnemyBehaviorData behavior,float4 color)
        {
            var manager=World.EntityManager;
            Entity enemy=Rendered(mesh,material);
            manager.AddComponent<MonsterBrain>(enemy);manager.AddComponent<CombatEnemy>(enemy); manager.AddBuffer<ActiveBuff>(enemy);
            manager.AddComponentData(enemy,new AbilityUnit {Team=1,Health=1,MaxHealth=1,AttackSpeedMultiplier=1});
            manager.AddComponentData(enemy,attack); manager.AddComponentData(enemy,new AutoAttackState());
            manager.AddComponentData(enemy,behavior);
            manager.AddComponentData(enemy,new URPMaterialPropertyBaseColor {Value=color});
            manager.AddComponent<Prefab>(enemy);
            return enemy;
        }
        private static Color MaterialColor(Material material)
            =>!material?Color.white:material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
        // 实例颜色覆盖直接进入着色器常量；线性色彩空间下需要先把材质面板里的颜色转换为线性值。
        private static float4 Linear(Color color)
        {
            var value=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
            return new float4(value.r,value.g,value.b,value.a);
        }
        private static float4 LinearColor(Material material)=>Linear(MaterialColor(material));
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
                // 说明与图标属于展示配置，配方 JSON 不携带时沿用原技能。
                if(string.IsNullOrWhiteSpace(candidate.description)) candidate.description=activeSkills[index].description;
                if(!candidate.icon) candidate.icon=activeSkills[index].icon;
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
        /// <summary>选择形态的配置事务。保持目录身份、等级与在途版本；成功后新计划由执行器接管。</summary>
        public bool TryApplyForm(int slot,SkillDefinition form,out string failure)
        {
            failure=null;
            var manager=World.EntityManager;
            if(!manager.Exists(Entity) || !form) {failure="会话或形态无效。";return false;}
            var slots=manager.GetBuffer<AbilitySlot>(Entity);
            if(slot<0 || slot>=slots.Length) {failure="技能槽不存在。";return false;}
            int index=slots[slot].Definition;
            var candidate=UnityEngine.Object.Instantiate(form);
            candidate.configId=activeSkills[index].configId;candidate.stableId=activeSkills[index].stableId;
            candidate.growth=activeSkills[index].growth.Clone();
            var catalog=ScriptableObject.CreateInstance<SkillCatalog>();
            BlobAssetReference<AbilityLibrary> replacement=default;
            try
            {
                catalog.skills=(SkillDefinition[])activeSkills.Clone();catalog.skills[index]=candidate;catalog.buffs=activeBuffs;
                replacement=catalog.Compile();
                if(!World.GetExistingSystemManaged<CombatSimulationSystem>().TryReplaceLibrary(Entity,replacement,out failure)) return false;
                replacement=default;
                UnityEngine.Object.Destroy(activeSkills[index]);activeSkills[index]=candidate;candidate=null;
                SkillNames[slot]=activeSkills[index].displayName;return true;
            }
            catch(Exception error) {failure=error.Message;return false;}
            finally
            {
                if(replacement.IsCreated) replacement.Dispose();
                if(candidate) UnityEngine.Object.Destroy(candidate);
                UnityEngine.Object.Destroy(catalog);
            }
        }
        private void Clear()
        {
            World.EntityManager.CompleteAllTrackedJobs();
            if(Entity!=Entity.Null) World.GetExistingSystemManaged<CombatSimulationSystem>()?.ReleaseOwner(Entity);
            World.EntityManager.DestroyEntity(owned);
            if(originalLibrary.IsCreated) originalLibrary.Dispose();
            if(progressionLibrary.IsCreated) progressionLibrary.Dispose();
            if(equipmentLibrary.IsCreated) equipmentLibrary.Dispose();
            foreach(var monster in monsterLibraries) if(monster.IsCreated) monster.Dispose();monsterLibraries.Clear();
            if(activeSkills!=null) foreach(var skill in activeSkills) if(skill) UnityEngine.Object.Destroy(skill);
            activeSkills=null; activeBuffs=null;
            EnemyNames=Array.Empty<string>(); EnemyArchetypes=Array.Empty<EnemyArchetype>(); Items=Array.Empty<EquipmentDefinition>();
            originalLibrary=default; progressionLibrary=default; equipmentLibrary=default; Entity=Entity.Null;
        }
        public void Dispose()
        {
            if(World!=null && World.IsCreated) { Clear(); owned.Dispose(); ReleaseMeshes(); }
            else
            {
                if(originalLibrary.IsCreated) originalLibrary.Dispose();
                if(progressionLibrary.IsCreated) progressionLibrary.Dispose();
                if(equipmentLibrary.IsCreated) equipmentLibrary.Dispose();
            foreach(var monster in monsterLibraries) if(monster.IsCreated) monster.Dispose();monsterLibraries.Clear();
                if(activeSkills!=null) foreach(var skill in activeSkills) if(skill) UnityEngine.Object.Destroy(skill);
                activeSkills=null; activeBuffs=null;
                ReleaseMeshes();
            }
        }
        private void ReleaseMeshes()
        {
            foreach(var mesh in meshCache.Values) if(mesh) UnityEngine.Object.Destroy(mesh);
            meshCache.Clear();
        }
    }
}
