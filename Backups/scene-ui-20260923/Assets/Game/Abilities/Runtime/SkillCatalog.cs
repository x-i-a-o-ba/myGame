using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>Inspector 可编辑资源集合；编译为会话拥有的只读 Blob，销毁会话前先结束所有引用它的 Job。</summary>
    [CreateAssetMenu(menuName="Roguelike/Abilities/Catalog")]
    public sealed class SkillCatalog : ScriptableObject
    {
        public SkillDefinition[] skills;
        public BuffDefinition[] buffs;
        public PassiveDefinition[] passives;

        public BlobAssetReference<AbilityLibrary> Compile()
        {
            if(skills==null || skills.Length==0 || buffs==null || passives==null)
                throw new InvalidOperationException("技能库缺少技能/Buff/被动数组。");
            if(passives.Length>30) throw new InvalidOperationException("首版被动位图最多 30 个槽。");
            var ids=new HashSet<string>();
            void Id(string id) { if(string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidOperationException("技能库 ID 为空或重复："+id); }
            using(var builder=new BlobBuilder(Allocator.Temp))
            {
                ref var root=ref builder.ConstructRoot<AbilityLibrary>();
                var b=builder.Allocate(ref root.Buffs,buffs.Length);
                for(int i=0;i<buffs.Length;i++)
                {
                    var d=buffs[i]; if(!d) throw new InvalidOperationException("Buff 引用为空。"); Id(d.stableId);
                    if(d.duration<0 || d.interval<0.05f || d.maxStacks<1 || d.maxStacks>20)
                        throw new InvalidOperationException("Buff 时长/周期/层数不合法："+d.name);
                    b[i]=new BuffData { Duration=d.duration, Interval=d.interval, PeriodicDamage=d.periodicDamage,
                        SpeedPercent=d.moveSpeedPercent, AttackFlat=d.attackDamageFlat, Regen=d.healthRegen,
                        Stacking=d.stacking, MaxStacks=d.maxStacks, Dispellable=d.dispellable };
                }
                var s=builder.Allocate(ref root.Skills,skills.Length);
                for(int i=0;i<skills.Length;i++)
                {
                    var d=skills[i]; if(!d) throw new InvalidOperationException("技能引用为空。"); Id(d.stableId);
                    if(d.radius<=0 || d.castRange<0 || d.cooldown<0 || d.manaCost<0 || d.areaInterval<0.05f || d.areaDuration<=0)
                        throw new InvalidOperationException("技能范围/时间/消耗不合法："+d.name);
                    s[i]=new SkillData { Delivery=d.delivery, Targets=d.targets, CastRange=d.castRange, Radius=d.radius,
                        Cooldown=d.cooldown, ManaCost=d.manaCost, AreaDuration=d.areaDuration, AreaInterval=d.areaInterval, LevelPower=d.levelPower };
                    CompileEffects(builder,ref s[i].Effects,d.effects);
                    if(d.delivery==SkillDelivery.PersistentCircle)
                        foreach(var effect in d.effects)
                            if(effect.kind!=EffectKind.ApplyBuff || effect.buff.stacking!=BuffStackRule.Refresh)
                                throw new InvalidOperationException("首版持续区域只刷新不叠层 Buff；持续伤害请放入 Buff 周期。");
                }
                var p=builder.Allocate(ref root.Passives,passives.Length);
                for(int i=0;i<passives.Length;i++)
                {
                    var d=passives[i]; if(!d) throw new InvalidOperationException("被动引用为空。"); Id(d.stableId);
                    p[i].Trigger=d.trigger;
                    CompileEffects(builder,ref p[i].Effects,d.effects);
                    if(d.trigger==PassiveTrigger.WhileLearned)
                        foreach(var e in d.effects)
                            if(e.kind!=EffectKind.ApplyBuff || e.buff.duration!=0)
                                throw new InvalidOperationException("常驻被动首版只接受永久 Buff，确保能够撤销贡献。");
                }
                return builder.CreateBlobAssetReference<AbilityLibrary>(Allocator.Persistent);
            }
        }
        private void CompileEffects(BlobBuilder builder,ref BlobArray<EffectData> destination,EffectEntry[] input)
        {
            if(input==null || input.Length==0) throw new InvalidOperationException("效果列表为空。");
            var output=builder.Allocate(ref destination,input.Length);
            for(int i=0;i<input.Length;i++)
            {
                var e=input[i]; int index=e.buff?Array.IndexOf(buffs,e.buff):-1;
                if(e.kind==EffectKind.ApplyBuff && (index<0 || e.buffScale<=0)) throw new InvalidOperationException("Buff 未在库中登记或倍率不合法。");
                if(e.amount<0) throw new InvalidOperationException("即时效果数值不能为负。");
                output[i]=new EffectData { Kind=e.kind, Amount=e.amount, Buff=index, BuffScale=e.buffScale };
            }
        }
    }
}
