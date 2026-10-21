using System;
using System.Collections;
using System.IO;
using System.Linq;
using Roguelike.Foundation.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    // Opt-in Player integration check. In normal play this component disables itself.
    public sealed class GameCombatProbe : MonoBehaviour
    {
        private bool failed;
        private string output;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--game-smoke") < 0) { enabled = false; yield break; }
            output = Path.Combine(Application.persistentDataPath, "GameSmoke");
            int pathIndex = Array.IndexOf(args, "--game-output");
            if (pathIndex >= 0 && pathIndex + 1 < args.Length) output = args[pathIndex + 1];
            Directory.CreateDirectory(output);
            var demo = GetComponent<GameCombatController>();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!demo.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(demo.IsReady, "Game initializes");
            if (failed) { Application.Quit(1); yield break; }
            yield return null;
            int expectedEnemies = Mathf.Clamp(demo.enemyCount, 1, 30000);
            Check(demo.State.AliveEnemies == expectedEnemies, "Configured enemies spawned");
            var authoredHud = UnityEngine.Object.FindFirstObjectByType<CombatHud>();
            var authoredCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            int authoredNodeCount = demo.GetComponentsInChildren<Transform>(true).Length;
            Check(authoredHud != null && authoredHud.HasBindings, "Authored HUD references are valid");
            var before = demo.State.Position;
            Vector3 target = demo.gameCamera.WorldToScreenPoint(new Vector3(8, 0, 8));
            Check(demo.TryMoveFromScreen(target), "Mouse-to-ground movement accepted");
            var destination = demo.State.Destination;
            Check(!demo.TryMoveFromScreen(new Vector2(Screen.width * 0.5f, 5)), "Bottom HUD blocks ground input");
            Check(math.distancesq(destination, demo.State.Destination) < 0.01f, "UI preserves destination");
            demo.SetDestination(new Vector3(Mathf.Min(demo.safeSpawnRadius + 12, demo.arenaHalfSize - 5), 0, 0));
            yield return new WaitForSeconds(3);
            var fighting = demo.State;
            Check(math.distance(before, fighting.Position) > 5, "Player moved while fighting");
            Check(fighting.ShotsFired > 0 && fighting.Hits > 0 && fighting.Kills > 0, "Shots, hits and kills occur");
            Check(fighting.AliveEnemies + fighting.Kills == expectedEnemies, "Killed enemies are not replenished");
            yield return Capture("combat.png");

            var indicator = UnityEngine.Object.FindFirstObjectByType<GameAbilityIndicator>();
            Check(indicator != null && indicator.HasBindings, "Game ability indicator is bound");
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var abilities = manager.CreateEntityQuery(typeof(CombatAbilitySettings), typeof(CombatAbilityState));
            Entity skillSession = abilities.GetSingletonEntity();
            var tuning = manager.GetComponentData<CombatAbilitySettings>(skillSession);
            Check(manager.GetComponentData<CombatAbilityState>(skillSession).QStacks == tuning.QRequiredHits,
                "Basic hits charged Q");
            ClickSkill(authoredHud, CombatAbilityId.Q);
            yield return null;
            Check(manager.GetComponentData<CombatAbilityState>(skillSession).QBuffRemaining > 0,
                "Q HUD button starts attack speed buff");

            ClickSkill(authoredHud, CombatAbilityId.W);
            Check(indicator.IsAiming && indicator.ActiveAbility == CombatAbilityId.W,
                "W HUD button opens the fan indicator");
            Vector3 aim = (Vector3)demo.State.Position + Vector3.right * 10;
            indicator.UpdateAim((Vector3)demo.State.Position, aim, true, tuning, demo.arenaHalfSize);
            Check(indicator.CanConfirm && indicator.GetComponentInChildren<LineRenderer>(true).enabled,
                "W fan preview can confirm");
            Check(demo.TryConfirmAbilityAt(aim), "W indicator confirms a cast");
            yield return null;
            Check(manager.GetComponentData<CombatAbilityState>(skillSession).WCooldownRemaining > 0,
                "W command reaches combat simulation");

            ClickSkill(authoredHud, CombatAbilityId.E);
            Check(indicator.IsAiming && indicator.ActiveAbility == CombatAbilityId.E,
                "E HUD button opens the target circle");
            Vector3 eTarget = new Vector3(20, 0, 15);
            indicator.UpdateAim((Vector3)demo.State.Position, eTarget, true, tuning, demo.arenaHalfSize);
            Check(indicator.CanConfirm && indicator.GetComponentsInChildren<Roguelike.SkillIndicators.CircleIndicatorView>(true)
                .Count(circle => circle.gameObject.activeSelf) == 2, "E range and target circles are visible");
            yield return Capture("e-aim.png", () => indicator.UpdateAim((Vector3)demo.State.Position,
                eTarget, true, tuning, demo.arenaHalfSize));
            int oldCharges = manager.GetComponentData<CombatAbilityState>(skillSession).ECharges;
            Check(demo.TryConfirmAbilityAt(eTarget), "E indicator confirms a target point");
            yield return null;
            Check(manager.GetComponentData<CombatAbilityState>(skillSession).ECharges == oldCharges - 1,
                "E command spends one charge");

            ClickSkill(authoredHud, CombatAbilityId.R);
            Check(indicator.IsAiming && indicator.ActiveAbility == CombatAbilityId.R,
                "R HUD button opens the line indicator");
            aim = (Vector3)demo.State.Position + Vector3.right * 10;
            indicator.UpdateAim((Vector3)demo.State.Position, aim, true, tuning, demo.arenaHalfSize);
            Check(indicator.CanConfirm && indicator.GetComponentInChildren<LineRenderer>(true).enabled,
                "R line preview can confirm");
            Check(demo.TryConfirmAbilityAt(aim), "R indicator confirms a cast");
            yield return null;
            Check(manager.GetComponentData<CombatAbilityState>(skillSession).RCooldownRemaining > 0,
                "R command reaches combat simulation");
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
            var restartButton = authoredHud.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .FirstOrDefault(button => button.name == "Restart");
            Check(restartButton != null, "Restart button is present");
            if (restartButton == null) { Application.Quit(1); yield break; }
            restartButton.onClick.Invoke();
            Check(demo.GetComponentsInChildren<Transform>(true).Length == authoredNodeCount,
                "Restart preserves authored hierarchy without duplicate nodes");
            Check(demo.State.Health == health && demo.State.AliveEnemies == expectedEnemies && demo.State.Kills == 0, "Restart restores the run");
            var world = World.DefaultGameObjectInjectionWorld;
            using (var enemies = world.EntityManager.CreateEntityQuery(typeof(CombatEnemy)))
                Check(enemies.CalculateEntityCount() == expectedEnemies, "Restart leaves exactly one enemy population");
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
                passed = !failed, enemies = expectedEnemies, shots = fighting.ShotsFired, hits = fighting.Hits,
                kills = fighting.Kills, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                unity = Application.unityVersion, resolution = Screen.width + "x" + Screen.height,
                notes = "Functional integration check only; not a 60 FPS performance certification."
            }, true));
            Debug.Log(failed ? "GAME_SMOKE_FAILED" : "GAME_SMOKE_PASSED");
            Application.Quit(failed ? 1 : 0);
        }

        private void ClickSkill(CombatHud hud, CombatAbilityId ability)
        {
            var action = hud.GetComponentsInChildren<UIAction>(true)
                .FirstOrDefault(item => item.command == "combat.ability" && item.value == (int)ability);
            Check(action != null, ability + " button is bound");
            if (action != null) action.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        private IEnumerator Capture(string name, Action beforeRender = null)
        {
            yield return null;
            beforeRender?.Invoke();
            var demo = GetComponent<GameCombatController>();
            var camera = demo.gameCamera;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
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
            Debug.Log((condition ? "GAME_PASS " : "GAME_FAIL ") + description);
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
