using System;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.Foundation.UI;
namespace Roguelike.Abilities
{
    public enum SkillDemoCommand { Q,W,E,UpgradeQ,UpgradeW,UpgradeE,PassiveStats,PassivePoison,AutoAttack,MoveEnemies,Dispel,Reset,Load10k,DamageHero }
    /// <summary>技能模块的 UI 事件适配器。只发布展示数据和转发意图，不持有运行时面板引用。</summary>
    public sealed class SkillDemoHud : MonoBehaviour
    {
        // 仅供旧场景首次迁移和原搭建工具读取；迁移后清空，不作为运行时依赖。
        [HideInInspector] public Text summary,skills,inspection;
        [HideInInspector] public RectTransform[] panels;
        private Roguelike.Foundation.UI.UIManager UI => GetComponent<Roguelike.Foundation.UI.UIManager>();
        public event Action<SkillDemoCommand> Command;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(UiCommand value) { if(value.Id=="ability.command") Command?.Invoke((SkillDemoCommand)value.Value); }
        public void Send(SkillDemoCommand value) => UI.Events.Send(new UiCommand("ability.command",(int)value));
        public string Summary { set => UI.Text("ability.summary",value); }
        public string Skills { set => UI.Text("ability.skills",value); }
        public string Inspection { set => UI.Text("ability.inspection",value); }
        public bool Blocks(Vector2 point) => UI.Blocks(point);
    }
}
