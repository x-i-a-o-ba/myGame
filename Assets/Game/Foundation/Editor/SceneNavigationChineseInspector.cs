using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Foundation.Editor
{
    /// <summary>
    /// 编辑器侧的场景 ID 查询：直接读取资源清单（按文件修改时间缓存）并核对 Build Settings，供 Inspector 实时提示与编辑器校验使用。
    /// 运行时仍只由 ResourcePaths 解析，本类不修改任何资源。
    /// </summary>
    public static class SceneCatalogLookup
    {
        public const string CatalogPath = "Assets/Resources/ProjectResourceCatalog.json";
        private static DateTime stamp = DateTime.MinValue;
        private static Dictionary<string, string> scenes = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>返回场景 ID 登记的场景名；未登记或类型不是 scene 时返回 null。</summary>
        public static string Resolve(string id)
        {
            Refresh();
            return id != null && scenes.TryGetValue(id, out var scene) ? scene : null;
        }

        /// <summary>场景名是否是 Build Settings 中启用的场景（运行时按名称加载需要）。</summary>
        public static bool InBuildSettings(string scene) =>
            !string.IsNullOrEmpty(scene) && EditorBuildSettings.scenes.Any(x => x.enabled && Path.GetFileNameWithoutExtension(x.path) == scene);

        /// <summary>检查场景 ID 能否在运行时加载；返回错误说明，没有错误时返回 null。</summary>
        public static string Check(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "场景 ID 为空。";
            if (id.Trim() != id || !id.StartsWith("Scene.", StringComparison.Ordinal)) return "场景 ID 必须是 Scene.<场景名> 且不带首尾空格：" + id;
            string scene = Resolve(id);
            if (scene == null) return "场景 ID 未登记在 " + CatalogPath + "（类型须为 scene）：" + id;
            if (!InBuildSettings(scene)) return "场景 " + scene + " 不在 Build Settings 的启用场景中，运行时无法加载。";
            return null;
        }

        private static void Refresh()
        {
            var time = File.Exists(CatalogPath) ? File.GetLastWriteTimeUtc(CatalogPath) : DateTime.MinValue;
            if (time == stamp) return;
            stamp = time;
            scenes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (time == DateTime.MinValue) return;
            try
            {
                var manifest = JsonUtility.FromJson<ResourcePaths.Manifest>(File.ReadAllText(CatalogPath));
                if (manifest?.entries == null) return;
                foreach (var entry in manifest.entries)
                    if (entry != null && entry.kind == "scene" && !string.IsNullOrEmpty(entry.id) && !scenes.ContainsKey(entry.id)) scenes.Add(entry.id, entry.path);
            }
            catch (ArgumentException) { scenes.Clear(); }
        }
    }

    /// <summary>场景导航路由表的编辑器校验：Inspector 与开局流程校验共用，只读取不修改。</summary>
    public static class SceneNavigationChecks
    {
        /// <summary>返回路由表的全部错误；空列表表示有效。</summary>
        public static List<string> Validate(SceneNavigation navigation)
        {
            var errors = new List<string>();
            if (!navigation) { errors.Add("缺少场景导航组件。"); return errors; }
            var routes = navigation.routes ?? Array.Empty<SceneNavigation.Route>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (string.IsNullOrWhiteSpace(route.command) || route.command.Trim() != route.command)
                {
                    errors.Add("第 " + (i + 1) + " 条路由的 UI 命令为空或带首尾空格。");
                    continue;
                }
                if (!seen.Add(route.command)) errors.Add("UI 命令 " + route.command + " 重复：同一命令只会执行第一条路由。");
                if (float.IsNaN(route.confirmSeconds) || float.IsInfinity(route.confirmSeconds) || route.confirmSeconds < 0)
                    errors.Add(route.command + " 的确认时间必须是不小于 0 的有限秒数。");
                switch (route.action)
                {
                    case SceneRouteAction.LoadScene:
                        string error = SceneCatalogLookup.Check(route.sceneId);
                        if (error != null) errors.Add(route.command + "：" + error);
                        break;
                    case SceneRouteAction.Quit: break;
                    default: errors.Add(route.command + " 的动作无效：" + (int)route.action); break;
                }
            }
            return errors;
        }

        /// <summary>命令对应的路由；没有时返回 false。</summary>
        public static bool TryGet(SceneNavigation navigation, string command, out SceneNavigation.Route route)
        {
            int index = navigation ? navigation.Find(command) : -1;
            route = index >= 0 ? navigation.routes[index] : default;
            return index >= 0;
        }
    }

    /// <summary>场景导航（SceneNavigation）的中文 Inspector：逐条编辑命令路由，并实时校验重复命令与场景 ID。</summary>
    [CustomEditor(typeof(SceneNavigation))]
    internal sealed class SceneNavigationChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("场景导航：把本 Canvas 面板按钮（以及转发到命令通道的键盘操作）发出的 UI 命令映射为“加载场景”或“退出游戏”，面板自己不切换场景。" +
                "确认时间大于 0 的路由第一次点击只显示绑定到“命令.confirm”的提示，在确认时间内再次点击才执行，超时自动取消。修改后重新进入场景生效。", MessageType.Info);
            var routes = serializedObject.FindProperty("routes");
            if (routes == null) { EditorGUILayout.HelpBox("缺少字段：routes", MessageType.Error); return; }
            Section("路由（" + routes.arraySize + " 条）");
            int remove = -1;
            for (int i = 0; i < routes.arraySize; i++)
            {
                var route = routes.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string command = route.FindPropertyRelative("command").stringValue;
                        EditorGUILayout.LabelField((i + 1) + ". " + (string.IsNullOrEmpty(command) ? "（未填写命令）" : command), EditorStyles.boldLabel);
                        if (GUILayout.Button("删除", GUILayout.Width(48))) remove = i;
                    }
                    Relative(route, "command", "UI 命令", "触发本路由的 UI 命令 ID，例如 main.start；同一命令只能出现一次。");
                    var action = route.FindPropertyRelative("action");
                    Relative(route, "action", "动作", "加载场景：按场景 ID 切换场景；退出游戏：关闭 Player（编辑器中无效果）。");
                    if (action.hasMultipleDifferentValues || action.enumValueIndex == (int)SceneRouteAction.LoadScene)
                        Relative(route, "sceneId", "场景 ID", "资源清单（Resources/ProjectResourceCatalog.json）中的场景 ID，例如 Scene.SelectScene；场景须在 Build Settings 中启用。");
                    Relative(route, "confirmSeconds", "确认时间（秒）", "大于 0 时首次点击只显示“命令.confirm”提示，需在该时间（真实时间，不受暂停影响）内再次点击才执行；0 表示立即执行。");
                }
            }
            if (remove >= 0) routes.DeleteArrayElementAtIndex(remove);
            if (GUILayout.Button("添加路由"))
            {
                routes.InsertArrayElementAtIndex(routes.arraySize);
                var added = routes.GetArrayElementAtIndex(routes.arraySize - 1);
                added.FindPropertyRelative("command").stringValue = "";
                added.FindPropertyRelative("action").enumValueIndex = (int)SceneRouteAction.LoadScene;
                added.FindPropertyRelative("sceneId").stringValue = "";
                added.FindPropertyRelative("confirmSeconds").floatValue = 0;
            }
            serializedObject.ApplyModifiedProperties();
            if (targets.Length != 1) return;
            var navigation = (SceneNavigation)target;
            Section("校验");
            var errors = SceneNavigationChecks.Validate(navigation);
            if (errors.Count == 0) EditorGUILayout.HelpBox("路由有效。", MessageType.None);
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (Application.isPlaying && navigation.PendingCommand != null)
                EditorGUILayout.HelpBox("等待再次点击确认：" + navigation.PendingCommand, MessageType.None);
        }

        private static void Relative(SerializedProperty parent, string name, string label, string tooltip)
        {
            var property = parent.FindPropertyRelative(name);
            if (property == null) { EditorGUILayout.HelpBox("缺少字段：" + name, MessageType.Error); return; }
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private static void Section(string text) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(text, EditorStyles.boldLabel); }
    }
}
