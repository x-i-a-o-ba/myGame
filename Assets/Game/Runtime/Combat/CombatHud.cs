using System;
using System.Collections.Generic;
using Roguelike.Foundation.UI;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Game HUD 展示桥：把 <see cref="CombatHudSnapshot"/> 发布为 UI 键值（只发布变化的值），
    /// 并把重开、施放、学习、天赋、指令按钮意图转发给控制器。面板布局与样式全部在 Prefab 中制作。
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        public event Action RestartRequested;
        public event Action<int> AbilityRequested;
        public event Action<int> LearnRequested;
        /// <summary>天赋按钮：值 = 层 × 2 + 选项（0 左、1 右）。</summary>
        public event Action<int> TalentRequested;
        /// <summary>指令按钮：0 攻击（进入攻击瞄准）、1 停止、2 原地待命。</summary>
        public event Action<int> OrderRequested;
        public const string ProgressionPanel="UI.Game.Progression", TalentPanel="UI.Game.Talent", DeathPanel="UI.Game.DeathOverlay";
        /// <summary>HUD 预制体提供的技能槽、每槽等级点与天赋层数量。</summary>
        public const int AbilitySlots=4, LevelPips=4, TalentRows=4;
        /// <summary>天赋树面板是否被玩家手动打开（有待选天赋时总是显示）。</summary>
        public bool TalentTreeOpen {get; private set;}
        // 0 未知、1 显示、2 隐藏；只在状态变化时调用 Show/Hide，避免每次刷新都触发面板打开事件。
        private int progressionVisible, talentVisible, deathVisible;
        private bool running, talentPending;
        private UIManager manager;
        private readonly Dictionary<string,string> lastText=new Dictionary<string,string>();
        private readonly Dictionary<string,float> lastFill=new Dictionary<string,float>();
        private readonly Dictionary<string,bool> lastToggle=new Dictionary<string,bool>();
        private readonly Dictionary<string,Texture> lastTexture=new Dictionary<string,Texture>();
        private static readonly AbilityKeys[] abilityKeys=BuildAbilityKeys();
        private static readonly TalentKeys[] talentKeys=BuildTalentKeys();
        private UIManager UI => manager ? manager : manager = GetComponent<UIManager>();
        public bool HasBindings => UI != null;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(UiCommand command)
        {
            if (command.Id == "combat.restart") RestartRequested?.Invoke();
            else if (command.Id == "combat.ability" && command.Value >= 0 && command.Value < AbilitySlots)
                AbilityRequested?.Invoke(command.Value);
            else if (command.Id == "combat.learnSkill" && command.Value >= 0 && command.Value < AbilitySlots)
                LearnRequested?.Invoke(command.Value);
            else if (command.Id == "combat.talent" && command.Value >= 0 && command.Value < TalentRows * 2)
                TalentRequested?.Invoke(command.Value);
            else if (command.Id == "combat.order" && command.Value >= 0 && command.Value <= 2)
                OrderRequested?.Invoke(command.Value);
            else if (command.Id == "combat.talentTree")
            { TalentTreeOpen = !TalentTreeOpen; UpdateTalentPanel(); }
        }
        public bool BlocksPointer(Vector2 position) => UI.Blocks(position);

        /// <summary>按快照刷新全部 HUD 键值；数值未变化的键不会重复发布。</summary>
        public void Refresh(CombatHudSnapshot s)
        {
            running = s.Outcome == CombatOutcome.Running;
            SetPanel(ProgressionPanel, running, ref progressionVisible);
            SetPanel(DeathPanel, !running, ref deathVisible);
            if (!running)
            {
                TalentTreeOpen = false;
                string clock = Clock(s.Elapsed);
                Text("combat.resultText", s.Outcome == CombatOutcome.Victory
                    ? $"胜利！击败第 {s.Wave} 波最终首领\n等级 {s.Level}  ·  击杀 {s.Kills:N0}  ·  用时 {clock}"
                    : $"倒在第 {s.Wave} 波  ·  等级 {s.Level}\n生存 {clock}  ·  击杀 {s.Kills:N0}");
            }
            // 英雄头像、等级与经验环
            Picture("hero.portrait", s.Portrait);
            Text("hero.name", s.HeroName);
            Text("hero.level", s.Level.ToString());
            Fill("hero.xp", s.ExperienceFraction);
            Text("hero.xpText", s.ExperienceText);
            Toggle("hero.pointsVisible", s.SkillPoints > 0);
            Text("hero.points", s.SkillPoints > 0 ? $"技能点 ×{s.SkillPoints}" : "");
            // 生命与法力
            Fill("hero.health", s.Health / Mathf.Max(1, s.MaximumHealth));
            Text("hero.healthText", $"{Mathf.CeilToInt(s.Health)} / {s.MaximumHealth:0}");
            Text("hero.regen", s.Regen > 0 ? $"+{s.Regen:0.0}" : "");
            Fill("hero.mana", s.MaximumMana > 0 ? s.Mana / s.MaximumMana : 0);
            Text("hero.manaText", $"{Mathf.FloorToInt(s.Mana)} / {s.MaximumMana:0}");
            // 属性面板
            Text("stats.attack", $"{s.AttackDamage:0}");
            Text("stats.attackSpeed", $"{s.AttacksPerSecond:0.0}/秒");
            Text("stats.range", $"{s.AttackRange:0}");
            Text("stats.move", $"{s.MoveSpeed:0.#}");
            // 自动攻击被动模块
            Picture("passive.icon", s.PassiveIcon);
            Text("passive.name", s.PassiveName);
            Text("passive.status", s.PassiveStatus);
            for (int i = 0; i < AbilitySlots && i < s.Abilities.Length; i++) Ability(abilityKeys[i], s.Abilities[i]);
            // 指令
            Text("order.current", "指令：" + s.Order);
            Toggle("order.attackArmed", s.AttackArmed);
            // 顶栏
            Text("top.clock", Clock(s.Elapsed));
            Text("top.wave", s.FinalWave > 0 ? $"第 {s.Wave} / {s.FinalWave} 波" : $"第 {s.Wave} 波（无尽）");
            Text("top.next", s.NextWave);
            Text("top.kills", $"{s.Kills:N0}");
            Text("top.alive", $"{s.Alive:N0}");
            Text("top.fps", $"{s.Fps:0} FPS");
            Text("top.projectiles", $"弹体 {s.Projectiles:N0}");
            Toggle("top.boss", running && s.BossAlive);
            if (s.BossAlive) { Text("top.bossName", s.BossName); Fill("top.bossFill", s.BossFraction); }
            // 提示条
            Text("progression.hint", s.Hint);
            Toggle("progression.hintVisible", !string.IsNullOrEmpty(s.Hint));
            // 天赋树
            talentPending = s.PendingTalent >= 0;
            Toggle("hud.talentPending", talentPending);
            Text("talent.title", s.TalentTitle);
            for (int i = 0; i < TalentRows && i < s.Talents.Length; i++) Talent(talentKeys[i], s.Talents[i], s.PendingTalent == i);
            UpdateTalentPanel();
        }
        private void Ability(in AbilityKeys keys, in AbilityView view)
        {
            Picture(keys.Icon, view.Icon);
            Text(keys.Name, view.Name);
            Toggle(keys.Locked, view.Level <= 0);
            Toggle(keys.Learn, running && view.Learnable);
            bool cooling = view.Level > 0 && view.Cooldown > 0;
            Toggle(keys.Cooling, cooling);
            Fill(keys.Cooldown, cooling ? view.Cooldown : 0);
            Text(keys.CooldownText, cooling ? view.CooldownText : "");
            Text(keys.Mana, view.ManaText);
            Toggle(keys.NoMana, view.NoMana);
            Text(keys.Status, view.Status);
            for (int p = 0; p < LevelPips; p++)
            {
                Toggle(keys.PipSlots[p], p < view.MaxLevel);
                Toggle(keys.Pips[p], p < view.Level);
            }
        }
        private void Talent(in TalentKeys keys, in TalentRowView row, bool pending)
        {
            Toggle(keys.Row, row.Exists);
            if (!row.Exists) return;
            Text(keys.Level, row.Level.ToString());
            Text(keys.Left, row.Left);
            Text(keys.Right, row.Right);
            Toggle(keys.LeftChosen, row.Chosen && !row.ChoseRight);
            Toggle(keys.RightChosen, row.Chosen && row.ChoseRight);
            Toggle(keys.Pending, pending);
            Toggle(keys.Locked, !row.Unlocked && !row.Chosen);
        }
        private void UpdateTalentPanel() => SetPanel(TalentPanel, running && (talentPending || TalentTreeOpen), ref talentVisible);
        private static string Clock(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
        private void Text(string key, string value)
        {
            value ??= "";
            if (lastText.TryGetValue(key, out var old) && old == value) return;
            lastText[key] = value; UI.Text(key, value);
        }
        private void Fill(string key, float value)
        {
            value = Mathf.Clamp01(float.IsFinite(value) ? value : 0);
            if (lastFill.TryGetValue(key, out var old) && Mathf.Abs(old - value) < 0.0005f) return;
            lastFill[key] = value; UI.Fill(key, value);
        }
        private void Toggle(string key, bool value)
        {
            if (lastToggle.TryGetValue(key, out var old) && old == value) return;
            lastToggle[key] = value; UI.Visible(key, value);
        }
        private void Picture(string key, Texture value)
        {
            if (lastTexture.TryGetValue(key, out var old) && old == value) return;
            lastTexture[key] = value; UI.Texture(key, value);
        }
        private void SetPanel(string id, bool visible, ref int cache)
        {
            int next = visible ? 1 : 2;
            if (cache == next) return;
            cache = next;
            if (visible) UI.Show(id); else UI.Hide(id);
        }
        private struct AbilityKeys
        {
            public string Icon, Name, Locked, Learn, Cooling, Cooldown, CooldownText, Mana, NoMana, Status;
            public string[] PipSlots, Pips;
        }
        private struct TalentKeys { public string Row, Level, Left, Right, LeftChosen, RightChosen, Pending, Locked; }
        private static AbilityKeys[] BuildAbilityKeys()
        {
            var result = new AbilityKeys[AbilitySlots];
            string[] letters = { "q", "w", "e", "r" };
            for (int i = 0; i < AbilitySlots; i++)
            {
                string p = "ability." + letters[i] + ".";
                result[i] = new AbilityKeys { Icon = p + "icon", Name = p + "name", Locked = p + "locked", Learn = p + "learn", Cooling = p + "cooling",
                    Cooldown = p + "cd", CooldownText = p + "cdText", Mana = p + "mana", NoMana = p + "noMana", Status = p + "status",
                    PipSlots = new string[LevelPips], Pips = new string[LevelPips] };
                for (int n = 0; n < LevelPips; n++) { result[i].PipSlots[n] = p + "pipSlot" + n; result[i].Pips[n] = p + "pip" + n; }
            }
            return result;
        }
        private static TalentKeys[] BuildTalentKeys()
        {
            var result = new TalentKeys[TalentRows];
            for (int i = 0; i < TalentRows; i++)
            {
                string p = "talent.t" + i + ".";
                result[i] = new TalentKeys { Row = p + "row", Level = p + "level", Left = p + "left", Right = p + "right",
                    LeftChosen = p + "leftChosen", RightChosen = p + "rightChosen", Pending = p + "pending", Locked = p + "locked" };
            }
            return result;
        }
    }

    /// <summary>HUD 所需的展示快照；由控制器每 0.1 秒填充并复用，不暴露 ECS 实体或 World。</summary>
    public sealed class CombatHudSnapshot
    {
        public CombatOutcome Outcome;
        public string HeroName; public Texture Portrait;
        public int Level, SkillPoints;
        public float ExperienceFraction; public string ExperienceText;
        public float Health, MaximumHealth, Regen, Mana, MaximumMana;
        public float AttackDamage, AttacksPerSecond, AttackRange, MoveSpeed;
        public string PassiveName, PassiveStatus; public Texture PassiveIcon;
        public readonly AbilityView[] Abilities = new AbilityView[CombatHud.AbilitySlots];
        public string Order; public bool AttackArmed, Aiming;
        public float Elapsed, Fps; public int Wave, FinalWave, Kills, Alive, Projectiles;
        public string NextWave;
        public bool BossAlive; public string BossName; public float BossFraction;
        public string Hint;
        /// <summary>当前待选天赋层下标；-1 表示没有待选天赋。</summary>
        public int PendingTalent = -1;
        public string TalentTitle;
        public readonly TalentRowView[] Talents = new TalentRowView[CombatHud.TalentRows];
    }

    /// <summary>一个技能槽的展示状态；Cooldown 为剩余冷却比例（1 = 刚进入冷却）。</summary>
    public struct AbilityView
    {
        public string Key, Name, Status, CooldownText, ManaText;
        public Texture Icon;
        public int Level, MaxLevel;
        public float Cooldown;
        public bool Learnable, NoMana;
    }

    /// <summary>天赋树一层的展示状态。</summary>
    public struct TalentRowView
    {
        public bool Exists, Unlocked, Chosen, ChoseRight;
        public int Level;
        public string Left, Right;
    }
}
