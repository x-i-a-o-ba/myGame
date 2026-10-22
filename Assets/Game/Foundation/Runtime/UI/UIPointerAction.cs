using UnityEngine;
using UnityEngine.EventSystems;

namespace Roguelike.Foundation.UI
{
    /// <summary>
    /// 指针点击意图：用指定鼠标键（默认右键）点击时向所属面板发送 (command, value)，例如右键出售装备。
    /// 需要自身或子对象上有接收射线的 Graphic；不持有任何面板、控制器或业务对象。左键点击请继续使用 Button + <see cref="UIAction"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPointerAction : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("点击命令 ID，例如 combat.equip.sell。")] public string command;
        [Tooltip("随命令发送的数值。")] public int value;
        [Tooltip("响应的鼠标键。")] public PointerEventData.InputButton button = PointerEventData.InputButton.Right;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != button || string.IsNullOrEmpty(command)) return;
            var panel = GetComponentInParent<UIPanel>();
            if (panel) panel.Send(command, value);
        }
    }
}
