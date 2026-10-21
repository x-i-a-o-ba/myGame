using System;
using System.Globalization;
using System.IO;
using System.Text;
using Roguelike.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Roguelike.Benchmark
{
    [DisallowMultipleComponent]
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        [Range(1, 100000)] public int enemyCount = 10000;
        public Material enemyMaterial;
        public bool renderEntities = true;
        public bool syntheticDamage;
        [Min(0)] public float warmupSeconds = 10;
        [Min(1)] public float sampleSeconds = 60;

        private World world;
        private EntityQuery ownedQuery;
        private Camera benchmarkCamera;
        private ProfilerRecorder gcRecorder;
        private readonly float[] frameTimes = new float[262144];
        private int sampleCount;
        private double startedAt;
        private double measuredSeconds;
        private long totalGcBytes;
        private long maxGcBytes;
        private int oldVsync;
        private int oldTargetFps;
        private bool oldRunInBackground;
        private bool ownsSession;
        private bool finished;
        private bool quitAfterReport;
        private string reportDirectory;

        private void OnEnable()
        {
            try { Initialize(); }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Initialize()
        {
            ReadArguments();
            enemyCount = Mathf.Clamp(enemyCount, 1, 100000);
            warmupSeconds = Mathf.Max(0, warmupSeconds);
            sampleSeconds = Mathf.Max(1, sampleSeconds);
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException("Default ECS world is unavailable.");
            var manager = world.EntityManager;
            using (var existing = manager.CreateEntityQuery(typeof(BenchmarkOwned)))
                if (!existing.IsEmptyIgnoreFilter) throw new InvalidOperationException("Only one benchmark session may run at a time.");
            if (renderEntities && enemyMaterial == null) throw new InvalidOperationException("Assign the benchmark URP material.");

            ownedQuery = manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<BenchmarkOwned>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
            oldVsync = QualitySettings.vSyncCount;
            oldTargetFps = Application.targetFrameRate;
            oldRunInBackground = Application.runInBackground;
            ownsSession = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;

            Entity settings = manager.CreateEntity(typeof(BenchmarkOwned), typeof(BenchmarkSettings));
            manager.SetComponentData(settings, new BenchmarkSettings { SyntheticDamage = syntheticDamage });
            Entity prototype = manager.CreateEntity(typeof(BenchmarkOwned), typeof(EnemyTag), typeof(Health),
                typeof(MoveSpeed), typeof(BenchmarkAgent), typeof(LocalTransform), typeof(LocalToWorld));
            manager.SetComponentData(prototype, new Health { Current = 100, Maximum = 100 });
            manager.SetComponentData(prototype, new MoveSpeed { Value = 2 });
            manager.SetComponentData(prototype, LocalTransform.FromScale(0.6f));
            if (renderEntities)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                cube.SetActive(false);
                Destroy(cube);
                var description = new RenderMeshDescription(shadowCastingMode: ShadowCastingMode.Off,
                    receiveShadows: false);
                RenderMeshUtility.AddComponents(prototype, manager, description,
                    new RenderMeshArray(new[] { enemyMaterial }, new[] { mesh }),
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }
            manager.AddComponent<Prefab>(prototype);
            int side = Mathf.CeilToInt(Mathf.Sqrt(enemyCount));
            using (var entities = new NativeArray<Entity>(enemyCount, Allocator.Temp))
            {
                manager.Instantiate(prototype, entities);
                for (int i = 0; i < entities.Length; i++)
                {
                    float3 origin = new float3(i % side - (side - 1) * 0.5f, 0, i / side - (side - 1) * 0.5f);
                    manager.SetComponentData(entities[i], new BenchmarkAgent { Origin = origin, Phase = i * 0.618034f });
                    manager.SetComponentData(entities[i], LocalTransform.FromPositionRotationScale(origin, quaternion.identity, 0.6f));
                }
            }
            manager.DestroyEntity(prototype);

            var cameraObject = new GameObject("Benchmark Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0, side + 10, 0), Quaternion.Euler(90, 0, 0));
            benchmarkCamera = cameraObject.GetComponent<Camera>();
            benchmarkCamera.orthographic = true;
            benchmarkCamera.orthographicSize = (side * 0.5f + 2) / Mathf.Min(1, benchmarkCamera.aspect);
            benchmarkCamera.farClipPlane = side + 30;
            benchmarkCamera.clearFlags = CameraClearFlags.SolidColor;
            benchmarkCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            benchmarkCamera.allowHDR = false;
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            sampleCount = 0;
            measuredSeconds = 0;
            totalGcBytes = maxGcBytes = 0;
            finished = false;
            startedAt = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Benchmark started: {enemyCount} agents; rendering={renderEntities}; syntheticDamage={syntheticDamage}. Warmup {warmupSeconds}s, sample {sampleSeconds}s. This is NOT a full-combat benchmark.");
        }

        private void Update()
        {
            if (!ownsSession || finished || Time.realtimeSinceStartupAsDouble - startedAt < warmupSeconds) return;
            float milliseconds = Time.unscaledDeltaTime * 1000;
            frameTimes[sampleCount++] = milliseconds;
            measuredSeconds += milliseconds / 1000.0;
            if (gcRecorder.Valid)
            {
                long bytes = gcRecorder.LastValue;
                totalGcBytes += bytes;
                maxGcBytes = Math.Max(maxGcBytes, bytes);
            }
            if (measuredSeconds >= sampleSeconds || sampleCount == frameTimes.Length)
            {
                finished = true;
                try
                {
                    ExportReport();
                    if (quitAfterReport && !Application.isEditor) Application.Quit(0);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    if (quitAfterReport && !Application.isEditor) Application.Quit(1);
                }
            }
        }

        private void ExportReport()
        {
            string directory = string.IsNullOrWhiteSpace(reportDirectory)
                ? Path.Combine(Application.persistentDataPath, "Benchmarks") : reportDirectory;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "benchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            var csv = new StringBuilder("frame,milliseconds\n");
            for (int i = 0; i < sampleCount; i++)
                csv.Append(i).Append(',').Append(frameTimes[i].ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
            File.WriteAllText(path + ".csv", csv.ToString());
            Array.Sort(frameTimes, 0, sampleCount);
            var report = new BenchmarkReport
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                editor = Application.isEditor,
                developmentBuild = Debug.isDebugBuild,
                cpu = SystemInfo.processorType,
                logicalCores = SystemInfo.processorCount,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                memoryMB = SystemInfo.systemMemorySize,
                resolution = Screen.width + "x" + Screen.height,
                workload = syntheticDamage ? "movement+synthetic-health-writes" : "movement-only",
                rendered = renderEntities,
                requestedEnemies = enemyCount,
                simulatedEnemies = ownedQuery.CalculateEntityCount() - 1,
                visibility = "Camera frames all agents geometrically; GPU visibility count not instrumented.",
                warmupSeconds = warmupSeconds,
                durationSeconds = measuredSeconds,
                frames = sampleCount,
                averageFps = sampleCount / measuredSeconds,
                p50Ms = Percentile(0.50f), p95Ms = Percentile(0.95f), p99Ms = Percentile(0.99f),
                gcRecorderAvailable = gcRecorder.Valid,
                totalGcBytes = gcRecorder.Valid ? totalGcBytes : -1,
                maxGcBytesPerFrame = gcRecorder.Valid ? maxGcBytes : -1,
                allocatedMemoryAtEnd = Profiler.GetTotalAllocatedMemoryLong(),
                limitations = "Cube mesh, URP Unlit, no animation/AI/collision/projectiles/drops. GPU timing and peak memory require external profiling. Not full combat."
            };
            File.WriteAllText(path + ".json", JsonUtility.ToJson(report, true));
            Debug.Log($"Benchmark report: {path}.json; average {report.averageFps:F1} FPS, p95 {report.p95Ms:F2} ms.");
        }

        private float Percentile(float fraction) => frameTimes[Mathf.Clamp(Mathf.CeilToInt(sampleCount * fraction) - 1, 0, sampleCount - 1)];

        private void ReadArguments()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                switch (arguments[i])
                {
                    case "--benchmark-count": enemyCount = int.Parse(arguments[++i], CultureInfo.InvariantCulture); break;
                    case "--benchmark-warmup": warmupSeconds = float.Parse(arguments[++i], CultureInfo.InvariantCulture); break;
                    case "--benchmark-seconds": sampleSeconds = float.Parse(arguments[++i], CultureInfo.InvariantCulture); break;
                    case "--benchmark-output": reportDirectory = arguments[++i]; break;
                    case "--benchmark-no-render": renderEntities = false; break;
                    case "--benchmark-synthetic-damage": syntheticDamage = true; break;
                    case "--benchmark-quit": quitAfterReport = true; break;
                }
            }
        }

        private void OnDisable()
        {
            if (!ownsSession) return;
            if (world != null && world.IsCreated)
            {
                world.EntityManager.CompleteAllTrackedJobs();
                world.EntityManager.DestroyEntity(ownedQuery);
                ownedQuery.Dispose();
            }
            gcRecorder.Dispose();
            if (benchmarkCamera != null) Destroy(benchmarkCamera.gameObject);
            QualitySettings.vSyncCount = oldVsync;
            Application.targetFrameRate = oldTargetFps;
            Application.runInBackground = oldRunInBackground;
            ownsSession = false;
        }

        [Serializable]
        private sealed class BenchmarkReport
        {
            public string unityVersion, platform, cpu, gpu, graphicsApi, resolution, workload, visibility, limitations;
            public bool editor, developmentBuild, rendered, gcRecorderAvailable;
            public int logicalCores, memoryMB, requestedEnemies, simulatedEnemies, frames;
            public float warmupSeconds, p50Ms, p95Ms, p99Ms;
            public double durationSeconds, averageFps;
            public long totalGcBytes, maxGcBytesPerFrame, allocatedMemoryAtEnd;
        }
    }
}
