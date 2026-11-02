using System;
using UnityEngine;

namespace Roguelike.Flow
{
    [CreateAssetMenu(menuName="Roguelike/远征/远征配置")]
    public sealed class ExpeditionCatalog : ScriptableObject
    {
        public StartSelectionCatalog selection;
        public RelicDefinition[] relics=Array.Empty<RelicDefinition>();
        [Min(0)] public float passiveGoldPerSecond=2;
        [Min(0)] public float victoryNoticeSeconds=2;
        [Tooltip("两幕路线图：层数、节点权重、击杀目标、精英数量、营火与奖励数值。")]
        public RouteMapSettings route=new RouteMapSettings();
        [Tooltip("路线图上的特殊事件。")]
        public ExpeditionEvent[] events=ExpeditionEvent.Defaults();
    }
}
