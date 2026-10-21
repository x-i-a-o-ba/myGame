using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Combat
{
    /// <summary>Game HUD 展示快照，并转发重开、技能、学习技能与天赋按钮意图。</summary>
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
        public event Action<int> AbilityRequested;
        public event Action<int> LearnRequested;
        public event Action<int> TalentRequested;
        public const string ProgressionPanel="UI.Game.Progression", TalentPanel="UI.Game.Talent";
        // 0 未知、1 显示、2 隐藏；只在状态变化时调用 Show/Hide，避免每次刷新都触发面板打开事件。
        private int progressionVisible, talentVisible;
        private Roguelike.Foundation.UI.UIManager UI => GetComponent<Roguelike.Foundation.UI.UIManager>();
        public bool HasBindings => UI != null;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(Roguelike.Foundation.UI.UiCommand command)
        {
            if (command.Id == "combat.restart") RestartRequested?.Invoke();
            else if (command.Id == "combat.ability" && command.Value >= 0 && command.Value <= 3)
                AbilityRequested?.Invoke(command.Value);
            else if (command.Id == "combat.learnSkill" && command.Value >= 0 && command.Value <= 3)
                LearnRequested?.Invoke(command.Value);
            else if (command.Id == "combat.talent" && (command.Value == 0 || command.Value == 1))
                TalentRequested?.Invoke(command.Value);
        }
        public bool BlocksPointer(Vector2 position) => UI.Blocks(position);
        public void Refresh(CombatHudState player,float fps,float range)
        {
            UI.Fill("combat.healthFill",player.Health/Mathf.Max(1,player.MaximumHealth));
            UI.Text("combat.healthText",$"生命  {Mathf.CeilToInt(player.Health)} / {player.MaximumHealth:0}");
            UI.Text("combat.detailText",$"自动攻击最近敌人 · 射程 {range:0}m\n右键移动 / QWER 施放 / Ctrl+QWER 学习 / 左键确认{(player.Aiming ? " / 右键或 Esc 取消" : "")}");
            UI.Text("combat.skillQ",player.SkillQ);
            UI.Text("combat.skillW",player.SkillW);
            UI.Text("combat.skillE",player.SkillE);
            UI.Text("combat.skillR",player.SkillR);
            UI.Text("combat.clockText",$"{(int)player.Elapsed/60:00}:{(int)player.Elapsed%60:00}     {fps:0} FPS");
            UI.Text("combat.counterText",$"存活敌人   {player.AliveEnemies:N0}\n击杀   {player.Kills:N0}       弹体   {player.ActiveProjectiles}\n滚轮拉远可查看整个敌群");
            if(player.Victory)
            {
                UI.Text("combat.resultText",$"胜利！击败第 {player.Wave} 波最终首领\n等级 {player.Level}  ·  击杀 {player.Kills:N0}  ·  用时 {(int)player.Elapsed/60:00}:{(int)player.Elapsed%60:00}");
                UI.Show("UI.Game.DeathOverlay");
            }
            else if(player.Health<=0)
            {
                UI.Text("combat.resultText",$"倒在第 {player.Wave} 波  ·  等级 {player.Level}\n生存 {player.Elapsed:0.0} 秒  ·  击杀 {player.Kills:N0}");
                UI.Show("UI.Game.DeathOverlay");
            }
            else UI.Hide("UI.Game.DeathOverlay");
        }

        /// <summary>等级、经验、技能点、波次与天赋快照；面板只在战斗进行中显示。</summary>
        public void RefreshProgression(in CombatProgressionHudState state)
        {
            bool running = state.Outcome == CombatOutcome.Running;
            SetVisible(ProgressionPanel, running, ref progressionVisible);
            SetVisible(TalentPanel, running && state.TalentPending, ref talentVisible);
            if (!running) return;
            UI.Text("progression.level", $"Lv {state.Level}");
            UI.Fill("progression.expFill", state.ExperienceFraction);
            UI.Text("progression.expText", state.ExperienceText);
            UI.Text("progression.points", $"技能点 {state.SkillPoints}");
            UI.Text("progression.wave", state.WaveText);
            UI.Text("progression.hint", state.Hint);
            UI.Text("progression.learnQ", state.LearnQ);
            UI.Text("progression.learnW", state.LearnW);
            UI.Text("progression.learnE", state.LearnE);
            UI.Text("progression.learnR", state.LearnR);
            if (!state.TalentPending) return;
            UI.Text("talent.title", state.TalentTitle);
            UI.Text("talent.left", state.TalentLeft);
            UI.Text("talent.right", state.TalentRight);
        }
        private void SetVisible(string id, bool visible, ref int cache)
        {
            int next = visible ? 1 : 2;
            if (cache == next) return;
            cache = next;
            if (visible) UI.Show(id); else UI.Hide(id);
        }

    }

    /// <summary>UI 所需的小型展示快照；不暴露 ECS 实体或 World。</summary>
    public struct CombatHudState
    {
        public float Health, MaximumHealth, Elapsed;
        public int AliveEnemies, Kills, ActiveProjectiles;
        public string SkillQ, SkillW, SkillE, SkillR;
        public bool Aiming;
        public bool Victory;
        public int Wave, Level;
    }

    /// <summary>成长面板与天赋面板的展示快照。</summary>
    public struct CombatProgressionHudState
    {
        public CombatOutcome Outcome;
        public int Level, SkillPoints;
        public float ExperienceFraction;
        public string ExperienceText, WaveText, Hint;
        public string LearnQ, LearnW, LearnE, LearnR;
        public bool TalentPending;
        public string TalentTitle, TalentLeft, TalentRight;
    }
}

