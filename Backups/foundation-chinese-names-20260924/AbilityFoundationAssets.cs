using System;
using System.IO;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>首次生成共享技能积木及两份英雄装配。重复执行仅创建缺失资源，保留手改及 GUID。</summary>
    public static class AbilityFoundationAssets
    {
        public const string Root="Assets/Game/Skills/Foundation";
        public const string CatalogPath=Root+"/Game Skill Catalog.asset";
        public const string HeroPath=Root+"/Frost Archer.asset";
        public static void Ensure(out SkillCatalog catalog,out HeroDefinition hero)
        {
            Folder(Root); Folder(Root+"/Buffs"); Folder(Root+"/Effects"); Folder(Root+"/Skills");
            var focus=Buff("Frost Focus","buff.frost_focus",5,AttributeKind.AttackSpeed,0.6f);
            var slow=Buff("Volley Slow","buff.volley_slow",2.5f,AttributeKind.MoveSpeed,-0.35f);
            var ultimateSlow=Buff("Crystal Slow","buff.crystal_slow",3,AttributeKind.MoveSpeed,-0.5f);
            var stun=Asset<BuffDefinition>("Buffs/Crystal Stun",x=>
            {
                x.stableId="buff.crystal_stun"; x.displayName="冰晶眩晕"; x.duration=1.5f;
                x.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked;
            });
            var focusEffect=Apply("Apply Frost Focus",focus);
            var slowEffect=Apply("Apply Volley Slow",slow);
            var ultimateSlowEffect=Apply("Apply Crystal Slow",ultimateSlow);
            var stunEffect=Apply("Apply Crystal Stun",stun);
            var volleyDamage=Damage("Volley Damage",30); var frostDamage=Damage("Frost Impact Damage",18);
            var impactDamage=Damage("Crystal Impact Damage",20); var explosionDamage=Damage("Crystal Explosion Damage",16);
            var q=Asset<SkillDefinition>("Skills/Frost Focus",x=>
            {
                Common(x,"ability.frost_focus","冰霜专注",AimMode.Self,0,7); x.requiredHits=4;
                x.stages=new[]{new SkillStage {scope=TargetScope.Self,targets=TargetRelation.Allies,range=0,
                    effects=new EffectDefinition[]{focusEffect}}};
            });
            var w=Asset<SkillDefinition>("Skills/Volley",x=>
            {
                Common(x,"ability.volley","冰霜扇击",AimMode.Direction,22,5);
                x.stages=new[]{new SkillStage {scope=TargetScope.Area,shape=AreaShape.Cone,range=22,radius=22,
                    halfAngleDegrees=35,effects=new EffectDefinition[]{volleyDamage,slowEffect}}};
            });
            var e=Asset<SkillDefinition>("Skills/Frost Impact",x=>
            {
                Common(x,"ability.frost_impact","冰霜冲击",AimMode.Point,25,0); x.maxCharges=2; x.rechargeSeconds=15;
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.PointProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=25,radius=5,speed=42,effects=new EffectDefinition[]{frostDamage}}};
            });
            var r=Asset<SkillDefinition>("Skills/Crystal Arrow",x=>
            {
                Common(x,"ability.crystal_arrow","冰晶巨箭",AimMode.Direction,150,20);
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.LinearProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=150,radius=7,hitRadius=1.15f,speed=48,
                    impactEffects=new EffectDefinition[]{impactDamage,stunEffect},effects=new EffectDefinition[]{explosionDamage,ultimateSlowEffect}}};
            });
            var skillSet=new[]{q,w,e,r};
            catalog=Asset<SkillCatalog>("Game Skill Catalog",x=>
            {x.skills=skillSet; x.buffs=new[]{focus,slow,ultimateSlow,stun}; x.passives=Array.Empty<PassiveDefinition>();});
            hero=Asset<HeroDefinition>("Frost Archer",x=>
            {x.stableId="hero.frost_archer"; x.displayName="寒冰射手"; x.skills=skillSet; x.health=300; x.speed=10; x.attackDamage=30; x.attackInterval=0.12f; x.range=20;});
            Asset<HeroDefinition>("Frost Archer Alternate Loadout",x=>
            {x.stableId="hero.frost_archer_alternate"; x.displayName="寒冰射手·换槽配置示例"; x.skills=new[]{e,w,q,r}; x.health=300; x.speed=10; x.attackDamage=30; x.attackInterval=0.12f; x.range=20;});
            AssetDatabase.SaveAssets();
        }
        private static void Common(SkillDefinition skill,string id,string label,AimMode aim,float range,float cooldown)
        {
            skill.stableId=id; skill.displayName=label; skill.aim=aim; skill.castRange=range;
            skill.cooldown=cooldown; skill.manaCost=0; skill.levelPower=0;
        }
        private static BuffDefinition Buff(string name,string id,float duration,AttributeKind attribute,float percent)
            =>Asset<BuffDefinition>("Buffs/"+name,x=>
            {
                x.stableId=id; x.displayName=name; x.duration=duration;
                x.modifiers=new[]{new AttributeModifier {attribute=attribute,operation=ModifierOperation.Percent,value=percent}};
            });
        private static ApplyBuffEffect Apply(string name,BuffDefinition buff)=>Asset<ApplyBuffEffect>("Effects/"+name,x=>x.buff=buff);
        private static DamageEffect Damage(string name,float value)=>Asset<DamageEffect>("Effects/"+name,x=>x.amount=value);
        private static T Asset<T>(string relativePath,Action<T> initialize) where T:ScriptableObject
        {
            string path=Root+"/"+relativePath+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<T>(path); if(existing) return existing;
            if(AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path))
                throw new InvalidOperationException("资源路径被不兼容或无法读取的资源占用："+path);
            var value=ScriptableObject.CreateInstance<T>();
            try { value.name=Path.GetFileNameWithoutExtension(path); initialize(value); AssetDatabase.CreateAsset(value,path); }
            catch { if(value && !EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value); throw; }
            return value;
        }
        private static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path)?.Replace('\\','/');
            if(string.IsNullOrEmpty(parent)) throw new IOException("无效资源目录："+path);
            Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}

