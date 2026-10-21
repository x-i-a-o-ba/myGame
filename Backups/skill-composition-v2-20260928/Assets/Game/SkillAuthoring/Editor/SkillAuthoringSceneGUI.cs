using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>世界单位的 Scene 可视化与参数句柄。所有图形消费共享快照；编辑数值写回唯一技能 SO。</summary>
    [InitializeOnLoad]
    internal static class SkillAuthoringSceneGUI
    {
        private static readonly Dictionary<int, SkillAuthoringPreview> Cache = new Dictionary<int, SkillAuthoringPreview>();
        private static readonly List<Vector3> Points = new List<Vector3>(130);
        private static GUIStyle labelStyle;
        static SkillAuthoringSceneGUI()
        {
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            EditorSceneManager.sceneClosed += _ => Invalidate();
            AssemblyReloadEvents.beforeAssemblyReload += Cache.Clear;
        }
        private static void Invalidate() { Cache.Clear(); SceneView.RepaintAll(); }
        internal static SkillAuthoringPreview Get(SkillAuthoringWorkspace workspace, bool force = false)
        {
            int key = workspace.GetInstanceID();
            if (!Cache.TryGetValue(key, out var value)) Cache.Add(key, value = new SkillAuthoringPreview());
            value.Refresh(workspace.skill, force); return value;
        }

        internal static void Placement(SkillAuthoringWorkspace workspace, in SkillAimPreview skill,
            out Vector3 origin, out Vector3 point, out Vector3 direction, out Vector3 center)
        {
            origin = workspace.caster ? workspace.caster.position : workspace.transform.position;
            Vector3 raw = workspace.target ? workspace.target.position : origin + Vector3.forward * 8;
            point = skill.Aim == AimMode.Self ? origin : (Vector3)SkillTargeting.ClampPoint(origin, raw, skill.CastRange);
            if (skill.Aim == AimMode.Unit) { point = raw; point.y = origin.y; }
            direction = point - origin; direction.y = 0;
            if (direction.sqrMagnitude < 0.00001f)
            {
                direction = workspace.caster ? workspace.caster.forward : Vector3.forward;
                direction.y = 0; if (direction.sqrMagnitude < 0.00001f) direction = Vector3.forward;
            }
            direction.Normalize();
            direction = SkillPreviewGeometry.DeliveryDirection(skill, origin, point, direction);
            Vector3 impact = skill.Delivery == DeliveryKind.LinearProjectile
                ? origin + direction * skill.Range * workspace.impactPreviewFraction : point;
            center = SkillPreviewGeometry.AreaCenter(skill, origin, impact);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawSavedPreview(SkillAuthoringWorkspace workspace, GizmoType gizmoType)
        {
            if (!workspace.showPreview || Application.isPlaying) return;
            var color = Gizmos.color; var matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            try
            {
                Vector3 origin = workspace.caster ? workspace.caster.position : workspace.transform.position;
                if (workspace.showGrid)
                {
                    int half = Mathf.Clamp(workspace.gridHalfExtent,5,100);
                    for (int i = -half; i <= half; i++)
                    {
                        Gizmos.color = i % 5 == 0 ? new Color(0.34f,0.43f,0.53f,0.7f) : new Color(0.2f,0.27f,0.35f,0.5f);
                        Gizmos.DrawLine(origin + new Vector3(i,0.01f,-half), origin + new Vector3(i,0.01f,half));
                        Gizmos.DrawLine(origin + new Vector3(-half,0.01f,i), origin + new Vector3(half,0.01f,i));
                    }
                }
                var preview = Get(workspace); if (!preview.Valid) return;
                int selected = Mathf.Clamp(workspace.stageIndex,0,preview.Stages.Length-1);
                for (int i = 0; i < preview.Stages.Length; i++)
                {
                    if (i != selected && !workspace.showAllStages) continue;
                    var skill = preview.Stages[i];
                    Placement(workspace,skill,out origin,out var point,out var direction,out var center);
                    float height = 0.06f + i * 0.005f;
                    Gizmos.color = skill.IndicatorColor * (i == selected ? 1 : 0.45f);
                    if (i == selected && skill.Aim != AimMode.Self)
                    {
                        Circle(origin + Vector3.up*height, skill.CastRange);
                        Gizmos.DrawLine(origin + Vector3.up*height,point + Vector3.up*height);
                    }
                    if (skill.Delivery == DeliveryKind.LinearProjectile)
                    {
                        SkillPreviewGeometry.ProjectileOutline(skill,origin + Vector3.up*height,direction,Points); Lines();
                        Gizmos.color = new Color(1,0.64f,0.2f,i == selected ? 1 : 0.35f);
                        Gizmos.DrawWireSphere(center + Vector3.up*height,0.15f);
                    }
                    if (skill.Delivery == DeliveryKind.ParabolicProjectile)
                    {
                        // 直接使用世界轨迹，不能再加地面轮廓的显示抬高，否则与真实飞行位置不一致。
                        SkillTrajectory.Sample(skill,origin,point,Points); Lines();
                        Vector3 start=origin+Vector3.up*skill.LaunchHeight;
                        Vector3 end=point+Vector3.up*skill.ArrivalHeight;
                        Vector3 position=(Vector3)SkillTrajectory.Evaluate(start,end,skill.ArcHeight,workspace.flightPreviewProgress);
                        Gizmos.DrawWireSphere(start,0.12f); Gizmos.DrawWireSphere(end,0.12f);
                        Gizmos.DrawSphere(position,0.16f);
                    }
                    SkillPreviewGeometry.AreaOutline(skill,center + Vector3.up*height,direction,Points); Lines();
                    if (skill.Scope == TargetScope.Self) Circle(origin + Vector3.up*height,0.45f);
                    if (skill.Scope == TargetScope.Single) Circle(center + Vector3.up*height,0.45f);
                }
            }
            finally { Gizmos.color = color; Gizmos.matrix = matrix; }
        }
        private static void Circle(Vector3 center, float radius)
        {
            if (radius <= 0) return;
            var circle = new SkillAimPreview { Scope = TargetScope.Area, Shape = AreaShape.Circle, Radius = radius };
            SkillPreviewGeometry.AreaOutline(circle,center,Vector3.forward,Points); Lines();
        }
        private static void Lines() { for(int i=1;i<Points.Count;i++) Gizmos.DrawLine(Points[i-1],Points[i]); }

        internal static void DrawInteractive(SkillAuthoringWorkspace workspace)
        {
            labelRects.Clear();
            if (!workspace.showPreview || Application.isPlaying) return;
            var preview = Get(workspace); if (!preview.Valid) return;
            int selected = Mathf.Clamp(workspace.stageIndex,0,preview.Stages.Length-1);
            var skill = preview.Stages[selected];
            Placement(workspace,skill,out var origin,out var point,out var direction,out var center);
            using (new Handles.DrawingScope(skill.IndicatorColor,Matrix4x4.identity))
            {
                var ztest = Handles.zTest; Handles.zTest = CompareFunction.Always;
                try
                {
                    Label(origin,"施法者 · 1 格 = 1 Unity 单位",20);
                    if (skill.Aim != AimMode.Self) Label(point,$"目标中心 · 距离 {Vector3.Distance(origin,point):0.##}",-24);
                    if (skill.Aim == AimMode.Unit && Vector3.Distance(origin,point) > skill.CastRange)
                        Label(point,"单位超距：运行时拒绝施放",-80);
                    string description = skill.Scope == TargetScope.All ? "全体符合条件的目标（没有有限边界）" :
                        skill.Scope == TargetScope.Self ? "作用于施法者" : skill.Scope == TargetScope.Single ? "单个目标（标记大小不参与命中）" :
                        skill.Shape == AreaShape.Circle ? $"半径 {skill.Radius:0.##} 单位" : skill.Shape == AreaShape.Cone ?
                        $"长度 {skill.Range:0.##} · 半角 {skill.HalfAngleDegrees:0.#}° / 总角 {skill.HalfAngleDegrees*2:0.#}°" :
                        $"长度 {skill.Range:0.##} · 半宽 {skill.Radius:0.##} / 总宽 {skill.Radius*2:0.##}";
                    Label(center,$"阶段 {selected+1} · {description}",-52);
                    if (skill.Delivery == DeliveryKind.LinearProjectile)
                        Label(center,$"首碰后范围示意 · 碰撞半径 {skill.HitRadius:0.##}（不保证在此命中）",20);
                    if (skill.Delivery == DeliveryKind.ParabolicProjectile)
                    {
                        Vector3 start=origin+Vector3.up*skill.LaunchHeight;
                        Vector3 end=point+Vector3.up*skill.ArrivalHeight;
                        Vector3 position=(Vector3)SkillTrajectory.Evaluate(start,end,skill.ArcHeight,workspace.flightPreviewProgress);
                        Label(position,$"飞行示意 {workspace.flightPreviewProgress:P0} · {skill.FlightSeconds*workspace.flightPreviewProgress:0.##}/{skill.FlightSeconds:0.##} 秒",-28);
                        Label(center,"到点结算 · 沿途不碰撞 · 范围按 XZ 平面查询",20);
                    }
                    if (!workspace.editHandles) return;
                    if (workspace.target && skill.Aim != AimMode.Self)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 next = Handles.PositionHandle(workspace.target.position,Quaternion.identity); next.y=origin.y;
                        if (EditorGUI.EndChangeCheck())
                        { Undo.RecordObject(workspace.target,"移动技能预览目标"); workspace.target.position=next; EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene); }
                    }
                    Vector3 side = new Vector3(direction.z,0,-direction.x);
                    if (skill.Aim != AimMode.Self)
                    {
                        Handles.color = new Color(0.5f,0.65f,1);
                        Slider(workspace,"castRange",origin,Vector3.right,skill.CastRange,0,"施法距离");
                    }
                    Handles.color = skill.IndicatorColor;
                    string path = $"stages.Array.data[{selected}].";
                    if (skill.Delivery == DeliveryKind.ParabolicProjectile)
                    {
                        Vector3 start=origin+Vector3.up*skill.LaunchHeight;
                        Vector3 end=point+Vector3.up*skill.ArrivalHeight;
                        // 高度以起终点连线的中点为零，不混入发射高度与落点高度。
                        Slider(workspace,path+"arcHeight",Vector3.Lerp(start,end,0.5f),Vector3.up,skill.ArcHeight,0,"弧线附加高度");
                    }
                    if (skill.Delivery == DeliveryKind.LinearProjectile)
                    {
                        Slider(workspace,path+"range",origin,direction,skill.Range,0.01f,"飞行长度");
                        Slider(workspace,path+"hitRadius",origin+direction*skill.Range*0.25f,side,skill.HitRadius,0.01f,"碰撞半径");
                    }
                    if (skill.Scope != TargetScope.Area) return;
                    if (skill.Shape != AreaShape.Cone) Slider(workspace,path+"radius",center,side,skill.Radius,0.01f,skill.Shape==AreaShape.Circle?"范围半径":"范围半宽");
                    if (skill.Shape != AreaShape.Circle && skill.Delivery != DeliveryKind.LinearProjectile)
                        Slider(workspace,path+"range",center,direction,skill.Range,0.01f,"区域长度");
                    if (skill.Shape == AreaShape.Cone)
                    {
                        Vector3 edge = Quaternion.AngleAxis(skill.HalfAngleDegrees,Vector3.up)*direction;
                        Vector3 handle = center + edge * skill.Range;
                        Label(handle,$"半角 {skill.HalfAngleDegrees:0.#}°",4);
                        EditorGUI.BeginChangeCheck();
                        var next = Handles.Slider2D(handle,Vector3.up,direction,side,HandleUtility.GetHandleSize(handle)*0.085f,Handles.DotHandleCap,0);
                        if (EditorGUI.EndChangeCheck()) SetFloat(workspace.skill,path+"halfAngleDegrees",Mathf.Clamp(Vector3.Angle(direction,next-center),0.1f,180));
                    }
                }
                finally { Handles.zTest = ztest; }
            }
        }
        private static void Slider(SkillAuthoringWorkspace workspace,string path,Vector3 center,Vector3 axis,float value,float minimum,string label)
        {
            Vector3 location=center+axis*value;
            Label(location,$"{label} {value:0.##}",4);
            EditorGUI.BeginChangeCheck();
            Vector3 next=Handles.Slider(location,axis,HandleUtility.GetHandleSize(location)*0.085f,Handles.DotHandleCap,0.25f);
            if(EditorGUI.EndChangeCheck()) SetFloat(workspace.skill,path,Mathf.Max(minimum,Vector3.Dot(next-center,axis)));
        }
        private static readonly System.Collections.Generic.List<Rect> labelRects = new();
        private static void Label(Vector3 world,string text,float offset)
        {
            Handles.BeginGUI();
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.helpBox) {fontSize=12,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(6,6,3,3)};
                labelStyle.normal.textColor=Color.white;
            }
            Vector2 point=HandleUtility.WorldToGUIPoint(world); var content=new GUIContent(text);
            Vector2 size=labelStyle.CalcSize(content);
            var rectangle=new Rect(point.x+12,point.y+offset,size.x,size.y);
            var view=SceneView.currentDrawingSceneView;
            if(view) rectangle.x=Mathf.Clamp(rectangle.x,4,Mathf.Max(4,view.position.width-size.x-4));
            // 屏幕空间避让，目标、进度球与弧高接近时也能读清各自标签。
            for(int attempt=0;attempt<20;attempt++)
            {
                bool overlap=false;
                foreach(var occupied in labelRects) if(rectangle.Overlaps(occupied)) {overlap=true;break;}
                if(!overlap) break;
                rectangle.y-=size.y+4;
            }
            labelRects.Add(rectangle);
            GUI.Label(rectangle,content,labelStyle);
            Handles.EndGUI();
        }
        /// <summary>数值框与场景句柄走同一序列化字段；Unity 自动记录 Undo 并置脏资源。</summary>
        internal static void SetFloat(SkillDefinition skill,string propertyPath,float value)
        {
            var serialized = new SerializedObject(skill); serialized.Update();
            var property=serialized.FindProperty(propertyPath);
            if(property==null) throw new System.InvalidOperationException("不存在的技能字段："+propertyPath);
            property.floatValue=value; serialized.ApplyModifiedProperties(); SceneView.RepaintAll();
        }
    }
}
