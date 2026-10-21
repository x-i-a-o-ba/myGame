using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Combat
{
    /// <summary>场景 HUD 的展示组件；布局与字体由场景配置，只接收展示快照并发出重开意图。</summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [Header("输入遮挡区域")]
        [SerializeField,HideInInspector] private RectTransform bottom, titleRect, hintRect;
        [Header("状态显示")]
        [SerializeField,HideInInspector] private Image healthFill;
        [SerializeField,HideInInspector] private Text healthText, counterText, clockText, detailText, resultText;
        [Header("结算与交互")]
        [SerializeField,HideInInspector] private GameObject deathOverlay;
        [SerializeField,HideInInspector] private Button restartButton;

        public event Action RestartRequested;
        private Roguelike.Foundation.UI.UIManager UI => GetComponent<Roguelike.Foundation.UI.UIManager>();
        public bool HasBindings => UI != null;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(Roguelike.Foundation.UI.UiCommand command)
        { if(command.Id=="combat.restart") RestartRequested?.Invoke(); }
        public bool BlocksPointer(Vector2 position) => UI.Blocks(position);
        public void Refresh(CombatHudState player,float fps,float range)
        {
            UI.Fill("combat.healthFill",player.Health/Mathf.Max(1,player.MaximumHealth));
            UI.Text("combat.healthText",$"生命  {Mathf.CeilToInt(player.Health)} / {player.MaximumHealth:0}");
            UI.Text("combat.detailText",$"追踪弹 · 自动攻击    射程 {range:0}m\n移动中持续开火");
            UI.Text("combat.clockText",$"{(int)player.Elapsed/60:00}:{(int)player.Elapsed%60:00}     {fps:0} FPS");
            UI.Text("combat.counterText",$"存活敌人   {player.AliveEnemies:N0}\n击杀   {player.Kills:N0}       弹体   {player.ActiveProjectiles}\n滚轮拉远可查看整个敌群");
            if(player.Health<=0)
            {
                UI.Text("combat.resultText",$"生存 {player.Elapsed:0.0} 秒    ·    击杀 {player.Kills:N0}");
                UI.Show("UI.Game.DeathOverlay");
            }
            else UI.Hide("UI.Game.DeathOverlay");
        }

    }

    /// <summary>UI 所需的小型展示快照；不暴露 ECS 实体或 World。</summary>
    public struct CombatHudState
    {
        public float Health, MaximumHealth, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles;
    }
}
