using System;
using System.Linq;
using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>只改变配置资源的编辑器文案；字段名、枚举数值与序列化数据保持不变。</summary>
    internal abstract class AbilityChineseInspector : UnityEditor.Editor
    {
        protected void Begin()
        {
            serializedObject.Update();
            var script = serializedObject.FindProperty("m_Script");
            if (script != null)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(script, new GUIContent("脚本"));
        }

        protected void End() => serializedObject.ApplyModifiedProperties();

        protected void AssignMissingNumber()
        {
            if (targets.Length != 1 || serializedObject.FindProperty("configId").intValue != 0) return;
            if (!GUILayout.Button("分配数字 ID（保存此资源）")) return;
            serializedObject.ApplyModifiedProperties();
            try
            {
                if (target is SkillDefinition skill)
                {
                    if (string.IsNullOrWhiteSpace(skill.stableId)) skill.stableId = "ability." + Guid.NewGuid().ToString("N");
                    Roguelike.SkillAuthoring.Editor.NumericConfigIds.EnsureSkill(skill);
                }
                else if (target is HeroDefinition hero)
                {
                    if (string.IsNullOrWhiteSpace(hero.stableId)) hero.stableId = "hero." + Guid.NewGuid().ToString("N");
                    Roguelike.SkillAuthoring.Editor.NumericConfigIds.EnsureHero(hero);
                }
                else if(target is EffectDefinition effect)
                {
                    if(string.IsNullOrWhiteSpace(effect.stableId)) effect.stableId="effect."+Guid.NewGuid().ToString("N");
                    Roguelike.SkillAuthoring.Editor.NumericConfigIds.EnsureEffect(effect);
                }
                else if(target is BuffDefinition buff)
                {
                    if(string.IsNullOrWhiteSpace(buff.stableId)) buff.stableId="buff."+Guid.NewGuid().ToString("N");
                    Roguelike.SkillAuthoring.Editor.NumericConfigIds.EnsureBuff(buff);
                }
                serializedObject.Update();
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        protected void Field(string name, string label, string explanation = null)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, explanation), true);
        }

        protected void Array(string name, string title, string itemName, string explanation = null)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null) return;
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded,
                new GUIContent($"{title}（{property.arraySize}）", explanation), true);
            if (!property.isExpanded) return;
            EditorGUI.indentLevel++;
            property.arraySize = Mathf.Clamp(EditorGUILayout.IntField("数量", property.arraySize), 0, 1024);
            for (int i = 0; i < property.arraySize; i++)
                EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i),
                    new GUIContent($"{itemName} {i + 1}"), true);
            EditorGUI.indentLevel--;
        }

        protected void Section(string text) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(text, EditorStyles.boldLabel); }
        protected static bool LegacyFoldout;
    }

    [CustomEditor(typeof(SkillDefinition))]
    [CanEditMultipleObjects]
    internal sealed class SkillDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            using(new EditorGUI.DisabledScope(true)) { Field("configId", "技能 ID（数字编号）"); Field("stableId", "内部兼容键"); }
            AssignMissingNumber();
            Field("displayName", "技能名称", "用于界面显示。");
            Field("icon", "技能图标", "Game 底部技能栏显示；为空时显示纯色底。");
            Field("description", "技能说明", "鼠标悬停技能图标时显示；伤害、范围、冷却等数值行由配置自动生成，这里只写玩法描述。");
            Field("aim", "瞄准方式", "自身、目标点、方向或单位；不会决定伤害类型。");
            bool recipe=SkillRecipeAdapter.IsRecipe((SkillDefinition)target);
            Field("modules","组合模块目录","通过数字 ID 查找投递、查询和效果模块。");
            if(recipe)
            {
                var definition=(SkillDefinition)target;
                for(int i=0;i<definition.recipe.nodes.Length;i++)
                {
                    var node=definition.recipe.nodes[i];
                    if(node==null || !definition.modules || !definition.modules.deliveries.Any(d=>d.id==node.deliveryModuleId && d.kind==DeliveryKind.LinearProjectile)) continue;
                    Section("步骤 "+(i+1)+" · 直线首碰投递");
                    Field("recipe.nodes.Array.data["+i+"].projectileCount","每次发射数量（支）","1–32 支；每支首次命中后独立消失。重开生效。");
                    Field("recipe.nodes.Array.data["+i+"].spreadDegrees","完整散射张角（度）","0–180 度；一支时始终沿中心方向。重开生效。");
                }
                EditorGUILayout.HelpBox("当前使用数字配方。旧阶段仅用于回退，不参与执行。请在技能设计器中编辑步骤卡片。",MessageType.Info);
                if(GUILayout.Button("打开技能配方设计器")) Roguelike.SkillAuthoring.Editor.SkillAuthoringWindow.Edit((SkillDefinition)target);
            }
            else Array("stages", "技能阶段", "阶段", "按顺序执行；每段可独立配置投递、目标和效果。");
            Field("maxCharges", "最大充能次数", "每个英雄独立保存当前充能。");
            Field("rechargeSeconds", "单次充能恢复时间（秒）", "设为 0 时使用技能冷却时间。");
            Field("requiredHits", "施放所需普攻命中次数", "仅普攻命中积累；成功施放后清零。");
            Field("castRange", "施法距离（Unity 单位）", "1 单位等于 1 世界坐标单位；目标点和单位目标的最大距离，重开 Game 后生效。");
            Field("cooldown", "冷却时间（秒）", "成功提交施法后开始计算；每个英雄独立保存。");
            Field("castTime", "释放前摇 / 释放时间（秒）", "前摇结束才释放；阶段延迟从此刻计算。默认 0。Game 前后摇期间禁止移动、普攻和再次施法，重开会话后生效。");
            Field("recoveryTime", "释放后摇（秒）", "从前摇结束计算，不等待飞行或多段完成。前摇被禁止施法状态打断会取消释放，不退资源和冷却。");
            Field("manaCost", "法力消耗（点）", "技能 1 级的基础蓝耗；按等级表缩放，成功提交时扣除一次，不足时施法失败。");
            Field("growth", "等级成长", "学习门槛、最高等级与每级倍率。1 级等于上方配置数值；Game 开局技能为 0 级，需要用技能点学习。");
            bool formal = recipe || serializedObject.FindProperty("stages").arraySize > 0;
            Field("indicatorColor", formal ? "指示器颜色" : "旧示例指示器颜色",
                "工作台预览立即更新；Game 在重开会话时读取颜色快照。");
            if (!formal)
            {
                EditorGUILayout.HelpBox("当前资源使用旧独立示例格式；Game 需要至少一个组合阶段。", MessageType.Info);
                Section("旧独立示例参数");
                Field("delivery", "旧投递方式");
                Field("targets", "旧目标关系");
                Field("radius", "旧范围半径（米）");
                Field("areaDuration", "旧区域持续时间（秒）");
                Field("areaInterval", "旧区域查询间隔（秒）");
                Array("effects", "旧效果列表", "效果");
                Field("levelPower", "旧示例每级效果增幅", "仅旧独立示例使用；Game 组合技能使用“等级成长”。");
            }
            End();
        }
    }

    [CustomEditor(typeof(BuffDefinition))]
    [CanEditMultipleObjects]
    internal sealed class BuffDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            using(new EditorGUI.DisabledScope(true)) {Field("configId","Buff ID（数字编号）"); Field("stableId", "内部兼容键");}
            AssignMissingNumber();
            Field("displayName", "Buff 名称");
            Section("持续与叠层");
            Field("duration", "持续时间（秒）", "0 表示永久，须显式移除或重开会话。");
            Field("interval", "周期结算间隔（秒）", "刷新 Buff 不重置下一次周期时间。");
            Field("periodicDamage", "每周期伤害", "0 表示没有周期伤害。");
            Field("stacking", "叠层规则");
            Field("maxStacks", "最大层数");
            Field("dispellable", "允许驱散");
            Field("sourcePolicy", "来源合并方式", "按定义、按来源或每次独立保存状态。");
            Section("属性与控制");
            Array("modifiers", "属性修饰", "修饰", "多个有效 Buff 共同参与属性重算。");
            Field("controls", "控制限制", "不同 Buff 的限制会合并；各自到期后重新计算。");
            LegacyFoldout = EditorGUILayout.Foldout(LegacyFoldout, "旧独立示例兼容参数", true);
            if (LegacyFoldout)
            {
                Field("moveSpeedPercent", "旧移速百分比", "与属性修饰列表一起累计。");
                Field("attackDamageFlat", "旧攻击力固定加成");
                Field("healthRegen", "旧每秒生命回复");
            }
            End();
        }
    }

    [CustomEditor(typeof(HeroDefinition))]
    [CanEditMultipleObjects]
    internal sealed class HeroDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            using(new EditorGUI.DisabledScope(true)) { Field("configId", "英雄 ID（数字编号）"); Field("stableId", "内部兼容键"); }
            AssignMissingNumber();
            Field("displayName", "英雄名称");
            Array("skills", "技能装配", "槽位", "按顺序对应 Game 的 Q、W、E、R 输入槽；当前 Game 最多支持四槽。");
            Section("基础属性（重开后生效）");
            Field("health", "基础生命值");
            Field("speed", "移动速度（米/秒）");
            Field("attackDamage", "普攻伤害");
            Field("attackInterval", "普攻间隔（秒）", "旧字段：未指定自动攻击模块时作为基础攻击间隔。");
            Field("range", "普攻距离（米）", "旧字段：未指定自动攻击模块时使用。");
            Field("basicAttack", "自动攻击（被动模块）", "攻击距离、索敌距离、基础攻击间隔、前后摇与弹道；可与敌人共用同一模块。");
            Field("turnRate", "转身速度（度/秒）", "移动与开始攻击前先转向；0 表示瞬间转身。");
            Field("attackWhileMoving", "移动中攻击", "开启：右键移动途中自动攻击攻击距离内的敌人且不停下，前摇不定身，按住右键拖动也能持续出手。关闭：Dota 2 式移动取消攻击。重开后生效。");
            Field("visualPrefab", "英雄展示预制体");
            Field("portrait", "头像", "Game 底部 HUD 显示。");
            Section("等级成长（Dota 2 式，每升 1 级累加，重开后生效）");
            Field("baseMana", "开局最大法力");
            Field("baseHealthRegen", "开局生命回复（点/秒）");
            Field("baseManaRegen", "开局法力回复（点/秒）");
            Field("healthPerLevel", "每级最大生命", "升级时当前生命同步增加。");
            Field("manaPerLevel", "每级最大法力");
            Field("attackPerLevel", "每级普攻伤害");
            Field("attackSpeedPerLevel", "每级攻击速度（%）", "百分点：2.5 表示每级 +2.5% 攻击速度，与天赋、装备的攻击速度直接相加。");
            Field("moveSpeedPerLevel", "每级移动速度（米/秒）");
            Field("regenPerLevel", "每级生命回复（点/秒）");
            Field("manaRegenPerLevel", "每级法力回复（点/秒）");
            Array("talents", "天赋树", "天赋层", "按英雄等级升序排列；到达等级后二选一，不消耗技能点。最多 31 层。");
            Section("额外基础属性（重开后生效）");
            Array("baseStats", "额外基础属性", "属性", "直接加到英雄基础属性上（不换算），例如 +10 暴击几率、+5 普攻吸血；百分比类属性填写百分点（25 = +25%）。最多 16 条。");
            End();
        }
    }

    [CustomEditor(typeof(SkillCatalog))]
    [CanEditMultipleObjects]
    internal sealed class SkillCatalogChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            Array("skills", "技能定义", "技能", "被英雄装备的技能必须登记在这里。");
            Array("buffs", "Buff 定义", "Buff", "被效果引用的 Buff 必须登记在这里。");
            Array("passives", "旧示例被动定义", "被动", "当前 Game 尚未接入通用被动触发框架。");
            End();
        }
    }

    [CustomEditor(typeof(AutoAttackDefinition))]
    [CanEditMultipleObjects]
    internal sealed class AutoAttackDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            EditorGUILayout.HelpBox("自动攻击被动模块：英雄和敌人共用。Dota 2 式节奏：开始攻击 → 前摇（可被移动取消；英雄开启“移动中攻击”时移动不取消）→ 出手 → 后摇（可被指令打断）；" +
                "实际间隔、前摇、后摇都除以攻速倍率。重开 Game 后生效。", MessageType.Info);
            Field("stableId", "内部兼容键");
            Field("displayName", "模块名称");
            Field("icon", "图标", "HUD 被动栏显示。");
            Field("description", "说明");
            Section("攻击方式");
            Field("delivery", "结算方式", "近战前摇结束立即命中；弹道前摇结束发射追踪弹体。");
            Field("projectileSpeed", "弹道速度（米/秒）", "仅弹道使用。");
            Field("countsAsBasicAttackHit", "触发普攻命中事件", "命中时为需要普攻叠层的技能计数，例如冰霜专注。");
            Section("距离");
            Field("range", "攻击距离（米）", "从单位中心计算，另加目标体型。");
            Field("acquisitionRange", "索敌距离（米）", "待机或攻击移动时自动寻找此距离内最近的敌人；小于攻击距离时按攻击距离处理。");
            Field("rangeBuffer", "攻击距离缓冲（米）", "前摇期间目标离开攻击距离不超过该值时仍会出手。");
            Field("autoAcquire", "自动索敌", "关闭后只攻击被右键指定的目标。");
            Section("节奏（随攻速缩放）");
            Field("baseAttackTime", "基础攻击间隔（秒）", "Dota 2 的 BAT；实际间隔 = 基础间隔 / 攻速倍率。");
            Field("attackPoint", "攻击前摇（秒）", "开始攻击到出手；不能超过基础攻击间隔。");
            Field("backswing", "攻击后摇（秒）", "出手后的收招；可被任意移动或指令打断。");
            var data = (AutoAttackDefinition)target;
            if (targets.Length == 1 && data.baseAttackTime > 0)
                EditorGUILayout.HelpBox($"攻速倍率 1 时：每秒 {1 / data.baseAttackTime:0.##} 次攻击，前摇 {data.attackPoint:0.###} 秒，后摇 {data.backswing:0.###} 秒。", MessageType.None);
            End();
        }
    }

    [CustomEditor(typeof(PassiveDefinition))]
    [CanEditMultipleObjects]
    internal sealed class PassiveDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            EditorGUILayout.HelpBox("此资源供旧独立技能示例使用；当前 Game 不装配被动技能。", MessageType.Info);
            Field("stableId", "稳定 ID");
            Field("displayName", "被动名称");
            Field("trigger", "触发方式");
            Array("effects", "触发效果", "效果");
            End();
        }
    }

    [CustomEditor(typeof(EffectDefinition), true)]
    [CanEditMultipleObjects]
    internal sealed class EffectDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            using(new EditorGUI.DisabledScope(true)) {Field("configId","效果 ID（数字编号）"); Field("stableId","内部兼容键");}
            AssignMissingNumber();
            if (target is DamageEffect) Section("即时伤害效果");
            else if (target is HealEffect) Section("即时治疗效果");
            else if (target is ApplyBuffEffect) Section("施加 Buff 效果");
            else if (target is DispelEffect) Section("驱散效果");
            Field("target", "作用对象", "选中目标，或每段只结算一次的施法者。");
            if (target is DamageEffect) Field("amount", "伤害值");
            else if (target is HealEffect) Field("amount", "治疗值");
            else if (target is ApplyBuffEffect)
            {
                Field("buff", "Buff 定义", "引用共享状态资源。");
                Field("buffScale", "Buff 强度倍率", "只影响当前效果组合，不修改共享 Buff 资源。");
            }
            // 新效果模块必须补上中文编辑字段；仍显示未登记字段，避免配置被隐藏。
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.depth != 0 || iterator.name == "m_Script" || iterator.name == "target" ||
                    iterator.name == "amount" || iterator.name == "buff" || iterator.name == "buffScale" || iterator.name=="configId" || iterator.name=="stableId") continue;
                EditorGUILayout.HelpBox("新效果字段“" + iterator.name + "”尚未设置中文标签，请更新效果 Inspector。", MessageType.Warning);
                EditorGUILayout.PropertyField(iterator, true);
            }
            End();
        }
    }

    internal static class AbilityPropertyLayout
    {
        public static float Line => EditorGUIUtility.singleLineHeight;
        public static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public static float FieldHeight(SerializedProperty property) => EditorGUI.GetPropertyHeight(property, true) + Gap;

        public static void Field(ref Rect line, SerializedProperty property, string label, string explanation = null)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(new Rect(line.x, line.y, line.width, height), property,
                new GUIContent(label, explanation), true);
            line.y += height + Gap;
        }

        public static float ArrayHeight(SerializedProperty property)
        {
            float result = Line + Gap;
            if (!property.isExpanded) return result;
            result += Line + Gap;
            for (int i = 0; i < property.arraySize; i++) result += FieldHeight(property.GetArrayElementAtIndex(i));
            return result;
        }

        public static void Array(ref Rect line, SerializedProperty property, string title, string item)
        {
            property.isExpanded = EditorGUI.Foldout(new Rect(line.x, line.y, line.width, Line),
                property.isExpanded, $"{title}（{property.arraySize}）", true);
            line.y += Line + Gap;
            if (!property.isExpanded) return;
            EditorGUI.indentLevel++;
            property.arraySize = Mathf.Clamp(EditorGUI.IntField(new Rect(line.x, line.y, line.width, Line),
                "数量", property.arraySize), 0, 64);
            line.y += Line + Gap;
            for (int i = 0; i < property.arraySize; i++) Field(ref line, property.GetArrayElementAtIndex(i), $"{item} {i + 1}");
            EditorGUI.indentLevel--;
        }
    }

    [CustomPropertyDrawer(typeof(SkillStage))]
    internal sealed class SkillStageChineseDrawer : PropertyDrawer
    {
        private static readonly (string name, string label, string hint)[] Fields =
        {
            ("delay", "释放后延迟（秒）", "从前摇结束、正式释放时计算；不包含前摇和后摇。"),
            ("repeatCount", "执行次数", "同一阶段可按间隔重复执行。"),
            ("projectileCount", "每次发射数量（支）", "仅直线弹体；每支首碰造成效果后消失。"),
            ("spreadDegrees", "完整散射张角（度）", "仅直线弹体；一支时始终朝中心发射。"),
            ("repeatInterval", "重复间隔（秒）", "执行次数大于 1 时必须为正数。"),
            ("delivery", "投递方式", "立即结算、飞向目标点、直线首碰或三维抛物线投递。抛物线沿途不碰撞，到点结算。"),
            ("scope", "目标范围", "自身、单体、区域或全体。"),
            ("shape", "区域形状", "仅区域目标使用。"),
            ("targets", "目标关系", "按施法者阵营筛选。"),
            ("range", "几何或飞行距离（Unity 单位）", "扇形、线段和直线弹体使用；1 单位 = 1 世界坐标单位。"),
            ("radius", "范围半径或直线半宽（Unity 单位）", "圆形爆炸与直线区域使用；总宽为半宽的两倍。"),
            ("hitRadius", "弹体碰撞半径（Unity 单位）", "只决定直线弹体首碰；与爆炸范围分开。"),
            ("halfAngleDegrees", "扇形半角（度）", "仅扇形区域使用。"),
            ("speed", "弹体速度（Unity 单位/秒）", "目标点直飞与直线首碰使用；抛物线使用独立飞行时长。"),
            ("arcHeight", "弧线附加高度（Unity 单位）", "相对起终点连线的高度；飞行进度一半时达到该附加高度。"),
            ("flightSeconds", "飞行时长（秒）", "必须大于 0；与距离无关，使用定时弧线，不模拟物理重力。"),
            ("launchHeight", "发射高度（Unity 单位）", "相对施法者起点的世界竖直偏移。"),
            ("arrivalHeight", "落点高度（Unity 单位）", "相对目标点的世界竖直偏移；到点后仍按 XZ 平面查询目标。")
        };

        private static IEnumerable<(string name, string label, string hint)> Visible(SerializedProperty stage)
        {
            var delivery = (DeliveryKind)stage.FindPropertyRelative("delivery").enumValueIndex;
            var scope = (TargetScope)stage.FindPropertyRelative("scope").enumValueIndex;
            var shape = (AreaShape)stage.FindPropertyRelative("shape").enumValueIndex;
            foreach (var entry in Fields)
            {
                if (entry.name == "repeatInterval" && stage.FindPropertyRelative("repeatCount").intValue <= 1) continue;
                if (entry.name == "shape" && scope != TargetScope.Area) continue;
                if (entry.name == "targets" && scope == TargetScope.Self) continue;
                if (entry.name == "range" && delivery != DeliveryKind.LinearProjectile &&
                    !(scope == TargetScope.Area && (shape == AreaShape.Cone || shape == AreaShape.Line))) continue;
                if (entry.name == "radius" && (scope != TargetScope.Area || shape == AreaShape.Cone)) continue;
                if (entry.name == "hitRadius" && delivery != DeliveryKind.LinearProjectile) continue;
                if (entry.name == "halfAngleDegrees" && !(scope == TargetScope.Area && shape == AreaShape.Cone)) continue;
                if (entry.name == "speed" && delivery != DeliveryKind.PointProjectile && delivery != DeliveryKind.LinearProjectile) continue;
                if ((entry.name == "arcHeight" || entry.name == "flightSeconds" || entry.name == "launchHeight" || entry.name == "arrivalHeight") &&
                    delivery != DeliveryKind.ParabolicProjectile) continue;
                yield return entry;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (!property.isExpanded) return height;
            foreach (var entry in Visible(property)) height += AbilityPropertyLayout.FieldHeight(property.FindPropertyRelative(entry.name));
            height += AbilityPropertyLayout.ArrayHeight(property.FindPropertyRelative("effects"));
            if ((DeliveryKind)property.FindPropertyRelative("delivery").enumValueIndex == DeliveryKind.LinearProjectile)
                height += AbilityPropertyLayout.ArrayHeight(property.FindPropertyRelative("impactEffects"));
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var entry in Visible(property))
                    AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative(entry.name), entry.label, entry.hint);
                AbilityPropertyLayout.Array(ref line, property.FindPropertyRelative("effects"), "范围效果", "效果");
                if ((DeliveryKind)property.FindPropertyRelative("delivery").enumValueIndex == DeliveryKind.LinearProjectile)
                    AbilityPropertyLayout.Array(ref line, property.FindPropertyRelative("impactEffects"), "首碰效果", "效果");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(AttributeModifier))]
    internal sealed class AttributeModifierChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            property.isExpanded ? AbilityPropertyLayout.Line * 4 + AbilityPropertyLayout.Gap * 4 :
                AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("attribute"), "属性");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("operation"), "运算方式");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("value"), "数值",
                    "百分比填写小数：0.2 表示 +20%；乘法系数 1 表示不变。");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EffectEntry))]
    internal sealed class LegacyEffectEntryChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            property.isExpanded ? AbilityPropertyLayout.Line * 5 + AbilityPropertyLayout.Gap * 5 :
                AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("kind"), "效果类型");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("amount"), "伤害或治疗数值");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("buff"), "Buff 定义");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("buffScale"), "Buff 强度倍率");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
