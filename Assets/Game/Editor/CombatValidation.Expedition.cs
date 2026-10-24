using System;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        public static void ValidateExpeditionCore()
        {
            checks=0;
            ValidateSlotsAndStages();ValidateSourceAndDelay();ValidateProjectiles();ValidateBuffs();ValidateHitGate();ValidateInvalidConfiguration();
            var rules=AssetDatabase.LoadAssetAtPath<HeroProgressionRules>("Assets/GameData/成长/经验与等级规则.asset");
            var hero=AssetDatabase.LoadAssetAtPath<HeroDefinition>("Assets/GameData/英雄/2000_寒冰射手.asset");
            Check(rules.maxLevel==15 && rules.CumulativeExperience()[14]==20020,"十五级累计经验与定稿一致");
            using(var library=HeroProgressionLogic.Compile(rules,hero))
            {
                var p=HeroProgressionLogic.Create(library,1);HeroProgressionLogic.GainExperience(ref p,20020);
                Check(HeroProgressionLogic.ApplyPendingLevels(ref p)==14 && p.Level==15 && p.SkillPoints==15,"满级恰有十五个技能点");
                Check(StatText.DisplayOrder.Length==11,"属性面板与属性运算只保留十一项");
            }
            var catalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>("Assets/GameData/目录/游戏技能库.asset");
            ValidateMonsters(catalog);ValidateDirector();ValidateExpeditionContent(catalog);
            Check(catalog.skills.Length==8,"两位英雄各四个技能");
            foreach(var definition in catalog.skills)
            {
                Check(definition.forms.Length==2,"每个技能有两个满级形态："+definition.displayName);
                foreach(var form in definition.forms) {SkillRecipeCompiler.Validate(form);Check(true,"形态编译："+form.displayName);}
            }
            using(var f=new Fixture(catalog))
            {
                var owner=f.Owner(new[]{1}); var front=f.Unit(new float3(0,0,4),1);var back=f.Unit(new float3(0,0,8),1);
                Strengthen(f,owner);Strengthen(f,front);Strengthen(f,back);
                f.Command(owner,0,new float3(0,0,22),new float3(0,0,1));f.Step(owner,0,.01f);f.Step(owner,.4f,.4f);
                Check(Mathf.Approximately(f.State(front).Health,9960) && f.State(back).Health==10000,"普通齐射在首个目标处消失，只结算一次");
            }
            var temporary=ScriptableObject.CreateInstance<SkillCatalog>();
            try
            {
                temporary.buffs=catalog.buffs;temporary.skills=new[]{catalog.skills[1].forms[0]};
                using(var f=new Fixture(temporary))
                {
                    var owner=f.Owner(new[]{0});var front=f.Unit(new float3(0,0,4),1);var back=f.Unit(new float3(0,0,8),1);
                    Strengthen(f,owner);Strengthen(f,front);Strengthen(f,back);
                    f.Command(owner,0,new float3(0,0,22),new float3(0,0,1));f.Step(owner,0,.01f);f.Step(owner,.1f,.1f);f.Step(owner,.11f,.01f);f.Step(owner,.4f,.29f);
                    Check(Mathf.Approximately(f.State(front).Health,9960) && Mathf.Approximately(f.State(back).Health,9960),"穿透箭跨帧对前后目标各命中一次");
                }
                temporary.skills=new[]{catalog.skills[4]};
                using(var f=new Fixture(temporary))
                {
                    var owner=f.Owner(new[]{0});var enemy=f.Unit(new float3(0,0,3),1);Strengthen(f,owner);Strengthen(f,enemy);
                    f.Command(owner,0,float3.zero,new float3(0,0,1));f.Step(owner,0,.01f);
                    Check(f.State(enemy).Health==10000,"被动重劈拒绝手动施法");
                    f.Execution.RecordBasicAttackHit(owner,enemy);f.Execution.RecordBasicAttackHit(owner,enemy);f.Step(owner,.1f,.1f);
                    Check(f.State(enemy).Health==10000,"两次主普攻尚不触发");
                    f.Execution.RecordBasicAttackHit(owner,enemy);f.Step(owner,.2f,.1f);
                    Check(Mathf.Approximately(f.State(enemy).Health,9910),"第三次主普攻自动触发共享阶段");
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(temporary);}
            Check(SkillTargeting.Contains(AreaShape.Ring,9,9,180,6,float3.zero,new float3(0,0,1),new float3(8,0,0)) &&
                !SkillTargeting.Contains(AreaShape.Ring,9,9,180,6,float3.zero,new float3(0,0,1),new float3(4,0,0)),"圆环内外边界正确");
            Debug.Log("EXPEDITION_CORE_VALIDATION_PASSED checks="+checks);
        }
        private static void Strengthen(Fixture fixture,Entity entity)
        {
            var unit=fixture.State(entity);unit.Health=unit.MaxHealth=10000;unit.Mana=unit.MaxMana=10000;
            fixture.Manager.SetComponentData(entity,unit);
        }
    }
}
