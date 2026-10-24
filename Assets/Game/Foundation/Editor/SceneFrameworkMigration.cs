using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Manager = Roguelike.Foundation.UI.UIManager;

namespace Roguelike.Foundation.Editor
{
    /// <summary>
    /// 场景框架维护：检查正式场景的统一结构（单根节点、单 Canvas/UIManager/EventSystem、面板为 Prefab 且只绑定内部控件、无 Missing Script），
    /// 更新资源目录，构建跨场景 UI 验证 Player。旧场景的一次性迁移已完成，于 2026-10-24 随演示场景一起移除；类名保留以兼容命令行 -executeMethod。
    /// </summary>
    public static class SceneFrameworkMigration
    {
        /// <summary>纳入统一场景框架检查与资源目录的场景。技能编辑场景（技能配置工作台）只在编辑器使用，不在此列。</summary>
        public static readonly string[] Scenes = {
            "Assets/Scenes/Main.unity", "Assets/Scenes/SelectScene.unity", "Assets/Scenes/Game.unity", "Assets/Scenes/EmberRift.unity",
            "Assets/Game/Scenes/Benchmark10k.unity"
        };

        private static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        [MenuItem("Tools/Roguelike/维护/更新资源目录")]
        public static void WriteManifest()
        {
            var entries = new List<ResourcePaths.Entry>();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/",StringComparison.Ordinal) && x.Contains("/Resources/") && !AssetDatabase.IsValidFolder(x) && !x.EndsWith("ProjectResourceCatalog.json")))
            {
                string relative = path.Substring(path.LastIndexOf("/Resources/",StringComparison.Ordinal)+11);
                relative = relative.Substring(0,relative.Length-Path.GetExtension(relative).Length);
                entries.Add(new ResourcePaths.Entry { id=relative, path=relative, kind="resource" });
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var panel = prefab ? prefab.GetComponent<UIPanel>() : null;
                if (panel) entries.Add(new ResourcePaths.Entry {id=panel.panelId,path=relative,kind="resource"});
            }
            foreach(var scene in Scenes) entries.Add(new ResourcePaths.Entry {id="Scene."+Path.GetFileNameWithoutExtension(scene),path=Path.GetFileNameWithoutExtension(scene),kind="scene"});
            string catalogPath="Assets/Resources/ProjectResourceCatalog.json";
            if(File.Exists(catalogPath))
            {
                var existing=JsonUtility.FromJson<ResourcePaths.Manifest>(File.ReadAllText(catalogPath));
                if(existing.managedVersion>=2)
                {
                    // 显式添加的业务别名和手动路径优先保留；自动发现只补充新 ID。
                    var previous=existing.entries.ToDictionary(x=>x.id);
                    foreach(var entry in entries) if(!previous.ContainsKey(entry.id)) previous.Add(entry.id,entry);
                    entries=previous.Values.ToList();
                }
            }
            File.WriteAllText(catalogPath,JsonUtility.ToJson(new ResourcePaths.Manifest { managedVersion=2, entries=entries.OrderBy(x=>x.id).ToArray() },true));
            AssetDatabase.ImportAsset("Assets/Resources/ProjectResourceCatalog.json");
        }
        [MenuItem("Tools/Roguelike/维护/检查场景")]
        public static void ValidateAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try { foreach(var path in Scenes) Validate(EditorSceneManager.OpenScene(path)); }
            finally { if(!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        private static void Validate(Scene scene)
        {
            if(scene.rootCount!=1 || All<Canvas>(scene).Count(x=>x.isRootCanvas)!=1 || All<Manager>(scene).Count()!=1 || All<EventSystem>(scene).Count()!=1)
                throw new InvalidOperationException(scene.name+" 根节点/Canvas/UIManager/EventSystem 数量错误");
            var ids=new HashSet<string>();
            foreach(var panel in All<UIPanel>(scene))
            {
                if(!ids.Add(panel.panelId) || !PrefabUtility.IsPartOfPrefabInstance(panel)) throw new InvalidOperationException("面板 ID 重复或不是 Prefab："+panel.name);
                foreach(var target in panel.texts.Select(x=>(Component)x.target).Concat(panel.images.Select(x=>(Component)x.target)).Concat(panel.fills.Select(x=>(Component)x.target)))
                    if(!target || !target.transform.IsChildOf(panel.transform)) throw new InvalidOperationException("面板存在外部控件引用："+panel.name);
            }
            if(All<Transform>(scene).Any(x=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject)>0)) throw new InvalidOperationException("Missing Script："+scene.name);
            Debug.Log("SCENE_FRAMEWORK_PASS "+scene.name+" panels="+ids.Count);
        }
        public static void BuildVerificationPlayer()
        {
            ValidateAll();
            WriteManifest();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/SceneFramework");
            var report=BuildPipeline.BuildPlayer(Scenes,"Builds/SceneFramework/SceneFramework.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("场景框架验证构建失败");
            Debug.Log("SCENE_FRAMEWORK_BUILD_PASSED");
        }
    }
}
