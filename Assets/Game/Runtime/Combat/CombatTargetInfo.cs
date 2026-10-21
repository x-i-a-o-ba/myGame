using System;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>左上角“选中目标”信息面板的展示快照；由控制器在 HUD 刷新时填充并复用，只含文字、数值与纹理。</summary>
    public sealed class TargetInfoView
    {
        public bool Visible;
        public EnemyTier Tier;
        public string Name, TierLabel, Subtitle, Description;
        public Texture Portrait, AttackIcon;
        public float HealthFraction; public string HealthText;
        public string Damage, AttackInterval, Range, MoveSpeed, Experience, Distance;
        /// <summary>目标是否在英雄当前攻击距离内（按体表距离计算，与模拟系统一致）。</summary>
        public bool InHeroRange;
        public string AttackName, AttackDetail, AttackStatus, Trait;
        /// <summary>状态效果标签，前 <see cref="StatusCount"/> 项有效。</summary>
        public readonly string[] Statuses = new string[CombatHud.TargetStatusSlots];
        public int StatusCount;
    }

    /// <summary>组装目标信息所需的会话上下文：种类名称/预制体（下标 = CombatEnemy.Kind）、Buff 定义与英雄位置。</summary>
    public struct TargetInfoContext
    {
        public string[] KindNames;
        public EnemyArchetype[] KindArchetypes;
        public BuffDefinition[] Buffs;
        public float3 HeroPosition;
        public float HeroAttackRange;
        /// <summary>技能运行时钟（AbilityRuntime.Time），Buff 到期时间以它为基准。</summary>
        public float Now;
    }

    /// <summary>
    /// 只读地把一个敌人实体的权威状态（AbilityUnit、CombatEnemy、自动攻击、行为、Buff）整理成 <see cref="TargetInfoView"/>。
    /// 不写入任何 ECS 数据；目标不存在或已死亡时返回 false。
    /// </summary>
    public static class TargetInfoBuilder
    {
        private const string Weakened = "#8FD3FF", Empowered = "#FF8A6A";

        public static bool Build(EntityManager manager, Entity entity, in TargetInfoContext context, TargetInfoView view)
        {
            view.Visible = false; view.StatusCount = 0;
            if (entity == Entity.Null || !manager.Exists(entity) || !manager.HasComponent<CombatEnemy>(entity) ||
                !manager.HasComponent<AbilityUnit>(entity) || !manager.HasComponent<LocalTransform>(entity)) return false;
            var unit = manager.GetComponentData<AbilityUnit>(entity);
            if (unit.Health <= 0) return false;
            var enemy = manager.GetComponentData<CombatEnemy>(entity);
            var transform = manager.GetComponentData<LocalTransform>(entity);
            var data = manager.HasComponent<AutoAttackData>(entity) ? manager.GetComponentData<AutoAttackData>(entity) : default;
            var state = manager.HasComponent<AutoAttackState>(entity) ? manager.GetComponentData<AutoAttackState>(entity) : default;
            var behavior = manager.HasComponent<EnemyBehaviorData>(entity) ? manager.GetComponentData<EnemyBehaviorData>(entity) : default;
            EnemyArchetype archetype = context.KindArchetypes != null && enemy.Kind >= 0 && enemy.Kind < context.KindArchetypes.Length
                ? context.KindArchetypes[enemy.Kind] : null;
            var attackModule = archetype ? archetype.attack : null;

            // 身份
            view.Visible = true;
            view.Tier = enemy.Tier;
            view.Name = context.KindNames != null && enemy.Kind >= 0 && enemy.Kind < context.KindNames.Length ? context.KindNames[enemy.Kind]
                : archetype ? archetype.Label : "敌人";
            view.TierLabel = TierLabel(enemy.Tier);
            view.Subtitle = $"第 {math.max(1, enemy.Wave)} 波 · {EnemyArchetype.BehaviorLabel(behavior.Behavior)}";
            view.Description = archetype && !string.IsNullOrWhiteSpace(archetype.description) ? archetype.description.Trim() : "";
            view.AttackIcon = attackModule ? attackModule.icon : null;
            view.Portrait = archetype && archetype.portrait ? archetype.portrait : view.AttackIcon;

            // 生命
            float maxHealth = math.max(1, unit.MaxHealth);
            view.HealthFraction = math.saturate(unit.Health / maxHealth);
            view.HealthText = $"{Mathf.CeilToInt(unit.Health):N0} / {Mathf.CeilToInt(maxHealth):N0}";

            // 属性：数值偏离基础值时附带百分比（被削弱为冰蓝色、被强化为橙红色）。
            float attackSpeed = unit.AttackSpeedMultiplier > 0 ? unit.AttackSpeedMultiplier : 1;
            float interval = AutoAttackLogic.Interval(data, attackSpeed);
            float reach = data.Range + enemy.Reach;
            view.Damage = Modified($"{unit.Attack:0}", unit.Attack, unit.BaseAttack);
            view.AttackInterval = Modified($"{interval:0.00} 秒", interval, data.BaseAttackTime, false);
            view.Range = $"{reach:0.#} 米";
            view.MoveSpeed = Modified($"{unit.Speed:0.#}", unit.Speed, unit.BaseSpeed);
            view.Experience = $"{enemy.Experience:0}";
            float2 offset = transform.Position.xz - context.HeroPosition.xz;
            float distance = math.length(offset);
            view.Distance = $"{distance:0.0} 米";
            view.InHeroRange = distance - CombatSimulationSystem.BodyRadius * transform.Scale <= context.HeroAttackRange;

            // 自动攻击被动模块
            view.AttackName = attackModule ? attackModule.Label : data.Delivery == AttackDelivery.Projectile ? "远程攻击" : "近战攻击";
            string delivery = data.Delivery == AttackDelivery.Projectile ? $"弹道 {data.ProjectileSpeed:0} 米/秒" : "近战";
            view.AttackDetail = $"被动 · {delivery} · 前摇 {AutoAttackLogic.Point(data, attackSpeed):0.00} 秒 · 后摇 {AutoAttackLogic.BackswingTime(data, attackSpeed):0.00} 秒";
            if ((unit.Controls & ControlFlags.AttackBlocked) != 0) view.AttackStatus = "<color=#FF8A6A>无法攻击</color>";
            else if (state.Phase == AttackPhase.Windup) view.AttackStatus = "<color=#FFB347>前摇中</color>";
            else if (state.Phase == AttackPhase.Backswing) view.AttackStatus = "后摇";
            else if (state.Cooldown > 0.05f) view.AttackStatus = $"冷却 {state.Cooldown:0.0} 秒";
            else view.AttackStatus = "就绪";
            view.Trait = Trait(enemy, behavior, data, state, unit, reach);

            // 状态效果
            if (manager.HasBuffer<ActiveBuff>(entity))
            {
                var buffs = manager.GetBuffer<ActiveBuff>(entity, true);
                int slots = view.Statuses.Length;
                int shown = buffs.Length > slots ? slots - 1 : buffs.Length;
                for (int i = 0; i < shown; i++) view.Statuses[i] = BuffLabel(buffs[i], context);
                if (buffs.Length > slots) view.Statuses[shown++] = $"+{buffs.Length - (slots - 1)} 个状态";
                view.StatusCount = shown;
            }
            for (int i = view.StatusCount; i < view.Statuses.Length; i++) view.Statuses[i] = "";
            return true;
        }

        public static string TierLabel(EnemyTier tier) => tier == EnemyTier.Boss ? "首领" : tier == EnemyTier.Elite ? "精英" : "普通";

        /// <summary>按行为说明当前出手方式与行为状态（冲锋冷却、自爆前摇等）。</summary>
        private static string Trait(in CombatEnemy enemy, in EnemyBehaviorData behavior, in AutoAttackData data, in AutoAttackState state, in AbilityUnit unit, float reach)
        {
            switch (behavior.Behavior)
            {
                case EnemyBehavior.Ranged:
                {
                    float preferred = math.max(0.5f, data.Range * behavior.PreferredDistance) + enemy.Reach;
                    return $"远程游击：约 {preferred:0.#} 米处射击，英雄靠近 {behavior.RetreatDistance:0.#} 米内后撤";
                }
                case EnemyBehavior.Charger:
                {
                    float impact = unit.Attack * behavior.ChargeDamageMultiplier;
                    if (enemy.ChargeTimer > 0) return $"<color=#FF8A6A>冲锋中！</color>撞击造成 {impact:0} 伤害";
                    string ready = enemy.ChargeReadyIn > 0.05f ? $"冷却 {enemy.ChargeReadyIn:0.0} 秒" : "冲锋就绪";
                    return $"冲锋：{behavior.ChargeTriggerRange:0.#} 米内突进，撞击 {impact:0} 伤害 · {ready}";
                }
                case EnemyBehavior.Exploder:
                {
                    float blast = unit.Attack * behavior.ExplosionDamageMultiplier;
                    float radius = behavior.ExplosionRadius + enemy.Reach;
                    if (state.Phase == AttackPhase.Windup) return $"<color=#FF8A6A>即将自爆！</color>{radius:0.#} 米内造成 {blast:0} 伤害";
                    return $"自爆：贴身引爆，{radius:0.#} 米内造成 {blast:0} 伤害";
                }
                default:
                    return $"近战追击：追到 {reach:0.#} 米内出手";
            }
        }

        private static string BuffLabel(in ActiveBuff buff, in TargetInfoContext context)
        {
            var definitions = context.Buffs;
            var definition = definitions != null && buff.Definition >= 0 && buff.Definition < definitions.Length ? definitions[buff.Definition] : null;
            string name = definition ? (string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName) : "状态";
            string stacks = buff.Stacks > 1 ? $"×{buff.Stacks}" : "";
            string time = buff.ExpiresAt == float.MaxValue ? "常驻" : $"{math.max(0, buff.ExpiresAt - context.Now):0.0}秒";
            return $"{name}{stacks} {time}";
        }

        /// <summary>当前值与基础值不同（超过 0.5%）时附带彩色百分比；higherIsStronger 为假表示数值越大越弱（例如攻击间隔）。</summary>
        private static string Modified(string text, float current, float baseline, bool higherIsStronger = true)
        {
            if (baseline <= 0 || !float.IsFinite(current) || math.abs(current / baseline - 1) < 0.005f) return text;
            float percent = (current / baseline - 1) * 100;
            bool weakened = higherIsStronger ? percent < 0 : percent > 0;
            string sign = percent > 0 ? "+" : "";
            return $"<color={(weakened ? Weakened : Empowered)}>{text}</color><size=11> {sign}{percent:0}%</size>";
        }
    }
}
