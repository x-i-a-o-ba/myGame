using System;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>主任务在隔离 Editor 运行；使用真实 World/编译库/执行器检查动作时间，不运行游戏。</summary>
    public static class CastTimingValidation
    {
        private static int checks;
        public static void Run()
        {
            checks=0;
            using(var f=new Fixture())
            {
                f.Cast();f.Step(0);Near(f.Health,90,"零前摇立即生效");
                Check(!AbilityCastTiming.IsBusy(f.Runtime,0),"零后摇不锁动作");
                f.Cast();f.Step(0.1f);Near(f.Health,80,"零时长保持下一帧可施法");
            }
            using(var f=new Fixture(1,2))
            {
                f.Cast();f.Cast();f.Step(0);
                Check(f.Runtime.CastSerial==1 && f.Runtime.LastFailure==CastFailure.Busy,"同帧只接受一次非零时长施法");
                Near(f.Mana,90,"提交只扣一次费用");Near(f.Health,100,"开始前摇不伤害");
                Check(AbilityCastTiming.Phase(f.Runtime,0)==AbilityCastPhase.Windup,"查询前摇");
                f.Step(0.99f);Near(f.Health,100,"释放前不伤害");
                f.Step(1);Near(f.Health,90,"准确释放边界结算");
                Check(AbilityCastTiming.Phase(f.Runtime,1)==AbilityCastPhase.Recovery,"释放边界进入后摇");
                f.Cast();f.Step(2.99f);Check(f.Runtime.CastSerial==1,"后摇不能再次施法");
                Near(f.Mana,90,"拒绝命令不扣资源");
                Check(!AbilityCastTiming.IsBusy(f.Runtime,3),"后摇边界解除");
                f.Cast();f.Step(3);Check(f.Runtime.CastSerial==2,"后摇结束可再次施法");
                Near(f.Mana,80,"新施法恰好扣一次");
            }
            using(var f=new Fixture(1,0,s=>{s.delay=0.5f;s.repeatCount=3;s.repeatInterval=0.25f;}))
            {
                f.Cast();f.Step(0);f.Step(1.49f);Near(f.Health,100,"阶段延迟从释放点算");
                f.Step(1.5f);Near(f.Health,90,"前摇加阶段延迟到期");
                f.Step(5);Near(f.Health,70,"大步长补发所有剩余重复且无额外费用");Near(f.Mana,90,"多阶段只支付一次");
            }
            using(var f=new Fixture(1,2))
            {
                f.Cast();f.Step(0);f.Controls(ControlFlags.CastBlocked);f.Step(0.5f);
                Check(f.Pending==0 && !AbilityCastTiming.IsBusy(f.Runtime,0.5f),"眩晕取消前摇及动作锁");
                Check(f.Runtime.LastFailure==CastFailure.Interrupted,"返回中断状态");
                f.Controls(ControlFlags.None);f.Step(5);Near(f.Health,100,"被打断技能不迟发");Near(f.Mana,90,"打断不退款");
            }
            using(var f=new Fixture(1,2))
            {
                f.Cast();f.Step(0);f.Controls(ControlFlags.CastBlocked);f.Step(2);
                Near(f.Health,100,"跨释放点时既有控制优先于待释放动作");
            }
            using(var f=new Fixture(1,2,s=>s.delay=1))
            {
                f.Cast();f.Step(0);f.Step(1);f.Controls(ControlFlags.CastBlocked);f.Step(2);
                Near(f.Health,90,"后摇中控制不取消已释放延迟阶段");
            }
            using(var f=new Fixture(1,0,s=>s.delay=1))
            {
                f.Cast();f.Step(0);f.Cast();f.Step(1);
                Check(f.Runtime.CastSerial==2 && f.Pending==2,"旧技能释放边界接受新施法且保留两次调度");
                f.Controls(ControlFlags.CastBlocked);f.Step(1.5f);
                Check(f.Pending==1,"中断仅删除当前前摇，不删除旧已释放阶段");
                f.Step(2);Near(f.Health,90,"旧施法延迟阶段不受新施法序号影响");
            }
            using(var f=new Fixture(1,2))
            {
                f.Cast();f.Step(0);f.Kill();f.Step(0.5f);
                Check(f.Pending==0 && f.Flights==0 && !AbilityCastTiming.IsBusy(f.Runtime,0.5f),"死亡清空动作与调度");
                f.Step(5);Near(f.Health,100,"死亡不迟发");
            }
            using(var f=new Fixture(1,2,configureSkill:s=>s.cooldown=5))
            {
                f.Cast();f.Step(0);Near(f.ReadyAt,5,"冷却从提交算");
                f.Controls(ControlFlags.CastBlocked);f.Step(0.5f);Near(f.ReadyAt,5,"中断不退款冷却");
            }
            foreach(var delivery in new[]{DeliveryKind.PointProjectile,DeliveryKind.LinearProjectile,DeliveryKind.ParabolicProjectile})
            using(var f=new Fixture(1,0,s=>{s.delivery=delivery;s.speed=8;s.flightSeconds=1;}))
            {
                f.Cast();f.Step(0);f.Step(1.5f);
                Check(f.Flights==1,"跨释放点仍只飞行半秒："+delivery);
                Near(f.Position.z,4,"三种投递共用释放后的飞行时钟："+delivery);
                Near(f.Health,100,"半程不提前伤害："+delivery);
                f.Step(2);Near(f.Health,90,"释放后一秒命中："+delivery);
            }
            using(var f=new Fixture(0,2))
            {
                f.Cast();f.Step(0);Near(f.Health,90,"仅后摇仍立即释放");
                Check(AbilityCastTiming.Phase(f.Runtime,0)==AbilityCastPhase.Recovery,"仅后摇状态");
            }
            using(var f=new Fixture())Check(f.Pending==0 && !AbilityCastTiming.IsBusy(f.Runtime,0),"新会话无遗留动作锁");
            Reject(s=>s.castTime=-1,"负前摇");Reject(s=>s.castTime=float.NaN,"前摇 NaN");
            Reject(s=>s.recoveryTime=-1,"负后摇");Reject(s=>s.recoveryTime=float.PositiveInfinity,"后摇无穷");
            Reject(s=>{s.castTime=float.MaxValue;s.recoveryTime=float.MaxValue;},"总时长溢出");
            Reject(s=>{s.castTime=1;s.stages=Array.Empty<SkillStage>();},"旧调度拒绝不支持的动作时间");
            ArcDeliveryValidation.Run();
            Debug.Log("CAST_TIMING_VALIDATION_PASSED checks="+checks);
        }
        private static void Reject(Action<SkillDefinition> configure,string label)
        {
            bool rejected=false;
            try{using(var f=new Fixture(configureSkill:configure)) { }}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"编译拒绝："+label);
        }
        private static void Check(bool condition,string label)
        {if(!condition)throw new InvalidOperationException("前后摇验证失败："+label);checks++;}
        private static void Near(float actual,float expected,string label)=>Check(math.abs(actual-expected)<0.001f,label+"；实际 "+actual+"，预期 "+expected);
        private sealed class Fixture:IDisposable
        {
            private DamageEffect damage;
            private SkillDefinition skill;
            private SkillCatalog catalog;
            private BlobAssetReference<AbilityLibrary> library;
            private World world;
            private AbilityExecution execution;
            private Entity owner,target;
            private float time;
            private EntityManager Manager=>world.EntityManager;
            public AbilityRuntime Runtime=>Manager.GetComponentData<AbilityRuntime>(owner);
            public float Health=>Manager.GetComponentData<AbilityUnit>(target).Health;
            public float Mana=>Manager.GetComponentData<AbilityUnit>(owner).Mana;
            public float ReadyAt=>Manager.GetBuffer<AbilitySlot>(owner)[0].ReadyAt;
            public int Pending=>Manager.GetBuffer<ScheduledSkillStage>(owner).Length;
            public int Flights=>Manager.GetBuffer<SkillFlight>(owner).Length;
            public float3 Position=>Manager.GetBuffer<SkillFlight>(owner)[0].Position;
            public Fixture(float cast=0,float recovery=0,Action<SkillStage> configure=null,Action<SkillDefinition> configureSkill=null)
            {
                try
                {
                    damage=ScriptableObject.CreateInstance<DamageEffect>();damage.amount=10;
                    skill=ScriptableObject.CreateInstance<SkillDefinition>();catalog=ScriptableObject.CreateInstance<SkillCatalog>();
                    var stage=new SkillStage{radius=0.1f,range=8,hitRadius=0.01f,effects=new EffectDefinition[]{damage}};
                    configure?.Invoke(stage);
                    skill.stableId="ability.cast_timing_validation";skill.castTime=cast;skill.recoveryTime=recovery;
                    skill.castRange=20;skill.cooldown=0;skill.manaCost=10;skill.stages=new[]{stage};
                    configureSkill?.Invoke(skill);catalog.skills=new[]{skill};library=catalog.Compile();
                    world=new World("前后摇隔离验证");owner=Unit(float3.zero,0);target=Unit(new float3(0,0,8),1);
                    Manager.AddComponentData(owner,new AbilityRuntime{Library=library});
                    Manager.AddBuffer<SkillCommand>(owner);Manager.AddBuffer<ScheduledSkillStage>(owner);
                    Manager.AddBuffer<SkillFlight>(owner);Manager.AddBuffer<SkillCue>(owner);
                    Manager.AddBuffer<AbilitySlot>(owner).Add(new AbilitySlot{Definition=0,Level=1,Charges=1});
                    execution=new AbilityExecution(Manager);execution.RebuildIndex();
                }
                catch{Dispose();throw;}
            }
            private Entity Unit(float3 point,int team)
            {
                var e=Manager.CreateEntity(typeof(AbilityUnit),typeof(LocalTransform));
                Manager.SetComponentData(e,new AbilityUnit{Health=100,MaxHealth=100,Mana=100,MaxMana=100,Team=team});
                Manager.SetComponentData(e,LocalTransform.FromPosition(point));Manager.AddBuffer<ActiveBuff>(e);return e;
            }
            public void Cast()=>Manager.GetBuffer<SkillCommand>(owner).Add(new SkillCommand{Slot=0,Point=new float3(0,0,8),Direction=new float3(0,0,1),Target=target});
            public void Step(float now){execution.Step(owner,now,now-time);time=now;}
            public void Controls(ControlFlags controls){var u=Manager.GetComponentData<AbilityUnit>(owner);u.Controls=controls;Manager.SetComponentData(owner,u);}
            public void Kill(){var u=Manager.GetComponentData<AbilityUnit>(owner);u.Health=0;Manager.SetComponentData(owner,u);}
            public void Dispose()
            {
                execution?.Dispose();execution=null;
                if(world!=null && world.IsCreated)world.Dispose();world=null;
                if(library.IsCreated)library.Dispose();
                if(catalog)UnityEngine.Object.DestroyImmediate(catalog);
                if(skill)UnityEngine.Object.DestroyImmediate(skill);
                if(damage)UnityEngine.Object.DestroyImmediate(damage);
            }
        }
    }
}
