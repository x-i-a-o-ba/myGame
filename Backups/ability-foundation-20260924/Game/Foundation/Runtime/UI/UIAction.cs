using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>按钮只向所属面板发送意图，不拖拽任何外部面板或业务对象。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIAction : MonoBehaviour
    {
        public string command;
        public int value;
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Click);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Click);
        private void Click() => GetComponentInParent<UIPanel>()?.Send(command, value);
    }
}
