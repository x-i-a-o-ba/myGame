using System;
using System.Collections;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    // Opt-in Player integration check. In normal play this component disables itself.
    public sealed class CombatDemoProbe : MonoBehaviour
    {
        private bool failed;
        private string output;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--demo-smoke") < 0) { enabled = false; yield break; }
            output = Path.Combine(Application.persistentDataPath, "DemoSmoke");
            int pathIndex = Array.IndexOf(args, "--demo-output");
            if (pathIndex >= 0 && pathIndex + 1 < args.Length) output = args[pathIndex + 1];
            Directory.CreateDirectory(output);
            var demo = GetComponent<CombatDemoController>();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!demo.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(demo.IsReady, "Demo initializes");
            if (failed) { Application.Quit(1); yield break; }
            yield return null;
            Check(demo.State.AliveEnemies == 10000, "10,000 enemies spawned");
            var authoredHud = demo.GetComponentInChildren<CombatHud>(true);
            var authoredCanvas = demo.GetComponentInChildren<Canvas>(true);
            int authoredNodeCount = demo.GetComponentsInChildren<Transform>(true).Length;
            Check(authoredHud != null && authoredHud.HasBindings, "Authored HUD references are valid");
            var before = demo.State.Position;
            Vector3 target = demo.gameCamera.WorldToScreenPoint(new Vector3(8, 0, 8));
            Check(demo.TryMoveFromScreen(target), "Mouse-to-ground movement accepted");
            var destination = demo.State.Destination;
            Check(!demo.TryMoveFromScreen(new Vector2(Screen.width * 0.5f, 5)), "Bottom HUD blocks ground input");
            Check(math.distancesq(destination, demo.State.Destination) < 0.01f, "UI preserves destination");
            yield return new WaitForSeconds(3);
            var fighting = demo.State;
            Check(math.distance(before, fighting.Position) > 5, "Player moved while fighting");
            Check(fighting.ShotsFired > 0 && fighting.Hits > 0 && fighting.Kills > 0, "Shots, hits and kills occur");
            Check(fighting.AliveEnemies + fighting.Kills == 10000, "Killed enemies are not replenished");
            yield return Capture("combat.png");
            demo.cameraZoom = 155;
            yield return null;
            yield return Capture("overview.png");

            float health = demo.playerHealth;
            float range = demo.enemyAttackRange;
            demo.playerHealth = 1;
            demo.enemyAttackRange = 300;
            demo.RestartRun();
            yield return new WaitForSeconds(0.5f);
            Check(demo.State.Health <= 0, "Enemy contact can kill the player");
            int shotsAtDeath = demo.State.ShotsFired;
            yield return new WaitForSeconds(0.3f);
            Check(demo.State.ShotsFired == shotsAtDeath, "Dead player cannot shoot");
            Check(!demo.TryMoveFromScreen(target), "Dead player cannot move");
            yield return Capture("defeat.png");
            demo.playerHealth = health;
            demo.enemyAttackRange = range;
            demo.cameraZoom = 28;
            // Exercise the authored restart button and HUD event bridge instead of directly calling the controller.
            var restartButton = authoredHud.GetComponentInChildren<UnityEngine.UI.Button>(true);
            restartButton.onClick.Invoke();
            Check(demo.GetComponentsInChildren<Transform>(true).Length == authoredNodeCount,
                "Restart preserves authored hierarchy without duplicate nodes");
            Check(demo.State.Health == health && demo.State.AliveEnemies == 10000 && demo.State.Kills == 0, "Restart restores the run");
            var world = World.DefaultGameObjectInjectionWorld;
            using (var enemies = world.EntityManager.CreateEntityQuery(typeof(CombatEnemy)))
                Check(enemies.CalculateEntityCount() == 10000, "Restart leaves exactly one enemy population");
            Destroy(demo);
            yield return null;
            yield return null;
            using (var owned = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CombatOwned>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            })) Check(owned.IsEmptyIgnoreFilter, "Scene controller destruction releases session and prefabs");
            Check(authoredHud != null && authoredCanvas != null, "Controller disposal preserves scene-owned HUD");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Report
            {
                passed = !failed, enemies = 10000, shots = fighting.ShotsFired, hits = fighting.Hits,
                kills = fighting.Kills, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                unity = Application.unityVersion, resolution = Screen.width + "x" + Screen.height,
                notes = "Functional integration check only; not a 60 FPS performance certification."
            }, true));
            Debug.Log(failed ? "GAME_DEMO_SMOKE_FAILED" : "GAME_DEMO_SMOKE_PASSED");
            Application.Quit(failed ? 1 : 0);
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            var demo = GetComponent<CombatDemoController>();
            var camera = demo.gameCamera;
            var canvas = demo.GetComponentInChildren<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                // Hidden/minimized windows can have a black backbuffer. Request an actual URP
                // render including the same HUD into a texture, independent of window visibility.
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                Color first = texture.GetPixel(0, 0);
                bool varied = false;
                for (int y = 30; y < 1080; y += 97)
                    for (int x = 20; x < 1920; x += 113)
                        if (((Vector4)(texture.GetPixel(x, y) - first)).sqrMagnitude > 0.01f) varied = true;
                Check(varied, "Offscreen render contains scene/UI pixels: " + name);
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlaneDistance;
                target.Release();
                Destroy(target);
                Destroy(texture);
            }
        }

        private void Check(bool condition, string description)
        {
            Debug.Log((condition ? "DEMO_PASS " : "DEMO_FAIL ") + description);
            if (!condition) failed = true;
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public int enemies, shots, hits, kills;
            public string cpu, gpu, unity, resolution, notes;
        }
    }
}
