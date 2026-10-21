using System;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.Foundation.UI;
namespace Roguelike.SkillIndicators
{
    /// <summary>技能指示器的 UI 事件边界。按钮和文本控件全部由所属面板管理。</summary>
    public sealed class IndicatorDemoHud : MonoBehaviour
    {
        [SerializeField,HideInInspector] private Button qButton,wButton,eButton;
        [SerializeField,HideInInspector] private Text status,details;
        [SerializeField,HideInInspector] private RectTransform[] blockingPanels;
        private Roguelike.Foundation.UI.UIManager UI => GetComponent<Roguelike.Foundation.UI.UIManager>();
        public event Action<int> SkillRequested;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(UiCommand command) { if(command.Id=="indicator.skill") SkillRequested?.Invoke(command.Value); }
        public bool BlocksPointer(Vector2 point) => UI.Blocks(point);
        public void ShowStatus(string message,string parameters)
        { UI.Text("indicator.status",message); UI.Text("indicator.details",parameters); }
    }
}
