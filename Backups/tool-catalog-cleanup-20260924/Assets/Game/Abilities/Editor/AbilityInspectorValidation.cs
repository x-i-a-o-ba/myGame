using System;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>在隔离 Editor 中核对中文 Inspector 的注册与枚举显示，不进入游戏。</summary>
    public static class AbilityInspectorValidation
    {
        [MenuItem("Tools/Roguelike/技能/检查中文 Inspector")]
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
                    Attribute.GetCustomAttribute(typeof(AttributeModifierChineseDrawer), typeof(CustomPropertyDrawer)) == null)
                    throw new InvalidOperationException("嵌套配置的中文绘制器未注册。");
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
            Debug.Log("ABILITY_INSPECTOR_CHINESE_PASSED: nine inspectors, enum labels, nested drawers.");
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
