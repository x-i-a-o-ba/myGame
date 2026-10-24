using System;
using System.Collections.Generic;
using Roguelike.Foundation.UI;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Game HUD 展示桥：把 <see cref="CombatHudSnapshot"/> 发布为 UI 键值（只发布变化的值），
    /// 并把重开、施放、学习、天赋、指令、开商店、购买、出售与装备拖动（换位、丢弃到地面、拖到商店出售）意图转发给控制器。
    /// 面板布局与样式全部在 Prefab 中制作；地上装备的提示框锚点由本组件在画布下创建。
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
        /// <summary>选中目标面板的关闭按钮（×）。</summary>
        public event Action TargetCloseRequested;
        /// <summary>悬停的 HUD 槽变化（进入或离开技能 / 被动图标、装备格、属性面板、商店卡片）；控制器据此立即刷新提示框。</summary>
        public event Action HoverChanged;
        /// <summary>商店卡片左键购买：值 = 装备库下标（当前页 × <see cref="ShopCards"/> + 卡片序号）。</summary>
        public event Action<int> BuyRequested;
        /// <summary>装备栏右键出售或把装备拖到商店面板：值 = 装备栏格（0–5）。</summary>
        public event Action<int> SellRequested;
        /// <summary>商店打开、关闭或翻页；控制器据此立即刷新 HUD。</summary>
        public event Action ShopChanged;
        /// <summary>商店按钮在商店关闭时被点击：由控制器判断英雄是否在商店范围内再调用 <see cref="ToggleShop"/>；没有订阅者时直接打开。</summary>
        public event Action ShopRequested;
        /// <summary>把装备拖到装备栏另一格松开：值 = (起始格, 目标格)；目标格有装备时两格交换。</summary>
        public event Action<int,int> MoveRequested;
        /// <summary>把装备拖出 HUD 松开（Dota 2 式丢弃到地面）：值 = (装备栏格, 松开时的屏幕坐标)。</summary>
        public event Action<int,Vector2> DropRequested;
        public const string ProgressionPanel="UI.Game.Progression", TalentPanel="UI.Game.Talent", DeathPanel="UI.Game.DeathOverlay";
        /// <summary>左键选中敌人后在左上角显示的目标信息面板。</summary>
        public const string TargetPanel="UI.Game.TargetInfo";
        /// <summary>鼠标悬停技能或被动图标时显示的 Dota 2 式详细提示框。</summary>
        public const string TooltipPanel="UI.Game.AbilityTooltip";
        /// <summary>按 B 或点击商店按钮打开的商店面板。</summary>
        public const string ShopPanel="UI.Game.Shop";
        /// <summary>
        /// 悬停命令：值 0–3 为 Q/W/E/R，<see cref="PassiveSlot"/> 为自动攻击被动，<see cref="EquipmentHoverBase"/>+格为装备栏，
        /// <see cref="StatsHover"/> 为英雄头像总览，<see cref="ShopHoverBase"/>+卡片为商店卡片；离开时为 -1 - 值。
        /// </summary>
        public const string HoverCommand="combat.hover";
        public const int PassiveSlot=4;
        /// <summary>商店与装备命令：商店开关、购买（值 = 当前页卡片序号）、翻页（0 上一页、1 下一页）、出售（值 = 装备栏格，右键）。</summary>
        public const string ShopCommand="combat.shop", BuyCommand="combat.shop.buy", PageCommand="combat.shop.page", SellCommand="combat.equip.sell";
        /// <summary>
        /// 装备拖动命令：开始拖动（值 = 格）、松开（值 = 格，附带屏幕坐标）、放到装备栏某格（值 = 目标格）、放到商店面板（出售）。
        /// 放置命令先于松开命令到达；松开时没有被放置目标接住且不在 HUD 上，就是丢弃到地面。
        /// </summary>
        public const string DragCommand="combat.equip.drag", DragEndCommand="combat.equip.dragEnd", DropOnCommand="combat.equip.dropOn", ShopDropCommand="combat.shop.drop";
        /// <summary>HUD 预制体提供的装备栏格数与商店每页卡片数；装备栏、属性面板与商店卡片的悬停值。</summary>
        public const int EquipmentSlots=6, ShopCards=12, EquipmentHoverBase=10, StatsHover=20, StatHoverBase=30, StatHoverCount=15, ShopHoverBase=100;
        /// <summary>HUD 预制体提供的技能槽、每槽等级点与天赋层数量。</summary>
        public const int AbilitySlots=4, LevelPips=4, TalentRows=4;
        /// <summary>目标面板的状态效果标签数量。</summary>
        public const int TargetStatusSlots=4;
        /// <summary>天赋树面板是否被玩家手动打开（有待选天赋时总是显示）。</summary>
        public bool TalentTreeOpen {get; private set;}
        /// <summary>当前悬停的 HUD 槽（取值见 <see cref="IsHoverValue"/>），-1 表示没有悬停。</summary>
        public int HoveredSlot {get; private set;}=-1;
        /// <summary>商店面板是否打开（B 键或商店按钮切换，Esc 关闭；战斗结束时自动关闭）。</summary>
        public bool ShopOpen {get; private set;}
        /// <summary>商店当前页（从 0 开始），按快照的总页数夹紧。</summary>
        public int ShopPage {get; private set;}
        /// <summary>正在拖动的装备栏格；没有拖动时为 -1。拖动期间隐藏提示框。</summary>
        public int DraggingSlot {get; private set;}=-1;
        /// <summary>是否正在显示地上装备的提示框（由控制器每帧定位锚点）。</summary>
        public bool GroundTooltipActive {get; private set;}
        /// <summary>地上装备提示框的锚点（画布下的空矩形）；尚未显示过时为 null。</summary>
        public RectTransform GroundAnchor => groundAnchor;
        private RectTransform groundAnchor;
        private bool dropHandled;
        // 0 未知、1 显示、2 隐藏；只在状态变化时调用 Show/Hide，避免每次刷新都触发面板打开事件。
        private int progressionVisible, talentVisible, deathVisible, targetVisible, tooltipVisible, shopVisible;
        private int shopPages = 1;
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
            else if (command.Id == TargetKeys.CloseCommand) TargetCloseRequested?.Invoke();
            else if (command.Id == HoverCommand) Hover(command.Value);
            else if (command.Id == ShopCommand) { if (ShopOpen || ShopRequested == null) ToggleShop(); else ShopRequested.Invoke(); }
            else if (command.Id == BuyCommand && command.Value >= 0 && command.Value < ShopCards)
                BuyRequested?.Invoke(ShopPage * ShopCards + command.Value);
            else if (command.Id == PageCommand) TurnPage(command.Value == 0 ? -1 : 1);
            else if (command.Id == SellCommand && command.Value >= 0 && command.Value < EquipmentSlots)
                SellRequested?.Invoke(command.Value);
            else if (command.Id == DragCommand && command.Value >= 0 && command.Value < EquipmentSlots) BeginDrag(command.Value);
            else if (command.Id == DropOnCommand && command.Value >= 0 && command.Value < EquipmentSlots) DropOnSlot(command.Value);
            else if (command.Id == ShopDropCommand) DropOnShop();
            else if (command.Id == DragEndCommand) EndDrag(command.Value, command.Pointer);
            else if (command.Id == "combat.talentTree")
            { TalentTreeOpen = !TalentTreeOpen; UpdateTalentPanel(); }
        }
        public bool BlocksPointer(Vector2 position) => UI.Blocks(position);
        /// <summary>HUD 识别的悬停值：0–3 技能、4 被动、10–15 装备栏、20 头像总览、30–44 单项属性、100–111 商店卡片。</summary>
        public static bool IsHoverValue(int value)
            => (value >= 0 && value <= PassiveSlot) || (value >= EquipmentHoverBase && value < EquipmentHoverBase + EquipmentSlots) ||
               value == StatsHover || IsStatHover(value) || (value >= ShopHoverBase && value < ShopHoverBase + ShopCards);
        public static bool IsStatHover(int value)=>value>=StatHoverBase && value<StatHoverBase+StatHoverCount;
        /// <summary>打开或关闭商店；战斗未进行时不能打开。</summary>
        public void ToggleShop()
        {
            if (!ShopOpen && !running) return;
            ShopOpen = !ShopOpen;
            SetPanel(ShopPanel, ShopOpen, ref shopVisible);
            Toggle(ShopKeys.Open, ShopOpen);
            ShopChanged?.Invoke();
        }
        /// <summary>关闭商店；返回商店原本是否打开（Esc 据此决定是否继续取消选中目标）。</summary>
        public bool CloseShop()
        {
            if (!ShopOpen) return false;
            ShopOpen = false;
            SetPanel(ShopPanel, false, ref shopVisible);
            Toggle(ShopKeys.Open, false);
            ShopChanged?.Invoke();
            return true;
        }
        private void TurnPage(int delta)
        {
            int next = Mathf.Clamp(ShopPage + delta, 0, Mathf.Max(0, shopPages - 1));
            if (next == ShopPage) return;
            ShopPage = next;
            ShopChanged?.Invoke();
        }
        private void Hover(int value)
        {
            int next = HoveredSlot;
            if (IsHoverValue(value)) next = value;
            else if (value < 0 && UIHoverAction.ExitValue(HoveredSlot) == value) next = -1;
            if (next == HoveredSlot) return;
            HoveredSlot = next;
            // 离开时立即隐藏；进入时等控制器下一次刷新填好内容再显示，避免闪出上一个技能的文字。
            // 正在查看地上的装备时，离开 HUD 槽后把提示框锚点交还给地上的装备。
            if (next < 0)
            {
                SetPanel(TooltipPanel, false, ref tooltipVisible);
                if (GroundTooltipActive && groundAnchor) UI.SetHover(groundAnchor, true);
            }
            HoverChanged?.Invoke();
        }
        // ---------- 装备拖动（Dota 2 式：拖到其他格换位、拖到地面丢弃、拖到商店出售） ----------
        private void BeginDrag(int slot)
        {
            DraggingSlot = slot; dropHandled = false;
            SetPanel(TooltipPanel, false, ref tooltipVisible);
            HoverChanged?.Invoke();
        }
        private void DropOnSlot(int target)
        {
            if (DraggingSlot < 0 || dropHandled) return;
            dropHandled = true;
            if (target != DraggingSlot) MoveRequested?.Invoke(DraggingSlot, target);
        }
        private void DropOnShop()
        {
            if (DraggingSlot < 0 || dropHandled) return;
            dropHandled = true;
            if (ShopOpen) SellRequested?.Invoke(DraggingSlot);
        }
        private void EndDrag(int slot, Vector2 pointer)
        {
            int from = DraggingSlot; bool handled = dropHandled;
            DraggingSlot = -1; dropHandled = false;
            if (from < 0 || from != slot) return;
            // 落在 HUD 其他位置（没有放置目标）时取消；落在场景中时丢弃到松开位置。
            if (!handled && running && !BlocksPointer(pointer)) DropRequested?.Invoke(from, pointer);
            HoverChanged?.Invoke();
        }
        // ---------- 地上装备的提示框 ----------
        /// <summary>
        /// 把地上装备的提示框锚点放到屏幕位置 screen（像素，图标牌中心），size 为图标牌在屏幕上的边长（像素）。
        /// 提示框内容来自快照的 Tooltip（没有悬停 HUD 槽时）；悬停 HUD 槽时让位给 HUD 槽，离开后自动恢复。
        /// </summary>
        public void ShowGroundTooltip(Vector2 screen, float size)
        {
            var anchor = EnsureGroundAnchor();
            if (!anchor) return;
            var canvas = UI.GetComponent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)UI.transform, screen, camera, out var world)) anchor.position = world;
            float scale = canvas ? Mathf.Max(0.01f, canvas.scaleFactor) : 1;
            anchor.sizeDelta = Vector2.one * Mathf.Max(8, size / scale);
            bool changed = !GroundTooltipActive;
            GroundTooltipActive = true;
            if (HoveredSlot < 0) UI.SetHover(anchor, true);
            if (changed) HoverChanged?.Invoke();
        }
        /// <summary>隐藏地上装备的提示框（取消查看、装备被拾取或不在屏幕内）。</summary>
        public void HideGroundTooltip()
        {
            if (!GroundTooltipActive) return;
            GroundTooltipActive = false;
            if (groundAnchor) UI.SetHover(groundAnchor, false);
            if (HoveredSlot < 0) SetPanel(TooltipPanel, false, ref tooltipVisible);
            HoverChanged?.Invoke();
        }
        private RectTransform EnsureGroundAnchor()
        {
            if (groundAnchor) return groundAnchor;
            if (!UI) return null;
            var anchor = new GameObject("Ground Tooltip Anchor", typeof(RectTransform));
            groundAnchor = (RectTransform)anchor.transform;
            groundAnchor.SetParent(UI.transform, false);
            groundAnchor.anchorMin = groundAnchor.anchorMax = groundAnchor.pivot = new Vector2(0.5f, 0.5f);
            return groundAnchor;
        }

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
                    ? $"本关完成！击败第 {s.Stage} 关首领\n等级 {s.Level}  ·  击杀 {s.Kills:N0}  ·  用时 {clock}"
                    : $"倒在第 {s.Stage} 关  ·  等级 {s.Level}\n生存 {clock}  ·  击杀 {s.Kills:N0}");
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
            Text("hero.manaRegen", $"{(s.ManaRegen>=0?"+":"")}{s.ManaRegen:0.0}/秒");
            // 单项属性悬停展示来源；英雄头像显示完整总览
            Text(StatsKeys.Attack, $"{s.AttackDamage:0}");
            Text(StatsKeys.AttackSpeed, $"{s.AttacksPerSecond:0.0}/秒");
            Text(StatsKeys.Range, $"{s.AttackRange:0.#}");
            Text(StatsKeys.Move, $"{s.MoveSpeed:0.#}");
            Text(StatsKeys.Amplify, Percent(s.SkillAmplify));
            Text(StatsKeys.Cooldown, Percent(s.CooldownReduction));
            Text(StatsKeys.Reduction, Percent(s.DamageReduction));
            Text(StatsKeys.Critical, Percent(s.CriticalChance));
            // 金币、装备栏与商店（商店按钮在商店范围外变暗并显示距离）
            Text(EquipmentKeys.Gold, $"{Mathf.FloorToInt(Mathf.Max(0, s.Gold)):N0}");
            for (int i = 0; i < EquipmentSlots && i < s.Equipment.Length; i++) Equip(i, s.Equipment[i]);
            if (!running && ShopOpen) CloseShop();
            if (!running) HideGroundTooltip();
            if (DraggingSlot >= 0 && !UIDragAction.Current) { DraggingSlot = -1; dropHandled = false; }
            bool far = running && s.ShopLimited && !s.ShopInRange;
            Toggle(ShopKeys.Far, far);
            Text(ShopKeys.Distance, far ? $"{Mathf.CeilToInt(s.ShopDistance)} 米" : "");
            Shop(s);
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
            // 天赋树
            talentPending = s.PendingTalent >= 0;
            Toggle("hud.talentPending", talentPending);
            Text("talent.title", s.TalentTitle);
            for (int i = 0; i < TalentRows && i < s.Talents.Length; i++) Talent(talentKeys[i], s.Talents[i], s.PendingTalent == i);
            UpdateTalentPanel();
            // 选中目标：先打开面板再发布键值（UiEvents 会缓存并在面板启用时回放）。
            bool showTarget = running && s.Target.Visible;
            SetPanel(TargetPanel, showTarget, ref targetVisible);
            if (showTarget) Target(s.Target);
            bool showTooltip = running && DraggingSlot < 0 && (HoveredSlot >= 0 || GroundTooltipActive) && s.Tooltip.Visible;
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
            // 拖动装备经过另一格时高亮该格（松开即换位）。
            Toggle(EquipmentKeys.Drop[slot], DraggingSlot >= 0 && slot != DraggingSlot && view.Exists && HoveredSlot == EquipmentHoverBase + slot);
            Toggle(EquipmentKeys.Filled[slot], view.Filled);
            Picture(EquipmentKeys.Icons[slot], view.Filled ? view.Icon : null);
            Toggle(EquipmentKeys.HasIcon[slot], view.Filled && view.Icon != null);
            Text(EquipmentKeys.Names[slot], view.Filled ? view.Name : "");
        }
        private void Shop(CombatHudSnapshot s)
        {
            shopPages = Mathf.Max(1, s.ShopPages);
            ShopPage = Mathf.Clamp(ShopPage, 0, shopPages - 1);
            Toggle(ShopKeys.Open, ShopOpen);
            SetPanel(ShopPanel, running && ShopOpen, ref shopVisible);
            Text(ShopKeys.Hint, s.ShopHint);
            Toggle(ShopKeys.Paged, shopPages > 1);
            Text(ShopKeys.Page, $"{s.ShopPage + 1} / {shopPages}");
            for (int c = 0; c < ShopCards && c < s.Shop.Length; c++)
            {
                var card = s.Shop[c];
                Toggle(ShopKeys.Visible[c], card.Visible);
                if (!card.Visible) continue;
                Picture(ShopKeys.Icons[c], card.Icon);
                Toggle(ShopKeys.HasIcon[c], card.Icon != null);
                Text(ShopKeys.Names[c], card.Name);
                Text(ShopKeys.Prices[c], $"{card.Price:N0}");
                Toggle(ShopKeys.Poor[c], card.CannotAfford);
                Toggle(ShopKeys.Owned[c], card.Owned);
            }
        }
        private static string Percent(float value) => $"{value:0.#}%";
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
        private void Target(TargetInfoView t)
        {
            Picture(TargetKeys.Portrait, t.Portrait);
            Picture(TargetKeys.AttackIcon, t.AttackIcon);
            Text(TargetKeys.Name, t.Name);
            Text(TargetKeys.Tier, t.TierLabel);
            Toggle(TargetKeys.TierNormal, t.Tier == EnemyTier.Normal);
            Toggle(TargetKeys.TierElite, t.Tier == EnemyTier.Elite);
            Toggle(TargetKeys.TierBoss, t.Tier == EnemyTier.Boss);
            Text(TargetKeys.Subtitle, t.Subtitle);
            Fill(TargetKeys.Health, t.HealthFraction);
            Text(TargetKeys.HealthText, t.HealthText);
            Toggle(TargetKeys.InRange, t.InHeroRange);
            Text(TargetKeys.Damage, t.Damage);
            Text(TargetKeys.AttackRate, t.AttackInterval);
            Text(TargetKeys.Range, t.Range);
            Text(TargetKeys.Move, t.MoveSpeed);
            Text(TargetKeys.Experience, t.Experience);
            Text(TargetKeys.Distance, t.Distance);
            Text(TargetKeys.AttackName, t.AttackName);
            Text(TargetKeys.AttackDetail, t.AttackDetail);
            Text(TargetKeys.AttackStatus, t.AttackStatus);
            Text(TargetKeys.Trait, t.Trait);
            Text(TargetKeys.Description, t.Description);
            for (int i = 0; i < TargetStatusSlots; i++)
            {
                bool shown = i < t.StatusCount;
                Toggle(TargetKeys.Statuses[i], shown);
                Text(TargetKeys.StatusTexts[i], shown ? t.Statuses[i] : "");
            }
            Toggle(TargetKeys.NoStatus, t.StatusCount == 0);
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
        /// <summary>目标信息面板 UI.Game.TargetInfo 的绑定键；Prefab 由 Tools/HudArt/hud.py 生成，编辑器校验会核对全部键。</summary>
        public static class TargetKeys
        {
            public const string Name = "target.name", Tier = "target.tier", Subtitle = "target.subtitle", HealthText = "target.healthText",
                Damage = "target.damage", AttackRate = "target.attackRate", Range = "target.range", Move = "target.move",
                Experience = "target.xp", Distance = "target.distance", AttackName = "target.attackName", AttackDetail = "target.attackDetail",
                AttackStatus = "target.attackStatus", Trait = "target.trait", Description = "target.description";
            public const string Portrait = "target.portrait", AttackIcon = "target.attackIcon", Health = "target.health";
            public const string TierNormal = "target.tierNormal", TierElite = "target.tierElite", TierBoss = "target.tierBoss",
                InRange = "target.inRange", NoStatus = "target.noStatus";
            public const string CloseCommand = "combat.target.close";
            public static readonly string[] Statuses = Numbered("target.status");
            public static readonly string[] StatusTexts = Numbered("target.statusText");
            public static readonly string[] Texts = Join(new[] { Name, Tier, Subtitle, HealthText, Damage, AttackRate, Range, Move, Experience,
                Distance, AttackName, AttackDetail, AttackStatus, Trait, Description }, StatusTexts);
            public static readonly string[] Images = { Portrait, AttackIcon };
            public static readonly string[] Fills = { Health };
            public static readonly string[] Toggles = Join(new[] { TierNormal, TierElite, TierBoss, InRange, NoStatus }, Statuses);
            private static string[] Numbered(string prefix)
            {
                var result = new string[TargetStatusSlots];
                for (int i = 0; i < result.Length; i++) result[i] = prefix + i;
                return result;
            }
            internal static string[] Join(string[] a, string[] b)
            {
                var result = new string[a.Length + b.Length];
                a.CopyTo(result, 0); b.CopyTo(result, a.Length);
                return result;
            }
        }
        /// <summary>技能提示框 UI.Game.AbilityTooltip 的绑定键；Prefab 由 Tools/HudArt/hud.py 生成，编辑器校验会核对全部键。</summary>
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
            public static readonly string[] Toggles = TargetKeys.Join(TargetKeys.Join(new[] { HasHotkey, HasLevel, Passive, HasDescription, HasValues, HasNotes,
                HasCost, HasCooldown, HasMana, HasStatus }, PipSlots), Pips);
            private static string[] Numbered(string prefix, int count)
            {
                var result = new string[count];
                for (int i = 0; i < count; i++) result[i] = prefix + i;
                return result;
            }
        }
        /// <summary>属性面板（BottomHUD）的绑定键；左列攻击 / 攻速 / 距离 / 移速，右列技能伤害 / 冷却缩减 / 伤害减免 / 暴击。</summary>
        public static class StatsKeys
        {
            public const string Attack = "stats.attack", AttackSpeed = "stats.attackSpeed", Range = "stats.range", Move = "stats.move",
                Amplify = "stats.amplify", Cooldown = "stats.cooldown", Reduction = "stats.reduction", Critical = "stats.critical";
            public static readonly string[] Texts = { Attack, AttackSpeed, Range, Move, Amplify, Cooldown, Reduction, Critical };
        }
        /// <summary>金币与装备栏（BottomHUD）的绑定键；第 i 格的键前缀为 equip.s{i}.。金币键同时绑定在商店面板上。</summary>
        public static class EquipmentKeys
        {
            public const string Gold = "hero.gold";
            public static readonly string[] Icons = Numbered("equip.s", ".icon", EquipmentSlots), Filled = Numbered("equip.s", ".filled", EquipmentSlots),
                HasIcon = Numbered("equip.s", ".hasIcon", EquipmentSlots), Names = Numbered("equip.s", ".name", EquipmentSlots),
                Locked = Numbered("equip.s", ".locked", EquipmentSlots), Drop = Numbered("equip.s", ".drop", EquipmentSlots);
            public static readonly string[] Texts = TargetKeys.Join(new[] { Gold }, Names);
            public static readonly string[] Images = Icons;
            public static readonly string[] Toggles = TargetKeys.Join(TargetKeys.Join(TargetKeys.Join(Filled, HasIcon), Locked), Drop);
        }
        /// <summary>
        /// 商店面板 UI.Game.Shop 的绑定键；第 c 张卡片的键前缀为 shop.c{c}.。
        /// <see cref="Open"/>、<see cref="Far"/>（不在商店范围内时变暗）与 <see cref="Distance"/>（到商店的距离）绑定在 BottomHUD 的商店按钮上。
        /// </summary>
        public static class ShopKeys
        {
            public const string Open = "shop.open", Page = "shop.page", Paged = "shop.paged", Hint = "shop.hint", Far = "shop.far", Distance = "shop.distance";
            public static readonly string[] Visible = Numbered("shop.c", ".visible", ShopCards), Icons = Numbered("shop.c", ".icon", ShopCards),
                Names = Numbered("shop.c", ".name", ShopCards), Prices = Numbered("shop.c", ".price", ShopCards),
                Poor = Numbered("shop.c", ".poor", ShopCards), Owned = Numbered("shop.c", ".owned", ShopCards),
                HasIcon = Numbered("shop.c", ".hasIcon", ShopCards);
            public static readonly string[] Texts = TargetKeys.Join(TargetKeys.Join(new[] { Page, Hint, EquipmentKeys.Gold }, Names), Prices);
            public static readonly string[] Images = Icons;
            public static readonly string[] Toggles = TargetKeys.Join(TargetKeys.Join(TargetKeys.Join(TargetKeys.Join(new[] { Paged }, Visible), Poor), Owned), HasIcon);
        }
        private static string[] Numbered(string prefix, string suffix, int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++) result[i] = prefix + i + suffix;
            return result;
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
        /// <summary>当前战场名称；场景没有战场定义时为空。</summary>
        public string MapName;
        public int Level, SkillPoints;
        public float ExperienceFraction; public string ExperienceText;
        public float Health, MaximumHealth, Regen, Mana, MaximumMana, ManaRegen;
        public float AttackDamage, AttacksPerSecond, AttackRange, MoveSpeed;
        /// <summary>技能伤害、冷却缩减、伤害减免（已夹到上限）与暴击几率，单位为百分点。</summary>
        public float SkillAmplify, CooldownReduction, DamageReduction, CriticalChance;
        /// <summary>当前金币（可带小数，界面向下取整）与装备栏各格。</summary>
        public float Gold;
        public readonly EquipmentSlotView[] Equipment = new EquipmentSlotView[CombatHud.EquipmentSlots];
        /// <summary>商店当前页的卡片、页码（从 0 开始，已夹紧）、总页数与装备总数；ShopHint 为商店底部说明。</summary>
        public readonly ShopCardView[] Shop = new ShopCardView[CombatHud.ShopCards];
        public int ShopPage, ShopPages = 1, ShopItemCount;
        public string ShopHint;
        /// <summary>商店位置：ShopLimited 为真时只有在商店范围内（ShopInRange）才能打开商店；ShopDistance 为英雄到商店范围边缘的距离（米）。</summary>
        public bool ShopLimited, ShopInRange;
        public float ShopDistance;
        public string PassiveName, PassiveStatus; public Texture PassiveIcon;
        public readonly AbilityView[] Abilities = new AbilityView[CombatHud.AbilitySlots];
        public string Order; public bool AttackArmed, Aiming;
        public float Elapsed, Fps; public int Stage, Kills, Alive, Projectiles;
        public string SpawnStatus;
        public bool BossAlive; public string BossName; public float BossFraction;
        public string Hint;
        /// <summary>当前待选天赋层下标；-1 表示没有待选天赋。</summary>
        public int PendingTalent = -1;
        public string TalentTitle;
        public readonly TalentRowView[] Talents = new TalentRowView[CombatHud.TalentRows];
        /// <summary>左键选中的敌人；Visible 为假时隐藏目标面板。</summary>
        public readonly TargetInfoView Target = new TargetInfoView();
        /// <summary>悬停技能 / 被动图标、装备、属性面板或查看地上的装备时的详细提示；Visible 为假时隐藏提示框。</summary>
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

    /// <summary>商店当前页一张卡片的展示状态；Item 为装备库下标（-1 表示空卡）。</summary>
    public struct ShopCardView
    {
        public bool Visible, CannotAfford, Owned;
        public int Item, Price;
        public Texture Icon;
        public string Name;
    }

    /// <summary>天赋树一层的展示状态。</summary>
    public struct TalentRowView
    {
        public bool Exists, Unlocked, Chosen, ChoseRight;
        public int Level;
        public string Left, Right;
    }
}
