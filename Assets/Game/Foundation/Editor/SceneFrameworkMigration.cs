using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Benchmark;
using Roguelike.Combat;
using Roguelike.FogOfWar;
using Roguelike.Foundation.UI;
using Roguelike.SkillIndicators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Manager = Roguelike.Foundation.UI.UIManager;

namespace Roguelike.Foundation.Editor
{
    /// <summary>显式、可重复的七场景迁移。通过序列化引用搬迁，不重建原有布局或改写玩法配置。</summary>
    public static class SceneFrameworkMigration
    {
        public static readonly string[] Scenes = {
            "Assets/Scenes/Main.unity", "Assets/Scenes/SelectScene.unity", "Assets/Scenes/Game.unity",
            "Assets/Scenes/SkillIndicatorDemo.unity", "Assets/Scenes/FogOfWarDemo.unity",
            "Assets/Scenes/SkillArchitectureDemo.unity", "Assets/Game/Scenes/Benchmark10k.unity"
        };
        private const string PanelRoot = "Assets/Resources/UI/Panels";

        public static void MigrateAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                CreateHelp();
                foreach (string path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    if (!All<SceneLayout>(scene).Any())
                    {
                        var backup = "Backups/scene-framework-editor/" + path;
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        if (!File.Exists(backup)) File.Copy(path, backup);
                        Migrate(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    // 从第一版层级显式升级数据查找作用域，不改变开发者排好的节点。
                    foreach(var fog in All<FogDemoSession>(scene))
                        if(!fog.contentRoot)
                        {
                            fog.contentRoot=All<SceneLayout>(scene).Single().transform.parent;
                            EditorUtility.SetDirty(fog);
                            EditorSceneManager.SaveScene(scene);
                        }
                    Validate(scene);
                }
                WriteManifest();
                AssetDatabase.SaveAssets();
                Debug.Log("SCENE_FRAMEWORK_MIGRATION_PASSED: 7 scenes");
            }
            finally { if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        private static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
        private static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name).transform; node.SetParent(parent, false); return node;
        }
        private static RectTransform Layer(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); node.SetParent(parent, false);
            node.anchorMin = Vector2.zero; node.anchorMax = Vector2.one; node.offsetMin = node.offsetMax = Vector2.zero; return node;
        }
        private static void Migrate(Scene scene)
        {
            var oldRoots = scene.GetRootGameObjects();
            var root = Node(scene.name + "_Scene", null);
            Node("00_Bootstrap", root).gameObject.AddComponent<SceneLayout>();
            var systems = Node("01_Systems", root);
            var world = Node("02_World", root);
            var environment = Node("Environment", world); var actors = Node("Actors", world);
            var presentation = Node("03_Presentation", root);
            var cameras = Node("Cameras", presentation); var lighting = Node("Lighting", presentation); var effects = Node("Effects", presentation);
            var uiRoot = Node("04_UI", root); Node("05_Debug", root);

            var canvases = All<Canvas>(scene).Where(c => c.isRootCanvas).ToArray();
            if (canvases.Length > 1) throw new InvalidOperationException(scene.name + " 有多个根 Canvas，需显式处理渲染模式后合并。");
            var canvas = canvases.FirstOrDefault();
            if (!canvas)
            {
                var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920,1080);
            }
            canvas.transform.SetParent(uiRoot, false); canvas.name = "Canvas - UIManager";
            MigrateUI(scene, canvas);
            var eventSystems = All<EventSystem>(scene).ToArray();
            if (eventSystems.Length == 0) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(uiRoot, false);
            else foreach (var input in eventSystems) input.transform.SetParent(uiRoot, true);

            foreach (var camera in All<Camera>(scene).ToArray()) camera.transform.SetParent(cameras, true);
            foreach (var light in All<Light>(scene).ToArray()) light.transform.SetParent(lighting, true);
            foreach (var old in oldRoots)
            {
                if (!old || old.transform.parent) continue;
                var hasBehaviour = old.GetComponents<MonoBehaviour>().Any();
                if (!hasBehaviour) { old.transform.SetParent(environment, true); continue; }
                old.transform.SetParent(systems, true);
                foreach (Transform child in old.transform.Cast<Transform>().ToArray())
                {
                    if (child.name == "Scene Views")
                    {
                        foreach (Transform view in child.Cast<Transform>().ToArray())
                            view.SetParent(view.name.Contains("Arena") ? environment : view.name.Contains("Player") ? actors : effects, true);
                        Object.DestroyImmediate(child.gameObject);
                    }
                    else
                    {
                        string name = child.name.ToLowerInvariant();
                        Transform parent = name.Contains("camera") ? cameras : name.Contains("player") || name.Contains("caster") || name.Contains("source") || name.Contains("observer") || name.Contains("vision") ? actors :
                            name.Contains("indicator") || name.Contains("feedback") || name.Contains("area") || name.Contains("fog") || name.Contains("visual") ? effects : environment;
                        child.SetParent(parent, true);
                    }
                }
            }
            var benchmark = All<BenchmarkRunner>(scene).FirstOrDefault();
            if (benchmark)
            {
                var camera = new GameObject("Benchmark Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.transform.SetParent(cameras, false); camera.transform.position = new Vector3(0,110,0); camera.transform.rotation = Quaternion.Euler(90,0,0);
                camera.orthographic = true; camera.orthographicSize = 55; camera.farClipPlane = 300;
                benchmark.benchmarkCamera = camera;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); benchmark.unitMesh = cube.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(cube);
            }
        }
        private static void MigrateUI(Scene scene, Canvas canvas)
        {
            var manager = canvas.gameObject.AddComponent<Manager>();
            var textKeys = new Dictionary<Text,string>();
            var imageKeys = new Dictionary<RawImage,string>();
            var fillKeys = new Dictionary<Image,string>();
            var legacy = canvas.GetComponents<MonoBehaviour>().Where(x => x is SkillDemoHud || x is FogDemoHud || x is CombatHud || x is IndicatorDemoHud).ToArray();
            foreach (var hud in legacy)
            {
                var data = new SerializedObject(hud);
                string prefix = hud is SkillDemoHud ? "ability." : hud is FogDemoHud ? "fog." : hud is CombatHud ? "combat." : "indicator.";
                var iterator = data.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
                    var value = iterator.objectReferenceValue;
                    if (value is Text text) textKeys[text] = prefix + iterator.name;
                    if (value is RawImage image) imageKeys[image] = prefix + iterator.name;
                    if (value is Image fill) fillKeys[fill] = prefix + iterator.name;
                    if (value is Button button)
                    {
                        if (hud is IndicatorDemoHud) Action(button, "indicator.skill", iterator.name == "qButton" ? 0 : iterator.name == "wButton" ? 1 : 2);
                        if (hud is CombatHud) Action(button, "combat.restart", 0);
                    }
                    if (hud is CombatHud && iterator.name == "deathOverlay" && value is GameObject death) death.name = "DeathOverlay";
                    // 迁移后的桥接层不再持有任意面板控件引用。
                    iterator.objectReferenceValue = null;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var button in canvas.GetComponentsInChildren<SkillDemoButton>(true))
            { Action(button.GetComponent<Button>(), "ability.command", (int)button.command); Object.DestroyImmediate(button); }
            foreach (var button in canvas.GetComponentsInChildren<FogDemoButton>(true))
            { Action(button.GetComponent<Button>(), "fog.command", (int)button.command); Object.DestroyImmediate(button); }
            foreach (var old in canvas.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x && x.GetType().Name == "MainPanel").ToArray()) Object.DestroyImmediate(old);
            if (scene.name == "Main")
            {
                canvas.gameObject.AddComponent<SceneNavigation>();
                foreach (var button in canvas.GetComponentsInChildren<Button>(true)) Action(button, button.name == "btnStart" ? "main.start" : "main.quit", 0);
            }
            var children = canvas.transform.Cast<Transform>().ToArray();
            var hudLayer = Layer("00_HUD", canvas.transform);
            var panelLayer = Layer("10_Panels", canvas.transform);
            var popupLayer = Layer("20_Popups", canvas.transform);
            Layer("30_Overlay", canvas.transform); manager.popupLayer = popupLayer;
            foreach (var child in children)
            {
                child.SetParent(child.name == "DeathOverlay" ? popupLayer : hudLayer, false);
                var panel = child.gameObject.AddComponent<UIPanel>();
                panel.panelId = "UI." + scene.name + "." + Safe(child.name);
                panel.texts = textKeys.Where(x => x.Key.transform.IsChildOf(child)).Select(x => new UIPanel.TextBinding { key=x.Value, target=x.Key }).ToArray();
                panel.images = imageKeys.Where(x => x.Key.transform.IsChildOf(child)).Select(x => new UIPanel.ImageBinding { key=x.Value, target=x.Key }).ToArray();
                panel.fills = fillKeys.Where(x => x.Key.transform.IsChildOf(child)).Select(x => new UIPanel.FillBinding { key=x.Value, target=x.Key }).ToArray();
                SavePanel(panel);
            }
        }
        private static string Safe(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray());
        private static void Action(Button button, string command, int value)
        {
            var action = button.gameObject.GetComponent<UIAction>() ?? button.gameObject.AddComponent<UIAction>();
            action.command = command; action.value = value;
        }
        private static void SavePanel(UIPanel panel)
        {
            Directory.CreateDirectory(PanelRoot);
            string path = PanelRoot + "/" + panel.panelId + ".prefab";
            // 首次迁移生成 Prefab；已有文件不覆盖开发者编辑。
            if (File.Exists(path)) throw new InvalidOperationException("发现已有面板资源，请人工确认迁移冲突：" + path);
            PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject, path, InteractionMode.AutomatedAction);
        }
        private static void CreateHelp()
        {
            string path = PanelRoot + "/UI.DeveloperHelp.prefab";
            if (File.Exists(path)) return;
            Directory.CreateDirectory(PanelRoot);
            var go = new GameObject("Developer Help", typeof(RectTransform), typeof(Image), typeof(UIPanel));
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(720,360);
            go.GetComponent<Image>().color = new Color(.035f,.055f,.075f,.99f);
            go.GetComponent<UIPanel>().panelId = "UI.DeveloperHelp";
            var label = new GameObject("说明文本", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform,false);
            var text = label.GetComponent<Text>(); text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf"); text.fontSize=23; text.color=Color.white; text.raycastTarget=false;
            text.text = "开发者场景框架\n\n面板由 UIManager 统一管理，F1 按需加载此面板。\n布局：Resources/UI/Panels 的 Prefab。\n路径：Resources/ProjectResourceCatalog.json。\n面板只绑定内部控件，通过事件交换外部数据。\n详细规范：Docs/scene-ui-framework.md";
            var textRect = (RectTransform)label.transform; textRect.anchorMin=Vector2.zero; textRect.anchorMax=Vector2.one; textRect.offsetMin=new Vector2(24,65); textRect.offsetMax=new Vector2(-24,-24);
            var close = new GameObject("Close",typeof(RectTransform),typeof(Image),typeof(Button)); close.transform.SetParent(go.transform,false);
            var buttonRect=(RectTransform)close.transform; buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(.5f,0);buttonRect.pivot=new Vector2(.5f,0);buttonRect.anchoredPosition=new Vector2(0,15);buttonRect.sizeDelta=new Vector2(180,40);
            close.GetComponent<Image>().color=new Color(.15f,.25f,.32f);Action(close.GetComponent<Button>(),"ui.help.close",0);
            var caption = Object.Instantiate(label,close.transform,false); caption.name="Caption"; caption.GetComponent<Text>().text="关闭";caption.GetComponent<Text>().alignment=TextAnchor.MiddleCenter;
            var cr=(RectTransform)caption.transform;cr.offsetMin=cr.offsetMax=Vector2.zero;
            PrefabUtility.SaveAsPrefabAsset(go,path); Object.DestroyImmediate(go);
        }
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
            MigrateAll();
            Directory.CreateDirectory("Builds/SceneFramework");
            var report=BuildPipeline.BuildPlayer(Scenes,"Builds/SceneFramework/SceneFramework.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("场景框架验证构建失败");
            Debug.Log("SCENE_FRAMEWORK_BUILD_PASSED");
        }
    }
}
