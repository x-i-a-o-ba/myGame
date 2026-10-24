using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    public static partial class ExpeditionContent
    {
        static MonsterAbilityDefinition Monster(string id,string name,MonsterAbilityTemplate template,Action<MonsterAbilityDefinition> setup)
        {
            var m=Asset<MonsterAbilityDefinition>("怪物技能/"+name+".asset");
            // 新实例默认值完整复制，确保重复执行迁移不会保留废弃字段。
            var defaults=ScriptableObject.CreateInstance<MonsterAbilityDefinition>();EditorUtility.CopySerialized(defaults,m);UnityEngine.Object.DestroyImmediate(defaults);
            m.stableId="monster."+id;m.displayName=name;m.template=template;setup(m);return m;
        }
        [MenuItem("Tools/Roguelike/远征/迁移怪物技能")]
        public static void Monsters()
        {
            modules=Asset<SkillModuleCatalog>("目录/组合模块目录.asset");buffs.Clear();effects.Clear();
            int slow=Apply(4069,"怪物震地迟缓",Buff(5063,"震地迟缓",2,b=>b.moveSpeedPercent=-.4f));
            int ice=Apply(4070,"怪物冰环冻结",Buff(5064,"冰环冻结",1,b=>b.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked));
            int element=Apply(4071,"精英寒霜命中",Buff(5065,"精英寒霜",2,b=>b.moveSpeedPercent=-.3f));
            ApplyBuffEffect Effect(int id)=>(ApplyBuffEffect)effects.First(x=>x.configId==id);
            var prefabs=new Dictionary<int,EnemyArchetype>();
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/GameData/怪物"}))
            {var go=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var a=go?go.GetComponent<EnemyArchetype>():null;if(a) prefabs[a.configId]=a;}
            EnemyArchetype Enemy(int id)=>prefabs[id];
            void Set(int id,params MonsterAbilityDefinition[] skills)
            {
                var a=Enemy(id);a.abilities=skills;a.elementalSlow=Effect(element);a.fireElement=id>=3009;
                a.armoredAboveHalf=id==3014?.5f:0;
                int role=(id-3001)%8;
                if(role<6)
                {
                    a.healthMultiplier=new[]{1f,.7f,.8f,2,.6f,5}[role];a.damageMultiplier=new[]{1f,.8f,1,1,1,1.5f}[role];
                    a.speedMultiplier=new[]{1f,1.4f,.9f,1,1.2f,.7f}[role];a.experienceMultiplier=new[]{1f,1,1.5f,2,1,4}[role];
                    a.goldMultiplier=new[]{1f,1,2,2,1,4}[role];
                }
                a.attack.baseAttackTime=1;a.attack.attackPoint=.25f;a.attack.backswing=.2f;
                a.attack.range=a.behavior==EnemyBehavior.Ranged?10:1.4f;a.attack.acquisitionRange=a.attack.range;
                EditorUtility.SetDirty(a.attack);
                if(a.behavior==EnemyBehavior.Charger || a.behavior==EnemyBehavior.Exploder) a.behavior=EnemyBehavior.Melee;
                EditorUtility.SetDirty(a);PrefabUtility.SavePrefabAsset(a.gameObject);
            }
            MonsterAbilityDefinition Summon(string id,string name,int count,EnemyArchetype target,float cd,int stage=1)
                =>Monster(id,name,MonsterAbilityTemplate.Summon,m=>{m.summon=target;m.summonCount=count;m.cooldown=cd;m.warningSeconds=1;m.radius=3;m.triggerRange=35;m.minimumStage=stage;});
            Set(3001);
            Set(3002,Monster("pounce","猎犬扑咬",MonsterAbilityTemplate.Dash,m=>{m.triggerRange=4;m.shape=AreaShape.Line;m.radius=.8f;m.range=4;m.warningSeconds=.4f;m.cooldown=6;m.speed=14;m.damageRatio=1.5f;}));
            Set(3003,Monster("volley","骷髅齐射",MonsterAbilityTemplate.ProjectileBarrage,m=>{m.shape=AreaShape.Cone;m.halfAngle=22;m.radius=.65f;m.range=18;m.warningSeconds=.5f;m.cooldown=5;m.projectileCount=3;m.spreadDegrees=44;m.speed=12;}));
            Set(3004,Monster("charge","蛮牛冲锋",MonsterAbilityTemplate.Dash,m=>{m.shape=AreaShape.Line;m.radius=1.1f;m.range=12;m.warningSeconds=.8f;m.cooldown=8;m.knockback=3;m.speed=16;m.damageRatio=2.5f;}));
            Set(3005,Monster("poison","毒囊自爆",MonsterAbilityTemplate.TelegraphArea,m=>{m.radius=2.5f;m.triggerRange=2.5f;m.warningSeconds=1;m.sacrifice=true;m.damageRatio=4;m.trailSeconds=3;m.trailDamageRatio=.5f;m.color=new Color(.6f,1,.15f,.7f);}));
            Set(3006,Monster("quake","岩石震地",MonsterAbilityTemplate.TelegraphArea,m=>{m.radius=m.range=3;m.triggerRange=4;m.warningSeconds=1;m.cooldown=8;m.damageRatio=2;m.status=Effect(slow);}));
            Set(3008,Summon("lich_summon","霜骨召唤",6,Enemy(3001),10),
                Monster("ice_ring","霜骨冰环",MonsterAbilityTemplate.TelegraphArea,m=>{m.shape=AreaShape.Ring;m.radius=m.range=7;m.innerRadius=2;m.warningSeconds=1;m.cooldown=8;m.status=Effect(ice);m.color=new Color(.3f,.7f,1,.8f);}));
            Set(3007,Monster("sweep","深渊横扫",MonsterAbilityTemplate.TelegraphArea,m=>{m.shape=AreaShape.Cone;m.radius=m.range=6;m.halfAngle=60;m.triggerRange=9;m.warningSeconds=.8f;m.cooldown=5;m.damageRatio=2.5f;}),
                Summon("tide","深渊召唤潮",60,Enemy(3001),25),
                Monster("fissure","深渊地裂",MonsterAbilityTemplate.TelegraphArea,m=>{m.shape=AreaShape.Line;m.radius=1.4f;m.range=20;m.warningSeconds=1.2f;m.cooldown=10;m.damageRatio=3;m.repeatCount=3;m.repeatInterval=.15f;m.repeatOffset=.1f;m.minimumStage=3;}));
            Set(3009,Monster("ember_death","小鬼余火",MonsterAbilityTemplate.OnDeath,m=>{m.deathAction=MonsterDeathAction.GroundZone;m.warningSeconds=0;m.radius=m.range=1.5f;m.duration=2;m.damageRatio=.5f;}));
            Set(3010,Monster("flame_cone","蜥蜴喷火",MonsterAbilityTemplate.TelegraphArea,m=>{m.shape=AreaShape.Cone;m.radius=m.range=4;m.halfAngle=30;m.triggerRange=5;m.warningSeconds=.5f;m.cooldown=6;m.damageRatio=2;}));
            Set(3011,Monster("fireball","术士火球",MonsterAbilityTemplate.ProjectileBarrage,m=>{m.atHero=true;m.projectileAtPoint=true;m.radius=2;m.range=20;m.warningSeconds=.8f;m.cooldown=6;m.speed=15;m.damageRatio=2;m.trailSeconds=3;}));
            Set(3012,Monster("fire_charge","犀牛火线冲锋",MonsterAbilityTemplate.Dash,m=>{m.shape=AreaShape.Line;m.radius=1.2f;m.range=12;m.warningSeconds=.8f;m.cooldown=9;m.speed=16;m.damageRatio=2.5f;m.knockback=2;m.trailSeconds=3;}));
            Set(3013,Monster("fire_explode","火灵自爆",MonsterAbilityTemplate.TelegraphArea,m=>{m.radius=m.range=3;m.triggerRange=3;m.warningSeconds=1;m.sacrifice=true;m.damageRatio=4;}),
                Monster("fire_chain","火灵连锁",MonsterAbilityTemplate.OnDeath,m=>{m.deathAction=MonsterDeathAction.Chain;m.radius=m.range=3;m.warningSeconds=.45f;m.damageRatio=1.5f;}));
            Set(3014,Monster("golem_split","巨像分裂",MonsterAbilityTemplate.OnDeath,m=>{m.deathAction=MonsterDeathAction.Split;m.warningSeconds=0;m.summon=Enemy(3014);m.summonCount=2;m.summonHealthRatio=.2f;}));
            Set(3016,Monster("heal_aura","女巫治疗光环",MonsterAbilityTemplate.Aura,m=>{m.healMaxHealth=.03f;m.damageRatio=0;m.radius=m.range=8;m.duration=2;m.cooldown=2;m.warningSeconds=0;m.triggerRange=30;m.color=new Color(.25f,1,.45f,.4f);}),
                Monster("rain","女巫火雨",MonsterAbilityTemplate.TelegraphArea,m=>{m.atHero=true;m.radius=m.range=2.5f;m.warningSeconds=1;m.cooldown=8;m.damageRatio=2;m.repeatCount=3;m.repeatInterval=.2f;m.repeatOffset=2.5f;m.trailSeconds=2;}));
            Set(3015,Monster("eruption","领主熔岩喷发",MonsterAbilityTemplate.GroundZone,m=>{m.atHero=true;m.radius=m.range=3;m.warningSeconds=1.2f;m.duration=3;m.cooldown=10;m.damageRatio=1.5f;m.repeatCount=5;m.repeatOffset=4;m.repeatInterval=.15f;}),
                Monster("expanding_ring","领主扩散火环",MonsterAbilityTemplate.GroundZone,m=>{m.shape=AreaShape.Ring;m.radius=m.range=18;m.innerRadius=1;m.expandingRing=true;m.duration=4;m.interval=.2f;m.warningSeconds=1;m.cooldown=12;m.damageRatio=5;m.triggerRange=25;}),
                Summon("imp_tide","领主小鬼潮",60,Enemy(3009),25,3));
            foreach(string guid in AssetDatabase.FindAssets("t:GameMapDefinition"))
            {var map=AssetDatabase.LoadAssetAtPath<GameMapDefinition>(AssetDatabase.GUIDToAssetPath(guid));map.lavaHazards=map.configId==7002;map.enemyHealthMultiplier=map.lavaHazards?1.15f:1;map.enemyDamageMultiplier=map.lavaHazards?1.1f:1;EditorUtility.SetDirty(map);
                foreach(var entry in map.enemyRoster.entries)
                {
                    int role=(entry.prefab.configId-3001)%8;if(role>=6) continue;
                    entry.weight=new[]{55f,15,12,6,8,4}[role];entry.firstSecond=new[]{0f,45,90,210,150,300}[role];
                }
                EditorUtility.SetDirty(map.enemyRoster);}
            modules.buffs=modules.buffs.Concat(buffs).Distinct().ToArray();
            modules.effects=modules.effects.Where(x=>x!=null && x.definition && !effects.Contains(x.definition))
                .Concat(effects.Select(x=>new EffectModule{definition=x,playerAllowed=false})).ToArray();
            var catalog=Asset<SkillCatalog>("目录/游戏技能库.asset");catalog.buffs=modules.buffs;
            AssetDatabase.SaveAssets();NumericConfigIds.AssignMissing();
            int total=0;foreach(var a in prefabs.Values) foreach(var m in a.abilities) {m.Compile(catalog);total++;}
            Debug.Log("EXPEDITION_MONSTERS_READY archetypes="+prefabs.Count+" modules="+total);
        }
    }
}
