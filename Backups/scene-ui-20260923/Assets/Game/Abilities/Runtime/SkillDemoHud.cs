using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Abilities
{
    public enum SkillDemoCommand { Q,W,E,UpgradeQ,UpgradeW,UpgradeE,PassiveStats,PassivePoison,AutoAttack,MoveEnemies,Dispel,Reset,Load10k,DamageHero }
    /// <summary>纯 UI 边界：文本由展示层填充，按钮只发意图，不读取 ECS。</summary>
    public sealed class SkillDemoHud : MonoBehaviour
    {
        public Text summary,skills,inspection;
        public RectTransform[] panels;
        public event Action<SkillDemoCommand> Command;
        public void Send(SkillDemoCommand value) => Command?.Invoke(value);
        public bool Blocks(Vector2 point)
        {
            foreach(var panel in panels) if(panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(panel,point)) return true;
            return false;
        }
    }
}
