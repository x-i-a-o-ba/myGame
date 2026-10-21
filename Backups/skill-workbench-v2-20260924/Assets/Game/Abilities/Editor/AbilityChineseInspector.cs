using System;
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
            Field("stableId", "稳定 ID", "跨表引用和存档使用；修改显示名称时无需改动 ID。");
            Field("displayName", "技能名称", "用于界面显示。");
            Field("aim", "瞄准方式", "自身、目标点、方向或单位；不会决定伤害类型。");
            Array("stages", "技能阶段", "阶段", "按顺序执行；每段可独立配置投递、目标和效果。");
            Field("maxCharges", "最大充能次数", "每个英雄独立保存当前充能。");
            Field("rechargeSeconds", "单次充能恢复时间（秒）", "设为 0 时使用技能冷却时间。");
            Field("requiredHits", "施放所需普攻命中次数", "仅普攻命中积累；成功施放后清零。");
            Field("castRange", "施法距离（Unity 单位）", "1 单位等于 1 世界坐标单位；目标点和单位目标的最大距离，重开 Game 后生效。");
            Field("cooldown", "冷却时间（秒）", "成功提交施法后开始计算；每个英雄独立保存。");
            Field("manaCost", "资源消耗", "成功提交时扣除一次；不足时施法失败。");
            bool formal = serializedObject.FindProperty("stages").arraySize > 0;
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
                Field("levelPower", "旧示例每级效果增幅", "当前 Game 技能固定为一级。");
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
            Field("stableId", "稳定 ID", "供技能效果引用；与显示名称分开。");
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
            Field("stableId", "稳定 ID", "英雄的非本地化唯一标识。");
            Field("displayName", "英雄名称");
            Array("skills", "技能装配", "槽位", "按顺序对应 Game 的 Q、W、E、R 输入槽；当前 Game 最多支持四槽。");
            Section("基础属性（重开后生效）");
            Field("health", "基础生命值");
            Field("speed", "移动速度（米/秒）");
            Field("attackDamage", "普攻伤害");
            Field("attackInterval", "普攻间隔（秒）");
            Field("range", "普攻距离（米）");
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
                    iterator.name == "amount" || iterator.name == "buff" || iterator.name == "buffScale") continue;
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
            ("delay", "开始延迟（秒）", "从成功施法起计算。"),
            ("repeatCount", "执行次数", "同一阶段可按间隔重复执行。"),
            ("repeatInterval", "重复间隔（秒）", "执行次数大于 1 时必须为正数。"),
            ("delivery", "投递方式", "立即结算、飞向目标点或直线首碰。"),
            ("scope", "目标范围", "自身、单体、区域或全体。"),
            ("shape", "区域形状", "仅区域目标使用。"),
            ("targets", "目标关系", "按施法者阵营筛选。"),
            ("range", "几何或飞行距离（Unity 单位）", "扇形、线段和直线弹体使用；1 单位 = 1 世界坐标单位。"),
            ("radius", "范围半径或直线半宽（Unity 单位）", "圆形爆炸与直线区域使用；总宽为半宽的两倍。"),
            ("hitRadius", "弹体碰撞半径（Unity 单位）", "只决定直线弹体首碰；与爆炸范围分开。"),
            ("halfAngleDegrees", "扇形半角（度）", "仅扇形区域使用。"),
            ("speed", "弹体速度（Unity 单位/秒）", "立即结算时不使用。")
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
                if (entry.name == "speed" && delivery == DeliveryKind.Instant) continue;
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
