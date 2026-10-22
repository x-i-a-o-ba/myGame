using UnityEngine;
using UnityEngine.EventSystems;

namespace Roguelike.Foundation.UI
{
    /// <summary>
    /// 放置目标：<see cref="UIDragAction"/> 的拖动在本对象（或没有放置目标的子对象）上松开时，向所属面板发送 (command, value, 松开时的屏幕坐标)，
    /// 先于拖动源的结束命令。需要自身或子对象上有接收射线的 Graphic；不持有任何面板、控制器或业务对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIDropAction : MonoBehaviour, IDropHandler
    {
        [Tooltip("放置命令 ID，例如 combat.equip.dropOn。")] public string command;
        [Tooltip("随命令发送的数值，例如目标装备栏格。")] public int value;
        public void OnDrop(PointerEventData eventData)
        {
            if (!UIDragAction.Current || string.IsNullOrEmpty(command)) return;
            var panel = GetComponentInParent<UIPanel>();
            if (panel) panel.Send(command, value, eventData != null ? eventData.position : Vector2.zero);
        }
    }
}
