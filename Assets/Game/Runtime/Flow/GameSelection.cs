using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>
    /// 本次运行的开局选择（只在内存中，不写存档）：选择界面确认时写入，战场场景的 GameCombatController 开局时读取。
    /// 只引用配置资源，不复制也不修改它们；直接打开战场场景时为空，战场沿用场景上的配置。
    /// </summary>
    public static class GameSelection
    {
        public static HeroDefinition Hero {get; private set;}
        public static GameMapDefinition Map {get; private set;}
        public static bool HasSelection=>Hero && Map;
        public static void Set(HeroDefinition hero,GameMapDefinition map)
        {
            if(!hero) throw new ArgumentNullException(nameof(hero));
            if(!map) throw new ArgumentNullException(nameof(map));
            Hero=hero; Map=map;
        }
        public static void Clear() { Hero=null; Map=null; }
        // 关闭域重载进入播放模式时静态值会保留，这里显式清空。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()=>Clear();
    }
}
