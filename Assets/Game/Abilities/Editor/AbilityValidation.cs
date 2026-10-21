using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>隔离 World 中运行实际 ECS 系统与结算函数，验证配置共享和生命周期，不修改场景资源。</summary>
    public static class AbilityValidation
    {
        public static void Validate(SkillCatalog catalog)
        {
            using(var world=new World("Ability rules validation"))
            using(var runtime=new SkillDemoWorld(world,catalog))
            {
                var system=world.GetOrCreateSystemManaged<SkillSimulationSystem>(); runtime.Reset(4);
                double time=0;
                void Step(float dt) { time+=dt;world.SetTime(new TimeData(time,dt));system.Update(); }
                void Auto(bool enabled) { var state=runtime.State;state.AutoAttack=enabled;runtime.Manager.SetComponentData(runtime.Session,state); }
                Auto(false);
                var em=runtime.Manager; var hero=runtime.Hero;
                var target=runtime.CreateUnit(new float3(4,0.55f,0),1,1);
                runtime.Cast(0,new float3(4,0,0));Step(0.01f);
                Check(Near(em.GetComponentData<AbilityUnit>(target).Health,75),"Q applies instant damage");
                Check(em.GetBuffer<ActiveBuff>(target).Length==1,"Q applies burn only, not basic attack poison");
                float ready=em.GetBuffer<AbilitySlot>(hero)[0].ReadyAt;
                runtime.Cast(0,new float3(4,0,0));Step(0.01f);
                Check(runtime.State.LastFailure==CastFailure.Cooldown && Near(em.GetBuffer<AbilitySlot>(hero)[0].ReadyAt,ready),"cooldown rejection does not recommit");
                Check(em.GetComponentData<AbilityUnit>(hero).Mana>83,"failed cast does not pay mana");
                runtime.SetPassive(0,false);Check(Near(em.GetComponentData<AbilityUnit>(hero).Attack,18),"removing permanent passive restores base attribute");
                runtime.SetPassive(0,true);runtime.SetPassive(0,true);Check(Near(em.GetComponentData<AbilityUnit>(hero).Attack,23),"learning passive twice does not duplicate contribution");
                Auto(true);Step(0.1f);Auto(false);
                Check(runtime.State.AttackSerial==1 && Has(em.GetBuffer<ActiveBuff>(target),4),"real basic hit applies passive poison");
                int attacks=runtime.State.AttackSerial;Step(1.05f);
                Check(runtime.State.AttackSerial==attacks,"periodic damage does not trigger a basic attack loop");
                var state=runtime.State;ref var library=ref state.Library.Value;
                var sample=runtime.CreateUnit(new float3(40,0.55f,40),1,1);
                var unit=em.GetComponentData<AbilityUnit>(sample);var buffs=em.GetBuffer<ActiveBuff>(sample);
                var poison=new EffectRequest{Source=hero,Target=sample,Origin=1001,Power=1,Effect=new EffectData{Kind=EffectKind.ApplyBuff,Buff=4,BuffScale=1}};
                AbilityEffects.Apply(ref unit,buffs,ref library,poison,0);
                Check(Near(unit.Speed,1.6f),"poison uses common attribute modifier");
                AbilityEffects.Tick(ref unit,buffs,ref library,3,0);
                Check(Near(unit.Health,108) && buffs.Length==0 && Near(unit.Speed,2),"periodic final tick and expiration restore speed");
                var burn=poison;burn.Origin=0;burn.Effect.Buff=0;
                for(int i=0;i<5;i++) AbilityEffects.Apply(ref unit,buffs,ref library,burn,0);
                Check(buffs.Length==1 && buffs[0].Stacks==3,"burn respects stack cap");
                float tick=buffs[0].NextTick;AbilityEffects.Apply(ref unit,buffs,ref library,burn,0.5f);
                Check(Near(buffs[0].NextTick,tick),"refresh preserves pending periodic tick");
                burn.Source=sample;AbilityEffects.Apply(ref unit,buffs,ref library,burn,0.5f);
                Check(buffs.Length==2,"different sources keep independent buff instances");
                AbilityEffects.Dispel(ref unit,buffs,ref library);Check(buffs.Length==0,"dispel removes temporary contributions");
                runtime.Dispel(hero);Check(Has(em.GetBuffer<ActiveBuff>(hero),3),"dispel preserves non-dispellable permanent passive");
                var fresh=runtime.CreateUnit(new float3(10,0.55f,0),1,1);
                runtime.Cast(1,new float3(10,0,0));Step(0.01f);
                Check(Has(em.GetBuffer<ActiveBuff>(fresh),1),"persistent area applies slow");
                em.SetComponentData(fresh,LocalTransform.FromPosition(new float3(35,0.55f,0)));Step(0.7f);
                Check(!Has(em.GetBuffer<ActiveBuff>(fresh),1),"leaving area expires short refresh buff");
                runtime.Cast(2,float3.zero);Step(0.01f);
                Check(em.GetComponentData<AbilityUnit>(hero).Speed>8 && !Has(em.GetBuffer<ActiveBuff>(fresh),2),"self circle buffs allies, not enemies");
                var other=runtime.CreateUnit(new float3(0,0.55f,2),0,0);
                em.AddBuffer<AbilitySlot>(other).Add(new AbilitySlot{Definition=0,Level=1});
                runtime.Upgrade(0);
                Check(em.GetBuffer<AbilitySlot>(hero)[0].Level==2 && em.GetBuffer<AbilitySlot>(other)[0].Level==1 && em.GetBuffer<AbilitySlot>(other)[0].ReadyAt==0,"shared skill definition keeps owner level and cooldown independent");
                var p=AbilityGeometry.Resolve(float3.zero,new float3(50,0,0),18,true,out bool valid);
                Check(valid && Near(p.x,18),"out of range default clamps");
                AbilityGeometry.Resolve(float3.zero,new float3(50,0,0),18,false,out valid);Check(!valid,"out of range reject policy");
                em.DestroyEntity(other);Step(0.01f);Check(world.IsCreated,"destroyed target safe");
                runtime.Reset(4);Auto(false);Step(0.01f);
                Check(runtime.State.AliveEnemies==4 && em.GetBuffer<ActiveBuff>(runtime.Hero).Length==1 && em.GetBuffer<ActiveArea>(runtime.Session).Length==0,"reset recreates only own session state");
            }
            Debug.Log("ABILITY_RULES_PASSED");
        }
        private static bool Has(DynamicBuffer<ActiveBuff> buffs,int index) { for(int i=0;i<buffs.Length;i++) if(buffs[i].Definition==index) return true;return false; }
        private static bool Near(float a,float b) => math.abs(a-b)<0.01f;
        private static void Check(bool condition,string message) { if(!condition) throw new Exception("ABILITY_RULE_FAILED "+message);Debug.Log("ABILITY_RULE_PASS "+message); }
    }
}
