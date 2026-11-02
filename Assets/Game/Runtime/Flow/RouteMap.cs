using System;
using System.Collections.Generic;
using UnityEngine;
using Random=Unity.Mathematics.Random;

namespace Roguelike.Flow
{
    /// <summary>路线图节点类型。战斗类节点在战场内进行遭遇战，其余节点只打开界面。</summary>
    public enum RouteNodeKind
    {
        [InspectorName("战斗")] Combat,
        [InspectorName("精英")] Elite,
        [InspectorName("商店")] Shop,
        [InspectorName("事件")] Event,
        [InspectorName("营火")] Rest,
        [InspectorName("首领")] Boss
    }

    /// <summary>路线图的一个节点：所在层、所在列（首领为 -1）与通往下一层的连线。</summary>
    public sealed class RouteNode
    {
        public int Id,Floor,Lane;
        public RouteNodeKind Kind;
        public readonly List<int> Next=new List<int>(3);
        public bool Visited;
        public bool IsBattle=>Kind==RouteNodeKind.Combat || Kind==RouteNodeKind.Elite || Kind==RouteNodeKind.Boss;
    }

    /// <summary>
    /// 一幕的分支路线图（类似《杀戮尖塔》）：第 0 层全是战斗，倒数第二层全是营火，最后一层是唯一的首领。
    /// 只保存在内存的局内状态中，由种子确定；生成与可达判定不依赖场景或界面。
    /// </summary>
    public sealed class RouteMap
    {
        public const int Lanes=4,MinFloors=5,MaxFloors=8;
        /// <summary>界面中首领节点使用的固定槽位值；普通节点为 层 × <see cref="Lanes"/> + 列。</summary>
        public const int BossSlot=99;
        public readonly List<RouteNode> Nodes=new List<RouteNode>(32);
        public int Act {get;private set;}
        public int Floors {get;private set;}
        /// <summary>当前所在节点；-1 表示刚进入本幕，尚未选择第 0 层节点。</summary>
        public int Current=-1;
        /// <summary>当前节点的内容（战斗、商店、事件等）已经结束，可以前往下一层。</summary>
        public bool Cleared=true;
        public RouteNode Boss=>Nodes.Count>0?Nodes[Nodes.Count-1]:null;
        public RouteNode CurrentNode=>Current>=0 && Current<Nodes.Count?Nodes[Current]:null;
        public int CurrentFloor=>CurrentNode!=null?CurrentNode.Floor:-1;

        public bool CanEnter(int id)
        {
            if(id<0 || id>=Nodes.Count || !Cleared) return false;
            if(Current<0) return Nodes[id].Floor==0;
            return Nodes[Current].Next.Contains(id);
        }
        /// <summary>界面槽位值对应的节点编号；不存在时返回 -1。</summary>
        public int FromSlot(int slot)
        {
            if(slot==BossSlot) return Boss!=null?Boss.Id:-1;
            int floor=slot/Lanes,lane=slot%Lanes;
            foreach(var node in Nodes) if(node.Floor==floor && node.Lane==lane) return node.Id;
            return -1;
        }
        public static int Slot(RouteNode node)=>node.Kind==RouteNodeKind.Boss?BossSlot:node.Floor*Lanes+node.Lane;

        public static string Label(RouteNodeKind kind)
        {
            switch(kind)
            {
                case RouteNodeKind.Combat: return "战斗";
                case RouteNodeKind.Elite: return "精英";
                case RouteNodeKind.Shop: return "商店";
                case RouteNodeKind.Event: return "事件";
                case RouteNodeKind.Rest: return "营火";
                default: return "首领";
            }
        }
        /// <summary>节点中央的单字图标，带富文本颜色。</summary>
        public static string Glyph(RouteNodeKind kind)
        {
            switch(kind)
            {
                case RouteNodeKind.Combat: return "<color=#D9DEE4>战</color>";
                case RouteNodeKind.Elite: return "<color=#FF7B5C>精</color>";
                case RouteNodeKind.Shop: return "<color=#F7D485>商</color>";
                case RouteNodeKind.Event: return "<color=#9FDBFF>？</color>";
                case RouteNodeKind.Rest: return "<color=#FFB04A>火</color>";
                default: return "<color=#FF4D4D>王</color>";
            }
        }

        public static RouteMap Generate(RouteMapSettings settings,int act,uint seed)
        {
            settings??=new RouteMapSettings();
            var map=new RouteMap{Act=Mathf.Max(1,act),Floors=Mathf.Clamp(settings.floors,MinFloors,MaxFloors)};
            var random=Random.CreateFromIndex(seed);
            int rest=map.Floors-2;
            var grid=new int[map.Floors-1,Lanes];
            for(int floor=0;floor<map.Floors-1;floor++) for(int lane=0;lane<Lanes;lane++) grid[floor,lane]=-1;
            int paths=Mathf.Clamp(settings.paths,2,6),firstStart=-1;
            for(int path=0;path<paths;path++)
            {
                int lane=random.NextInt(Lanes);
                // 前两条路线从不同的列出发，保证第 0 层至少有两个选择。
                if(path==1 && lane==firstStart) lane=(lane+1+random.NextInt(Lanes-1))%Lanes;
                if(path==0) firstStart=lane;
                int previous=-1;
                for(int floor=0;floor<=rest;floor++)
                {
                    if(floor>0) lane=Mathf.Clamp(lane+random.NextInt(-1,2),0,Lanes-1);
                    int id=map.Ensure(grid,floor,lane);
                    if(previous>=0 && !map.Nodes[previous].Next.Contains(id)) map.Nodes[previous].Next.Add(id);
                    previous=id;
                }
            }
            var boss=new RouteNode{Id=map.Nodes.Count,Floor=map.Floors-1,Lane=-1,Kind=RouteNodeKind.Boss};
            foreach(var node in map.Nodes) if(node.Floor==rest) node.Next.Add(boss.Id);
            map.Nodes.Add(boss);
            foreach(var node in map.Nodes)
            {
                if(node.Kind==RouteNodeKind.Boss) continue;
                node.Kind=node.Floor==0?RouteNodeKind.Combat:node.Floor==rest?RouteNodeKind.Rest:Roll(settings,node.Floor,rest,ref random);
            }
            map.Guarantee(RouteNodeKind.Shop,settings.shopMinFloor,rest,ref random);
            map.Guarantee(RouteNodeKind.Elite,settings.eliteMinFloor,rest,ref random);
            return map;
        }
        private int Ensure(int[,] grid,int floor,int lane)
        {
            if(grid[floor,lane]>=0) return grid[floor,lane];
            var node=new RouteNode{Id=Nodes.Count,Floor=floor,Lane=lane};
            Nodes.Add(node); grid[floor,lane]=node.Id; return node.Id;
        }
        private static RouteNodeKind Roll(RouteMapSettings settings,int floor,int rest,ref Random random)
        {
            float combat=Mathf.Max(0,settings.combatWeight);
            float elite=floor>=settings.eliteMinFloor?Mathf.Max(0,settings.eliteWeight):0;
            float shop=floor>=settings.shopMinFloor?Mathf.Max(0,settings.shopWeight):0;
            float happening=Mathf.Max(0,settings.eventWeight);
            // 首领前一层固定是营火，紧挨着它的上一层不再出现营火。
            float camp=floor>=settings.restMinFloor && floor<rest-1?Mathf.Max(0,settings.restWeight):0;
            float total=combat+elite+shop+happening+camp;
            if(total<=0) return RouteNodeKind.Combat;
            float roll=random.NextFloat(total);
            if(roll<combat) return RouteNodeKind.Combat;
            roll-=combat;
            if(roll<elite) return RouteNodeKind.Elite;
            roll-=elite;
            if(roll<shop) return RouteNodeKind.Shop;
            roll-=shop;
            if(roll<happening) return RouteNodeKind.Event;
            return RouteNodeKind.Rest;
        }
        /// <summary>整幕没有该类型时，把中段的一个战斗或事件节点改为该类型。</summary>
        private void Guarantee(RouteNodeKind kind,int minFloor,int rest,ref Random random)
        {
            foreach(var node in Nodes) if(node.Kind==kind) return;
            var candidates=new List<RouteNode>();
            foreach(var node in Nodes)
                if(node.Floor>=Mathf.Max(1,minFloor) && node.Floor<rest && (node.Kind==RouteNodeKind.Combat || node.Kind==RouteNodeKind.Event)) candidates.Add(node);
            if(candidates.Count>0) candidates[random.NextInt(candidates.Count)].Kind=kind;
        }
    }

    /// <summary>路线图与遭遇战配置。强度时间按“幕 + 层”计算，与刷怪导演、难度曲线使用同一时间轴（分钟）。</summary>
    [Serializable]
    public sealed class RouteMapSettings
    {
        [Range(RouteMap.MinFloors,RouteMap.MaxFloors), Tooltip("每幕层数（含首领层）。第 0 层全是战斗，倒数第二层全是营火。")] public int floors=8;
        [Range(2,6), Tooltip("生成时走出的路线数；越多分支越密。")] public int paths=4;
        [Header("节点权重（第 0 层、营火层、首领层除外）")]
        [Min(0)] public float combatWeight=45;
        [Min(0)] public float eliteWeight=14;
        [Min(0)] public float shopWeight=12;
        [Min(0)] public float eventWeight=22;
        [Min(0)] public float restWeight=7;
        [Min(0), Tooltip("精英最早出现的层。")] public int eliteMinFloor=2;
        [Min(0), Tooltip("商店最早出现的层。")] public int shopMinFloor=2;
        [Min(0), Tooltip("营火最早出现的层（首领前一层固定是营火）。")] public int restMinFloor=3;
        [Header("强度与过关条件")]
        [Min(0.1f), Tooltip("每幕起点相差的强度时间（分钟）。")] public float actMinutes=6.75f;
        [Min(0), Tooltip("每上一层增加的强度时间（分钟）；战斗中强度时间继续随用时增长。")] public float minutesPerFloor=0.85f;
        [Tooltip("普通战斗的击杀目标：x = 强度时间（分钟），y = 击杀数；节点之间线性插值。")]
        public Vector2[] killGoals={new Vector2(0,60),new Vector2(1,140),new Vector2(3,420),new Vector2(6,950),new Vector2(9,1600),new Vector2(12,2400)};
        [Tooltip("精英战需要击败的精英数量，按幕取值。")] public int[] elitesPerAct={2,3};
        [Header("奖励")]
        [Min(0), Tooltip("普通战斗胜利的额外金币。")] public float combatGold=30;
        [Min(0), Tooltip("精英战胜利的额外金币（另可选一件遗物）。")] public float eliteGold=90;
        [Range(0,1), Tooltip("营火休息回复的最大生命比例。")] public float restHeal=.35f;
        [Range(0,1), Tooltip("营火训练获得的经验 = 本级升级所需经验 × 该比例。")] public float restTraining=.6f;

        public float Minute(int act,int floor)=>actMinutes*Mathf.Max(0,act-1)+minutesPerFloor*Mathf.Max(0,floor);
        public int KillGoal(float minute)
        {
            if(killGoals==null || killGoals.Length==0) return 60;
            if(minute<=killGoals[0].x) return Mathf.Max(10,Mathf.RoundToInt(killGoals[0].y));
            for(int i=1;i<killGoals.Length;i++)
                if(minute<killGoals[i].x)
                {
                    float t=Mathf.InverseLerp(killGoals[i-1].x,killGoals[i].x,minute);
                    return Mathf.Max(10,Mathf.RoundToInt(Mathf.Lerp(killGoals[i-1].y,killGoals[i].y,t)));
                }
            return Mathf.Max(10,Mathf.RoundToInt(killGoals[killGoals.Length-1].y));
        }
        public int Elites(int act)=>elitesPerAct==null || elitesPerAct.Length==0?2:Mathf.Max(1,elitesPerAct[Mathf.Clamp(act-1,0,elitesPerAct.Length-1)]);
    }
}
