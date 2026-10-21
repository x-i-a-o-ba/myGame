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
    /// <summary>英雄成长、技能等级与波次难度的编辑器验证；使用真实编译库与 ECS 执行入口，不加载 Game 场景。</summary>
    public static partial class CombatValidation
    {
        [MenuItem("Tools/Roguelike/验证/战斗与成长（不构建）")]
        public static void ValidateFromMenu()
        {
            Validate();
            EditorUtility.DisplayDialog("验证通过","战斗、技能等级、英雄成长与波次难度检查全部通过（共 "+checks+" 项）。详见 Console。","确定");
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
                    var slot=f.Manager.GetBuffer<AbilitySlot>(owner)[0]; slot.ReadyAt=0; f.Manager.GetBuffer<AbilitySlot>(owner)[0]=slot;
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
                    var progression=HeroProgressionLogic.Create(library,rules.startingSkillPoints);
                    var unit=new AbilityUnit{Health=300,MaxHealth=300,Mana=100,MaxMana=100,BaseAttack=30,BaseSpeed=10,AttackSpeedMultiplier=1};
                    HeroProgressionLogic.GainExperience(ref progression,239);
                    Check(HeroProgressionLogic.ApplyPendingLevels(ref progression,ref unit)==0 && progression.Level==1,"239 experience stays at level 1");
                    unit.Health=200;
                    HeroProgressionLogic.GainExperience(ref progression,1);
                    Check(HeroProgressionLogic.ApplyPendingLevels(ref progression,ref unit)==1 && progression.Level==2 && progression.SkillPoints==2,"240 experience reaches level 2 and grants a skill point");
                    Check(unit.MaxHealth==322 && unit.Health==222 && unit.BaseAttack==33 && Mathf.Approximately(unit.AttackSpeedBonus,0.025f),"Level growth adds to max and current health, attack and attack speed");
                    HeroProgressionLogic.GainExperience(ref progression,float.NaN);
                    HeroProgressionLogic.GainExperience(ref progression,-50);
                    Check(progression.Experience==240,"Invalid or negative experience is ignored");
                    HeroProgressionLogic.GainExperience(ref progression,100000);
                    HeroProgressionLogic.ApplyPendingLevels(ref progression,ref unit);
                    Check(progression.Level==30 && progression.SkillPoints==30 && HeroProgressionLogic.LevelProgress(progression).y==0,"Experience beyond the table caps at max level");
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
                    var talented=HeroProgressionLogic.Create(library,1); talented.Level=9;
                    var stats=new AbilityUnit{Health=100,MaxHealth=100,BaseAttack=10,AttackSpeedMultiplier=1};
                    Check(HeroProgressionLogic.PendingTalent(talented)<0 && !HeroProgressionLogic.TryChooseTalent(ref talented,ref stats,0),"Talent tier stays locked below level 10");
                    talented.Level=15;
                    Check(HeroProgressionLogic.PendingTalent(talented)==0,"Lowest unlocked talent tier is offered first");
                    Check(HeroProgressionLogic.TryChooseTalent(ref talented,ref stats,1) && Mathf.Approximately(talented.ExperienceMultiplier,1.25f) && stats.BaseAttack==10,
                        "Right option of tier 10 raises experience gain only");
                    HeroProgressionLogic.GainExperience(ref talented,100);
                    Check(Mathf.Approximately(talented.Experience,125),"Experience gain talent multiplies later experience");
                    Check(HeroProgressionLogic.TryChooseTalent(ref talented,ref stats,1) && stats.MaxHealth==350 && stats.Health==350 && (talented.TalentRightMask&3)==3,
                        "Tier 15 right option raises max and current health");
                    Check(HeroProgressionLogic.PendingTalent(talented)<0 && !HeroProgressionLogic.TryChooseTalent(ref talented,ref stats,0) && talented.LastFailure==ProgressionFailure.NoPendingTalent,
                        "Each talent tier can be chosen once");
                    var cdr=new AbilityUnit();
                    HeroProgressionLogic.ApplyTalent(ref talented,ref cdr,new TalentData{Stat=TalentStat.CooldownReduction,Value=0.2f});
                    HeroProgressionLogic.ApplyTalent(ref talented,ref cdr,new TalentData{Stat=TalentStat.CooldownReduction,Value=0.5f});
                    Check(Mathf.Approximately(cdr.CooldownReduction,0.6f),"Cooldown reduction talents stack multiplicatively");
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
                hero.talents=new[]{new HeroTalentTier(10,new TalentOption("",(TalentStat)99,1),default)};
                RejectProgression(rules,hero,"Unknown talent stat rejected");
            }
        }

        private static void ValidateWaves()
        {
            using(var assets=new Resources())
            {
                var difficulty=assets.New<CombatDifficultyDefinition>();
                var inspector=UnityEditor.Editor.CreateEditor(difficulty);
                try { Check(inspector is CombatDifficultyChineseInspector,"Difficulty curve uses the Chinese inspector"); }
                finally { UnityEngine.Object.DestroyImmediate(inspector); }
                var waves=difficulty.ToSettings();
                var combat=new CombatSettings{EnemyHealth=30,EnemySpeed=2,EnemyAttackDamage=4,EnemyAttackInterval=1,ArenaHalfSize=125,SafeSpawnRadius=30,MaxAlive=3000};
                var first=CombatWaveDirector.Stats(waves,combat,1,EnemyTier.Normal);
                var eleventh=CombatWaveDirector.Stats(waves,combat,11,EnemyTier.Normal);
                Check(first.Count==80 && first.Health==30 && first.Damage==4 && first.Speed==2 && first.Experience==3,"Wave 1 uses scene base enemy values");
                Check(eleventh.Count==200 && Mathf.Approximately(eleventh.Health,30*Mathf.Pow(1.07f,10)) && eleventh.Speed>first.Speed && eleventh.Experience>first.Experience,
                    "Later waves grow count, health, speed and experience");
                Check(CombatWaveDirector.IsEliteWave(waves,5) && !CombatWaveDirector.IsEliteWave(waves,6) && CombatWaveDirector.IsBossWave(waves,10) &&
                    CombatWaveDirector.IsBossWave(waves,30) && !CombatWaveDirector.IsBossWave(waves,29),"Elites every 5 waves, bosses every 10 waves and on the final wave");
                var boss=CombatWaveDirector.Stats(waves,combat,10,EnemyTier.Boss);
                Check(boss.Count==1 && Mathf.Approximately(boss.Health,eleventh.Health/1.07f*60) && boss.Scale>first.Scale,"Boss multiplies same-wave health and size");
                using(var world=new World("WaveValidation"))
                {
                    var manager=world.EntityManager;
                    var prefab=manager.CreateEntity(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform),typeof(Prefab));
                    manager.AddBuffer<ActiveBuff>(prefab);
                    combat.EnemyPrefab=prefab; combat.MaxAlive=5;
                    var state=new CombatWaveState{Wave=30,Seed=7};
                    int spawned=CombatWaveDirector.SpawnWave(manager,combat,waves,ref state,float3.zero,0);
                    using(var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform)))
                    using(var enemies=query.ToEntityArray(Allocator.Temp))
                    {
                        bool bossFound=false,inRing=true;
                        foreach(var enemy in enemies)
                        {
                            var data=manager.GetComponentData<CombatEnemy>(enemy);
                            bossFound|=data.Tier==EnemyTier.Boss && data.Wave==30;
                            float distance=math.length(manager.GetComponentData<LocalTransform>(enemy).Position.xz);
                            inRing&=distance>=29.9f && distance<=55.1f;
                        }
                        Check(spawned==5 && enemies.Length==5 && bossFound && state.FinalBossSpawned,"Final wave always spawns the boss and respects the alive cap");
                        Check(inRing,"Enemies spawn in the safe ring around the player");
                        Check(state.Skipped>0 && state.SpawnedTotal==5,"Capped spawns are counted as skipped");
                    }
                }
                difficulty.healthGrowth=0.9f;
                bool rejected=false;
                try { difficulty.ToSettings(); } catch(InvalidOperationException) { rejected=true; }
                Check(rejected,"Difficulty curve that weakens enemies over time is rejected");
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
