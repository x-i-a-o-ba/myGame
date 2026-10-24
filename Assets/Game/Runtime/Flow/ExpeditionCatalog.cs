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
        [Range(0,1)] public float dangerousHealthBonus=.25f;
        [Min(1)] public float dangerousGoldMultiplier=1.6f;
        [Range(0,1)] public float abyssHealthBonus=.3f;
        [Min(0)] public float victoryNoticeSeconds=2;
    }
}
