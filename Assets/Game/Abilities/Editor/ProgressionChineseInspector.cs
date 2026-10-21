using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>经验与等级规则的中文 Inspector：逐级显示升级经验并给出累计预览。</summary>
    [CustomEditor(typeof(HeroProgressionRules))]
    internal sealed class HeroProgressionRulesChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            EditorGUILayout.HelpBox("英雄从 1 级开始，击杀敌人获得经验。默认经验表取自 Dota 2（30 级累计 63900）。每级获得技能点，天赋选择不消耗技能点。重开 Game 后生效。", MessageType.Info);
            Field("maxLevel", "最高等级", "英雄可达到的最高等级，Dota 2 默认 30。");
            Field("startingSkillPoints", "开局技能点", "Dota 2 英雄 1 级拥有 1 个技能点。");
            Field("skillPointsPerLevel", "每级技能点");
            var table = serializedObject.FindProperty("experienceToNextLevel");
            int maxLevel = serializedObject.FindProperty("maxLevel").intValue;
            table.isExpanded = EditorGUILayout.Foldout(table.isExpanded, new GUIContent($"每级所需经验（{table.arraySize} 项）",
                "第 1 项是 1→2 级所需经验；数量至少为最高等级 - 1。"), true);
            if (table.isExpanded)
            {
                EditorGUI.indentLevel++;
                table.arraySize = Mathf.Clamp(EditorGUILayout.IntField("数量", table.arraySize), 0, HeroProgressionRules.MaximumLevel);
                long total = 0;
                for (int i = 0; i < table.arraySize; i++)
                {
                    var item = table.GetArrayElementAtIndex(i);
                    total += Mathf.Max(0, item.intValue);
                    EditorGUILayout.PropertyField(item, new GUIContent($"{i + 1} → {i + 2} 级", $"到达 {i + 2} 级的累计经验：{total}"));
                }
                EditorGUI.indentLevel--;
            }
            if (table.arraySize < maxLevel - 1)
                EditorGUILayout.HelpBox($"经验表只有 {table.arraySize} 项，最高等级 {maxLevel} 需要至少 {maxLevel - 1} 项。", MessageType.Error);
            if (GUILayout.Button("恢复 Dota 2 默认经验表"))
            {
                var values = HeroProgressionRules.DotaExperienceTable();
                table.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) table.GetArrayElementAtIndex(i).intValue = values[i];
            }
            End();
        }
    }

    /// <summary>技能等级成长的中文绘制器；普通/终极档显示逐级预览。</summary>
    [CustomPropertyDrawer(typeof(SkillLevelGrowth))]
    internal sealed class SkillLevelGrowthChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (!property.isExpanded) return line;
            float height = line + AbilityPropertyLayout.FieldHeight(property.FindPropertyRelative("track"));
            if (IsCustom(property)) height += AbilityPropertyLayout.ArrayHeight(property.FindPropertyRelative("customLevels"));
            else height += line * 5;
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
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("track"), "成长类型",
                    "普通技能 4 级（英雄 1/3/5/7 级可学）；终极技能 3 级（英雄 6/12/18 级可学）；或自定义逐级表。");
                if (IsCustom(property))
                    AbilityPropertyLayout.Array(ref line, property.FindPropertyRelative("customLevels"), "自定义逐级表", "技能等级");
                else
                {
                    var effect = property.FindPropertyRelative("effectPerLevel");
                    var buff = property.FindPropertyRelative("buffPerLevel");
                    var cooldown = property.FindPropertyRelative("cooldownReductionPerLevel");
                    var mana = property.FindPropertyRelative("manaCostPerLevel");
                    AbilityPropertyLayout.Field(ref line, effect, "每级伤害/治疗增幅", "0.9 表示每升 1 级增加 1 级数值的 90%。");
                    AbilityPropertyLayout.Field(ref line, buff, "每级 Buff 强度增幅", "乘到 Buff 修饰值、周期伤害与回复上；不改变持续时间。");
                    AbilityPropertyLayout.Field(ref line, cooldown, "每级冷却缩短比例", "0.08 表示每升 1 级冷却缩短 1 级冷却的 8%。");
                    AbilityPropertyLayout.Field(ref line, mana, "每级资源消耗增幅");
                    int levels = property.FindPropertyRelative("track").enumValueIndex == (int)SkillLevelTrack.Ultimate ? 3 : 4;
                    var text = new System.Text.StringBuilder("预览  ");
                    for (int i = 0; i < levels; i++)
                        text.Append($"Lv{i + 1}: 伤害×{1 + effect.floatValue * i:0.##} 冷却×{1 - cooldown.floatValue * i:0.##}   ");
                    EditorGUI.LabelField(new Rect(line.x, line.y, line.width, AbilityPropertyLayout.Line), text.ToString(), EditorStyles.miniLabel);
                    line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        private static bool IsCustom(SerializedProperty property) =>
            property.FindPropertyRelative("track").enumValueIndex == (int)SkillLevelTrack.Custom;
    }

    [CustomPropertyDrawer(typeof(SkillLevelRow))]
    internal sealed class SkillLevelRowChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            (AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap) * (property.isExpanded ? 6 : 1);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("requiredHeroLevel"), "所需英雄等级");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("effectScale"), "伤害/治疗倍率", "1 表示与 1 级配置数值相同。");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("buffScale"), "Buff 强度倍率");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("cooldownScale"), "冷却/充能倍率", "0.8 表示缩短 20%；不低于 0.05。");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("manaCostScale"), "资源消耗倍率");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(HeroTalentTier))]
    internal sealed class HeroTalentTierChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (!property.isExpanded) return line;
            return line * 2 + AbilityPropertyLayout.FieldHeight(property.FindPropertyRelative("left")) +
                AbilityPropertyLayout.FieldHeight(property.FindPropertyRelative("right"));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            var level = property.FindPropertyRelative("heroLevel");
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, new GUIContent($"{label.text}（{level.intValue} 级）"), true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                AbilityPropertyLayout.Field(ref line, level, "解锁英雄等级");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("left"), "左侧选项（按 1）");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("right"), "右侧选项（按 2）");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(TalentOption))]
    internal sealed class TalentOptionChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            (AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap) * (property.isExpanded ? 4 : 1);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            line.y += AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("displayName"), "显示文字", "留空时按属性和数值自动生成。");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("stat"), "属性");
                AbilityPropertyLayout.Field(ref line, property.FindPropertyRelative("value"), "数值", "百分比类属性填写小数：0.2 表示 20%。");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
