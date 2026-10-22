using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Abilities.Editor
{
    /// <summary>首次创建测试配置，已有资源原样保留；修改效果和参数应直接编辑资源。</summary>
    public static class SkillDemoAssets
    {
        public const string Root="Assets/Game/Abilities";
        public static SkillCatalog Ensure()
        {
            Directory.CreateDirectory("Assets/GameData/历史示例/技能系统"); Directory.CreateDirectory(Root+"/Materials");
            var burn=Asset<BuffDefinition>("Burn",d=>{d.stableId="buff.burn";d.displayName="灼烧";d.duration=4;d.periodicDamage=5;d.interval=1;d.maxStacks=3;d.stacking=BuffStackRule.AddAndRefresh;});
            var slow=Asset<BuffDefinition>("Frost Slow",d=>{d.stableId="buff.frost";d.displayName="寒霜减速";d.duration=0.6f;d.moveSpeedPercent=-0.35f;});
            var haste=Asset<BuffDefinition>("Haste",d=>{d.stableId="buff.haste";d.displayName="鼓舞加速";d.duration=5;d.moveSpeedPercent=0.5f;});
            var vitality=Asset<BuffDefinition>("Vitality",d=>{d.stableId="buff.vitality";d.displayName="强健体魄";d.duration=0;d.attackDamageFlat=5;d.healthRegen=3;d.dispellable=false;});
            var poison=Asset<BuffDefinition>("Poison",d=>{d.stableId="buff.poison";d.displayName="毒刺";d.duration=3;d.periodicDamage=4;d.interval=1;d.moveSpeedPercent=-0.2f;});
            var q=Asset<SkillDefinition>("Q Burst",d=>{d.stableId="skill.burst";d.displayName="爆裂";d.radius=5;d.cooldown=3;d.manaCost=16;
                d.indicatorColor=new Color(1,0.48f,0.12f);d.effects=new[]{new EffectEntry{kind=EffectKind.Damage,amount=45},Buff(burn)};});
            var w=Asset<SkillDefinition>("W Frost Field",d=>{d.stableId="skill.frost";d.displayName="寒霜区域";d.delivery=SkillDelivery.PersistentCircle;
                d.radius=6;d.cooldown=6;d.manaCost=20;d.areaDuration=5;d.areaInterval=0.2f;d.indicatorColor=Color.cyan;d.effects=new[]{Buff(slow)};});
            var e=Asset<SkillDefinition>("E Inspire",d=>{d.stableId="skill.inspire";d.displayName="鼓舞";d.delivery=SkillDelivery.SelfCircle;d.targets=TargetRelation.Allies;
                d.castRange=8;d.radius=8;d.cooldown=7;d.manaCost=15;d.indicatorColor=new Color(0.3f,1,0.4f);d.effects=new[]{Buff(haste)};});
            var p0=Asset<PassiveDefinition>("Passive Vitality",d=>{d.stableId="passive.vitality";d.displayName="强健体魄";d.trigger=PassiveTrigger.WhileLearned;d.effects=new[]{Buff(vitality)};});
            var p1=Asset<PassiveDefinition>("Passive Poison Sting",d=>{d.stableId="passive.poison";d.displayName="毒刺";d.trigger=PassiveTrigger.BasicAttackHit;d.effects=new[]{Buff(poison)};});
            return Asset<SkillCatalog>("Demo Skill Catalog",d=>{d.skills=new[]{q,w,e};d.buffs=new[]{burn,slow,haste,vitality,poison};d.passives=new[]{p0,p1};});
        }
        private static EffectEntry Buff(BuffDefinition definition) => new EffectEntry{kind=EffectKind.ApplyBuff,buff=definition,buffScale=1};
        private static T Asset<T>(string name,Action<T> initialize) where T:ScriptableObject
        {
            var paths=new System.Collections.Generic.Dictionary<string,string>
            {
                {"Burn","Assets/GameData/历史示例/技能系统/5000_灼烧.asset"},
                {"Demo Skill Catalog","Assets/GameData/历史示例/技能系统/历史示例技能库.asset"},
                {"E Inspire","Assets/GameData/历史示例/技能系统/1004_E_鼓舞示例.asset"},
                {"Frost Slow","Assets/GameData/历史示例/技能系统/5001_冰霜减速.asset"},
                {"Haste","Assets/GameData/历史示例/技能系统/5002_急速.asset"},
                {"Passive Poison Sting","Assets/GameData/历史示例/技能系统/被动_毒刺示例.asset"},
                {"Passive Vitality","Assets/GameData/历史示例/技能系统/被动_活力示例.asset"},
                {"Poison","Assets/GameData/历史示例/技能系统/5003_中毒.asset"},
                {"Q Burst","Assets/GameData/历史示例/技能系统/1005_Q_爆发示例.asset"},
                {"Vitality","Assets/GameData/历史示例/技能系统/5004_活力.asset"},
                {"W Frost Field","Assets/GameData/历史示例/技能系统/1006_W_冰霜区域示例.asset"}
            };
            string path=paths[name];
            var asset=AssetDatabase.LoadAssetAtPath<T>(path); if(asset) return asset;
            asset=ScriptableObject.CreateInstance<T>(); initialize(asset); AssetDatabase.CreateAsset(asset,path); return asset;
        }
        public static Material Material(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            var value=AssetDatabase.LoadAssetAtPath<Material>(path); if(value) return value;
            value=new Material(Shader.Find("Universal Render Pipeline/Unlit")){enableInstancing=true};
            value.SetColor("_BaseColor",color); AssetDatabase.CreateAsset(value,path); return value;
        }
    }
}
