using System;
using System.IO;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>显式首次搭建编辑工作场景；重复调用只打开原场景，不覆盖策划布局和资源。</summary>
    public static class SkillAuthoringSceneTools
    {
        public const string ScenePath = "Assets/Scenes/技能配置工作台.unity";
        public const string ContentRoot = "Assets/Game/SkillAuthoring/Content";
        [MenuItem("Tools/Roguelike/技能/打开技能配置工作台")]
        public static void Open()
        {
            if (Application.isPlaying) { Debug.LogWarning("请先退出运行模式再打开技能工作场景。"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureScene();
            var scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var workspace = Find(scene);
            Selection.activeGameObject = workspace.gameObject;
            SkillAuthoringWindow.Open();
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.LookAt(new Vector3(3,0,3),Quaternion.Euler(65,0,0),22,false,true);
            view.drawGizmos = true;
        }

        public static void EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) return;
            if (File.Exists(ScenePath)) throw new IOException("场景路径存在但无法加载，已停止以保留原文件："+ScenePath);
            Folder("Assets/Scenes"); Folder(ContentRoot);
            string samplePath=ContentRoot+"/范围技能示例.asset";
            var sample=AssetDatabase.LoadAssetAtPath<SkillDefinition>(samplePath);
            if (!sample) sample=SkillAuthoringAssets.CreateAtPath(samplePath);
            string catalogPath=ContentRoot+"/工作台技能库.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>(catalogPath);
            if (!catalog)
            {
                catalog=ScriptableObject.CreateInstance<SkillCatalog>(); catalog.name="工作台技能库";
                catalog.skills=new[]{sample}; catalog.buffs=SkillAuthoringPreview.CollectBuffs(sample).ToArray();
                AssetDatabase.CreateAsset(catalog,catalogPath);
            }
            Scene previous=SceneManager.GetActiveScene();
            bool replaceCleanUntitled=string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            if (string.IsNullOrEmpty(previous.path) && previous.isDirty)
                throw new InvalidOperationException("请先保存当前未命名场景，再创建技能工作台，避免丢失现有编辑。");
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceCleanUntitled?NewSceneMode.Single:NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root=new GameObject("技能配置工作台"); root.tag="EditorOnly";
                var setup=Node("00_编辑装配",root.transform);
                var world=Node("01_世界参照",root.transform);
                var presentation=Node("02_相机与灯光",root.transform);
                var workspace=setup.AddComponent<SkillAuthoringWorkspace>();
                workspace.skill=sample; workspace.catalog=catalog;
                workspace.caster=Node("施法者锚点",world.transform).transform;
                workspace.target=Node("目标锚点（可拖动）",world.transform).transform;
                workspace.target.position=new Vector3(7,0,5);
                var floor=Primitive("工作平面（XZ）",PrimitiveType.Plane,world.transform,Vector3.zero,new Vector3(8,1,8));
                floor.GetComponent<Renderer>().sharedMaterial=Material("工作平面",new Color(0.045f,0.065f,0.09f));
                var reference=Primitive("1×1×1 Unity 单位参照",PrimitiveType.Cube,world.transform,new Vector3(-3,0.5f,0),Vector3.one);
                reference.GetComponent<Renderer>().sharedMaterial=Material("单位参照",new Color(0.92f,0.92f,0.88f));
                var caster=Primitive("施法者参照",PrimitiveType.Cylinder,workspace.caster,Vector3.up*0.75f,new Vector3(0.7f,0.75f,0.7f));
                caster.GetComponent<Renderer>().sharedMaterial=Material("施法者参照",new Color(0.12f,0.62f,0.83f));
                var target=Primitive("目标中心参照",PrimitiveType.Cube,workspace.target,Vector3.up*0.2f,Vector3.one*0.4f);
                target.GetComponent<Renderer>().sharedMaterial=Material("目标参照",new Color(1,0.6f,0.15f));
                var camera=Node("预览相机",presentation.transform).AddComponent<Camera>();
                camera.transform.position=new Vector3(15,25,-20); camera.transform.LookAt(new Vector3(3,0,4));
                camera.orthographic=true; camera.orthographicSize=20; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(0.025f,0.035f,0.05f); camera.nearClipPlane=0.1f; camera.farClipPlane=200;
                var light=Node("预览主光",presentation.transform).AddComponent<Light>();
                light.type=LightType.Directional; light.intensity=1.2f; light.transform.rotation=Quaternion.Euler(55,-25,0);
                RenderSettings.ambientLight=new Color(0.5f,0.55f,0.6f);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("技能配置工作台场景保存失败。");
            }
            finally
            {
                if (!replaceCleanUntitled)
                {
                    EditorSceneManager.CloseScene(scene,true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
            Debug.Log("技能配置工作台已创建："+ScenePath);
        }
        internal static SkillAuthoringWorkspace Find(Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                var result=root.GetComponentInChildren<SkillAuthoringWorkspace>(true); if(result) return result;
            }
            throw new InvalidOperationException("场景中缺少技能配置工作台组件："+scene.path);
        }
        private static GameObject Node(string name,Transform parent)
        { var node=new GameObject(name); node.transform.SetParent(parent,false); return node; }
        private static GameObject Primitive(string name,PrimitiveType kind,Transform parent,Vector3 local,Vector3 scale)
        {
            var node=GameObject.CreatePrimitive(kind); node.name=name; node.transform.SetParent(parent,false);
            node.transform.localPosition=local; node.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(node.GetComponent<Collider>()); return node;
        }
        private static Material Material(string name,Color color)
        {
            string path=ContentRoot+"/"+name+".mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(!shader) throw new InvalidOperationException("未找到 URP Lit 着色器。");
            var material=new Material(shader){name=name}; material.SetColor("_BaseColor",color);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        private static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
