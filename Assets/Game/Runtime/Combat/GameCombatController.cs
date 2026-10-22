using System;
using Roguelike.Abilities;
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
    /// 输入只写 Dota 2 式指令意图（移动、攻击目标、攻击移动、停止、待命、拾取、丢弃）与装备指令，由模拟系统结算；
    /// 地上的装备与商店范围圈只做展示。
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
        [Header("装备与商店（重开生效）")]
        [Tooltip("装备库：商店出售的装备、装备栏格数、开局金币、出售返还比例与丢弃 / 拾取距离。为空时商店为空（装备栏仍为 6 格）并输出警告。")]
        public EquipmentCatalog equipmentCatalog;
        [Tooltip("场景中的商店（GameShop）：英雄走进它的范围（地面金色圆圈）后才能打开商店购买与出售。为空时任何位置都能打开商店并输出警告。")]
        public GameShop shop;
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
        private int observedLevelUps,observedCommands,observedEquipment;
        private Renderer markerRenderer;
        private MaterialPropertyBlock markerProperties;
        private GroundItemViews groundViews;
        private CircleIndicatorView shopCircle;
        private int hoveredGround,inspectedGround;
        private readonly CombatHudSnapshot snapshot=new CombatHudSnapshot();
        /// <summary>鼠标点选敌人时在体型半径外额外放宽的距离（米）。</summary>
        public const float PickPadding=0.45f;
        private static readonly Color MoveColor=new Color(0.35f,1f,0.45f,1),AttackColor=new Color(1f,0.32f,0.25f,1),HoverColor=new Color(1f,0.25f,0.2f,1),TargetColor=new Color(1f,0.62f,0.2f,1),
            SelectionColor=new Color(1f,0.86f,0.35f,1),ShopColor=new Color(1f,0.8f,0.3f,1);
        private static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        /// <summary>当前英雄自动攻击的攻击距离（米，已计入攻击距离加成）；会话未就绪时读英雄旧字段。</summary>
        public float attackRange=>IsReady?LiveAttack.Range:hero?hero.range:0;
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
        /// <summary>商店面板是否打开（B 键或 HUD 商店按钮切换）。</summary>
        public bool ShopOpen=>hud && hud.ShopOpen;
        /// <summary>本局商店位置与范围（来自场景的 GameShop）；Limited 为假表示不限距离。</summary>
        public ShopSite Shop=>IsReady && session.World.EntityManager.HasComponent<ShopSite>(session.Entity)?session.World.EntityManager.GetComponentData<ShopSite>(session.Entity):default;
        /// <summary>英雄是否在商店范围内（地面金色圆圈）；只有在范围内才能打开商店、购买与出售。</summary>
        public bool InShopRange=>IsReady && EquipmentLogic.InShop(Shop,State.Position);
        /// <summary>英雄还要走多远才能进入商店范围（米）；已在范围内或不限距离时为 0。</summary>
        public float ShopDistance
        {
            get
            {
                var site=Shop;
                return IsReady && site.Limited?Mathf.Max(0,math.distance(site.Position.xz,State.Position.xz)-site.Radius):0;
            }
        }
        /// <summary>地上装备的数量；第 index 件见 <see cref="GroundAt"/>。</summary>
        public int GroundCount=>IsReady && session.World.EntityManager.HasBuffer<GroundItem>(session.Entity)?session.World.EntityManager.GetBuffer<GroundItem>(session.Entity).Length:0;
        public GroundItem GroundAt(int index)
        {
            if(index<0 || index>=GroundCount) return new GroundItem{Item=-1};
            return session.World.EntityManager.GetBuffer<GroundItem>(session.Entity)[index];
        }
        /// <summary>按编号查找地上的装备。</summary>
        public bool TryGetGroundItem(int id,out GroundItem item)
        {
            item=new GroundItem{Item=-1};
            if(GroundCount==0) return false;
            var ground=session.World.EntityManager.GetBuffer<GroundItem>(session.Entity);
            int index=EquipmentLogic.FindGround(ground,id);
            if(index<0) return false;
            item=ground[index]; return true;
        }
        /// <summary>正在查看（显示提示框）的地上装备编号；0 表示没有。</summary>
        public int InspectedGround=>inspectedGround;
        /// <summary>鼠标悬停的地上装备编号（地面圈高亮）；0 表示没有。</summary>
        public int HoveredGround=>hoveredGround;
        /// <summary>地上装备图标牌中心在屏幕上的位置（像素），供点选与检查；不存在或不在相机前方时返回 false。</summary>
        public bool TryGetGroundScreen(int id,out Vector2 screen)
        {
            screen=default;
            if(groundViews==null || !gameCamera || !groundViews.TryGetCard(id,out Vector3 card)) return false;
            Vector3 point=gameCamera.WorldToScreenPoint(card);
            screen=point; return point.z>0;
        }
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
                    // 没有难度资源的旧场景才使用控制器基础值；正式 Game 从 GameData 编译。
                    fallbackDifficulty.baseEnemyHealth=enemyHealth; fallbackDifficulty.baseEnemyDamage=enemyAttackDamage; fallbackDifficulty.baseEnemySpeed=enemySpeed;
                    Debug.LogWarning("Game 未指定关卡难度曲线，使用默认曲线。",this);
                }
                if(!enemyRoster) Debug.LogWarning("Game 未指定敌人名册，只刷出统一敌人。",this);
                if(!equipmentCatalog) Debug.LogWarning("Game 未指定装备库，商店为空。",this);
                if(!shop) Debug.LogWarning("Game 未指定商店位置（GameShop），任何位置都能打开商店。",this);
                markerRenderer=destinationMarker.GetComponentInChildren<Renderer>(true);
                groundViews=new GroundItemViews(abilityIndicator,markerMaterial,null);
                shopCircle=abilityIndicator.CreateCircle("Shop Range Circle");
                session=new GameCombatSession(world);
                enemyQuery=world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CombatEnemy>(),ComponentType.ReadOnly<LocalTransform>(),ComponentType.ReadOnly<AbilityUnit>());
                hasEnemyQuery=true;
                hud.RestartRequested+=RestartRun; hud.AbilityRequested+=CastAbility;
                hud.LearnRequested+=LearnSkill; hud.TalentRequested+=ChooseTalentEncoded; hud.OrderRequested+=OnOrderButton;
                hud.TargetCloseRequested+=ClearSelection; hud.HoverChanged+=OnHoverChanged;
                hud.BuyRequested+=BuyItem; hud.SellRequested+=SellItem; hud.ShopChanged+=OnHoverChanged;
                hud.ShopRequested+=ToggleShop; hud.MoveRequested+=MoveItem; hud.DropRequested+=OnDropRequested;
                RestartRun();
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        public void RestartRun()
        {
            if(session==null) return;
            abilityIndicator.Cancel(); suppressRightUntilRelease=false; rightDragMoving=false; AttackArmed=false; hovered=Entity.Null; selected=Entity.Null;
            abilityIndicator.HideSelection(); hud.CloseShop();
            hoveredGround=0; inspectedGround=0; hud.HideGroundTooltip(); groundViews?.Clear();
            session.Reset(this,ActiveRules,ActiveDifficulty,enemyMesh,projectileMesh);
            destinationMarker.gameObject.SetActive(false); nextHudUpdate=0;
            observedLevelUps=0; observedCommands=0; observedEquipment=0;
            ShowNotice(shop?"按 Ctrl+Q/W/E/R 或点击技能上方的“+”学习技能；商店在上方的金色圆圈里，走进去按 B 购买装备":
                "按 Ctrl+Q/W/E/R 或点击技能上方的“+”学习第一个技能；按 B 打开商店购买装备",6);
            Debug.Log($"GAME_STARTED maxAlive={enemyCount}; hero={hero.stableId}; composedSkills={hero.skills.Length}; enemyKinds={session.EnemyNames.Length}; finalWave={ActiveDifficulty.finalWave}; shopItems={session.Items.Length}; gold={Equipment.Gold:0}");
        }
        /// <summary>选择英雄配置并重新开局；不改变共享配置。选人界面可调用此边界。</summary>
        public void SelectHero(HeroDefinition selected)
        {
            if(!selected) throw new ArgumentNullException(nameof(selected));
            selected.ResolveSkills(skillCatalog); hero=selected;
            if(session!=null) RestartRun();
        }
        // ---------- Dota 2 式指令 ----------
        /// <summary>移动指令（右键地面）：走到目标点，到达后待机。英雄开启“移动中攻击”时途中自动攻击攻击距离内的敌人且不停下。保留旧名称供探针与外部调用。</summary>
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
        /// <summary>写入一条英雄指令；value / item 为拾取（地上装备编号）与丢弃（装备栏格、下令时的装备下标）的参数。</summary>
        private void IssueOrder(HeroOrderKind kind,Vector3 point,Entity target,int value=0,int item=-1)
        {
            if(!IsReady || State.Health<=0) return;
            var manager=session.World.EntityManager;
            var order=manager.GetComponentData<HeroOrder>(session.Entity);
            float y=State.Position.y;
            order.Kind=kind; order.Target=target; order.Value=value; order.Item=item; order.Serial++;
            order.Point=new float3(Mathf.Clamp(point.x,-arenaHalfSize+2,arenaHalfSize-2),y,Mathf.Clamp(point.z,-arenaHalfSize+2,arenaHalfSize-2));
            manager.SetComponentData(session.Entity,order);
            AttackArmed=false;
            bool marked=Marked(kind);
            destinationMarker.gameObject.SetActive(marked);
            if(marked) { destinationMarker.position=(Vector3)order.Point; TintMarker(kind==HeroOrderKind.AttackMove?AttackColor:MoveColor); }
        }
        /// <summary>显示目的地标记的指令：移动、攻击移动与丢弃（落点）；拾取由地上装备自身高亮。</summary>
        private static bool Marked(HeroOrderKind kind)=>kind==HeroOrderKind.Move || kind==HeroOrderKind.AttackMove || kind==HeroOrderKind.DropItem;
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
            if(!Running) { abilityIndicator.Cancel(); AttackArmed=false; hovered=Entity.Null; selected=Entity.Null; hoveredGround=0; CloseGroundInspection(); return; }
            // Dota 2 默认操作：Ctrl+技能键学习/升级技能，单按技能键施放；A 攻击、S 停止、H 待命。
            bool learn=Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            for(int i=0;i<keys.Length;i++)
                if(Input.GetKeyDown(keys[i])) { if(learn) LearnSkill(i); else { AttackArmed=false; CastAbility(i); } }
            if(!learn)
            {
                if(Input.GetKeyDown(KeyCode.A)) ArmAttack();
                if(Input.GetKeyDown(KeyCode.S)) Stop();
                if(Input.GetKeyDown(KeyCode.H)) Hold();
                if(Input.GetKeyDown(KeyCode.B)) ToggleShop();
            }
            if(HeroProgressionLogic.PendingTalent(Progression)>=0)
            {
                if(Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChooseTalent(0);
                else if(Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChooseTalent(1);
            }
            // Esc：先取消技能/攻击瞄准；没有瞄准时关闭商店；再关闭地上装备的提示框；最后取消选中目标。
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(abilityIndicator.IsAiming || AttackArmed) { abilityIndicator.Cancel(); AttackArmed=false; }
                else if(hud.CloseShop()) { }
                else if(inspectedGround!=0) CloseGroundInspection();
                else ClearSelection();
            }
            if(Input.GetMouseButtonDown(1) && (abilityIndicator.IsAiming || AttackArmed))
            { abilityIndicator.Cancel(); AttackArmed=false; suppressRightUntilRelease=true; return; }
            bool overHud=hud.BlocksPointer(Input.mousePosition);
            if(!overHud) cameraZoom=Mathf.Clamp(cameraZoom-Input.mouseScrollDelta.y*Mathf.Max(2,cameraZoom*0.08f),12,160);
            Vector3 point=default;
            bool ground=!overHud && TryGround(Input.mousePosition,out point);
            if(abilityIndicator.IsAiming)
            {
                hovered=Entity.Null; hoveredGround=0;
                abilityIndicator.UpdateAim((Vector3)State.Position,point,ground,arenaHalfSize);
                if(Input.GetMouseButtonDown(0) && abilityIndicator.CanConfirm) TryConfirmAbilityAt(point);
                return;
            }
            bool click=Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            // 悬停点选每 0.05 秒刷新一次；点击时立即重新点选，保证指令目标与光标一致。敌人与地上的装备取更接近光标的一个。
            if(!ground) { hovered=Entity.Null; hoveredGround=0; }
            else if(click || Time.unscaledTime>=nextHover) { PickUnderCursor(Input.mousePosition); nextHover=Time.unscaledTime+0.05f; }
            if(!ground) return;
            if(AttackArmed)
            {
                if(Input.GetMouseButtonDown(0)) { if(hovered!=Entity.Null) IssueAttack(hovered); else IssueAttackMove(point); }
                return;
            }
            // 左键敌人：选中并查看信息；左键地上的装备：只查看信息（不拾取）；左键空地：取消选中并关闭地上装备的提示框。
            if(Input.GetMouseButtonDown(0))
            {
                if(hoveredGround!=0) InspectGroundItem(hoveredGround);
                else if(hovered!=Entity.Null) { SelectEnemy(hovered); CloseGroundInspection(); }
                else { ClearSelection(); CloseGroundInspection(); }
            }
            if(suppressRightUntilRelease) return;
            // 右键地上的装备：显示信息并走过去拾取（Dota 2 式）；右键敌人：攻击；右键地面：移动（同时关闭地上装备的提示框）。
            if(Input.GetMouseButtonDown(1))
            {
                if(hoveredGround!=0) { InspectGroundItem(hoveredGround); PickUpGroundItem(hoveredGround); rightDragMoving=false; }
                else if(hovered!=Entity.Null) { IssueAttack(hovered); rightDragMoving=false; CloseGroundInspection(); }
                else { SetDestination(point); rightDragMoving=true; lastMovePoint=point; CloseGroundInspection(); }
            }
            // 按住右键拖动时持续更新移动目标（Dota 2 的“按住右键移动”）。
            else if(rightDragMoving && Input.GetMouseButton(1) && (point-lastMovePoint).sqrMagnitude>0.36f)
            { SetDestination(point); lastMovePoint=point; }
        }
        /// <summary>按屏幕射线点选存活敌人：计算射线到敌人中心的垂直距离，取相对体型最近者。</summary>
        public Entity PickEnemy(Vector2 screen)=>PickEnemy(screen,out _);
        /// <summary>同 <see cref="PickEnemy(Vector2)"/>，并输出相对距离 score（越小越接近光标，小于 1 才算点中）。</summary>
        public Entity PickEnemy(Vector2 screen,out float score)
        {
            score=1;
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
                float relative=math.lengthsq(offset-direction*along)/(pick*pick);
                if(relative<bestScore) { bestScore=relative; best=units[i]; }
            }
            score=bestScore;
            return best;
        }
        /// <summary>
        /// 按屏幕位置点选地上的装备：射线到图标牌中心的垂直距离，或射线落点到地面圈中心的距离，取相对大小最近者。
        /// 返回地上装备编号，没有时返回 0；score 越小越接近光标（小于 1 才算点中）。
        /// </summary>
        public int PickGroundItem(Vector2 screen,out float score)
        {
            score=1;
            if(GroundCount==0 || !gameCamera || groundViews==null) return 0;
            var ground=session.World.EntityManager.GetBuffer<GroundItem>(session.Entity);
            var ray=gameCamera.ScreenPointToRay(screen);
            float3 origin=ray.origin,direction=math.normalizesafe((float3)ray.direction);
            bool floor=new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float enter);
            float3 hit=floor?(float3)ray.GetPoint(enter):default;
            float card=groundViews.Size*0.62f,circle=groundViews.Size*0.6f;
            int best=0;
            for(int i=0;i<ground.Length;i++)
            {
                var item=ground[i];
                if(groundViews.TryGetCard(item.Id,out Vector3 center))
                {
                    float3 offset=(float3)center-origin;
                    float along=math.dot(offset,direction);
                    float value=along>0?math.lengthsq(offset-direction*along)/(card*card):float.MaxValue;
                    if(value<score) { score=value; best=item.Id; }
                }
                if(floor)
                {
                    float value=math.distancesq(hit.xz,item.Position.xz)/(circle*circle);
                    if(value<score) { score=value; best=item.Id; }
                }
            }
            return best;
        }
        public int PickGroundItem(Vector2 screen)=>PickGroundItem(screen,out _);
        /// <summary>悬停点选：敌人与地上的装备取更接近光标的一个（同样接近时地上的装备优先）。</summary>
        private void PickUnderCursor(Vector2 screen)
        {
            Entity enemy=PickEnemy(screen,out float enemyScore);
            int item=PickGroundItem(screen,out float itemScore);
            if(item!=0 && (enemy==Entity.Null || itemScore<=enemyScore)) { hoveredGround=item; hovered=Entity.Null; }
            else { hoveredGround=0; hovered=enemy; }
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
        // ---------- 商店与装备 ----------
        /// <summary>打开或关闭商店（B 键、HUD 商店按钮）；只在战斗进行中、英雄在商店范围内（地面金色圆圈）时可以打开。</summary>
        public void ToggleShop()
        {
            if(!IsReady) return;
            if(!ShopOpen && Running && !InShopRange)
            {
                ShowNotice($"离商店太远（还有 {Mathf.CeilToInt(ShopDistance)} 米）：走进商店范围（地面金色圆圈）后再按 B",3);
                nextHudUpdate=0; return;
            }
            hud.ToggleShop();
        }
        /// <summary>购买装备库第 item 件装备。规则在模拟系统中权威结算，这里预检：明显失败时只提示、不提交指令。</summary>
        public void BuyItem(int item)
        {
            if(!Running) return;
            var manager=session.World.EntityManager;
            var equipment=manager.GetComponentData<HeroEquipment>(session.Entity);
            var failure=EquipmentLogic.CanBuy(equipment,manager.GetBuffer<EquipmentSlot>(session.Entity),item);
            if(failure==EquipmentFailure.InvalidItem) return;
            if(!InShopRange) { ShowNotice($"{ItemName(item)}：{EquipmentLogic.Describe(EquipmentFailure.NotInShop)}，走进地面金色圆圈后再购买",2.5f); return; }
            if(failure==EquipmentFailure.NotEnoughGold)
            {
                int missing=Mathf.CeilToInt(equipment.Library.Value.Items[item].Price-equipment.Gold-0.0001f);
                ShowNotice($"{ItemName(item)}：金币不足，还差 {Mathf.Max(1,missing)} 金币",2.5f); return;
            }
            if(failure!=EquipmentFailure.None) { ShowNotice($"{ItemName(item)}：{EquipmentLogic.Describe(failure)}",2.5f); return; }
            manager.GetBuffer<EquipmentCommand>(session.Entity).Add(new EquipmentCommand{Kind=EquipmentCommandKind.Buy,Value=item});
        }
        /// <summary>出售装备栏第 slot 格的装备。需要先在商店范围内打开商店，避免右键移动时误卖；空格忽略。</summary>
        public void SellItem(int slot)
        {
            if(!Running) return;
            var manager=session.World.EntityManager;
            var slots=manager.GetBuffer<EquipmentSlot>(session.Entity);
            if(slot<0 || slot>=slots.Length || slots[slot].Item<0) return;
            if(!ShopOpen)
            {
                ShowNotice(InShopRange?"打开商店（B）后右键装备即可出售":"走进商店范围（地面金色圆圈）并按 B 打开商店后才能出售",2.5f);
                return;
            }
            manager.GetBuffer<EquipmentCommand>(session.Entity).Add(new EquipmentCommand{Kind=EquipmentCommandKind.Sell,Value=slot});
        }
        /// <summary>换位（拖动装备到另一格）：目标格为空时移过去，有装备时两格交换。随时可用，不需要商店。</summary>
        public void MoveItem(int from,int to)
        {
            if(!Running || from==to || EquippedAt(from)<0) return;
            var manager=session.World.EntityManager;
            if(to<0 || to>=manager.GetBuffer<EquipmentSlot>(session.Entity).Length) return;
            manager.GetBuffer<EquipmentCommand>(session.Entity).Add(new EquipmentCommand{Kind=EquipmentCommandKind.Move,Value=from,Argument=to});
        }
        /// <summary>
        /// 丢弃指令（Dota 2 式拖动装备到地面）：英雄走到落点的丢弃距离内后，把第 slot 格的装备放到地上。空格忽略。
        /// 地上的装备可以右键捡回；属性在装备离开装备栏时立即重算。
        /// </summary>
        public bool DropItem(int slot,Vector3 point)
        {
            if(!Running) return false;
            int item=EquippedAt(slot);
            if(item<0) return false;
            abilityIndicator.Cancel();
            IssueOrder(HeroOrderKind.DropItem,point,Entity.Null,slot,item);
            return true;
        }
        /// <summary>按屏幕位置丢弃（HUD 拖动松开）；落在 HUD 上或落点无效时返回 false。</summary>
        public bool DropItemAtScreen(int slot,Vector2 screen)
        {
            if(!Running || hud.BlocksPointer(screen) || !TryGround(screen,out Vector3 point)) return false;
            return DropItem(slot,point);
        }
        private void OnDropRequested(int slot,Vector2 screen)=>DropItemAtScreen(slot,screen);
        /// <summary>
        /// 拾取指令（Dota 2 式右键地上的装备）：英雄走到拾取距离内后把它放进第一个空格。
        /// 装备栏已满或唯一装备已拥有时只提示、不下令。
        /// </summary>
        public bool PickUpGroundItem(int id)
        {
            if(!Running || !TryGetGroundItem(id,out var item)) return false;
            var manager=session.World.EntityManager;
            var failure=EquipmentLogic.CanPickUp(Equipment,manager.GetBuffer<EquipmentSlot>(session.Entity),manager.GetBuffer<GroundItem>(session.Entity),id,State.Position,false);
            if(failure!=EquipmentFailure.None)
            {
                ShowNotice($"{ItemName(item.Item)}：{EquipmentLogic.Describe(failure)}"+(failure==EquipmentFailure.InventoryFull?"，先出售或丢弃一件装备":""),2.5f);
                return false;
            }
            abilityIndicator.Cancel();
            IssueOrder(HeroOrderKind.PickUp,(Vector3)item.Position,Entity.Null,id,item.Item);
            return true;
        }
        /// <summary>查看地上的装备（左键或右键点击）：在它上方显示 Dota 2 式装备提示框，并取消敌人选中。</summary>
        public bool InspectGroundItem(int id)
        {
            if(!Running || !TryGetGroundItem(id,out _)) return false;
            inspectedGround=id; ClearSelection(); nextHudUpdate=0;
            return true;
        }
        /// <summary>关闭地上装备的提示框（点击空地、Esc、装备被拾取或重开）。</summary>
        public void CloseGroundInspection()
        {
            if(inspectedGround==0 && (!hud || !hud.GroundTooltipActive)) return;
            inspectedGround=0; nextHudUpdate=0;
            if(hud) hud.HideGroundTooltip();
        }
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
            if(!Marked(order.Kind) || math.distancesq(state.Position.xz,order.Point.xz)<0.1f) destinationMarker.gameObject.SetActive(false);
            gameCamera.orthographicSize=cameraZoom;
            var rotation=Quaternion.Euler(58,0,0);
            gameCamera.transform.SetPositionAndRotation((Vector3)state.Position+rotation*new Vector3(0,-cameraZoom*0.13f,-300),rotation);
            UpdateOrderPreview(state,order);
            UpdateSelection();
            UpdateShop(state);
            UpdateGroundItems(manager);
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
        /// <summary>商店范围圈（在范围内更亮）；英雄离开商店范围时自动关闭商店。</summary>
        private void UpdateShop(in CombatPlayer state)
        {
            var site=Shop;
            bool inside=EquipmentLogic.InShop(site,state.Position);
            if(shopCircle)
            {
                if(site.Limited && Running) shopCircle.Show(new Vector3(site.Position.x,0.035f,site.Position.z),site.Radius,ShopColor,inside?0.95f:0.5f);
                else shopCircle.Hide();
            }
            if(ShopOpen && Running && !inside) { hud.CloseShop(); ShowNotice("离开商店范围，商店已关闭",2.5f); nextHudUpdate=0; }
        }
        /// <summary>同步地上装备的展示与查看中的提示框锚点；查看的装备被拾取或不在屏幕内时关闭 / 隐藏提示框。</summary>
        private void UpdateGroundItems(EntityManager manager)
        {
            if(groundViews==null || !manager.HasBuffer<GroundItem>(session.Entity)) return;
            if(inspectedGround!=0 && !TryGetGroundItem(inspectedGround,out _)) CloseGroundInspection();
            if(hoveredGround!=0 && !TryGetGroundItem(hoveredGround,out _)) hoveredGround=0;
            groundViews.Sync(manager.GetBuffer<GroundItem>(session.Entity),session.Items,gameCamera,hoveredGround,inspectedGround);
            if(inspectedGround!=0 && Running && groundViews.TryGetCard(inspectedGround,out Vector3 card))
            {
                Vector3 screen=gameCamera.WorldToScreenPoint(card);
                bool visible=screen.z>0 && screen.x>=0 && screen.y>=0 && screen.x<=Screen.width && screen.y<=Screen.height;
                float pixels=gameCamera.orthographic?groundViews.Size/(2*Mathf.Max(0.01f,gameCamera.orthographicSize))*Screen.height:48;
                if(visible) hud.ShowGroundTooltip(screen,pixels); else hud.HideGroundTooltip();
            }
            else hud.HideGroundTooltip();
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
                case HeroOrderKind.PickUp: return "拾取装备";
                case HeroOrderKind.DropItem: return "丢弃装备";
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
            var attackData=manager.GetComponentData<AutoAttackData>(session.Entity);
            var heroStats=manager.GetComponentData<HeroStats>(session.Entity);
            var equipment=manager.GetComponentData<HeroEquipment>(session.Entity);
            var equipped=manager.GetBuffer<EquipmentSlot>(session.Entity);
            s.Outcome=state.Health<=0?CombatOutcome.Defeat:wave.Outcome;
            s.HeroName=string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName; s.Portrait=hero.portrait;
            s.Level=progression.Level; s.SkillPoints=progression.SkillPoints;
            s.Health=state.Health; s.MaximumHealth=state.MaximumHealth; s.Regen=unit.Regen; s.Mana=unit.Mana; s.MaximumMana=unit.MaxMana;
            s.ManaRegen=AbilityEffects.BaseManaRegen+unit.ManaRegenBonus;
            s.AttackDamage=unit.Attack; s.AttacksPerSecond=AutoAttackLogic.AttacksPerSecond(attackData,unit.AttackSpeedMultiplier);
            s.AttackRange=attackData.Range; s.MoveSpeed=unit.Speed;
            s.SkillAmplify=unit.SkillAmplify*100; s.CooldownReduction=unit.CooldownReduction*100; s.DamageReduction=unit.DamageReduction*100;
            s.CriticalChance=Mathf.Clamp(heroStats.Total.CriticalChance,0,100);
            BuildEquipment(s,equipment,equipped);
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
            if(equipment.CommandSerial!=observedEquipment)
            {
                observedEquipment=equipment.CommandSerial;
                string item=ItemName(equipment.LastItem);
                if(equipment.LastFailure!=EquipmentFailure.None) ShowNotice((equipment.LastItem>=0?item+"：":"")+EquipmentLogic.Describe(equipment.LastFailure),2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.Buy) ShowNotice($"已购买 {item}",2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.Drop) ShowNotice($"已丢弃 {item}：右键地上的装备可以捡回",2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.PickUp) ShowNotice($"已拾取 {item}",2.5f);
                else if(equipment.LastCommand==EquipmentCommandKind.Sell && equipment.Library.IsCreated && equipment.LastItem>=0 && equipment.LastItem<equipment.Library.Value.Items.Length)
                    ShowNotice($"已出售 {item}，返还 {equipment.Library.Value.Items[equipment.LastItem].SellPrice} 金币",2.5f);
            }
            BuildTooltip(s,slots,unit,runtime,attackState,attackData,heroStats,equipment,equipped,progression,busy);
            var targetContext=new TargetInfoContext{KindNames=session.EnemyNames,KindArchetypes=session.EnemyArchetypes,Buffs=session.Buffs,
                HeroPosition=state.Position,HeroAttackRange=attackData.Range,Now=runtime.Time};
            if(!TargetInfoBuilder.Build(manager,selected,targetContext,s.Target)) selected=Entity.Null;
            s.Hint=Time.unscaledTime<noticeUntil?notice:AttackArmed?"攻击：左键敌人攻击目标，左键地面攻击移动；右键或 Esc 取消":
                s.PendingTalent>=0?"有可选天赋：按 1 / 2 或打开天赋树选择":progression.SkillPoints>0?"有可用技能点：Ctrl+Q/W/E/R 或点击技能上方的“+”":
                s.ShopLimited && s.ShopInRange && !hud.ShopOpen?"在商店范围内：按 B 打开商店":"";
            return s;
        }
        /// <summary>金币、装备栏与商店卡片（当前页）的展示数据；商店是否打开由 HUD 决定。</summary>
        private void BuildEquipment(CombatHudSnapshot s,in HeroEquipment equipment,DynamicBuffer<EquipmentSlot> equipped)
        {
            var items=session.Items;
            s.Gold=equipment.Gold;
            s.ShopLimited=Shop.Limited; s.ShopInRange=InShopRange; s.ShopDistance=ShopDistance;
            for(int i=0;i<s.Equipment.Length;i++)
            {
                ref var view=ref s.Equipment[i];
                view=default; view.Exists=i<equipped.Length;
                int item=view.Exists?equipped[i].Item:-1;
                var definition=item>=0 && item<items.Length?items[item]:null;
                if(!definition) continue;
                view.Filled=true; view.Icon=definition.icon; view.Name=definition.Label;
            }
            int pages=Mathf.Max(1,(items.Length+CombatHud.ShopCards-1)/CombatHud.ShopCards);
            int page=Mathf.Clamp(hud.ShopPage,0,pages-1);
            s.ShopPage=page; s.ShopPages=pages; s.ShopItemCount=items.Length;
            bool compiled=equipment.Library.IsCreated;
            for(int c=0;c<s.Shop.Length;c++)
            {
                ref var card=ref s.Shop[c];
                card=default; card.Item=-1;
                int item=page*CombatHud.ShopCards+c;
                if(!compiled || item>=items.Length || item>=equipment.Library.Value.Items.Length || !items[item]) continue;
                ref var data=ref equipment.Library.Value.Items[item];
                card.Visible=true; card.Item=item; card.Icon=items[item].icon; card.Name=items[item].Label; card.Price=data.Price;
                card.CannotAfford=equipment.Gold+0.0001f<data.Price;
                card.Owned=data.Unique && EquipmentLogic.Owns(equipped,item);
            }
            s.ShopHint=items.Length==0?"商店暂无装备：在 Game 场景的控制器上指定装备库":
                $"左键购买  ·  右键装备或把装备拖到这里出售（返还 {session.SellRatio*100:0}%）  ·  B / Esc 关闭";
        }
        /// <summary>悬停的 HUD 槽或商店页变化时立即刷新 HUD，避免提示框与商店等待下一个 0.1 秒刷新周期。</summary>
        private void OnHoverChanged()=>nextHudUpdate=0;
        /// <summary>
        /// 按 HUD 悬停源组装 Dota 2 式提示：0–3 技能槽、4 自动攻击被动、10–15 装备栏、20 属性面板、100–111 商店卡片；
        /// 没有悬停 HUD 槽时显示正在查看的地上装备。只读 ECS 权威状态。
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
            if(index==CombatHud.StatsHover)
            {
                AbilityTooltipBuilder.BuildStats(heroStats,unit,attackData,progression.Level,s.HeroName,hero.portrait,view);
                return;
            }
            if(CombatHud.IsStatHover(index))
            {
                AbilityTooltipBuilder.BuildStat((StatKind)(index-CombatHud.StatHoverBase),heroStats,unit,attackData,progression.Level,s.HeroName,hero.portrait,view);
                return;
            }
            bool fromShop=index>=CombatHud.ShopHoverBase && index<CombatHud.ShopHoverBase+CombatHud.ShopCards;
            if(fromShop || (index>=CombatHud.EquipmentHoverBase && index<CombatHud.EquipmentHoverBase+CombatHud.EquipmentSlots))
            {
                int slotIndex=index-CombatHud.EquipmentHoverBase;
                int item=fromShop?s.Shop[index-CombatHud.ShopHoverBase].Item:slotIndex<equipped.Length?equipped[slotIndex].Item:-1;
                var items=session.Items;
                if(item<0 || item>=items.Length || !items[item] || !equipment.Library.IsCreated || item>=equipment.Library.Value.Items.Length) return;
                var itemContext=new ItemTooltipContext{Definition=items[item],Buffs=session.Buffs,Gold=equipment.Gold,FromShop=fromShop,ShopOpen=hud.ShopOpen,
                    BuyFailure=fromShop?EquipmentLogic.CanBuy(equipment,equipped,item):EquipmentFailure.None};
                AbilityTooltipBuilder.BuildItem(ref session.Library.Value,ref equipment.Library.Value.Items[item],itemContext,view);
                return;
            }
            if(index<0 && inspectedGround!=0 && TryGetGroundItem(inspectedGround,out var lying))
            {
                var items=session.Items;
                int item=lying.Item;
                if(item<0 || item>=items.Length || !items[item] || !equipment.Library.IsCreated || item>=equipment.Library.Value.Items.Length) return;
                var manager=session.World.EntityManager;
                var order=Order; var state=State;
                var groundContext=new ItemTooltipContext{Definition=items[item],Buffs=session.Buffs,Gold=equipment.Gold,FromGround=true,ShopOpen=hud.ShopOpen,
                    PickingUp=order.Kind==HeroOrderKind.PickUp && order.Value==inspectedGround,
                    Distance=math.distance(state.Position.xz,lying.Position.xz),
                    PickupFailure=EquipmentLogic.CanPickUp(equipment,equipped,manager.GetBuffer<GroundItem>(session.Entity),inspectedGround,state.Position,false)};
                AbilityTooltipBuilder.BuildItem(ref session.Library.Value,ref equipment.Library.Value.Items[item],groundContext,view);
                return;
            }
            if(index<0 || index>=slots.Length || index>=slotKeys.Length) return;
            var slot=slots[index]; var original=SkillAt(index); var active=session.ActiveSkill(slot.Definition);
            string description=active && !string.IsNullOrWhiteSpace(active.description)?active.description:original?original.description:"";
            var context=new SkillTooltipContext{Hotkey=slotKeys[index],Name=session.SkillNames[index],Description=description,
                Icon=active && active.icon?active.icon:original?original.icon:null,
                Track=active?active.growth.track:original?original.growth.track:SkillLevelTrack.Basic,
                Buffs=session.Buffs,Slot=slot,Owner=unit,Progression=progression,Now=runtime.Time,
                CastWindup=busy && runtime.CastSlot==index && runtime.Time<runtime.ReleaseAt,
                CastRecovery=busy && runtime.CastSlot==index && runtime.Time>=runtime.ReleaseAt};
            AbilityTooltipBuilder.BuildSkill(ref session.Library.Value,ref session.Library.Value.Skills[slot.Definition],context,view);
        }
        private void OnDestroy()
        {
            if(hud) { hud.RestartRequested-=RestartRun; hud.AbilityRequested-=CastAbility; hud.LearnRequested-=LearnSkill; hud.TalentRequested-=ChooseTalentEncoded; hud.OrderRequested-=OnOrderButton; hud.TargetCloseRequested-=ClearSelection; hud.HoverChanged-=OnHoverChanged;
                hud.BuyRequested-=BuyItem; hud.SellRequested-=SellItem; hud.ShopChanged-=OnHoverChanged;
                hud.ShopRequested-=ToggleShop; hud.MoveRequested-=MoveItem; hud.DropRequested-=OnDropRequested; }
            groundViews?.Dispose(); groundViews=null;
            if(shopCircle) Destroy(shopCircle.gameObject);
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
