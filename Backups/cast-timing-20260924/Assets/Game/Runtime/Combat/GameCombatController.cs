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
        [Header("场景资源")]
        public Camera gameCamera;
        public Material playerMaterial,enemyMaterial,projectileMaterial,groundMaterial,markerMaterial;
        [Header("英雄和技能库（重开生效）")]
        public HeroDefinition hero;
        public SkillCatalog skillCatalog;
        [Header("场地与敌人（重开生效）")]
        [Range(1,30000)] public int enemyCount=10000;
        [Min(30)] public float arenaHalfSize=125;
        [Min(3)] public float safeSpawnRadius=9;
        [Min(1)] public float enemyHealth=30;
        [Min(0)] public float enemySpeed=2;
        [Min(0.1f)] public float enemyAttackRange=1.25f;
        [Min(0)] public float enemyAttackDamage=4;
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
        public float attackRange=>hero?hero.range:0;
        public float playerHealth=>hero?hero.health:0;
        public bool IsReady=>session!=null && session.World.IsCreated && session.World.EntityManager.Exists(session.Entity);
        public CombatPlayer State=>IsReady?session.World.EntityManager.GetComponentData<CombatPlayer>(session.Entity):default;
        public Entity SessionEntity=>IsReady?session.Entity:Entity.Null;
        public SkillDefinition SkillAt(int slot)=>hero && slot>=0 && slot<hero.skills.Length?hero.skills[slot]:null;
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
                session=new GameCombatSession(world);
                hud.RestartRequested+=RestartRun; hud.AbilityRequested+=CastAbility;
                RestartRun();
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        public void RestartRun()
        {
            if(session==null) return;
            abilityIndicator.Cancel(); suppressRightUntilRelease=false;
            session.Reset(this,enemyMesh,projectileMesh);
            destinationMarker.gameObject.SetActive(false); nextHudUpdate=0;
            Debug.Log($"GAME_STARTED enemies={enemyCount}; hero={hero.stableId}; composedSkills={hero.skills.Length}");
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
        private void Update()
        {
            if(!IsReady) return;
            if(!Input.GetMouseButton(1)) suppressRightUntilRelease=false;
            if(Input.GetKeyDown(KeyCode.F5)) { RestartRun(); return; }
            if(State.Health<=0) { abilityIndicator.Cancel(); return; }
            for(int i=0;i<keys.Length;i++) if(Input.GetKeyDown(keys[i])) CastAbility(i);
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
            if(!IsReady || State.Health<=0) return;
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
            if(slot<0 || slot>=slots.Length) return;
            ref var skill=ref session.Library.Value.Skills[slots[slot].Definition];
            if(skill.Aim==AimMode.Self) { Queue(slot,(Vector3)State.Position,(Vector3)State.Facing); return; }
            if(abilityIndicator.IsAiming && abilityIndicator.ActiveSlot==slot) abilityIndicator.Cancel();
            else
            {
                abilityIndicator.BeginAim(slot,SkillAimPreview.From(ref skill));
            }
        }
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
            if(!IsReady || State.Health<=0 || !abilityIndicator.IsAiming) return false;
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
                bool arc=session.Library.Value.Skills[flight.Skill].Stages[flight.Stage].Delivery==DeliveryKind.ParabolicProjectile;
                // 抛物线的世界位置已经包含配置高度；旧投递保留原有模型离地补偿。
                Vector3 position=(Vector3)flight.Position+(arc?Vector3.zero:Vector3.up*0.5f);
                Graphics.DrawMesh(projectileMesh,Matrix4x4.TRS(position,Quaternion.identity,Vector3.one*0.5f),projectileMaterial,0);
            }
            manager.GetBuffer<SkillCue>(session.Entity).Clear(); // 特效订阅边界；详细命中特效待内容迭代。
            smoothFrameTime=Mathf.Lerp(smoothFrameTime,Time.unscaledDeltaTime,0.08f);
            if(Time.unscaledTime<nextHudUpdate) return;
            nextHudUpdate=Time.unscaledTime+0.1f;
            hud.Refresh(new CombatHudState{Health=state.Health,MaximumHealth=state.MaximumHealth,Elapsed=state.Elapsed,
                AliveEnemies=state.AliveEnemies,Kills=state.Kills,ActiveProjectiles=state.ActiveProjectiles,Aiming=abilityIndicator.IsAiming,
                SkillQ=SlotLabel(0),SkillW=SlotLabel(1),SkillE=SlotLabel(2),SkillR=SlotLabel(3)},smoothFrameTime>0?1/smoothFrameTime:0,attackRange);
        }
        private string SlotLabel(int index)
        {
            var manager=session.World.EntityManager;
            var slots=manager.GetBuffer<AbilitySlot>(session.Entity);
            if(index>=slots.Length) return "空槽";
            var slot=slots[index]; ref var skill=ref session.Library.Value.Skills[slot.Definition];
            float now=manager.GetComponentData<AbilityRuntime>(session.Entity).Time;
            string status=slot.ReadyAt>now?$"冷却 {slot.ReadyAt-now:0.0}s":skill.RequiredHits>slot.HitStacks?$"{slot.HitStacks}/{skill.RequiredHits} 层":slot.Charges<=0?$"充能 {Mathf.Max(0,slot.RechargeAt-now):0.0}s":"就绪";
            return $"{session.SkillNames[index]}\n{status}";
        }
        private void OnDestroy()
        {
            if(hud) { hud.RestartRequested-=RestartRun; hud.AbilityRequested-=CastAbility; }
            if(abilityIndicator) abilityIndicator.Cancel();
            session?.Dispose(); session=null;
            if(gameCamera)
            { gameCamera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation); gameCamera.orthographicSize=originalCameraSize; gameCamera.farClipPlane=originalFarClip; gameCamera.orthographic=originalOrthographic; }
        }
    }
}
