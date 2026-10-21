using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Roguelike.SkillIndicators;

namespace Roguelike.Abilities.Editor
{
    /// <summary>独立技能场景的显式搭建工具；存在场景时仅打开，不自动重建或覆盖布局。</summary>
    public static class SkillDemoTools
    {
        public const string ScenePath="Assets/Scenes/SkillArchitectureDemo.unity";
        public static void Open() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
        public static void Create()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var catalog=SkillDemoAssets.Ensure();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Skill Architecture Demo"); var demo=root.AddComponent<SkillDemoController>(); demo.catalog=catalog;
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); camera.tag="MainCamera";
            camera.orthographic=true;camera.orthographicSize=32;camera.farClipPlane=300;
            camera.transform.position=new Vector3(0,65,-45);camera.transform.rotation=Quaternion.Euler(58,0,0);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.018f,0.025f,0.036f);demo.gameCamera=camera;
            demo.enemyMaterial=SkillDemoAssets.Material("Enemy",new Color(0.85f,0.3f,0.2f));
            demo.allyMaterial=SkillDemoAssets.Material("Ally",new Color(0.2f,0.85f,0.4f));
            var ground=Primitive(root.transform,"Training Ground - 140m",PrimitiveType.Plane,SkillDemoAssets.Material("Ground",new Color(0.11f,0.16f,0.19f)));
            ground.transform.localScale=Vector3.one*14;
            var player=Primitive(root.transform,"Player View - scene owned",PrimitiveType.Capsule,SkillDemoAssets.Material("Player",new Color(0.15f,0.7f,1)));
            player.transform.position=Vector3.up;demo.playerView=player.transform;
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);demo.unitMesh=temp.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(temp);
            var views=new GameObject("Skill indicator views - reusable prefabs").transform;views.SetParent(root.transform,false);
            demo.rangeView=Circle(views,"CastRange","Cast Range");demo.targetView=Circle(views,"TargetArea","Aim Area");demo.feedbackView=Circle(views,"CastFeedback","Cast Feedback");
            var regions=new GameObject("Persistent area view slots - 8").transform;regions.SetParent(root.transform,false);
            demo.areaViews=new CircleIndicatorView[8];
            for(int i=0;i<8;i++) demo.areaViews[i]=Circle(regions,"TargetArea","Area Slot "+i);
            var beam=new GameObject("Basic Attack Feedback",typeof(LineRenderer));beam.transform.SetParent(root.transform,false);
            demo.attackBeam=beam.GetComponent<LineRenderer>();demo.attackBeam.positionCount=2;demo.attackBeam.useWorldSpace=true;
            demo.attackBeam.startWidth=demo.attackBeam.endWidth=0.08f;demo.attackBeam.sharedMaterial=SkillDemoAssets.Material("Attack Beam",new Color(1,0.85f,0.3f));demo.attackBeam.enabled=false;
            demo.hud=CreateHud(root.transform);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(root.transform,false);
            root.AddComponent<SkillDemoProbe>();
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
        private static CircleIndicatorView Circle(Transform parent,string prefab,string name)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/SkillIndicatorDemo/Prefabs/"+prefab+".prefab");
            if(!source) throw new Exception("缺少现有技能指示器 Prefab："+prefab);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);go.name=name;go.SetActive(false);return go.GetComponent<CircleIndicatorView>();
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
            go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;return go;
        }
        private static SkillDemoHud CreateHud(Transform root)
        {
            var canvas=new GameObject("Skill Demo HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(root,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            var hud=canvas.AddComponent<SkillDemoHud>();
            var panel=Panel(canvas.transform,"Commands and runtime summary",new Vector2(18,-18),new Vector2(410,700),Vector2.up);
            Text(panel,"Title","技能架构 / ABILITY LAB",new Vector2(16,-10),new Vector2(380,42),25);
            hud.summary=Text(panel,"Summary","启动中…",new Vector2(16,-58),new Vector2(380,190),17);
            string[] labels={"Q 爆裂","W 寒霜区域","E 鼓舞","升级 Q","升级 W","升级 E","常驻被动开关","中毒被动开关","自动普攻开关","目标移动开关","驱散选中/主角","重置","一万目标重置","主角扣血测试"};
            for(int i=0;i<labels.Length;i++)
            {
                var button=Panel(panel,labels[i],new Vector2(16+i%2*193,-260-i/2*47),new Vector2(183,39),Vector2.up);
                button.GetComponent<Image>().color=new Color(0.08f,0.15f,0.2f,1);
                button.gameObject.AddComponent<Button>().targetGraphic=button.GetComponent<Image>();
                var action=button.gameObject.AddComponent<SkillDemoButton>();action.hud=hud;action.command=(SkillDemoCommand)i;
                Text(button,"Caption",labels[i],Vector2.zero,new Vector2(183,39),16).alignment=TextAnchor.MiddleCenter;
            }
            Text(panel,"Instructions","右键移动 · Q/W/E 瞄准\n左键确认/选择 · 右键/Esc 取消\n方向键平移镜头 · 滚轮缩放\n绿色友方目标用于观察加速效果",new Vector2(16,-596),new Vector2(380,95),16);
            var info=Panel(canvas.transform,"Buff inspector",new Vector2(-18,-18),new Vector2(320,510),Vector2.one);
            hud.inspection=Text(info,"Buff details","Buff 状态",new Vector2(16,-16),new Vector2(288,478),18);
            var skills=Panel(canvas.transform,"Skill slots - editable layout",new Vector2(0,18),new Vector2(980,120),new Vector2(0.5f,0));
            hud.skills=Text(skills,"Skill states","技能状态",new Vector2(20,-16),new Vector2(940,96),21);
            hud.panels=new[]{panel,info,skills};return hud;
        }
        private static RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
            go.GetComponent<Image>().color=new Color(0.018f,0.035f,0.055f,0.97f);return rect;
        }
        private static Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf");
            text.text=value;text.color=new Color(0.86f,0.93f,1);text.fontSize=fontSize;text.raycastTarget=false;return text;
        }
        public static void Validate() => AbilityValidation.Validate(SkillDemoAssets.Ensure());
        public static void Build()
        {
            Create();Validate();Directory.CreateDirectory("Builds/SkillArchitectureDemo");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/SkillArchitectureDemo/SkillArchitectureDemo.exe"});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("技能演示构建失败");
            Debug.Log("ABILITY_BUILD_SUCCEEDED");
        }
    }
}
