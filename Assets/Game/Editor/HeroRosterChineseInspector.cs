using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    [CustomEditor(typeof(HeroRoster))]
    internal sealed class HeroRosterChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("按钮按下列顺序选择英雄并重开本局。重开会清空等级、金币、装备与技能状态；调整列表时须同步按钮绑定。",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heroes"),new GUIContent("可选英雄（按钮顺序）"),true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"),new GUIContent("战斗控制器"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
