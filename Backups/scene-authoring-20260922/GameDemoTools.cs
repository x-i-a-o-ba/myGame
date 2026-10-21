using System;
using System.IO;
using System.Linq;
using Roguelike.Combat;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Editor
{
    public static class GameDemoTools
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/Roguelike/Open Game Demo")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Roguelike/Prepare Game Demo Scene")]
        public static void PrepareScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var demo = UnityEngine.Object.FindFirstObjectByType<CombatDemoController>();
            if (demo == null) demo = new GameObject("Game Demo").AddComponent<CombatDemoController>();
            if (demo.GetComponent<CombatDemoProbe>() == null) demo.gameObject.AddComponent<CombatDemoProbe>();
            demo.gameCamera = Camera.main;
            demo.playerMaterial = Material("DemoPlayer", new Color(0.14f, 0.65f, 0.85f), true);
            demo.enemyMaterial = Material("DemoEnemy", new Color(0.72f, 0.18f, 0.13f), true);
            demo.projectileMaterial = Material("DemoProjectile", new Color(1, 0.79f, 0.19f), false);
            demo.markerMaterial = Material("DemoMarker", new Color(0.2f, 0.9f, 0.65f), false);
            demo.groundMaterial = Material("DemoGround", new Color(0.24f, 0.30f, 0.29f), true);
            if (demo.gameCamera != null)
            {
                demo.gameCamera.orthographic = true;
                demo.gameCamera.orthographicSize = 28;
                demo.gameCamera.farClipPlane = 900;
                demo.gameCamera.transform.SetPositionAndRotation(new Vector3(0, 45, -28), Quaternion.Euler(58, 0, 0));
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.shadows = LightShadows.None;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("GAME_DEMO_SCENE_PREPARED");
        }

        private static Material Material(string name, Color color, bool lit)
        {
            string path = "Assets/Game/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP shader unavailable.");
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("Tools/Roguelike/Build Windows Game Demo")]
        public static void Build()
        {
            BenchmarkTools.ValidateFoundation();
            CombatValidation.Validate();
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new InvalidOperationException("Game scene missing.");
            Directory.CreateDirectory("Builds/GameDemo");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/GameDemo/GameDemo.exe", options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Game Demo build failed.");
            Debug.Log("GAME_DEMO_BUILD_SUCCEEDED");
        }

        public static void PrepareAndBuild()
        {
            PrepareScene();
            Build();
        }
    }
}
