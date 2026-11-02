using System.Text;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>选中敌人信息面板（Dota 2 式单位面板）的展示快照：只含文字、比例与纹理，由控制器在 HUD 刷新时填充并复用。</summary>
    public sealed class TargetInfoView
    {
        public EnemyTier Tier;
        public Texture Portrait;
        public float HealthFraction;
        public string Name,TierLabel,Subtitle,HealthText,Damage,AttackInterval,Range,MoveSpeed,Armor,Bounty,Affixes,Abilities,Statuses,Description;
    }

    /// <summary>
    /// 只读地把一个敌人实体的权威状态（AbilityUnit、CombatEnemy、自动攻击、词缀、Buff）整理成 <see cref="TargetInfoView"/>。
    /// 不写入任何 ECS 数据；目标不存在或已死亡时返回 false。
    /// </summary>
    public static class TargetInfoBuilder
    {
        private const string Weakened="#8FD3FF",Empowered="#FF8A6A";
        private static readonly StringBuilder text=new StringBuilder(128);

        public static bool Build(EntityManager manager,Entity entity,GameCombatSession session,float now,TargetInfoView view)
        {
            if(entity==Entity.Null || !manager.Exists(entity) || !manager.HasComponent<CombatEnemy>(entity) ||
               !manager.HasComponent<AbilityUnit>(entity) || !manager.HasComponent<LocalTransform>(entity)) return false;
            var unit=manager.GetComponentData<AbilityUnit>(entity);
            if(unit.Health<=0) return false;
            var enemy=manager.GetComponentData<CombatEnemy>(entity);
            var data=manager.HasComponent<AutoAttackData>(entity)?manager.GetComponentData<AutoAttackData>(entity):default;
            var behavior=manager.HasComponent<EnemyBehaviorData>(entity)?manager.GetComponentData<EnemyBehaviorData>(entity):default;
            var archetypes=session.EnemyArchetypes;var names=session.EnemyNames;
            EnemyArchetype archetype=archetypes!=null && enemy.Kind>=0 && enemy.Kind<archetypes.Length?archetypes[enemy.Kind]:null;

            view.Tier=enemy.Tier;
            view.Name=names!=null && enemy.Kind>=0 && enemy.Kind<names.Length?names[enemy.Kind]:archetype?archetype.Label:"敌人";
            view.TierLabel=enemy.Tier==EnemyTier.Boss?"<color=#FF5A4D>首领</color>":enemy.Tier==EnemyTier.Elite?"<color=#FFB347>精英</color>":"<color=#C9D1D9>普通</color>";
            view.Subtitle=$"第 {math.max(1,enemy.Stage)} 幕 · {EnemyArchetype.BehaviorLabel(behavior.Behavior)}";
            view.Portrait=archetype && archetype.portrait?archetype.portrait:archetype && archetype.attack?archetype.attack.icon:null;
            float maxHealth=math.max(1,unit.MaxHealth);
            view.HealthFraction=math.saturate(unit.Health/maxHealth);
            view.HealthText=$"{Mathf.CeilToInt(unit.Health):N0} / {Mathf.CeilToInt(maxHealth):N0}";

            // 数值偏离基础值时着色：被削弱为冰蓝色，被强化为橙红色（与 Dota 2 的增减益颜色习惯一致）。
            float attackSpeed=unit.AttackSpeedMultiplier>0?unit.AttackSpeedMultiplier:1;
            float interval=AutoAttackLogic.Interval(data,attackSpeed);
            view.Damage=Modified($"{unit.Attack:0}",unit.Attack,unit.BaseAttack,true);
            view.AttackInterval=Modified($"{interval:0.00} 秒",interval,data.BaseAttackTime,false);
            view.Range=$"{data.Range+enemy.Reach:0.#} 米";
            view.MoveSpeed=Modified($"{unit.Speed:0.#}",unit.Speed,unit.BaseSpeed,true);
            float reduction=math.min(.8f,unit.DamageReduction+unit.BuffDamageReduction);
            view.Armor=$"{reduction*100:0}%";
            view.Bounty=enemy.Tier==EnemyTier.Normal?$"经验 {enemy.Experience:0.#} · 金币 {enemy.Gold:0.#}":$"经验 {enemy.Experience:0} · 金币 {enemy.Gold:0}";

            // 词缀与技能
            text.Clear();
            if(manager.HasComponent<MonsterBrain>(entity))
            {
                var brain=manager.GetComponentData<MonsterBrain>(entity);
                Affix(brain.Affixes,MonsterAffix.Rage,brain.Enraged?"狂怒（已激活）":"狂怒");
                Affix(brain.Affixes,MonsterAffix.Shield,"护盾");
                Affix(brain.Affixes,MonsterAffix.Summon,"召唤");
                Affix(brain.Affixes,MonsterAffix.Split,"分裂");
                Affix(brain.Affixes,MonsterAffix.Elemental,brain.ElementIsFire?"余烬":"寒霜");
                Affix(brain.Affixes,MonsterAffix.Fortified,"坚韧");
                if(brain.ArmoredAboveHalf>0) Append($"半血前减伤 {brain.ArmoredAboveHalf*100:0}%");
            }
            view.Affixes=text.Length>0?"词缀："+text:"";
            text.Clear();
            if(archetype && archetype.attack) Append(archetype.attack.Label);
            if(archetype && archetype.abilities!=null)
                foreach(var ability in archetype.abilities)
                    if(ability) Append(string.IsNullOrWhiteSpace(ability.displayName)?ability.name:ability.displayName);
            view.Abilities=text.Length>0?"技能："+text:"";

            // 状态效果（Dota 2 的增益 / 减益栏），附剩余时间。
            text.Clear();
            if(manager.HasBuffer<ActiveBuff>(entity))
            {
                var buffs=manager.GetBuffer<ActiveBuff>(entity,true);var definitions=session.Buffs;
                for(int i=0;i<buffs.Length && i<6;i++)
                {
                    var buff=buffs[i];
                    string name=definitions!=null && buff.Definition>=0 && buff.Definition<definitions.Length && definitions[buff.Definition] &&
                        !string.IsNullOrWhiteSpace(definitions[buff.Definition].displayName)?definitions[buff.Definition].displayName:"状态";
                    float remaining=buff.ExpiresAt-now;
                    Append(remaining>0 && remaining<600?$"{name} {remaining:0.0}秒":name);
                }
                if(buffs.Length>6) Append($"另有 {buffs.Length-6} 个");
            }
            view.Statuses=text.Length>0?"状态："+text:"无状态效果";
            view.Description=archetype && !string.IsNullOrWhiteSpace(archetype.description)?archetype.description.Trim():"";
            return true;
        }
        private static void Affix(MonsterAffix affixes,MonsterAffix flag,string label) {if((affixes&flag)!=0) Append(label);}
        private static void Append(string value) {if(text.Length>0) text.Append("、");text.Append(value);}
        /// <summary>higherIsStronger：数值越高对敌人越有利（攻击、移速）；攻击间隔反之。</summary>
        private static string Modified(string value,float current,float baseline,bool higherIsStronger)
        {
            if(baseline<=0 || math.abs(current-baseline)<baseline*0.01f) return value;
            bool stronger=higherIsStronger?current>baseline:current<baseline;
            float percent=(current/baseline-1)*100;
            return $"<color={(stronger?Empowered:Weakened)}>{value}（{(percent>0?"+":"")}{percent:0}%）</color>";
        }
    }
}
