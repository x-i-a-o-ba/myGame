using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>仅 --scene-ui-smoke 启用的跨场景验证，检查按需加载、重复请求、状态回放和事件隔离。</summary>
    public sealed class SceneFrameworkProbe : MonoBehaviour
    {
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SelectStartupScene()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--start-scene");
            if(index>=0 && index+1<args.Length) SceneManager.LoadScene(ResourcePaths.Resolve("Scene."+args[index+1],"scene"));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("--scene-ui-smoke")) return;
            var go = new GameObject("Scene framework verification probe");
            DontDestroyOnLoad(go); go.AddComponent<SceneFrameworkProbe>();
        }
        private IEnumerator Start()
        {
            var names = new[] { "Main", "SelectScene", "Game", "SkillIndicatorDemo", "FogOfWarDemo", "SkillArchitectureDemo", "Benchmark10k", "Main" };
            UIManager previous = null;
            foreach (string scene in names)
            {
                yield return SceneManager.LoadSceneAsync(ResourcePaths.Resolve("Scene." + scene, "scene"));
                yield return null; yield return null;
                var manager = FindFirstObjectByType<UIManager>();
                Check(manager && FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(x=>x.isRootCanvas)==1, scene+": single Canvas/manager");
                Check(!previous, scene+": previous UI scope disposed"); previous = manager;
                if (!manager) continue;
                int before=manager.PanelCount;
                manager.Show("UI.DeveloperHelp"); manager.Show("UI.DeveloperHelp"); manager.Hide("UI.DeveloperHelp");
                float deadline=Time.realtimeSinceStartup+10;
                while (!manager.FindPanel("UI.DeveloperHelp") && Time.realtimeSinceStartup<deadline) yield return null;
                Check(manager.PanelCount==before+1 && !manager.IsVisible("UI.DeveloperHelp"),scene+": async coalescing and hide-before-complete");
                manager.Show("UI.DeveloperHelp");
                var help=manager.FindPanel("UI.DeveloperHelp");
                Check(help && manager.IsVisible(help.panelId),scene+": cached reopen");
                if(help) help.GetComponentInChildren<Button>().onClick.Invoke();
                Check(!manager.IsVisible("UI.DeveloperHelp"),scene+": button event closes panel");
                foreach(var panel in manager.GetComponentsInChildren<UIPanel>(true))
                {
                    if(panel.texts.Length==0) continue;
                    var binding=panel.texts[0]; bool wasVisible=panel.gameObject.activeSelf;
                    manager.Hide(panel.panelId); manager.Text(binding.key,"事件回放验证");manager.Show(panel.panelId);
                    Check(binding.target.text=="事件回放验证",scene+": hidden panel state replay");
                    if(!wasVisible) manager.Hide(panel.panelId);
                    break;
                }
                if(scene=="Main")
                {
                    yield return new WaitForSeconds(.2f);
                    Check(SceneManager.GetActiveScene().name=="Main","Main does not auto-navigate");
                    var start=manager.GetComponentsInChildren<UIAction>(true).First(x=>x.command=="main.start");
                    start.GetComponent<Button>().onClick.Invoke();
                    yield return null; yield return null;
                    Check(SceneManager.GetActiveScene().name=="SelectScene","Main button navigates through event adapter");
                    previous=FindFirstObjectByType<UIManager>();
                }
                Debug.Log("SCENE_UI_VISITED "+scene);
            }
            string output=Path.Combine(Application.dataPath,"../scene-ui-result.json");
            File.WriteAllText(output,"{\"passed\":"+(!failed?"true":"false")+",\"sceneVisits\":8}");
            Debug.Log(failed?"SCENE_UI_SMOKE_FAILED":"SCENE_UI_SMOKE_PASSED");
            Application.Quit(failed?1:0);
        }
        private void Check(bool passed,string label)
        { if(!passed) failed=true; Debug.Log((passed?"SCENE_UI_PASS ":"SCENE_UI_FAIL ")+label); }
    }
}
