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
    public static class GameTools
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/Roguelike/打开游戏场景")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void PrepareScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var demo = UnityEngine.Object.FindFirstObjectByType<GameCombatController>();
            if (demo == null) demo = new GameObject("Game Combat").AddComponent<GameCombatController>();
            if (demo.GetComponent<GameCombatProbe>() == null) demo.gameObject.AddComponent<GameCombatProbe>();
            bool needsMigration = UnityEngine.Object.FindFirstObjectByType<CombatHud>() == null;
            if (demo.gameCamera == null) demo.gameCamera = Camera.main;
            if (demo.playerMaterial == null) demo.playerMaterial = Material("GamePlayer", new Color(0.14f, 0.65f, 0.85f), true);
            if (demo.enemyMaterial == null) demo.enemyMaterial = Material("GameEnemy", new Color(0.72f, 0.18f, 0.13f), true);
            if (demo.projectileMaterial == null) demo.projectileMaterial = Material("GameProjectile", new Color(1, 0.79f, 0.19f), false);
            if (demo.markerMaterial == null) demo.markerMaterial = Material("GameMarker", new Color(0.2f, 0.9f, 0.65f), false);
            if (demo.groundMaterial == null) demo.groundMaterial = Material("GameGround", new Color(0.24f, 0.30f, 0.29f), true);
            CombatSceneAuthoring.EnsureViews(demo);
            if (needsMigration && demo.gameCamera != null)
            {
                demo.gameCamera.orthographic = true;
                demo.gameCamera.orthographicSize = 28;
                demo.gameCamera.farClipPlane = 900;
                demo.gameCamera.clearFlags = CameraClearFlags.SolidColor;
                demo.gameCamera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
                demo.gameCamera.transform.SetPositionAndRotation(new Vector3(0, 45, -28), Quaternion.Euler(58, 0, 0));
            }
            if (needsMigration)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.shadows = LightShadows.None;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("GAME_SCENE_PREPARED");
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

        [MenuItem("Tools/Roguelike/构建/Windows 游戏")]
        public static void Build()
        {
            BenchmarkTools.ValidateFoundation();
            CombatValidation.Validate();
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new InvalidOperationException("Game scene missing.");
            EditorSceneManager.OpenScene(ScenePath);
            CombatSceneAuthoring.Validate(UnityEngine.Object.FindFirstObjectByType<GameCombatController>());
            Directory.CreateDirectory("Builds/Game");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/Game/Game.exe", options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Game build failed.");
            Debug.Log("GAME_BUILD_SUCCEEDED");
        }

        public static void PrepareAndBuild()
        {
            PrepareScene();
            Build();
        }
    }
}
