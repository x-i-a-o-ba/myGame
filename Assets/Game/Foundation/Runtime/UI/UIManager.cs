using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Foundation.UI
{
    /// <summary>场景 Canvas 的唯一管理入口：面板登记、按需打开/关闭、输入遮挡和事件作用域。无战斗规则。</summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UIManager : MonoBehaviour
    {
        [Tooltip("动态面板的挂载层，静态面板保留场景位置。")] public Transform popupLayer;
        public UiEvents Events { get; } = new UiEvents();
        private readonly Dictionary<string, UIPanel> panels = new Dictionary<string, UIPanel>();
        private readonly HashSet<string> loading = new HashSet<string>();
        private readonly Dictionary<string, bool> desiredVisibility = new Dictionary<string, bool>();
        public int PanelCount => panels.Count;
        public event System.Action<UIPanel> PanelOpened;
        public UIPanel FindPanel(string id) => panels.TryGetValue(id, out var panel) ? panel : null;
        private void Awake()
        {
            foreach (var panel in GetComponentsInChildren<UIPanel>(true)) Register(panel);
            Events.Command += OnCommand;
        }
        private void Register(UIPanel panel)
        {
            if (panels.TryGetValue(panel.panelId, out var old) && old != panel)
                throw new System.InvalidOperationException("重复面板 ID：" + panel.panelId);
            panels[panel.panelId] = panel;
        }
        private void OnCommand(UiCommand command)
        {
            if (command.Id == "ui.help") Show("UI.DeveloperHelp");
            else if (command.Id == "ui.help.close") Hide("UI.DeveloperHelp");
        }
        private void Update() { if (Input.GetKeyDown(KeyCode.F1)) Show("UI.DeveloperHelp"); }
        public void Show(string id)
        {
            desiredVisibility[id] = true;
            if (panels.TryGetValue(id, out var panel) && panel) { panel.gameObject.SetActive(true); PanelOpened?.Invoke(panel); return; }
            // 先校验清单，避免失败协程把 ID 永久留在 loading 集合中。
            try { ResourcePaths.Resolve(id); }
            catch (System.Exception error) { Debug.LogError(error.Message, this); return; }
            if (loading.Add(id)) StartCoroutine(LoadPanel(id));
        }
        private IEnumerator LoadPanel(string id)
        {
            GameObject prefab = null;
            yield return ResourcePaths.LoadAsync<GameObject>(id, value => prefab = value);
            loading.Remove(id);
            if (!prefab) yield break;
            var instance = Instantiate(prefab, popupLayer ? popupLayer : transform, false);
            var panel = instance.GetComponent<UIPanel>();
            if (!panel || panel.panelId != id) { Destroy(instance); Debug.LogError("面板资源 ID 不匹配：" + id); yield break; }
            Register(panel);
            instance.SetActive(desiredVisibility.TryGetValue(id, out var show) && show);
            PanelOpened?.Invoke(panel);
        }
        public void Hide(string id)
        {
            desiredVisibility[id] = false;
            if (panels.TryGetValue(id, out var panel) && panel) panel.gameObject.SetActive(false);
        }
        public bool IsVisible(string id) => panels.TryGetValue(id, out var panel) && panel && panel.gameObject.activeInHierarchy;
        public bool Blocks(Vector2 point)
        {
            var canvas = GetComponent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var panel in panels.Values)
                if (panel && panel.blocksPointer && panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)panel.transform, point, camera)) return true;
            return false;
        }
        public void Text(string key, string value) => Events.Publish(new UiValue { Key = key, Text = value, Kind = UiValueKind.Text });
        public void Texture(string key, Texture value) => Events.Publish(new UiValue { Key = key, Texture = value, Kind = UiValueKind.Texture });
        public void Fill(string key, float value) => Events.Publish(new UiValue { Key = key, Number = value, Kind = UiValueKind.Fill });
        /// <summary>按键切换面板内绑定对象的显隐；对象不存在时忽略。</summary>
        public void Visible(string key, bool visible) => Events.Publish(new UiValue { Key = key, Number = visible ? 1 : 0, Kind = UiValueKind.Visible });
        private void OnDestroy() { StopAllCoroutines(); Events.Clear(); PanelOpened = null; panels.Clear(); loading.Clear(); desiredVisibility.Clear(); }
    }
}
