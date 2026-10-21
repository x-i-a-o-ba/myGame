using System;
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
    [DisallowMultipleComponent]
    public sealed class CombatDemoController : MonoBehaviour
    {
        [Header("Scene assets")]
        public Camera gameCamera;
        public Material playerMaterial, enemyMaterial, projectileMaterial, groundMaterial, markerMaterial;
        [Header("Population and arena")]
        [Range(1, 30000)] public int enemyCount = 10000;
        [Min(30)] public float arenaHalfSize = 125;
        [Min(3)] public float safeSpawnRadius = 9;
        [Header("Player")]
        [Min(1)] public float playerHealth = 300;
        [Min(0.1f)] public float playerSpeed = 10;
        [Min(1)] public float attackRange = 20;
        [Min(0.02f)] public float attackInterval = 0.12f;
        [Min(0.1f)] public float projectileSpeed = 38;
        [Min(1)] public float projectileDamage = 30;
        [Header("Enemies")]
        [Min(1)] public float enemyHealth = 30;
        [Min(0)] public float enemySpeed = 2;
        [Min(0.1f)] public float enemyAttackRange = 1.25f;
        [Min(0)] public float enemyAttackDamage = 4;
        [Min(0.1f)] public float enemyAttackInterval = 1;
        [Header("Camera")]
        [Range(12, 160)] public float cameraZoom = 28;

        private World world;
        private Entity session;
        private EntityQuery ownedQuery;
        private bool ownsSession;
        private Transform visualRoot, playerView, destinationMarker;
        private CombatHud hud;
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
                if (playerMaterial == null || enemyMaterial == null || projectileMaterial == null || groundMaterial == null)
                    throw new InvalidOperationException("Game Demo materials are missing. Run Tools/Roguelike/Prepare Game Demo Scene.");
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
                CreateViews();
                RestartRun();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void CreateViews()
        {
            visualRoot = new GameObject("Demo runtime views").transform;
            visualRoot.SetParent(transform, false);
            var ground = Primitive("Arena", PrimitiveType.Plane, groundMaterial);
            ground.transform.localScale = Vector3.one * (arenaHalfSize * 2 / 10);
            playerView = Primitive("Player", PrimitiveType.Capsule, playerMaterial).transform;
            playerView.localScale = new Vector3(0.8f, 0.95f, 0.8f);
            var facing = Primitive("Facing", PrimitiveType.Cube, projectileMaterial).transform;
            facing.SetParent(playerView, false);
            facing.localPosition = new Vector3(0, 0.25f, 0.65f);
            facing.localScale = new Vector3(0.2f, 0.2f, 0.6f);
            destinationMarker = Primitive("Move destination", PrimitiveType.Cylinder, markerMaterial != null ? markerMaterial : playerMaterial).transform;
            destinationMarker.localScale = new Vector3(0.65f, 0.025f, 0.65f);
            destinationMarker.gameObject.SetActive(false);
            hud = new GameObject("Combat HUD").AddComponent<CombatHud>();
            hud.transform.SetParent(visualRoot, false);
            hud.Initialize(this);
            gameCamera.orthographic = true;
            gameCamera.farClipPlane = 900;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
        }

        private GameObject Primitive(string label, PrimitiveType type, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = label;
            instance.transform.SetParent(visualRoot, false);
            var collider = instance.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return instance;
        }

        public void RestartRun()
        {
            if (!ownsSession || world == null || !world.IsCreated) return;
            var manager = world.EntityManager;
            manager.CompleteAllTrackedJobs();
            manager.DestroyEntity(ownedQuery);
            int count = Mathf.Clamp(enemyCount, 1, 30000);
            arenaHalfSize = Mathf.Max(30, arenaHalfSize);
            float safeRadius = Mathf.Clamp(safeSpawnRadius, 3, arenaHalfSize * 0.5f);

            Mesh cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            Mesh bulletMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            Entity bulletPrefab = CreateRenderedEntity(bulletMesh, projectileMaterial);
            manager.AddComponent<CombatProjectile>(bulletPrefab);
            manager.AddComponent<Prefab>(bulletPrefab);

            Entity enemyPrefab = CreateRenderedEntity(cubeMesh, enemyMaterial);
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
            hud.Refresh(State, 0, attackRange);
            Debug.Log($"GAME_DEMO_STARTED enemies={count}; mouse movement, pursuit, homing projectiles enabled.");
        }

        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temporary = GameObject.CreatePrimitive(type);
            temporary.SetActive(false);
            var mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporary);
            return mesh;
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
                hud.Refresh(player, smoothFrameTime > 0 ? 1 / smoothFrameTime : 0, attackRange);
            }
        }

        private void OnDestroy()
        {
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
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            ownsSession = false;
        }
    }
}
