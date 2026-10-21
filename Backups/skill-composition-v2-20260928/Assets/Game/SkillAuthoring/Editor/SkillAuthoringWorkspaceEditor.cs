using UnityEditor;
using UnityEngine;
namespace Roguelike.SkillAuthoring.Editor
{
    [CustomEditor(typeof(SkillAuthoringWorkspace))]
    internal sealed class SkillAuthoringWorkspaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var w=(SkillAuthoringWorkspace)target;
            EditorGUILayout.HelpBox("1 Unity 单位 = 1 世界坐标单位。技能配置请在分类设计器中编辑；这里管理场景预览。",MessageType.Info);
            if(GUILayout.Button("打开分类技能设计器",GUILayout.Height(35))) SkillAuthoringWindow.Edit(w.skill);
            if(Application.isPlaying) {EditorGUILayout.HelpBox("退出运行模式后可编辑配置。",MessageType.Info);return;}
            serializedObject.Update();
            foreach(var pair in new[]{("skill","预览技能"),("catalog","目标技能库"),("caster","施法者锚点"),("target","目标锚点"),("stageIndex","预览阶段索引（从 0 开始）"),("showPreview","显示范围"),("editHandles","允许拖动修改范围"),("showGrid","显示一单位网格"),("flightPreviewProgress","抛物线飞行进度（0～1）")}) EditorGUILayout.PropertyField(serializedObject.FindProperty(pair.Item1),new GUIContent(pair.Item2));
            if(serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll();
            if(w.skill) EditorGUILayout.LabelField(SkillAuthoringPanels.Summary(w.skill),EditorStyles.wordWrappedLabel);
        }
        private void OnSceneGUI()=>SkillAuthoringSceneGUI.DrawInteractive((SkillAuthoringWorkspace)target);
    }
}

