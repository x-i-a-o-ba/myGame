using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>
    /// 跨场景局内快照，只存在内存。两幕各对应一张战场，每幕一张分支路线图。
    /// 战斗时 ECS 为权威，离开节点、营地交易或换幕时由宿主保存。
    /// </summary>
    public sealed class RunState
    {
        public const int FinalAct=2;
        public static RunState Current {get;private set;}
        public HeroDefinition Hero {get;private set;}
        public GameMapDefinition FirstMap {get;private set;}
        public GameMapDefinition SecondMap {get;private set;}
        public int Act=1,Level=1,SkillPoints=1,Kills;
        public float Experience,Gold=300,GoldEarned,TotalBattleTime;
        /// <summary>下一场战斗的敌人生命加成（事件结果），开战时消耗。</summary>
        public float NextFightHealthBonus;
        public bool Restored;
        public readonly int[] SkillLevels=new int[4],Forms=new int[4],EquipmentIds={-1,-1,-1,-1,-1,-1};
        public readonly List<RelicDefinition> Relics=new List<RelicDefinition>(4);
        /// <summary>本局已遇到的事件编号；全部遇到后重新开始抽取。</summary>
        public readonly List<string> SeenEvents=new List<string>(8);
        public uint Seed;
        /// <summary>当前幕的路线图；进入新幕后由 <see cref="EnsureRoute"/> 按种子生成。</summary>
        public RouteMap Route {get;private set;}
        public GameMapDefinition Map=>Act>=2?SecondMap:FirstMap;
        public bool IsFinalAct=>Act>=FinalAct;
        public static RunState Begin(HeroDefinition hero,GameMapDefinition first,GameMapDefinition second,uint seed=0)
        {
            if(!hero || !first || !second || first==second) throw new InvalidOperationException("远征需要英雄和两张不同战场。");
            Current=new RunState{Hero=hero,FirstMap=first,SecondMap=second,Seed=seed==0?(uint)Environment.TickCount|1u:seed};
            return Current;
        }
        public static void Clear()=>Current=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>Clear();
        public RouteMap EnsureRoute(RouteMapSettings settings)
        {
            if(Route==null || Route.Act!=Act) Route=RouteMap.Generate(settings,Act,Seed*31u+(uint)Act*7919u+1u);
            return Route;
        }
        /// <summary>击败本幕首领后进入下一幕：换战场、清空路线图。</summary>
        public void Advance()
        {
            if(Act>=FinalAct) throw new InvalidOperationException("最后一幕后没有下一幕。");
            Act++;Route=null;Restored=true;GameSelection.Set(Hero,Map);
        }
    }
}
