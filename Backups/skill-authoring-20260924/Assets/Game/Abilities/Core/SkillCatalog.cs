using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>资源注册表与唯一编译边界。验证引用/枚举/有限数值后生成会话拥有的只读 Blob。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/技能库")]
    public sealed class SkillCatalog : ScriptableObject
    {
        [InspectorName("技能定义")] public SkillDefinition[] skills=Array.Empty<SkillDefinition>();
        [InspectorName("Buff 定义")] public BuffDefinition[] buffs=Array.Empty<BuffDefinition>();
        [InspectorName("旧示例被动定义")] public PassiveDefinition[] passives=Array.Empty<PassiveDefinition>();
        public const int MaximumStages=32;
        public const int MaximumRepeats=32;

        /// <summary>调用方唯一拥有返回值；所有引用它的 Job 完成后再 Dispose。失败不返回半成品。</summary>
        public BlobAssetReference<AbilityLibrary> Compile()
        {
            if(skills==null || skills.Length==0 || buffs==null || passives==null)
                throw new InvalidOperationException("技能库必须包含技能，以及非 null 的 Buff/被动数组。");
            if(passives.Length>30) throw new InvalidOperationException("旧示例被动位图最多 30 个槽。");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            void Id(string id)
            {
                if(string.IsNullOrWhiteSpace(id) || id.Trim()!=id || !ids.Add(id))
                    throw new InvalidOperationException("技能库 ID 为空、带首尾空格或重复："+id);
            }
            using(var builder=new BlobBuilder(Allocator.Temp))
            {
                ref var root=ref builder.ConstructRoot<AbilityLibrary>();
                var b=builder.Allocate(ref root.Buffs,buffs.Length);
                for(int i=0;i<buffs.Length;i++)
                {
                    var d=buffs[i]; if(!d) throw new InvalidOperationException("Buff 引用为空，索引 "+i); Id(d.stableId);
                    ValidateBuff(d);
                    b[i]=new BuffData { Duration=d.duration, Interval=d.interval, PeriodicDamage=d.periodicDamage,
                        SpeedPercent=d.moveSpeedPercent, AttackFlat=d.attackDamageFlat, Regen=d.healthRegen,
                        Stacking=d.stacking, MaxStacks=d.maxStacks, Dispellable=d.dispellable,
                        Controls=d.controls, SourcePolicy=d.sourcePolicy };
                    var input=d.modifiers ?? Array.Empty<AttributeModifier>();
                    var modifiers=builder.Allocate(ref b[i].Modifiers,input.Length);
                    for(int m=0;m<input.Length;m++)
                    {
                        var item=input[m];
                        EnumValue(item.attribute,"Buff 属性："+d.name);
                        EnumValue(item.operation,"Buff 运算："+d.name);
                        Finite(item.value,"Buff 修饰数值："+d.name);
                        if(item.operation==ModifierOperation.Multiply && item.value<0)
                            throw new InvalidOperationException("Buff Multiply 不接受负数："+d.name);
                        modifiers[m]=new AttributeModifierData{Attribute=item.attribute,Operation=item.operation,Value=item.value};
                    }
                }
                var s=builder.Allocate(ref root.Skills,skills.Length);
                for(int i=0;i<skills.Length;i++)
                {
                    var d=skills[i]; if(!d) throw new InvalidOperationException("技能引用为空，索引 "+i); Id(d.stableId);
                    NonNegative(d.cooldown,"技能冷却："+d.name); NonNegative(d.manaCost,"技能消耗："+d.name);
                    NonNegative(d.castRange,"技能施法距离："+d.name);
                    NonNegative(d.rechargeSeconds,"技能充能："+d.name); NonNegative(d.levelPower,"技能等级倍率："+d.name);
                    if(d.maxCharges<1 || d.maxCharges>32 || d.requiredHits<0 || d.requiredHits>100000)
                        throw new InvalidOperationException("技能充能必须 1..32，所需命中层数必须 0..100000："+d.name);
                    EnumValue(d.aim,"技能瞄准："+d.name);
                    s[i]=new SkillData { Aim=d.aim,MaxCharges=d.maxCharges,
                        RechargeSeconds=d.rechargeSeconds>0?d.rechargeSeconds:d.cooldown,RequiredHits=d.requiredHits,
                        Cooldown=d.cooldown,ManaCost=d.manaCost,LevelPower=d.levelPower,
                        Delivery=d.delivery,Targets=d.targets,CastRange=d.castRange,Radius=d.radius,
                        AreaDuration=d.areaDuration,AreaInterval=d.areaInterval };
                    if(d.stages!=null && d.stages.Length>0)
                    {
                        if(d.stages.Length>MaximumStages) throw new InvalidOperationException("技能阶段超过 "+MaximumStages+"："+d.name);
                        var stages=builder.Allocate(ref s[i].Stages,d.stages.Length);
                        for(int stage=0;stage<d.stages.Length;stage++)
                        {
                            if(d.stages[stage]!=null && d.stages[stage].scope==TargetScope.Single &&
                                d.aim!=AimMode.Unit && d.stages[stage].delivery!=DeliveryKind.LinearProjectile)
                                throw new InvalidOperationException("单体阶段需要单位瞄准或直线首碰提供目标："+d.name+" / 阶段 "+stage);
                            CompileStage(builder,ref stages[stage],d.stages[stage],d.name+" / 阶段 "+stage);
                        }
                        // Historical sample reads these fields. Formal execution exclusively consumes Stages.
                        ref var first=ref stages[0];
                        s[i].Delivery=d.aim==AimMode.Self?SkillDelivery.SelfCircle:SkillDelivery.InstantCircle;
                        s[i].Targets=first.Targets; s[i].Radius=first.Radius;
                        CompileModules(builder,ref s[i].Effects,d.stages[0].effects,true,d.name);
                    }
                    else
                    {
                        ValidateLegacySkill(d);
                        CompileEffects(builder,ref s[i].Effects,d.effects);
                        // Legacy sample remains on its own scheduler; no fake one-shot translation of a persistent area.
                        builder.Allocate(ref s[i].Stages,0);
                        if(d.delivery==SkillDelivery.PersistentCircle)
                            foreach(var effect in d.effects)
                                if(effect.kind!=EffectKind.ApplyBuff || effect.buff.stacking!=BuffStackRule.Refresh)
                                    throw new InvalidOperationException("旧示例持续区域仅支持刷新型 Buff："+d.name);
                    }
                }
                var p=builder.Allocate(ref root.Passives,passives.Length);
                for(int i=0;i<passives.Length;i++)
                {
                    var d=passives[i]; if(!d) throw new InvalidOperationException("被动引用为空。"); Id(d.stableId);
                    EnumValue(d.trigger,"被动触发："+d.name);
                    p[i].Trigger=d.trigger; CompileEffects(builder,ref p[i].Effects,d.effects);
                    if(d.trigger==PassiveTrigger.WhileLearned)
                        foreach(var e in d.effects)
                            if(e.kind!=EffectKind.ApplyBuff || e.buff.duration!=0)
                                throw new InvalidOperationException("常驻被动只接受永久 Buff，确保能够撤销贡献："+d.name);
                }
                return builder.CreateBlobAssetReference<AbilityLibrary>(Allocator.Persistent);
            }
        }

        private void CompileStage(BlobBuilder builder,ref StageData output,SkillStage input,string context)
        {
            if(input==null) throw new InvalidOperationException("空技能阶段："+context);
            EnumValue(input.delivery,context+" 投递"); EnumValue(input.scope,context+" 目标范围");
            EnumValue(input.shape,context+" 几何"); EnumValue(input.targets,context+" 目标关系");
            NonNegative(input.delay,context+" 延迟"); NonNegative(input.repeatInterval,context+" 重复间隔");
            NonNegative(input.range,context+" 距离"); NonNegative(input.radius,context+" 半径");
            NonNegative(input.hitRadius,context+" 碰撞半径"); NonNegative(input.speed,context+" 弹速");
            Finite(input.halfAngleDegrees,context+" 半角");
            if(input.repeatCount<1 || input.repeatCount>MaximumRepeats ||
                (input.repeatCount>1 && input.repeatInterval<=0) || input.halfAngleDegrees<=0 || input.halfAngleDegrees>180)
                throw new InvalidOperationException("重复次数、间隔或扇形半角无效："+context);
            if(input.scope==TargetScope.Area &&
                ((input.shape!=AreaShape.Cone && input.radius<=0) ||
                 (input.shape!=AreaShape.Circle && input.range<=0)))
                throw new InvalidOperationException("圆/线需要正半径，扇形/线需要正长度："+context);
            if(input.delivery!=DeliveryKind.Instant && input.speed<=0)
                throw new InvalidOperationException("弹体必须具有正速度："+context);
            if(input.delivery==DeliveryKind.LinearProjectile && (input.range<=0 || input.hitRadius<=0))
                throw new InvalidOperationException("直线弹体必须具有正距离和碰撞半径："+context);
            bool hasEffects=input.effects!=null && input.effects.Length>0;
            bool hasImpact=input.impactEffects!=null && input.impactEffects.Length>0;
            if(!hasEffects && !hasImpact) throw new InvalidOperationException("技能阶段不能没有效果："+context);
            if(hasImpact && input.delivery!=DeliveryKind.LinearProjectile)
                throw new InvalidOperationException("碰撞目标效果仅支持直线弹体："+context);
            output=new StageData { Delay=input.delay,RepeatCount=input.repeatCount,RepeatInterval=input.repeatInterval,
                Delivery=input.delivery,Scope=input.scope,Shape=input.shape,Targets=input.targets,
                Range=input.range,Radius=input.radius,HitRadius=input.hitRadius,HalfAngleDegrees=input.halfAngleDegrees,Speed=input.speed };
            CompileModules(builder,ref output.Effects,input.effects,true,context);
            CompileModules(builder,ref output.ImpactEffects,input.impactEffects,true,context);
        }

        private void CompileModules(BlobBuilder builder,ref BlobArray<EffectData> destination,EffectDefinition[] input,bool allowEmpty,string context)
        {
            int count=input?.Length??0;
            if(!allowEmpty && count==0) throw new InvalidOperationException("效果模块列表为空："+context);
            if(count>64) throw new InvalidOperationException("单段效果模块最多 64 个："+context);
            var output=builder.Allocate(ref destination,count);
            for(int i=0;i<count;i++)
            {
                var definition=input[i];
                if(!definition) throw new InvalidOperationException("效果模块引用为空："+context+" / "+i);
                var effect=definition.Compile();
                if(effect.Kind==EffectKind.ApplyBuff)
                    effect.Buff=definition.ReferencedBuff?Array.IndexOf(buffs,definition.ReferencedBuff):-1;
                ValidateEffect(effect,definition.name);
                output[i]=effect;
            }
        }

        private void CompileEffects(BlobBuilder builder,ref BlobArray<EffectData> destination,EffectEntry[] input)
        {
            if(input==null || input.Length==0 || input.Length>64) throw new InvalidOperationException("旧效果列表必须包含 1..64 项。");
            var output=builder.Allocate(ref destination,input.Length);
            for(int i=0;i<input.Length;i++)
            {
                var e=input[i];
                var value=new EffectData { Kind=e.kind,Amount=e.amount,
                    Buff=e.buff?Array.IndexOf(buffs,e.buff):-1,BuffScale=e.buffScale,Target=EffectTarget.Selected };
                ValidateEffect(value,"旧效果 "+i); output[i]=value;
            }
        }

        private static void ValidateEffect(EffectData e,string context)
        {
            EnumValue(e.Kind,context+" 效果类型"); EnumValue(e.Target,context+" 效果目标");
            NonNegative(e.Amount,context+" 数值"); NonNegative(e.BuffScale,context+" Buff 强度");
            if(e.Kind==EffectKind.ApplyBuff && (e.Buff<0 || e.BuffScale<=0))
                throw new InvalidOperationException("Buff 未登记或强度不合法："+context);
        }
        private static void ValidateBuff(BuffDefinition d)
        {
            NonNegative(d.duration,"Buff 时长："+d.name); Finite(d.interval,"Buff 周期："+d.name);
            NonNegative(d.periodicDamage,"Buff 周期伤害："+d.name); NonNegative(d.healthRegen,"Buff 恢复："+d.name);
            Finite(d.moveSpeedPercent,"Buff 移速："+d.name); Finite(d.attackDamageFlat,"Buff 攻击："+d.name);
            EnumValue(d.stacking,"Buff 叠层："+d.name); EnumValue(d.sourcePolicy,"Buff 来源："+d.name);
            if(d.interval<0.05f || d.maxStacks<1 || d.maxStacks>20 ||
                (d.controls & ~(ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked))!=0)
                throw new InvalidOperationException("Buff 周期、层数或控制标记不合法："+d.name);
            if(d.modifiers!=null && d.modifiers.Length>64)
                throw new InvalidOperationException("Buff 属性修饰最多 64 项："+d.name);
        }
        private static void ValidateLegacySkill(SkillDefinition d)
        {
            EnumValue(d.delivery,"旧技能投递："+d.name); EnumValue(d.targets,"旧技能目标："+d.name);
            NonNegative(d.castRange,"旧技能距离："+d.name); NonNegative(d.radius,"旧技能半径："+d.name);
            NonNegative(d.areaDuration,"旧技能时长："+d.name); NonNegative(d.areaInterval,"旧技能周期："+d.name);
            if(d.radius<=0 || d.areaDuration<=0 || d.areaInterval<0.05f)
                throw new InvalidOperationException("旧技能范围或持续区域参数不合法："+d.name);
        }
        private static void EnumValue<T>(T value,string context) where T:struct
        {
            if(!Enum.IsDefined(typeof(T),value)) throw new InvalidOperationException("未知枚举值："+context+" / "+value);
        }
        private static void Finite(float value,string context)
        {
            if(float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("数值必须有限："+context);
        }
        private static void NonNegative(float value,string context)
        {
            Finite(value,context); if(value<0) throw new InvalidOperationException("数值不能为负："+context);
        }
    }
}
