using System;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 场景组合根与输入桥。英雄/技能/敌人规则来自配置，实体生命周期交给会话。
    /// 输入只写 Dota 2 式指令意图（移动、攻击目标、攻击移动、停止、待命），由模拟系统结算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCombatController : MonoBehaviour
    {
        /// <summary>未配置敌人名册时兼容原型使用的配置编号。</summary>
        public const int EnemyConfigId=3000;
        [Header("场景资源")]
        public Camera gameCamera;
        public Material playerMaterial,enemyMaterial,projectileMaterial,groundMaterial,markerMaterial;
        [Tooltip("敌方远程弹体材质；为空时使用敌人材质。")] public Material enemyProjectileMaterial;
        [Header("英雄和技能库（重开生效）")]
        public HeroDefinition hero;
        public SkillCatalog skillCatalog;
        [Header("成长与难度（重开生效）")]
        [Tooltip("经验与等级规则：经验表、最高等级、技能点。为空时使用 Dota 2 默认值并输出警告。")]
        public HeroProgressionRules progressionRules;
        [Tooltip("关卡难度曲线：波次间隔、数量/生命/伤害增长、精英与首领、最终波。为空时使用默认曲线并输出警告。")]
        public CombatDifficultyDefinition difficulty;
        [Tooltip("敌人名册：引用敌人预制体并配置出场波次、权重与档位。为空时只刷场景统一敌人（近战，使用下方攻击距离与间隔）。")]
        public EnemyRoster enemyRoster;
        [Header("场地与敌人基础数值（重开生效）")]
        [Range(1,30000), Tooltip("同屏存活敌人上限；波次刷怪超过上限的部分会被跳过（首领除外）。")] public int enemyCount=10000;
        [Min(30)] public float arenaHalfSize=125;
        [Min(3)] public float safeSpawnRadius=9;
        [Min(1), Tooltip("第 1 波普通敌人的基础生命；敌人种类再乘自身倍率，之后按难度曲线逐波增长。")] public float enemyHealth=30;
        [Min(0), Tooltip("第 1 波普通敌人的基础移动速度（米/秒）；敌人种类再乘自身倍率。")] public float enemySpeed=2;
        [Min(0.1f), Tooltip("仅未配置名册时的统一敌人使用：近战攻击距离（米）。")] public float enemyAttackRange=1.25f;
        [Min(0), Tooltip("第 1 波普通敌人的基础攻击伤害；敌人种类再乘自身倍率，之后按难度曲线逐波增长。")] public float enemyAttackDamage=4;
        [Min(0.1f), Tooltip("仅未配置名册时的统一敌人使用：攻击间隔（秒）。")] public float enemyAttackInterval=1;
        [Min(0.1f), Tooltip("英雄未配置自动攻击模块时的弹道速度（米/秒）。")] public float projectileSpeed=38;
        [Header("相机")]
        [Range(12,160)] public float cameraZoom=28;
        [Header("场景展示引用")]
        [SerializeField] private Transform playerView,destinationMarker;
        [SerializeField] private CombatHud hud;
        [SerializeField] private GameAbilityIndicator abilityIndicator;
        [SerializeField] private Mesh enemyMesh,projectileMesh;
        private GameCombatSession session;
        private EntityQuery enemyQuery;
        private bool hasEnemyQuery;
        private bool suppressRightUntilRelease,rightDragMoving;
        private Vector3 lastMovePoint;
        private float nextHudUpdate,smoothFrameTime,nextHover;
        private Entity hovered,selected;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraSize,originalFarClip;
        private bool originalOrthographic;
        private readonly KeyCode[] keys={KeyCode.Q,KeyCode.W,KeyCode.E,KeyCode.R};
        private static readonly string[] slotKeys={"Q","W","E","R"};
        private HeroProgressionRules fallbackRules;
        private CombatDifficultyDefinition fallbackDifficulty;
        private string notice;
        private float noticeUntil;
        private int observedLevelUps,observedCommands;
        private Renderer markerRenderer;
        private MaterialPropertyBlock markerProperties;
        private readonly CombatHudSnapshot snapshot=new CombatHudSnapshot();
        /// <summary>鼠标点选敌人时在体型半径外额外放宽的距离（米）。</summary>
        public const float PickPadding=0.45f;
        private static readonly Color MoveColor=new Color(0.35f,1f,0.45f,1),AttackColor=new Color(1f,0.32f,0.25f,1),HoverColor=new Color(1f,0.25f,0.2f,1),TargetColor=new Color(1f,0.62f,0.2f,1),
            SelectionColor=new Color(1f,0.86f,0.35f,1);
        private static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        /// <summary>当前英雄自动攻击的攻击距离（米）；会话未就绪时读英雄旧字段。</summary>
        public float attackRange=>IsReady?session.HeroAttack.Range:hero?hero.range:0;
        public float playerHealth=>hero?hero.health:0;
        public bool IsReady=>session!=null && session.World.IsCreated && session.World.EntityManager.Exists(session.Entity);
        public CombatPlayer State=>IsReady?session.World.EntityManager.GetComponentData<CombatPlayer>(session.Entity):default;
        public Entity SessionEntity=>IsReady?session.Entity:Entity.Null;
        public SkillDefinition SkillAt(int slot)=>hero && slot>=0 && slot<hero.skills.Length?hero.skills[slot]:null;
        public HeroProgression Progression=>IsReady?session.World.EntityManager.GetComponentData<HeroProgression>(session.Entity):default;
        public CombatWaveState Wave=>IsReady?session.World.EntityManager.GetComponentData<CombatWaveState>(session.Entity):default;
        /// <summary>当前英雄指令（输入层写入，模拟系统推进）。</summary>
        public HeroOrder Order=>IsReady?session.World.EntityManager.GetComponentData<HeroOrder>(session.Entity):default;
        /// <summary>英雄自动攻击运行状态：当前目标、前后摇阶段与冷却。</summary>
        public AutoAttackState Attack=>IsReady?session.World.EntityManager.GetComponentData<AutoAttackState>(session.Entity):default;
        /// <summary>本局名册中的敌人种类名称，下标与 CombatEnemy.Kind 一致。</summary>
        public string[] EnemyKinds=>IsReady?session.EnemyNames:Array.Empty<string>();
        /// <summary>左键选中的敌人（左上角显示信息面板）；未选中为 Entity.Null。只用于展示，不影响指令与模拟。</summary>
        public Entity Selected=>selected;
        /// <summary>是否处于 A 键攻击瞄准：下一次左键对敌人下达攻击、对地面下达攻击移动。</summary>
        public bool AttackArmed {get; private set;}
        public HeroProgressionRules ActiveRules=>progressionRules?progressionRules:fallbackRules;
        public CombatDifficultyDefinition ActiveDifficulty=>difficulty?difficulty:fallbackDifficulty;
        private bool Running=>IsReady && State.Health>0 && Wave.Outcome==CombatOutcome.Running;
        private void Start()
        {
            try
            {
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
                    Debug.LogWarning("Game 未指定经验与等级规则，使用 Dota 2 默认经验表。",this);
                }
                if(!difficulty)
                {
                    fallbackDifficulty=ScriptableObject.CreateInstance<CombatDifficultyDefinition>(); fallbackDifficulty.name="默认关卡难度曲线";
                    Debug.LogWarning("Game 未指定关卡难度曲线，使用默认曲线。",this);
                }
                if(!enemyRoster) Debug.LogWarning("Game 未指定敌人名册，只刷出统一敌人。",this);
                markerRenderer=destinationMarker.GetComponentInChildren<Renderer>(true);
                session=new GameCombatSession(world);
                enemyQuery=world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatEnemy>(),ComponentType.ReadOnly<LocalTransform>(),ComponentType.ReadOnly<AbilityUnit>());
                hasEnemyQuery=true;
                hud.RestartRequested+=RestartRun; hud.AbilityRequested+=CastAbility;
                hud.LearnRequested+=LearnSkill; hud.TalentRequested+=ChooseTalentEncoded; hud.OrderRequested+=OnOrderButton;
                hud.TargetCloseRequested+=ClearSelection;
                RestartRun();
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        public void RestartRun()
        {
            if(session==null) return;
            abilityIndicator.Cancel(); suppressRightUntilRelease=false; rightDragMoving=false; AttackArmed=false; hovered=Entity.Null; selected=Entity.Null;
            abilityIndicator.HideSelection();
            session.Reset(this,ActiveRules,ActiveDifficulty,enemyMesh,projectileMesh);
            destinationMarker.gameObject.SetActive(false); nextHudUpdate=0;
            observedLevelUps=0; observedCommands=0;
            ShowNotice("按 Ctrl+Q/W/E/R 或点击技能上方的“+”学习第一个技能",6);
            Debug.Log($"GAME_STARTED maxAlive={enemyCount}; hero={hero.stableId}; composedSkills={hero.skills.Length}; enemyKinds={session.EnemyNames.Length}; finalWave={ActiveDifficulty.finalWave}");
        }
        /// <summary>选择英雄配置并重新开局；不改变共享配置。选人界面可调用此边界。</summary>
        public void SelectHero(HeroDefinition selected)
        {
            if(!selected) throw new ArgumentNullException(nameof(selected));
            selected.ResolveSkills(skillCatalog); hero=selected;
            if(session!=null) RestartRun();
        }
        // ---------- Dota 2 式指令 ----------
        /// <summary>移动指令（右键地面）：走到目标点，途中不攻击，到达后待机。保留旧名称供探针与外部调用。</summary>
        public void SetDestination(Vector3 point)=>IssueOrder(HeroOrderKind.Move,point,Entity.Null);
        /// <summary>攻击移动（A + 左键地面）：走向目标点，途中自动攻击索敌距离内的敌人。</summary>
        public void IssueAttackMove(Vector3 point)=>IssueOrder(HeroOrderKind.AttackMove,point,Entity.Null);
        /// <summary>攻击指定敌人（右键敌人，或 A + 左键敌人）：追击直到目标死亡。</summary>
        public void IssueAttack(Entity target)
        {
            if(!IsReady || target==Entity.Null || !session.World.EntityManager.Exists(target) || !session.World.EntityManager.HasComponent<CombatEnemy>(target)) return;
            IssueOrder(HeroOrderKind.AttackTarget,(Vector3)State.Position,target);
        }
        /// <summary>停止（S）：取消当前指令与攻击前摇，转为待机（仍会自动攻击附近敌人）。</summary>
        public void Stop()=>IssueOrder(HeroOrderKind.Idle,IsReady?(Vector3)State.Position:Vector3.zero,Entity.Null);
        /// <summary>原地待命（H）：不移动，只攻击攻击距离内的敌人。</summary>
        public void Hold()=>IssueOrder(HeroOrderKind.Hold,IsReady?(Vector3)State.Position:Vector3.zero,Entity.Null);
        private void IssueOrder(HeroOrderKind kind,Vector3 point,Entity target)
        {
            if(!IsReady || State.Health<=0) return;
            var manager=session.World.EntityManager;
            var order=manager.GetComponentData<HeroOrder>(session.Entity);
            float y=State.Position.y;
            order.Kind=kind; order.Target=target; order.Serial++;
            order.Point=new float3(Mathf.Clamp(point.x,-arenaHalfSize+2,arenaHalfSize-2),y,Mathf.Clamp(point.z,-arenaHalfSize+2,arenaHalfSize-2));
            manager.SetComponentData(session.Entity,order);
            AttackArmed=false;
            bool marked=kind==HeroOrderKind.Move || kind==HeroOrderKind.AttackMove;
            destinationMarker.gameObject.SetActive(marked);
            if(marked) { destinationMarker.position=(Vector3)order.Point; TintMarker(kind==HeroOrderKind.Move?MoveColor:AttackColor); }
        }
        /// <summary>左键选中敌人：左上角显示该敌人的信息面板并在脚下显示选中圈。只读展示，不下达指令。</summary>
        public bool SelectEnemy(Entity target)
        {
            if(!Running || !Alive(target) || !session.World.EntityManager.HasComponent<CombatEnemy>(target)) return false;
            selected=target; nextHudUpdate=0; return true;
        }
        /// <summary>取消选中（左键空地、Esc、面板 × 按钮，或目标死亡）。</summary>
        public void ClearSelection()
        {
            if(selected==Entity.Null) return;
            selected=Entity.Null; nextHudUpdate=0;
        }
        /// <summary>A 键：进入攻击瞄准（显示攻击距离），技能瞄准会被取消。</summary>
        public void ArmAttack()
        {
            if(!Running) return;
            abilityIndicator.Cancel(); AttackArmed=true;
        }
        private void OnOrderButton(int value)
        {
            if(value==0) { if(AttackArmed) AttackArmed=false; else ArmAttack(); }
            else if(value==1) Stop();
            else if(value==2) Hold();
        }
        private void TintMarker(Color color)
        {
            if(!markerRenderer) return;
            if(markerProperties==null) markerProperties=new MaterialPropertyBlock();
            markerRenderer.GetPropertyBlock(markerProperties);
            markerProperties.SetColor(BaseColorId,color); markerProperties.SetColor(ColorId,color);
            markerRenderer.SetPropertyBlock(markerProperties);
        }
        public bool TryApplySkillRecipe(int slot,string json,out string failure)
        {
            if(!IsReady) { failure="战斗会话尚未准备好。"; return false; }
            bool applied=session.TryApplySkillRecipe(slot,json,out failure);
            if(applied) abilityIndicator.Cancel();
            return applied;
        }
        private void Update()
        {
            if(!IsReady) return;
            if(!Input.GetMouseButton(1)) { suppressRightUntilRelease=false; rightDragMoving=false; }
            if(Input.GetKeyDown(KeyCode.F5)) { RestartRun(); return; }
            if(!Running) { abilityIndicator.Cancel(); AttackArmed=false; hovered=Entity.Null; selected=Entity.Null; return; }
            // Dota 2 默认操作：Ctrl+技能键学习/升级技能，单按技能键施放；A 攻击、S 停止、H 待命。
            bool learn=Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            for(int i=0;i<keys.Length;i++)
                if(Input.GetKeyDown(keys[i])) { if(learn) LearnSkill(i); else { AttackArmed=false; CastAbility(i); } }
            if(!learn)
            {
                if(Input.GetKeyDown(KeyCode.A)) ArmAttack();
                if(Input.GetKeyDown(KeyCode.S)) Stop();
                if(Input.GetKeyDown(KeyCode.H)) Hold();
            }
            if(HeroProgressionLogic.PendingTalent(Progression)>=0)
            {
                if(Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChooseTalent(0);
                else if(Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChooseTalent(1);
            }
            // Esc：先取消技能/攻击瞄准；没有瞄准时取消选中目标。
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(!abilityIndicator.IsAiming && !AttackArmed) ClearSelection();
                abilityIndicator.Cancel(); AttackArmed=false;
            }
            if(Input.GetMouseButtonDown(1) && (abilityIndicator.IsAiming || AttackArmed))
            { abilityIndicator.Cancel(); AttackArmed=false; suppressRightUntilRelease=true; return; }
            bool overHud=hud.BlocksPointer(Input.mousePosition);
            if(!overHud) cameraZoom=Mathf.Clamp(cameraZoom-Input.mouseScrollDelta.y*Mathf.Max(2,cameraZoom*0.08f),12,160);
            Vector3 point=default;
            bool ground=!overHud && TryGround(Input.mousePosition,out point);
            if(abilityIndicator.IsAiming)
            {
                hovered=Entity.Null;
                abilityIndicator.UpdateAim((Vector3)State.Position,point,ground,arenaHalfSize);
                if(Input.GetMouseButtonDown(0) && abilityIndicator.CanConfirm) TryConfirmAbilityAt(point);
                return;
            }
            bool click=Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            // 悬停点选每 0.05 秒刷新一次；点击时立即重新点选，保证指令目标与光标一致。
            if(!ground) hovered=Entity.Null;
            else if(click || Time.unscaledTime>=nextHover) { hovered=PickEnemy(Input.mousePosition); nextHover=Time.unscaledTime+0.05f; }
            if(!ground) return;
            if(AttackArmed)
            {
                if(Input.GetMouseButtonDown(0)) { if(hovered!=Entity.Null) IssueAttack(hovered); else IssueAttackMove(point); }
                return;
            }
            // 左键敌人：选中并查看信息；左键空地：取消选中。
            if(Input.GetMouseButtonDown(0)) { if(hovered!=Entity.Null) SelectEnemy(hovered); else ClearSelection(); }
            if(suppressRightUntilRelease) return;
            if(Input.GetMouseButtonDown(1))
            {
                if(hovered!=Entity.Null) { IssueAttack(hovered); rightDragMoving=false; }
                else { SetDestination(point); rightDragMoving=true; lastMovePoint=point; }
            }
            // 按住右键拖动时持续更新移动目标（Dota 2 的“按住右键移动”）。
            else if(rightDragMoving && Input.GetMouseButton(1) && (point-lastMovePoint).sqrMagnitude>0.36f)
            { SetDestination(point); lastMovePoint=point; }
        }
        /// <summary>按屏幕射线点选存活敌人：计算射线到敌人中心的垂直距离，取相对体型最近者。</summary>
        public Entity PickEnemy(Vector2 screen)
        {
            if(!IsReady || !hasEnemyQuery || !gameCamera) return Entity.Null;
            var ray=gameCamera.ScreenPointToRay(screen);
            float3 origin=ray.origin,direction=math.normalizesafe((float3)ray.direction);
            Entity best=Entity.Null; float bestScore=1;
            using var units=enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms=enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var stats=enemyQuery.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            for(int i=0;i<units.Length;i++)
            {
                if(stats[i].Health<=0) continue;
                float3 offset=transforms[i].Position-origin;
                float along=math.dot(offset,direction);
                if(along<=0) continue;
                float pick=CombatSimulationSystem.BodyRadius*transforms[i].Scale+PickPadding;
                float score=math.lengthsq(offset-direction*along)/(pick*pick);
                if(score<bestScore) { bestScore=score; best=units[i]; }
            }
            return best;
        }
        /// <summary>离指定地面点最近（平面距离不超过 maxDistance）的存活敌人。</summary>
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
            if(!Running) return;
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length) return;
            AttackArmed=false;
            // 未学习的技能不进入瞄准；执行器同样会以 NotLearned 拒绝，此处仅提供即时提示。
            if(slots[slot].Level<=0)
            { abilityIndicator.Cancel(); ShowNotice($"{session.SkillNames[slot]} 尚未学习：按 Ctrl+{slotKeys[slot]} 学习",2.5f); return; }
            ref var skill=ref session.Library.Value.Skills[slots[slot].Definition];
            if(skill.Aim==AimMode.Self) { Queue(slot,(Vector3)State.Position,(Vector3)State.Facing); return; }
            if(abilityIndicator.IsAiming && abilityIndicator.ActiveSlot==slot) abilityIndicator.Cancel();
            else abilityIndicator.BeginAim(slot,SkillAimPreview.From(ref skill));
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
        /// <summary>选择当前待选天赋层的左（0）或右（1）选项。</summary>
        public void ChooseTalent(int option)
        {
            if(!Running || (option!=0 && option!=1)) return;
            int tier=HeroProgressionLogic.PendingTalent(Progression);
            if(tier<0) return;
            var talents=hero.talents;
            if(talents!=null && tier<talents.Length) ShowNotice("已选择天赋："+(option==0?talents[tier].left:talents[tier].right).Label,3);
            session.World.EntityManager.GetBuffer<ProgressionCommand>(session.Entity).Add(new ProgressionCommand{Kind=ProgressionCommandKind.ChooseTalent,Value=option});
        }
        /// <summary>天赋树按钮：值 = 层 × 2 + 选项（0 左、1 右）。只有当前待选层可以选择，其他层给出提示。</summary>
        public void ChooseTalentEncoded(int value)
        {
            if(value<0) return;
            ChooseTalentAt(value/2,value%2);
        }
        public void ChooseTalentAt(int tier,int option)
        {
            if(!Running || hero.talents==null || tier<0 || tier>=hero.talents.Length) return;
            var progression=Progression;
            int pending=HeroProgressionLogic.PendingTalent(progression);
            if(tier==pending) { ChooseTalent(option); return; }
            if((progression.TalentChosenMask&(1<<tier))!=0) ShowNotice($"{hero.talents[tier].heroLevel} 级天赋已经选择",2.5f);
            else if(progression.Level<hero.talents[tier].heroLevel) ShowNotice($"需要英雄等级 {hero.talents[tier].heroLevel} 才能选择该层天赋",2.5f);
            else if(pending>=0) ShowNotice($"请先选择 {hero.talents[pending].heroLevel} 级天赋",2.5f);
        }
        private void ShowNotice(string text,float seconds) { notice=text; noticeUntil=Time.unscaledTime+seconds; }
        private void Queue(int slot,Vector3 point,Vector3 direction)
        {
            Entity target=Entity.Null;
            var equipped=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity)[slot];
            if(session.Library.Value.Skills[equipped.Definition].Aim==AimMode.Unit) target=NearestEnemy(point,1.5f);
            session.World.EntityManager.GetBuffer<SkillCommand>(session.Entity).Add(new SkillCommand{Slot=slot,Point=point,Direction=direction,Target=target});
        }
        public bool TryConfirmAbilityAt(Vector3 point)
        {
            if(!Running || !abilityIndicator.IsAiming) return false;
            abilityIndicator.UpdateAim((Vector3)State.Position,point,true,arenaHalfSize);
            int slot=abilityIndicator.ActiveSlot;
            if(!abilityIndicator.TryConfirm(out Vector3 target,out Vector3 direction)) return false;
            Queue(slot,target,direction); return true;
        }
        private bool TryGround(Vector2 screen,out Vector3 point)
        {
            point=default;
            if(screen.x<0 || screen.y<0 || screen.x>=Screen.width || screen.y>=Screen.height) return false;
            var ray=gameCamera.ScreenPointToRay(screen);
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance)) return false;
            point=ray.GetPoint(distance); return true;
        }
        /// <summary>按屏幕位置下达移动指令；HUD 遮挡、瞄准中或落点无效时返回 false。</summary>
        public bool TryMoveFromScreen(Vector2 screen)
        {
            if(!IsReady || State.Health<=0 || abilityIndicator.IsAiming || hud.BlocksPointer(screen) || !TryGround(screen,out Vector3 point)) return false;
            SetDestination(point); return true;
        }
        private void LateUpdate()
        {
            if(!IsReady) return;
            var state=State; var manager=session.World.EntityManager;
            var order=manager.GetComponentData<HeroOrder>(session.Entity);
            playerView.position=new Vector3(state.Position.x,0.95f,state.Position.z);
            if(math.lengthsq(state.Facing)>0.001f) playerView.rotation=Quaternion.LookRotation((Vector3)state.Facing);
            bool marked=order.Kind==HeroOrderKind.Move || order.Kind==HeroOrderKind.AttackMove;
            if(!marked || math.distancesq(state.Position.xz,order.Point.xz)<0.1f) destinationMarker.gameObject.SetActive(false);
            gameCamera.orthographicSize=cameraZoom;
            var rotation=Quaternion.Euler(58,0,0);
            gameCamera.transform.SetPositionAndRotation((Vector3)state.Position+rotation*new Vector3(0,-cameraZoom*0.13f,-300),rotation);
            UpdateOrderPreview(state,order);
            UpdateSelection();
            var flights=manager.GetBuffer<SkillFlight>(session.Entity);
            for(int i=0;i<flights.Length;i++)
            {
                var flight=flights[i];
                var flightLibrary=session.World.GetExistingSystemManaged<CombatSimulationSystem>().GetLibrary(session.Entity,flight.ProgramVersion);
                bool arc=flightLibrary.Value.Skills[flight.Skill].Stages[flight.Stage].Delivery==DeliveryKind.ParabolicProjectile;
                // 抛物线的世界位置已经包含配置高度；旧投递保留原有模型离地补偿。
                Vector3 position=(Vector3)flight.Position+(arc?Vector3.zero:Vector3.up*0.5f);
                Graphics.DrawMesh(projectileMesh,Matrix4x4.TRS(position,Quaternion.identity,Vector3.one*0.5f),projectileMaterial,0);
            }
            manager.GetBuffer<SkillCue>(session.Entity).Clear(); // 特效订阅边界；详细命中特效待内容迭代。
            smoothFrameTime=Mathf.Lerp(smoothFrameTime,Time.unscaledDeltaTime,0.08f);
            if(Time.unscaledTime<nextHudUpdate) return;
            nextHudUpdate=Time.unscaledTime+0.1f;
            hud.Refresh(BuildSnapshot(state,order));
        }
        /// <summary>攻击瞄准显示攻击距离圈；悬停敌人显示红圈，否则标出当前攻击目标。技能瞄准优先。</summary>
        private void UpdateOrderPreview(in CombatPlayer state,in HeroOrder order)
        {
            if(abilityIndicator.IsAiming) return;
            if(!Running) { abilityIndicator.HideOrderPreview(); return; }
            var manager=session.World.EntityManager;
            Entity ring=hovered; Color color=HoverColor;
            if(!Alive(ring)) { ring=Entity.Null; hovered=Entity.Null; }
            if(ring==Entity.Null)
            {
                var attack=manager.GetComponentData<AutoAttackState>(session.Entity);
                if(order.Kind==HeroOrderKind.AttackTarget && Alive(attack.Target)) { ring=attack.Target; color=TargetColor; }
            }
            Vector3 center=default; float radius=0;
            if(ring!=Entity.Null)
            {
                var transform=manager.GetComponentData<LocalTransform>(ring);
                center=(Vector3)transform.Position; radius=CombatSimulationSystem.BodyRadius*transform.Scale+0.25f;
            }
            abilityIndicator.ShowOrderPreview((Vector3)state.Position,attackRange,AttackArmed,center,radius,ring!=Entity.Null,color);
        }
        /// <summary>选中圈跟随目标；目标死亡或被销毁时自动取消选中。不受技能瞄准影响。</summary>
        private void UpdateSelection()
        {
            if(selected!=Entity.Null && (!Running || !Alive(selected))) ClearSelection();
            if(selected==Entity.Null) { abilityIndicator.HideSelection(); return; }
            var transform=session.World.EntityManager.GetComponentData<LocalTransform>(selected);
            abilityIndicator.ShowSelection((Vector3)transform.Position,CombatSimulationSystem.BodyRadius*transform.Scale+0.5f,SelectionColor);
        }
        private bool Alive(Entity entity)
        {
            if(entity==Entity.Null || !IsReady) return false;
            var manager=session.World.EntityManager;
            return manager.Exists(entity) && manager.HasComponent<AbilityUnit>(entity) && manager.HasComponent<LocalTransform>(entity) &&
                   manager.GetComponentData<AbilityUnit>(entity).Health>0;
        }
        private static string OrderLabel(HeroOrderKind kind)
        {
            switch(kind)
            {
                case HeroOrderKind.Move: return "移动";
                case HeroOrderKind.AttackTarget: return "攻击目标";
                case HeroOrderKind.AttackMove: return "攻击移动";
                case HeroOrderKind.Hold: return "原地待命";
                default: return "待机";
            }
        }
        /// <summary>组装 HUD 展示快照：只读 ECS 状态与配置，不写入任何规则数据。</summary>
        private CombatHudSnapshot BuildSnapshot(in CombatPlayer state,in HeroOrder order)
        {
            var manager=session.World.EntityManager;
            var progression=Progression; var wave=Wave; var s=snapshot;
            var unit=manager.GetComponentData<AbilityUnit>(session.Entity);
            var runtime=manager.GetComponentData<AbilityRuntime>(session.Entity);
            var attackState=manager.GetComponentData<AutoAttackState>(session.Entity);
            var attackData=session.HeroAttack;
            s.Outcome=state.Health<=0?CombatOutcome.Defeat:wave.Outcome;
            s.HeroName=string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName; s.Portrait=hero.portrait;
            s.Level=progression.Level; s.SkillPoints=progression.SkillPoints;
            s.Health=state.Health; s.MaximumHealth=state.MaximumHealth; s.Regen=unit.Regen; s.Mana=unit.Mana; s.MaximumMana=unit.MaxMana;
            s.AttackDamage=unit.Attack; s.AttacksPerSecond=AutoAttackLogic.AttacksPerSecond(attackData,unit.AttackSpeedMultiplier);
            s.AttackRange=attackData.Range; s.MoveSpeed=unit.Speed;
            s.Elapsed=state.Elapsed; s.Kills=state.Kills; s.Alive=state.AliveEnemies; s.Projectiles=state.ActiveProjectiles;
            s.Fps=smoothFrameTime>0?1/smoothFrameTime:0; s.Wave=wave.Wave;
            s.Order=OrderLabel(order.Kind); s.AttackArmed=AttackArmed; s.Aiming=abilityIndicator.IsAiming;
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
            var settings=manager.GetComponentData<CombatWaveSettings>(session.Entity);
            s.FinalWave=settings.FinalWave;
            s.NextWave=settings.FinalWave>0 && wave.FinalBossSpawned?"击杀最终首领获胜":$"下一波 {Mathf.Max(0,wave.NextWaveAt-state.Elapsed):0} 秒";
            s.BossAlive=Alive(wave.Boss) && manager.HasComponent<CombatEnemy>(wave.Boss);
            if(s.BossAlive)
            {
                var boss=manager.GetComponentData<AbilityUnit>(wave.Boss); var record=manager.GetComponentData<CombatEnemy>(wave.Boss);
                string name=record.Kind>=0 && record.Kind<session.EnemyNames.Length?session.EnemyNames[record.Kind]:"首领";
                s.BossName=$"首领 · {name}（第 {record.Wave} 波）"; s.BossFraction=boss.Health/Mathf.Max(1,boss.MaxHealth);
            }
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            float now=runtime.Time; bool busy=AbilityCastTiming.IsBusy(runtime,now);
            for(int i=0;i<s.Abilities.Length;i++)
            {
                ref var view=ref s.Abilities[i];
                view=default; view.Key=slotKeys[i];
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
            for(int i=0;i<s.Talents.Length;i++)
            {
                ref var row=ref s.Talents[i];
                row=default;
                if(hero.talents==null || i>=hero.talents.Length) continue;
                var talent=hero.talents[i];
                row.Exists=true; row.Level=talent.heroLevel; row.Left=talent.left.Label; row.Right=talent.right.Label;
                row.Chosen=(progression.TalentChosenMask&(1<<i))!=0; row.ChoseRight=(progression.TalentRightMask&(1<<i))!=0;
                row.Unlocked=progression.Level>=talent.heroLevel;
            }
            int tier=HeroProgressionLogic.PendingTalent(progression);
            s.PendingTalent=tier>=0 && hero.talents!=null && tier<hero.talents.Length?tier:-1;
            s.TalentTitle=s.PendingTalent>=0?$"天赋树  ·  {hero.talents[s.PendingTalent].heroLevel} 级天赋可选（按 1 / 2 或点击，不消耗技能点）":"天赋树  ·  10 / 15 / 20 / 25 级各选择一项";
            if(progression.LevelUpSerial!=observedLevelUps)
            { observedLevelUps=progression.LevelUpSerial; ShowNotice($"升到 {progression.Level} 级！"+(progression.SkillPoints>0?"按 Ctrl+Q/W/E/R 学习技能":""),3); }
            if(progression.CommandSerial!=observedCommands)
            {
                observedCommands=progression.CommandSerial;
                if(progression.LastFailure!=ProgressionFailure.None) ShowNotice(HeroProgressionLogic.Describe(progression.LastFailure),2.5f);
            }
            var targetContext=new TargetInfoContext{KindNames=session.EnemyNames,KindArchetypes=session.EnemyArchetypes,Buffs=session.Buffs,
                HeroPosition=state.Position,HeroAttackRange=attackData.Range,Now=runtime.Time};
            if(!TargetInfoBuilder.Build(manager,selected,targetContext,s.Target)) selected=Entity.Null;
            s.Hint=Time.unscaledTime<noticeUntil?notice:AttackArmed?"攻击：左键敌人攻击目标，左键地面攻击移动；右键或 Esc 取消":
                s.PendingTalent>=0?"有可选天赋：按 1 / 2 或打开天赋树选择":progression.SkillPoints>0?"有可用技能点：Ctrl+Q/W/E/R 或点击技能上方的“+”":"";
            return s;
        }
        private void OnDestroy()
        {
            if(hud) { hud.RestartRequested-=RestartRun; hud.AbilityRequested-=CastAbility; hud.LearnRequested-=LearnSkill; hud.TalentRequested-=ChooseTalentEncoded; hud.OrderRequested-=OnOrderButton; hud.TargetCloseRequested-=ClearSelection; }
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
