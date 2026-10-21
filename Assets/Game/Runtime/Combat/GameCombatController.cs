using System;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>场景组合根与输入桥。英雄/技能规则来自配置，实体生命周期交给会话。</summary>
    [DisallowMultipleComponent]
    public sealed class GameCombatController : MonoBehaviour
    {
        // 当前 Game 只有一种统一怪物配置；编号随每个生成实体保存。
        public const int EnemyConfigId = 3000;
        [Header("场景资源")]
        public Camera gameCamera;
        public Material playerMaterial,enemyMaterial,projectileMaterial,groundMaterial,markerMaterial;
        [Header("英雄和技能库（重开生效）")]
        public HeroDefinition hero;
        public SkillCatalog skillCatalog;
        [Header("成长与难度（重开生效）")]
        [Tooltip("经验与等级规则：经验表、最高等级、技能点。为空时使用 Dota 2 默认值并输出警告。")]
        public HeroProgressionRules progressionRules;
        [Tooltip("关卡难度曲线：波次间隔、数量/生命/伤害增长、精英与首领、最终波。为空时使用默认曲线并输出警告。")]
        public CombatDifficultyDefinition difficulty;
        [Header("场地与敌人（重开生效）")]
        [Range(1,30000), Tooltip("同屏存活敌人上限；波次刷怪超过上限的部分会被跳过（首领除外）。")] public int enemyCount=10000;
        [Min(30)] public float arenaHalfSize=125;
        [Min(3)] public float safeSpawnRadius=9;
        [Min(1), Tooltip("第 1 波普通敌人的生命；之后按难度曲线逐波增长。")] public float enemyHealth=30;
        [Min(0), Tooltip("第 1 波普通敌人的移动速度（米/秒）。")] public float enemySpeed=2;
        [Min(0.1f)] public float enemyAttackRange=1.25f;
        [Min(0), Tooltip("第 1 波普通敌人的攻击伤害；之后按难度曲线逐波增长。")] public float enemyAttackDamage=4;
        [Min(0.1f)] public float enemyAttackInterval=1;
        [Min(0.1f)] public float projectileSpeed=38;
        [Header("相机")]
        [Range(12,160)] public float cameraZoom=28;
        [Header("场景展示引用")]
        [SerializeField] private Transform playerView,destinationMarker;
        [SerializeField] private CombatHud hud;
        [SerializeField] private GameAbilityIndicator abilityIndicator;
        [SerializeField] private Mesh enemyMesh,projectileMesh;
        private GameCombatSession session;
        private bool suppressRightUntilRelease;
        private float nextHudUpdate,smoothFrameTime;
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
        public float attackRange=>hero?hero.range:0;
        public float playerHealth=>hero?hero.health:0;
        public bool IsReady=>session!=null && session.World.IsCreated && session.World.EntityManager.Exists(session.Entity);
        public CombatPlayer State=>IsReady?session.World.EntityManager.GetComponentData<CombatPlayer>(session.Entity):default;
        public Entity SessionEntity=>IsReady?session.Entity:Entity.Null;
        public SkillDefinition SkillAt(int slot)=>hero && slot>=0 && slot<hero.skills.Length?hero.skills[slot]:null;
        public HeroProgression Progression=>IsReady?session.World.EntityManager.GetComponentData<HeroProgression>(session.Entity):default;
        public CombatWaveState Wave=>IsReady?session.World.EntityManager.GetComponentData<CombatWaveState>(session.Entity):default;
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
                session=new GameCombatSession(world);
                hud.RestartRequested+=RestartRun; hud.AbilityRequested+=CastAbility;
                hud.LearnRequested+=LearnSkill; hud.TalentRequested+=ChooseTalent;
                RestartRun();
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        public void RestartRun()
        {
            if(session==null) return;
            abilityIndicator.Cancel(); suppressRightUntilRelease=false;
            session.Reset(this,ActiveRules,ActiveDifficulty,enemyMesh,projectileMesh);
            destinationMarker.gameObject.SetActive(false); nextHudUpdate=0;
            observedLevelUps=0; observedCommands=0;
            ShowNotice("按 Ctrl+Q/W/E/R 或点击“学习”按钮学习第一个技能",6);
            Debug.Log($"GAME_STARTED maxAlive={enemyCount}; hero={hero.stableId}; composedSkills={hero.skills.Length}; finalWave={ActiveDifficulty.finalWave}");
        }
        /// <summary>选择英雄配置并重新开局；不改变共享配置。选人界面可调用此边界。</summary>
        public void SelectHero(HeroDefinition selected)
        {
            if(!selected) throw new ArgumentNullException(nameof(selected));
            selected.ResolveSkills(skillCatalog); hero=selected;
            if(session!=null) RestartRun();
        }
        public void SetDestination(Vector3 point)
        {
            if(!IsReady || State.Health<=0) return;
            var state=State; state.Destination=new float3(Mathf.Clamp(point.x,-arenaHalfSize+2,arenaHalfSize-2),state.Position.y,Mathf.Clamp(point.z,-arenaHalfSize+2,arenaHalfSize-2));
            session.World.EntityManager.SetComponentData(session.Entity,state);
            destinationMarker.position=(Vector3)state.Destination; destinationMarker.gameObject.SetActive(true);
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
            if(!Input.GetMouseButton(1)) suppressRightUntilRelease=false;
            if(Input.GetKeyDown(KeyCode.F5)) { RestartRun(); return; }
            if(!Running) { abilityIndicator.Cancel(); return; }
            // Dota 2 默认操作：Ctrl+技能键学习/升级技能，单按技能键施放。
            bool learn=Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            for(int i=0;i<keys.Length;i++)
                if(Input.GetKeyDown(keys[i])) { if(learn) LearnSkill(i); else CastAbility(i); }
            if(HeroProgressionLogic.PendingTalent(Progression)>=0)
            {
                if(Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChooseTalent(0);
                else if(Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChooseTalent(1);
            }
            if(Input.GetKeyDown(KeyCode.Escape)) abilityIndicator.Cancel();
            if(Input.GetMouseButtonDown(1) && abilityIndicator.IsAiming)
            { abilityIndicator.Cancel(); suppressRightUntilRelease=true; return; }
            bool overHud=hud.BlocksPointer(Input.mousePosition);
            if(!overHud) cameraZoom=Mathf.Clamp(cameraZoom-Input.mouseScrollDelta.y*Mathf.Max(2,cameraZoom*0.08f),12,160);
            if(abilityIndicator.IsAiming)
            {
                Vector3 point=default;
                bool usable=!overHud && TryGround(Input.mousePosition,out point);
                abilityIndicator.UpdateAim((Vector3)State.Position,point,usable,arenaHalfSize);
                if(Input.GetMouseButtonDown(0) && abilityIndicator.CanConfirm) TryConfirmAbilityAt(point);
                return;
            }
            if(!overHud && !suppressRightUntilRelease && Input.GetMouseButton(1)) TryMoveFromScreen(Input.mousePosition);
        }
        public void CastAbility(int slot)
        {
            if(!Running) return;
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length) return;
            // 未学习的技能不进入瞄准；执行器同样会以 NotLearned 拒绝，此处仅提供即时提示。
            if(slots[slot].Level<=0)
            { abilityIndicator.Cancel(); ShowNotice($"{session.SkillNames[slot]} 尚未学习：按 Ctrl+{slotKeys[slot]} 学习",2.5f); return; }
            ref var skill=ref session.Library.Value.Skills[slots[slot].Definition];
            if(skill.Aim==AimMode.Self) { Queue(slot,(Vector3)State.Position,(Vector3)State.Facing); return; }
            if(abilityIndicator.IsAiming && abilityIndicator.ActiveSlot==slot) abilityIndicator.Cancel();
            else
            {
                abilityIndicator.BeginAim(slot,SkillAimPreview.From(ref skill));
            }
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
        private void ShowNotice(string text,float seconds) { notice=text; noticeUntil=Time.unscaledTime+seconds; }
        private void Queue(int slot,Vector3 point,Vector3 direction)
        {
            Entity target=Entity.Null;
            var equipped=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity)[slot];
            if(session.Library.Value.Skills[equipped.Definition].Aim==AimMode.Unit)
            {
                float best=2.25f;
                using var query=session.World.EntityManager.CreateEntityQuery(typeof(CombatEnemy),typeof(Unity.Transforms.LocalTransform),typeof(AbilityUnit));
                using var units=query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach(var unit in units)
                {
                    if(session.World.EntityManager.GetComponentData<AbilityUnit>(unit).Health<=0) continue;
                    float distance=math.distancesq(((float3)point).xz,session.World.EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(unit).Position.xz);
                    if(distance<best) { best=distance; target=unit; }
                }
            }
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
        public bool TryMoveFromScreen(Vector2 screen)
        {
            if(!IsReady || State.Health<=0 || abilityIndicator.IsAiming || hud.BlocksPointer(screen) || !TryGround(screen,out Vector3 point)) return false;
            SetDestination(point); return true;
        }
        private void LateUpdate()
        {
            if(!IsReady) return;
            var state=State;
            playerView.position=new Vector3(state.Position.x,0.95f,state.Position.z);
            if(math.lengthsq(state.Facing)>0.001f) playerView.rotation=Quaternion.LookRotation((Vector3)state.Facing);
            if(math.distancesq(state.Position,state.Destination)<0.1f) destinationMarker.gameObject.SetActive(false);
            gameCamera.orthographicSize=cameraZoom;
            var rotation=Quaternion.Euler(58,0,0);
            gameCamera.transform.SetPositionAndRotation((Vector3)state.Position+rotation*new Vector3(0,-cameraZoom*0.13f,-300),rotation);
            var manager=session.World.EntityManager;
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
            var progression=Progression; var wave=Wave;
            hud.Refresh(new CombatHudState{Health=state.Health,MaximumHealth=state.MaximumHealth,Elapsed=state.Elapsed,
                AliveEnemies=state.AliveEnemies,Kills=state.Kills,ActiveProjectiles=state.ActiveProjectiles,Aiming=abilityIndicator.IsAiming,
                SkillQ=SlotLabel(0),SkillW=SlotLabel(1),SkillE=SlotLabel(2),SkillR=SlotLabel(3),
                Victory=wave.Outcome==CombatOutcome.Victory,Wave=wave.Wave,Level=progression.Level},smoothFrameTime>0?1/smoothFrameTime:0,attackRange);
            hud.RefreshProgression(ProgressionSnapshot(state,progression,wave));
        }
        private CombatProgressionHudState ProgressionSnapshot(in CombatPlayer state,in HeroProgression progression,in CombatWaveState wave)
        {
            var result=new CombatProgressionHudState {Outcome=state.Health<=0?CombatOutcome.Defeat:wave.Outcome,
                Level=progression.Level,SkillPoints=progression.SkillPoints};
            if(!progression.Library.IsCreated) return result;
            float2 progress=HeroProgressionLogic.LevelProgress(progression);
            result.ExperienceFraction=progress.y>0?progress.x/progress.y:1;
            result.ExperienceText=progress.y>0?$"经验 {progress.x:0} / {progress.y:0}":"已满级";
            var settings=session.World.EntityManager.GetComponentData<CombatWaveSettings>(session.Entity);
            string final=settings.FinalWave>0?$" / {settings.FinalWave}":"（无尽）";
            string next=settings.FinalWave>0 && wave.FinalBossSpawned?"击杀最终首领获胜":$"下一波 {Mathf.Max(0,wave.NextWaveAt-state.Elapsed):0}s";
            result.WaveText=$"第 {wave.Wave}{final} 波  ·  {next}";
            if(progression.LevelUpSerial!=observedLevelUps)
            { observedLevelUps=progression.LevelUpSerial; ShowNotice($"升到 {progression.Level} 级！"+(progression.SkillPoints>0?"按 Ctrl+Q/W/E/R 学习技能":""),3); }
            if(progression.CommandSerial!=observedCommands)
            {
                observedCommands=progression.CommandSerial;
                if(progression.LastFailure!=ProgressionFailure.None) ShowNotice(HeroProgressionLogic.Describe(progression.LastFailure),2.5f);
            }
            result.Hint=Time.unscaledTime<noticeUntil?notice:progression.SkillPoints>0?"有可用技能点：Ctrl+Q/W/E/R 学习":"";
            result.LearnQ=LearnLabel(0,progression); result.LearnW=LearnLabel(1,progression);
            result.LearnE=LearnLabel(2,progression); result.LearnR=LearnLabel(3,progression);
            int tier=HeroProgressionLogic.PendingTalent(progression);
            if(tier>=0 && hero.talents!=null && tier<hero.talents.Length)
            {
                var talent=hero.talents[tier];
                result.TalentPending=true;
                result.TalentTitle=$"{talent.heroLevel} 级天赋  ·  按 1 / 2 或点击选择（不消耗技能点）";
                result.TalentLeft="1   "+talent.left.Label; result.TalentRight="2   "+talent.right.Label;
            }
            return result;
        }
        private string LearnLabel(int index,in HeroProgression progression)
        {
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(index>=slots.Length) return "";
            var slot=slots[index]; ref var skill=ref session.Library.Value.Skills[slot.Definition];
            var failure=HeroProgressionLogic.CanLearn(progression,ref skill,slot.Level);
            string key=slotKeys[index];
            switch(failure)
            {
                case ProgressionFailure.None: return $"+ {key}  {slot.Level}→{slot.Level+1}";
                case ProgressionFailure.SkillMaxed: return $"{key} 已满级";
                case ProgressionFailure.HeroLevelTooLow: return $"{key} 需 Lv{SkillLevels.RequiredHeroLevel(ref skill,slot.Level+1)}";
                default: return $"{key}  {slot.Level}/{SkillLevels.MaxLevel(ref skill)}";
            }
        }
        private string SlotLabel(int index)
        {
            var manager=session.World.EntityManager;
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            if(index>=slots.Length) return "空槽";
            var slot=slots[index]; ref var skill=ref session.Library.Value.Skills[slot.Definition];
            if(slot.Level<=0) return $"{session.SkillNames[index]}\n未学习";
            var runtime=manager.GetComponentData<AbilityRuntime>(session.Entity);
            float now=runtime.Time;
            string status=slot.ReadyAt>now?$"冷却 {slot.ReadyAt-now:0.0}s":skill.RequiredHits>slot.HitStacks?$"{slot.HitStacks}/{skill.RequiredHits} 层":slot.Charges<=0?$"充能 {Mathf.Max(0,slot.RechargeAt-now):0.0}s":"就绪";
            if(AbilityCastTiming.IsBusy(runtime,now))
                status=runtime.CastSlot!=index?"动作锁定":now<runtime.ReleaseAt?$"释放前摇 {runtime.ReleaseAt-now:0.0}s":$"释放后摇 {runtime.ActionReadyAt-now:0.0}s";
            return $"{session.SkillNames[index]} {slot.Level}/{SkillLevels.MaxLevel(ref skill)}\n{status}";
        }
        private void OnDestroy()
        {
            if(hud) { hud.RestartRequested-=RestartRun; hud.AbilityRequested-=CastAbility; hud.LearnRequested-=LearnSkill; hud.TalentRequested-=ChooseTalent; }
            if(abilityIndicator) abilityIndicator.Cancel();
            session?.Dispose(); session=null;
            if(fallbackRules) Destroy(fallbackRules);
            if(fallbackDifficulty) Destroy(fallbackDifficulty);
            if(gameCamera)
            { gameCamera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation); gameCamera.orthographicSize=originalCameraSize; gameCamera.farClipPlane=originalFarClip; gameCamera.orthographic=originalOrthographic; }
        }
    }
}
