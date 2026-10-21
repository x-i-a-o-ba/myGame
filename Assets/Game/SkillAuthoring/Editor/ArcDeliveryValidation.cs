using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>由主 AI 在隔离 Editor 显式调用：真实 World/AbilityExecution 的抛物线投递验证，不启动游戏。</summary>
    public static class ArcDeliveryValidation
    {
        private static int checks;
        public static void Run()
        {
            checks=0;
            using(var run=new Fixture(DeliveryKind.ParabolicProjectile))
            {
                run.Cast(); run.Step(0,0);
                Check(run.Flights==1,"创建真实飞行缓冲"); Near(run.Position.y,1,"发射高度");
                run.Step(0.5f,0.5f);
                Near(run.Position.y,3.5f,"执行中点高度含起终点插值和附加高度");
                Near(run.Position.z,4,"执行中点水平位置"); Near(run.Health,100,"抵达前不伤害");
                Near(run.PassingHealth,100,"沿途目标不碰撞");
                ref var definition=ref run.Library.Value.Skills[0];
                var snapshot=SkillAimPreview.From(ref definition); var points=new List<Vector3>();
                SkillTrajectory.Sample(snapshot,Vector3.zero,new Vector3(0,0,8),points,32);
                Near(points[16].y,run.Position.y,"编辑预览与执行中点一致");
                run.Step(1,0.5f); Near(run.Health,90,"落点使用共享伤害结算");
                Check(run.Flights==0 && run.Cues==1,"落点移除且只发布一次结果");
                run.Step(5,4); Near(run.Health,90,"后续帧不重复伤害");
            }
            using(var run=new Fixture(DeliveryKind.ParabolicProjectile,s=>{s.launchHeight=2;s.arrivalHeight=2;},float3.zero))
            {
                run.Cast();run.Step(0,0);Check(run.Flights==1,"零水平距离仍飞行");
                run.Step(0.5f,0.5f);Near(run.Position.y,5,"垂直原地弧线中点");Near(run.Health,100,"原地投递提前不结算");
                run.Step(1,0.5f);Near(run.Health,90,"原地投递按时抵达");
            }
            using(var run=new Fixture(DeliveryKind.ParabolicProjectile,s=>{s.delay=0.5f;s.repeatCount=3;s.repeatInterval=0.25f;}))
            {
                run.Cast();run.Step(0,0);Check(run.Flights==0,"延迟前不发射");
                run.Step(0.5f,0.5f);Near(run.Position.y,1,"延迟到期从发射点起飞");
                run.Step(3,2.5f);Near(run.Health,70,"大 dt 补发三次各自抵达");
                Check(run.Flights==0 && run.Cues==3,"重复阶段各结算一次并清理");
            }
            using(var run=new Fixture(DeliveryKind.ParabolicProjectile,s=>{s.repeatCount=2;s.repeatInterval=2;}))
            {
                run.Cast();run.Step(0,0);run.KillCaster();run.Step(0.5f,0.5f);
                Check(run.Flights==0 && run.Scheduled==0,"施法者死亡清理飞行与未发阶段");
                run.Step(5,4.5f);Near(run.Health,100,"死亡取消的弹体不结算");
            }
            // 新会话有全新缓冲；旧会话由 Fixture 按 execution -> World -> Blob 顺序释放。
            using(var run=new Fixture(DeliveryKind.ParabolicProjectile))
                Check(run.Flights==0 && run.Scheduled==0 && run.Health==100,"重建 World 不继承上局飞行");
            foreach(var delivery in new[]{DeliveryKind.Instant,DeliveryKind.PointProjectile,DeliveryKind.LinearProjectile})
            {
                using(var run=new Fixture(delivery))
                {
                    run.Cast();run.Step(0,0);run.Step(1,1);
                    Near(run.Health,90,"旧投递回归："+delivery);Check(run.Flights==0,"旧投递完成清理："+delivery);
                }
            }
            Reject(s=>s.flightSeconds=0,"零飞行时间"); Reject(s=>s.flightSeconds=float.NaN,"非有限飞行时间");
            Reject(s=>s.arcHeight=-1,"负弧高"); Reject(s=>s.launchHeight=float.PositiveInfinity,"非有限发射高度");
            Reject(s=>s.arrivalHeight=-1,"负落点高度");
            Debug.Log("ARC_DELIVERY_VALIDATION_PASSED checks="+checks);
        }

        private static void Reject(Action<SkillStage> configure,string label)
        {
            bool rejected=false;
            try { using(var run=new Fixture(DeliveryKind.ParabolicProjectile,configure)) { } }
            catch(InvalidOperationException) { rejected=true; }
            Check(rejected,"编译拒绝："+label);
        }
        private static void Check(bool condition,string label)
        { if(!condition) throw new InvalidOperationException("抛物线投递验证失败："+label); checks++; }
        private static void Near(float actual,float expected,string label)
            => Check(math.abs(actual-expected)<0.001f,label+"，实际 "+actual+"，预期 "+expected);

        private sealed class Fixture : IDisposable
        {
            private readonly DamageEffect damage;
            private readonly SkillDefinition skill;
            private readonly SkillCatalog catalog;
            private World world;
            private AbilityExecution execution;
            private Entity owner,target,passing;
            private readonly float3 targetPoint;
            public BlobAssetReference<AbilityLibrary> Library;
            private EntityManager Manager=>world.EntityManager;
            public int Flights=>Manager.GetBuffer<SkillFlight>(owner).Length;
            public int Scheduled=>Manager.GetBuffer<ScheduledSkillStage>(owner).Length;
            public int Cues=>Manager.GetBuffer<SkillCue>(owner).Length;
            public float3 Position=>Manager.GetBuffer<SkillFlight>(owner)[0].Position;
            public float Health=>Manager.GetComponentData<AbilityUnit>(target).Health;
            public float PassingHealth=>Manager.GetComponentData<AbilityUnit>(passing).Health;

            public Fixture(DeliveryKind delivery,Action<SkillStage> configure=null,float3? point=null)
            {
                targetPoint=point??new float3(0,0,8);
                damage=ScriptableObject.CreateInstance<DamageEffect>();damage.amount=10;
                skill=ScriptableObject.CreateInstance<SkillDefinition>();
                catalog=ScriptableObject.CreateInstance<SkillCatalog>();
                try
                {
                    var stage=new SkillStage{delivery=delivery,radius=1.1f,range=8,speed=8,hitRadius=0.25f,
                        effects=new EffectDefinition[]{damage},arcHeight=3,flightSeconds=1,launchHeight=1,arrivalHeight=0};
                    configure?.Invoke(stage);
                    skill.stableId="ability.arc_validation";skill.castRange=20;skill.cooldown=0;skill.manaCost=0;
                    skill.stages=new[]{stage};catalog.skills=new[]{skill};Library=catalog.Compile();
                    world=new World("三维投递隔离验证");
                    owner=Unit(float3.zero,0);target=Unit(targetPoint,1);
                    // 直线回归不放置中途敌人；弧线则明确验证沿途不发生碰撞。
                    passing=Unit(delivery==DeliveryKind.ParabolicProjectile?new float3(0,0,4):new float3(50,0,4),1);
                    Manager.AddComponentData(owner,new AbilityRuntime{Library=Library});
                    Manager.AddBuffer<SkillCommand>(owner);Manager.AddBuffer<ScheduledSkillStage>(owner);
                    Manager.AddBuffer<SkillFlight>(owner);Manager.AddBuffer<SkillCue>(owner);
                    Manager.AddBuffer<AbilitySlot>(owner).Add(new AbilitySlot{Definition=0,Level=1,Charges=1});
                    execution=new AbilityExecution(Manager);execution.RebuildIndex();
                }
                catch { Dispose(); throw; }
            }
            private Entity Unit(float3 position,int team)
            {
                var entity=Manager.CreateEntity(typeof(AbilityUnit),typeof(LocalTransform));
                Manager.SetComponentData(entity,new AbilityUnit{Health=100,MaxHealth=100,Mana=100,MaxMana=100,Team=team,AttackSpeedMultiplier=1});
                Manager.SetComponentData(entity,LocalTransform.FromPosition(position));Manager.AddBuffer<ActiveBuff>(entity);
                return entity;
            }
            public void Cast()=>Manager.GetBuffer<SkillCommand>(owner).Add(new SkillCommand{Slot=0,Point=targetPoint,Direction=new float3(0,0,1),Target=target});
            public void Step(float now,float dt)=>execution.Step(owner,now,dt);
            public void KillCaster(){var unit=Manager.GetComponentData<AbilityUnit>(owner);unit.Health=0;Manager.SetComponentData(owner,unit);}
            public void Dispose()
            {
                execution?.Dispose();execution=null;
                if(world!=null && world.IsCreated) world.Dispose();world=null;
                if(Library.IsCreated) Library.Dispose();
                if(catalog) UnityEngine.Object.DestroyImmediate(catalog);
                if(skill) UnityEngine.Object.DestroyImmediate(skill);
                if(damage) UnityEngine.Object.DestroyImmediate(damage);
            }
        }
    }
}
