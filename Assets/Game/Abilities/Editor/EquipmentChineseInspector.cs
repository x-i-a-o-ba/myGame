using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>一条属性加成的单行绘制：左侧属性、右侧数值。数值直接相加，百分比类属性填写百分点（25 = +25%）。</summary>
    [CustomPropertyDrawer(typeof(StatModifier))]
    internal sealed class StatModifierChineseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            AbilityPropertyLayout.Line + AbilityPropertyLayout.Gap;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, AbilityPropertyLayout.Line);
            var content = EditorGUI.PrefixLabel(line, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float valueWidth = Mathf.Min(90, content.width * 0.35f);
            EditorGUI.PropertyField(new Rect(content.x, content.y, content.width - valueWidth - 4, content.height),
                property.FindPropertyRelative("stat"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(content.xMax - valueWidth, content.y, valueWidth, content.height),
                property.FindPropertyRelative("value"), GUIContent.none);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(EquipmentDefinition))]
    [CanEditMultipleObjects]
    internal sealed class EquipmentDefinitionChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            Section("身份");
            Field("configId", "装备 ID（数字编号）", "装备使用 6000–6999，并在 Docs/游戏配置/编号登记.json 登记（category 为 item）；0 表示尚未分配。");
            Field("stableId", "内部兼容键", "英文小写、数字与下划线，例如 item.iron_sword；资源改名后保持不变。");
            Field("displayName", "装备名称");
            Field("icon", "图标", "HUD 装备栏与商店卡片显示；为空时显示装备名称。");
            Field("quality", "品质", "只影响图标边框颜色与提示文字，不改变数值。");
            Section("商店");
            Field("price", "价格（金币）", "出售按装备库的出售返还比例结算。");
            Field("unique", "唯一", "同一件装备最多持有一件。");
            Field("description", "说明");
            Section("属性加成（直接相加，不做换算）");
            Array("stats", "属性加成", "加成", "左侧选属性、右侧填数值；百分比类属性填写百分点（25 = +25%），可以为负数。最多 8 条。");
            Section("普攻命中效果");
            Array("onHitEffects", "命中效果", "效果", "复用技能效果资源（例如“施加扇击减速”）；作用对象为“施法者”时作用于英雄自身。引用的 Buff 必须登记在技能库中。最多 4 个。");
            End();
            if (targets.Length == 1)
            {
                string failure = ((EquipmentDefinition)target).Validate();
                if (failure != null) EditorGUILayout.HelpBox(failure, MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(EquipmentCatalog))]
    [CanEditMultipleObjects]
    internal sealed class EquipmentCatalogChineseInspector : AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            Section("商店与装备栏（重开后生效）");
            Field("startingGold", "开局金币");
            Field("sellRatio", "出售返还比例", "0.5 表示返还购买价格的 50%（向下取整）。");
            Field("slotCount", "装备栏格数", "HUD 最多显示 6 格。");
            Section("丢弃与拾取（重开后生效）");
            Field("dropRange", "丢弃距离（米）", "把装备拖到地面后，英雄走到落点这个距离内才放下；Dota 2 为 100 码，约 3.3 米。");
            Field("pickupRange", "拾取距离（米）", "右键地上的装备后，英雄走到这个距离内才拾取；Dota 2 为 150 码，约 5 米。");
            Array("items", "出售的装备", "装备", "按此顺序显示在商店中（每页 12 件）。新增装备：新建“装备定义”资源后加入这里。");
            End();
            if (targets.Length == 1)
            {
                var catalog = (EquipmentCatalog)target;
                string failure = catalog.Validate();
                if (failure != null) EditorGUILayout.HelpBox(failure, MessageType.Warning);
                else EditorGUILayout.HelpBox($"共 {catalog.items.Length} 件装备，配置校验通过（命中效果的 Buff 登记在开局编译时检查）。", MessageType.Info);
            }
        }
    }
}
