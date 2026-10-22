using UnityEngine;
using UnityEngine.EventSystems;

namespace Roguelike.Foundation.UI
{
    /// <summary>
    /// 悬停提示源：指针进入时向所属面板发送 (command, value)，离开或被禁用时发送 (command, <see cref="ExitValue"/>(value))。
    /// 同时把自身矩形登记为 <see cref="UIManager.HoverSource"/>，供提示框定位。不持有任何面板、控制器或业务对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIHoverAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("悬停命令 ID，例如 combat.hover。")] public string command;
        [Tooltip("进入时发送的数值（非负）；离开时发送 -1 - 数值。")] public int value;
        private bool hovered;
        /// <summary>当前是否处于悬停状态。</summary>
        public bool IsHovered => hovered;
        /// <summary>离开消息的数值编码：-1 - value，始终为负数，便于和进入消息区分。</summary>
        public static int ExitValue(int value) => -1 - value;
        public void OnPointerEnter(PointerEventData eventData) { if (hovered) return; hovered = true; Notify(true); }
        public void OnPointerExit(PointerEventData eventData) { if (!hovered) return; hovered = false; Notify(false); }
        // 面板被隐藏或对象被销毁时不会收到指针离开事件，这里补发一次，避免提示框残留。
        private void OnDisable() { if (!hovered) return; hovered = false; Notify(false); }
        private void Notify(bool entered)
        {
            var manager = GetComponentInParent<UIManager>(true);
            if (manager) manager.SetHover((RectTransform)transform, entered);
            var panel = GetComponentInParent<UIPanel>(true);
            if (panel) panel.Send(command, entered ? value : ExitValue(value));
        }
    }
}
