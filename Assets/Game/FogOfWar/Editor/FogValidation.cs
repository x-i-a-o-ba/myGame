using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.FogOfWar.Editor
{
    /// <summary>小地图规则回归，使用真实 Burst 计算路径，不依赖画面判断。</summary>
    public static class FogValidation
    {
        public static void Validate()
        {
            using(var solver=new FogSolver(32))
            {
                var sources=new NativeList<FogSourceData>(4,Allocator.TempJob);
                try
                {
                var a=new FogSourceData{cell=new int2(8,8),radius=10,team=0};
                sources.Add(a); Solve(solver,sources);
                Check(solver.CanSee(8*32+16,0,false),"flat range");
                Check(!solver.CanSee(8*32+20,0,false),"outside range");
                Check(!solver.CanSee(8*32+10,1,false),"team separation");
                Check(!solver.CanSee(8*32+10,0,true),"invisible requires detection");
                sources.Add(new FogSourceData{cell=new int2(10,8),detectionRadius=5,team=0}); Solve(solver,sources);
                Check(solver.CanSee(8*32+10,0,true),"vision plus true sight");
                sources.RemoveAt(0); Solve(solver,sources);
                Check(!solver.CanSee(8*32+10,0,true),"true sight does not reveal fog");
                Check((solver.Explored[8*32+10]&1)!=0,"exploration retained");
                sources.Clear(); sources.Add(a);
                for(int y=0;y<32;y++) solver.Blockers[y*32+11]=1;
                Solve(solver,sources); Check(!solver.CanSee(8*32+14,0,false),"tree wall occludes");
                a.flying=true; sources[0]=a; Solve(solver,sources);
                Check(solver.CanSee(8*32+14,0,false),"flying bypasses trees");
                for(int i=0;i<solver.Blockers.Length;i++) solver.Blockers[i]=0;
                a.flying=false; sources[0]=a;
                for(int y=0;y<32;y++) for(int x=11;x<32;x++) solver.Heights[y*32+x]=1;
                Solve(solver,sources); Check(!solver.CanSee(8*32+13,0,false),"low cannot see high ground");
                a.cell=new int2(13,8); sources[0]=a; Solve(solver,sources);
                Check(solver.CanSee(8*32+8,0,false),"high sees lower ground");
                sources.Add(a); Solve(solver,sources); sources.RemoveAt(0); Solve(solver,sources);
                Check(solver.CanSee(8*32+8,0,false),"removing overlapping source preserves other source");
                sources.Clear(); Solve(solver,sources); Check(!solver.CanSee(8*32+8,0,false),"last source removed");
                a.cell=new int2(0,0); a.radius=32; sources.Add(a); Solve(solver,sources);
                Check(solver.CanSee(0,0,false),"map boundary safe");
                solver.Reset(false); Check(solver.Explored[0]==0,"reset exploration");
                solver.Reset(true); Check(solver.Explored[0]==3,"initial terrain known option");
                }
                finally { sources.Dispose(); }
            }
            Debug.Log("FOG_RULES_VALIDATED");
        }
        private static void Solve(FogSolver solver,NativeList<FogSourceData> sources) => solver.Schedule(sources.AsArray()).Complete();
        private static void Check(bool value,string message) { if(!value) throw new Exception("FOG_RULE_FAILED: "+message); }
    }
}
