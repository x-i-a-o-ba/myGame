using System;
using System.Collections;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Foundation.UI;
using Unity.Collections;
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
            yield return null; yield return null;
            Check(controller.Wave.Wave==1 && controller.State.AliveEnemies>0 && controller.State.AliveEnemies<=controller.enemyCount,"First difficulty wave spawns within alive cap");
            Check(controller.Progression.Level==1 && controller.Progression.SkillPoints==1,"Hero starts at level 1 with one skill point");
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[0].Level==0,"Skills start unlearned");
            Click(1);
            Check(!indicator.IsAiming,"Unlearned skill cannot enter aim");
            // 直接写入权威累计经验，模拟一段击杀进度；升级、技能点与属性成长由模拟系统结算。
            var progression=manager.GetComponentData<HeroProgression>(controller.SessionEntity);
            float maxHealthBefore=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).MaxHealth;
            progression.Experience=progression.Library.Value.CumulativeExperience[6];
            manager.SetComponentData(controller.SessionEntity,progression);
            yield return null; yield return null;
            Check(controller.Progression.Level==7 && controller.Progression.SkillPoints==7,"Experience table levels hero to 7 and grants skill points");
            Check(manager.GetComponentData<AbilityUnit>(controller.SessionEntity).MaxHealth>maxHealthBefore,"Level growth raises authoritative max health");
            yield return WaitForAction("combat.learnSkill",3);
            for(int i=0;i<4;i++) ClickLearn(i);
            yield return null; yield return null;
            var learned=manager.GetBuffer<AbilitySlot>(controller.SessionEntity);
            Check(learned[0].Level==1 && learned[1].Level==1 && learned[2].Level==1 && learned[3].Level==1 && controller.Progression.SkillPoints==3,"HUD learn buttons learn every slot once");
            controller.LearnSkill(3); yield return null; yield return null;
            Check(manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[3].Level==1 && controller.Progression.LastFailure==ProgressionFailure.HeroLevelTooLow,"Ultimate level 2 waits for hero level 12");
            Check(!controller.TryMoveFromScreen(new Vector2(Screen.width*0.5f,5)),"HUD blocks movement");
            Check(controller.EnemyKinds.Length>1,"Enemy roster provides several enemy kinds");
            // 攻击移动：走向远处并自动攻击途中索敌距离内的敌人（Dota 2 的 A + 左键地面）。
            controller.IssueAttackMove(new Vector3(controller.safeSpawnRadius+12,0,0));
            yield return null;
            Check(controller.Order.Kind==HeroOrderKind.AttackMove,"Attack-move order is recorded");
            yield return new WaitForSeconds(3);
            Check(controller.State.ShotsFired>0 && controller.State.Hits>0,"Auto attack module fires through the shared effect pipeline");
            yield return OrderChecks(controller,manager);
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
            progression=manager.GetComponentData<HeroProgression>(controller.SessionEntity);
            progression.Experience=progression.Library.Value.CumulativeExperience[9];
            manager.SetComponentData(controller.SessionEntity,progression);
            yield return null; yield return null;
            Check(controller.Progression.Level==10 && HeroProgressionLogic.PendingTalent(controller.Progression)==0,"Level 10 unlocks first talent tier");
            float attackBefore=manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack;
            yield return WaitForAction("combat.talent",0);
            ClickTalent(0); yield return null; yield return null;
            Check(HeroProgressionLogic.PendingTalent(controller.Progression)<0 && manager.GetComponentData<AbilityUnit>(controller.SessionEntity).BaseAttack>attackBefore,"Talent button applies left option");
            // 把下一波时间提前到当前，验证计时触发与逐波数值，而不是让探针等待完整波次间隔。
            var waveState=manager.GetComponentData<CombatWaveState>(controller.SessionEntity);
            int spawnedBefore=waveState.SpawnedTotal; waveState.NextWaveAt=0;
            manager.SetComponentData(controller.SessionEntity,waveState);
            yield return null; yield return null;
            Check(controller.Wave.Wave==2 && controller.Wave.SpawnedTotal>spawnedBefore,"Next difficulty wave spawns when its timer elapses");
            Check(DistinctEnemyKinds(manager)>1,"Waves mix several enemy kinds from the roster");
            // 换序的英雄证明输入槽不是技能身份；克隆仅供探针，不修改项目资源。
            var alternate=Instantiate(originalHero);
            alternate.stableId="probe.reordered"; alternate.skills=(SkillDefinition[])originalHero.skills.Clone();
            (alternate.skills[0],alternate.skills[2])=(alternate.skills[2],alternate.skills[0]);
            controller.SelectHero(alternate); yield return null;
            controller.LearnSkill(0); yield return null; yield return null;
            Click(0);
            Check(indicator.IsAiming && controller.SkillAt(0).aim==AimMode.Point,"Different hero loadout changes Q aim without simulation branches");
            indicator.Cancel(); controller.SelectHero(originalHero); Destroy(alternate); yield return null;
            Check(controller.State.Kills==0 && manager.GetBuffer<SkillFlight>(controller.SessionEntity).Length==0,"Restart clears combat and flights");
            Check(controller.Progression.Level==1 && manager.GetBuffer<AbilitySlot>(controller.SessionEntity)[0].Level==0,"Restart resets level and learned skills");
            var unit=manager.GetComponentData<AbilityUnit>(controller.SessionEntity); unit.Health=0; manager.SetComponentData(controller.SessionEntity,unit);
            yield return null; yield return null;
            Check(controller.State.Health==0,"Death snapshot comes from authoritative unit");
            Check(controller.Wave.Outcome==CombatOutcome.Defeat,"Hero death records defeat outcome");
            controller.RestartRun(); yield return null;
            Check(controller.State.Health==originalHero.health,"Restart restores configured hero");
            yield return Capture("foundation-game.png");
            Finish(controller);
        }
        /// <summary>Dota 2 式指令：右键敌人攻击、S 停止、H 待命、HUD 指令按钮与鼠标点选敌人。</summary>
        private IEnumerator OrderChecks(GameCombatController controller,EntityManager manager)
        {
            Entity target=NearestEnemy(controller,manager);
            Check(target!=Entity.Null,"Enemies remain near the hero after attack-move");
            if(target==Entity.Null) yield break;
            var screen=controller.gameCamera.WorldToScreenPoint(manager.GetComponentData<Unity.Transforms.LocalTransform>(target).Position);
            Check(controller.PickEnemy(screen)!=Entity.Null,"Mouse ray picks the enemy under the cursor");
            float healthBefore=manager.GetComponentData<AbilityUnit>(target).Health;
            controller.IssueAttack(target); yield return null;
            Check((controller.Order.Kind==HeroOrderKind.AttackTarget && controller.Order.Target==target) || !manager.Exists(target),"Right-clicking an enemy issues an attack order");
            float until=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<until && manager.Exists(target) && manager.GetComponentData<AbilityUnit>(target).Health>=healthBefore) yield return null;
            Check(!manager.Exists(target) || manager.GetComponentData<AbilityUnit>(target).Health<healthBefore,"Attack order damages the chosen target");
            controller.Hold(); yield return null; yield return null;
            var held=controller.State.Position;
            yield return new WaitForSeconds(0.5f);
            Check(controller.Order.Kind==HeroOrderKind.Hold && math.distance(held.xz,controller.State.Position.xz)<0.01f,"Hold position keeps the hero in place");
            yield return WaitForAction("combat.order",1);
            ClickAction("combat.order",1); yield return null;
            Check(controller.Order.Kind==HeroOrderKind.Idle,"HUD stop button issues stop");
            ClickAction("combat.order",0); yield return null;
            Check(controller.AttackArmed,"HUD attack button arms attack targeting");
            ClickAction("combat.order",0); yield return null;
            Check(!controller.AttackArmed,"Second attack button press cancels attack targeting");
        }
        private static Entity NearestEnemy(GameCombatController controller,EntityManager manager)
        {
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(Unity.Transforms.LocalTransform)))
            using(var entities=query.ToEntityArray(Allocator.Temp))
            using(var transforms=query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp))
            {
                Entity best=Entity.Null; float bestDistance=float.MaxValue; float2 hero=controller.State.Position.xz;
                for(int i=0;i<entities.Length;i++)
                {
                    float distance=math.distancesq(transforms[i].Position.xz,hero);
                    if(distance<bestDistance) { bestDistance=distance; best=entities[i]; }
                }
                return best;
            }
        }
        private static int DistinctEnemyKinds(EntityManager manager)
        {
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy)))
            using(var enemies=query.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
            {
                var ids=new System.Collections.Generic.HashSet<int>();
                foreach(var enemy in enemies) ids.Add(enemy.ConfigId);
                return ids.Count;
            }
        }
        /// <summary>面板由 UIManager 异步加载，并在 HUD 下一次刷新时显示；按钮可见前等待，最多 5 秒。</summary>
        private IEnumerator WaitForAction(string command,int value)
        {
            float until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until)
            {
                foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if(action.command==command && action.value==value) yield break;
                yield return null;
            }
        }
        private void ClickLearn(int slot)=>ClickAction("combat.learnSkill",slot);
        private void ClickTalent(int option)=>ClickAction("combat.talent",option);
        private void ClickAction(string command,int value)
        {
            foreach(var action in UnityEngine.Object.FindObjectsByType<UIAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(action.command==command && action.value==value)
                { action.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return; }
            Check(false,"Missing visible authored button "+command+" "+value);
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
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Report{passed=!failed,enemies=controller.IsReady?controller.State.AliveEnemies:0,
                wave=controller.IsReady?controller.Wave.Wave:0,level=controller.IsReady?controller.Progression.Level:0},true));
            Debug.Log(failed?"GAME_SMOKE_FAILED":"GAME_SMOKE_PASSED"); Application.Quit(failed?1:0);
        }
        [Serializable] private class Report { public bool passed; public int enemies,wave,level; }
    }
}


