using System.Collections.Generic;
using System.Text;
using Roguelike.Abilities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>技能 / 自动攻击被动 / 装备 / 英雄属性的悬停提示框快照（Dota 2 式详细信息）；由控制器在 HUD 刷新时填充并复用，只含文字与纹理。</summary>
    public sealed class AbilityTooltipView
    {
        public bool Visible, Passive;
        /// <summary>不显示等级行与等级点（装备、属性提示）。</summary>
        public bool NoLevel;
        public Texture Icon;
        /// <summary>名称、热键、类别（普通/终极/被动）、等级文字。</summary>
        public string Name, Hotkey, Kind, LevelText;
        /// <summary>属性行（技能类型、影响、效果、施法距离）、说明、逐级数值、附注、冷却、法力与当前状态。空字符串表示隐藏该段。</summary>
        public string Properties, Description, Values, Notes, Cooldown, Mana, Status;
        public int Level, MaxLevel;
    }

    /// <summary>组装一个技能槽提示所需的只读上下文；全部来自会话配置与 ECS 权威状态的副本。</summary>
    public struct SkillTooltipContext
    {
        public string Hotkey, Name, Description;
        public Texture Icon;
        public SkillLevelTrack Track;
        /// <summary>本局 Buff 定义（下标 = 库索引），只用于显示名称。</summary>
        public BuffDefinition[] Buffs;
        public AbilitySlot Slot;
        public AbilityUnit Owner;
        public HeroProgression Progression;
        public float Now;
        /// <summary>该槽正处于施法前摇 / 后摇。</summary>
        public bool CastWindup, CastRecovery;
    }

    /// <summary>组装一件装备提示所需的只读上下文：装备定义（名称、图标、说明）、Buff 名称、当前金币、购买与拾取预检结果。</summary>
    public struct ItemTooltipContext
    {
        public EquipmentDefinition Definition;
        public BuffDefinition[] Buffs;
        public float Gold;
        /// <summary>悬停的是商店卡片（否则为装备栏格或地上的装备）；ShopOpen 表示商店面板已打开（可以出售）。</summary>
        public bool FromShop, ShopOpen;
        /// <summary>商店卡片的购买预检结果（装备栏格不使用）。</summary>
        public EquipmentFailure BuyFailure;
        /// <summary>查看的是地上的装备；PickingUp 表示英雄正在走过去拾取它。</summary>
        public bool FromGround, PickingUp;
        /// <summary>地上的装备：英雄到它的平面距离（米）。</summary>
        public float Distance;
        /// <summary>地上的装备：不计距离的拾取预检结果（装备栏已满、唯一装备已拥有等）。</summary>
        public EquipmentFailure PickupFailure;
    }

    /// <summary>
    /// 只读地把技能库编译数据（权威执行配置，含配方覆盖）与槽位运行状态整理成 Dota 2 式提示：逐级数值“当前级白色、可升到的下一级绿色、其他灰色”。
    /// 不写入任何 ECS 数据，也不按技能或英雄身份分支。
    /// </summary>
    public static class AbilityTooltipBuilder
    {
        public const string Current = "#FFFFFF", Next = "#7CFF6B", Other = "#7D8892", Label = "#9AA7B4", Gold = "#F7D485",
            Warn = "#FF8A6A", Frost = "#9FDBFF", Good = "#7CFF6B";
        private static readonly StringBuilder line = new StringBuilder(256), block = new StringBuilder(1024);
        private static readonly List<string> tags = new List<string>(8);

        // ---------------- 技能 ----------------
        public static void BuildSkill(ref AbilityLibrary library, ref SkillData skill, in SkillTooltipContext c, AbilityTooltipView view)
        {
            int max = SkillLevels.MaxLevel(ref skill), level = math.clamp(c.Slot.Level, 0, max);
            var rows = new SkillLevelData[max];
            for (int i = 0; i < max; i++) rows[i] = SkillLevels.Row(ref skill, i + 1);
            var learn = HeroProgressionLogic.CanLearn(c.Progression, ref skill, level);
            // 有技能点且满足等级门槛时，下一级以绿色标出（Dota 2 升级预览）。
            int next = learn == ProgressionFailure.None ? level + 1 : 0;
            float amplify = math.max(0, c.Owner.SkillAmplify);
            float reduction = math.clamp(c.Owner.CooldownReduction, 0, SkillLevels.MaximumCooldownReduction);

            view.Visible = true; view.Passive = false; view.NoLevel = false;
            view.Icon = c.Icon; view.Name = string.IsNullOrWhiteSpace(c.Name) ? "技能" : c.Name; view.Hotkey = c.Hotkey ?? "";
            view.Level = level; view.MaxLevel = max;
            view.Kind = (c.Track == SkillLevelTrack.Ultimate ? "终极技能" : c.Track == SkillLevelTrack.Basic ? "普通技能" : "技能") +
                        (skill.RequiredHits > 0 ? " · 普攻叠层" : skill.MaxCharges > 1 ? " · 充能" : "");
            view.LevelText = level <= 0 ? $"未学习 · 最高 {max} 级" : $"等级 {level} / {max}";
            view.Description = string.IsNullOrWhiteSpace(c.Description) ? "" : c.Description.Trim();

            // 属性行
            block.Clear();
            Property("技能", AimLabel(skill.Aim));
            Property("影响", Affects(ref skill));
            string categories = Categories(ref library, ref skill);
            if (categories.Length > 0) Property("效果", categories);
            if (skill.Aim != AimMode.Self && skill.CastRange > 0) Property("施法距离", $"{N(skill.CastRange)} 米");
            if (skill.CastTime > 0 || skill.RecoveryTime > 0) Property("施法动作", $"前摇 {N(skill.CastTime)} 秒 · 后摇 {N(skill.RecoveryTime)} 秒");
            view.Properties = Trim(block);

            // 逐级数值：每段的投递/范围与效果
            block.Clear();
            int stages = skill.Stages.Length;
            for (int s = 0; s < stages; s++)
            {
                ref var stage = ref skill.Stages[s];
                string prefix = stages > 1 ? $"<color={Gold}>{StageLabel(ref stage, s)}</color> " : "";
                string geometry = Geometry(ref stage);
                if (geometry.Length > 0) Line(prefix + geometry);
                if (stage.RepeatCount > 1) Line($"{prefix}{Key("次数")}{V(stage.RepeatCount.ToString())} 次 · 间隔 {N(stage.RepeatInterval)} 秒");
                if (stage.Delay > 0) Line($"{prefix}{Key("延迟")}{V(N(stage.Delay))} 秒");
                for (int e = 0; e < stage.ImpactEffects.Length; e++)
                    Effect(ref library, stage.ImpactEffects[e], rows, level, next, amplify, prefix + "首个命中目标 · ", c.Buffs);
                string areaPrefix = stage.Delivery == DeliveryKind.LinearProjectile && stage.ImpactEffects.Length > 0 ? prefix + "范围内 · " : prefix;
                for (int e = 0; e < stage.Effects.Length; e++)
                    Effect(ref library, stage.Effects[e], rows, level, next, amplify, areaPrefix, c.Buffs);
            }
            view.Values = Trim(block);

            // 冷却与法力（含天赋冷却缩减）
            var values = new float[max];
            for (int i = 0; i < max; i++) values[i] = skill.Cooldown * rows[i].CooldownScale * (1 - reduction);
            view.Cooldown = skill.Cooldown > 0 ? $"{Key("冷却")}{Series(values, level, next)} 秒" : "";
            if (skill.MaxCharges > 1)
            {
                for (int i = 0; i < max; i++) values[i] = skill.RechargeSeconds * rows[i].CooldownScale * (1 - reduction);
                string recharge = $"{Key("充能")}{skill.MaxCharges} 次 · 每 {Series(values, level, next)} 秒恢复 1 次";
                view.Cooldown = view.Cooldown.Length > 0 ? view.Cooldown + "\n" + recharge : recharge;
            }
            for (int i = 0; i < max; i++) values[i] = skill.ManaCost * rows[i].ManaCostScale;
            view.Mana = skill.ManaCost > 0 ? $"{Key("法力")}{Series(values, level, next)}" : "";

            // 附注
            block.Clear();
            for (int i = 0; i < max; i++) values[i] = rows[i].RequiredHeroLevel;
            Line($"{Key("学习门槛")}英雄等级 {Series(values, level, next)}");
            if (skill.RequiredHits > 0) Line($"需要普攻命中 {skill.RequiredHits} 次叠满层数才能施放，施放后清零。");
            if (skill.MaxCharges > 1) Line($"最多储存 {skill.MaxCharges} 次充能，满充能时开始计时。");
            if (amplify > 0.0005f) Line($"伤害已计入技能增强 <color={Good}>+{amplify * 100:0.#}%</color>。");
            if (reduction > 0.0005f) Line($"冷却已计入冷却缩减 <color={Good}>-{reduction * 100:0.#}%</color>。");
            view.Notes = Trim(block);

            // 当前状态
            block.Clear();
            string key = string.IsNullOrEmpty(c.Hotkey) ? "" : c.Hotkey;
            if (level <= 0)
            {
                if (learn == ProgressionFailure.None) Line($"<color={Good}>可学习：按 Ctrl+{key} 或点击技能上方的“+”</color>");
                else if (learn == ProgressionFailure.HeroLevelTooLow) Line($"<color={Warn}>需要英雄等级 {SkillLevels.RequiredHeroLevel(ref skill, 1)} 才能学习</color>");
                else Line($"<color={Label}>未学习：获得技能点后按 Ctrl+{key} 学习</color>");
            }
            else
            {
                bool ready = true;
                if (c.CastWindup) { Line($"<color={Frost}>施法前摇中</color>"); ready = false; }
                else if (c.CastRecovery) { Line($"<color={Frost}>施法后摇中</color>"); ready = false; }
                if (c.Slot.ReadyAt > c.Now) { Line($"<color={Warn}>冷却中：剩余 {c.Slot.ReadyAt - c.Now:0.0} 秒</color>"); ready = false; }
                else if (c.Slot.Charges <= 0) { Line($"<color={Warn}>充能中：{math.max(0, c.Slot.RechargeAt - c.Now):0.0} 秒后恢复 1 次</color>"); ready = false; }
                else if (skill.MaxCharges > 1) Line($"<color={Frost}>当前充能 {c.Slot.Charges} / {skill.MaxCharges}</color>");
                if (skill.RequiredHits > c.Slot.HitStacks) { Line($"<color={Frost}>普攻叠层 {c.Slot.HitStacks} / {skill.RequiredHits}</color>"); ready = false; }
                if (SkillLevels.ManaCost(ref skill, level) > c.Owner.Mana) { Line($"<color={Warn}>法力不足</color>"); ready = false; }
                if ((c.Owner.Controls & ControlFlags.CastBlocked) != 0) { Line($"<color={Warn}>无法施法（被控制）</color>"); ready = false; }
                if (ready) Line(skill.Aim == AimMode.Self ? $"<color={Good}>就绪：按 {key} 立即施放</color>" : $"<color={Good}>就绪：按 {key} 瞄准，左键确认，右键 / Esc 取消</color>");
                if (learn == ProgressionFailure.None) Line($"<color={Good}>可升级：按 Ctrl+{key} 升到 {level + 1} 级</color>");
                else if (learn == ProgressionFailure.HeroLevelTooLow) Line($"<color={Label}>升到 {level + 1} 级需要英雄等级 {SkillLevels.RequiredHeroLevel(ref skill, level + 1)}</color>");
                else if (learn == ProgressionFailure.SkillMaxed) Line($"<color={Gold}>已满级</color>");
            }
            view.Status = Trim(block);
        }

        // ---------------- 自动攻击被动 ----------------
        /// <summary>自动攻击被动模块的提示：攻击力、攻击间隔与攻速、距离、前后摇、弹道与当前攻击阶段。</summary>
        public static void BuildAttack(in AutoAttackData data, in AutoAttackState state, in AbilityUnit unit, AutoAttackDefinition module,
            bool attackWhileMoving, bool targetAlive, AbilityTooltipView view)
        {
            float speed = unit.AttackSpeedMultiplier > 0 ? unit.AttackSpeedMultiplier : 1;
            float interval = AutoAttackLogic.Interval(data, speed), perSecond = AutoAttackLogic.AttacksPerSecond(data, speed);
            view.Visible = true; view.Passive = true; view.NoLevel = false;
            view.Icon = module ? module.icon : null;
            view.Name = module ? module.Label : "普通攻击";
            view.Hotkey = ""; view.Kind = "被动 · 自动攻击"; view.LevelText = ""; view.Level = 0; view.MaxLevel = 0;
            view.Description = module && !string.IsNullOrWhiteSpace(module.description) ? module.description.Trim() : "";
            view.Cooldown = ""; view.Mana = "";

            block.Clear();
            Property("技能", "被动（自动施放）");
            Property("影响", "敌方单位");
            Property("结算", data.Delivery == AttackDelivery.Projectile ? $"弹道追踪 · {N(data.ProjectileSpeed)} 米/秒" : "近战 · 前摇结束立即命中");
            view.Properties = Trim(block);

            block.Clear();
            Line($"{Key("攻击力")}{V(N(unit.Attack))}{Base(unit.Attack, unit.BaseAttack)}");
            Line($"{Key("攻击间隔")}{V(interval.ToString("0.00"))} 秒（基础 {N(data.BaseAttackTime)} 秒 ÷ 攻速 ×{speed:0.00}）");
            Line($"{Key("每秒攻击")}{V(perSecond.ToString("0.0"))} 次 · 理论每秒伤害 {V(N(unit.Attack * perSecond))}");
            Line($"{Key("攻击距离")}{V(N(data.Range))} 米（另加目标体型）");
            Line($"{Key("索敌距离")}{V(N(data.AcquisitionRange))} 米");
            Line($"{Key("攻击前摇")}{V(AutoAttackLogic.Point(data, speed).ToString("0.00"))} 秒 · {Key("后摇")}{V(AutoAttackLogic.BackswingTime(data, speed).ToString("0.00"))} 秒");
            Line($"{Key("前摇缓冲")}{V(N(data.RangeBuffer))} 米");
            view.Values = Trim(block);

            block.Clear();
            if (attackWhileMoving) Line("移动中攻击：右键移动途中自动攻击攻击距离内的敌人，前摇不定身。");
            else Line("移动会取消攻击前摇；停下后重新开始攻击。");
            if (data.AutoAcquire) Line("待机与攻击移动时自动攻击索敌距离内最近的敌人。");
            if (data.CountsAsBasicAttackHit) Line("命中计入“普攻命中”，为需要叠层的技能积累层数。");
            Line("攻击间隔、前摇与后摇都随攻击速度缩短。");
            view.Notes = Trim(block);

            if ((unit.Controls & ControlFlags.AttackBlocked) != 0) view.Status = $"<color={Warn}>无法攻击（被控制）</color>";
            else if (state.Phase == AttackPhase.Windup) view.Status = $"<color={Frost}>攻击前摇中</color>";
            else if (state.Phase == AttackPhase.Backswing) view.Status = $"<color={Frost}>攻击后摇</color>";
            else if (targetAlive) view.Status = $"<color={Good}>攻击中</color>";
            else view.Status = $"<color={Label}>索敌中</color>";
        }

        // ---------------- 装备 ----------------
        /// <summary>装备提示：品质与价格、属性加成（直接相加）、说明、普攻命中效果、唯一与出售价，以及购买 / 出售 / 丢弃 / 拾取操作提示。</summary>
        public static void BuildItem(ref AbilityLibrary library, ref EquipmentItemData item, in ItemTooltipContext c, AbilityTooltipView view)
        {
            var definition = c.Definition;
            view.Visible = true; view.Passive = false; view.NoLevel = true;
            view.Icon = definition ? definition.icon : null;
            view.Name = definition ? definition.Label : "装备";
            view.Hotkey = ""; view.LevelText = ""; view.Level = 0; view.MaxLevel = 0; view.Cooldown = ""; view.Mana = "";
            view.Kind = $"<color={QualityColor(item.Quality)}>{QualityLabel(item.Quality)}装备</color> · <color={Gold}>{item.Price:N0} 金币</color>";
            view.Description = definition && !string.IsNullOrWhiteSpace(definition.description) ? definition.description.Trim() : "";

            // 属性加成：正数绿色、负数橙色，按属性面板顺序排列；单位见属性面板提示，这里只写“+1.5 移动速度”。
            block.Clear();
            foreach (var stat in StatText.DisplayOrder)
            {
                float value = item.Stats[stat];
                if (math.abs(value) < 0.0005f) continue;
                Line($"<color={(value > 0 ? Good : Warn)}>{StatText.Format(stat, value)}</color>");
            }
            if (block.Length == 0) Line($"{Key("属性")}无（只有命中效果）");
            view.Properties = Trim(block);

            // 普攻命中效果：复用技能效果的文字，强度固定为 1，不受技能伤害加成影响。
            block.Clear();
            var rows = new[] { SkillLevels.Identity };
            for (int e = 0; e < item.OnHit.Length; e++) Effect(ref library, item.OnHit[e], rows, 1, 0, 0, $"<color={Gold}>普攻命中</color> · ", c.Buffs);
            view.Values = Trim(block);

            block.Clear();
            if (item.Unique) Line("唯一：同一件装备最多持有一件。");
            if (item.OnHit.Length > 0) Line("命中效果在每次普攻命中时触发，不受技能伤害加成影响。");
            Line($"出售返还 {item.SellPrice:N0} 金币。");
            view.Notes = Trim(block);

            if (c.FromShop)
            {
                switch (c.BuyFailure)
                {
                    case EquipmentFailure.None: view.Status = $"<color={Good}>左键购买</color>"; break;
                    case EquipmentFailure.NotEnoughGold:
                        view.Status = $"<color={Warn}>金币不足：还差 {math.max(1, (int)math.ceil(item.Price - c.Gold - 0.0001f)):N0} 金币</color>"; break;
                    case EquipmentFailure.AlreadyOwned: view.Status = $"<color={Label}>已拥有（唯一装备）</color>"; break;
                    case EquipmentFailure.InventoryFull: view.Status = $"<color={Warn}>装备栏已满：先出售或丢弃一件装备</color>"; break;
                    default: view.Status = $"<color={Warn}>{EquipmentLogic.Describe(c.BuyFailure)}</color>"; break;
                }
            }
            else if (c.FromGround)
            {
                string action;
                switch (c.PickupFailure)
                {
                    case EquipmentFailure.None: action = c.PickingUp ? $"<color={Good}>正在走过去拾取……</color>" : $"<color={Good}>右键拾取（走到附近自动捡起）</color>"; break;
                    case EquipmentFailure.InventoryFull: action = $"<color={Warn}>装备栏已满：先出售或丢弃一件装备再拾取</color>"; break;
                    case EquipmentFailure.AlreadyOwned: action = $"<color={Label}>已拥有（唯一装备）：不能再拾取一件</color>"; break;
                    default: action = $"<color={Warn}>{EquipmentLogic.Describe(c.PickupFailure)}</color>"; break;
                }
                view.Status = $"{action}\n<color={Label}>地上的装备 · 距离 {c.Distance:0.#} 米 · 点击空地或 Esc 关闭</color>";
            }
            else view.Status = (c.ShopOpen ? $"<color={Gold}>右键出售（或拖到商店）：返还 {item.SellPrice:N0} 金币</color>"
                    : $"<color={Label}>走进商店范围（地面金色圆圈）按 B 打开商店后，右键出售</color>") +
                $"\n<color={Label}>拖到地面丢弃 · 拖到其他格换位</color>";
        }
        public static string QualityLabel(EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Uncommon: return "精良";
                case EquipmentQuality.Rare: return "稀有";
                case EquipmentQuality.Epic: return "史诗";
                case EquipmentQuality.Legendary: return "传说";
                default: return "普通";
            }
        }
        /// <summary>品质颜色，与 Tools/HudArt/items.py 生成的图标边框一致。</summary>
        public static string QualityColor(EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Uncommon: return "#6BE07A";
                case EquipmentQuality.Rare: return "#4FA8FF";
                case EquipmentQuality.Epic: return "#C77DFF";
                case EquipmentQuality.Legendary: return "#FFB347";
                default: return "#C8D0D8";
            }
        }

        // ---------------- 英雄属性 ----------------
        /// <summary>单项属性来源。复用权威属性快照；状态效果只展示结算后的结果，不从 UI 反推战斗。</summary>
        public static void BuildStat(StatKind stat,in HeroStats stats,in AbilityUnit unit,in AutoAttackData attack,int level,string heroName,Texture icon,AbilityTooltipView view)
        {
            BuildStats(stats,unit,attack,level,heroName,icon,view);
            view.Name=StatText.Name(stat);
            view.Properties="";
            int levels=math.max(0,level-1);
            block.Clear();
            Line($"{Key("基础值")}{V(StatText.Value(stat,stats.Base[stat]))}{Unit(stat)}");
            Line($"{Key("等级成长")}{V(StatText.Value(stat,stats.PerLevel[stat]*levels))}{Unit(stat)}（每级 {StatText.Value(stat,stats.PerLevel[stat])} × {levels} 次）");
            Line($"{Key("天赋加成")}{V(StatText.Value(stat,stats.Talents[stat]))}{Unit(stat)}");
            Line($"{Key("装备加成")}{V(StatText.Value(stat,stats.Equipment[stat]))}{Unit(stat)}");
            Line($"{Key("属性合计")}{V(StatText.Value(stat,stats.Total[stat]))}{Unit(stat)}{Cap(stat,stats.Total[stat])}");
            view.Values=Trim(block);
            float current=stats.Total[stat];
            switch(stat)
            {
                case StatKind.MaxHealth: current=unit.MaxHealth; break;
                case StatKind.MaxMana: current=unit.MaxMana; break;
                case StatKind.HealthRegen: current=unit.Regen; break;
                case StatKind.ManaRegen: current=AbilityEffects.BaseManaRegen+unit.ManaRegenBonus; break;
                case StatKind.AttackDamage: current=unit.Attack; break;
                case StatKind.AttackSpeed: current=(unit.AttackSpeedMultiplier-1)*100; break;
                case StatKind.MoveSpeed: current=unit.Speed; break;
                case StatKind.AttackRange: current=attack.Range; break;
                case StatKind.SkillAmplify: current=unit.SkillAmplify*100; break;
                case StatKind.CooldownReduction: current=unit.CooldownReduction*100; break;
                case StatKind.DamageReduction: current=unit.DamageReduction*100; break;
                case StatKind.CriticalChance: current=math.clamp(current,0,100); break;
                case StatKind.CriticalDamage: current=math.max(100,current); break;
            }
            view.Status=$"{Key("当前实际（含状态效果）")}{V(StatText.Value(stat,current))}{Unit(stat)}";
            view.Notes="基础 + 每级成长 ×（等级 - 1）+ 天赋 + 装备。状态效果在合计上继续结算。";
            switch(stat)
            {
                case StatKind.AttackDamage: view.Description="普攻命中时使用当前攻击力；暴击倍率和技能伤害加成分别结算。"; break;
                case StatKind.AttackSpeed:
                    view.Description=$"基础攻击间隔 {attack.BaseAttackTime:0.##} 秒；当前每秒攻击 {AutoAttackLogic.AttacksPerSecond(attack,unit.AttackSpeedMultiplier):0.0} 次。";
                    view.Notes+="\n百分比按百分点相加。增益可能继续乘算攻速，前后摇也随攻速缩短。"; break;
                case StatKind.ManaRegen: view.Description="每秒恢复法力，达到最大法力后停止；升级、天赋和装备可增加回复。"; break;
                case StatKind.HealthRegen: view.Description="每秒回复生命，达到最大生命后停止。"; break;
                case StatKind.MaxMana: view.Description=$"当前法力 {unit.Mana:0} / {unit.MaxMana:0}；每秒回复 {AbilityEffects.BaseManaRegen+unit.ManaRegenBonus:0.##} 点。升级增加上限时同步增加当前法力。"; break;
                case StatKind.MaxHealth: view.Description=$"当前生命 {unit.Health:0} / {unit.MaxHealth:0}；每秒回复 {unit.Regen:0.##} 点。升级增加上限时同步增加当前生命。"; break;
                case StatKind.AttackRange: view.Description="普攻可攻击的距离；另计目标体型。距离单位为 Unity 世界单位。"; break;
                case StatKind.MoveSpeed: view.Description="每秒移动的 Unity 世界单位；减速、眩晕等状态还会影响实际移动。"; break;
                case StatKind.SkillAmplify: view.Description="只加成技能伤害，不加成普攻伤害。"; break;
                case StatKind.CooldownReduction: view.Description="缩短技能冷却和充能恢复时间，最高 80%。"; break;
                case StatKind.DamageReduction: view.Description="按比例减少受到的伤害，最高 80%。"; break;
                case StatKind.CriticalChance: view.Description=$"普攻暴击概率；暴击时造成 {stats.Total.CriticalDamage:0.#}% 伤害。"; break;
                default: view.Description="属性按来源直接相加；百分比类数值使用百分点。"; break;
            }
        }
        /// <summary>
        /// 属性面板提示：全部属性的合计与来源（基础、等级成长、天赋、装备，直接相加），上限说明，以及含状态效果的当前攻击、攻速与移速。
        /// </summary>
        public static void BuildStats(in HeroStats stats, in AbilityUnit unit, in AutoAttackData attack, int level, string heroName, Texture icon, AbilityTooltipView view)
        {
            view.Visible = true; view.Passive = false; view.NoLevel = true;
            view.Icon = icon; view.Name = "英雄属性"; view.Hotkey = ""; view.LevelText = ""; view.Level = 0; view.MaxLevel = 0;
            view.Kind = $"{(string.IsNullOrWhiteSpace(heroName) ? "英雄" : heroName)} · 等级 {math.max(1, level)}";
            view.Description = ""; view.Cooldown = ""; view.Mana = "";

            block.Clear();
            Property("合计", "基础 + 成长 ×（等级 - 1）+ 天赋 + 装备");
            Property("规则", "所有来源直接相加，不做任何换算");
            view.Properties = Trim(block);

            int levels = math.max(0, level - 1);
            block.Clear();
            foreach (var stat in StatText.DisplayOrder)
            {
                float total = stats.Total[stat], baseline = stats.Base[stat], growth = stats.PerLevel[stat] * levels,
                    talent = stats.Talents[stat], equipment = stats.Equipment[stat];
                line.Clear();
                line.Append(Key(StatText.Name(stat))).Append(V(StatText.Value(stat, total))).Append(Unit(stat));
                line.Append(Cap(stat, total));
                int sources = 0;
                var detail = new StringBuilder(64);
                Source(detail, ref sources, "成长", stat, growth);
                Source(detail, ref sources, "天赋", stat, talent);
                Source(detail, ref sources, "装备", stat, equipment);
                if (sources > 0) line.Append($"<color={Label}><size=12>（基础 {StatText.Value(stat, baseline)}{detail}）</size></color>");
                Line(line.ToString());
            }
            view.Values = Trim(block);

            block.Clear();
            Line("百分比类属性按百分点直接相加，例如 +20% 与 +30% 攻击速度合计 +50%。");
            Line($"冷却缩减与伤害减免最高 {N(HeroStatsLogic.MaximumCooldownReduction)}%；暴击只作用于普攻，暴击伤害是暴击时的伤害倍率。");
            Line("技能伤害只加成技能；普攻吸血按普攻实际造成的伤害回复生命。");
            view.Notes = Trim(block);

            float speed = unit.AttackSpeedMultiplier > 0 ? unit.AttackSpeedMultiplier : 1;
            view.Status = $"{Key("当前（含状态效果）")}攻击力 {V(N(unit.Attack))} · 每秒攻击 {V(AutoAttackLogic.AttacksPerSecond(attack, speed).ToString("0.0"))} 次 · 移速 {V(N(unit.Speed))}";
        }
        private static void Source(StringBuilder detail, ref int sources, string label, StatKind stat, float value)
        {
            if (math.abs(value) < 0.0005f) return;
            detail.Append(" · ").Append(label).Append(value < 0 ? " -" : " +").Append(StatText.Value(stat, math.abs(value)));
            sources++;
        }
        /// <summary>属性的单位后缀：回复为“/秒”，攻击距离为“米”，移动速度为“米/秒”；其余没有单位（百分比已带 %）。</summary>
        private static string Unit(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.HealthRegen: case StatKind.ManaRegen: return "/秒";
                case StatKind.AttackRange: return " 米";
                case StatKind.MoveSpeed: return " 米/秒";
                default: return "";
            }
        }
        /// <summary>超出结算上下限时的提示（结算使用夹紧后的值）。</summary>
        private static string Cap(StatKind stat, float total)
        {
            switch (stat)
            {
                case StatKind.CooldownReduction:
                    return total > HeroStatsLogic.MaximumCooldownReduction ? $" <color={Warn}>（上限 {N(HeroStatsLogic.MaximumCooldownReduction)}%）</color>" : "";
                case StatKind.DamageReduction:
                    return total > HeroStatsLogic.MaximumDamageReduction ? $" <color={Warn}>（上限 {N(HeroStatsLogic.MaximumDamageReduction)}%）</color>" : "";
                case StatKind.CriticalChance:
                    return total > 100 ? $" <color={Warn}>（上限 100%）</color>" : "";
                case StatKind.AttackSpeed:
                    return total < HeroStatsLogic.MinimumAttackSpeed ? $" <color={Warn}>（下限 {N(HeroStatsLogic.MinimumAttackSpeed)}%）</color>" : "";
                default: return "";
            }
        }

        // ---------------- 组成部分 ----------------
        private static void Effect(ref AbilityLibrary library, in EffectData effect, SkillLevelData[] rows, int level, int next, float amplify,
            string prefix, BuffDefinition[] buffs)
        {
            int max = rows.Length; var values = new float[max];
            string self = effect.Target == EffectTarget.Source ? "自身 · " : "";
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    for (int i = 0; i < max; i++) values[i] = effect.Amount * rows[i].EffectScale * (1 + amplify);
                    Line($"{prefix}{self}{Key("伤害")}{Series(values, level, next)}");
                    break;
                case EffectKind.Heal:
                    for (int i = 0; i < max; i++) values[i] = effect.Amount * rows[i].EffectScale;
                    Line($"{prefix}{self}{Key("治疗")}{Series(values, level, next)}");
                    break;
                case EffectKind.Dispel:
                    Line($"{prefix}{self}{Key("驱散")}移除可驱散的状态效果");
                    break;
                case EffectKind.ApplyBuff:
                    if (effect.Buff < 0 || effect.Buff >= library.Buffs.Length) break;
                    ref var buff = ref library.Buffs[effect.Buff];
                    line.Clear();
                    line.Append(prefix).Append(self).Append(Key(BuffName(buffs, effect.Buff)));
                    int parts = 0;
                    string controls = ControlLabel(buff.Controls);
                    if (controls.Length > 0) { line.Append(V(controls)); parts++; }
                    for (int m = 0; m < buff.Modifiers.Length; m++)
                    {
                        var modifier = buff.Modifiers[m];
                        for (int i = 0; i < max; i++)
                        {
                            float strength = rows[i].BuffScale * effect.BuffScale;
                            values[i] = modifier.Operation == ModifierOperation.Multiply ? math.pow(modifier.Value, strength) :
                                modifier.Operation == ModifierOperation.Percent ? modifier.Value * strength * 100 : modifier.Value * strength;
                        }
                        Part(ref parts, AttributeLabel(modifier.Attribute) + " " + Signed(values, level, next, modifier.Operation));
                    }
                    if (math.abs(buff.SpeedPercent) > 0)
                    {
                        for (int i = 0; i < max; i++) values[i] = buff.SpeedPercent * rows[i].BuffScale * effect.BuffScale * 100;
                        Part(ref parts, "移动速度 " + Signed(values, level, next, ModifierOperation.Percent));
                    }
                    if (math.abs(buff.AttackFlat) > 0)
                    {
                        for (int i = 0; i < max; i++) values[i] = buff.AttackFlat * rows[i].BuffScale * effect.BuffScale;
                        Part(ref parts, "攻击力 " + Signed(values, level, next, ModifierOperation.Add));
                    }
                    if (buff.Regen > 0)
                    {
                        for (int i = 0; i < max; i++) values[i] = buff.Regen * rows[i].BuffScale * effect.BuffScale;
                        Part(ref parts, "生命回复 +" + Series(values, level, next) + "/秒");
                    }
                    if (buff.PeriodicDamage > 0)
                    {
                        for (int i = 0; i < max; i++) values[i] = buff.PeriodicDamage * rows[i].BuffScale * effect.BuffScale;
                        Part(ref parts, $"每 {N(buff.Interval)} 秒造成 " + Series(values, level, next) + " 伤害");
                    }
                    Part(ref parts, buff.Duration > 0 ? $"持续 {V(N(buff.Duration))} 秒" : "永久");
                    if (buff.Stacking == BuffStackRule.AddAndRefresh && buff.MaxStacks > 1) Part(ref parts, $"可叠加 {buff.MaxStacks} 层");
                    if (!buff.Dispellable) Part(ref parts, $"<color={Warn}>不可驱散</color>");
                    Line(line.ToString());
                    break;
            }
        }
        private static void Part(ref int parts, string text) { line.Append(parts++ > 0 ? " · " : "").Append(text); }

        /// <summary>“影响”行：按阶段目标关系与效果作用对象汇总，例如“敌方单位、自身”。</summary>
        private static string Affects(ref SkillData skill)
        {
            tags.Clear();
            for (int s = 0; s < skill.Stages.Length; s++)
            {
                ref var stage = ref skill.Stages[s];
                if (stage.Scope == TargetScope.Self) Tag("自身");
                else if (HasSelected(ref stage.Effects) || HasSelected(ref stage.ImpactEffects)) Tag(stage.Targets == TargetRelation.Allies ? "友方单位" : "敌方单位");
                if (HasSource(ref stage.Effects) || HasSource(ref stage.ImpactEffects)) Tag("自身");
            }
            return tags.Count > 0 ? string.Join("、", tags) : "无";
        }
        private static bool HasSelected(ref Unity.Entities.BlobArray<EffectData> effects)
        { for (int i = 0; i < effects.Length; i++) if (effects[i].Target == EffectTarget.Selected) return true; return false; }
        private static bool HasSource(ref Unity.Entities.BlobArray<EffectData> effects)
        { for (int i = 0; i < effects.Length; i++) if (effects[i].Target == EffectTarget.Source) return true; return false; }

        /// <summary>“效果”行：伤害、治疗、驱散与 Buff 带来的减速、眩晕、攻速提升等类别。</summary>
        private static string Categories(ref AbilityLibrary library, ref SkillData skill)
        {
            tags.Clear();
            for (int s = 0; s < skill.Stages.Length; s++)
            {
                ref var stage = ref skill.Stages[s];
                for (int pass = 0; pass < 2; pass++)
                {
                    ref var effects = ref pass == 0 ? ref stage.ImpactEffects : ref stage.Effects;
                    for (int e = 0; e < effects.Length; e++)
                    {
                        var effect = effects[e];
                        if (effect.Kind == EffectKind.Damage) Tag("伤害");
                        else if (effect.Kind == EffectKind.Heal) Tag("治疗");
                        else if (effect.Kind == EffectKind.Dispel) Tag("驱散");
                        else if (effect.Buff >= 0 && effect.Buff < library.Buffs.Length)
                        {
                            ref var buff = ref library.Buffs[effect.Buff];
                            string control = ControlLabel(buff.Controls);
                            if (control.Length > 0) Tag(control.Split('（')[0]);
                            for (int m = 0; m < buff.Modifiers.Length; m++)
                            {
                                var modifier = buff.Modifiers[m];
                                bool up = modifier.Operation == ModifierOperation.Multiply ? modifier.Value > 1 : modifier.Value > 0;
                                if (modifier.Attribute == AttributeKind.MoveSpeed) Tag(up ? "加速" : "减速");
                                else if (modifier.Attribute == AttributeKind.AttackSpeed) Tag(up ? "攻速提升" : "攻速降低");
                                else Tag(up ? "攻击力提升" : "攻击力降低");
                            }
                            if (buff.SpeedPercent < 0) Tag("减速"); else if (buff.SpeedPercent > 0) Tag("加速");
                            if (buff.PeriodicDamage > 0) Tag("持续伤害");
                            if (buff.Regen > 0) Tag("生命回复");
                        }
                    }
                }
            }
            return string.Join("、", tags);
        }
        private static void Tag(string value) { if (!tags.Contains(value)) tags.Add(value); }

        /// <summary>投递与范围几何，例如“扇形：长 22 米 · 角度 70°”“直线弹体：射程 150 米 · 速度 48 米/秒”。</summary>
        private static string Geometry(ref StageData stage)
        {
            line.Clear();
            switch (stage.Delivery)
            {
                case DeliveryKind.PointProjectile: line.Append(Key("弹体")).Append($"飞向目标点 · 速度 {V(N(stage.Speed))} 米/秒"); break;
                case DeliveryKind.LinearProjectile: line.Append($"{V(stage.ProjectileCount.ToString())} 支 · 散射 {V(N(stage.SpreadDegrees))}° · 首碰消失\n").Append(Key("直线弹体")).Append($"射程 {V(N(stage.Range))} 米 · 速度 {V(N(stage.Speed))} 米/秒 · 宽度 {V(N(stage.HitRadius * 2))} 米"); break;
                case DeliveryKind.ParabolicProjectile: line.Append(Key("抛物线投掷")).Append($"飞行 {V(N(stage.FlightSeconds))} 秒"); break;
            }
            string area = "";
            switch (stage.Scope)
            {
                case TargetScope.Self: area = stage.Delivery == DeliveryKind.Instant ? Key("范围") + "自身" : ""; break;
                case TargetScope.Single: area = Key("目标") + "单体"; break;
                case TargetScope.All: area = Key("范围") + "全体单位"; break;
                case TargetScope.Area:
                    if (stage.Shape == AreaShape.Cone) area = Key("扇形") + $"长 {V(N(stage.Range))} 米 · 角度 {V(N(stage.HalfAngleDegrees * 2))}°";
                    else if (stage.Shape == AreaShape.Line) area = Key("直线") + $"长 {V(N(stage.Range))} 米 · 宽 {V(N(stage.Radius * 2))} 米";
                    else area = Key(stage.Delivery == DeliveryKind.Instant ? "作用半径" : "爆炸半径") + $"{V(N(stage.Radius))} 米";
                    break;
            }
            if (area.Length > 0) line.Append(line.Length > 0 ? "\n" : "").Append(area);
            return line.ToString();
        }
        private static string StageLabel(ref StageData stage, int index)
        {
            if (stage.ParentStage < 0) return $"第 {index + 1} 段";
            switch (stage.Trigger)
            {
                case RecipeTrigger.OnResolved: return "到达后";
                case RecipeTrigger.OnProjectileHit: return "命中后";
                case RecipeTrigger.OnDamage: return "造成伤害后";
                case RecipeTrigger.OnKill: return "击杀后";
                default: return $"第 {index + 1} 段";
            }
        }
        public static string AimLabel(AimMode aim)
        {
            switch (aim)
            {
                case AimMode.Self: return "无目标（以自身为中心）";
                case AimMode.Direction: return "指向方向";
                case AimMode.Unit: return "指向单位";
                default: return "指向地点";
            }
        }
        /// <summary>控制标记的 Dota 2 式名称：三项全禁为眩晕，单项分别为定身、缴械、沉默。</summary>
        public static string ControlLabel(ControlFlags flags)
        {
            const ControlFlags all = ControlFlags.MovementBlocked | ControlFlags.AttackBlocked | ControlFlags.CastBlocked;
            if ((flags & all) == all) return "眩晕（无法移动、攻击和施法）";
            var parts = new List<string>(3);
            if ((flags & ControlFlags.MovementBlocked) != 0) parts.Add("定身");
            if ((flags & ControlFlags.AttackBlocked) != 0) parts.Add("缴械");
            if ((flags & ControlFlags.CastBlocked) != 0) parts.Add("沉默");
            return string.Join("、", parts);
        }
        private static string AttributeLabel(AttributeKind kind) => kind == AttributeKind.MoveSpeed ? "移动速度" : kind == AttributeKind.AttackSpeed ? "攻击速度" : "攻击力";
        private static string BuffName(BuffDefinition[] buffs, int index)
        {
            var definition = buffs != null && index >= 0 && index < buffs.Length ? buffs[index] : null;
            return definition ? (string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName) : "状态";
        }

        /// <summary>逐级数值：全部相同时只显示一个；否则“a / b / c”，当前级白色加粗、下一级绿色、其他灰色。level 为 0 表示未学习。</summary>
        public static string Series(float[] values, int level, int next, string format = "0.##")
        {
            if (values == null || values.Length == 0) return "";
            bool same = true;
            for (int i = 1; i < values.Length; i++) if (math.abs(values[i] - values[0]) > 0.0005f * math.max(1, math.abs(values[0]))) same = false;
            if (same) return V(values[0].ToString(format));
            var builder = new StringBuilder(values.Length * 24);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) builder.Append($"<color={Other}> / </color>");
                string text = values[i].ToString(format);
                if (i + 1 == level) builder.Append($"<color={Current}><b>{text}</b></color>");
                else if (i + 1 == next) builder.Append($"<color={Next}>{text}</color>");
                else builder.Append($"<color={Other}>{text}</color>");
            }
            return builder.ToString();
        }
        /// <summary>带正负号的逐级修饰值：百分比加 %，乘法系数前缀 ×。</summary>
        private static string Signed(float[] values, int level, int next, ModifierOperation operation)
        {
            if (operation == ModifierOperation.Multiply) return "×" + Series(values, level, next, "0.##");
            bool negative = values.Length > 0 && values[0] < 0;
            var shown = new float[values.Length];
            for (int i = 0; i < values.Length; i++) shown[i] = math.abs(values[i]);
            return (negative ? "-" : "+") + Series(shown, level, next, "0.#") + (operation == ModifierOperation.Percent ? "%" : "");
        }
        private static string Base(float current, float baseline)
            => baseline > 0 && math.abs(current / baseline - 1) >= 0.005f ? $"<color={Label}>（基础 {N(baseline)}）</color>" : "";
        private static void Property(string key, string value) => Line(Key(key) + value);
        private static string Key(string key) => $"<color={Label}>{key}：</color>";
        private static string V(string value) => $"<color={Current}><b>{value}</b></color>";
        private static string N(float value) => value.ToString("0.##");
        private static void Line(string text) { if (block.Length > 0) block.Append('\n'); block.Append(text); }
        private static string Trim(StringBuilder builder) => builder.ToString();
    }
}
