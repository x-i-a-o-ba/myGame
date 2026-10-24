using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>英雄成长与技能等级的编辑器验证；使用真实编译库与 ECS 执行入口，不加载 Game 场景。</summary>
    public static partial class CombatValidation
    {
        [MenuItem("Tools/Roguelike/验证/战斗与成长（不构建）")]
        public static void ValidateFromMenu()
        {
            Validate();
            EditorUtility.DisplayDialog("验证通过","战斗、技能等级、英雄成长、属性与装备、刷怪导演检查全部通过（共 "+checks+" 项）。详见 Console。","确定");
        }

        private static void ValidateSkillLevels()
        {
            using(var assets=new Resources())
            {
                var damage=assets.Damage(10);
                var nuke=assets.Skill("level_nuke",AimMode.Point,new SkillStage{scope=TargetScope.All,effects=new EffectDefinition[]{damage}});
                nuke.cooldown=10; nuke.manaCost=10;
                var power=assets.Buff("level_power",0);
                power.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.AttackDamage,operation=ModifierOperation.Add,value=10}};
                var apply=assets.New<ApplyBuffEffect>(); apply.buff=power;
                var self=assets.Skill("level_self",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{apply}});
                var ultimate=assets.Skill("level_ultimate",AimMode.Point,new SkillStage{scope=TargetScope.All,effects=new EffectDefinition[]{damage}});
                ultimate.growth.track=SkillLevelTrack.Ultimate;
                using(var f=new Fixture(assets.Catalog(new[]{nuke,self,ultimate},new[]{power})))
                {
                    ref var compiled=ref f.Library.Value.Skills[0];
                    Check(compiled.MaxLevel==4 && compiled.Levels[0].EffectScale==1 && compiled.Levels[0].CooldownScale==1 && compiled.Levels[3].RequiredHeroLevel==7,
                        "Basic growth compiles four Dota levels with level 1 identical to authored values");
                    ref var ult=ref f.Library.Value.Skills[2];
                    Check(ult.MaxLevel==3 && ult.Levels[0].RequiredHeroLevel==6 && ult.Levels[1].RequiredHeroLevel==12 && ult.Levels[2].RequiredHeroLevel==18,
                        "Ultimate growth compiles three levels gated at hero 6/12/18");
                    var owner=f.Owner(new[]{0,1}); var enemy=f.Unit(new float3(3,0,0),1);
                    SetLevel(f,owner,0,0);
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.1f,0.1f);
                    var runtime=f.Manager.GetComponentData<AbilityRuntime>(owner);
                    Check(runtime.CastSerial==0 && runtime.LastFailure==CastFailure.NotLearned && f.State(enemy).Health==100 && f.State(owner).Mana==100,
                        "Unlearned slot rejects cast without payment or effects");
                    SetLevel(f,owner,0,2);
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.2f,0.1f);
                    Check(Mathf.Approximately(f.State(enemy).Health,81),"Level 2 scales damage by authored growth (10 x 1.9)");
                    Check(Mathf.Approximately(f.State(owner).Mana,89),"Level 2 scales mana cost (10 x 1.1)");
                    Check(Mathf.Approximately(f.Manager.GetBuffer<AbilitySlot>(owner)[0].ReadyAt,0.2f+9.2f),"Level 2 shortens cooldown (10 x 0.92)");
                    var unit=f.State(owner); unit.SkillAmplify=0.5f; unit.CooldownReduction=0.5f; f.Manager.SetComponentData(owner,unit);
                    SetLevel(f,owner,0,1);
                    var ownerSlots=f.Manager.GetBuffer<AbilitySlot>(owner);
                    var slot=ownerSlots[0]; slot.ReadyAt=0; slot.Charges=compiled.MaxCharges; ownerSlots[0]=slot;
                    f.Command(owner,0,float3.zero,new float3(1,0,0)); f.Step(owner,0.3f,0.1f);
                    Check(Mathf.Approximately(f.State(enemy).Health,66),"Skill amplify multiplies skill damage (10 x 1.5)");
                    Check(Mathf.Approximately(f.Manager.GetBuffer<AbilitySlot>(owner)[0].ReadyAt,0.3f+5),"Cooldown reduction shortens cooldown (10 x 0.5)");
                    f.Execution.Apply(owner,enemy,-1,new EffectData{Kind=EffectKind.Damage,Amount=10},ref f.Library.Value,0.3f);
                    Check(Mathf.Approximately(f.State(enemy).Health,56),"Basic attack path keeps power 1 and ignores skill amplify");
                    SetLevel(f,owner,1,3);
                    f.Command(owner,1,float3.zero,new float3(1,0,0)); f.Step(owner,0.4f,0.1f);
                    Check(Mathf.Approximately(f.State(owner).Attack,23),"Level 3 scales buff strength (10 + 10 x 1.3)");
                    SetLevel(f,owner,0,0);
                    for(int i=0;i<3;i++) f.Execution.RecordBasicAttackHit(owner);
                    Check(f.Manager.GetBuffer<AbilitySlot>(owner)[0].HitStacks==0,"Unlearned slot does not collect basic attack stacks");
                }
                nuke.growth.track=SkillLevelTrack.Custom;
                nuke.growth.customLevels=new[]{new SkillLevelRow{requiredHeroLevel=3},new SkillLevelRow{requiredHeroLevel=2}};
                Reject(assets.Catalog(new[]{nuke}),"Custom growth with decreasing hero gates rejected");
                nuke.growth.customLevels=new[]{new SkillLevelRow{requiredHeroLevel=1,effectScale=float.NaN}};
                Reject(assets.Catalog(new[]{nuke}),"Custom growth with NaN scale rejected");
                nuke.growth.track=SkillLevelTrack.Basic; nuke.growth.cooldownReductionPerLevel=0.3f; nuke.growth.effectPerLevel=-1;
                Reject(assets.Catalog(new[]{nuke}),"Negative growth rejected");
            }
        }

        private static void ValidateHeroProgression()
        {
            using(var assets=new Resources())
            {
                var rules=assets.New<HeroProgressionRules>();
                var hero=assets.New<HeroDefinition>(); hero.stableId="hero.progression";
                var cumulative=rules.CumulativeExperience();
                Check(cumulative.Length==30 && cumulative[1]==240 && cumulative[5]==2440 && cumulative[29]==63900,"Default experience table matches Dota 2 cumulative values");
                var library=HeroProgressionLogic.Compile(rules,hero);
                try
                {
                    // 英雄属性由 HeroStatsLogic 统一合成：基础 + 成长 ×（等级 - 1）+ 天赋 + 装备，全部直接相加。
                    hero.attackDamage=30; hero.speed=10;
                    var attack=new AutoAttackData{Range=20,AcquisitionRange=25};
                    var stats=HeroStatsLogic.Create(hero.BaseSheet(attack),library.Value.Growth,attack.AcquisitionRange);
                    var progression=HeroProgressionLogic.Create(library,rules.startingSkillPoints);
                    var unit=new AbilityUnit{AttackSpeedMultiplier=1};
                    HeroStatsLogic.Rebuild(ref stats,default,ref progression,ref unit,ref attack);
                    unit.Health=unit.MaxHealth; unit.Mana=unit.MaxMana;
                    Check(unit.MaxHealth==300 && unit.BaseAttack==30 && unit.BaseSpeed==10 && unit.MaxMana==100 && unit.AttackSpeedBonus==0 && unit.ManaRegenBonus==0 &&
                          stats.Total.CriticalDamage==HeroStatsLogic.DefaultCriticalDamage && attack.Range==20 && attack.AcquisitionRange==25,
                        "Level 1 stats equal the authored hero base values");
                    HeroProgressionLogic.GainExperience(ref progression,239);
                    Check(HeroProgressionLogic.ApplyPendingLevels(ref progression)==0 && progression.Level==1,"239 experience stays at level 1");
                    unit.Health=200;
                    HeroProgressionLogic.GainExperience(ref progression,1);
                    Check(HeroProgressionLogic.ApplyPendingLevels(ref progression)==1 && progression.Level==2 && progression.SkillPoints==2 && progression.LevelUpSerial==1,
                        "240 experience reaches level 2 and grants a skill point");
                    HeroStatsLogic.Rebuild(ref stats,default,ref progression,ref unit,ref attack);
                    Check(unit.MaxHealth==322 && unit.Health==222 && unit.BaseAttack==33 && Mathf.Approximately(unit.AttackSpeedBonus,0.025f) && unit.MaxMana==112,
                        "Level growth adds directly to max and current health, mana, attack and attack speed");
                    HeroProgressionLogic.GainExperience(ref progression,float.NaN);
                    HeroProgressionLogic.GainExperience(ref progression,-50);
                    Check(progression.Experience==240,"Invalid or negative experience is ignored");
                    HeroProgressionLogic.GainExperience(ref progression,100000);
                    HeroProgressionLogic.ApplyPendingLevels(ref progression);
                    Check(progression.Level==30 && progression.SkillPoints==30 && HeroProgressionLogic.LevelProgress(progression).y==0,"Experience beyond the table caps at max level");
                    HeroStatsLogic.Rebuild(ref stats,default,ref progression,ref unit,ref attack);
                    Check(unit.MaxHealth==300+22*29 && unit.BaseAttack==30+3*29 && Mathf.Approximately(unit.AttackSpeedBonus,0.725f),"Max level adds 29 levels of growth without conversion");
                    using(var skills=new Resources())
                    {
                        var basic=skills.Skill("learn_basic",AimMode.Point,new SkillStage{effects=new EffectDefinition[]{skills.Damage(1)}});
                        var ult=skills.Skill("learn_ult",AimMode.Point,new SkillStage{effects=new EffectDefinition[]{skills.Damage(1)}});
                        ult.growth.track=SkillLevelTrack.Ultimate;
                        using(var f=new Fixture(skills.Catalog(new[]{basic,ult})))
                        {
                            var owner=f.Owner(new[]{0,1}); SetLevel(f,owner,0,0); SetLevel(f,owner,1,0);
                            var learner=HeroProgressionLogic.Create(library,1); learner.Level=2; learner.SkillPoints=2;
                            var slots=f.Manager.GetBuffer<AbilitySlot>(owner);
                            Check(!HeroProgressionLogic.TryLearn(ref learner,slots,1,ref f.Library.Value) && learner.LastFailure==ProgressionFailure.HeroLevelTooLow,"Ultimate cannot be learned before hero level 6");
                            Check(HeroProgressionLogic.TryLearn(ref learner,slots,0,ref f.Library.Value) && slots[0].Level==1 && learner.SkillPoints==1,"Basic skill learns at hero level 1+");
                            Check(!HeroProgressionLogic.TryLearn(ref learner,slots,0,ref f.Library.Value) && learner.LastFailure==ProgressionFailure.HeroLevelTooLow && slots[0].Level==1,"Basic level 2 waits for hero level 3");
                            learner.Level=7; learner.SkillPoints=10;
                            for(int i=0;i<5;i++) HeroProgressionLogic.TryLearn(ref learner,slots,0,ref f.Library.Value);
                            Check(slots[0].Level==4 && learner.LastFailure==ProgressionFailure.SkillMaxed && learner.SkillPoints==7,"Basic skill caps at level 4 without spending extra points");
                            learner.SkillPoints=0;
                            Check(!HeroProgressionLogic.TryLearn(ref learner,slots,1,ref f.Library.Value) && learner.LastFailure==ProgressionFailure.NoSkillPoints,"Learning requires a skill point");
                            Check(!HeroProgressionLogic.TryLearn(ref learner,slots,5,ref f.Library.Value) && learner.LastFailure==ProgressionFailure.InvalidSlot,"Invalid slot is rejected");
                        }
                    }
                    // 天赋只记录选择掩码，属性在 Rebuild 时按掩码累加；成长为 0 的属性表便于单独观察天赋贡献。
                    var talented=HeroProgressionLogic.Create(library,1); talented.Level=9;
                    var talentStats=HeroStatsLogic.Create(new StatSheet{MaxHealth=100,AttackDamage=10},default,0);
                    var talentUnit=new AbilityUnit{AttackSpeedMultiplier=1}; var talentAttack=new AutoAttackData();
                    HeroStatsLogic.Rebuild(ref talentStats,default,ref talented,ref talentUnit,ref talentAttack); talentUnit.Health=talentUnit.MaxHealth;
                    Check(HeroProgressionLogic.PendingTalent(talented)<0 && !HeroProgressionLogic.TryChooseTalent(ref talented,0),"Talent tier stays locked below level 10");
                    talented.Level=15;
                    Check(HeroProgressionLogic.PendingTalent(talented)==0,"Lowest unlocked talent tier is offered first");
                    Check(HeroProgressionLogic.TryChooseTalent(ref talented,1) && (talented.TalentChosenMask&1)==1 && (talented.TalentRightMask&1)==1,"Choosing a talent records the tier and side");
                    HeroStatsLogic.Rebuild(ref talentStats,default,ref talented,ref talentUnit,ref talentAttack);
                    Check(Mathf.Approximately(talented.ExperienceMultiplier,1.25f) && talentUnit.BaseAttack==10 && talentStats.Talents.ExperienceGain==25,
                        "Right option of tier 10 raises experience gain only");
                    HeroProgressionLogic.GainExperience(ref talented,100);
                    Check(Mathf.Approximately(talented.Experience,125),"Experience gain talent multiplies later experience");
                    Check(HeroProgressionLogic.TryChooseTalent(ref talented,1) && (talented.TalentRightMask&3)==3,"Tier 15 right option is chosen");
                    HeroStatsLogic.Rebuild(ref talentStats,default,ref talented,ref talentUnit,ref talentAttack);
                    Check(talentUnit.MaxHealth==350 && talentUnit.Health==350,"Tier 15 right option raises max and current health");
                    Check(HeroProgressionLogic.PendingTalent(talented)<0 && !HeroProgressionLogic.TryChooseTalent(ref talented,0) && talented.LastFailure==ProgressionFailure.NoPendingTalent,
                        "Each talent tier can be chosen once");
                    talented.Level=20;
                    Check(!HeroProgressionLogic.TryChooseTalent(ref talented,2) && talented.LastFailure==ProgressionFailure.InvalidTalent && (talented.TalentChosenMask&4)==0,
                        "Invalid talent option is rejected without consuming the tier");
                    Check(HeroProgressionLogic.TryChooseTalent(ref talented,1),"Tier 20 right option (cooldown reduction) is chosen");
                    HeroStatsLogic.Rebuild(ref talentStats,new StatSheet{CooldownReduction=50},ref talented,ref talentUnit,ref talentAttack);
                    Check(Mathf.Approximately(talentUnit.CooldownReduction,0.7f) && talentStats.Talents.CooldownReduction==20 && talentStats.Equipment.CooldownReduction==50,
                        "Cooldown reduction from talents and equipment adds directly (20 + 50 = 70%)");
                    HeroStatsLogic.Rebuild(ref talentStats,new StatSheet{CooldownReduction=70},ref talented,ref talentUnit,ref talentAttack);
                    Check(Mathf.Approximately(talentUnit.CooldownReduction,0.8f) && talentStats.Total.CooldownReduction==90 && talentUnit.MaxHealth==350,
                        "Cooldown reduction caps at 80% while the total keeps the raw sum");
                    using(var world=new World("ProgressionRecalculate"))
                    {
                        var entity=world.EntityManager.CreateEntity(); var buffs=world.EntityManager.AddBuffer<ActiveBuff>(entity);
                        var grown=new AbilityUnit{BaseSpeed=10,BaseAttack=30,AttackSpeedBonus=0.25f,BaseRegen=2};
                        using(var empty=assets.Catalog(new[]{assets.Skill("recalculate",AimMode.Self,new SkillStage{scope=TargetScope.Self,effects=new EffectDefinition[]{assets.Damage(1)}})}).Compile())
                            AbilityEffects.Recalculate(ref grown,buffs,ref empty.Value);
                        Check(Mathf.Approximately(grown.AttackSpeedMultiplier,1.25f) && grown.Regen==2 && grown.Attack==30,"Recalculate keeps growth base values for attack speed and regeneration");
                    }
                }
                finally { library.Dispose(); }
                rules.experienceToNextLevel=new[]{240,400};
                RejectProgression(rules,hero,"Experience table shorter than max level rejected");
                rules.experienceToNextLevel=HeroProgressionRules.DotaExperienceTable(); rules.experienceToNextLevel[3]=0;
                RejectProgression(rules,hero,"Non-positive experience step rejected");
                rules.experienceToNextLevel=HeroProgressionRules.DotaExperienceTable();
                hero.talents=new[]{new HeroTalentTier(15,default,default),new HeroTalentTier(10,default,default)};
                RejectProgression(rules,hero,"Talent tiers out of level order rejected");
                hero.talents=new[]{new HeroTalentTier(10,new TalentOption("",(StatKind)99,1),default)};
                RejectProgression(rules,hero,"Unknown talent stat rejected");
                hero.talents=new[]{new HeroTalentTier(10,new TalentOption("",StatKind.CooldownReduction,90),default)};
                RejectProgression(rules,hero,"Cooldown reduction talent above 80 rejected");
                hero.talents=HeroTalentTier.Defaults(); hero.baseStats=new[]{new StatModifier((StatKind)99,1)};
                RejectProgression(rules,hero,"Unknown extra base stat rejected");
                hero.baseStats=new[]{new StatModifier(StatKind.CriticalChance,float.PositiveInfinity)};
                RejectProgression(rules,hero,"Infinite extra base stat rejected");
            }
        }

        private static void SetLevel(Fixture f,Entity owner,int slot,int level)
        {
            var slots=f.Manager.GetBuffer<AbilitySlot>(owner); var value=slots[slot]; value.Level=level; slots[slot]=value;
        }
        private static void RejectProgression(HeroProgressionRules rules,HeroDefinition hero,string name)
        {
            bool rejected=false;
            try { using(var library=HeroProgressionLogic.Compile(rules,hero)) { } }
            catch(InvalidOperationException) { rejected=true; }
            Check(rejected,name);
        }
    }
}
