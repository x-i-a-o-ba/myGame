using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Combat
{
    /// <summary>场景 HUD 的展示组件；布局与字体由场景配置，只接收展示快照并发出重开意图。</summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [Header("输入遮挡区域")]
        [SerializeField] private RectTransform bottom, titleRect, hintRect;
        [Header("状态显示")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthText, counterText, clockText, detailText, resultText;
        [Header("结算与交互")]
        [SerializeField] private GameObject deathOverlay;
        [SerializeField] private Button restartButton;

        public event Action RestartRequested;
        public bool HasBindings => bottom && titleRect && hintRect && healthFill && healthText &&
            counterText && clockText && detailText && resultText && deathOverlay && restartButton;

        private void OnEnable()
        {
            if (restartButton) restartButton.onClick.AddListener(RequestRestart);
        }

        private void OnDisable()
        {
            if (restartButton) restartButton.onClick.RemoveListener(RequestRestart);
        }

        private void RequestRestart() => RestartRequested?.Invoke();

        public bool BlocksPointer(Vector2 position) => deathOverlay != null && deathOverlay.activeSelf ||
            bottom != null && RectTransformUtility.RectangleContainsScreenPoint(bottom, position) ||
            titleRect != null && RectTransformUtility.RectangleContainsScreenPoint(titleRect, position) ||
            hintRect != null && RectTransformUtility.RectangleContainsScreenPoint(hintRect, position);

        public void Refresh(CombatHudState player, float fps, float range)
        {
            if (healthFill == null) return;
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(player.Health / Mathf.Max(1, player.MaximumHealth)), 1);
            healthText.text = $"生命  {Mathf.CeilToInt(player.Health)} / {player.MaximumHealth:0}";
            detailText.text = $"追踪弹 · 自动攻击    射程 {range:0}m\n移动中持续开火";
            clockText.text = $"{(int)player.Elapsed / 60:00}:{(int)player.Elapsed % 60:00}     {fps:0} FPS";
            counterText.text = $"存活敌人   {player.AliveEnemies:N0}\n击杀   {player.Kills:N0}       弹体   {player.ActiveProjectiles}\n滚轮拉远可查看整个敌群";
            deathOverlay.SetActive(player.Health <= 0);
            if (player.Health <= 0) resultText.text = $"生存 {player.Elapsed:0.0} 秒    ·    击杀 {player.Kills:N0}";
        }

    }

    /// <summary>UI 所需的小型展示快照；不暴露 ECS 实体或 World。</summary>
    public struct CombatHudState
    {
        public float Health, MaximumHealth, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles;
    }
}
