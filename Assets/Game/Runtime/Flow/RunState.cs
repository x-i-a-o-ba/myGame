using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>跨场景局内快照，只存在内存。战斗时 ECS 为权威，过关/营地交易后由宿主保存。</summary>
    public sealed class RunState
    {
        public static RunState Current {get;private set;}
        public HeroDefinition Hero {get;private set;}
        public GameMapDefinition FirstMap {get;private set;}
        public GameMapDefinition SecondMap {get;private set;}
        public int Stage=1,Level=1,SkillPoints=1,Kills;
        public float Experience,Gold=300,GoldEarned,TotalBattleTime;
        public bool DangerousRoute,Restored;
        public readonly int[] SkillLevels=new int[4],Forms=new int[4],EquipmentIds={-1,-1,-1,-1,-1,-1};
        public readonly List<RelicDefinition> Relics=new List<RelicDefinition>(2);
        public uint Seed;
        public GameMapDefinition Map=>Stage==2?SecondMap:FirstMap;
        public bool Abyss=>Stage==3;
        public static RunState Begin(HeroDefinition hero,GameMapDefinition first,GameMapDefinition second,uint seed=0)
        {
            if(!hero || !first || !second || first==second) throw new InvalidOperationException("远征需要英雄和两张不同战场。");
            Current=new RunState{Hero=hero,FirstMap=first,SecondMap=second,Seed=seed==0?(uint)Environment.TickCount|1u:seed};
            return Current;
        }
        public static void Clear()=>Current=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Clear();
        public void Advance()
        {
            if(Stage>=3) throw new InvalidOperationException("最终关后没有下一关。");
            Stage++;Restored=true;GameSelection.Set(Hero,Map);
        }
    }
}
