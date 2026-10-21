using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.SkillIndicators
{
    /// <summary>仅 --indicator-smoke 启用的 Player 回归，日常运行不接管输入。</summary>
    public sealed class IndicatorDemoProbe : MonoBehaviour
    {
        private bool failed;
        private string output;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--indicator-smoke");
            if(index<0) { enabled=false; yield break; }
            output=Path.GetFullPath("DemoReports/SkillIndicator");
            int dir=Array.IndexOf(args,"--demo-output");
            if(dir>=0 && dir+1<args.Length) output=args[dir+1];
            Directory.CreateDirectory(output);
            yield return null;
            var controller=GetComponent<IndicatorDemoController>();
            var input=GetComponent<IndicatorDemoInput>(); input.enabled=false;
            var origin=controller.CasterPosition;
            int count=GetComponentsInChildren<Transform>(true).Length;
            Check(controller.ClampOutOfRange,"Default clamp enabled");
            controller.SelectSkill(0);
            var inside=origin+new Vector3(6,0,3);
            Check(input.UpdatePointer(Camera.main.WorldToScreenPoint(inside)),"Screen-to-ground projection");
            Check(controller.CanCast && Vector3.Distance(controller.Target,inside)<0.02f,"In-range target preserved");
            yield return Capture("aim.png");
            Check(!input.UpdatePointer(new Vector2(Screen.width*0.5f,50)),"HUD blocks pointer");
            Check(!controller.TryConfirm(),"HUD cannot confirm stale point");
            controller.SetPointer(origin+Vector3.right*30,true);
            Check(Mathf.Abs(Vector3.Distance(origin,controller.Target)-10)<0.01f,"Out-of-range clamps to exact boundary");
            yield return Capture("clamped.png");
            Check(controller.TryConfirm() && !controller.IsAiming,"Clamped cast confirms and exits aim");
            yield return Capture("feedback.png");
            yield return new WaitForSeconds(1);
            Check(!UnityEngine.Object.FindFirstObjectByType<IndicatorFeedback>().IsPlaying,"Feedback expires");
            controller.ClampOutOfRange=false; controller.SelectSkill(0);
            controller.SetPointer(origin+Vector3.right*18,true);
            Check(!controller.CanCast && !controller.TryConfirm(),"Disabled clamp rejects outside target");
            yield return Capture("rejected.png");
            controller.SetPointer(origin,true);
            Check(controller.CanCast && controller.TryConfirm(),"Return inside restores casting");
            controller.SelectSkill(1); controller.SetPointer(origin+Vector3.right*7,true);
            Check(controller.ActiveSkill.areaRadius==4 && controller.CanCast,"W selects separate config");
            int casts=controller.CastCount; controller.CancelAim();
            Check(!controller.TryConfirm() && controller.CastCount==casts,"Cancel does not cast");
            controller.SelectSkill(2); controller.SetPointer(origin+Vector3.forward*14,true);
            Check(controller.CanCast,"Exact E boundary accepted");
            controller.CancelAim();
            Check(GetComponentsInChildren<Transform>(true).Length==count,"Repeated selections reuse scene objects");
            controller.ClampOutOfRange=true;
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"passed\":"+(!failed ? "true":"false")+",\"casts\":"+controller.CastCount+"}");
            Debug.Log(failed?"INDICATOR_SMOKE_FAILED":"INDICATOR_SMOKE_PASSED");
            Application.Quit(failed?1:0);
        }
        private IEnumerator Capture(string name)
        {
            yield return null;
            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                // Hidden/minimized windows can have a black backbuffer. Request an actual URP
                // render including the same HUD into a texture, independent of window visibility.
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                Color first = texture.GetPixel(0, 0);
                bool varied = false;
                for (int y = 30; y < 1080; y += 97)
                    for (int x = 20; x < 1920; x += 113)
                        if (((Vector4)(texture.GetPixel(x, y) - first)).sqrMagnitude > 0.01f) varied = true;
                Check(varied, "Offscreen render contains scene/UI pixels: " + name);
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlaneDistance;
                target.Release();
                Destroy(target);
                Destroy(texture);
            }
        }


        private void Check(bool condition,string message)
        {
            Debug.Log((condition?"INDICATOR_PASS ":"INDICATOR_FAIL ")+message);
            if(!condition) failed=true;
        }
    }
}
