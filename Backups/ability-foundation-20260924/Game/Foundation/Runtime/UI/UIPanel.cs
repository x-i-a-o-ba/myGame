using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>一个面板只持有自己的控件。数据通过事件进入，命令通过事件离开，可独立制作 Prefab。</summary>
    public class UIPanel : MonoBehaviour
    {
        [Serializable] public struct TextBinding { public string key; public Text target; }
        [Serializable] public struct ImageBinding { public string key; public RawImage target; }
        [Serializable] public struct FillBinding { public string key; public Image target; }
        [Tooltip("唯一面板 ID，与资源清单保持一致。")] public string panelId;
        [Tooltip("鼠标落在本面板内时阻止地面操作。")] public bool blocksPointer = true;
        public TextBinding[] texts = Array.Empty<TextBinding>();
        public ImageBinding[] images = Array.Empty<ImageBinding>();
        public FillBinding[] fills = Array.Empty<FillBinding>();
        private UIManager manager;

        protected UiEvents Channel => manager ? manager.Events : null;
        protected virtual void OnEnable()
        {
            manager = GetComponentInParent<UIManager>();
            if (!manager) { Debug.LogError("面板必须位于 UIManager 的 Canvas 下：" + panelId, this); return; }
            manager.Events.Changed += Receive;
            manager.Events.Replay(Receive);
        }
        protected virtual void OnDisable() { if (manager) manager.Events.Changed -= Receive; }
        public void Send(string id, int value) { if (manager) manager.Events.Send(new UiCommand(id, value)); }
        protected virtual void Receive(UiValue value)
        {
            if (value.Kind == UiValueKind.Text)
                foreach (var item in texts) if (item.key == value.Key && item.target && item.target.text != value.Text) item.target.text = value.Text;
            if (value.Kind == UiValueKind.Texture)
                foreach (var item in images) if (item.key == value.Key && item.target) item.target.texture = value.Texture;
            if (value.Kind == UiValueKind.Fill)
                foreach (var item in fills) if (item.key == value.Key && item.target)
                    item.target.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value.Number), 1);
        }
    }
}
