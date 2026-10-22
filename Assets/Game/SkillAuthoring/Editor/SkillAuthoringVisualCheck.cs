using System;
using System.IO;
using System.Reflection;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>仅供隔离图形 Editor 调用：打开真实 Scene/工作台窗口并留存屏幕证据，完成后退出该验证 Editor。</summary>
    public static class SkillAuthoringVisualCheck
    {
        private static SceneView view;
        private static SkillAuthoringWindow window;
        private static double started;
        private static int phase;
        private static bool timingView;
        public static void CaptureTimingAndExit() { timingView=true; CaptureAndExit(); }
        public static void CaptureAndExit()
        {
            SkillAuthoringSceneTools.EnsureScene();
            var scene=EditorSceneManager.OpenScene(SkillAuthoringSceneTools.ScenePath);
            var workspace=SkillAuthoringSceneTools.Find(scene);
            var sample=SkillAuthoringWorkflow.CreateTemplate("Assets/GameData/编辑示例/技能工作台/抛物线编辑验证.asset",SkillTemplateKind.ArcExplosion);
            if(timingView) { sample.castTime=0.6f; sample.recoveryTime=0.4f; EditorUtility.SetDirty(sample); }
            workspace.skill=sample; workspace.target.position=new Vector3(9,0,5); Selection.activeGameObject=workspace.gameObject;
            Directory.CreateDirectory("Logs/SkillAuthoring");
            view=ScriptableObject.CreateInstance<SceneView>();view.ShowUtility();view.position=new Rect(30,50,1120,820);
            view.drawGizmos=true;view.sceneLighting=false;view.LookAt(new Vector3(3,0,3),Quaternion.Euler(45,0,0),15,false,true);
            window=ScriptableObject.CreateInstance<SkillAuthoringWindow>();window.ShowUtility();window.position=new Rect(50,50,1180,880);
            window.SelectSkill(sample);
            var windowData=new SerializedObject(window); windowData.FindProperty("page").intValue=timingView?0:2; windowData.ApplyModifiedPropertiesWithoutUndo();
            view.Focus();view.Repaint();window.Repaint();
            started=EditorApplication.timeSinceStartup;phase=0;EditorApplication.update+=Tick;
        }
        private static void Tick()
        {
            double elapsed=EditorApplication.timeSinceStartup-started;
            try
            {
                if(phase==0 && elapsed>5)
                {
                    view.Focus();view.Repaint();phase=1;
                }
                else if(phase==1 && elapsed>8)
                {
                    CaptureWindow(view,"Logs/SkillAuthoring/scene-view.png");
                    if(view.camera.targetTexture) CaptureTexture(view.camera.targetTexture,"Logs/SkillAuthoring/scene-render.png");
                    window.Focus();window.Repaint();phase=2;
                }
                else if(phase==2 && elapsed>11)
                {
                    CaptureWindow(window,"Logs/SkillAuthoring/workbench-panel.png");
                    var state=new SerializedObject(window); state.FindProperty("page").intValue=timingView?4:3; state.ApplyModifiedPropertiesWithoutUndo(); window.Repaint(); phase=3;
                }
                else if(phase==3 && elapsed>14)
                {
                    CaptureWindow(window,"Logs/SkillAuthoring/effects-panel.png");
                    EditorApplication.update-=Tick;window.Close();view.Close();
                    Debug.Log("SKILL_AUTHORING_VISUAL_CAPTURED: SceneView and Chinese workbench GUI.");EditorApplication.Exit(0);
                }
            }
            catch(Exception exception)
            {EditorApplication.update-=Tick;Debug.LogException(exception);EditorApplication.Exit(1);}
        }
        private static void CaptureWindow(EditorWindow target,string path)
        {
            float scale=EditorGUIUtility.pixelsPerPoint;
            var rectangle=new Rect(0,0,target.position.width*scale,target.position.height*scale);
            var texture=RenderTexture.GetTemporary((int)rectangle.width,(int)rectangle.height,0,RenderTextureFormat.ARGB32);
            var corrected=RenderTexture.GetTemporary(texture.descriptor);
            try
            {
                var parent=typeof(EditorWindow).GetField("m_Parent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
                var grab=parent.GetType().GetMethod("GrabPixels",BindingFlags.NonPublic|BindingFlags.Instance);
                grab.Invoke(parent,new object[]{texture,rectangle});
                var material=(Material)EditorGUIUtility.LoadRequired("SceneView/BlitSceneViewCapture.mat");
                Graphics.Blit(texture,corrected,material);CaptureTexture(corrected,path);
            }
            finally {RenderTexture.ReleaseTemporary(texture);RenderTexture.ReleaseTemporary(corrected);}
        }
        private static void CaptureTexture(RenderTexture texture,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;
            var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}





