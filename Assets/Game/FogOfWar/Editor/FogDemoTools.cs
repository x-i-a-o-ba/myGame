using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Roguelike.FogOfWar.Editor
{
    /// <summary>首次搭建并保存场景/预制体。已存在场景不重建，防止覆盖人工修改。</summary>
    public static class FogDemoTools
    {
        public const string ScenePath="Assets/Scenes/FogOfWarDemo.unity";
        private const string Root="Assets/Game/FogOfWar";
        public static void Open() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
        public static void Create()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/Prefabs");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Fog of War Demo");
            var session=root.AddComponent<FogDemoSession>();
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=32;
            camera.transform.position=new Vector3(-15,60,-60); camera.transform.rotation=Quaternion.Euler(58,0,0);
            camera.farClipPlane=300; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.01f,0.015f,0.02f);
            session.gameCamera=camera;
            var terrainMaterial=Material("Terrain",new Color(0.35f,0.5f,0.43f),true);
            var cliffMaterial=Material("High Ground",new Color(0.65f,0.54f,0.34f),true);
            var treeMaterial=Material("Tree",new Color(0.16f,0.36f,0.18f),true);
            session.blueMaterial=Material("Blue Unit",new Color(0.15f,0.8f,1),false);
            session.redMaterial=Material("Red Unit",new Color(1,0.2f,0.12f),false);
            var floor=Primitive(root.transform,"Terrain - 128m",PrimitiveType.Plane,terrainMaterial);
            floor.transform.localScale=Vector3.one*12.8f;
            var high=Primitive(root.transform,"High Ground Region",PrimitiveType.Cube,cliffMaterial);
            high.transform.position=new Vector3(3,1.5f,6); high.transform.localScale=new Vector3(18,3,20);
            var region=high.AddComponent<FogHeightRegion>(); region.size=new Vector2(18,20); region.level=1;
            var forest=new GameObject("Trees - destructible and remembered").transform; forest.SetParent(root.transform,false);
            for(int i=0;i<15;i++)
            {
                var tree=Primitive(forest,"Tree "+i,PrimitiveType.Cylinder,treeMaterial);
                tree.transform.position=new Vector3(-14,1.5f,-25+i*3); tree.transform.localScale=new Vector3(1.6f,1.5f,1.6f);
                var obstacle=tree.AddComponent<FogObstacle>(); obstacle.visual=tree.GetComponent<Renderer>();
            }
            var prefab=EnemyVisionPrefab(session.redMaterial);
            session.blueScout=Source(root.transform,"Blue Scout",new Vector3(-28,0,-10),0,session.blueMaterial);
            session.redScout=((GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform)).GetComponent<VisionSource>();
            session.redScout.name="Red Scout - 敌人视野预制体实例"; session.redScout.transform.position=new Vector3(22,0,12);
            session.detector=Source(root.transform,"True Sight Emitter",new Vector3(-28,0,-10),0,Material("Detector",Color.yellow,false));
            session.detector.dayRadius=session.detector.nightRadius=0; session.detector.trueSightRadius=14; session.detector.providesVision=false;
            var wards=new GameObject("Ward slots - reusable scene objects").transform; wards.SetParent(root.transform,false);
            session.wardSlots=new VisionSource[8];
            for(int i=0;i<8;i++)
            {
                session.wardSlots[i]=Source(wards,"Ward Slot "+i,Vector3.zero,0,Material("Ward",new Color(0.9f,0.65f,0.2f),false));
                session.wardSlots[i].gameObject.SetActive(false);
            }
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube); session.targetMesh=temporary.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.DestroyImmediate(temporary);
            session.hud=CreateHud(root.transform);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(root.transform,false);
            root.AddComponent<FogDemoProbe>();
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        }
        private static GameObject EnemyVisionPrefab(Material material)
        {
            string path=Root+"/Prefabs/敌人视野.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing) return existing;
            var source=Source(null,"敌人视野",Vector3.zero,1,material);
            var prefab=PrefabUtility.SaveAsPrefabAsset(source.gameObject,path);
            UnityEngine.Object.DestroyImmediate(source.gameObject); return prefab;
        }
        private static VisionSource Source(Transform parent,string name,Vector3 position,int team,Material material)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.position=position;
            var source=obj.AddComponent<VisionSource>(); source.team=team;
            var marker=Primitive(obj.transform,"Demo marker - replace or remove",PrimitiveType.Capsule,material);
            marker.transform.localPosition=Vector3.up; marker.transform.localScale=new Vector3(0.9f,1,0.9f);
            return source;
        }
        private static Material Material(string name,Color color,bool fog)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path); if(material) return material;
            material=new Material(Shader.Find(fog?"Roguelike/FogDemo/WorldVisibility":"Universal Render Pipeline/Unlit")){enableInstancing=true};
            material.SetColor("_BaseColor",color); AssetDatabase.CreateAsset(material,path); return material;
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; return go;
        }
        private static FogDemoHud CreateHud(Transform parent)
        {
            var canvas=new GameObject("Fog Demo HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0.5f;
            var hud=canvas.AddComponent<FogDemoHud>();
            var panel=Panel(canvas.transform,"Debug controls",new Vector2(18,-18),new Vector2(425,640),Vector2.up);
            Label(panel,"Title","战争迷雾 / VISION LAB",new Vector2(16,-10),new Vector2(395,42),26);
            hud.status=Label(panel,"Status","启动中…",new Vector2(16,-60),new Vector2(395,164),17);
            string[] captions={"切换阵营","切换昼夜","自动昼夜","地面 / 飞行","敌人视野开关","真视开关","目标0隐身开关","砍树 / 恢复","放侦察眼 30s","放真视眼 30s","清除本方眼","临时飞行视野 3s","未探索黑雾","重置","总览 / 近景","调试全知"};
            for(int i=0;i<captions.Length;i++)
            {
                var button=Panel(panel,captions[i],new Vector2(16+(i%2)*200,-235-(i/2)*46),new Vector2(190,39),Vector2.up);
                button.gameObject.AddComponent<Button>().targetGraphic=button.GetComponent<Image>();
                var action=button.gameObject.AddComponent<FogDemoButton>(); action.hud=hud; action.command=(FogCommand)i;
                Label(button,"Caption",captions[i],Vector2.zero,new Vector2(190,39),16).alignment=TextAnchor.MiddleCenter;
            }
            var mapPanel=Panel(canvas.transform,"Minimap - filtered",new Vector2(-18,-18),new Vector2(280,316),Vector2.one);
            Label(mapPanel,"Label","阵营可见小地图",new Vector2(12,-4),new Vector2(256,32),18);
            var map=new GameObject("Map",typeof(RectTransform),typeof(RawImage)); map.transform.SetParent(mapPanel,false);
            var rect=map.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=new Vector2(12,-42); rect.sizeDelta=new Vector2(256,256);
            hud.minimap=map.GetComponent<RawImage>();
            var selected=Panel(canvas.transform,"Selected target information",new Vector2(0,18),new Vector2(780,76),new Vector2(0.5f,0));
            hud.selection=Label(selected,"Safe target data","隐藏目标不可选择",new Vector2(16,-8),new Vector2(748,60),19);
            hud.blockingPanels=new[]{panel,mapPanel,selected}; return hud;
        }
        private static RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); var rect=go.GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=rect.pivot=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            go.GetComponent<Image>().color=new Color(0.025f,0.055f,0.07f,0.96f); return rect;
        }
        private static Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf");
            text.text=value; text.color=new Color(0.8f,0.9f,0.92f); text.fontSize=fontSize; text.raycastTarget=false; return text;
        }
        public static void Build()
        {
            Create(); UpgradeSmoothing(); FogValidation.Validate(); Directory.CreateDirectory("Builds/FogOfWarDemo");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/FogOfWarDemo/FogOfWarDemo.exe"});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Fog Demo build failed");
            Debug.Log("FOG_BUILD_SUCCEEDED");
        }
        public static void UpgradeSmoothing()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<FogDemoSession>();
            if(!session) throw new Exception("请先打开 FogOfWarDemo 场景。");
            string path=Root+"/Materials/Fog Smoothing.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                material=new Material(Shader.Find("Hidden/Roguelike/FogSmoothing"));
                AssetDatabase.CreateAsset(material,path);
            }
            // 只补充新增引用，保留开发者调整过的相机、布局、参数和对象。
            if(!session.fogSmoothingMaterial)
            {
                session.fogSmoothingMaterial=material;
                EditorUtility.SetDirty(session);
                EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
                EditorSceneManager.SaveScene(session.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
