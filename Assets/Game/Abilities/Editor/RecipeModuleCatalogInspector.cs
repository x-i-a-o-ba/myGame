using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>模块维护入口的中文展示；不改机器字段和数字模块身份。</summary>
    [CustomEditor(typeof(SkillModuleCatalog))]
    internal sealed class RecipeModuleCatalogInspector:AbilityChineseInspector
    {
        public override void OnInspectorGUI()
        {
            Begin();
            EditorGUILayout.HelpBox("目录定义可复用积木。ID 一经使用请勿更改；参数边界用于编译检查。技能组合请使用技能设计器。Buff 强度/持续时间覆盖本版不开放。",MessageType.Info);
            Field("catalogVersion","目录版本");
            Array("deliveries","投递模块","投递"); Array("queries","目标查询模块","查询");
            Array("effects","效果模块","效果"); Array("buffs","Buff 定义","Buff"); End();
        }
    }
    [CustomPropertyDrawer(typeof(DeliveryModule))]
    [CustomPropertyDrawer(typeof(QueryModule))]
    [CustomPropertyDrawer(typeof(EffectModule))]
    internal sealed class RecipeModuleDrawer:PropertyDrawer
    {
        private static readonly (string,string)[] Labels={
            ("id","模块 ID"),("displayName","模块名称"),("playerAllowed","允许玩家使用"),("kind","投递方式"),
            ("scope","目标范围"),("shape","区域形状"),("targets","目标关系"),("definition","效果资源"),
            ("maxRange","最大距离（单位）"),("maxRadius","最大半径（单位）"),
            ("maxSpeed","速度上限（单位/秒）"),("maxFlightSeconds","飞行时长上限（秒）"),("maxArcHeight","最大弧高（单位）"),
            ("allowAmountOverride","允许覆盖数值"),("minAmount","覆盖数值下限"),("maxAmount","覆盖数值上限")};
        public override float GetPropertyHeight(SerializedProperty property,GUIContent label)
        { int count=1; if(property.isExpanded) foreach(var item in Labels) if(property.FindPropertyRelative(item.Item1)!=null) count++; return count*(EditorGUIUtility.singleLineHeight+3); }
        public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
        {
            position.height=EditorGUIUtility.singleLineHeight;
            property.isExpanded=EditorGUI.Foldout(position,property.isExpanded,label,true);
            if(!property.isExpanded) return;
            EditorGUI.indentLevel++;
            foreach(var item in Labels) {var p=property.FindPropertyRelative(item.Item1);if(p==null)continue;position.y+=position.height+3;EditorGUI.PropertyField(position,p,new GUIContent(item.Item2,"距离/高度的 1 单位 = 1 Unity 世界坐标单位。上限在编译配方时校验；数值覆盖仅适用于伤害或治疗。"));}
            EditorGUI.indentLevel--;
        }
    }
}
