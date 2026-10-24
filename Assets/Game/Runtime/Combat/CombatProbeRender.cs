using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>仅验证探针持有的离屏渲染目标。显式提交真实 URP 帧并等待 GPU，隐藏窗口也不能跳过渲染；销毁时恢复 Canvas。</summary>
    internal sealed class CombatProbeRender : IDisposable
    {
        public Camera Camera {get;}
        private readonly RenderTexture target;
        private readonly List<(Canvas canvas,RenderMode mode,Camera camera,float distance)> canvases=new();
        public CombatProbeRender(Camera source,bool overview)
        {
            var go=new GameObject("验证用渲染相机"); Camera=go.AddComponent<Camera>(); Camera.CopyFrom(source); Camera.enabled=false;
            Camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            if(overview)
            {
                Camera.orthographicSize=160;
                var rotation=Quaternion.Euler(58,0,0);
                Camera.transform.SetPositionAndRotation(rotation*new Vector3(0,0,-300),rotation);
            }
            target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32); target.Create(); Camera.targetTexture=target;
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if(!canvas.isRootCanvas) continue;
                canvases.Add((canvas,canvas.renderMode,canvas.worldCamera,canvas.planeDistance));
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=Camera; canvas.planeDistance=1;
            }
            Canvas.ForceUpdateCanvases();
        }
        public void Render()
        {
            RenderPipeline.SubmitRenderRequest(Camera,new RenderPipeline.StandardRequest{destination=target});
            // 只读回一个像素作为 GPU 完成栅栏，计入墙钟时间，不把 CPU 仅提交命令的耗时冒充完成帧耗时。
            var request=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1); request.WaitForCompletion();
            if(request.hasError) throw new InvalidOperationException("压力验证 GPU 读回失败，不能报告渲染帧耗时。");
        }
        public bool Save(string path)
        {
            var previous=RenderTexture.active; var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                Render(); RenderTexture.active=target; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
                Color first=texture.GetPixel(0,0); bool varied=false;
                for(int y=30;y<1080;y+=16) for(int x=20;x<1920;x+=16)
                    if(((Vector4)(texture.GetPixel(x,y)-first)).sqrMagnitude>0.002f) varied=true;
                System.IO.File.WriteAllBytes(path,texture.EncodeToPNG()); return varied;
            }
            finally { RenderTexture.active=previous; UnityEngine.Object.Destroy(texture); }
        }
        public void Dispose()
        {
            foreach(var state in canvases) if(state.canvas)
            { state.canvas.renderMode=state.mode; state.canvas.worldCamera=state.camera; state.canvas.planeDistance=state.distance; }
            Camera.targetTexture=null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(Camera.gameObject);
        }
    }
}
