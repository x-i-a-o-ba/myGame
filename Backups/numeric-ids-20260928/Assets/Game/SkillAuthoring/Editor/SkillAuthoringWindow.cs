using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>技能资源是编辑权威；窗口选择与场景预览独立，关闭窗口不会删除配置。</summary>
    public sealed class SkillAuthoringWindow : EditorWindow
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private SkillCatalog catalog;
        [SerializeField] private SkillAuthoringWorkspace workspace;
        [SerializeField] private int page, stageIndex;
        [SerializeField] private SkillTemplateKind template = SkillTemplateKind.ArcExplosion;
        private string search = "", newName = "新技能", notice;
        private bool failed;
        private Vector2 libraryScroll, formScroll;
        private readonly List<SkillDefinition> library = new();
        private readonly SkillAuthoringPreview preview = new();
        private SkillAuthoringPanels panels;
        private static readonly string[] Pages = { "基础信息", "瞄准与范围", "飞行方式", "效果组合", "多段与节奏", "高级 / 预览" };
        [MenuItem("Tools/Roguelike/技能设计器")]
        public static void Open() { var w = GetWindow<SkillAuthoringWindow>("技能设计器"); w.minSize = new Vector2(880, 600); w.Show(); }
        public static void Edit(SkillDefinition value) { Open(); GetWindow<SkillAuthoringWindow>().SelectSkill(value); }
        [OnOpenAsset] private static bool OpenAsset(int id, int line)
        { if (EditorUtility.InstanceIDToObject(id) is not SkillDefinition value) return false; Edit(value); return true; }
        internal void SelectSkill(SkillDefinition value) { skill = value; stageIndex = 0; notice = null; BindPreview(); Repaint(); }
        private void OnEnable()
        {
            titleContent = new GUIContent("技能设计器"); panels = new SkillAuthoringPanels();
            RefreshLibrary(); Undo.undoRedoPerformed += Changed; EditorApplication.projectChanged += RefreshLibrary;
            workspace = UnityEngine.Object.FindFirstObjectByType<SkillAuthoringWorkspace>();
            if (!skill) skill = Selection.activeObject as SkillDefinition;
            if (workspace) { if (!skill) skill = workspace.skill; if (!catalog) catalog = workspace.catalog; }
        }
        private void OnDisable() { Undo.undoRedoPerformed -= Changed; EditorApplication.projectChanged -= RefreshLibrary; panels?.Dispose(); }
        private void RefreshLibrary() { library.Clear(); foreach (var id in AssetDatabase.FindAssets("t:SkillDefinition")) { var s = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(id)); if(s) library.Add(s); } library.Sort((a,b)=>string.Compare(a.displayName,b.displayName,StringComparison.Ordinal)); Repaint(); }
        private void Changed() { if (skill) preview.Refresh(skill,true); SceneView.RepaintAll(); Repaint(); }
        private void Run(Action action) { try { failed = false; notice = null; action(); Changed(); } catch (Exception e) { failed = true; notice = e.Message; } }
        private void BindPreview()
        {
            if (!workspace || !skill || (workspace.skill == skill && workspace.stageIndex == stageIndex)) return;
            Undo.RecordObject(workspace,"切换技能预览"); workspace.skill = skill; workspace.stageIndex = stageIndex;
            EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene); SceneView.RepaintAll();
        }
        private void OnGUI()
        {
            if (panels == null) panels = new SkillAuthoringPanels();
            EditorGUILayout.LabelField("技能设计器", new GUIStyle(EditorStyles.boldLabel){fontSize=20}, GUILayout.Height(30));
            EditorGUILayout.LabelField("选模板 → 定范围 → 选飞行 → 拼效果 → 保存    ·    1 Unity 单位 = 1 世界坐标单位", EditorStyles.miniLabel);
            if (Application.isPlaying) EditorGUILayout.HelpBox("请退出运行模式后编辑配置。",MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("打开场景预览",EditorStyles.toolbarButton)) Run(()=>{ var selected=skill; SkillAuthoringSceneTools.Open(); workspace=UnityEngine.Object.FindFirstObjectByType<SkillAuthoringWorkspace>(); if(selected) SelectSkill(selected); });
                    using(new EditorGUI.DisabledScope(!skill))
                    {
                        if(GUILayout.Button("复制为新技能",EditorStyles.toolbarButton)) Run(()=>{ var path=EditorUtility.SaveFilePanelInProject("复制技能",skill.name+"副本","asset","选择保存位置"); if(path.Length>0) { SelectSkill(SkillAuthoringAssets.CreateAtPath(path,skill)); notice="已复制技能；效果仍为共享资源，可在效果页制作独立副本。"; } });
                        if(GUILayout.Button("校验配置",EditorStyles.toolbarButton)) Run(()=>{ preview.Refresh(skill,true); if(!preview.Valid) throw new InvalidOperationException(preview.Error); notice="技能及引用依赖编译通过。"; });
                        if(GUILayout.Button("保存配置",EditorStyles.toolbarButton)) Save();
                    }
                    GUILayout.FlexibleSpace();
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    DrawLibrary();
                    using(new EditorGUILayout.VerticalScope())
                    {
                        var selected=(SkillDefinition)EditorGUILayout.ObjectField("当前技能",skill,typeof(SkillDefinition),false);
                        if(selected!=skill) SelectSkill(selected);
                        if(skill) DrawSkill(); else EditorGUILayout.HelpBox("从左侧选择已有技能，或选择模板创建第一个技能。不需要打开工作台场景。",MessageType.Info);
                    }
                }
            }
            if(!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice,failed?MessageType.Error:MessageType.Info);
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.S && (Event.current.control||Event.current.command) && skill && !Application.isPlaying) { Save(); Event.current.Use(); }
        }
        private void Save() => Run(()=>{ SkillAuthoringAssets.Save(skill,catalog); notice="技能及效果/Buff 依赖已保存；场景锚点请在预览页单独保存。"; });
        private void DrawLibrary()
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox,GUILayout.Width(215)))
            {
                EditorGUILayout.LabelField("技能库",EditorStyles.boldLabel);
                search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
                GUI.Label(GUILayoutUtility.GetLastRect(),new GUIContent(string.Empty,"按技能名称或唯一 ID 搜索"),GUIStyle.none);
                libraryScroll=EditorGUILayout.BeginScrollView(libraryScroll,GUILayout.MinHeight(120));
                foreach(var item in library)
                {
                    string label=string.IsNullOrWhiteSpace(item.displayName)?item.name:item.displayName;
                    if(!label.Contains(search,StringComparison.OrdinalIgnoreCase) &&
                       !(item.stableId ?? string.Empty).Contains(search,StringComparison.OrdinalIgnoreCase)) continue;
                    var style=new GUIStyle(EditorStyles.miniButton){alignment=TextAnchor.MiddleLeft,wordWrap=true};
                    if(GUILayout.Toggle(item==skill,label,style,GUILayout.MinHeight(29)) && item!=skill) SelectSkill(item);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(); EditorGUILayout.LabelField("从模板开始",EditorStyles.boldLabel);
                template=(SkillTemplateKind)EditorGUILayout.EnumPopup(template);
                EditorGUILayout.HelpBox(SkillAuthoringWorkflow.TemplateDescription(template),MessageType.None);
                newName=EditorGUILayout.TextField(newName);
                if(GUILayout.Button("创建技能资源",GUILayout.Height(30))) Run(()=>{ var path=EditorUtility.SaveFilePanelInProject("创建技能",string.IsNullOrWhiteSpace(newName)?"新技能":newName,"asset","配置保存在项目中，关闭窗口后仍可继续编辑。"); if(path.Length>0) { SelectSkill(SkillAuthoringWorkflow.CreateTemplate(path,template)); RefreshLibrary(); } });
            }
        }
        private void DrawSkill()
        {
            EditorGUILayout.LabelField(SkillAuthoringPanels.Summary(skill),EditorStyles.wordWrappedLabel,GUILayout.MinHeight(30));
            page=GUILayout.Toolbar(page,Pages,GUILayout.Height(29));
            int count=skill.stages?.Length??0;
            if(count>0)
            {
                var names=new string[count]; for(int i=0;i<count;i++) names[i]="阶段 "+(i+1);
                int next=EditorGUILayout.Popup("正在编辑的阶段",Mathf.Clamp(stageIndex,0,count-1),names);
                if(next!=stageIndex) { stageIndex=next; BindPreview(); }
            }
            else EditorGUILayout.HelpBox("这是旧版兼容技能。请使用模板创建正式组合技能，避免直接改动旧数据。",MessageType.Warning);
            formScroll=EditorGUILayout.BeginScrollView(formScroll);
            float oldLabelWidth=EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth=260;
            try
            {
                panels.Draw(skill,stageIndex,page,Run,i=>{stageIndex=i; BindPreview();},Changed);
                if(page==5) DrawPreview();
            }
            finally {EditorGUIUtility.labelWidth=oldLabelWidth;}
            EditorGUILayout.EndScrollView();
            preview.Refresh(skill);
            EditorGUILayout.HelpBox(preview.Valid?"配置可编译 · 修改会立即更新场景预览；Game 重开会话后生效。":"配置待修正："+preview.Error,preview.Valid?MessageType.None:MessageType.Warning);
        }
        private void DrawPreview()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("技能库登记",EditorStyles.boldLabel);
            catalog=(SkillCatalog)EditorGUILayout.ObjectField("目标技能库",catalog,typeof(SkillCatalog),false);
            using(new EditorGUI.DisabledScope(!catalog)) if(GUILayout.Button("登记当前技能到技能库")) Run(()=>{SkillAuthoringAssets.Register(skill,catalog);notice="已登记并校验全库。请保存配置。";});
            EditorGUILayout.Space(); EditorGUILayout.LabelField("场景预览（不影响命中结算）",EditorStyles.boldLabel);
            workspace=(SkillAuthoringWorkspace)EditorGUILayout.ObjectField("工作台节点",workspace,typeof(SkillAuthoringWorkspace),true);
            if(!workspace) { EditorGUILayout.HelpBox("点击顶部“打开场景预览”，在三维 Scene 视图拖动目标与范围控制柄。",MessageType.Info); return; }
            if(GUILayout.Button("将当前技能同步到场景并选中工作台")) { BindPreview(); Selection.activeGameObject=workspace.gameObject; }
            var so=new SerializedObject(workspace); so.Update();
            foreach(var pair in new[]{("showPreview","显示范围"),("showAllStages","叠加全部阶段"),("editHandles","允许拖动修改范围"),("showGrid","显示一单位网格"),("flightPreviewProgress","抛物线飞行进度（0～1）"),("impactPreviewFraction","直线首碰示意比例")}) EditorGUILayout.PropertyField(so.FindProperty(pair.Item1),new GUIContent(pair.Item2));
            if(so.ApplyModifiedProperties()) SceneView.RepaintAll();
            if(GUILayout.Button("保存工作台场景与锚点")) Run(()=>{if(!EditorSceneManager.SaveScene(workspace.gameObject.scene)) throw new InvalidOperationException("场景未保存。");notice="工作台场景已保存。";});
        }
    }
}

