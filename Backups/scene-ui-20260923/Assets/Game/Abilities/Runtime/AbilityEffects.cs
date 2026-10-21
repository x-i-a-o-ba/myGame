using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>唯一的即时效果/Buff 应用入口。调用者负责目标写入所有权，不能被多个 Job 同时写同一目标。</summary>
    public static class AbilityEffects
    {
        public static void Apply(ref AbilityUnit target,DynamicBuffer<ActiveBuff> buffs,
            ref AbilityLibrary library,in EffectRequest request,float now)
        {
            if(target.Health<=0) return;
            var effect=request.Effect;
            switch(effect.Kind)
            {
                case EffectKind.Damage: target.Health=math.max(0,target.Health-effect.Amount*request.Power); break;
                case EffectKind.Heal: target.Health=math.min(target.MaxHealth,target.Health+effect.Amount*request.Power); break;
                case EffectKind.ApplyBuff:
                    ref var definition=ref library.Buffs[effect.Buff];
                    int index=-1;
                    for(int i=0;i<buffs.Length;i++)
                        if(buffs[i].Definition==effect.Buff && buffs[i].Source==request.Source && buffs[i].Origin==request.Origin) { index=i; break; }
                    var active=index>=0?buffs[index]:new ActiveBuff { Definition=effect.Buff,Source=request.Source,Origin=request.Origin,
                        NextTick=now+definition.Interval };
                    active.Stacks=definition.Stacking==BuffStackRule.AddAndRefresh?math.min(active.Stacks+1,definition.MaxStacks):1;
                    active.ExpiresAt=definition.Duration<=0?float.MaxValue:now+definition.Duration;
                    // 刷新不会重置周期，连续普攻不会让中毒永远无法触发。
                    active.Scale=request.Power*effect.BuffScale;
                    if(index<0) buffs.Add(active); else buffs[index]=active;
                    break;
            }
            Recalculate(ref target,buffs,ref library);
        }
        public static void Recalculate(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library)
        {
            float speed=0,attack=0,regen=0;
            for(int i=0;i<buffs.Length;i++)
            {
                var active=buffs[i]; ref var d=ref library.Buffs[active.Definition];
                float magnitude=active.Scale*active.Stacks;
                speed+=d.SpeedPercent*magnitude; attack+=d.AttackFlat*magnitude; regen+=d.Regen*magnitude;
            }
            unit.Speed=unit.BaseSpeed*math.clamp(1+speed,0.1f,4);
            unit.Attack=math.max(0,unit.BaseAttack+attack); unit.Regen=math.max(0,regen);
        }
        public static void Tick(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library,float now,float dt)
        {
            if(unit.Health<=0) { buffs.Clear(); Recalculate(ref unit,buffs,ref library); return; }
            for(int i=buffs.Length-1;i>=0;i--)
            {
                var active=buffs[i]; ref var d=ref library.Buffs[active.Definition];
                if(d.PeriodicDamage>0)
                {
                    // 按时间点补齐有效期内的周期；卡帧不静默丢掉伤害，周期伤害不产生普攻事件。
                    float until=math.min(now,active.ExpiresAt);
                    if(active.NextTick<=until)
                    {
                        int ticks=1+(int)math.floor((until-active.NextTick+0.00001f)/d.Interval);
                        unit.Health=math.max(0,unit.Health-ticks*d.PeriodicDamage*active.Scale*active.Stacks);
                        active.NextTick+=ticks*d.Interval;
                    }
                }
                if(now>=active.ExpiresAt) buffs.RemoveAt(i); else buffs[i]=active;
            }
            Recalculate(ref unit,buffs,ref library);
            if(unit.Health>0) unit.Health=math.min(unit.MaxHealth,unit.Health+unit.Regen*dt);
            unit.Mana=math.min(unit.MaxMana,unit.Mana+dt*3);
        }
        public static void Dispel(ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library)
        {
            for(int i=buffs.Length-1;i>=0;i--) if(library.Buffs[buffs[i].Definition].Dispellable) buffs.RemoveAt(i);
            Recalculate(ref unit,buffs,ref library);
        }
        public static void SetPassive(Entity owner,int index,bool enabled,ref AbilityUnit unit,DynamicBuffer<ActiveBuff> buffs,ref AbilityLibrary library,float now)
        {
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
