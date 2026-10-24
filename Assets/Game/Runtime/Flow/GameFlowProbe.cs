using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roguelike.Flow
{
    /// <summary>
    /// 仅 --select-smoke 启用的开局流程验证：主菜单 → 选英雄 → 选战场 → 进入战场 → 两次点击返回选择 → 换英雄与战场再进入 → 返回主菜单。
    /// 只点击已制作的按钮并读取公开状态，不改任何战斗数据；结果写入 --select-output 目录（默认 persistentDataPath/SelectFlow）。
    /// 正常游戏不创建此对象。
    /// </summary>
    public sealed class GameFlowProbe : MonoBehaviour
    {
        private bool failed;
        private int passed;
        private readonly List<string> failures=new List<string>();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--select-smoke")<0) return;
            var go=new GameObject("开局流程验证（命令行）");
            DontDestroyOnLoad(go); go.AddComponent<GameFlowProbe>();
        }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--select-output");
            output=index>=0 && index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"SelectFlow");
            Directory.CreateDirectory(output);
            yield return Run();
            Finish();
        }
        private IEnumerator Run()
        {
            if(SceneManager.GetActiveScene().name!="Main") SceneManager.LoadScene(ResourcePaths.Resolve("Scene.Main","scene"));
            yield return WaitScene("Main");
            Check(SceneManager.GetActiveScene().name=="Main","Flow starts at the main menu");
            yield return Click("main.start",0);
            SelectSceneController select=null;
            yield return WaitFor(()=>(select=FindFirstObjectByType<SelectSceneController>()) && select.IsReady,"Main start opens the ready selection scene");
            if(!select || !select.IsReady) yield break;
            var catalog=select.catalog;
            bool enough=catalog.heroes.Length>=2 && catalog.maps.Length>=2;
            Check(enough,"Catalog offers at least two heroes and two battlefields");
            if(!enough) yield break;
            HeroDefinition archer=catalog.heroes[0], second=catalog.heroes[1];
            GameMapDefinition firstMap=catalog.maps[0], secondMap=catalog.maps[1];
            Check(select.HeroIndex==0 && select.MapIndex==0 && !select.ChoosingMap,"Fresh selection defaults to the first hero and battlefield");
            Check(Toggle("select.step.hero") && !Toggle("select.step.map"),"Hero step is shown first");
            Check(Enumerable.Range(0,SelectSceneController.HeroSlots).Count(i=>Toggle($"select.hero.{i}.visible"))==catalog.heroes.Length,"Hero cards match the catalog");
            Check(Text("select.hero.name")==Label(archer) && Text("select.hero.role")==archer.role,"Hero details show the first hero");
            Check(Text("select.skill.0.name")==SkillName(archer.skills[0]),"Hero details list the first skill");
            yield return Click(SelectSceneController.HeroCommand,1);
            Check(select.HeroIndex==1 && Text("select.hero.name")==Label(second) && Toggle("select.hero.1.selected") && !Toggle("select.hero.0.selected"),"Hero card selects the second hero and updates details");
            var showcase=select.showcase;
            Check(showcase && showcase.Shown==second && (second.visualPrefab?(bool)showcase.Visual:!showcase.Visual),"Showcase displays the selected hero model");
            yield return Capture(Camera.main,"select-hero.png");
            yield return Click(SelectSceneController.NextCommand,0);
            Check(select.ChoosingMap && Toggle("select.step.map") && !Toggle("select.step.hero") && showcase && !showcase.turntable.gameObject.activeInHierarchy,"Next opens the battlefield step and hides the showcase");
            yield return Click(SelectSceneController.MapCommand,1);
            int secondKinds=Mathf.Min(SelectSceneController.Kinds(secondMap.enemyRoster).Count,SelectSceneController.EnemySlots);
            Check(select.MapIndex==1 && Toggle("select.map.1.selected") && !Toggle("select.map.0.selected"),"Battlefield card selects the second battlefield");
            Check(Text("select.map.1.name")==secondMap.Label && Enumerable.Range(0,SelectSceneController.EnemySlots).Count(m=>Toggle($"select.map.1.enemy.{m}.visible"))==secondKinds,"Battlefield card lists its own monsters");
            Check(Text("select.map.0.enemy.0.name")!=Text("select.map.1.enemy.0.name"),"Battlefields show different monsters");
            yield return Capture(Camera.main,"select-map.png");
            yield return Click(SelectSceneController.BackCommand,0);
            Check(!select.ChoosingMap && select.HeroIndex==1 && select.MapIndex==1,"Back returns to the hero step and keeps both choices");
            yield return Click(SelectSceneController.NextCommand,0);
            yield return Click(SelectSceneController.StartCommand,0);
            Check(GameSelection.Hero==second && GameSelection.Map==secondMap,"Start records the selection");
            yield return Battle(second,secondMap,"second battlefield",false);

            // 返回选择后预选上一局；换第一位英雄与第一个战场再进入。
            yield return WaitFor(()=>(select=FindFirstObjectByType<SelectSceneController>()) && select.IsReady,"Leaving the battlefield returns to the selection scene");
            if(!select || !select.IsReady) yield break;
            Check(select.HeroIndex==1 && select.MapIndex==1 && !select.ChoosingMap,"Returning preselects the previous hero and battlefield");
            yield return Click(SelectSceneController.HeroCommand,0);
            yield return Click(SelectSceneController.NextCommand,0);
            yield return Click(SelectSceneController.MapCommand,0);
            yield return Click(SelectSceneController.StartCommand,0);
            yield return Battle(archer,firstMap,"first battlefield",true);
            yield return WaitFor(()=>(select=FindFirstObjectByType<SelectSceneController>()) && select.IsReady,"Second leave returns to the selection scene");
            if(!select || !select.IsReady) yield break;
            yield return Click(SelectSceneController.MenuCommand,0);
            yield return WaitScene("Main");
            Check(SceneManager.GetActiveScene().name=="Main","Selection returns to the main menu");
        }
        /// <summary>检查战场使用所选英雄与战场名册，然后通过“返回选择”（两次点击确认）回到选择场景。</summary>
        private IEnumerator Battle(HeroDefinition hero,GameMapDefinition map,string label,bool checkTimeout)
        {
            string scene=ResourcePaths.Resolve(map.sceneId,"scene");
            yield return WaitScene(scene);
            Check(SceneManager.GetActiveScene().name==scene,label+": start loads the battlefield scene "+scene);
            GameCombatController controller=null;
            yield return WaitFor(()=>(controller=FindFirstObjectByType<GameCombatController>()) && controller.IsReady && controller.State.AliveEnemies>0,label+": battle starts and spawns enemies",30);
            if(!controller || !controller.IsReady) yield break;
            Check(controller.hero==hero,label+": battle uses the selected hero");
            Check(controller.map==map && controller.ActiveRoster==map.enemyRoster && controller.ActiveDifficulty==map.difficulty,label+": battle uses the battlefield roster and difficulty");
            var kinds=SelectSceneController.Kinds(map.enemyRoster);
            Check(new HashSet<string>(controller.EnemyKinds).SetEquals(kinds.Select(x=>x.archetype.Label)),label+": enemy kinds come from the battlefield roster");
            var ids=new HashSet<int>(kinds.Select(x=>x.archetype.configId));
            var manager=World.DefaultGameObjectInjectionWorld.EntityManager;
            using(var query=manager.CreateEntityQuery(typeof(CombatEnemy)))
            using(var enemies=query.ToComponentDataArray<CombatEnemy>(Allocator.Temp))
            {
                bool all=enemies.Length>0;
                foreach(var enemy in enemies) all&=ids.Contains(enemy.ConfigId);
                Check(all,label+": every spawned enemy belongs to the battlefield roster");
            }
            yield return WaitFor(()=>(Text("top.map")??"").Contains(map.Label),label+": HUD shows the battlefield name",5);
            yield return Capture(controller.gameCamera,map.stableId.Replace('.','-')+".png");
            // 仅验证编译快照驱动的范围表现；不施法、不绕过学习门槛修改战斗状态。
            var indicator=FindFirstObjectByType<GameAbilityIndicator>();
            Check(indicator && indicator.HasBindings,label+": migrated indicator views are bound");
            if(indicator && indicator.HasBindings)
            {
                using(var library=controller.skillCatalog.Compile())
                {
                    int index=hero.ResolveSkills(controller.skillCatalog)[3];
                    indicator.BeginAim(3,SkillAimPreview.From(ref library.Value.Skills[index]));
                }
                Check(indicator.IsAiming,label+": compiled point preview opens");
                yield return Capture(controller.gameCamera,map.stableId.Replace('.','-')+"-indicator.png",()=>
                {
                    Vector3 origin=(Vector3)controller.State.Position;
                    indicator.UpdateAim(origin,origin+Vector3.forward*8,true,controller.arenaHalfSize);
                    Check(indicator.CanConfirm,label+": compiled point preview has a valid target");
                });
                indicator.Cancel();
            }
            yield return Click("game.leave",0);
            Check(SceneManager.GetActiveScene().name==scene && Toggle("game.leave.confirm"),label+": first leave click only asks for confirmation");
            if(checkTimeout)
            {
                yield return new WaitForSecondsRealtime(3.4f);
                Check(SceneManager.GetActiveScene().name==scene && !Toggle("game.leave.confirm"),label+": leave confirmation expires");
                yield return Click("game.leave",0);
            }
            yield return Click("game.leave",0);
            yield return WaitScene("SelectScene");
            Check(SceneManager.GetActiveScene().name=="SelectScene",label+": confirmed leave returns to the selection scene");
        }

        private static string Label(HeroDefinition hero)=>string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName;
        private static string SkillName(SkillDefinition skill)=>!skill?"":string.IsNullOrWhiteSpace(skill.displayName)?skill.name:skill.displayName;
        private static UIPanel[] Panels()=>FindObjectsByType<UIPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        /// <summary>读取当前场景面板里绑定到 key 的第一个文本。</summary>
        private static string Text(string key)
        {
            foreach(var panel in Panels()) foreach(var binding in panel.texts) if(binding.key==key && binding.target) return binding.target.text;
            return null;
        }
        /// <summary>绑定到 key 的第一个正向显隐对象当前是否显示。</summary>
        private static bool Toggle(string key)
        {
            foreach(var panel in Panels()) foreach(var binding in panel.toggles) if(binding.key==key && !binding.invert && binding.target) return binding.target.activeInHierarchy;
            return false;
        }
        private IEnumerator Click(string command,int value)
        {
            float until=Time.realtimeSinceStartup+5; UIAction found=null;
            while(true)
            {
                foreach(var action in FindObjectsByType<UIAction>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if(action.command==command && action.value==value && action.isActiveAndEnabled) { found=action; break; }
                if(found || Time.realtimeSinceStartup>until) break;
                yield return null;
            }
            Check(found,$"Visible authored button {command} {value}");
            if(found) found.GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
        }
        private IEnumerator WaitScene(string scene,float seconds=30)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(SceneManager.GetActiveScene().name!=scene && Time.realtimeSinceStartup<until) yield return null;
            yield return null; yield return null;
        }
        private IEnumerator WaitFor(Func<bool> condition,string label,float seconds=20)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),label);
        }
        /// <summary>离屏渲染相机与全部根 Canvas 到 1920×1080 图片，不依赖窗口是否可见。</summary>
        private IEnumerator Capture(Camera camera,string name,Action beforeRender=null)
        {
            yield return null;
            beforeRender?.Invoke();
            if(!camera) { Check(false,"Camera available for capture: "+name); yield break; }
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
            var states=canvases.Select(c=>(canvas:c,mode:c.renderMode,camera:c.worldCamera,distance:c.planeDistance)).ToArray();
            var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active;
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                target.Create(); camera.targetTexture=target;
                foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
                Color first=texture.GetPixel(0,0); bool varied=false;
                for(int y=30;y<1080;y+=97) for(int x=20;x<1920;x+=113) if(((Vector4)(texture.GetPixel(x,y)-first)).sqrMagnitude>0.01f) varied=true;
                Check(varied,"Offscreen render contains scene/UI pixels: "+name);
                File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());
            }
            catch(Exception exception) { failed=true; failures.Add("capture "+name+": "+exception.Message); Debug.LogException(exception); }
            finally
            {
                RenderTexture.active=oldActive; camera.targetTexture=oldTarget;
                foreach(var state in states) if(state.canvas) { state.canvas.renderMode=state.mode; state.canvas.worldCamera=state.camera; state.canvas.planeDistance=state.distance; }
                target.Release(); Destroy(target); Destroy(texture);
            }
        }
        private void Check(bool condition,string label)
        {
            if(condition) { passed++; Debug.Log("SELECT_CHECK_PASSED: "+label); }
            else { failed=true; failures.Add(label); Debug.LogError("SELECT_CHECK_FAILED: "+label); }
        }
        private void Finish()
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Report {passed=!failed,checks=passed+failures.Count,failures=failures.ToArray()},true));
            Debug.Log(failed?"SELECT_SMOKE_FAILED":"SELECT_SMOKE_PASSED checks="+passed);
            Application.Quit(failed?1:0);
        }
        [Serializable] private class Report { public bool passed; public int checks; public string[] failures; }
    }
}
