using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>填充绑定的写入方式：水平/垂直改锚点（适合血条），图片填充量适合径向冷却与经验环。</summary>
    public enum FillAxis
    {
        [InspectorName("水平（修改右锚点）")] Horizontal,
        [InspectorName("垂直（修改上锚点）")] Vertical,
        [InspectorName("图片填充量（Filled 图片）")] Amount
    }

    /// <summary>一个面板只持有自己的控件。数据通过事件进入，命令通过事件离开，可独立制作 Prefab。</summary>
    public class UIPanel : MonoBehaviour
    {
        [Serializable] public struct TextBinding { public string key; public Text target; }
        [Serializable] public struct ImageBinding { public string key; public RawImage target; }
        [Serializable] public struct FillBinding { public string key; public Image target; public FillAxis axis; }
        /// <summary>显隐绑定：数值大于 0.5 显示；invert 为真时反向。</summary>
        [Serializable] public struct ToggleBinding { public string key; public GameObject target; public bool invert; }
        [Tooltip("唯一面板 ID，与资源清单保持一致。")] public string panelId;
        [Tooltip("鼠标落在本面板内时阻止地面操作。")] public bool blocksPointer = true;
        public TextBinding[] texts = Array.Empty<TextBinding>();
        public ImageBinding[] images = Array.Empty<ImageBinding>();
        public FillBinding[] fills = Array.Empty<FillBinding>();
        public ToggleBinding[] toggles = Array.Empty<ToggleBinding>();
        private UIManager manager;
        // 键到绑定下标的索引；面板启用时重建，避免每条消息遍历全部绑定。
        private readonly Dictionary<string, List<int>> textIndex = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, List<int>> imageIndex = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, List<int>> fillIndex = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, List<int>> toggleIndex = new Dictionary<string, List<int>>();

        protected UiEvents Channel => manager ? manager.Events : null;
        protected virtual void OnEnable()
        {
            manager = GetComponentInParent<UIManager>();
            if (!manager) { Debug.LogError("面板必须位于 UIManager 的 Canvas 下：" + panelId, this); return; }
            BuildIndex();
            manager.Events.Changed += Receive;
            manager.Events.Replay(Receive);
        }
        protected virtual void OnDisable() { if (manager) manager.Events.Changed -= Receive; }
        public void Send(string id, int value) { if (manager) manager.Events.Send(new UiCommand(id, value)); }
        /// <summary>发送附带指针屏幕坐标的命令（拖动松开、放置等）。</summary>
        public void Send(string id, int value, Vector2 pointer) { if (manager) manager.Events.Send(new UiCommand(id, value, pointer)); }
        private void BuildIndex()
        {
            Index(textIndex, texts, item => item.key);
            Index(imageIndex, images, item => item.key);
            Index(fillIndex, fills, item => item.key);
            Index(toggleIndex, toggles, item => item.key);
        }
        private static void Index<T>(Dictionary<string, List<int>> index, T[] items, Func<T, string> key)
        {
            index.Clear();
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                string name = key(items[i]);
                if (string.IsNullOrEmpty(name)) continue;
                if (!index.TryGetValue(name, out var list)) index[name] = list = new List<int>(1);
                list.Add(i);
            }
        }
        protected virtual void Receive(UiValue value)
        {
            if (value.Key == null) return;
            if (value.Kind == UiValueKind.Text && textIndex.TryGetValue(value.Key, out var textTargets))
                foreach (int i in textTargets)
                {
                    var target = texts[i].target;
                    if (target && target.text != value.Text) target.text = value.Text;
                }
            if (value.Kind == UiValueKind.Texture && imageIndex.TryGetValue(value.Key, out var imageTargets))
                foreach (int i in imageTargets) { if (images[i].target) { images[i].target.texture = value.Texture; images[i].target.enabled=value.Texture!=null; } }
            if (value.Kind == UiValueKind.Fill && fillIndex.TryGetValue(value.Key, out var fillTargets))
                foreach (int i in fillTargets)
                {
                    var target = fills[i].target;
                    if (!target) continue;
                    float amount = Mathf.Clamp01(value.Number);
                    if (fills[i].axis == FillAxis.Amount) target.fillAmount = amount;
                    else if (fills[i].axis == FillAxis.Vertical) target.rectTransform.anchorMax = new Vector2(target.rectTransform.anchorMax.x, amount);
                    else target.rectTransform.anchorMax = new Vector2(amount, target.rectTransform.anchorMax.y);
                }
            if (value.Kind == UiValueKind.Visible && toggleIndex.TryGetValue(value.Key, out var toggleTargets))
                foreach (int i in toggleTargets)
                {
                    var target = toggles[i].target;
                    bool visible = (value.Number > 0.5f) != toggles[i].invert;
                    if (target && target.activeSelf != visible) target.SetActive(visible);
                }
        }
    }
}
