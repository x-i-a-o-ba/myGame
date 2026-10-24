using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    [CustomPropertyDrawer(typeof(EquipmentProc))]
    internal sealed class EquipmentProcDrawer:PropertyDrawer
    {
        static readonly (string key,string label)[] Fields={
            ("trigger","触发时机"),("chance","触发概率（0–1）"),("radius","查询半径 / 每跳距离（米）"),
            ("maxTargets","最多目标数"),("chain","逐目标传递（不重复）"),("includePrimary","包含主普攻目标"),("effects","共享效果")};
        public override float GetPropertyHeight(SerializedProperty property,GUIContent label)
        {
            float height=EditorGUIUtility.singleLineHeight+3;
            if(property.isExpanded) foreach(var field in Fields) height+=EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field.key),true)+3;
            return height;
        }
        public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);position.height=EditorGUIUtility.singleLineHeight;
            property.isExpanded=EditorGUI.Foldout(position,property.isExpanded,label,true);
            if(property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach(var field in Fields)
                {position.y+=position.height+3;var p=property.FindPropertyRelative(field.key);position.height=EditorGUI.GetPropertyHeight(p,true);EditorGUI.PropertyField(position,p,new GUIContent(field.label),true);}
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
