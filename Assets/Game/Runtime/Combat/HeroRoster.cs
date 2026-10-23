using Roguelike.Abilities;
using UnityEngine;
namespace Roguelike.Combat
{
    /// <summary>只负责选择装配并重开；生命、装备与技能状态仍归会话。</summary>
    public sealed class HeroRoster : MonoBehaviour
    {
        [Tooltip("可选英雄；点击按钮后重新开始本局。")] public HeroDefinition[] heroes;
        public GameCombatController controller;
        public void Select(int index)
        { if(controller && heroes!=null && index>=0 && index<heroes.Length) controller.SelectHero(heroes[index]); }
    }
}
