using System.Collections.Generic;
using System.Text;
using Roguelike.Abilities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>技能 / 自动攻击被动的悬停提示框快照（Dota 2 式详细信息）；由控制器在 HUD 刷新时填充并复用，只含文字与纹理。</summary>
    public sealed class AbilityTooltipView
    {
        public bool Visible, Passive;
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

            view.Visible = true; view.Passive = false;
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
            view.Visible = true; view.Passive = true;
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
                case DeliveryKind.LinearProjectile: line.Append(Key("直线弹体")).Append($"射程 {V(N(stage.Range))} 米 · 速度 {V(N(stage.Speed))} 米/秒 · 宽度 {V(N(stage.HitRadius * 2))} 米"); break;
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
