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
        public const string CatalogPath=Root+"/游戏技能库.asset";
        public const string HeroPath=Root+"/寒冰射手.asset";
        private static readonly (string oldPath,string newPath)[] ChineseNames=
        {
            ("Buffs/Crystal Slow","Buffs/冰晶减速"),
            ("Buffs/Crystal Stun","Buffs/冰晶眩晕"),
            ("Buffs/Frost Focus","Buffs/冰霜专注"),
            ("Buffs/Volley Slow","Buffs/扇击减速"),
            ("Effects/Apply Crystal Slow","Effects/施加冰晶减速"),
            ("Effects/Apply Crystal Stun","Effects/施加冰晶眩晕"),
            ("Effects/Apply Frost Focus","Effects/施加冰霜专注"),
            ("Effects/Apply Volley Slow","Effects/施加扇击减速"),
            ("Effects/Crystal Explosion Damage","Effects/冰晶爆炸伤害"),
            ("Effects/Crystal Impact Damage","Effects/冰晶首中伤害"),
            ("Effects/Frost Impact Damage","Effects/冰霜冲击伤害"),
            ("Effects/Volley Damage","Effects/冰霜扇击伤害"),
            ("Skills/Crystal Arrow","Skills/冰晶巨箭"),
            ("Skills/Frost Focus","Skills/冰霜专注"),
            ("Skills/Frost Impact","Skills/冰霜冲击"),
            ("Skills/Volley","Skills/冰霜扇击"),
            ("Game Skill Catalog","游戏技能库"),
            ("Frost Archer","寒冰射手"),
            ("Frost Archer Alternate Loadout","寒冰射手·换槽示例")
        };

        public static void LocalizeExisting()
        {
            int moved=RenameExisting();
            Ensure(out var catalog,out var hero);
            if(!catalog || !hero) throw new InvalidOperationException("技能库或英雄资源缺失");
            Debug.Log($"技能资源中文命名完成：移动 {moved} 份，当前 {ChineseNames.Length} 份；原 GUID 保持不变。");
        }

        private static int RenameExisting()
        {
            foreach(var pair in ChineseNames)
            {
                string oldPath=Root+"/"+pair.oldPath+".asset";
                string newPath=Root+"/"+pair.newPath+".asset";
                if(AssetDatabase.LoadMainAssetAtPath(oldPath) && AssetDatabase.LoadMainAssetAtPath(newPath))
                    throw new InvalidOperationException("中文与英文资源同时存在，请先处理冲突："+oldPath+" / "+newPath);
            }
            int moved=0;
            foreach(var pair in ChineseNames)
            {
                string oldPath=Root+"/"+pair.oldPath+".asset";
                if(!AssetDatabase.LoadMainAssetAtPath(oldPath)) continue;
                string newPath=Root+"/"+pair.newPath+".asset";
                string guid=AssetDatabase.AssetPathToGUID(oldPath);
                string error=AssetDatabase.MoveAsset(oldPath,newPath);
                if(!string.IsNullOrEmpty(error)) throw new IOException("资源中文命名失败："+oldPath+" → "+newPath+"："+error);
                if(guid!=AssetDatabase.AssetPathToGUID(newPath)) throw new IOException("资源 GUID 在改名后发生变化："+newPath);
                var asset=AssetDatabase.LoadMainAssetAtPath(newPath);
                asset.name=Path.GetFileNameWithoutExtension(newPath);
                if(asset is BuffDefinition buff && buff.displayName==Path.GetFileNameWithoutExtension(oldPath))
                    buff.displayName=asset.name;
                EditorUtility.SetDirty(asset);
                moved++;
            }
            if(moved>0) AssetDatabase.SaveAssets();
            return moved;
        }
        public static void Ensure(out SkillCatalog catalog,out HeroDefinition hero)
        {
            RenameExisting();
            Folder(Root); Folder(Root+"/Buffs"); Folder(Root+"/Effects"); Folder(Root+"/Skills");
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
            var volleyDamage=Damage("冰霜扇击伤害",30); var frostDamage=Damage("冰霜冲击伤害",18);
            var impactDamage=Damage("冰晶首中伤害",20); var explosionDamage=Damage("冰晶爆炸伤害",16);
            var q=Asset<SkillDefinition>("Skills/冰霜专注",x=>
            {
                Common(x,"ability.frost_focus","冰霜专注",AimMode.Self,0,7); x.requiredHits=4;
                x.stages=new[]{new SkillStage {scope=TargetScope.Self,targets=TargetRelation.Allies,range=0,
                    effects=new EffectDefinition[]{focusEffect}}};
            });
            var w=Asset<SkillDefinition>("Skills/冰霜扇击",x=>
            {
                Common(x,"ability.volley","冰霜扇击",AimMode.Direction,22,5);
                x.stages=new[]{new SkillStage {scope=TargetScope.Area,shape=AreaShape.Cone,range=22,radius=22,
                    halfAngleDegrees=35,effects=new EffectDefinition[]{volleyDamage,slowEffect}}};
            });
            var e=Asset<SkillDefinition>("Skills/冰霜冲击",x=>
            {
                Common(x,"ability.frost_impact","冰霜冲击",AimMode.Point,25,0); x.maxCharges=2; x.rechargeSeconds=15;
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.PointProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=25,radius=5,speed=42,effects=new EffectDefinition[]{frostDamage}}};
            });
            var r=Asset<SkillDefinition>("Skills/冰晶巨箭",x=>
            {
                Common(x,"ability.crystal_arrow","冰晶巨箭",AimMode.Direction,150,20);
                x.stages=new[]{new SkillStage {delivery=DeliveryKind.LinearProjectile,scope=TargetScope.Area,
                    shape=AreaShape.Circle,range=150,radius=7,hitRadius=1.15f,speed=48,
                    impactEffects=new EffectDefinition[]{impactDamage,stunEffect},effects=new EffectDefinition[]{explosionDamage,ultimateSlowEffect}}};
            });
            var skillSet=new[]{q,w,e,r};
            catalog=Asset<SkillCatalog>("游戏技能库",x=>
            {x.skills=skillSet; x.buffs=new[]{focus,slow,ultimateSlow,stun}; x.passives=Array.Empty<PassiveDefinition>();});
            hero=Asset<HeroDefinition>("寒冰射手",x=>
            {x.stableId="hero.frost_archer"; x.displayName="寒冰射手"; x.skills=skillSet; x.health=300; x.speed=10; x.attackDamage=30; x.attackInterval=0.12f; x.range=20;});
            Asset<HeroDefinition>("寒冰射手·换槽示例",x=>
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

