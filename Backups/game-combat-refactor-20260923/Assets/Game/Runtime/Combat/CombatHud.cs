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
            UI.Text("combat.detailText",$"寒冰技能 · 自动攻击可见目标    射程 {range:0}m\n右键移动 / QWER 施放 / N 切换昼夜");
            UI.Text("combat.skillQ",player.SkillQ);
            UI.Text("combat.skillW",player.SkillW);
            UI.Text("combat.skillE",player.SkillE);
            UI.Text("combat.skillR",player.SkillR);
            UI.Text("combat.clockText",$"{(int)player.Elapsed/60:00}:{(int)player.Elapsed%60:00}     {fps:0} FPS     {(player.AllVisible ? "全可见" : player.Night ? "夜" : "昼")}");
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
        public string SkillQ, SkillW, SkillE, SkillR;
        public bool Night, AllVisible;
    }
}
