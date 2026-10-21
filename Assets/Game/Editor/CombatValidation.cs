using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>通过真实编译库和 ECS 执行入口验证组合合同；主 AI 统一安排运行，不加载 Game 场景。</summary>
    public static partial class CombatValidation
    {
        private static int checks;
        public static void Validate()
        {
            checks=0;
            Check(SkillTargeting.FirstContact(float3.zero,new float3(10,0,0),new float3(5,0,0),1,out float first) && Mathf.Approximately(first,0.4f),"Swept collision uses entry point, not target center");
            Check(SkillTargeting.FirstContact(float3.zero,new float3(10,0,0),new float3(4.8f,0,0.99f),1,out float grazing) && grazing>first,"Grazing target with nearer center does not steal earlier collision");
            ValidateSlotsAndStages(); ValidateSourceAndDelay(); ValidateProjectiles(); ValidateBuffs(); ValidateHitGate(); ValidateInvalidConfiguration();
            ValidateSkillLevels(); ValidateHeroProgression(); ValidateWaves();
            Debug.Log("COMBAT_VALIDATION_PASSED ability foundation checks="+checks);
        }

        private static void ValidateSlotsAndStages()
        {
            using(var assets=new Resources())
            {
                var damage=assets.Damage(10);
                var single=assets.Skill("single",AimMode.Unit,new SkillStage {scope=TargetScope.Single,effects=new EffectDefinition[]{damage}});
                var all=assets.Skill("all",AimMode.Point,new SkillStage {scope=TargetScope.All,repeatCount=3,repeatInterval=0.2f,
                    effects=new EffectDefinition[]{damage}});
                all.manaCost=7; all.cooldown=3;
                using(var f=new Fixture(assets.Catalog(new[]{single,all})))
                {
                    var hero=assets.New<HeroDefinition>(); hero.stableId="hero.validation"; hero.skills=new[]{all,single};
                    var owner=f.Owner(hero.ResolveSkills(f.Catalog));
                    var second=f.Owner(hero.ResolveSkills(f.Catalog));
                    var near=f.Unit(new float3(3,0,0),1); var far=f.Unit(new float3(100,0,0),1);
                    var ally=f.Unit(new float3(3,0,0),0);
                    f.Command(owner,1,new float3(3,0,0),new float3(1,0,0),near); f.Step(owner,0.1f,0.1f);
                    Check(f.State(near).Health==90 && f.State(far).Health==100,"Unit selection uses slot loadout, not QWER identity");
                    Check(f.State(ally).Health==100,"Enemy relation excludes allies");
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.2f,0.1f);
                    Check(f.State(near).Health==80 && f.State(far).Health==90,"All scope reaches outside local geometry");
                    f.Step(owner,0.4f,0.2f); f.Step(owner,0.61f,0.21f);
                    Check(f.State(near).Health==60 && f.State(far).Health==70,"Three stages execute exactly three damage applications");
                    Check(f.State(owner).Mana==93,"Repeated stage pays mana only on cast commit");
                    Check(f.Manager.GetBuffer<ScheduledSkillStage>(owner).Length==0,"Completed repeats release scheduled state");
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.7f,0.09f);
                    Check(f.State(owner).Mana==93 && f.State(far).Health==70,"Cooldown rejection has no partial payment/effects");
                    Check(f.Manager.GetBuffer<AbilitySlot>(second)[0].Charges==1 && f.State(second).Mana==100,
                        "Shared definition leaves second owner's cooldown and resources independent");
                    Check(all.stages[0].repeatCount==3 && all.manaCost==7,"Execution does not mutate source definitions");
                    var badTarget=f.Unit(new float3(2,0,0),1); f.Manager.DestroyEntity(badTarget);
                    f.Command(second,1,new float3(2,0,0),new float3(1,0,0),badTarget); f.Step(second,1,0.1f);
                    Check(f.Manager.GetComponentData<AbilityRuntime>(second).CastSerial==0,"Destroyed unit target is rejected safely");
                }
            }
        }

        private static void ValidateSourceAndDelay()
        {
            using(var assets=new Resources())
            {
                var sourceDamage=assets.Damage(5); sourceDamage.target=EffectTarget.Source;
                var first=assets.Damage(10); var later=assets.Damage(7);
                var skill=assets.Skill("source_and_delay",AimMode.Point,new SkillStage{scope=TargetScope.Area,radius=5,
                    effects=new EffectDefinition[]{first,sourceDamage}});
                skill.stages=new[]{skill.stages[0],new SkillStage{delay=0.5f,scope=TargetScope.All,effects=new EffectDefinition[]{later}}};
                using(var f=new Fixture(assets.Catalog(new[]{skill})))
                {
                    var owner=f.Owner(new[]{0}); var a=f.Unit(new float3(2,0,0),1); var b=f.Unit(new float3(3,0,0),1);
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                    Check(f.State(owner).Health==95 && f.State(a).Health==90 && f.State(b).Health==90,
                        "Source effect runs once per stage rather than once per selected enemy");
                    f.Step(owner,0.4f,0.3f);
                    Check(f.State(a).Health==90,"Second authored stage waits for its delay");
                    f.Step(owner,0.61f,0.21f);
                    Check(f.State(a).Health==83 && f.State(b).Health==83,"Second stage composes its own target query and effect");
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.7f,0.09f);
                    var dead=f.State(owner); dead.Health=0; f.Manager.SetComponentData(owner,dead);
                    f.Step(owner,2,1.3f);
                    Check(f.Manager.GetBuffer<ScheduledSkillStage>(owner).Length==0 && f.State(a).Health==73,
                        "Owner death cancels pending stages without extra damage");
                }
            }
        }

        private static void ValidateProjectiles()
        {
            using(var assets=new Resources())
            {
                var point=assets.Skill("point",AimMode.Point,new SkillStage {delivery=DeliveryKind.PointProjectile,
                    scope=TargetScope.Area,radius=1,speed=5,range=20,effects=new EffectDefinition[]{assets.Damage(18)}});
                var line=assets.Skill("line",AimMode.Direction,new SkillStage {delivery=DeliveryKind.LinearProjectile,
                    scope=TargetScope.Area,radius=3,hitRadius=0.2f,range=20,speed=20,
                    impactEffects=new EffectDefinition[]{assets.Damage(20)},effects=new EffectDefinition[]{assets.Damage(16)}});
                using(var f=new Fixture(assets.Catalog(new[]{point,line})))
                {
                    var owner=f.Owner(new[]{0,1});
                    var atPoint=f.Unit(new float3(5,0,0),1);
                    f.Command(owner,0,new float3(5,0,0),new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                    Check(f.State(atPoint).Health==100 && f.Manager.GetBuffer<SkillFlight>(owner).Length==1,
                        "Point projectile waits for arrival");
                    f.Step(owner,1.1f,1);
                    Check(f.State(atPoint).Health==82 && f.Manager.GetBuffer<SkillFlight>(owner).Length==0,
                        "Point arrival applies exactly one area effect and reclaims flight");
                }
                using(var f=new Fixture(assets.Catalog(new[]{point,line})))
                {
                    var owner=f.Owner(new[]{1});
                    var offPath=f.Unit(new float3(2,0,1),1); var first=f.Unit(new float3(4,0,0),1);
                    var further=f.Unit(new float3(9,0,0),1);
                    f.Command(owner,0,new float3(20,0,0),new float3(1,0,0)); f.Step(owner,0.5f,0.5f);
                    Check(f.State(first).Health==64,"Linear projectile applies first impact plus explosion to the first hit");
                    Check(f.State(offPath).Health==84,"Collision radius is independent from explosion radius");
                    Check(f.State(further).Health==100,"Linear projectile stops at first hit and does not pierce");
                    Check(f.Manager.GetBuffer<SkillFlight>(owner).Length==0,"First impact consumes projectile");
                }
            }
        }

        private static void ValidateBuffs()
        {
            using(var assets=new Resources())
            {
                var stacks=assets.Buff("stacks",2);
                stacks.stacking=BuffStackRule.AddAndRefresh; stacks.maxStacks=2;
                stacks.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.AttackDamage,operation=ModifierOperation.Add,value=5}};
                var slow=assets.Buff("slow",1);
                slow.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.MoveSpeed,operation=ModifierOperation.Percent,value=-0.35f}};
                slow.controls=ControlFlags.AttackBlocked; slow.sourcePolicy=BuffSourcePolicy.BySource;
                var poison=assets.Buff("poison",2); poison.periodicDamage=4; poison.interval=1;
                var damage=assets.Skill("damage",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{assets.Damage(1)}});
                using(var f=new Fixture(assets.Catalog(new[]{damage},new[]{stacks,slow,poison})))
                {
                    var a=f.Owner(new[]{0}); var b=f.Owner(new[]{0}); var target=f.Unit(float3.zero,1);
                    f.ApplyBuff(a,target,0,0); f.ApplyBuff(a,target,0,0.1f); f.ApplyBuff(a,target,0,0.2f);
                    Check(f.Manager.GetBuffer<ActiveBuff>(target).Length==1 && f.State(target).Attack==20,"Stack limit and shared-source merge apply");
                    f.TickBuffs(target,2.3f,0);
                    Check(f.State(target).Attack==10 && f.Manager.GetBuffer<ActiveBuff>(target).Length==0,"Expired stacks restore base attack without subtraction drift");
                    f.ApplyBuff(a,target,1,3); f.ApplyBuff(b,target,1,3.5f);
                    Check(f.Manager.GetBuffer<ActiveBuff>(target).Length==2,"Separate sources retain separate instances");
                    f.TickBuffs(target,4.1f,0);
                    Check(f.State(target).Controls==ControlFlags.AttackBlocked && Mathf.Approximately(f.State(target).Speed,6.5f),
                        "One source expiring preserves another source's control and modifier");
                    f.TickBuffs(target,4.6f,0);
                    Check(f.State(target).Controls==ControlFlags.None && f.State(target).Speed==10,"Final control expiration restores base movement");
                    f.ApplyBuff(a,target,2,5); f.ApplyBuff(a,target,2,5.5f);
                    f.TickBuffs(target,6,0);
                    Check(f.State(target).Health==96,"Refresh retains periodic schedule");
                    f.TickBuffs(target,7.5f,0);
                    Check(f.State(target).Health==92 && f.Manager.GetBuffer<ActiveBuff>(target).Length==0,"Final valid periodic tick and expiration happen once");
                    f.ApplyBuff(a,target,1,8);
                    f.Execution.Apply(a,target,0,new EffectData{Kind=EffectKind.Dispel},ref f.Library.Value,8);
                    Check(f.Manager.GetBuffer<ActiveBuff>(target).Length==0 && f.State(target).Controls==ControlFlags.None,"Dispel uses same contribution cleanup");
                    f.Execution.Apply(a,target,0,new EffectData{Kind=EffectKind.Heal,Amount=100},ref f.Library.Value,8);
                    Check(f.State(target).Health==100,"Heal uses shared health authority and clamps to maximum");
                }
            }
        }

        private static void ValidateHitGate()
        {
            using(var assets=new Resources())
            {
                var focus=assets.Buff("focus",5);
                focus.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.AttackSpeed,operation=ModifierOperation.Percent,value=0.6f}};
                var apply=assets.New<ApplyBuffEffect>(); apply.buff=focus;
                var skill=assets.Skill("focus_skill",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{apply}});
                skill.requiredHits=4; skill.cooldown=7;
                using(var f=new Fixture(assets.Catalog(new[]{skill},new[]{focus})))
                {
                    var owner=f.Owner(new[]{0}); f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                    Check(f.Manager.GetBuffer<ActiveBuff>(owner).Length==0,"Required hit count blocks premature self buff");
                    for(int i=0;i<4;i++) f.Execution.RecordBasicAttackHit(owner);
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.2f,0.1f);
                    Check(Mathf.Approximately(f.State(owner).AttackSpeedMultiplier,1.6f),"Hit-gated self effect grants configured attack speed");
                    Check(f.Manager.GetBuffer<AbilitySlot>(owner)[0].HitStacks==0,"Successful commit consumes hit stacks");
                    f.TickBuffs(owner,5.3f,0);
                    Check(Mathf.Approximately(f.State(owner).AttackSpeedMultiplier,1),"Attack speed returns to base after duration");
                }
            }
        }

        private static void ValidateInvalidConfiguration()
        {
            using(var assets=new Resources())
            {
                var damage=assets.Damage(1);
                var skill=assets.Skill("valid",AimMode.Point,new SkillStage{effects=new EffectDefinition[]{damage}});
                var catalog=assets.Catalog(new[]{skill});
                skill.stages[0].repeatCount=0; Reject(catalog,"Zero repeats rejected"); skill.stages[0].repeatCount=1;
                damage.amount=float.NaN; Reject(catalog,"NaN effect rejected"); damage.amount=1;
                skill.stages[0].shape=(AreaShape)255; Reject(catalog,"Unknown enum rejected"); skill.stages[0].shape=AreaShape.Circle;
                skill.stages[0].scope=TargetScope.Single; Reject(catalog,"Single target without unit aim or linear hit rejected"); skill.stages[0].scope=TargetScope.Area;
                catalog.skills=new[]{skill,skill}; Reject(catalog,"Duplicate stable ID rejected"); catalog.skills=new[]{skill};
                var unknown=assets.New<ApplyBuffEffect>(); unknown.buff=assets.Buff("unregistered",1);
                skill.stages[0].effects=new EffectDefinition[]{unknown}; Reject(catalog,"Unregistered Buff reference rejected");
            }
        }
        private static void Reject(SkillCatalog catalog,string name)
        {
            bool rejected=false;
            try { using(var library=catalog.Compile()) { } }
            catch(InvalidOperationException) { rejected=true; }
            Check(rejected,name);
        }
        private static void Check(bool passed,string message)
        {if(!passed) throw new InvalidOperationException("Ability foundation validation failed: "+message); checks++;}

        private sealed class Resources:IDisposable
        {
            private readonly List<ScriptableObject> owned=new List<ScriptableObject>();
            public T New<T>() where T:ScriptableObject {var value=ScriptableObject.CreateInstance<T>(); owned.Add(value); return value;}
            public DamageEffect Damage(float value) {var effect=New<DamageEffect>(); effect.amount=value; return effect;}
            public BuffDefinition Buff(string id,float duration) {var buff=New<BuffDefinition>(); buff.stableId=id; buff.duration=duration; return buff;}
            public SkillDefinition Skill(string id,AimMode aim,SkillStage stage)
            {var skill=New<SkillDefinition>(); skill.stableId=id; skill.aim=aim; skill.manaCost=0; skill.cooldown=0; skill.castRange=30; skill.stages=new[]{stage}; return skill;}
            public SkillCatalog Catalog(SkillDefinition[] skills,BuffDefinition[] buffs=null)
            {var catalog=New<SkillCatalog>(); catalog.skills=skills; catalog.buffs=buffs??Array.Empty<BuffDefinition>(); catalog.passives=Array.Empty<PassiveDefinition>(); return catalog;}
            public void Dispose() {for(int i=owned.Count-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(owned[i]);}
        }

        private sealed class Fixture:IDisposable
        {
            private readonly World world;
            public readonly EntityManager Manager;
            public readonly SkillCatalog Catalog;
            public BlobAssetReference<AbilityLibrary> Library;
            public readonly AbilityExecution Execution;
            public Fixture(SkillCatalog catalog)
            {
                Catalog=catalog; Library=catalog.Compile(); world=new World("AbilityFoundationValidation");
                Manager=world.EntityManager; Execution=new AbilityExecution(Manager);
            }
            public Entity Unit(float3 position,int team)
            {
                var entity=Manager.CreateEntity();
                Manager.AddComponentData(entity,LocalTransform.FromPosition(position));
                Manager.AddComponentData(entity,new AbilityUnit{Team=team,Health=100,MaxHealth=100,Mana=100,MaxMana=100,
                    BaseSpeed=10,Speed=10,BaseAttack=10,Attack=10,AttackSpeedMultiplier=1});
                Manager.AddBuffer<ActiveBuff>(entity); return entity;
            }
            public Entity Owner(int[] definitions)
            {
                var entity=Unit(float3.zero,0); Manager.AddComponentData(entity,new AbilityRuntime{Library=Library});
                var slots=Manager.AddBuffer<AbilitySlot>(entity);
                foreach(int definition in definitions) slots.Add(new AbilitySlot{Definition=definition,Level=1,Charges=Library.Value.Skills[definition].MaxCharges});
                Manager.AddBuffer<SkillCommand>(entity); Manager.AddBuffer<ScheduledSkillStage>(entity);
                Manager.AddBuffer<SkillFlight>(entity); Manager.AddBuffer<SkillCue>(entity); return entity;
            }
            public AbilityUnit State(Entity entity)=>Manager.GetComponentData<AbilityUnit>(entity);
            public void Command(Entity owner,int slot,float3 point,float3 direction,Entity target=default)
                =>Manager.GetBuffer<SkillCommand>(owner).Add(new SkillCommand{Slot=slot,Point=point,Direction=direction,Target=target});
            public void Step(Entity owner,float now,float dt) {Execution.RebuildIndex(); Execution.Step(owner,now,dt);}
            public void ApplyBuff(Entity source,Entity target,int definition,float now)
                =>Execution.Apply(source,target,0,new EffectData{Kind=EffectKind.ApplyBuff,Buff=definition,BuffScale=1},ref Library.Value,now);
            public void TickBuffs(Entity entity,float now,float dt)
            {
                var unit=State(entity); AbilityEffects.Tick(ref unit,Manager.GetBuffer<ActiveBuff>(entity),ref Library.Value,now,dt);
                Manager.SetComponentData(entity,unit);
            }
            public void Dispose() {Execution.Dispose(); world.Dispose(); if(Library.IsCreated) Library.Dispose();}
        }
    }
}
