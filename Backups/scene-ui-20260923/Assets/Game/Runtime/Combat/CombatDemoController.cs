using System;
using NaughtyAttributes;
using Roguelike.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>当前 Demo 会话入口；负责连接场景资源、ECS 会话与展示。职责拆分方案见 Docs/architecture-guidelines.md。</summary>
    [DisallowMultipleComponent]
    public sealed class CombatDemoController : MonoBehaviour
    {
        [Header("场景资源")]
        [InfoBox("右键移动，自动攻击最近敌人。战斗参数重开后生效；选中对象可查看 Scene 范围。", EInfoBoxType.Normal)]
        [Label("游戏相机"), Tooltip("俯视角正交相机。")]
        public Camera gameCamera;
        public Material playerMaterial, enemyMaterial, projectileMaterial, groundMaterial, markerMaterial;
        [Header("数量与场地（重开后生效）")]
        [Label("敌人数量"), Tooltip("每次开局生成的可战斗敌人数，不含子弹。")]
        [Range(1, 30000)] public int enemyCount = 10000;
        [Label("场地半宽"), Tooltip("世界单位；玩家移动边界。")]
        [Min(30)] public float arenaHalfSize = 125;
        [Label("出生安全半径"), Tooltip("世界单位；初始生成时避开玩家的区域。")]
        [Min(3)] public float safeSpawnRadius = 9;
        [Header("玩家（重开后生效）")]
        [Label("玩家生命"), Tooltip("重开时恢复的生命值。")]
        [Min(1)] public float playerHealth = 300;
        [Label("玩家移速"), Tooltip("世界单位/秒。")]
        [Min(0.1f)] public float playerSpeed = 10;
        [Label("自动攻击范围"), Tooltip("世界单位；范围内选择最近存活敌人。")]
        [Min(1)] public float attackRange = 20;
        [Label("开火间隔"), Tooltip("秒；越小发射越快。")]
        [Min(0.02f)] public float attackInterval = 0.12f;
        [Label("子弹速度"), Tooltip("世界单位/秒。")]
        [Min(0.1f)] public float projectileSpeed = 38;
        [Label("子弹伤害"), Tooltip("每次命中造成的生命损失。")]
        [Min(1)] public float projectileDamage = 30;
        [Header("敌人（重开后生效）")]
        [Label("敌人生命"), Tooltip("每只敌人的初始生命值。")]
        [Min(1)] public float enemyHealth = 30;
        [Label("敌人移速"), Tooltip("世界单位/秒。")]
        [Min(0)] public float enemySpeed = 2;
        [Label("近战距离"), Tooltip("世界单位；进入后按攻击间隔造成伤害。")]
        [Min(0.1f)] public float enemyAttackRange = 1.25f;
        [Label("近战伤害"), Tooltip("单只敌人每次攻击的伤害。")]
        [Min(0)] public float enemyAttackDamage = 4;
        [Label("近战间隔"), Tooltip("秒；每只敌人独立计时。")]
        [Min(0.1f)] public float enemyAttackInterval = 1;
        [Header("相机")]
        [Label("镜头视野"), Tooltip("正交尺寸；可在运行时滚轮调整。")]
        [Range(12, 160)] public float cameraZoom = 28;

        private World world;
        private Entity session;
        private EntityQuery ownedQuery;
        private bool ownsSession;
        [Header("场景展示引用（直接在 Hierarchy 修改布局/外观）")]
        [SerializeField, Label("主角外观")] private Transform playerView;
        [SerializeField, Label("移动标记")] private Transform destinationMarker;
        [SerializeField, Label("战斗 HUD")] private CombatHud hud;
        [Header("ECS 批量渲染资源")]
        [SerializeField] private Mesh enemyMesh, projectileMesh;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private bool originalOrthographic;
        private float originalCameraSize, originalFarClip;
        private CameraClearFlags originalClearFlags;
        private Color originalBackground;
        private float nextHudUpdate;
        private float smoothFrameTime;

        public bool IsReady => ownsSession && world != null && world.IsCreated && world.EntityManager.Exists(session);
        public CombatPlayer State => IsReady ? world.EntityManager.GetComponentData<CombatPlayer>(session) : default;

        private void Start()
        {
            if (!enabled) return;
            try
            {
                if (enemyMaterial == null || projectileMaterial == null || enemyMesh == null || projectileMesh == null)
                    throw new InvalidOperationException("Game Demo materials are missing. Run Tools/Roguelike/Prepare Game Demo Scene.");
                if (playerView == null || destinationMarker == null || hud == null || !hud.HasBindings)
                    throw new InvalidOperationException("Game 场景展示引用不完整，请检查 Demo 和 HUD 的 Inspector 引用。");
                if (gameCamera == null) gameCamera = Camera.main;
                if (gameCamera == null) throw new InvalidOperationException("Game scene needs a camera.");
                originalCameraPosition = gameCamera.transform.position;
                originalCameraRotation = gameCamera.transform.rotation;
                originalOrthographic = gameCamera.orthographic;
                originalCameraSize = gameCamera.orthographicSize;
                originalFarClip = gameCamera.farClipPlane;
                originalClearFlags = gameCamera.clearFlags;
                originalBackground = gameCamera.backgroundColor;
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) throw new InvalidOperationException("ECS world is unavailable.");
                using (var existing = world.EntityManager.CreateEntityQuery(typeof(CombatSettings)))
                    if (!existing.IsEmptyIgnoreFilter) throw new InvalidOperationException("Only one Game Demo may be active.");
                ownedQuery = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<CombatOwned>() },
                    Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
                });
                ownsSession = true;
                hud.RestartRequested += RestartRun;
                RestartRun();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        /// <summary>重建本会话的 ECS 状态；场景拥有的 UI、外观、字体和地面保持不变。</summary>
        public void RestartRun()
        {
            if (!ownsSession || world == null || !world.IsCreated) return;
            var manager = world.EntityManager;
            manager.CompleteAllTrackedJobs();
            manager.DestroyEntity(ownedQuery);
            int count = Mathf.Clamp(enemyCount, 1, 30000);
            arenaHalfSize = Mathf.Max(30, arenaHalfSize);
            float safeRadius = Mathf.Clamp(safeSpawnRadius, 3, arenaHalfSize * 0.5f);

            Entity bulletPrefab = CreateRenderedEntity(projectileMesh, projectileMaterial);
            manager.AddComponent<CombatProjectile>(bulletPrefab);
            manager.AddComponent<Prefab>(bulletPrefab);

            Entity enemyPrefab = CreateRenderedEntity(enemyMesh, enemyMaterial);
            manager.AddComponent<EnemyTag>(enemyPrefab);
            manager.AddComponentData(enemyPrefab, new Health { Current = Mathf.Max(1, enemyHealth), Maximum = Mathf.Max(1, enemyHealth) });
            manager.AddComponentData(enemyPrefab, new MoveSpeed { Value = Mathf.Max(0, enemySpeed) });
            manager.AddComponent<CombatEnemy>(enemyPrefab);
            manager.AddComponent<Prefab>(enemyPrefab);
            // Deterministic sunflower disk: no overlap with the player's initial safe zone.
            float outerRadius = arenaHalfSize - 8;
            using (var enemies = new NativeArray<Entity>(count, Allocator.Temp))
            {
                manager.Instantiate(enemyPrefab, enemies);
                for (int i = 0; i < count; i++)
                {
                    float radius = math.sqrt(math.lerp(safeRadius * safeRadius, outerRadius * outerRadius, (i + 0.5f) / count));
                    float angle = i * 2.39996323f;
                    var position = new float3(math.cos(angle) * radius, 0.55f, math.sin(angle) * radius);
                    manager.SetComponentData(enemies[i], LocalTransform.FromPositionRotationScale(position, quaternion.identity, 0.85f));
                    manager.SetComponentData(enemies[i], new CombatEnemy { AttackCooldown = (i % 17) / 17f * enemyAttackInterval });
                }
            }
            manager.DestroyEntity(enemyPrefab);
            session = manager.CreateEntity(typeof(CombatOwned), typeof(CombatPlayer), typeof(CombatSettings));
            manager.SetComponentData(session, new CombatSettings
            {
                ProjectilePrefab = bulletPrefab,
                PlayerSpeed = Mathf.Max(0.1f, playerSpeed),
                AttackRange = Mathf.Max(1, attackRange),
                AttackInterval = Mathf.Max(0.02f, attackInterval),
                ProjectileSpeed = Mathf.Max(0.1f, projectileSpeed),
                ProjectileDamage = Mathf.Max(1, projectileDamage),
                EnemyAttackRange = Mathf.Max(0.1f, enemyAttackRange),
                EnemyAttackDamage = Mathf.Max(0, enemyAttackDamage),
                EnemyAttackInterval = Mathf.Max(0.1f, enemyAttackInterval),
                ArenaHalfSize = arenaHalfSize
            });
            manager.SetComponentData(session, new CombatPlayer
            {
                Position = new float3(0, 0.55f, 0), Destination = new float3(0, 0.55f, 0),
                Facing = new float3(0, 0, 1), Health = Mathf.Max(1, playerHealth), MaximumHealth = Mathf.Max(1, playerHealth),
                AliveEnemies = count
            });
            destinationMarker.gameObject.SetActive(false);
            nextHudUpdate = 0;
            RefreshHud(State, 0);
            Debug.Log($"GAME_DEMO_STARTED enemies={count}; mouse movement, pursuit, homing projectiles enabled.");
        }

        // 胶水层在低频展示边界转换数据，HUD 不依赖 CombatPlayer 或 EntityManager。
        private void RefreshHud(CombatPlayer player, float fps)
        {
            hud.Refresh(new CombatHudState
            {
                Health = player.Health, MaximumHealth = player.MaximumHealth, Elapsed = player.Elapsed,
                AliveEnemies = player.AliveEnemies, Kills = player.Kills, ActiveProjectiles = player.ActiveProjectiles
            }, fps, attackRange);
        }

        private Entity CreateRenderedEntity(Mesh mesh, Material material)
        {
            var manager = world.EntityManager;
            Entity entity = manager.CreateEntity(typeof(CombatOwned), typeof(LocalTransform), typeof(LocalToWorld));
            manager.SetComponentData(entity, LocalTransform.Identity);
            RenderMeshUtility.AddComponents(entity, manager,
                new RenderMeshDescription(ShadowCastingMode.Off, false),
                new RenderMeshArray(new[] { material }, new[] { mesh }), MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            return entity;
        }

        // Shared by mouse input and the automated Player interaction probe.
        public void SetDestination(Vector3 target)
        {
            if (!IsReady) return;
            var player = State;
            if (player.Health <= 0) return;
            player.Destination = new float3(Mathf.Clamp(target.x, -arenaHalfSize + 2, arenaHalfSize - 2),
                player.Position.y, Mathf.Clamp(target.z, -arenaHalfSize + 2, arenaHalfSize - 2));
            world.EntityManager.SetComponentData(session, player);
            destinationMarker.position = new Vector3(player.Destination.x, 0.035f, player.Destination.z);
            destinationMarker.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsReady) return;
            if (Input.GetKeyDown(KeyCode.F5)) RestartRun();
            bool overHud = hud.BlocksPointer(Input.mousePosition);
            if (!overHud)
            {
                cameraZoom = Mathf.Clamp(cameraZoom - Input.mouseScrollDelta.y * Mathf.Max(2, cameraZoom * 0.08f), 12, 160);
                if (Input.GetMouseButton(1)) TryMoveFromScreen(Input.mousePosition);
            }
        }

        public bool TryMoveFromScreen(Vector2 screenPosition)
        {
            if (!IsReady || State.Health <= 0 || hud.BlocksPointer(screenPosition)) return false;
            var ground = new Plane(Vector3.up, Vector3.zero);
            Ray ray = gameCamera.ScreenPointToRay(screenPosition);
            if (!ground.Raycast(ray, out float distance)) return false;
            SetDestination(ray.GetPoint(distance));
            return true;
        }

        private void LateUpdate()
        {
            if (!IsReady) return;
            var player = State;
            playerView.position = new Vector3(player.Position.x, 0.95f, player.Position.z);
            Vector3 facing = new Vector3(player.Facing.x, 0, player.Facing.z);
            if (facing.sqrMagnitude > 0.001f) playerView.rotation = Quaternion.LookRotation(facing);
            if (math.distancesq(player.Position, player.Destination) < 0.1f) destinationMarker.gameObject.SetActive(false);
            gameCamera.orthographicSize = cameraZoom;
            var rotation = Quaternion.Euler(58, 0, 0);
            // Offset player above the bottom HUD and keep the entire arena in the far plane.
            gameCamera.transform.SetPositionAndRotation((Vector3)player.Position + rotation * new Vector3(0, -cameraZoom * 0.13f, -300), rotation);
            smoothFrameTime = Mathf.Lerp(smoothFrameTime, Time.unscaledDeltaTime, 0.08f);
            if (Time.unscaledTime >= nextHudUpdate)
            {
                nextHudUpdate = Time.unscaledTime + 0.1f;
                RefreshHud(player, smoothFrameTime > 0 ? 1 / smoothFrameTime : 0);
            }
        }

        private void OnDestroy()
        {
            if (hud != null) hud.RestartRequested -= RestartRun;
            if (!ownsSession) return;
            if (world != null && world.IsCreated)
            {
                world.EntityManager.CompleteAllTrackedJobs();
                world.EntityManager.DestroyEntity(ownedQuery);
                ownedQuery.Dispose();
            }
            if (gameCamera != null)
            {
                gameCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                gameCamera.orthographic = originalOrthographic;
                gameCamera.orthographicSize = originalCameraSize;
                gameCamera.farClipPlane = originalFarClip;
                gameCamera.clearFlags = originalClearFlags;
                gameCamera.backgroundColor = originalBackground;
            }
            // 场景资源由场景生命周期管理，销毁会话不销毁可编辑的展示节点。
            if (destinationMarker != null) destinationMarker.gameObject.SetActive(false);
            ownsSession = false;
        }
    }
}
