using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Roguelike.SkillIndicators.Editor
{
    /// <summary>独立测试场景的首次搭建入口；已有场景只打开，不重建开发者编辑的节点。</summary>
    public static class IndicatorDemoTools
    {
        public const string ScenePath = "Assets/Scenes/SkillIndicatorDemo.unity";
        [MenuItem("Tools/Roguelike/Skill Indicators/Open Demo")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Roguelike/Skill Indicators/Create Demo If Missing")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Skill Indicator Demo").transform;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 19;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f,0.04f,0.055f);
            camera.transform.position = new Vector3(0,28,-20); camera.transform.LookAt(new Vector3(0,0,1.8f));
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 150;
            var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(48,-30,0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f,0.5f,0.56f);
            var environment = new GameObject("Environment - editable test floor").transform;
            environment.SetParent(root,false);
            var floor = Primitive(environment,"Floor",PrimitiveType.Plane,IndicatorDemoAssets.Material("Floor",new Color(0.10f,0.16f,0.19f),false));
            floor.localScale = Vector3.one*6;
            var gridMaterial = IndicatorDemoAssets.Material("Grid",new Color(0.17f,0.25f,0.28f),false);
            for (int i=-14;i<=14;i++)
            {
                var x=Primitive(environment,"Grid X "+i,PrimitiveType.Cube,gridMaterial);
                x.position=new Vector3(i*2,0.006f,0); x.localScale=new Vector3(0.018f,0.01f,56);
                var z=Primitive(environment,"Grid Z "+i,PrimitiveType.Cube,gridMaterial);
                z.position=new Vector3(0,0.006f,i*2); z.localScale=new Vector3(56,0.01f,0.018f);
            }
            var caster=new GameObject("Caster Origin - stationary demo").transform;
            caster.SetParent(root,false); caster.position=new Vector3(-4,0,0);
            var body=Primitive(caster,"Hero placeholder",PrimitiveType.Capsule,IndicatorDemoAssets.Material("Hero",new Color(0.16f,0.57f,0.72f),false));
            body.localPosition=Vector3.up; body.localScale=new Vector3(0.8f,1,0.8f);
            var facing=Primitive(caster,"Facing",PrimitiveType.Cube,IndicatorDemoAssets.Material("Facing",new Color(0.9f,0.75f,0.36f),false));
            facing.localPosition=new Vector3(0,1.2f,0.7f); facing.localScale=new Vector3(0.15f,0.15f,0.7f);
            var views=new GameObject("Indicators - prefab instances").transform; views.SetParent(root,false);
            var range=Instance(IndicatorDemoAssets.CirclePrefab("CastRange",false,false,false),views,"Cast Range");
            range.transform.position=caster.position+Vector3.up*0.04f; range.transform.localScale=Vector3.one*10;
            var area=Instance(IndicatorDemoAssets.CirclePrefab("TargetArea",true,true,false),views,"Target Area");
            area.transform.position=new Vector3(3,0.05f,3); area.transform.localScale=Vector3.one*2.5f;
            var feedback=Instance(IndicatorDemoAssets.CirclePrefab("CastFeedback",false,true,true),views,"Cast Feedback");
            feedback.SetActive(false);
            var feedbackController=new GameObject("Feedback Player").AddComponent<IndicatorFeedback>();
            feedbackController.transform.SetParent(root,false);
            var serialized=new SerializedObject(feedbackController);
            IndicatorDemoHudBuilder.Bind(serialized,"ring",feedback.GetComponent<CircleIndicatorView>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            IndicatorDemoHudBuilder.Create(root);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)); events.transform.SetParent(root,false);
            IndicatorDemoAssets.Preset("Q - 精准落点",10,2.5f,new Color(0.15f,0.85f,1));
            IndicatorDemoAssets.Preset("W - 扩大范围",8,4,new Color(0.35f,0.95f,0.68f));
            IndicatorDemoAssets.Preset("E - 远距投放",14,1.5f,new Color(0.8f,0.6f,1));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,ScenePath);
            Debug.Log("INDICATOR_VISUAL_SCENE_CREATED");
        }

        private static GameObject Instance(GameObject prefab,Transform parent,string name)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            instance.name=name; return instance;
        }
        private static Transform Primitive(Transform parent,string name,PrimitiveType type,Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
