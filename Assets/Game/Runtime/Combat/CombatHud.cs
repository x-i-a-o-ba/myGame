using System;
using System.Collections.Generic;
using Roguelike.Foundation.UI;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>发布战斗快照并转发技能学习/施放意图；营地和暂停归远征界面。</summary>
    public sealed class CombatHud : MonoBehaviour
    {
        public event Action<int> AbilityRequested,LearnRequested;
        public event Action HoverChanged;
        public const string TooltipPanel="UI.Game.AbilityTooltip",HoverCommand="combat.hover";
        public const int PassiveSlot=4,EquipmentSlots=6,EquipmentHoverBase=10,AbilitySlots=4,LevelPips=4;
        public int HoveredSlot {get;private set;}=-1;
        private bool running;private int tooltipVisible;private UIManager manager;
        private readonly Dictionary<string,string> lastText=new Dictionary<string,string>();
        private readonly Dictionary<string,float> lastFill=new Dictionary<string,float>();
        private readonly Dictionary<string,bool> lastToggle=new Dictionary<string,bool>();
        private readonly Dictionary<string,Texture> lastTexture=new Dictionary<string,Texture>();
        private static readonly AbilityKeys[] abilityKeys=BuildAbilityKeys();
        private UIManager UI=>manager?manager:manager=GetComponent<UIManager>();
        public bool HasBindings=>UI!=null;
        private void OnEnable() {if(UI) UI.Events.Command+=Receive;}
        private void OnDisable() {if(UI) UI.Events.Command-=Receive;}
        private void Receive(UiCommand command)
        {
            if(command.Id=="combat.ability" && command.Value>=0 && command.Value<4) AbilityRequested?.Invoke(command.Value);
            else if(command.Id=="combat.learnSkill" && command.Value>=0 && command.Value<4) LearnRequested?.Invoke(command.Value);
            else if(command.Id==HoverCommand)
            {
                int next=command.Value;
                if(next<0) {if(UIHoverAction.ExitValue(HoveredSlot)!=next) return;next=-1;}
                else if(!IsHoverValue(next)) return;
                HoveredSlot=next;if(next<0) SetPanel(TooltipPanel,false,ref tooltipVisible);HoverChanged?.Invoke();
            }
        }
        public bool BlocksPointer(Vector2 position)=>UI.Blocks(position);
        public static bool IsHoverValue(int value)=>(value>=0 && value<=PassiveSlot)||(value>=EquipmentHoverBase && value<EquipmentHoverBase+EquipmentSlots);
        public void Refresh(CombatHudSnapshot s)
        {
            running = s.Outcome == CombatOutcome.Running;
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
            Text("hero.manaRegen", $"{(s.ManaRegen>=0?"+":"")}{s.ManaRegen:0.0}/秒");
            Text("stats.attack",$"{s.AttackDamage:0}");Text("stats.attackSpeed",$"{s.AttacksPerSecond:0.0}/秒");
            Text("stats.move",$"{s.MoveSpeed:0.#}");Text("stats.reduction",$"{s.DamageReduction:0.#}%");
            // 金币、装备栏与商店（商店按钮在商店范围外变暗并显示距离）
            Text(EquipmentKeys.Gold, $"{Mathf.FloorToInt(Mathf.Max(0, s.Gold)):N0}");
            for (int i = 0; i < EquipmentSlots && i < s.Equipment.Length; i++) Equip(i, s.Equipment[i]);
            // 自动攻击被动模块
            Picture("passive.icon", s.PassiveIcon);
            Text("passive.name", s.PassiveName);
            Text("passive.status", s.PassiveStatus);
            for (int i = 0; i < AbilitySlots && i < s.Abilities.Length; i++) Ability(abilityKeys[i], s.Abilities[i]);
            // 顶栏
            Text("top.clock", Clock(s.Elapsed));
            Text("top.wave", $"第 {s.Stage} 关 · 持续战斗");
            Text("top.next", s.SpawnStatus);
            Text("top.kills", $"{s.Kills:N0}");
            Text("top.alive", $"{s.Alive:N0}");
            Text("top.fps", $"{s.Fps:0} FPS");
            Text("top.projectiles", $"弹体 {s.Projectiles:N0}");
            Text("top.map", string.IsNullOrEmpty(s.MapName) ? "" : "战场 · " + s.MapName);
            Toggle("top.boss", running && s.BossAlive);
            if (s.BossAlive) { Text("top.bossName", s.BossName); Fill("top.bossFill", s.BossFraction); }
            // 提示条
            Text("progression.hint", s.Hint);
            Toggle("progression.hintVisible", !string.IsNullOrEmpty(s.Hint));
            bool showTooltip = running && HoveredSlot >= 0 && s.Tooltip.Visible;
            if (showTooltip) Tooltip(s.Tooltip);
            SetPanel(TooltipPanel, showTooltip, ref tooltipVisible);
            if (showTooltip)
            {
                // 文字刚写入，立即重新排版与定位，避免显示上一帧的高度。
                var panel = UI.FindPanel(TooltipPanel);
                var layout = panel ? panel.GetComponent<UITooltip>() : null;
                if (layout) layout.Refresh();
            }
        }
        private void Equip(int slot, in EquipmentSlotView view)
        {
            Toggle(EquipmentKeys.Locked[slot], !view.Exists);
            Toggle(EquipmentKeys.Filled[slot], view.Filled);
            Picture(EquipmentKeys.Icons[slot], view.Filled ? view.Icon : null);
            Toggle(EquipmentKeys.HasIcon[slot], view.Filled && view.Icon != null);
            Text(EquipmentKeys.Names[slot], view.Filled ? view.Name : "");
        }
        private void Tooltip(AbilityTooltipView t)
        {
            Picture(TooltipKeys.Icon, t.Icon);
            Text(TooltipKeys.Name, t.Name);
            Text(TooltipKeys.Hotkey, t.Hotkey);
            Toggle(TooltipKeys.HasHotkey, !string.IsNullOrEmpty(t.Hotkey));
            Text(TooltipKeys.Kind, t.Kind);
            Text(TooltipKeys.Level, t.LevelText);
            Toggle(TooltipKeys.HasLevel, !t.Passive && !t.NoLevel);
            Toggle(TooltipKeys.Passive, t.Passive);
            Text(TooltipKeys.Properties, t.Properties);
            Section(TooltipKeys.Description, TooltipKeys.HasDescription, t.Description);
            Section(TooltipKeys.Values, TooltipKeys.HasValues, t.Values);
            Section(TooltipKeys.Notes, TooltipKeys.HasNotes, t.Notes);
            Section(TooltipKeys.Status, TooltipKeys.HasStatus, t.Status);
            Section(TooltipKeys.Cooldown, TooltipKeys.HasCooldown, t.Cooldown);
            Section(TooltipKeys.Mana, TooltipKeys.HasMana, t.Mana);
            Toggle(TooltipKeys.HasCost, !string.IsNullOrEmpty(t.Cooldown) || !string.IsNullOrEmpty(t.Mana));
            for (int p = 0; p < LevelPips; p++)
            {
                Toggle(TooltipKeys.PipSlots[p], !t.Passive && !t.NoLevel && p < t.MaxLevel);
                Toggle(TooltipKeys.Pips[p], !t.Passive && !t.NoLevel && p < t.Level);
            }
        }
        private void Section(string textKey, string toggleKey, string value)
        {
            bool shown = !string.IsNullOrEmpty(value);
            Text(textKey, value);
            Toggle(toggleKey, shown);
        }
        private void Ability(in AbilityKeys keys, in AbilityView view)
        {
            Picture(keys.Icon, view.Icon);
            Text(keys.Name, view.Name);Text(keys.Key,view.Key);
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
        public static class TooltipKeys
        {
            public const string Name = "tooltip.name", Hotkey = "tooltip.hotkey", Kind = "tooltip.kind", Level = "tooltip.level",
                Properties = "tooltip.properties", Description = "tooltip.description", Values = "tooltip.values", Notes = "tooltip.notes",
                Cooldown = "tooltip.cooldown", Mana = "tooltip.mana", Status = "tooltip.status";
            public const string Icon = "tooltip.icon";
            public const string HasHotkey = "tooltip.hasHotkey", HasLevel = "tooltip.hasLevel", Passive = "tooltip.passive",
                HasDescription = "tooltip.hasDescription", HasValues = "tooltip.hasValues", HasNotes = "tooltip.hasNotes",
                HasCost = "tooltip.hasCost", HasCooldown = "tooltip.hasCooldown", HasMana = "tooltip.hasMana", HasStatus = "tooltip.hasStatus";
            public static readonly string[] PipSlots = Numbered("tooltip.pipSlot", LevelPips);
            public static readonly string[] Pips = Numbered("tooltip.pip", LevelPips);
            public static readonly string[] Texts = { Name, Hotkey, Kind, Level, Properties, Description, Values, Notes, Cooldown, Mana, Status };
            public static readonly string[] Images = { Icon };
            public static readonly string[] Toggles = Join(Join(new[] { HasHotkey, HasLevel, Passive, HasDescription, HasValues, HasNotes,
                HasCost, HasCooldown, HasMana, HasStatus }, PipSlots), Pips);
            private static string[] Numbered(string prefix, int count)
            {
                var result = new string[count];
                for (int i = 0; i < count; i++) result[i] = prefix + i;
                return result;
            }
        }
        public static class EquipmentKeys
        {
            public const string Gold = "hero.gold";
            public static readonly string[] Icons = Numbered("equip.s", ".icon", EquipmentSlots), Filled = Numbered("equip.s", ".filled", EquipmentSlots),
                HasIcon = Numbered("equip.s", ".hasIcon", EquipmentSlots), Names = Numbered("equip.s", ".name", EquipmentSlots),
                Locked = Numbered("equip.s", ".locked", EquipmentSlots), Drop = Numbered("equip.s", ".drop", EquipmentSlots);
            public static readonly string[] Texts = Join(new[] { Gold }, Names);
            public static readonly string[] Images = Icons;
            public static readonly string[] Toggles = Join(Join(Join(Filled, HasIcon), Locked), Drop);
        }
        private static string[] Numbered(string prefix, string suffix, int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++) result[i] = prefix + i + suffix;
            return result;
        }
        private struct AbilityKeys
        {
            public string Icon, Name, Key, Locked, Learn, Cooling, Cooldown, CooldownText, Mana, NoMana, Status;
            public string[] PipSlots, Pips;
        }
        private static AbilityKeys[] BuildAbilityKeys()
        {
            var result = new AbilityKeys[AbilitySlots];
            string[] letters = { "q", "w", "e", "r" };
            for (int i = 0; i < AbilitySlots; i++)
            {
                string p = "ability." + letters[i] + ".";
                result[i] = new AbilityKeys { Icon = p + "icon", Name = p + "name", Key=p+"key", Locked = p + "locked", Learn = p + "learn", Cooling = p + "cooling",
                    Cooldown = p + "cd", CooldownText = p + "cdText", Mana = p + "mana", NoMana = p + "noMana", Status = p + "status",
                    PipSlots = new string[LevelPips], Pips = new string[LevelPips] };
                for (int n = 0; n < LevelPips; n++) { result[i].PipSlots[n] = p + "pipSlot" + n; result[i].Pips[n] = p + "pip" + n; }
            }
            return result;
        }
        private static string[] Join(string[] a,string[] b) {var result=new string[a.Length+b.Length];a.CopyTo(result,0);b.CopyTo(result,a.Length);return result;}
    }
    public sealed class CombatHudSnapshot
    {
        public CombatOutcome Outcome;
        public string HeroName; public Texture Portrait;
        /// <summary>当前战场名称；场景没有战场定义时为空。</summary>
        public string MapName;
        public int Level, SkillPoints;
        public float ExperienceFraction; public string ExperienceText;
        public float Health, MaximumHealth, Regen, Mana, MaximumMana, ManaRegen;
        public float AttackDamage, AttacksPerSecond, AttackRange, MoveSpeed;
        /// <summary>技能强度、冷却缩减与伤害减免（已夹到上限），单位为百分点。</summary>
        public float SkillAmplify, CooldownReduction, DamageReduction;
        /// <summary>当前金币（可带小数，界面向下取整）与装备栏各格。</summary>
        public float Gold;
        public readonly EquipmentSlotView[] Equipment = new EquipmentSlotView[CombatHud.EquipmentSlots];
        public string PassiveName, PassiveStatus; public Texture PassiveIcon;
        public readonly AbilityView[] Abilities = new AbilityView[CombatHud.AbilitySlots];
        public float Elapsed, Fps; public int Stage, Kills, Alive, Projectiles;
        public string SpawnStatus;
        public bool BossAlive; public string BossName; public float BossFraction;
        public string Hint;
        /// <summary>技能、普攻或装备的当前信息。</summary>
        public readonly AbilityTooltipView Tooltip = new AbilityTooltipView();
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

    /// <summary>装备栏一格的展示状态：Exists 表示装备库配置了这一格，Filled 表示格中有装备。</summary>
    public struct EquipmentSlotView
    {
        public bool Exists, Filled;
        public Texture Icon;
        public string Name;
    }

}
