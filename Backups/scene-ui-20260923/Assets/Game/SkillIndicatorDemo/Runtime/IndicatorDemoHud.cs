using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.SkillIndicators
{
    /// <summary>场景内的演示面板；按钮只发送选择意图，不引用施法控制器。</summary>
    public sealed class IndicatorDemoHud : MonoBehaviour
    {
        [SerializeField] private Button qButton, wButton, eButton;
        [SerializeField] private Text status, details;
        [SerializeField] private RectTransform[] blockingPanels;
        public event Action<int> SkillRequested;
        private void OnEnable()
        {
            qButton.onClick.AddListener(Q); wButton.onClick.AddListener(W); eButton.onClick.AddListener(E);
        }
        private void OnDisable()
        {
            qButton.onClick.RemoveListener(Q); wButton.onClick.RemoveListener(W); eButton.onClick.RemoveListener(E);
        }
        private void Q() => SkillRequested?.Invoke(0);
        private void W() => SkillRequested?.Invoke(1);
        private void E() => SkillRequested?.Invoke(2);

        public bool BlocksPointer(Vector2 screen)
        {
            foreach (var panel in blockingPanels)
                if (panel && panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(panel, screen)) return true;
            return false;
        }

        public void ShowStatus(string message, string parameters)
        {
            status.text = message;
            details.text = parameters;
        }
    }
}
