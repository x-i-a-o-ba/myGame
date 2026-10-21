using System;
using System.IO;
using System.Reflection;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>主任务串行调用的静态编译、编辑事务和实际窗口留证；不进入 Play Mode。</summary>
    public static class RecipeEditorChecks
    {
        public static void MigrateAndValidate() { RecipeAssetMigration.Run(); Validate(); }
        public static void Validate()
        {
            RecipeFoundationChecks.Run();
            RecipeProgramStoreValidation.Run();
            RecipeAuthoringValidation.Validate();
            NumericConfigIds.ValidateAll();
            int count=0;
            foreach(var catalog in RecipeAssetMigration.Assets<SkillCatalog>())
            { var blob=catalog.Compile(); blob.Dispose();count++; }
            Debug.Log("RECIPE_EDITOR_CHECKS_OK catalogs="+count+"; no World, no Play Mode.");
        }
        private static SkillAuthoringWindow window;
        private static RecipeInspectorProbe inspector;
        private static double started;
        private static int phase;
        public static void CaptureAndExit()
        {
            Validate();
            started=EditorApplication.timeSinceStartup;
            EditorApplication.update+=WaitForEditor;
        }
        private static void WaitForEditor()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup-started<8) return;
            EditorApplication.update-=WaitForEditor;
            BeginCapture();
        }
        private static void BeginCapture()
        {
            EditorSceneManager.OpenScene(SkillAuthoringSceneTools.ScenePath);
            var sample=NumericConfigIds.FindSkill(1003);
            Selection.activeObject=sample;
            window=ScriptableObject.CreateInstance<SkillAuthoringWindow>();
            window.ShowUtility();window.position=new Rect(40,40,1320,1050);window.SelectSkill(sample);
            var state=new SerializedObject(window);state.FindProperty("recipePage").intValue=1;state.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Logs/RecipeEditor");
            window.Focus();window.Repaint();started=EditorApplication.timeSinceStartup;phase=0;
            EditorApplication.update+=Tick;
        }
        private static void Tick()
        {
            try
            {
                double elapsed=EditorApplication.timeSinceStartup-started;
                if(phase==0 && elapsed>5)
                {
                    Capture(window,"Logs/RecipeEditor/组合步骤.png");
                    var data=new SerializedObject(window); data.FindProperty("recipePage").intValue=0;data.ApplyModifiedPropertiesWithoutUndo();window.Repaint();phase++;
                }
                else if(phase==1 && elapsed>8)
                {
                    Capture(window,"Logs/RecipeEditor/基础与释放.png");
                    inspector=ScriptableObject.CreateInstance<RecipeInspectorProbe>();
                    inspector.Inspect(NumericConfigIds.FindSkill(1003));
                    inspector.ShowUtility();inspector.position=new Rect(40,40,650,850);
                    inspector.Focus();inspector.Repaint();phase++;
                }
                else if(phase==2 && elapsed>11)
                {
                    Capture(inspector,"Logs/RecipeEditor/技能检查器.png");
                    Selection.activeObject=AssetDatabase.LoadAssetAtPath<SkillModuleCatalog>(RecipeAssetMigration.CatalogPath);
                    inspector.Inspect(Selection.activeObject);
                    var moduleData=new SerializedObject(Selection.activeObject);
                    var deliveries=moduleData.FindProperty("deliveries"); deliveries.isExpanded=true;
                    if(deliveries.arraySize>0) deliveries.GetArrayElementAtIndex(0).isExpanded=true;
                    inspector.Repaint();phase++;
                }
                else if(phase==3 && elapsed>14)
                {
                    Capture(inspector,"Logs/RecipeEditor/模块目录检查器.png");
                    EditorApplication.update-=Tick;window.Close();inspector.Close();
                    Debug.Log("RECIPE_EDITOR_VISUAL_OK; actual EditorWindow and Inspector captured; no Play Mode.");EditorApplication.Exit(0);
                }
            }
            catch(Exception error) { EditorApplication.update-=Tick;Debug.LogException(error);EditorApplication.Exit(1); }
        }
        private static void Capture(EditorWindow target,string path)
        {
            float scale=EditorGUIUtility.pixelsPerPoint;
            var rect=new Rect(0,0,target.position.width*scale,target.position.height*scale);
            var texture=RenderTexture.GetTemporary((int)rect.width,(int)rect.height,0,RenderTextureFormat.ARGB32);
            var corrected=RenderTexture.GetTemporary(texture.descriptor);
            var previous=RenderTexture.active;
            Texture2D image=null;
            try
            {
                var parent=typeof(EditorWindow).GetField("m_Parent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
                parent.GetType().GetMethod("GrabPixels",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(parent,new object[]{texture,rect});
                Graphics.Blit(texture,corrected,(Material)EditorGUIUtility.LoadRequired("SceneView/BlitSceneViewCapture.mat"));
                RenderTexture.active=corrected;image=new Texture2D(corrected.width,corrected.height,TextureFormat.RGB24,false);
                image.ReadPixels(rect,0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally { RenderTexture.active=previous;if(image) UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(texture);RenderTexture.ReleaseTemporary(corrected); }
        }
    }
    /// <summary>直接承载真实 CustomEditor，避免复用被锁定的 Inspector 标签页导致截图目标错误。</summary>
    internal sealed class RecipeInspectorProbe:EditorWindow
    {
        private UnityEditor.Editor editor;
        public void Inspect(UnityEngine.Object target)
        {
            if(editor) UnityEngine.Object.DestroyImmediate(editor);
            editor=UnityEditor.Editor.CreateEditor(target);titleContent=new GUIContent(target.name);Repaint();
        }
        private void OnGUI() {if(editor) editor.OnInspectorGUI();}
        private void OnDisable() {if(editor) UnityEngine.Object.DestroyImmediate(editor);}
    }
}
