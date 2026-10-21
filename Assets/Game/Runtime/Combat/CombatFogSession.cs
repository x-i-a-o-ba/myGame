// Game 视野功能已按用户要求暂停。旧实现集中保留在本文件，当前不参与编译或运行。
// 将来恢复时，需要重新建立 CombatEnemy 的视野状态及 Game 对 Roguelike.FogOfWar 的程序集引用，
// 并重新接通场景材质、控制器、输入和相应验证；不能只移除此条件编译开关。
#if false
using System;
using Roguelike.FogOfWar;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Game 战斗的玩家视野适配器。FogSolver 拥有逻辑位图；本类把同一快照交给
    /// 敌人索敌标志、Entities Graphics 显隐和场景迷雾材质。
    /// </summary>
    public sealed class CombatFogSession : IDisposable
    {
        private const int PlayerTeam = 0;
        private readonly EntityManager manager;
        private readonly EntityQuery enemyQuery;
        private readonly Transform terrainRoot;
        private readonly FogSolver solver;
        private readonly NativeList<FogSourceData> sources;
        private readonly NativeArray<Color32> pixels;
        private readonly Texture2D rawMask;
        private readonly float halfSize;
        private readonly float cellSize;
        private FogObstacle[] trees = Array.Empty<FogObstacle>();
        private FogHeightRegion[] regions = Array.Empty<FogHeightRegion>();
        private VisionSource[] sceneSources = Array.Empty<VisionSource>();
        private Renderer[][] sourceRenderers = Array.Empty<Renderer[]>();
        private bool resetVisual = true;
        private bool terrainDirty = true;
        private bool rendererSync = true;
        private bool lastNight;
        private bool lastAllVisible;
        private int lastFlareCount;
        private int lastSceneSourceCount;
        private float nextSolveTime;
        private bool disposed;

        /// <summary>白天玩家视距，单位为世界米；修改后下次调用 Tick 生效。</summary>
        public float DayRadius { get; set; } = 16f;
        /// <summary>夜晚玩家视距，单位为世界米；修改后下次调用 Tick 生效。</summary>
        public float NightRadius { get; set; } = 9f;
        /// <summary>视野快照频率。敌人位置和显示只在快照时更新，不停掉敌人模拟。</summary>
        public float RefreshRate { get; set; } = 10f;
        public FogVisualMask VisualMask { get; }

        /// <param name="enemyQuery">调用方持有、排除 Prefab 的 CombatEnemy + LocalTransform 查询。</param>
        /// <param name="terrainRoot">地形、树和高地的场景根；空值表示无障碍的平面。</param>
        public CombatFogSession(
            EntityManager manager, EntityQuery enemyQuery, float arenaHalfSize,
            Material smoothingMaterial, Transform terrainRoot, int gridSize = 256)
        {
            if (smoothingMaterial == null)
                throw new ArgumentNullException(nameof(smoothingMaterial), "Game 迷雾需要绑定 Fog Smoothing.mat。");
            if (gridSize < 32 || gridSize > 256)
                throw new ArgumentOutOfRangeException(nameof(gridSize), "迷雾网格须为 32–256 格。");

            this.manager = manager;
            this.enemyQuery = enemyQuery;
            this.terrainRoot = terrainRoot;
            halfSize = Mathf.Max(1f, arenaHalfSize);
            cellSize = halfSize * 2f / gridSize;
            solver = new FogSolver(gridSize);
            sources = new NativeList<FogSourceData>(16, Allocator.Persistent);
            pixels = new NativeArray<Color32>(gridSize * gridSize, Allocator.Persistent);
            rawMask = new Texture2D(gridSize, gridSize, TextureFormat.RGBA32, false, true)
            {
                name = "Game fog logical mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            VisualMask = new FogVisualMask(smoothingMaterial, gridSize);
            Shader.SetGlobalVector("_DemoFogBounds", new Vector4(-halfSize, -halfSize, halfSize * 2f, halfSize * 2f));
            RefreshTerrain();
        }

        /// <summary>地形或场景视野来源发生增删、移动或砍树时调用。</summary>
        public void RefreshTerrain()
        {
            ThrowIfDisposed();
            trees = terrainRoot ? terrainRoot.GetComponentsInChildren<FogObstacle>(true) : Array.Empty<FogObstacle>();
            regions = terrainRoot ? terrainRoot.GetComponentsInChildren<FogHeightRegion>(true) : Array.Empty<FogHeightRegion>();
            sceneSources = terrainRoot ? terrainRoot.GetComponentsInChildren<VisionSource>(true) : Array.Empty<VisionSource>();
            sourceRenderers = new Renderer[sceneSources.Length][];
            for (int i = 0; i < sceneSources.Length; i++)
            {
                var source = sceneSources[i];
                sourceRenderers[i] = source ? source.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
                if (source && source.lifetime > 0f && source.expiresAt == 0f)
                    source.expiresAt = Time.time + source.lifetime;
            }
            BuildMap();
            terrainDirty = true;
        }

        /// <summary>每次战斗重开，在敌人实例生成后调用；同步首张快照供本帧索敌使用。</summary>
        public void Reset(Vector3 playerPosition, bool night, bool allVisible)
        {
            ThrowIfDisposed();
            RefreshTerrain();
            solver.Reset(false);
            for (int i = 0; i < trees.Length; i++)
            {
                if (!trees[i]) continue;
                trees[i].rememberedBlue = trees[i].blocking;
                trees[i].rememberedRed = trees[i].blocking;
            }
            for (int i = 0; i < sceneSources.Length; i++)
            {
                var source = sceneSources[i];
                if (source) source.expiresAt = source.lifetime > 0f ? Time.time + source.lifetime : 0f;
            }
            resetVisual = true;
            rendererSync = true;
            lastFlareCount = 0;
            lastSceneSourceCount = 0;
            Solve(playerPosition, night, allVisible, default, false);
        }

        /// <summary>
        /// 每帧调用；内部按 RefreshRate 求解。E 技能缓冲中的活跃元素作为飞行视野来源。
        /// 昼夜、全可见、来源数量改变时立即刷新。
        /// </summary>
        public void Tick(
            Vector3 playerPosition, bool night, bool allVisible,
            DynamicBuffer<CombatVisionFlare> flares)
        {
            ThrowIfDisposed();
            int activeFlares = 0;
            for (int i = 0; i < flares.Length; i++)
                if (flares[i].RevealRemaining > 0f && flares[i].Radius > 0f) activeFlares++;
            int activeSceneSources = 0;
            for (int i = 0; i < sceneSources.Length; i++)
                if (SourceIsAlive(sceneSources[i])) activeSceneSources++;

            if (!terrainDirty && night == lastNight && allVisible == lastAllVisible &&
                activeFlares == lastFlareCount && activeSceneSources == lastSceneSourceCount &&
                Time.time < nextSolveTime)
                return;

            Solve(playerPosition, night, allVisible, flares, true);
        }

        private void Solve(
            Vector3 playerPosition, bool night, bool allVisible,
            DynamicBuffer<CombatVisionFlare> flares, bool includeFlares)
        {
            sources.Clear();
            AddSource(playerPosition, night ? NightRadius : DayRadius, false, PlayerTeam, 0f);
            int activeSceneSources = 0;
            for (int i = 0; i < sceneSources.Length; i++)
            {
                var source = sceneSources[i];
                if (!SourceIsAlive(source)) continue;
                AddSource(source.transform.position, night ? source.nightRadius : source.dayRadius,
                    source.flying, Mathf.Clamp(source.team, 0, 1), source.trueSightRadius);
                activeSceneSources++;
            }
            int activeFlares = 0;
            if (includeFlares)
            {
                for (int i = 0; i < flares.Length; i++)
                {
                    var flare = flares[i];
                    if (flare.RevealRemaining <= 0f || flare.Radius <= 0f) continue;
                    AddSource((Vector3)flare.Position, flare.Radius, true, PlayerTeam, 0f);
                    activeFlares++;
                }
            }

            // 同一 Solver 的任务在读取位图与重置来源前完成，不和 ECS 读写交错。
            solver.Schedule(sources.AsArray()).Complete();
            manager.CompleteAllTrackedJobs();
            UpdateEnemies(allVisible);
            UpdateTreeVisuals(allVisible);
            UpdateSourceVisuals(allVisible);
            UpdateVisualMask(allVisible);

            lastNight = night;
            lastAllVisible = allVisible;
            lastFlareCount = activeFlares;
            lastSceneSourceCount = activeSceneSources;
            nextSolveTime = Time.time + 1f / Mathf.Max(1f, RefreshRate);
            terrainDirty = false;
            rendererSync = false;
            resetVisual = false;
        }

        private bool SourceIsAlive(VisionSource source) =>
            source && source.isActiveAndEnabled && source.providesVision &&
            (source.expiresAt <= 0f || Time.time < source.expiresAt);

        private void AddSource(Vector3 position, float radiusMetres, bool flying, int team, float detectionMetres)
        {
            int2 cell = Cell(position);
            if (!Inside(cell)) return;
            sources.Add(new FogSourceData
            {
                cell = cell,
                radius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, radiusMetres) / cellSize), 0, solver.Size),
                detectionRadius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, detectionMetres) / cellSize), 0, solver.Size),
                team = team,
                flying = flying
            });
        }

        private void UpdateEnemies(bool allVisible)
        {
            // 查询实体快照允许下方只对显隐发生变化的敌人作结构变更。
            using var enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < enemies.Length; i++)
            {
                Entity entity = enemies[i];
                var position = manager.GetComponentData<LocalTransform>(entity).Position;
                var enemy = manager.GetComponentData<CombatEnemy>(entity);
                bool visible = allVisible || solver.CanSee(Index((Vector3)position), PlayerTeam, enemy.Invisible != 0);
                byte value = visible ? (byte)1 : (byte)0;
                bool changed = enemy.VisibleToPlayer != value;
                if (changed)
                {
                    enemy.VisibleToPlayer = value;
                    manager.SetComponentData(entity, enemy);
                }
                if (!changed && !rendererSync) continue;

                bool hidden = manager.HasComponent<DisableRendering>(entity);
                if (visible && hidden) manager.RemoveComponent<DisableRendering>(entity);
                else if (!visible && !hidden) manager.AddComponent<DisableRendering>(entity);
            }
        }

        private void UpdateTreeVisuals(bool allVisible)
        {
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                if (!tree) continue;
                Renderer visual = tree.visual ? tree.visual : tree.GetComponentInChildren<Renderer>(true);
                if (!visual) continue;
                int index = Index(tree.transform.position);
                if (index < 0) { visual.enabled = false; continue; }
                bool visible = (solver.Visible[index] & 1u) != 0;
                if (visible) tree.rememberedBlue = tree.blocking;
                bool explored = (solver.Explored[index] & 1u) != 0;
                bool show = allVisible ? tree.blocking : explored && tree.rememberedBlue;
                if (visual.enabled != show) visual.enabled = show;
            }
        }

        private void UpdateSourceVisuals(bool allVisible)
        {
            for (int i = 0; i < sceneSources.Length; i++)
            {
                var source = sceneSources[i];
                if (!source) continue;
                bool alive = SourceIsAlive(source);
                bool visible = alive && (allVisible || source.team == PlayerTeam ||
                    solver.CanSee(Index(source.transform.position), PlayerTeam, source.invisible));
                var renderers = sourceRenderers[i];
                for (int r = 0; r < renderers.Length; r++)
                {
                    var visual = renderers[r];
                    if (visual && visual.enabled != visible) visual.enabled = visible;
                }
            }
        }

        private void UpdateVisualMask(bool allVisible)
        {
            bool changed = resetVisual;
            // NativeArray 的副本共享底层内存；通过局部变量写入可保留字段的只读所有权。
            var writablePixels = pixels;
            for (int i = 0; i < writablePixels.Length; i++)
            {
                bool visible = allVisible || (solver.Visible[i] & 1u) != 0;
                bool explored = allVisible || (solver.Explored[i] & 1u) != 0;
                var pixel = new Color32(visible ? (byte)255 : (byte)0,
                    explored ? (byte)255 : (byte)0, 0, 255);
                if (writablePixels[i].r != pixel.r || writablePixels[i].g != pixel.g) changed = true;
                writablePixels[i] = pixel;
            }
            if (changed)
            {
                rawMask.SetPixelData(writablePixels, 0);
                rawMask.Apply(false);
                VisualMask.Push(rawMask, 0.85f, 0.12f, resetVisual);
            }
            Shader.SetGlobalFloat("_DemoFogOmniscient", allVisible ? 1f : 0f);
        }

        private void BuildMap()
        {
            int size = solver.Size;
            for (int y = 0; y < size; y++)
            {
                float z = -halfSize + (y + 0.5f) * cellSize;
                for (int x = 0; x < size; x++)
                {
                    float worldX = -halfSize + (x + 0.5f) * cellSize;
                    byte height = 0;
                    for (int r = 0; r < regions.Length; r++)
                    {
                        var region = regions[r];
                        if (!region || !region.isActiveAndEnabled) continue;
                        var centre = region.transform.position;
                        if (Mathf.Abs(worldX - centre.x) <= region.size.x * 0.5f &&
                            Mathf.Abs(z - centre.z) <= region.size.y * 0.5f)
                            height = (byte)Mathf.Max(height, Mathf.Clamp(region.level, 0, 255));
                    }
                    int index = y * size + x;
                    solver.Heights[index] = height;
                    solver.Blockers[index] = 0;
                }
            }

            for (int i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                if (!tree || !tree.isActiveAndEnabled || !tree.blocking) continue;
                int2 centre = Cell(tree.transform.position);
                int radius = Mathf.CeilToInt(Mathf.Max(0f, tree.radius) / cellSize);
                for (int y = Mathf.Max(0, centre.y - radius); y <= Mathf.Min(size - 1, centre.y + radius); y++)
                    for (int x = Mathf.Max(0, centre.x - radius); x <= Mathf.Min(size - 1, centre.x + radius); x++)
                        if (math.lengthsq(new int2(x, y) - centre) <= radius * radius)
                            solver.Blockers[y * size + x] = 1;
            }
        }

        private int2 Cell(Vector3 position) => (int2)math.floor(
            new float2((position.x + halfSize) / cellSize, (position.z + halfSize) / cellSize));

        private bool Inside(int2 cell) => cell.x >= 0 && cell.y >= 0 && cell.x < solver.Size && cell.y < solver.Size;

        private int Index(Vector3 position)
        {
            int2 cell = Cell(position);
            return Inside(cell) ? cell.y * solver.Size + cell.x : -1;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CombatFogSession));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            VisualMask.Dispose();
#if UNITY_EDITOR
            // EditMode integration checks must release their session-owned texture immediately.
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(rawMask);
            else UnityEngine.Object.Destroy(rawMask);
#else
            UnityEngine.Object.Destroy(rawMask);
#endif
            pixels.Dispose();
            sources.Dispose();
            solver.Dispose();
            Shader.SetGlobalFloat("_DemoFogOmniscient", 0f);
            Shader.SetGlobalVector("_DemoFogBounds", Vector4.zero);
        }
    }
}
#endif
