using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>权威生命结算快照。只有实际生命损失才派生伤害事件，击杀只记录存活到死亡的转变。</summary>
    public struct EffectOutcome
    {
        public float RequestedDamage,ActualDamage;
        public bool Killed;
    }
    /// <summary>即时效果与 Buff 状态的共同入口。调用者持有目标写权限；几何、投递和来源目标选择由调度层负责。</summary>
    public static class AbilityEffects
    {
        /// <summary>所有单位的基础法力回复（点/秒）；AbilityUnit.ManaRegenBonus 在此之上相加。</summary>
        public const float BaseManaRegen=3;
        /// <summary>伤害减免上限（0.8 = 最多减免 80%）。</summary>
        public const float MaximumDamageReduction=0.7f;
        public static EffectOutcome Apply(ref AbilityUnit target,DynamicBuffer<ActiveBuff> buffs,
            ref AbilityLibrary library,in EffectRequest request,float now)
        {
            if(target.Health<=0) return default;
            var effect=request.Effect;
            switch(effect.Kind)
            {
                case EffectKind.Damage:
                    return Damage(ref target,buffs,ref library,effect.Amount*request.Power);
                case EffectKind.Heal:
                    target.Health=math.min(target.MaxHealth,target.Health+effect.Amount*request.Power);
                    return default;
                case EffectKind.Dispel:
                    Dispel(ref target,buffs,ref library);
                    return default;
                case EffectKind.ApplyBuff:
                    ref var definition=ref library.Buffs[effect.Buff];
                    if(target.ControlImmune && (definition.Controls!=ControlFlags.None || definition.SpeedPercent<0)) return default;
                    int index=-1;
                    if(definition.SourcePolicy!=BuffSourcePolicy.Independent)
                        for(int i=0;i<buffs.Length;i++)
                            if(buffs[i].Definition==effect.Buff &&
                                (definition.SourcePolicy==BuffSourcePolicy.ByDefinition ||
                                 (buffs[i].Source==request.Source && buffs[i].Origin==request.Origin)))
                            { index=i; break; }
                    var active=index>=0?buffs[index]:new ActiveBuff { Definition=effect.Buff,
                        NextTick=now+definition.Interval };
                    // Definition-level merging adopts the latest application attribution and potency.
                    active.Source=request.Source; active.Origin=request.Origin; active.SourceAttack=request.SourceAttack; active.ExtraTargets=request.ExtraTargets;
                    active.Stacks=definition.Stacking==BuffStackRule.AddAndRefresh?
                        math.min(active.Stacks+1,definition.MaxStacks):1;
                    active.ExpiresAt=definition.Duration<=0?float.MaxValue:now+definition.Duration*(request.DurationScale>0?request.DurationScale:1);
                    // Refresh keeps the existing periodic clock: repeated hits cannot postpone every tick.
                    active.Scale=request.Power*effect.BuffScale;
                    if(index<0) buffs.Add(active); else buffs[index]=active;
                    Recalculate(ref target,buffs,ref library);
                    return default;
            }
            return default;
        }

        /// <summary>普通攻击、技能即时效果及旧周期伤害共用此入口；不广播普攻或技能事件。目标的伤害减免在这里统一生效。</summary>
        public static EffectOutcome Damage(ref AbilityUnit target,DynamicBuffer<ActiveBuff> buffs,
            ref AbilityLibrary library,float amount)
        {
            if(target.Health<=0) return default;
            float before=target.Health;
            float taken=math.max(0,amount)*(1+math.max(0,target.DamageTaken))*(1-math.clamp(target.DamageReduction+target.BuffDamageReduction,0,MaximumDamageReduction));
            float absorbed=math.min(math.max(0,target.Shield),taken); target.Shield-=absorbed; taken-=absorbed;
            target.Health=math.max(0,before-taken);
            var result=new EffectOutcome {RequestedDamage=math.max(0,amount),ActualDamage=before-target.Health,Killed=target.Health<=0};
            if(result.Killed) { buffs.Clear(); Recalculate(ref target,buffs,ref library); }
            return result;
        }

        /// <summary>从基础值和全部存活贡献重建属性，移除贡献不做反向减法。Multiply 按强度/层数乘方。英雄成长只改基础值后调用本方法。</summary>
        public static void Recalculate(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library)
        {
            float3 add=0,percent=0,multiply=1;
            float regen=0;
            unit.BuffDamageReduction=0; unit.RegenMaxHealth=0; unit.DamageTaken=0; unit.Reflect=0; unit.ExtraTargets=0;
            unit.ControlImmune=false; unit.PierceBasics=false; unit.AttackSplashRadius=0; unit.AttackSplashRatio=0;
            ControlFlags controls=ControlFlags.None;
            for(int i=0;i<buffs.Length;i++)
            {
                var active=buffs[i]; ref var definition=ref library.Buffs[active.Definition];
                float strength=active.Scale*active.Stacks;
                // Historical fields are contributions too; explicitly authored modifiers can coexist.
                percent.x+=definition.SpeedPercent*strength;
                add.y+=definition.AttackFlat*strength;
                regen+=definition.Regen*strength;
                controls|=definition.Controls;
                unit.BuffDamageReduction+=definition.DamageReduction*strength; unit.RegenMaxHealth+=definition.RegenMaxHealth*strength;
                unit.DamageTaken+=definition.DamageTaken*strength; unit.Reflect+=definition.Reflect;
                unit.ExtraTargets+=definition.ExtraTargets+active.ExtraTargets; unit.ControlImmune|=definition.ControlImmune; unit.PierceBasics|=definition.PierceBasics;
                unit.AttackSplashRadius=math.max(unit.AttackSplashRadius,definition.AttackSplashRadius); unit.AttackSplashRatio+=definition.AttackSplashRatio;
                for(int m=0;m<definition.Modifiers.Length;m++)
                {
                    var modifier=definition.Modifiers[m];
                    int attribute=(int)modifier.Attribute;
                    switch(modifier.Operation)
                    {
                        case ModifierOperation.Add: add[attribute]+=modifier.Value*strength; break;
                        case ModifierOperation.Percent: percent[attribute]+=modifier.Value*strength; break;
                        case ModifierOperation.Multiply:
                            multiply[attribute]*=math.pow(modifier.Value,strength); break;
                    }
                }
            }
            float3 final=math.max(0,(new float3(unit.BaseSpeed,unit.BaseAttack,1+unit.AttackSpeedBonus)+add)*(1+percent)*multiply);
            unit.Speed=final.x; unit.Attack=final.y; unit.AttackSpeedMultiplier=final.z;
            unit.Regen=math.max(0,unit.BaseRegen+regen+unit.MaxHealth*unit.RegenMaxHealth); unit.Controls=unit.ControlImmune?ControlFlags.None:controls;
            if(unit.ControlImmune || unit.SlowImmune) unit.Speed=math.max(unit.BaseSpeed,unit.Speed);
        }

        /// <summary>补齐有效期内周期伤害；刷新不改周期起点。死亡清贡献；周期伤害不派生普攻命中事件。</summary>
        public static void Tick(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library,float now,float dt)
        {
            if(unit.Health<=0) { buffs.Clear(); Recalculate(ref unit,buffs,ref library); return; }
            bool removed=false;
            for(int i=buffs.Length-1;i>=0;i--)
            {
                var active=buffs[i]; ref var definition=ref library.Buffs[active.Definition];
                if(definition.PeriodicDamage>0 || definition.PeriodicAttackRatio>0)
                {
                    float until=math.min(now,active.ExpiresAt);
                    if(active.NextTick<=until)
                    {
                        int ticks=1+(int)math.floor((until-active.NextTick+0.00001f)/definition.Interval);
                        // 旧 periodicDamage 没有逐跳配方回调，允许聚合补跳；仍使用统一生命入口。
                        Damage(ref unit,buffs,ref library,ticks*(definition.PeriodicDamage+definition.PeriodicAttackRatio*active.SourceAttack)*active.Scale*active.Stacks);
                        if(unit.Health<=0) return; // Damage 已清理 Buff，不能再写入失效的旧索引。
                        active.NextTick+=ticks*definition.Interval;
                    }
                }
                if(now>=active.ExpiresAt) { buffs.RemoveAt(i); removed=true; }
                else buffs[i]=active;
            }
            if(unit.Health<=0) { buffs.Clear(); Recalculate(ref unit,buffs,ref library); return; }
            // Empty-buffer initialization preserves compatibility with old callers that leave the new multiplier at 0.
            if(removed || unit.AttackSpeedMultiplier==0) Recalculate(ref unit,buffs,ref library);
            if(unit.Health>0) unit.Health=math.min(unit.MaxHealth,unit.Health+unit.Regen*dt);
            unit.Mana=math.clamp(unit.Mana+dt*(BaseManaRegen+unit.ManaRegenBonus),0,math.max(0,unit.MaxMana));
        }

        public static void Dispel(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library)
        {
            for(int i=buffs.Length-1;i>=0;i--) if(library.Buffs[buffs[i].Definition].Dispellable) buffs.RemoveAt(i);
            Recalculate(ref unit,buffs,ref library);
        }

        /// <summary>旧独立示例的被动桥接。正式英雄技能不使用此位图或保留的 1000 来源偏移。</summary>
        public static void SetPassive(Entity owner,int index,bool enabled,ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library,float now)
        {
            if(index<0 || index>=library.Passives.Length || index>=30) return;
            uint mask=1u<<index; bool old=(unit.PassiveMask&mask)!=0;
            if(old==enabled) return;
            unit.PassiveMask=enabled?unit.PassiveMask|mask:unit.PassiveMask&~mask;
            int origin=1000+index;
            if(!enabled)
            {
                for(int i=buffs.Length-1;i>=0;i--) if(buffs[i].Origin==origin && buffs[i].Source==owner) buffs.RemoveAt(i);
                Recalculate(ref unit,buffs,ref library); return;
            }
            ref var passive=ref library.Passives[index];
            if(passive.Trigger!=PassiveTrigger.WhileLearned) return;
            for(int i=0;i<passive.Effects.Length;i++)
                Apply(ref unit,buffs,ref library,new EffectRequest{Source=owner,Target=owner,Origin=origin,Effect=passive.Effects[i],Power=1},now);
        }
    }
}
