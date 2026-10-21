using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Abilities
{
    /// <summary>仅命令行启用的 Player 验证与性能探针。正常运行不接管场景。</summary>
    public sealed class SkillDemoProbe : MonoBehaviour
    {
        private bool failed;
        private string output;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            bool benchmark=Array.IndexOf(args,"--ability-benchmark")>=0;
            if(!benchmark && Array.IndexOf(args,"--ability-smoke")<0) { enabled=false;yield break; }
            output=Path.GetFullPath("DemoReports/Abilities");
            int n=Array.IndexOf(args,"--demo-output");if(n>=0 && n+1<args.Length) output=args[n+1];
            Directory.CreateDirectory(output);yield return null;yield return null;
            var demo=GetComponent<SkillDemoController>();Check(demo.Runtime!=null,"session initialized");
            if(failed) { Application.Quit(1);yield break; }
            if(benchmark) { yield return Benchmark(demo);Application.Quit(0);yield break; }
            var runtime=demo.Runtime;
            Check(runtime.State.AliveEnemies>=9990,"ten thousand ECS target load initialized");
            demo.Command(SkillDemoCommand.AutoAttack);
            yield return Capture("start.png");
            float mana=runtime.Manager.GetComponentData<AbilityUnit>(runtime.Hero).Mana;
            demo.SelectSkill(0);demo.SetAim(new Vector3(9,0,0),true);Check(demo.AimValid,"Q aim uses shared geometry");
            yield return Capture("aim.png");
            Check(demo.Confirm(),"Q confirm queues cast");yield return null;yield return null;
            Check(runtime.State.LastFailure==CastFailure.None && runtime.Manager.GetComponentData<AbilityUnit>(runtime.Hero).Mana<mana,"cast commits mana and effects");
            Check(runtime.State.ActiveBuffs>1,"area cast applies real Buff state");
            yield return Capture("burst.png");
            demo.SelectSkill(1);demo.SetAim(new Vector3(8,0,3),true);demo.Confirm();yield return null;yield return null;
            Check(runtime.Manager.GetBuffer<ActiveArea>(runtime.Session).Length==1,"W persists as a region");
            demo.SelectSkill(2);demo.SetAim(Vector3.zero,true);demo.Confirm();yield return null;yield return null;
            Check(runtime.Manager.GetComponentData<AbilityUnit>(runtime.Hero).Speed>8,"E changes actual movement attribute");
            demo.Command(SkillDemoCommand.PassiveStats);Check(runtime.Manager.GetComponentData<AbilityUnit>(runtime.Hero).Attack==18,"passive removal updates attributes");
            demo.Command(SkillDemoCommand.PassiveStats);demo.Command(SkillDemoCommand.UpgradeQ);
            Check(runtime.Manager.GetBuffer<AbilitySlot>(runtime.Hero)[0].Level==2,"numeric skill upgrade");
            demo.SelectSkill(0);demo.SetAim(new Vector3(999,0,0),true);Check(demo.AimValid && demo.AimPoint.x<=18.01f,"default range clamp");
            demo.clampOutOfRange=false;demo.SetAim(new Vector3(999,0,0),true);Check(!demo.AimValid && !demo.Confirm(),"invalid aim rejected");
            demo.clampOutOfRange=true;demo.SetAim(Vector3.zero,false);Check(!demo.Confirm(),"UI or missing ground cannot cast using stale point");demo.Cancel();
            demo.Move(new Vector3(2,0,0));yield return new WaitForSeconds(0.3f);
            Check(runtime.Manager.GetComponentData<LocalTransform>(runtime.Hero).Position.x>0.2f,"player movement uses runtime speed");
            yield return Capture("field-and-buffs.png");
            demo.Restart();yield return null;yield return null;
            Check(runtime.Manager.GetBuffer<AbilitySlot>(runtime.Hero)[0].Level==1 && runtime.Manager.GetBuffer<ActiveArea>(runtime.Session).Length==0,"restart clears upgrades and regions");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"passed\":"+(!failed?"true":"false")+",\"targets\":10000}");
            Debug.Log(failed?"ABILITY_SMOKE_FAILED":"ABILITY_SMOKE_PASSED");
            Destroy(demo);yield return null;yield return null;Application.Quit(failed?1:0);
        }
        private IEnumerator Benchmark(SkillDemoController demo)
        {
            var csv=new System.Text.StringBuilder("run,targets,active_buffs,regions,simulation_ms,frame_ms\n");
            var samples=new List<double>();
            for(int run=0;run<3;run++)
            {
                demo.Restart();var runtime=demo.Runtime;var state=runtime.State;state.AutoAttack=false;runtime.Manager.SetComponentData(runtime.Session,state);
                // High-health stationary targets keep load stable, instead of making later samples cheaper by killing them.
                using(var query=runtime.Manager.CreateEntityQuery(typeof(AbilityUnit)))
                using(var entities=query.ToEntityArray(Unity.Collections.Allocator.Temp))
                    foreach(var entity in entities) { var unit=runtime.Manager.GetComponentData<AbilityUnit>(entity);if(unit.Team==1) {unit.Health=unit.MaxHealth=100000;runtime.Manager.SetComponentData(entity,unit);} }
                float start=Time.realtimeSinceStartup, nextCast=0,nextSample=0;
                while(Time.realtimeSinceStartup-start<70)
                {
                    float now=Time.realtimeSinceStartup;
                    if(now>=nextCast)
                    {
                        nextCast=now+0.8f;
                        var hero=runtime.Manager.GetComponentData<AbilityUnit>(runtime.Hero);hero.Mana=hero.MaxMana;runtime.Manager.SetComponentData(runtime.Hero,hero);
                        var slots=runtime.Manager.GetBuffer<AbilitySlot>(runtime.Hero);for(int i=0;i<slots.Length;i++) {var slot=slots[i];slot.ReadyAt=0;slots[i]=slot;}
                        runtime.Cast(0,new Vector3(10,0,0));runtime.Cast(1,new Vector3(9,0,2));runtime.Cast(2,Vector3.zero);
                    }
                    if(now-start>=10 && now>=nextSample)
                    {
                        nextSample=now+0.1f;state=runtime.State;samples.Add(state.StepMs);
                        csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F4},{5:F4}",run+1,state.AliveEnemies,state.ActiveBuffs,runtime.Manager.GetBuffer<ActiveArea>(runtime.Session).Length,state.StepMs,Time.unscaledDeltaTime*1000));
                    }
                    yield return null;
                }
                Debug.Log("ABILITY_BENCHMARK_RUN_COMPLETE "+(run+1));
            }
            samples.Sort();File.WriteAllText(Path.Combine(output,"performance.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(output,"performance.txt"),$"CPU {SystemInfo.processorType}\nGPU {SystemInfo.graphicsDeviceName}\n10000 ECS targets, recurring Q/W/E, three runs 10s warmup + 60s capture\nSamples {samples.Count}\np50 {samples[(int)(samples.Count*.5)]:F3} ms\np95 {samples[(int)(samples.Count*.95)]:F3} ms\np99 {samples[(int)(samples.Count*.99)]:F3} ms\nSimulation CPU wall time; excludes Entities Graphics rendering. Background frame times are diagnostic, not foreground FPS acceptance.");
            Debug.Log("ABILITY_BENCHMARK_COMPLETE");
        }
        private IEnumerator Capture(string name)
        {
            yield return null;
            var camera = GetComponent<SkillDemoController>().gameCamera;
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
        { Debug.Log((condition?"ABILITY_PASS ":"ABILITY_FAIL ")+message);if(!condition) failed=true; }
    }
}
