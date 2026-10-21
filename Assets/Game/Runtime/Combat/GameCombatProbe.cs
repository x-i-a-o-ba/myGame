using System;
using System.Collections;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Foundation.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>显式命令行启用的Player集成检查；正常游戏不执行。</summary>
    public sealed class GameCombatProbe : MonoBehaviour
    {
        private bool failed;
        private string output;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--game-smoke")<0) { enabled=false; yield break; }
            output=Path.Combine(Application.persistentDataPath,"GameFoundation");
            int index=Array.IndexOf(args,"--game-output"); if(index>=0 && index+1<args.Length) output=args[index+1];
            Directory.CreateDirectory(output);
            var controller=GetComponent<GameCombatController>();
            float deadline=Time.realtimeSinceStartup+20;
            while(!controller.IsReady && Time.realtimeSinceStartup<deadline) yield return null;
            Check(controller.IsReady,"Game initializes composed hero");
            if(failed) { Finish(controller); yield break; }
            var manager=World.DefaultGameObjectInjectionWorld.EntityManager;
            var originalHero=controller.hero;
            var indicator=UnityEngine.Object.FindFirstObjectByType<GameAbilityIndicator>();
            Check(indicator && indicator.HasBindings,"Authored indicator references");
            Check(controller.State.AliveEnemies==controller.enemyCount,"Configured enemies spawned");
            Check(!controller.TryMoveFromScreen(new Vector2(Screen.width*0.5f,5)),"HUD blocks movement");
            controller.SetDestination(new Vector3(controller.safeSpawnRadius+12,0,0));
            yield return new WaitForSeconds(3);
            Check(controller.State.ShotsFired>0 && controller.State.Hits>0,"Basic attack uses shared effect pipeline");
            var slots=manager.GetBuffer<AbilitySlot>(controller.SessionEntity);
            var q=slots[0]; q.HitStacks=originalHero.skills[0].requiredHits; slots[0]=q;
            Click(0);
            yield return null; yield return null;
            Check(manager.GetComponentData<AbilityUnit>(controller.SessionEntity).AttackSpeedMultiplier>1,"Self buff modifies attack speed");
            Click(1);
            Check(indicator.IsAiming && indicator.ActiveSlot==1,"HUD slot enters configured aim");
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.right*12),"Confirm cone ability");
            yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[1].ReadyAt>0,"Successful cast commits cooldown");
            Click(2);
            indicator.UpdateAim((Vector3)controller.State.Position,(Vector3)controller.State.Position+Vector3.forward*10,true,controller.arenaHalfSize);
            yield return Capture("point-indicator.png",()=>indicator.UpdateAim((Vector3)controller.State.Position,(Vector3)controller.State.Position+Vector3.forward*10,true,controller.arenaHalfSize));
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.forward*10),"Point ability accepts target");
            yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[2].Charges==1,"Point projectile consumes one charge");
            Click(3);
            Check(controller.TryConfirmAbilityAt((Vector3)controller.State.Position+Vector3.right*50),"Linear projectile accepts direction");
            yield return new WaitForSeconds(0.2f);
            Check(manager.GetComponentData<AbilityRuntime>(controller.SessionEntity).CastSerial>=4,"All configured examples commit through one executor");
            // 换序的英雄证明输入槽不是技能身份；克隆仅供探针，不修改项目资源。
            var alternate=Instantiate(originalHero);
            alternate.stableId="probe.reordered"; alternate.skills=(SkillDefinition[])originalHero.skills.Clone();
            (alternate.skills[0],alternate.skills[2])=(alternate.skills[2],alternate.skills[0]);
            controller.SelectHero(alternate); yield return null;
            Click(0);
            Check(indicator.IsAiming && controller.SkillAt(0).aim==AimMode.Point,"Different hero loadout changes Q aim without simulation branches");
            indicator.Cancel(); controller.SelectHero(originalHero); Destroy(alternate); yield return null;
            Check(controller.State.Kills==0 && manager.GetBuffer<SkillFlight>(controller.SessionEntity).Length==0,"Restart clears combat and flights");
            var unit=manager.GetComponentData<AbilityUnit>(controller.SessionEntity); unit.Health=0; manager.SetComponentData(controller.SessionEntity,unit);
            yield return null; yield return null;
            Check(controller.State.Health==0,"Death snapshot comes from authoritative unit");
            controller.RestartRun(); yield return null;
            Check(controller.State.Health==originalHero.health,"Restart restores configured hero");
            yield return Capture("foundation-game.png");
            Finish(controller);
        }
        private void Click(int slot)
        {
            foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(action.command=="combat.ability" && action.value==slot)
                { action.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return; }
            Check(false,"Missing authored skill button "+slot);
        }
        private void Check(bool condition,string message)
        { if(!condition) { failed=true; Debug.LogError("GAME_CHECK_FAILED: "+message); } else Debug.Log("GAME_CHECK_PASSED: "+message); }
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

        private void Finish(GameCombatController controller)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Report{passed=!failed,enemies=controller.IsReady?controller.State.AliveEnemies:0},true));
            Debug.Log(failed?"GAME_SMOKE_FAILED":"GAME_SMOKE_PASSED"); Application.Quit(failed?1:0);
        }
        [Serializable] private class Report { public bool passed; public int enemies; }
    }
}


