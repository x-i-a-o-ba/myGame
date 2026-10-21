using System.Text;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>低频读取角色摘要与选中对象，复用场景视图；不为一万目标创建 UI 或 GameObject。</summary>
    public sealed class SkillDemoPresentation
    {
        private readonly SkillDemoController demo;
        private int castSerial,attackSerial;
        private float feedbackUntil,beamUntil,nextHud;
        private readonly StringBuilder text=new StringBuilder(768);
        public SkillDemoPresentation(SkillDemoController demo) { this.demo=demo; }
        public void Reset()
        {
            castSerial=attackSerial=0; nextHud=0; feedbackUntil=beamUntil=0;
            demo.feedbackView.Hide(); demo.attackBeam.enabled=false;
            foreach(var view in demo.areaViews) view.Hide();
        }
        public void Tick()
        {
            var runtime=demo.Runtime; var state=runtime.State; var manager=runtime.Manager;
            var hero=manager.GetComponentData<AbilityUnit>(state.Hero);
            var position=manager.GetComponentData<LocalTransform>(state.Hero).Position;
            demo.playerView.position=new Vector3(position.x,1,position.z);
            if(state.CastSerial!=castSerial)
            {
                castSerial=state.CastSerial;
                demo.SetMessage(state.LastFailure==CastFailure.None?"施放成功":"无法施放："+Failure(state.LastFailure));
                if(state.LastFailure==CastFailure.None)
                {
                    var slot=manager.GetBuffer<AbilitySlot>(state.Hero)[state.LastSkill]; ref var definition=ref state.Library.Value.Skills[slot.Definition];
                    demo.feedbackView.Show(new Vector3(state.LastPoint.x,0.06f,state.LastPoint.z),AbilityGeometry.Radius(ref definition,slot.Level),demo.catalog.skills[slot.Definition].indicatorColor);
                    feedbackUntil=Time.time+0.3f;
                }
            }
            if(Time.time>=feedbackUntil) demo.feedbackView.Hide();
            if(state.AttackSerial!=attackSerial)
            {
                attackSerial=state.AttackSerial; beamUntil=Time.time+0.08f;
                demo.attackBeam.SetPosition(0,new Vector3(position.x,1,position.z));
                demo.attackBeam.SetPosition(1,state.LastAttackPoint);
            }
            demo.attackBeam.enabled=Time.time<beamUntil;
            var areas=manager.GetBuffer<ActiveArea>(runtime.Session);
            for(int i=0;i<demo.areaViews.Length;i++)
                if(i<areas.Length) demo.areaViews[i].Show(new Vector3(areas[i].Center.x,0.025f,areas[i].Center.z),areas[i].Radius,demo.catalog.skills[areas[i].Skill].indicatorColor,0.65f);
                else demo.areaViews[i].Hide();
            if(Time.unscaledTime<nextHud) return; nextHud=Time.unscaledTime+0.1f;
            demo.hud.summary.text=$"生命 {hero.Health:F0}/{hero.MaxHealth:F0}    能量 {hero.Mana:F0}/{hero.MaxMana:F0}\n攻击 {hero.Attack:F0}    移速 {hero.Speed:F1}    回血 {hero.Regen:F1}/秒\n存活敌人 {state.AliveEnemies:N0}    Buff {state.ActiveBuffs:N0}\n模拟 {state.StepMs:F2}ms    区域 {areas.Length}/8\n常驻被动 {On(hero.PassiveMask,0)}    中毒被动 {On(hero.PassiveMask,1)}\n自动普攻 {(state.AutoAttack?"开启":"关闭")} / 目标移动 {(state.MoveEnemies?"开启":"关闭")}\n{demo.Message}";
            text.Clear(); var slots=manager.GetBuffer<AbilitySlot>(state.Hero);
            for(int i=0;i<slots.Length;i++)
            {
                var s=slots[i]; ref var def=ref state.Library.Value.Skills[s.Definition];
                text.Append($"{(i==0?"Q":i==1?"W":"E")} · {demo.catalog.skills[s.Definition].displayName}  Lv{s.Level}  范围 {AbilityGeometry.Radius(ref def,s.Level):F1}m  能量 {def.ManaCost:F0}  冷却 {Mathf.Max(0,s.ReadyAt-state.Time):F1}s\n");
            }
            demo.hud.skills.text=text.ToString();
            text.Clear(); AppendUnit("主角 Buff",state.Hero);
            if(demo.Selected!=Entity.Null && manager.Exists(demo.Selected)) AppendUnit("选中单位",demo.Selected);
            else text.Append("\n左键选中单位，查看 Buff 与属性。\n红色：敌方训练目标\n绿色：友方移动目标\n\nQ：伤害 + 灼烧\nW：持续区域 + 减速\nE：友方加速\n普攻：直接命中 + 被动中毒");
            demo.hud.inspection.text=text.ToString();
        }
        private void AppendUnit(string title,Entity entity)
        {
            var runtime=demo.Runtime; var unit=runtime.Manager.GetComponentData<AbilityUnit>(entity);
            text.Append($"{title}\n生命 {unit.Health:F0} / 移速 {unit.Speed:F1}\n");
            var buffs=runtime.Manager.GetBuffer<ActiveBuff>(entity);
            if(buffs.Length==0) text.Append("无 Buff\n");
            for(int i=0;i<Mathf.Min(8,buffs.Length);i++)
            {
                var b=buffs[i]; string duration=b.ExpiresAt==float.MaxValue?"常驻":Mathf.Max(0,b.ExpiresAt-runtime.State.Time).ToString("F1")+"s";
                text.Append($"{runtime.Catalog.buffs[b.Definition].displayName} ×{b.Stacks}  {duration}\n");
            }
        }
        private static string On(uint mask,int bit) => (mask&(1u<<bit))!=0?"开启":"关闭";
        private static string Failure(CastFailure failure)
        {
            switch(failure) { case CastFailure.Cooldown:return "冷却中"; case CastFailure.Mana:return "能量不足";
                case CastFailure.OutOfRange:return "超出距离"; case CastFailure.AreaLimit:return "区域槽已满";
                case CastFailure.Dead:return "角色已倒下"; default:return "配置或请求无效"; }
        }
    }
}
