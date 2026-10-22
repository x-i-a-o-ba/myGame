using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>
    /// 拖动意图（例如 Dota 2 式拖动装备）：按住左键拖动时向所属面板发送开始命令 (beginCommand, value)，
    /// 松开时发送结束命令 (endCommand, value, 松开时的屏幕坐标)；松开位置上的 <see cref="UIDropAction"/> 先于结束命令发送放置命令。
    /// 拖动期间在根画布最上层显示一个跟随指针的图标副本，并让源图标变暗。不持有任何面板、控制器或业务对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIDragAction : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("开始拖动时发送的命令 ID，例如 combat.equip.drag。")] public string beginCommand;
        [Tooltip("松开时发送的命令 ID（附带松开时的屏幕坐标），例如 combat.equip.dragEnd。")] public string endCommand;
        [Tooltip("随命令发送的数值，例如装备栏格。")] public int value;
        [Tooltip("只有该对象显示时才能开始拖动（例如格子里有装备时）；为空表示总是可以拖动。")] public GameObject requireActive;
        [Tooltip("拖动时跟随指针的图标：复制它的贴图与大小，拖动期间它变暗。为空时不显示拖动图标。")] public RawImage ghost;
        [Range(0, 1), Tooltip("拖动期间源图标的透明度倍率。")] public float dimmed = 0.35f;
        /// <summary>正在进行的拖动；没有拖动时为 null。同一时间只有一个拖动。</summary>
        public static UIDragAction Current { get; private set; }
        public bool IsDragging => Current == this;
        /// <summary>拖动图标副本（只读，供检查使用）；没有拖动时为 null。</summary>
        public RawImage Ghost => copy;
        private RawImage copy;
        private Color original = Color.white;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (requireActive && !requireActive.activeInHierarchy) return;
            if (Current && Current != this) Current.Finish(null, false);
            Current = this;
            ShowGhost(eventData.position);
            Send(beginCommand, eventData.position);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (IsDragging && eventData != null) Move(eventData.position);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (IsDragging) Finish(eventData, true);
        }
        // 对象被禁用或销毁时结束拖动但不发送结束命令（不当作松开），只清理拖动图标。
        private void OnDisable() { if (IsDragging) Finish(null, false); }
        private void Finish(PointerEventData eventData, bool notify)
        {
            Current = null;
            HideGhost();
            if (notify) Send(endCommand, eventData != null ? eventData.position : Vector2.zero);
        }
        private void Send(string command, Vector2 pointer)
        {
            if (string.IsNullOrEmpty(command)) return;
            var panel = GetComponentInParent<UIPanel>();
            if (panel) panel.Send(command, value, pointer);
        }
        private void ShowGhost(Vector2 screen)
        {
            if (!ghost) return;
            var canvas = ghost.canvas ? ghost.canvas.rootCanvas : null;
            if (!canvas) return;
            original = ghost.color;
            var item = new GameObject("Drag Ghost", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rect = (RectTransform)item.transform;
            rect.SetParent(canvas.transform, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ghost.rectTransform.rect.size;
            copy = item.GetComponent<RawImage>();
            copy.texture = ghost.texture; copy.uvRect = ghost.uvRect; copy.raycastTarget = false;
            copy.color = new Color(original.r, original.g, original.b, original.a * 0.9f);
            ghost.color = new Color(original.r, original.g, original.b, original.a * dimmed);
            Move(screen);
        }
        private void Move(Vector2 screen)
        {
            if (!copy) return;
            var rect = copy.rectTransform;
            var canvas = copy.canvas;
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rect.parent, screen, camera, out var world)) rect.position = world;
        }
        private void HideGhost()
        {
            if (ghost) ghost.color = original;
            if (copy) Destroy(copy.gameObject);
            copy = null;
        }
    }
}
