using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.FogOfWar
{
    /// <summary>显式命令行启用的 Player 功能/性能探针；日常使用不会接管场景。</summary>
    public sealed class FogDemoProbe:MonoBehaviour
    {
        private bool failed;
        private string output;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            bool benchmark=Array.IndexOf(args,"--fog-benchmark")>=0;
            bool visual=Array.IndexOf(args,"--fog-visual-smoke")>=0;
            if(!benchmark && !visual && Array.IndexOf(args,"--fog-smoke")<0) { enabled=false; yield break; }
            output=Path.GetFullPath("DemoReports/FogOfWar");
            int dir=Array.IndexOf(args,"--demo-output");
            if(dir>=0 && dir+1<args.Length) output=args[dir+1];
            Directory.CreateDirectory(output);
            yield return null;
            var session=GetComponent<FogDemoSession>();
            Check(session.Solver!=null,"session initialized");
            if(failed) { Application.Quit(1); yield break; }
            if(benchmark) { yield return Benchmark(session); Application.Quit(0); yield break; }
            if(visual)
            {
                yield return VisualCheck(session);
                File.WriteAllText(Path.Combine(output,"visual-result.json"),"{\"passed\":"+(!failed?"true":"false")+"}");
                Destroy(session); yield return null; Application.Quit(failed?1:0); yield break;
            }
            Check(session.Batch.Targets.Length==10000,"10000 targets independent from sources");
            Check(session.SourceCount==2,"only two enabled normal sources at startup");
            yield return Capture("blue-day.png");
            var target=session.Batch.Targets[0]; target.position=session.blueScout.transform.position+new Vector3(3,0.4f,0); target.invisible=true; session.Batch.Targets[0]=target;
            session.Tick(); Check((session.Batch.Visibility[0]&1)==0,"invisible target hidden without true sight");
            Check(session.Batch.Pick(target.position,0)!=0,"hidden target not selectable");
            session.Execute(FogCommand.TrueSight); Check((session.Batch.Visibility[0]&1)!=0,"true sight reveals within normal vision");
            var known=session.Batch.Memory[0];
            session.blueScout.providesVision=false; session.Tick();
            Check((session.Batch.Visibility[0]&1)==0,"true sight alone cannot see enemy");
            target.position+=new float3(40,0,0); session.Batch.Targets[0]=target; session.Tick();
            Check(math.distance(session.Batch.Memory[0].position,known.position)<0.001f,"last-seen memory does not track hidden movement");
            session.ResetSession(); session.blueScout.dayRadius=28; session.Tick();
            int day=VisibleCells(session,0); session.Execute(FogCommand.Night);
            Check(VisibleCells(session,0)<day,"night changes stationary source radius");
            session.Execute(FogCommand.Night); int before=VisibleCells(session,0); session.Execute(FogCommand.Trees);
            Check(VisibleCells(session,0)>before,"cut trees updates occlusion");
            session.ResetSession(); session.blueScout.transform.position=new Vector3(-8,0,6); session.Tick();
            int high=session.Index(new Vector3(3,0,6));
            Check(!session.Solver.CanSee(high,0,false),"high ground blocked from below");
            session.Execute(FogCommand.Flying); Check(session.Solver.CanSee(high,0,false),"flying reveals high ground");
            yield return Capture("flying.png");
            session.Execute(FogCommand.Team); Check(session.Observer==1,"observer team switches");
            yield return Capture("red-team.png");
            session.Execute(FogCommand.EnemySource); Check(session.SourceCount==1,"enemy source prefab toggle independent of targets");
            session.PlaceWard(new Vector3(35,0,35),0); Check(session.SourceCount==2,"observer ward adds source");
            session.wardSlots[0].expiresAt=Time.time+0.1f;
            yield return new WaitForSeconds(0.2f); session.Tick(); Check(session.SourceCount==1,"ward expires and contribution is removed");
            session.PlaceWard(new Vector3(35,0,35),1); session.Tick();
            Check(!session.Solver.CanSee(session.Index(new Vector3(35,0,35)),1,true),"sentry supplies detection without terrain vision");
            session.Execute(FogCommand.RemoveWards); session.ResetSession();
            Check(session.Observer==0 && session.SourceCount==2,"reset restores local scene");
            session.phaseSeconds=0.05f; session.Execute(FogCommand.Cycle);
            yield return new WaitForSeconds(0.06f);
            Check(session.night,"automatic day night cycle"); session.ResetSession(); session.phaseSeconds=30;
            var extra=new GameObject("Probe temporary source"); extra.transform.SetParent(session.transform);
            extra.AddComponent<VisionSource>(); session.RefreshSources(); session.Tick();
            Check(session.SourceCount==3,"runtime source registration");
            Destroy(extra); yield return null; session.Tick();
            Check(session.SourceCount==2,"destroyed source no longer contributes"); session.RefreshSources();
            var ray=new Ray(new Vector3(3,20,6),Vector3.down);
            Check(session.TryGroundPoint(ray,out var surface) && Mathf.Abs(surface.y-3)<0.01f,"ground picking respects high ground");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"passed\":"+(!failed?"true":"false")+",\"targets\":10000}");
            Debug.Log(failed?"FOG_SMOKE_FAILED":"FOG_SMOKE_PASSED");
            Destroy(session); yield return null; yield return null;
            Application.Quit(failed?1:0);
        }
        private static int VisibleCells(FogDemoSession session,int team)
        {
            int n=0; for(int i=0;i<session.Solver.Visible.Length;i++) if((session.Solver.Visible[i]&(1u<<team))!=0) n++;
            return n;
        }
        private IEnumerator VisualCheck(FogDemoSession session)
        {
            session.ResetSession(); session.fogTransitionSeconds=0.3f;
            Check(session.Solver.Size==128 && session.VisualMask.Current.width==512,"display resolution independent from logical grid");
            var mask=ReadMask(session.VisualMask.Current);
            int partial=0; foreach(var pixel in mask) if(pixel.r>0.02f && pixel.r<0.98f) partial++;
            Check(partial>500,"GPU mask has a continuous feather band");
            // 无 GameObject 万级副本；仅为离屏截图提交现有批量目标。
            session.Batch.Draw(session.Observer,false); yield return Capture("smooth-start.png");
            session.blueScout.transform.position+=new Vector3(7,0,0); session.Tick();
            Check(session.VisualMask.Blend<0.01f,"movement starts from previous displayed state");
            yield return new WaitForSeconds(0.06f);
            Check(session.VisualMask.Blend>0 && session.VisualMask.Blend<1,"intermediate transition exists");
            session.Batch.Draw(session.Observer,false); yield return Capture("smooth-middle.png");
            yield return new WaitForSeconds(0.35f);
            Check(session.VisualMask.Blend>=0.999f,"transition reaches target without repeated restarts");
            session.Batch.Draw(session.Observer,false); yield return Capture("smooth-end.png");
            session.Execute(FogCommand.Team);
            Check(session.VisualMask.Blend>=0.999f,"team switch clears transition immediately");
            var a=ReadMask(session.VisualMask.Current); var b=ReadMask(session.VisualMask.Previous);
            float error=0; for(int i=0;i<a.Length;i++) error=Mathf.Max(error,Mathf.Abs(a[i].r-b[i].r));
            Check(error<0.005f,"team switch contains no previous-team mask");
            session.ResetSession();
            session.fogTransitionSeconds=0.12f;
            Check(session.VisualMask.Blend>=0.999f,"reset clears transition history");
            Debug.Log(failed?"FOG_VISUAL_FAILED":"FOG_VISUAL_PASSED");
        }
        private static Color[] ReadMask(RenderTexture source)
        {
            var old=RenderTexture.active;
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active=source;
                texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0); texture.Apply();
                return texture.GetPixels();
            }
            finally { RenderTexture.active=old; UnityEngine.Object.Destroy(texture); }
        }
        private IEnumerator Benchmark(FogDemoSession session)
        {
            session.enabled=false;
            var samples=new List<double>();
            var csv=new System.Text.StringBuilder("run,sources,targets,solve_ms,query_ms,presentation_upload_ms\n");
            var sources=new NativeArray<FogSourceData>(128,Allocator.Persistent);
            try
            {
                for(int run=0;run<3;run++)
                {
                    float start=Time.realtimeSinceStartup; float finish=start+70; int tick=0;
                    while(Time.realtimeSinceStartup<finish)
                    {
                        for(int i=0;i<sources.Length;i++) sources[i]=new FogSourceData {
                            cell=new int2(8+(i*17+tick)%112,8+(i*23+tick/2)%112), radius=16, detectionRadius=i%8==0?12:0,team=i%2,flying=i%9==0 };
                        session.BenchmarkSnapshot(sources);
                        if(Time.realtimeSinceStartup-start>=10)
                        {
                            double total=session.SolveMs+session.QueryMs+session.UploadMs; samples.Add(total);
                            csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},128,10000,{1:F4},{2:F4},{3:F4}",run+1,session.SolveMs,session.QueryMs,session.UploadMs));
                        }
                        tick++; yield return new WaitForSecondsRealtime(0.1f);
                    }
                    Debug.Log("FOG_BENCHMARK_RUN_COMPLETE "+(run+1));
                }
            }
            finally { sources.Dispose(); }
            samples.Sort();
            File.WriteAllText(Path.Combine(output,"performance.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(output,"performance.txt"),$"CPU: {SystemInfo.processorType}\nGPU: {SystemInfo.graphicsDeviceName}\n128 moving sources / 10000 targets / 128x128 cells / radius16\nThree runs: 10s warmup + 60s capture each.\nSamples {samples.Count}\np50 {samples[(int)(samples.Count*0.50)]:F3} ms\np95 {samples[(int)(samples.Count*0.95)]:F3} ms\np99 {samples[(int)(samples.Count*0.99)]:F3} ms\nWall time for solver+target query+presentation upload; excludes drawing, not an FPS certification.");
            Debug.Log("FOG_BENCHMARK_COMPLETE");
        }
        private IEnumerator Capture(string name)
        {
            yield return null;
            var camera = GetComponent<FogDemoSession>().gameCamera;
            var canvas = GetComponentInChildren<Canvas>();
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
            Debug.Log((condition?"FOG_PASS ":"FOG_FAIL ")+message); if(!condition) failed=true;
        }
    }
}
