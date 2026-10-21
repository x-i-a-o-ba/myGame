using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>分类表单只编辑正式定义；文件与组合命令统一由 Workflow 执行。</summary>
    internal sealed class SkillAuthoringPanels : IDisposable
    {
        private readonly Dictionary<UnityEngine.Object,UnityEditor.Editor> editors=new();
        private readonly HashSet<UnityEngine.Object> editing=new();
        private EffectPreset preset;
        private EffectDefinition existing;
        internal static string Summary(SkillDefinition s)
        {
            if(s.stages==null||s.stages.Length==0) return "旧版兼容技能";
            var a=s.stages[0]; if(a==null) return "第 1 阶段为空，请修复配置";
            string delivery=a.delivery switch {DeliveryKind.Instant=>"立即生效",DeliveryKind.PointProjectile=>"飞向目标点",DeliveryKind.LinearProjectile=>"首碰爆炸",_=>"三维抛物线"};
            string scope=a.scope switch {TargetScope.Self=>"自身",TargetScope.Single=>"单体",TargetScope.All=>"全体",_=>a.shape switch {AreaShape.Circle=>"圆形范围",AreaShape.Cone=>"扇形范围",_=>"矩形范围"}};
            return $"{s.displayName}  ·  {delivery} → {scope} → {a.effects?.Length??0} 个效果  ·  共 {s.stages.Length} 阶段";
        }
        public void Dispose() { foreach(var e in editors.Values) if(e) UnityEngine.Object.DestroyImmediate(e); editors.Clear(); }
        internal void Draw(SkillDefinition skill,int index,int page,Action<Action> run,Action<int> select,Action changed)
        {
            var so=new SerializedObject(skill); so.Update();
            int count=skill.stages?.Length??0; index=Mathf.Clamp(index,0,Math.Max(0,count-1));
            if(count>0 && skill.stages[index]==null) { EditorGUILayout.HelpBox("当前阶段为空，需要修复后继续编辑。",MessageType.Error); if(GUILayout.Button("修复为空白阶段")) run(()=>{Undo.RecordObject(skill,"修复空阶段");skill.stages[index]=new SkillStage();EditorUtility.SetDirty(skill);});return; }
            var stage=count>0?so.FindProperty("stages").GetArrayElementAtIndex(index):null;
            SerializedProperty P(string key)=>stage.FindPropertyRelative(key);
            void F(string key,string label)=>EditorGUILayout.PropertyField(so.FindProperty(key),new GUIContent(label));
            void S(string key,string label)=>EditorGUILayout.PropertyField(P(key),new GUIContent(label));
            if(page==0)
            {
                Title("这个技能叫什么？"); F("displayName","技能名称"); F("indicatorColor","范围指示器颜色");
                Title("施放代价"); F("cooldown","冷却时间（秒）"); F("manaCost","资源消耗（点）");
                Title("施放动作时间"); F("castTime","释放前摇 / 释放时间（秒）"); F("recoveryTime","释放后摇（秒）");
                EditorGUILayout.HelpBox("前摇结束才释放技能，随后进入后摇；两者期间禁止移动、普攻和再次施法。阶段延迟从前摇结束计算，后摇不等待弹体落地。0 秒表示无对应动作等待。",MessageType.Info);
                EditorGUILayout.HelpBox("资源与冷却从接受施法时计算。前摇中被禁止施法状态打断会取消本次释放，不返还消耗；已释放的效果不因后摇结束或受控撤回。",MessageType.None);
                EditorGUILayout.HelpBox("技能由多个阶段组成，每个阶段独立选择范围、投递和效果。先完成一个阶段，再按需要添加。",MessageType.Info);
            }
            else if(page==5)
            {
                Title("高级施放条件"); F("maxCharges","最大充能次数"); F("rechargeSeconds","单次充能恢复（秒；0 使用冷却）"); F("requiredHits","需要普攻命中次数（0 不要求）");
                Title("程序标识"); F("stableId","稳定 ID（全库唯一）"); EditorGUILayout.HelpBox("改名请修改技能名称。稳定 ID 用于资源关联，通常不需要修改。",MessageType.None);
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(skill),EditorStyles.miniLabel,GUILayout.Height(30));
            }
            else if(stage!=null)
            {
                var value=skill.stages[index];
                if(page==1)
                {
                    Title("怎么选择目标？"); F("aim","瞄准方式"); F("castRange","最大施法距离（Unity 单位）");
                    Title("影响谁？"); S("scope","目标范围"); S("targets","目标关系");
                    if((TargetScope)P("scope").enumValueIndex==TargetScope.Area)
                    {
                        S("shape","区域形状"); var shape=(AreaShape)P("shape").enumValueIndex;
                        if(shape==AreaShape.Circle) S("radius","范围半径（Unity 单位）");
                        else { S("range","区域长度（Unity 单位）"); if(shape==AreaShape.Line) P("radius").floatValue=EditorGUILayout.FloatField("完整宽度（Unity 单位）",P("radius").floatValue*2)*.5f; else P("halfAngleDegrees").floatValue=EditorGUILayout.Slider("完整张角（度）",P("halfAngleDegrees").floatValue*2,0,360)*.5f; }
                    }
                    EditorGUILayout.HelpBox("范围与命中筛选当前采用 XZ 地面平面；三维飞行高度不会变成球形伤害范围。自身/单体/全体无需设置区域形状。",MessageType.None);
                }
                else if(page==2)
                {
                    Title("效果怎么到达目标？"); S("delivery","投递方式"); var d=(DeliveryKind)P("delivery").enumValueIndex;
                    string tip=d switch { DeliveryKind.Instant=>"在阶段开始时立即结算。",DeliveryKind.PointProjectile=>"直线飞向施放时的目标点，到达后结算；途中不拦截。",DeliveryKind.LinearProjectile=>"沿瞄准方向飞行，命中第一名有效敌人后结算；路径终点未命中则消失。",_=>"按设定时长沿三维弧线飞向施放时的落点，到达后结算一次；途中不拦截、不追踪移动目标。"};
                    EditorGUILayout.HelpBox(tip,MessageType.Info);
                    if(d==DeliveryKind.ParabolicProjectile) { S("flightSeconds","完整飞行时间（秒）"); S("arcHeight","连线以上附加弧高（Unity 单位）"); S("launchHeight","发射高度偏移（Unity 单位）"); S("arrivalHeight","落点高度偏移（Unity 单位）"); EditorGUILayout.HelpBox("这是可控的抛物线轨迹，不是物理重力模拟。打开场景预览可拖动弧高；在高级 / 预览页拖动飞行进度。",MessageType.None); }
                    else if(d!=DeliveryKind.Instant) S("speed","飞行速度（Unity 单位 / 秒）");
                    if(d==DeliveryKind.LinearProjectile) { S("range","最大飞行距离（Unity 单位）"); S("hitRadius","弹体碰撞半径（Unity 单位）"); }
                }
                else if(page==4)
                {
                    Title("当前阶段的时间安排"); S("delay","距前摇结束的延迟（秒）"); S("repeatCount","执行次数"); if(P("repeatCount").intValue>1) S("repeatInterval","重复间隔（秒）");
                    EditorGUILayout.HelpBox("每个阶段的延迟都从前摇结束、正式释放时计算，排序不会自动串成顺序动画。后摇独立于阶段和飞行时长。Game 瞄准显示第 1 阶段；多段叠加预览用于配置检查。",MessageType.Info);
                }
            }
            if(so.ApplyModifiedProperties()) changed();
            if(stage==null) return;
            if(page==3)
            {
                Effects(skill,index,false,run);
                if(skill.stages[index].delivery==DeliveryKind.LinearProjectile || (skill.stages[index].impactEffects?.Length??0)>0) { EditorGUILayout.Space(); if(skill.stages[index].delivery!=DeliveryKind.LinearProjectile) EditorGUILayout.HelpBox("当前投递不支持首碰效果，请移除下面的残留引用，或切回直线弹体。",MessageType.Warning); Effects(skill,index,true,run); }
            }
            if(page==4)
            {
                Title("管理阶段");
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("添加空白阶段")) run(()=>select(SkillAuthoringWorkflow.AddStage(skill)));
                    if(GUILayout.Button("复制当前阶段")) run(()=>select(SkillAuthoringWorkflow.DuplicateStage(skill,index)));
                    using(new EditorGUI.DisabledScope(count<=1)) if(GUILayout.Button("删除当前阶段")) run(()=>{SkillAuthoringWorkflow.RemoveStage(skill,index); select(Math.Min(index,skill.stages.Length-1));});
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    using(new EditorGUI.DisabledScope(index==0)) if(GUILayout.Button("阶段前移")) run(()=>{SkillAuthoringWorkflow.MoveStage(skill,index,-1);select(index-1);});
                    using(new EditorGUI.DisabledScope(index>=count-1)) if(GUILayout.Button("阶段后移")) run(()=>{SkillAuthoringWorkflow.MoveStage(skill,index,1);select(index+1);});
                }
            }
        }
        private void Effects(SkillDefinition skill,int stage,bool impact,Action<Action> run)
        {
            Title(impact?"首个碰撞目标效果":"选定目标 / 范围效果");
            EditorGUILayout.HelpBox(impact?"仅作用于直线弹体首碰单位；随后仍会结算范围效果，重复放伤害会叠加。":"按列表顺序执行。效果资源可以复用；要只修改当前技能，请先制作独立副本。",MessageType.None);
            var effects=impact?skill.stages[stage].impactEffects:skill.stages[stage].effects;
            for(int i=0;i<(effects?.Length??0);i++)
            {
                int at=i; var effect=effects[i];
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("效果 "+(i+1)+"（引用）",effect,typeof(EffectDefinition),false);
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        using(new EditorGUI.DisabledScope(i==0)) if(GUILayout.Button("上移")) {run(()=>SkillAuthoringWorkflow.MoveEffect(skill,stage,impact,at,-1));return;}
                        using(new EditorGUI.DisabledScope(i==effects.Length-1)) if(GUILayout.Button("下移")) {run(()=>SkillAuthoringWorkflow.MoveEffect(skill,stage,impact,at,1));return;}
                        if(GUILayout.Button("移除引用")) {run(()=>SkillAuthoringWorkflow.RemoveEffect(skill,stage,impact,at));return;}
                        using(new EditorGUI.DisabledScope(!effect)) if(GUILayout.Button("制作独立副本")) {run(()=>editing.Add(SkillAuthoringWorkflow.MakeEffectIndependent(skill,stage,impact,at)));return;}
                    }
                    if(!effect) {EditorGUILayout.HelpBox("缺少效果资源，请移除空引用或添加效果。",MessageType.Warning);continue;}
                    bool edit=editing.Contains(effect); bool next=EditorGUILayout.ToggleLeft("展开并编辑此效果资源（所有引用者都会受影响）",edit);
                    if(next) editing.Add(effect); else editing.Remove(effect);
                    if(next) DrawEditor(effect);
                }
            }
            using(new EditorGUILayout.HorizontalScope()) { preset=(EffectPreset)EditorGUILayout.EnumPopup("添加效果",preset); if(GUILayout.Button("新建并添加",GUILayout.Width(115))) run(()=>editing.Add(SkillAuthoringWorkflow.AddEffect(skill,stage,impact,preset))); }
            using(new EditorGUILayout.HorizontalScope()) { existing=(EffectDefinition)EditorGUILayout.ObjectField("复用已有",existing,typeof(EffectDefinition),false); using(new EditorGUI.DisabledScope(!existing)) if(GUILayout.Button("添加引用",GUILayout.Width(115))) run(()=>SkillAuthoringWorkflow.AddExistingEffect(skill,stage,impact,existing)); }
        }
        private void DrawEditor(UnityEngine.Object target)
        {
            if(!editors.TryGetValue(target,out var editor)||!editor) {editor=UnityEditor.Editor.CreateEditor(target);editors[target]=editor;}
            EditorGUI.BeginChangeCheck(); editor.OnInspectorGUI(); if(EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            var so=new SerializedObject(target); var buff=so.FindProperty("buff");
            if(buff!=null && buff.objectReferenceValue) {EditorGUILayout.LabelField("引用的 Buff（同样为共享资源）",EditorStyles.boldLabel);DrawEditor(buff.objectReferenceValue);}
        }
        private static void Title(string text) {EditorGUILayout.Space(9);EditorGUILayout.LabelField(text,EditorStyles.boldLabel);}
    }
}


