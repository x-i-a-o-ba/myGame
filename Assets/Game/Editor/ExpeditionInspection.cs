using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>把正式 CustomEditor 放进宽窗口，便于策划检查中文字段；不另做一份展示界面。</summary>
    public sealed class ExpeditionInspection:EditorWindow
    {
        static readonly string[] Names={"怪物技能","遗物","操作","普攻","技能","状态","装备"};
        static readonly string[] Paths={"怪物技能/领主扩散火环.asset","遗物/时之砂.asset","操作/战斗键位.asset","普攻/英雄/冰箭普攻.asset",
            "技能/1001_冰霜扇击.asset","Buff/5059_磐石战歌状态.asset","装备/6018_雷鸣锤.asset"};
        UnityEditor.Editor inspector;Vector2 scroll;int selected=-1;
        [MenuItem("Tools/Roguelike/远征/配置字段检查")]
        public static void Open()
        {var window=GetWindow<ExpeditionInspection>();window.titleContent=new GUIContent("远征配置 Inspector");window.minSize=new Vector2(650,700);window.Show();window.Focus();}
        void Select(int index)
        {
            if(inspector) DestroyImmediate(inspector);selected=index;scroll=Vector2.zero;
            var asset=AssetDatabase.LoadAssetAtPath<Object>("Assets/GameData/"+Paths[index]);
            if(!asset) return;Selection.activeObject=asset;inspector=UnityEditor.Editor.CreateEditor(asset);
        }
        void OnGUI()
        {
            int next=GUILayout.Toolbar(Mathf.Max(0,selected),Names);if(next!=selected) Select(next);
            scroll=EditorGUILayout.BeginScrollView(scroll);if(inspector) inspector.OnInspectorGUI();else EditorGUILayout.HelpBox("未找到配置资源。",MessageType.Warning);EditorGUILayout.EndScrollView();
        }
        void OnDisable() {if(inspector) DestroyImmediate(inspector);}
    }
}
