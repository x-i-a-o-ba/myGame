using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>即时效果与 Buff 状态的共同入口。调用者持有目标写权限；几何、投递和来源目标选择由调度层负责。</summary>
    public static class AbilityEffects
    {
        public static void Apply(ref AbilityUnit target,DynamicBuffer<ActiveBuff> buffs,
            ref AbilityLibrary library,in EffectRequest request,float now)
        {
            if(target.Health<=0) return;
            var effect=request.Effect;
            switch(effect.Kind)
            {
                case EffectKind.Damage:
                    target.Health=math.max(0,target.Health-effect.Amount*request.Power);
                    if(target.Health<=0) { buffs.Clear(); Recalculate(ref target,buffs,ref library); }
                    return;
                case EffectKind.Heal:
                    target.Health=math.min(target.MaxHealth,target.Health+effect.Amount*request.Power);
                    return;
                case EffectKind.Dispel:
                    Dispel(ref target,buffs,ref library);
                    return;
                case EffectKind.ApplyBuff:
                    ref var definition=ref library.Buffs[effect.Buff];
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
                    active.Source=request.Source; active.Origin=request.Origin;
                    active.Stacks=definition.Stacking==BuffStackRule.AddAndRefresh?
                        math.min(active.Stacks+1,definition.MaxStacks):1;
                    active.ExpiresAt=definition.Duration<=0?float.MaxValue:now+definition.Duration;
                    // Refresh keeps the existing periodic clock: repeated hits cannot postpone every tick.
                    active.Scale=request.Power*effect.BuffScale;
                    if(index<0) buffs.Add(active); else buffs[index]=active;
                    Recalculate(ref target,buffs,ref library);
                    return;
            }
        }

        /// <summary>从基础值和全部存活贡献重建属性，移除贡献不做反向减法。Multiply 按强度/层数乘方。</summary>
        public static void Recalculate(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library)
        {
            float3 add=0,percent=0,multiply=1;
            float regen=0;
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
            float3 final=math.max(0,(new float3(unit.BaseSpeed,unit.BaseAttack,1)+add)*(1+percent)*multiply);
            unit.Speed=final.x; unit.Attack=final.y; unit.AttackSpeedMultiplier=final.z;
            unit.Regen=math.max(0,regen); unit.Controls=controls;
        }

        /// <summary>补齐有效期内周期伤害；刷新不改周期起点。死亡清贡献；周期伤害不派生普攻命中事件。</summary>
        public static void Tick(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library,float now,float dt)
        {
            if(unit.Health<=0) { buffs.Clear(); Recalculate(ref unit,buffs,ref library); return; }
            bool removed=false;
            for(int i=buffs.Length-1;i>=0;i--)
            {
                var active=buffs[i]; ref var definition=ref library.Buffs[active.Definition];
                if(definition.PeriodicDamage>0)
                {
                    float until=math.min(now,active.ExpiresAt);
                    if(active.NextTick<=until)
                    {
                        int ticks=1+(int)math.floor((until-active.NextTick+0.00001f)/definition.Interval);
                        unit.Health=math.max(0,unit.Health-ticks*definition.PeriodicDamage*active.Scale*active.Stacks);
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
            unit.Mana=math.min(unit.MaxMana,unit.Mana+dt*3);
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
