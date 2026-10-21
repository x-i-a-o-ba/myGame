using System;
using System.IO;
using Roguelike.Benchmark;
using Roguelike.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roguelike.Editor
{
    public static class BenchmarkTools
    {
        public const string ScenePath = "Assets/Game/Scenes/Benchmark10k.unity";

        [MenuItem("Tools/Roguelike/Open 10k Benchmark")]
        public static void OpenBenchmark()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Roguelike/Validate Foundation")]
        public static void ValidateFoundation()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The default pipeline must be URP.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Rendering/RoguelikeForwardPlusRenderer.asset");
            if (renderer == null || renderer.renderingMode != RenderingMode.ForwardPlus)
                throw new InvalidOperationException("Expected Forward+ renderer.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Benchmark scene is missing.");
            // Runtime-created entities must retain DOTS shader variants in a Player.
            using (var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]))
            {
                var stripping = graphics.FindProperty("m_BrgStripping");
                if (stripping == null) throw new InvalidOperationException("Cannot locate BRG shader stripping settings.");
                stripping.intValue = (int)BatchRendererGroupStrippingMode.KeepAll;
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            ValidateSimulation();
            Debug.Log("ROGUELIKE_FOUNDATION_VALIDATED");
        }

        private static void ValidateSimulation()
        {
            using var world = new World("Benchmark isolated smoke test");
            var manager = world.EntityManager;
            var settings = manager.CreateEntity(typeof(BenchmarkSettings));
            manager.SetComponentData(settings, new BenchmarkSettings { SyntheticDamage = true });
            var archetype = manager.CreateArchetype(typeof(BenchmarkAgent), typeof(LocalTransform), typeof(Health), typeof(MoveSpeed));
            using var entities = manager.CreateEntity(archetype, 10000, Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                manager.SetComponentData(entities[i], new Health { Current = 1, Maximum = 100 });
                manager.SetComponentData(entities[i], new MoveSpeed { Value = 2 });
                manager.SetComponentData(entities[i], LocalTransform.Identity);
                manager.SetComponentData(entities[i], new BenchmarkAgent { Origin = new float3(i, 0, 0) });
            }
            var system = world.GetOrCreateSystem<BenchmarkSimulationSystem>();
            world.SetTime(new Unity.Core.TimeData(1, 0.2f));
            system.Update(world.Unmanaged);
            manager.CompleteAllTrackedJobs();
            for (int i = 0; i < entities.Length; i++)
            {
                var position = manager.GetComponentData<LocalTransform>(entities[i]).Position;
                var health = manager.GetComponentData<Health>(entities[i]);
                if (!math.all(math.isfinite(position)) || math.distance(position, new float3(i, 0, 0)) > 0.201f || health.Current != 100)
                    throw new InvalidOperationException("Benchmark movement bounds or synthetic health reset failed.");
            }
            manager.DestroyEntity(entities);
            manager.DestroyEntity(settings);
            system.Update(world.Unmanaged); // No singleton: RequireForUpdate must safely skip.
        }

        [MenuItem("Tools/Roguelike/Build Windows Benchmark")]
        public static void BuildWindowsBenchmark()
        {
            ValidateFoundation();
            Directory.CreateDirectory("Builds/Benchmark");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Benchmark/RoguelikeBenchmark.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Benchmark build failed: " + report.summary.result);
            Debug.Log("ROGUELIKE_BENCHMARK_BUILD_SUCCEEDED");
        }
    }
}
