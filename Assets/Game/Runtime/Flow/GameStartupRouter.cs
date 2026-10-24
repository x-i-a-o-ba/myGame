using System;
using Roguelike.Combat;
using Roguelike.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Flow
{
    /// <summary>
    /// 命令行启动路由。构建的首场景是主菜单；--game-smoke / --game-balance 未指定 --start-scene 时直接进入默认战场，
    /// 让既有的整局验证命令保持不变（--start-scene 由 SceneFrameworkProbe 处理，--select-smoke 从主菜单走完整流程）。
    /// 正常启动不做任何事。
    /// </summary>
    public static class GameStartupRouter
    {
        public const string DefaultBattlefield="Scene.Game";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Route()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--start-scene")>=0 || Array.IndexOf(args,"--select-smoke")>=0) return;
            if(Array.IndexOf(args,"--game-smoke")<0 && Array.IndexOf(args,"--game-balance")<0 && Array.IndexOf(args,"--director-smoke")<0 && Array.IndexOf(args,"--combat-stress")<0 && Array.IndexOf(args,"--expedition-smoke")<0) return;
            if(UnityEngine.Object.FindFirstObjectByType<GameCombatController>()) return;
            int map=Array.IndexOf(args,"--director-map");
            string scene=map>=0 && map+1<args.Length && args[map+1]=="ember"?"Scene.EmberRift":DefaultBattlefield;
            SceneManager.LoadScene(ResourcePaths.Resolve(scene,"scene"));
        }
    }
}
