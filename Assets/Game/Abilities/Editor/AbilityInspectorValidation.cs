using System;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>在隔离 Editor 中核对中文 Inspector 的注册与枚举显示，不进入游戏。</summary>
    public static class AbilityInspectorValidation
    {
        public static void Validate()
        {
            Inspect<SkillDefinition, SkillDefinitionChineseInspector>();
            Inspect<BuffDefinition, BuffDefinitionChineseInspector>();
            Inspect<HeroDefinition, HeroDefinitionChineseInspector>();
            Inspect<SkillCatalog, SkillCatalogChineseInspector>();
            Inspect<PassiveDefinition, PassiveDefinitionChineseInspector>();
            Inspect<DamageEffect, EffectDefinitionChineseInspector>();
            Inspect<HealEffect, EffectDefinitionChineseInspector>();
            Inspect<ApplyBuffEffect, EffectDefinitionChineseInspector>();
            Inspect<DispelEffect, EffectDefinitionChineseInspector>();
            Inspect<HeroProgressionRules, HeroProgressionRulesChineseInspector>();
            Inspect<EquipmentDefinition, EquipmentDefinitionChineseInspector>();
            Inspect<EquipmentCatalog, EquipmentCatalogChineseInspector>();

            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                skill.stages = new[] { new SkillStage() };
                var serialized = new SerializedObject(skill);
                var aim = serialized.FindProperty("aim");
                if (aim == null || Array.IndexOf(aim.enumDisplayNames, "目标点") < 0)
                    throw new InvalidOperationException("瞄准方式枚举没有显示中文名称。");
                var stage = serialized.FindProperty("stages").GetArrayElementAtIndex(0);
                var drawer = new SkillStageChineseDrawer();
                if (drawer.GetPropertyHeight(stage, new GUIContent("阶段 1")) <= EditorGUIUtility.singleLineHeight)
                    throw new InvalidOperationException("组合阶段缺少展开布局。");
                if (Attribute.GetCustomAttribute(typeof(SkillStageChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(AttributeModifierChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(SkillLevelGrowthChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(SkillLevelRowChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(HeroTalentTierChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(TalentOptionChineseDrawer), typeof(CustomPropertyDrawer)) == null ||
                    Attribute.GetCustomAttribute(typeof(StatModifierChineseDrawer), typeof(CustomPropertyDrawer)) == null)
                    throw new InvalidOperationException("嵌套配置的中文绘制器未注册。");
                var growth = serialized.FindProperty("growth");
                if (growth != null) growth.isExpanded = true;
                if (growth == null || new SkillLevelGrowthChineseDrawer().GetPropertyHeight(growth, new GUIContent("等级成长")) <= EditorGUIUtility.singleLineHeight * 2)
                    throw new InvalidOperationException("技能等级成长缺少展开布局。");
                var track = growth.FindPropertyRelative("track");
                if (track == null || Array.IndexOf(track.enumDisplayNames, "自定义逐级表") < 0)
                    throw new InvalidOperationException("技能成长类型枚举没有显示中文名称。");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }

            var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
            try
            {
                item.stats = new[] { new StatModifier(StatKind.LifeSteal, 10) };
                var serialized = new SerializedObject(item);
                var stat = serialized.FindProperty("stats").GetArrayElementAtIndex(0).FindPropertyRelative("stat");
                if (stat == null || Array.IndexOf(stat.enumDisplayNames, "普攻吸血（%）") < 0)
                    throw new InvalidOperationException("属性枚举没有显示中文名称。");
                var quality = serialized.FindProperty("quality");
                if (quality == null || Array.IndexOf(quality.enumDisplayNames, "传说") < 0)
                    throw new InvalidOperationException("装备品质枚举没有显示中文名称。");
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
            Debug.Log("ABILITY_INSPECTOR_CHINESE_PASSED: twelve inspectors, enum labels, nested drawers.");
        }

        private static void Inspect<TAsset, TEditor>() where TAsset : ScriptableObject where TEditor : UnityEditor.Editor
        {
            var asset = ScriptableObject.CreateInstance<TAsset>();
            UnityEditor.Editor inspector = null;
            try
            {
                inspector = UnityEditor.Editor.CreateEditor(asset);
                if (!(inspector is TEditor))
                    throw new InvalidOperationException(typeof(TAsset).Name + " 未使用指定的中文 Inspector：" + inspector?.GetType().Name);
            }
            finally
            {
                if (inspector) UnityEngine.Object.DestroyImmediate(inspector);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
