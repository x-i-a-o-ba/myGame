using System;
using System.IO;
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
        public static void CaptureAndExit()
        {
            SkillAuthoringSceneTools.EnsureScene();
            var scene=EditorSceneManager.OpenScene(SkillAuthoringSceneTools.ScenePath);
            Selection.activeGameObject=SkillAuthoringSceneTools.Find(scene).gameObject;
            Directory.CreateDirectory("Logs/SkillAuthoring");
            view=ScriptableObject.CreateInstance<SceneView>();view.ShowUtility();view.position=new Rect(30,50,1120,820);
            view.drawGizmos=true;view.sceneLighting=false;view.LookAt(new Vector3(3,0,3),Quaternion.Euler(75,0,0),19,false,true);
            window=ScriptableObject.CreateInstance<SkillAuthoringWindow>();window.ShowUtility();window.position=new Rect(1160,50,620,940);
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
                    EditorApplication.update-=Tick;window.Close();view.Close();
                    Debug.Log("SKILL_AUTHORING_VISUAL_CAPTURED: SceneView and Chinese workbench GUI.");EditorApplication.Exit(0);
                }
            }
            catch(Exception exception)
            {EditorApplication.update-=Tick;Debug.LogException(exception);EditorApplication.Exit(1);}
        }
        private static void CaptureWindow(EditorWindow target,string path)
        {
            var rect=target.position;
            int width=Mathf.RoundToInt(rect.width),height=Mathf.RoundToInt(rect.height);
            var pixels=InternalEditorUtility.ReadScreenPixel(rect.position,width,height);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{image.SetPixels(pixels);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(image);}
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
