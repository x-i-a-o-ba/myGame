using System;
using Roguelike.Abilities;
using Roguelike.Flow;
using Roguelike.Foundation;
using Roguelike.SkillIndicators;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 场景组合根与输入桥。英雄/技能/敌人规则来自配置，实体生命周期交给会话。
    /// 输入写入移动向量与技能意图，战斗实体负责结算；视图只消费权威状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class GameCombatController : MonoBehaviour
    {
        /// <summary>未配置敌人名册时兼容原型使用的配置编号。</summary>
        public const int EnemyConfigId=3000;
        [Header("场景资源")]
        public Camera gameCamera;
        public Material playerMaterial,enemyMaterial,projectileMaterial,groundMaterial,markerMaterial;
        [Tooltip("敌方远程弹体材质；为空时使用敌人材质。")] public Material enemyProjectileMaterial;
        [Header("战场（重开生效）")]
        [Tooltip("本场景对应的战场定义：提供敌人名册与关卡难度曲线，覆盖下方同名配置。从选择界面进入时由所选战场（必须指向本场景）替换；为空时使用下方名册与难度曲线。")]
        public GameMapDefinition map;
        [Header("英雄和技能库（重开生效）")]
        public HeroDefinition hero;
        public SkillCatalog skillCatalog;
        [Header("成长与难度（重开生效）")]
        [Tooltip("经验与等级规则：经验表、最高等级、技能点。为空时使用 十五级默认值并输出警告。")]
        public HeroProgressionRules progressionRules;
        [Tooltip("关卡难度曲线：按分钟增长的生命/伤害/移速与精英/首领数值。为空时使用默认曲线并输出警告。")]
        public CombatDifficultyDefinition difficulty;
        [Tooltip("持续补怪、潮涌、精英、首领时刻与软分离；重开生效。") ] public SpawnDirectorDefinition spawnDirector;
        [Tooltip("敌人名册：引用敌人预制体并配置出场强度时间（秒）、权重与档位。为空时只刷场景统一敌人（近战，使用下方攻击距离与间隔）。")]
        public EnemyRoster enemyRoster;
        [Header("装备与商店（重开生效）")]
        [Tooltip("装备库：营地装备候选、装备栏格数、开局金币与出售返还比例。为空时商店为空（装备栏仍为 6 格）并输出警告。")]
        public EquipmentCatalog equipmentCatalog;
        [Header("场地与敌人基础数值（重开生效）")]
        /// <summary>只读展示兼容入口，上限唯一配置在刷怪导演。</summary>
        public int enemyCount=>spawnDirector?spawnDirector.aliveLimit:0;
        [Min(30)] public float arenaHalfSize=125;
        [Min(3)] public float safeSpawnRadius=9;
        [Min(1), Tooltip("t=0普通敌人的基础生命；敌人种类再乘自身倍率，之后按难度曲线逐分钟增长。")] public float enemyHealth=30;
        [Min(0), Tooltip("t=0普通敌人的基础移动速度（米/秒）；敌人种类再乘自身倍率。")] public float enemySpeed=2;
        [Min(0.1f), Tooltip("仅未配置名册时的统一敌人使用：近战攻击距离（米）。")] public float enemyAttackRange=1.25f;
        [Min(0), Tooltip("t=0普通敌人的基础攻击伤害；敌人种类再乘自身倍率，之后按难度曲线逐分钟增长。")] public float enemyAttackDamage=4;
        [Min(0.1f), Tooltip("仅未配置名册时的统一敌人使用：攻击间隔（秒）。")] public float enemyAttackInterval=1;
        [Min(0.1f), Tooltip("英雄未配置自动攻击模块时的弹道速度（米/秒）。")] public float projectileSpeed=38;
        [Header("相机")]
        [Range(12,160)] public float cameraZoom=28;
        [Header("场景展示引用")]
        [SerializeField] private Transform playerView,destinationMarker;
        private GameObject heroVisual;
        private readonly System.Collections.Generic.List<(Roguelike.SkillIndicators.CircleIndicatorView view,float until)> skillRings=new();
        [SerializeField] private CombatHud hud;
        [SerializeField] private GameAbilityIndicator abilityIndicator;
        [SerializeField] private Mesh enemyMesh,projectileMesh;
        private GameCombatSession session;
        private EntityQuery enemyQuery;
        private bool hasEnemyQuery;
        public Material feedbackMaterial;
        private CombatFeedbackView feedbackView;
        private void OnGUI() {if(IsReady && Phase==ExpeditionPhase.Battle) feedbackView?.DrawGUI();}
        private float nextHudUpdate,smoothFrameTime,nextHover;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraSize,originalFarClip;
        private bool originalOrthographic;
        private HeroProgressionRules fallbackRules;
        private CombatDifficultyDefinition fallbackDifficulty;
        private string notice;
        private float noticeUntil;
        private int observedLevelUps,observedCommands,observedEquipment;
        private readonly CombatHudSnapshot snapshot=new CombatHudSnapshot();
        /// <summary>当前英雄自动攻击的攻击距离（米，已计入攻击距离加成）；会话未就绪时读英雄旧字段。</summary>
        public float attackRange=>IsReady?LiveAttack.Range:hero?hero.range:0;
        public float playerHealth=>hero?hero.health:0;
        public bool IsReady=>session!=null && session.World.IsCreated && session.World.EntityManager.Exists(session.Entity);
        public CombatPlayer State=>IsReady?session.World.EntityManager.GetComponentData<CombatPlayer>(session.Entity):default;
        public Entity SessionEntity=>IsReady?session.Entity:Entity.Null;
        public SkillDefinition SkillAt(int slot)=>IsReady && slot>=0 && slot<hero.skills.Length?session.ActiveSkill(session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity)[slot].Definition):hero && slot>=0 && slot<hero.skills.Length?hero.skills[slot]:null;
        public HeroProgression Progression=>IsReady?session.World.EntityManager.GetComponentData<HeroProgression>(session.Entity):default;
        public CombatDirectorState Director=>IsReady?session.World.EntityManager.GetComponentData<CombatDirectorState>(session.Entity):default;
        /// <summary>英雄自动攻击运行状态：当前目标、前后摇阶段与冷却。</summary>
        public AutoAttackState Attack=>IsReady?session.World.EntityManager.GetComponentData<AutoAttackState>(session.Entity):default;
        /// <summary>英雄当前自动攻击数据：攻击距离与索敌距离已计入属性加成（会话的 HeroAttack 是未加成的配置快照）。</summary>
        public AutoAttackData LiveAttack=>IsReady?session.World.EntityManager.GetComponentData<AutoAttackData>(session.Entity):default;
        /// <summary>英雄属性的来源拆分与合计：基础 + 成长 ×（等级 - 1）+ 天赋 + 装备，全部直接相加。</summary>
        public HeroStats Stats=>IsReady?session.World.EntityManager.GetComponentData<HeroStats>(session.Entity):default;
        /// <summary>金币与最近一次购买 / 出售的结果。</summary>
        public HeroEquipment Equipment=>IsReady?session.World.EntityManager.GetComponentData<HeroEquipment>(session.Entity):default;
        /// <summary>本局商店出售的装备；下标即购买编号，也是装备栏记录的装备下标。</summary>
        public EquipmentDefinition[] ShopItems=>IsReady?session.Items:Array.Empty<EquipmentDefinition>();
        /// <summary>装备栏第 slot 格的装备下标；空格或格不存在时为 -1。</summary>
        public int EquippedAt(int slot)
        {
            if(!IsReady) return -1;
            var slots=session.World.EntityManager.GetBuffer<EquipmentSlot>(session.Entity);
            return slot>=0 && slot<slots.Length?slots[slot].Item:-1;
        }
        /// <summary>本局名册中的敌人种类名称，下标与 CombatEnemy.Kind 一致。</summary>
        public string[] EnemyKinds=>IsReady?session.EnemyNames:Array.Empty<string>();
        public HeroProgressionRules ActiveRules=>progressionRules?progressionRules:fallbackRules;
        /// <summary>本局使用的关卡难度曲线：战场定义优先，其次本控制器的配置，最后为默认曲线。</summary>
        public CombatDifficultyDefinition ActiveDifficulty=>map && map.difficulty?map.difficulty:difficulty?difficulty:fallbackDifficulty;
        /// <summary>本局使用的敌人名册：战场定义优先，其次本控制器的配置；都为空时只刷统一敌人。</summary>
        public EnemyRoster ActiveRoster=>map && map.enemyRoster?map.enemyRoster:enemyRoster;
        private bool Running=>IsReady && State.Health>0 && Director.Outcome==CombatOutcome.Running;
        private void Start()
        {
            try
            {
                ApplySelection();
                if(!hero || !skillCatalog) throw new InvalidOperationException("Game缺少英雄或技能库配置。");
                if(!enemyMaterial || !projectileMaterial || !enemyMesh || !projectileMesh || !playerView || !destinationMarker || !hud || !hud.HasBindings || !abilityIndicator || !abilityIndicator.HasBindings)
                    throw new InvalidOperationException("Game场景展示引用不完整。");
                if(!gameCamera) gameCamera=Camera.main;
                if(!gameCamera) throw new InvalidOperationException("Game缺少相机。");
                originalCameraPosition=gameCamera.transform.position; originalCameraRotation=gameCamera.transform.rotation;
                originalCameraSize=gameCamera.orthographicSize; originalFarClip=gameCamera.farClipPlane; originalOrthographic=gameCamera.orthographic;
                var world=World.DefaultGameObjectInjectionWorld;
                if(world==null || !world.IsCreated) throw new InvalidOperationException("ECS World不可用。");
                if(!progressionRules)
                {
                    fallbackRules=ScriptableObject.CreateInstance<HeroProgressionRules>(); fallbackRules.name="默认经验与等级规则";
                    Debug.LogWarning("Game 未指定经验与等级规则，使用十五级远征经验表。",this);
                }
                if(!(map && map.difficulty) && !difficulty)
                {
                    fallbackDifficulty=ScriptableObject.CreateInstance<CombatDifficultyDefinition>(); fallbackDifficulty.name="默认关卡难度曲线";
                    // 没有难度资源的旧场景才使用控制器基础值；正式 Game 从 GameData 编译。
                    fallbackDifficulty.baseEnemyHealth=enemyHealth; fallbackDifficulty.baseEnemyDamage=enemyAttackDamage; fallbackDifficulty.baseEnemySpeed=enemySpeed;
                    Debug.LogWarning("Game 未指定关卡难度曲线，使用默认曲线。",this);
                }
                if(!ActiveRoster) Debug.LogWarning("Game 未指定敌人名册，只刷出统一敌人。",this);
                if(!equipmentCatalog) Debug.LogWarning("Game 未指定装备库，商店为空。",this);
                session=new GameCombatSession(world);
                enemyQuery=world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatEnemy>(),ComponentType.ReadOnly<LocalTransform>(),ComponentType.ReadOnly<AbilityUnit>());
                hasEnemyQuery=true;
                hud.AbilityRequested+=CastAbility;
                hud.LearnRequested+=LearnSkill;
                hud.HoverChanged+=OnHoverChanged;
                PrepareExpedition();RestartRun();
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        public void RestartRun()
        {
            Paused=false;
            if(session==null) return;
            if(heroVisual) Destroy(heroVisual);
            foreach(var renderer in playerView.GetComponentsInChildren<Renderer>(true)) renderer.enabled=!hero.visualPrefab;
            if(hero.visualPrefab) { heroVisual=Instantiate(hero.visualPrefab,playerView);heroVisual.transform.localPosition=Vector3.zero;heroVisual.transform.localRotation=Quaternion.identity; }
            feedbackView?.Dispose();feedbackView=null;
            abilityIndicator.Cancel();
            abilityIndicator.HideSelection();
            session.Reset(this,ActiveRules,ActiveDifficulty,enemyMesh,projectileMesh);
            RestoreRun();
            previousSpawnCenter=State.Position;
            destinationMarker.gameObject.SetActive(false); nextHudUpdate=0;
            observedLevelUps=0; observedCommands=0; observedEquipment=0;
            ShowNotice("WASD 移动 · Ctrl+Q/E/R/F 学习技能 · 击败首领后进入营地",6);
            Debug.Log($"GAME_STARTED maxAlive={enemyCount}; hero={hero.stableId}; map={(map?map.stableId:"none")}; composedSkills={hero.skills.Length}; enemyKinds={session.EnemyNames.Length}; directorCap={spawnDirector.aliveLimit}; shopItems={session.Items.Length}; gold={Equipment.Gold:0}");
        }
        /// <summary>
        /// 应用开局选择（GameSelection）：英雄总是替换；战场只在所选战场的场景就是本场景时替换，否则保留场景配置并警告。
        /// 直接打开战场场景（没有选择）时沿用场景上的英雄与战场。
        /// </summary>
        private void ApplySelection()
        {
            if(GameSelection.Hero) hero=GameSelection.Hero;
            var chosen=GameSelection.Map;
            if(!chosen || chosen==map) return;
            string scene;
            try { scene=ResourcePaths.Resolve(chosen.sceneId,"scene"); }
            catch(InvalidOperationException) { scene=null; }
            if(scene==gameObject.scene.name) map=chosen;
            else Debug.LogWarning($"所选战场 {chosen.Label} 属于场景 {chosen.sceneId}，与当前场景 {gameObject.scene.name} 不符，沿用场景上的战场配置。",this);
        }
        /// <summary>选择英雄配置并重新开局；不改变共享配置。选人界面可调用此边界。</summary>
        public void SelectHero(HeroDefinition selected)
        {
            if(!selected) throw new ArgumentNullException(nameof(selected));
            selected.ResolveSkills(skillCatalog); hero=selected;
            if(session!=null) RestartRun();
        }
        public bool TryApplySkillRecipe(int slot,string json,out string failure)
        {
            if(!IsReady) { failure="战斗会话尚未准备好。"; return false; }
            bool applied=session.TryApplySkillRecipe(slot,json,out failure);
            if(applied) abilityIndicator.Cancel();
            return applied;
        }
        private void Update()=>UpdateExpeditionInput();
        private Entity NearestEnemy(Vector3 point,float maxDistance)
        {
            if(!hasEnemyQuery) return Entity.Null;
            Entity best=Entity.Null; float bestDistance=maxDistance*maxDistance;
            using var units=enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms=enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var stats=enemyQuery.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            for(int i=0;i<units.Length;i++)
            {
                if(stats[i].Health<=0) continue;
                float distance=math.distancesq(((float3)point).xz,transforms[i].Position.xz);
                if(distance<bestDistance) { bestDistance=distance; best=units[i]; }
            }
            return best;
        }
        public void CastAbility(int slot)
        {
            if(!Running || Paused) return;
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length) return;
            if(slots[slot].Level<=0)
            { ShowNotice($"{session.SkillNames[slot]} 尚未学习：Ctrl+{KeyLabel(slot)}",2); return; }
            ref var skill=ref session.Library.Value.Skills[slots[slot].Definition];
            if(skill.AutoTriggerEveryHits>0) { ShowNotice("此技能通过普攻自动触发",2); return; }
            Vector3 origin=State.Position;
            if(!TryGround(Input.mousePosition,out var point)) point=origin+(Vector3)State.Facing*skill.CastRange;
            QuickCast(slot,point);
        }
        public bool QuickCast(int slot,Vector3 point)
        {
            if(!Running || Paused) return false;
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length || slots[slot].Level<=0) return false;
            ref var skill=ref session.Library.Value.Skills[slots[slot].Definition];
            if(skill.AutoTriggerEveryHits>0) return false;
            Vector3 origin=State.Position;
            point=SkillTargeting.ClampPoint(origin,point,skill.CastRange);
            Queue(slot,point,math.normalizesafe((float3)(point-origin),State.Facing));
            return true;
        }
        /// <summary>学习或升级技能槽。规则在模拟系统中权威结算，这里只预检以给出即时提示。</summary>
        public void LearnSkill(int slot)
        {
            if(!Running) return;
            var manager=session.World.EntityManager;
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length) return;
            var progression=Progression;
            var failure=HeroProgressionLogic.CanLearn(progression,ref session.Library.Value.Skills[slots[slot].Definition],slots[slot].Level);
            if(failure==ProgressionFailure.HeroLevelTooLow)
                ShowNotice($"{session.SkillNames[slot]} 需要英雄等级 {SkillLevels.RequiredHeroLevel(ref session.Library.Value.Skills[slots[slot].Definition],slots[slot].Level+1)}",2.5f);
            else if(failure!=ProgressionFailure.None) ShowNotice($"{session.SkillNames[slot]}：{HeroProgressionLogic.Describe(failure)}",2.5f);
            else ShowNotice($"{session.SkillNames[slot]} 升到 {slots[slot].Level+1} 级",2);
            manager.GetBuffer<ProgressionCommand>(session.Entity).Add(new ProgressionCommand{Kind=ProgressionCommandKind.LearnSkill,Value=slot});
        }
        // ---------- 商店与装备 ----------
        private string ItemName(int item)
        {
            var items=session.Items;
            return item>=0 && item<items.Length && items[item]?items[item].Label:"装备";
        }
        private void ShowNotice(string text,float seconds) { notice=text; noticeUntil=Time.unscaledTime+seconds; }
        private void Queue(int slot,Vector3 point,Vector3 direction)
        {
            Entity target=Entity.Null;
            var equipped=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity)[slot];
            if(session.Library.Value.Skills[equipped.Definition].Aim==AimMode.Unit) target=NearestEnemy(point,1.5f);
            session.World.EntityManager.GetBuffer<SkillCommand>(session.Entity).Add(new SkillCommand{Slot=slot,Point=point,Direction=direction,Target=target});
        }
        private bool TryGround(Vector2 screen,out Vector3 point)
        {
            point=default;
            if(screen.x<0 || screen.y<0 || screen.x>=Screen.width || screen.y>=Screen.height) return false;
            var ray=gameCamera.ScreenPointToRay(screen);
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance)) return false;
            point=ray.GetPoint(distance); return true;
        }
        private void LateUpdate()
        {
            if(!IsReady) return;
            var state=State; var manager=session.World.EntityManager;
            playerView.position=new Vector3(state.Position.x,0.95f,state.Position.z);
            if(math.lengthsq(state.Facing)>0.001f) playerView.rotation=Quaternion.LookRotation((Vector3)state.Facing);
            gameCamera.orthographicSize=cameraZoom;
            var rotation=Quaternion.Euler(58,0,0);
            gameCamera.transform.SetPositionAndRotation((Vector3)state.Position+rotation*new Vector3(0,-cameraZoom*0.13f,-300),rotation);
            UpdateSpawnView(state);
            var flights=manager.GetBuffer<SkillFlight>(session.Entity);
            for(int i=0;i<flights.Length;i++)
            {
                var flight=flights[i];
                var flightLibrary=session.World.GetExistingSystemManaged<CombatSimulationSystem>().GetLibrary(session.Entity,flight.ProgramVersion);
                bool arc=flightLibrary.Value.Skills[flight.Skill].Stages[flight.Stage].Delivery==DeliveryKind.ParabolicProjectile;
                // 抛物线的世界位置已经包含配置高度；旧投递保留原有模型离地补偿。
                Vector3 position=(Vector3)flight.Position+(arc?Vector3.zero:Vector3.up*0.5f);
                bool arrow=flightLibrary.Value.Skills[flight.Skill].Stages[flight.Stage].Delivery==DeliveryKind.LinearProjectile;
                Graphics.DrawMesh(projectileMesh,Matrix4x4.TRS(position,Quaternion.LookRotation((Vector3)flight.Direction),arrow?new Vector3(.16f,.16f,1.2f):Vector3.one*.5f),projectileMaterial,0);
            }
            if(feedbackView==null) feedbackView=new CombatFeedbackView(feedbackMaterial,gameCamera);
            feedbackView.Step(this,session);
            smoothFrameTime=Mathf.Lerp(smoothFrameTime,Time.unscaledDeltaTime,0.08f);
            if(Time.unscaledTime<nextHudUpdate) return;
            nextHudUpdate=Time.unscaledTime+0.1f;
            hud.Refresh(BuildSnapshot(state));
            PublishFormChoices();PublishExpedition();
        }
        private Vector3 previousSpawnCenter;
        private void UpdateSpawnView(in CombatPlayer state)
        {
            // 取地面与模型高度两层投影的并集；相机旋转/宽高比变化仍保持保守的不可刷矩形。
            var view=new CombatSpawnView{Min=new float2(float.MaxValue),Max=new float2(float.MinValue),Valid=true};
            for(int height=0;height<2;height++)
            {
                var plane=new Plane(Vector3.up,new Vector3(0,height*6,0));
                for(int corner=0;corner<4;corner++)
                {
                    var ray=gameCamera.ViewportPointToRay(new Vector3(corner%2,corner/2,0));
                    if(!plane.Raycast(ray,out float distance)) { view.Valid=false; continue; }
                    Vector3 point=ray.GetPoint(distance); float2 p=new float2(point.x,point.z);
                    view.Min=math.min(view.Min,p); view.Max=math.max(view.Max,p);
                }
            }
            Vector3 delta=(Vector3)state.Position-previousSpawnCenter; previousSpawnCenter=state.Position;
            view.Heading=math.normalizesafe(new float2(delta.x,delta.z));
            session.World.EntityManager.SetComponentData(session.Entity,view);
        }

        private bool Alive(Entity entity)
        {
            if(entity==Entity.Null || !IsReady) return false;
            var manager=session.World.EntityManager;
            return manager.Exists(entity) && manager.HasComponent<AbilityUnit>(entity) && manager.HasComponent<LocalTransform>(entity) &&
                   manager.GetComponentData<AbilityUnit>(entity).Health>0;
        }
        /// <summary>组装 HUD 展示快照：只读 ECS 状态与配置，不写入任何规则数据。</summary>
        private CombatHudSnapshot BuildSnapshot(in CombatPlayer state)
        {
            var manager=session.World.EntityManager;
            var progression=Progression; var wave=Director; var s=snapshot;
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);
            var runtime=manager.GetComponentData<AbilityRuntime>(session.Entity);
            var attackState=manager.GetComponentData<AutoAttackState>(session.Entity);
            var attackData=manager.GetComponentData<AutoAttackData>(session.Entity);
            var heroStats=manager.GetComponentData<HeroStats>(session.Entity);
            var equipment=manager.GetComponentData<HeroEquipment>(session.Entity);
            var equipped=manager.GetBuffer<EquipmentSlot>(session.Entity);
            s.Outcome=state.Health<=0?CombatOutcome.Defeat:wave.Outcome;
            s.HeroName=string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName; s.Portrait=hero.portrait;
            s.MapName=map?map.Label:"";
            s.Level=progression.Level; s.SkillPoints=progression.SkillPoints;
            s.Health=state.Health; s.MaximumHealth=state.MaximumHealth; s.Regen=unit.Regen; s.Mana=unit.Mana; s.MaximumMana=unit.MaxMana;
            s.ManaRegen=AbilityEffects.BaseManaRegen+unit.ManaRegenBonus;
            s.AttackDamage=unit.Attack; s.AttacksPerSecond=AutoAttackLogic.AttacksPerSecond(attackData,unit.AttackSpeedMultiplier);
            s.AttackRange=attackData.Range; s.MoveSpeed=unit.Speed;
            s.SkillAmplify=unit.SkillAmplify*100; s.CooldownReduction=unit.CooldownReduction*100; s.DamageReduction=math.min(.7f,unit.DamageReduction+unit.BuffDamageReduction)*100;
            BuildEquipment(s,equipment,equipped);
            s.Elapsed=wave.StageElapsed; s.Kills=state.Kills; s.Alive=state.AliveEnemies; s.Projectiles=state.ActiveProjectiles;
            s.Fps=smoothFrameTime>0?1/smoothFrameTime:0; s.Stage=wave.Stage;
            var basic=hero.basicAttack;
            s.PassiveName=basic?basic.Label:"普通攻击"; s.PassiveIcon=basic?basic.icon:null;
            s.PassiveStatus=attackState.Phase==AttackPhase.Windup?"前摇":attackState.Phase==AttackPhase.Backswing?"后摇":Alive(attackState.Target)?"攻击中":"索敌";
            s.ExperienceFraction=1; s.ExperienceText="已满级";
            if(progression.Library.IsCreated)
            {
                float2 progress=HeroProgressionLogic.LevelProgress(progression);
                s.ExperienceFraction=progress.y>0?progress.x/progress.y:1;
                s.ExperienceText=progress.y>0?$"经验 {progress.x:0} / {progress.y:0}":"已满级";
            }
            var settings=manager.GetComponentData<SpawnDirectorSettings>(session.Entity);
            s.SpawnStatus=wave.StageElapsed<wave.TideNoticeUntil?"潮涌来袭！":wave.BossEnraged?"首领狂暴 · 伤害翻倍":wave.BossSpawned?"击败首领完成本关":
                $"目标 {wave.TargetAlive}  ·  首领 {Mathf.Max(0,settings.BossAt-wave.StageElapsed):0} 秒";
            s.BossAlive=Alive(wave.Boss) && manager.HasComponent<CombatEnemy>(wave.Boss);
            if(s.BossAlive)
            {
                var boss=manager.GetComponentData<AbilityUnit>(wave.Boss); var record=manager.GetComponentData<CombatEnemy>(wave.Boss);
                string name=record.Kind>=0 && record.Kind<session.EnemyNames.Length?session.EnemyNames[record.Kind]:"首领";
                s.BossName=$"首领 · {name}（第 {record.Stage} 关）"; s.BossFraction=boss.Health/Mathf.Max(1,boss.MaxHealth);
            }
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            float now=runtime.Time; bool busy=AbilityCastTiming.IsBusy(runtime,now);
            for(int i=0;i<s.Abilities.Length;i++)
            {
                ref var view=ref s.Abilities[i];
                view=default; view.Key=KeyLabel(i);
                if(i>=slots.Length) { view.Name="空槽"; continue; }
                var slot=slots[i]; ref var skill=ref session.Library.Value.Skills[slot.Definition];
                var definition=SkillAt(i);
                view.Name=session.SkillNames[i]; view.Icon=definition?definition.icon:null;
                view.Level=slot.Level; view.MaxLevel=SkillLevels.MaxLevel(ref skill);
                view.Learnable=HeroProgressionLogic.CanLearn(progression,ref skill,slot.Level)==ProgressionFailure.None;
                float mana=SkillLevels.ManaCost(ref skill,math.max(1,slot.Level));
                view.ManaText=mana>0?$"{mana:0}":"";
                view.NoMana=slot.Level>0 && mana>unit.Mana;
                if(slot.Level<=0) { view.Status="未学习"; continue; }
                float duration=skill.Cooldown*SkillLevels.CooldownScale(ref skill,slot.Level,unit);
                if(slot.ReadyAt>now)
                {
                    view.Cooldown=duration>0?Mathf.Clamp01((slot.ReadyAt-now)/duration):1;
                    view.CooldownText=slot.ReadyAt-now>=10?$"{slot.ReadyAt-now:0}":$"{slot.ReadyAt-now:0.0}";
                }
                else if(slot.Charges<=0)
                {
                    view.Cooldown=1; view.CooldownText=$"{Mathf.Max(0,slot.RechargeAt-now):0.0}";
                }
                if(skill.RequiredHits>slot.HitStacks) view.Status=$"{slot.HitStacks}/{skill.RequiredHits} 层";
                else if(skill.MaxCharges>1) view.Status=$"充能 {slot.Charges}";
                if(busy && runtime.CastSlot==i) view.Status=now<runtime.ReleaseAt?"前摇":"后摇";
            }
            if(progression.LevelUpSerial!=observedLevelUps)
            { observedLevelUps=progression.LevelUpSerial; ShowNotice($"升到 {progression.Level} 级！"+(progression.SkillPoints>0?"按 Ctrl+Q/E/R/F 学习技能":""),3); }
            if(progression.CommandSerial!=observedCommands)
            {
                observedCommands=progression.CommandSerial;
                if(progression.LastFailure!=ProgressionFailure.None) ShowNotice(HeroProgressionLogic.Describe(progression.LastFailure),2.5f);
            }
            if(equipment.CommandSerial!=observedEquipment)
            {
                observedEquipment=equipment.CommandSerial;
                string item=ItemName(equipment.LastItem);
                if(equipment.LastFailure!=EquipmentFailure.None) ShowNotice((equipment.LastItem>=0?item+"：":"")+EquipmentLogic.Describe(equipment.LastFailure),2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.Buy) ShowNotice($"已购买 {item}",2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.Sell && equipment.Library.IsCreated && equipment.LastItem>=0 && equipment.LastItem<equipment.Library.Value.Items.Length)
                    ShowNotice($"已出售 {item}，返还 {equipment.Library.Value.Items[equipment.LastItem].SellPrice} 金币",2.5f);
            }
            BuildTooltip(s,slots,unit,runtime,attackState,attackData,heroStats,equipment,equipped,progression,busy);
            s.Hint=Time.unscaledTime<noticeUntil?notice:progression.SkillPoints>0?"Ctrl+Q/E/R/F 或点击 + 学习技能":"Tab 属性 · Esc 暂停";
            return s;
        }
        /// <summary>金币、装备栏与商店卡片（当前页）的展示数据；商店是否打开由 HUD 决定。</summary>
        private void BuildEquipment(CombatHudSnapshot s,in HeroEquipment equipment,DynamicBuffer<EquipmentSlot> equipped)
        {
            var items=session.Items;
            s.Gold=equipment.Gold;
            for(int i=0;i<s.Equipment.Length;i++)
            {
                ref var view=ref s.Equipment[i];
                view=default; view.Exists=i<equipped.Length;
                int item=view.Exists?equipped[i].Item:-1;
                var definition=item>=0 && item<items.Length?items[item]:null;
                if(!definition) continue;
                view.Filled=true; view.Icon=definition.icon; view.Name=definition.Label;
            }
        }
        /// <summary>悬停的 HUD 槽或商店页变化时立即刷新 HUD，避免提示框与商店等待下一个 0.1 秒刷新周期。</summary>
        private void OnHoverChanged()=>nextHudUpdate=0;
        /// <summary>
        /// 按 HUD 悬停源组装提示：0–3 技能槽、4 自动攻击被动、10–15 装备栏；
        /// 只读 ECS 权威状态。
        /// </summary>
        private void BuildTooltip(CombatHudSnapshot s,DynamicBuffer<AbilitySlot> slots,in AbilityUnit unit,in AbilityRuntime runtime,
            in AutoAttackState attackState,in AutoAttackData attackData,in HeroStats heroStats,in HeroEquipment equipment,DynamicBuffer<EquipmentSlot> equipped,
            in HeroProgression progression,bool busy)
        {
            var view=s.Tooltip;
            view.Visible=false;
            int index=hud.HoveredSlot;
            if(index==CombatHud.PassiveSlot)
            {
                AbilityTooltipBuilder.BuildAttack(attackData,attackState,unit,hero.basicAttack,hero.attackWhileMoving,Alive(attackState.Target),view);
                return;
            }
            if(index>=CombatHud.EquipmentHoverBase && index<CombatHud.EquipmentHoverBase+6)
            {
                int item=equipped[index-CombatHud.EquipmentHoverBase].Item;
                if(item<0 || item>=session.Items.Length || !equipment.Library.IsCreated) return;
                var contextItem=new ItemTooltipContext{Definition=session.Items[item],Buffs=session.Buffs,Gold=equipment.Gold};
                AbilityTooltipBuilder.BuildItem(ref session.Library.Value,ref equipment.Library.Value.Items[item],contextItem,view);return;
            }
            if(index<0 || index>=slots.Length || index>=4) return;
            var slot=slots[index]; var original=SkillAt(index); var active=session.ActiveSkill(slot.Definition);
            string description=active && !string.IsNullOrWhiteSpace(active.description)?active.description:original?original.description:"";
            var context=new SkillTooltipContext{Hotkey=KeyLabel(index),Name=session.SkillNames[index],Description=description,
                Icon=active && active.icon?active.icon:original?original.icon:null,
                Track=active?active.growth.track:original?original.growth.track:SkillLevelTrack.Basic,
                Buffs=session.Buffs,Slot=slot,Owner=unit,Progression=progression,Now=runtime.Time,
                CastWindup=busy && runtime.CastSlot==index && runtime.Time<runtime.ReleaseAt,
                CastRecovery=busy && runtime.CastSlot==index && runtime.Time>=runtime.ReleaseAt};
            AbilityTooltipBuilder.BuildSkill(ref session.Library.Value,ref session.Library.Value.Skills[slot.Definition],context,view);
        }
        private void OnDestroy()
        {
            if(ExpeditionUI) ExpeditionUI.Events.Command-=ExpeditionCommand;
            if(hud) {hud.AbilityRequested-=CastAbility;hud.LearnRequested-=LearnSkill;hud.HoverChanged-=OnHoverChanged;}
            if(heroVisual) Destroy(heroVisual);
            feedbackView?.Dispose();feedbackView=null;
            if(abilityIndicator) { abilityIndicator.Cancel(); abilityIndicator.HideSelection(); }
            if(hasEnemyQuery && session!=null && session.World.IsCreated) { try { enemyQuery.Dispose(); } catch(Exception) { } }
            hasEnemyQuery=false;
            session?.Dispose(); session=null;
            if(fallbackRules) Destroy(fallbackRules);
            if(fallbackDifficulty) Destroy(fallbackDifficulty);
            if(gameCamera)
            { gameCamera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation); gameCamera.orthographicSize=originalCameraSize; gameCamera.farClipPlane=originalFarClip; gameCamera.orthographic=originalOrthographic; }
        }
    }
}
