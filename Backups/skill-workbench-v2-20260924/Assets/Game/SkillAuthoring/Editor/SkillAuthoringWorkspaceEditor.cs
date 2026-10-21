using System;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>工作台中文 Inspector。内嵌既有技能 Inspector，避免新增第二套技能字段模型。</summary>
    [CustomEditor(typeof(SkillAuthoringWorkspace))]
    internal sealed class SkillAuthoringWorkspaceEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor skillEditor;
        private string notice;
        private bool noticeError;
        private void OnDisable() { if (skillEditor) DestroyImmediate(skillEditor); }
        public override void OnInspectorGUI()
        {
            var workspace = (SkillAuthoringWorkspace)target;
            EditorGUILayout.HelpBox("编辑态技能工作台 · 1 Unity 单位 = 1 世界坐标单位\n在 Scene 视图拖动控制柄即可修改技能；Ctrl+Z 撤销。场景锚点与技能资源分别保存。", MessageType.Info);
            if (Application.isPlaying) EditorGUILayout.HelpBox("请退出运行模式后编辑和保存技能配置。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                serializedObject.Update();
                Field("skill", "编辑技能"); Field("catalog", "登记目标技能库", "登记是显式操作；切换预览不会自动修改技能库。");
                Field("caster", "施法者锚点"); Field("target", "目标/单位锚点");
                var stageProperty = serializedObject.FindProperty("stageIndex");
                int stageCount = workspace.skill && workspace.skill.stages != null ? workspace.skill.stages.Length : 0;
                if (stageCount > 0)
                {
                    var names = new string[stageCount];
                    for (int i=0;i<stageCount;i++) names[i] = "阶段 " + (i+1);
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.Popup("当前预览阶段",Mathf.Clamp(stageProperty.intValue,0,stageCount-1),names);
                    if (EditorGUI.EndChangeCheck()) stageProperty.intValue = next;
                }
                Field("showAllStages", "叠加显示全部阶段"); Field("showPreview", "显示范围预览");
                Field("editHandles", "启用范围拖动"); Field("showGrid", "显示一单位网格"); Field("gridHalfExtent", "网格半边长（Unity 单位）");
                Field("impactPreviewFraction", "首碰示意位置（路径比例）", "只移动爆炸范围示意，不改变技能，不代表真实碰撞。");
                bool changed = serializedObject.ApplyModifiedProperties();
                if (changed) SceneView.RepaintAll();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新建技能")) Run(() => SkillAuthoringAssets.CreateForWorkspace(workspace, false));
                    using (new EditorGUI.DisabledScope(!workspace.skill))
                        if (GUILayout.Button("复制为新技能")) Run(() => SkillAuthoringAssets.CreateForWorkspace(workspace, true));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("校验当前技能")) Run(() =>
                    {
                        var preview = SkillAuthoringSceneGUI.Get(workspace, true);
                        if (!preview.Valid) throw new InvalidOperationException(preview.Error);
                        notice = "单技能与引用依赖编译通过。登记到技能库时还会校验全库 ID 与引用。";
                    });
                    if (GUILayout.Button("登记到当前技能库")) Run(() => { SkillAuthoringAssets.Register(workspace); notice = "已登记并通过全库编译；请保存资源。"; });
                }
                if (GUILayout.Button("保存技能、引用依赖与工作场景", GUILayout.Height(27))) Run(() => { SkillAuthoringAssets.Save(workspace); notice = "技能资源、引用依赖和工作场景已保存。"; });
                if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, noticeError ? MessageType.Error : MessageType.Info);
                if (!workspace.caster || !workspace.target) EditorGUILayout.HelpBox("请绑定施法者和目标锚点。", MessageType.Error);
                if (workspace.skill && workspace.skill.aim == AimMode.Unit && workspace.caster && workspace.target &&
                    Vector2.Distance(new Vector2(workspace.caster.position.x,workspace.caster.position.z),new Vector2(workspace.target.position.x,workspace.target.position.z)) > workspace.skill.castRange)
                    EditorGUILayout.HelpBox("单位目标超出施法距离，运行时会拒绝施放；目标锚点保留实际位置。", MessageType.Warning);
                if (workspace.transform.lossyScale != Vector3.one || (workspace.caster && workspace.caster.lossyScale != Vector3.one))
                    EditorGUILayout.HelpBox("范围按世界坐标绘制，节点缩放不会乘入技能半径。建议工作台保持 (1,1,1)。", MessageType.Warning);
                var snapshot = SkillAuthoringSceneGUI.Get(workspace);
                if (!snapshot.Valid) EditorGUILayout.HelpBox("配置草稿：" + snapshot.Error, MessageType.Warning);
                else
                {
                    int selected = Mathf.Clamp(workspace.stageIndex, 0, snapshot.Stages.Length - 1);
                    var stage = workspace.skill.stages[selected];
                    EditorGUILayout.LabelField($"预览阶段 {selected + 1}/{snapshot.Stages.Length} · 延迟 {stage.delay:g} 秒 · {stage.repeatCount} 次 · 间隔 {stage.repeatInterval:g} 秒", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.HelpBox("Game 瞄准使用第 1 阶段。这里的多阶段叠加是静态配置检查；直线弹体爆炸位置为示意。效果/Buff 引用是共享资源，复制技能不会复制其效果。", MessageType.None);
                }
                if (workspace.skill)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("技能配置（立即更新预览；Game 重开会话后生效）", EditorStyles.boldLabel);
                    UnityEditor.Editor.CreateCachedEditor(workspace.skill, null, ref skillEditor);
                    EditorGUI.BeginChangeCheck(); skillEditor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) { SkillAuthoringSceneGUI.Get(workspace, true); SceneView.RepaintAll(); }
                }
            }
        }
        private void Field(string name, string label, string tooltip = null) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip), true);
        private void Run(Action action)
        {
            try { notice = null; noticeError = false; action(); SceneView.RepaintAll(); }
            catch (Exception exception) { notice = exception.Message; noticeError = true; }
        }
        private void OnSceneGUI() => SkillAuthoringSceneGUI.DrawInteractive((SkillAuthoringWorkspace)target);
    }
}
