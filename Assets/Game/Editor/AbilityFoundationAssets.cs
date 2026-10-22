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
        public const string Root="Assets/GameData";
        public const string CatalogPath=Root+"/目录/游戏技能库.asset";
        public const string HeroPath=Root+"/英雄/2000_寒冰射手.asset";
        private static readonly System.Collections.Generic.Dictionary<string,string> Paths=new()
        {
            {"Skills/冰晶巨箭","Assets/GameData/技能/1003_冰晶巨箭.asset"},
            {"Skills/冰霜专注","Assets/GameData/技能/1000_冰霜专注.asset"},
            {"Skills/冰霜冲击","Assets/GameData/技能/1002_冰霜冲击.asset"},
            {"Skills/冰霜扇击","Assets/GameData/技能/1001_冰霜扇击.asset"},
            {"Effects/冰晶爆炸伤害","Assets/GameData/效果/4001_冰晶爆炸伤害.asset"},
            {"Effects/冰晶首中伤害","Assets/GameData/效果/4002_冰晶首中伤害.asset"},
            {"Effects/冰霜冲击伤害","Assets/GameData/效果/4003_冰霜冲击伤害.asset"},
            {"Effects/冰霜扇击伤害","Assets/GameData/效果/4004_冰霜扇击伤害.asset"},
            {"Effects/施加冰晶减速","Assets/GameData/效果/4005_施加冰晶减速.asset"},
            {"Effects/施加冰晶眩晕","Assets/GameData/效果/4006_施加冰晶眩晕.asset"},
            {"Effects/施加冰霜专注","Assets/GameData/效果/4007_施加冰霜专注.asset"},
            {"Effects/施加扇击减速","Assets/GameData/效果/4008_施加扇击减速.asset"},
            {"Effects/通用治疗","Assets/GameData/效果/4009_通用治疗.asset"},
            {"Effects/通用驱散","Assets/GameData/效果/4010_通用驱散.asset"},
            {"Buffs/冰晶减速","Assets/GameData/Buff/5005_冰晶减速.asset"},
            {"Buffs/冰晶眩晕","Assets/GameData/Buff/5006_冰晶眩晕.asset"},
            {"Buffs/冰霜专注","Assets/GameData/Buff/5007_冰霜专注.asset"},
            {"Buffs/扇击减速","Assets/GameData/Buff/5008_扇击减速.asset"},
            {"寒冰射手","Assets/GameData/英雄/2000_寒冰射手.asset"},
            {"寒冰射手·换槽示例","Assets/GameData/编辑示例/换槽/2001_寒冰射手·换槽示例.asset"},
            {"游戏技能库","Assets/GameData/目录/游戏技能库.asset"},
            {"组合模块目录","Assets/GameData/目录/组合模块目录.asset"}
        };
        public static void LocalizeExisting() => Ensure(out _,out _);
        public static void Ensure(out SkillCatalog catalog,out HeroDefinition hero)
        {
            foreach(var path in Paths.Values) Folder(Path.GetDirectoryName(path).Replace('\\','/'));
            var focus=Buff("冰霜专注","buff.frost_focus",5,AttributeKind.AttackSpeed,0.6f);
            var slow=Buff("扇击减速","buff.volley_slow",2.5f,AttributeKind.MoveSpeed,-0.35f);
            var ultimateSlow=Buff("冰晶减速","buff.crystal_slow",3,AttributeKind.MoveSpeed,-0.5f);
            var stun=Asset<BuffDefinition>("Buffs/冰晶眩晕",x=>
            {
                x.stableId="buff.crystal_stun"; x.displayName="冰晶眩晕"; x.duration=1.5f;
                x.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked;
            });
            var focusEffect=Apply("施加冰霜专注",focus);
            var slowEffect=Apply("施加扇击减速",slow);
            var ultimateSlowEffect=Apply("施加冰晶减速",ultimateSlow);
            var stunEffect=Apply("施加冰晶眩晕",stun);
            var volleyDamage=Damage("冰霜扇击伤害",60); var frostDamage=Damage("冰霜冲击伤害",90);
            var impactDamage=Damage("冰晶首中伤害",120); var explosionDamage=Damage("冰晶爆炸伤害",100);
            var q=Asset<SkillDefinition>("Skills/冰霜专注",x=>
            {
                Common(x,"ability.frost_focus","冰霜专注",AimMode.Self,0,10); x.requiredHits=4; x.manaCost=20;
                Growth(x,new[]{1,3,5,7},new[]{1f,1f,1f,1f},new[]{1f,1.25f,1.5f,1.75f},new[]{10f,9f,8f,7f},new[]{20f,22f,24f,26f});
                x.stages=new[]{new SkillStage {scope=TargetScope.Self,targets=TargetRelation.Allies,range=0,
                    effects=new EffectDefinition[]{focusEffect}}};
            });
            var w=Asset<SkillDefinition>("Skills/冰霜扇击",x=>
            {
                Common(x,"ability.volley","冰霜扇击",AimMode.Direction,22,8); x.manaCost=35;
                Growth(x,new[]{1,3,5,7},new[]{1f,100f/60,140f/60,3f},new[]{1f,1f,1f,1f},new[]{8f,7f,6f,5f},new[]{35f,40f,45f,50f});
                x.stages=new[]{new SkillStage {scope=TargetScope.Area,shape=AreaShape.Cone,range=22,radius=22,
                    halfAngleDegrees=35,effects=new EffectDefinition[]{volleyDamage,slowEffect}}};
            });
            var e=Asset<SkillDefinition>("Skills/冰霜冲击",x=>
            {
                Common(x,"ability.frost_impact","冰霜冲击",AimMode.Point,25,0); x.maxCharges=2; x.rechargeSeconds=14; x.manaCost=50;
                Growth(x,new[]{1,3,5,7},new[]{1f,150f/90,210f/90,3f},new[]{1f,1f,1f,1f},new[]{14f,13f,12f,11f},new[]{50f,55f,60f,65f});
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.PointProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=25,radius=5,speed=42,effects=new EffectDefinition[]{frostDamage}}};
            });
            var r=Asset<SkillDefinition>("Skills/冰晶巨箭",x=>
            {
                Common(x,"ability.crystal_arrow","冰晶巨箭",AimMode.Direction,150,30); x.manaCost=90;
                Growth(x,new[]{6,12,18},new[]{1f,1.75f,2.5f},new[]{1f,1f,1f},new[]{30f,25f,20f},new[]{90f,100f,110f});
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.LinearProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=150,radius=7,hitRadius=1.15f,speed=48,
                    impactEffects=new EffectDefinition[]{impactDamage,stunEffect},effects=new EffectDefinition[]{explosionDamage,ultimateSlowEffect}}};
            });
            var skillSet=new[]{q,w,e,r};
            catalog=Asset<SkillCatalog>("游戏技能库",x=>
            {x.skills=skillSet; x.buffs=new[]{focus,slow,ultimateSlow,stun}; x.passives=Array.Empty<PassiveDefinition>();});
            hero=Asset<HeroDefinition>("寒冰射手",x=>
            {x.stableId="hero.frost_archer"; x.displayName="寒冰射手"; x.skills=skillSet; x.baseMana=260; x.health=340; x.baseHealthRegen=1.8f; x.baseManaRegen=4.5f; x.healthPerLevel=38; x.manaPerLevel=17; x.attackPerLevel=2.8f; x.attackSpeedPerLevel=2.8f; x.regenPerLevel=0.19f; x.manaRegenPerLevel=0.25f; x.speed=10; x.attackDamage=30; x.attackInterval=0.12f; x.range=20;});
            Asset<HeroDefinition>("寒冰射手·换槽示例",x=>
            {x.stableId="hero.frost_archer_alternate"; x.displayName="寒冰射手·换槽配置示例"; x.skills=new[]{e,w,q,r}; x.baseMana=260; x.health=340; x.baseHealthRegen=1.8f; x.baseManaRegen=4.5f; x.healthPerLevel=38; x.manaPerLevel=17; x.attackPerLevel=2.8f; x.attackSpeedPerLevel=2.8f; x.regenPerLevel=0.19f; x.manaRegenPerLevel=0.25f; x.speed=10; x.attackDamage=30; x.attackInterval=0.12f; x.range=20;});
            AssetDatabase.SaveAssets();
        }
        private static void Growth(SkillDefinition skill,int[] gates,float[] effect,float[] buff,float[] cooldown,float[] mana)
        {
            var rows=new SkillLevelRow[gates.Length];
            for(int i=0;i<rows.Length;i++) rows[i]=new SkillLevelRow {requiredHeroLevel=gates[i],effectScale=effect[i],buffScale=buff[i],cooldownScale=cooldown[i]/cooldown[0],manaCostScale=mana[i]/mana[0]};
            skill.growth=new SkillLevelGrowth {track=SkillLevelTrack.Custom,customLevels=rows};
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
            string path=Paths[relativePath];
            var existing=AssetDatabase.LoadAssetAtPath<T>(path); if(existing) return existing;
            if(AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path))
                throw new InvalidOperationException("资源路径被不兼容或无法读取的资源占用："+path);
            var value=ScriptableObject.CreateInstance<T>();
            try { value.name=Path.GetFileNameWithoutExtension(path); initialize(value);
                var number=Path.GetFileName(path).Split('_')[0];
                if(int.TryParse(number,out int id))
                { var serialized=new SerializedObject(value); var field=serialized.FindProperty("configId"); if(field!=null) { field.intValue=id; serialized.ApplyModifiedPropertiesWithoutUndo(); } }
                AssetDatabase.CreateAsset(value,path); }
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

