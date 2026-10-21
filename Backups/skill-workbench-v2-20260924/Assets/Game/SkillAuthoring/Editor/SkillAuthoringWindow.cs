using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>固定工作台引用，允许在 Project 中查看效果时继续编辑技能；关闭时释放内嵌 Inspector。</summary>
    public sealed class SkillAuthoringWindow : EditorWindow
    {
        [SerializeField] private SkillAuthoringWorkspace workspace;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        [MenuItem("Tools/Roguelike/技能/显示技能配置面板")]
        public static void Open()
        {
            var window = GetWindow<SkillAuthoringWindow>("技能配置工作台");
            window.minSize = new Vector2(410, 400); window.Show();
        }
        private void OnEnable() { titleContent = new GUIContent("技能配置工作台"); Undo.undoRedoPerformed += Repaint; }
        private void OnDisable() { Undo.undoRedoPerformed -= Repaint; if (inspector) DestroyImmediate(inspector); }
        private void OnGUI()
        {
            if (!workspace) workspace = Object.FindFirstObjectByType<SkillAuthoringWorkspace>();
            workspace = (SkillAuthoringWorkspace)EditorGUILayout.ObjectField("工作台节点", workspace, typeof(SkillAuthoringWorkspace), true);
            if (!workspace)
            {
                EditorGUILayout.HelpBox("先打开技能配置工作台场景。", MessageType.Info);
                if (GUILayout.Button("打开技能配置工作台")) SkillAuthoringSceneTools.Open();
                return;
            }
            if (GUILayout.Button("选中工作台并聚焦 Scene 视图"))
            {
                Selection.activeGameObject = workspace.gameObject;
                var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
                view.LookAt(workspace.caster ? workspace.caster.position : Vector3.zero, Quaternion.Euler(65,0,0), 24, false, true);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            UnityEditor.Editor.CreateCachedEditor(workspace, typeof(SkillAuthoringWorkspaceEditor), ref inspector);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
