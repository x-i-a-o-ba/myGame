using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>商店位置（GameShop）的中文 Inspector：商店范围半径（米）与商店中心说明。</summary>
    [CustomEditor(typeof(GameShop))]
    [CanEditMultipleObjects]
    internal sealed class GameShopChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Dota 2 式固定商店：英雄走进地面金色圆圈（以本物体为中心）后才能打开商店、购买与出售，离开范围自动关闭商店。"+
                "移动本物体即可移动商店；子物体是商店模型，只做展示，不阻挡移动。重开 Game 后生效。",MessageType.Info);
            EditorGUILayout.LabelField("商店范围",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"),
                new GUIContent("商店范围半径（米）","英雄与商店中心的平面距离不超过该值时才能打开商店、购买与出售（"+GameShop.MinimumRadius+"–"+GameShop.MaximumRadius+" 米）。"));
            serializedObject.ApplyModifiedProperties();
            if(targets.Length==1)
            {
                var shop=(GameShop)target;
                EditorGUILayout.HelpBox($"商店中心：x {shop.Position.x:0.##}，z {shop.Position.z:0.##}（米）；生效半径 {shop.Radius:0.##} 米。",MessageType.None);
            }
        }
    }
}
