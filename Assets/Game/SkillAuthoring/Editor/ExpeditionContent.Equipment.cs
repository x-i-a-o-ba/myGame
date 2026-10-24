using System;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Flow;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    public static partial class ExpeditionContent
    {
        static EquipmentDefinition Item(int id,string name,EquipmentQuality quality,int price,string description,params StatModifier[] stats)
        {
            EquipmentDefinition item=null;
            foreach(var guid in AssetDatabase.FindAssets("t:EquipmentDefinition",new[]{"Assets/GameData"}))
            {
                var candidate=AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(candidate.configId==id) {item=candidate;break;}
            }
            string path=Root+$"装备/{id}_{name}.asset";
            if(!item) item=Asset<EquipmentDefinition>($"装备/{id}_{name}.asset");
            else if(AssetDatabase.GetAssetPath(item)!=path)
            {
                string error=AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(item),path);
                if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            item.name=id+"_"+name;item.configId=id;
            if(string.IsNullOrEmpty(item.stableId)) item.stableId="item.expedition_"+id;
            item.displayName=name;item.quality=quality;item.price=price;item.unique=false;item.stats=stats;
            item.onHitEffects=Array.Empty<EffectDefinition>();item.procs=Array.Empty<EquipmentProc>();
            item.description=string.Join("\n",stats.Select(s=>s.ToString()))+(string.IsNullOrEmpty(description)?"":"\n"+description);
            EditorUtility.SetDirty(item);return item;
        }
        static StatModifier Stat(StatKind kind,float amount)=>new StatModifier(kind,amount);
        static RelicDefinition Relic(string id,string name,string description,Action<RelicDefinition> configure)
        {
            var relic=Asset<RelicDefinition>($"遗物/{name}.asset");
            relic.stableId="relic."+id;relic.displayName=name;relic.description=description;
            relic.extraBasicTargets=0;relic.echoChance=0;relic.killHealMaxHealth=0;relic.skillHitBuff=null;
            relic.bodyScale=relic.maxHealthMultiplier=relic.basicRangeMultiplier=relic.controlDurationMultiplier=relic.eliteGoldMultiplier=1;
            relic.cooldownReduction=0;relic.cooldownCap=50;configure(relic);relic.Validate();return relic;
        }
        static DamageEffect ProcDamage(int id,string name,float damage,float ratio)
        {
            var effect=Asset<DamageEffect>($"效果/{id}_{name}.asset");effect.configId=id;effect.stableId="effect.expedition_"+id;
            effect.amount=damage;effect.sourceAttackRatio=ratio;effect.targetMaxHealthRatio=0;effects.Add(effect);return effect;
        }
        [MenuItem("Tools/Roguelike/远征/迁移装备与遗物")]
        public static void EquipmentAndRelics()
        {
            modules=Asset<SkillModuleCatalog>("目录/组合模块目录.asset");buffs.Clear();effects.Clear();
            int frost=Apply(4064,"霜语命中减速",Buff(5061,"霜语迟缓",1.5f,b=>b.moveSpeedPercent=-.25f));
            int ember=Apply(4065,"余烬技能灼烧",Buff(5062,"余烬灼烧",3,b=>b.periodicAttackRatio=.2f));
            var frostEffect=(ApplyBuffEffect)effects.First(x=>x.configId==frost);
            var emberEffect=(ApplyBuffEffect)effects.First(x=>x.configId==ember);
            var chain=ProcDamage(4066,"闪电链伤害",60,1);var splash=ProcDamage(4067,"巨斧溅射",0,.5f);var crown=ProcDamage(4068,"风暴落雷",150,.5f);
            var items=new[]{
                Item(6000,"铁剑",EquipmentQuality.Common,500,"",Stat(StatKind.AttackDamage,15)),
                Item(6001,"迅捷手套",EquipmentQuality.Common,500,"",Stat(StatKind.AttackSpeed,20)),
                Item(6002,"皮甲",EquipmentQuality.Common,450,"",Stat(StatKind.MaxHealth,200)),
                Item(6003,"疾风靴",EquipmentQuality.Common,500,"唯一装备",Stat(StatKind.MoveSpeed,1.2f)),
                Item(6005,"回复戒指",EquipmentQuality.Common,450,"",Stat(StatKind.HealthRegen,5)),
                Item(6009,"法师之书",EquipmentQuality.Common,550,"",Stat(StatKind.SkillAmplify,15)),
                Item(6011,"时光沙漏",EquipmentQuality.Common,600,"",Stat(StatKind.CooldownReduction,10)),
                Item(6004,"法力宝石",EquipmentQuality.Common,450,"",Stat(StatKind.MaxMana,150),Stat(StatKind.ManaRegen,2)),
                Item(6012,"霜语长弓",EquipmentQuality.Rare,2200,"普攻使目标减速 25%，持续 1.5 秒。",Stat(StatKind.AttackDamage,30),Stat(StatKind.AttackSpeed,20)),
                Item(6010,"守护铠甲",EquipmentQuality.Rare,2000,"",Stat(StatKind.MaxHealth,450),Stat(StatKind.DamageReduction,12)),
                Item(6013,"奥术法杖",EquipmentQuality.Rare,2200,"",Stat(StatKind.SkillAmplify,30),Stat(StatKind.CooldownReduction,10),Stat(StatKind.MaxMana,250)),
                Item(6016,"嗜血之刃",EquipmentQuality.Rare,1900,"",Stat(StatKind.AttackDamage,25),Stat(StatKind.LifeSteal,12)),
                Item(6018,"雷鸣锤",EquipmentQuality.Rare,2400,"主普攻命中有 25% 几率触发五目标闪电链，每跳 60 + 100% 攻击伤害。",Stat(StatKind.AttackDamage,20),Stat(StatKind.AttackSpeed,15)),
                Item(6014,"狂战巨斧",EquipmentQuality.Legendary,4200,"主普攻命中向周围 3.5 米溅射 50% 攻击伤害。",Stat(StatKind.AttackDamage,50)),
                Item(6015,"泰坦之心",EquipmentQuality.Legendary,4000,"",Stat(StatKind.MaxHealth,1200),Stat(StatKind.HealthRegen,15),Stat(StatKind.DamageReduction,10)),
                Item(6017,"风暴王冠",EquipmentQuality.Legendary,4600,"施放主动技能时，对 8 米内最多六个敌人落雷，每道 150 + 50% 攻击伤害。",Stat(StatKind.SkillAmplify,35),Stat(StatKind.CooldownReduction,15))
            };
            items[3].unique=true;items[8].onHitEffects=new EffectDefinition[]{frostEffect};
            items[12].procs=new[]{new EquipmentProc{trigger=EquipmentTrigger.BasicHit,chance=.25f,radius=6,maxTargets=5,chain=true,effects=new EffectDefinition[]{chain}}};
            items[13].procs=new[]{new EquipmentProc{trigger=EquipmentTrigger.BasicHit,radius=3.5f,maxTargets=32,includePrimary=false,effects=new EffectDefinition[]{splash}}};
            items[15].procs=new[]{new EquipmentProc{trigger=EquipmentTrigger.AbilityCast,radius=8,maxTargets=6,effects=new EffectDefinition[]{crown}}};
            var catalog=Asset<EquipmentCatalog>("目录/装备库.asset");catalog.items=items;catalog.slotCount=6;catalog.startingGold=300;catalog.sellRatio=.5f;
            // 这些旧属性装备已经从目录移除，显式迁移时删除资源；Git 保留历史。
            foreach(var old in new[]{"6006_侦察镜","6007_吸血獠牙","6008_幸运匕首"}) AssetDatabase.DeleteAsset(Root+"装备/"+old+".asset");
            modules.buffs=modules.buffs.Concat(buffs).Distinct().ToArray();
            modules.effects=modules.effects.Where(x=>x!=null && x.definition && !effects.Contains(x.definition))
                .Concat(effects.Select(x=>new EffectModule{definition=x,playerAllowed=true,allowAmountOverride=x is DamageEffect,maxAmount=100000})).ToArray();
            var skills=Asset<SkillCatalog>("目录/游戏技能库.asset");skills.buffs=modules.buffs;
            var expedition=Asset<ExpeditionCatalog>("目录/远征配置.asset");expedition.selection=Asset<StartSelectionCatalog>("目录/开局选择目录.asset");
            expedition.relics=new[]{
                Relic("hunter","狂猎徽记","分裂箭或顺劈的额外目标数 +1。",x=>x.extraBasicTargets=1),
                Relic("echo","回响水晶","主动技能有 15% 几率不消耗冷却或充能；仍消耗法力。",x=>x.echoChance=.15f),
                Relic("blood","血契","击杀回复 0.3% 最大生命。",x=>x.killHealMaxHealth=.003f),
                Relic("ember","余烬之心","技能伤害使敌人灼烧 3 秒，每秒造成施加时 20% 攻击伤害。",x=>x.skillHitBuff=emberEffect),
                Relic("frost","寒霜之心","施加的减速与冻结持续时间延长 50%。",x=>x.controlDurationMultiplier=1.5f),
                Relic("giant","巨人腰带","体型、最大生命与普攻距离增加 20%。",x=>{x.bodyScale=1.2f;x.maxHealthMultiplier=1.2f;x.basicRangeMultiplier=1.2f;}),
                Relic("sand","时之砂","冷却缩减 +10%，冷却缩减上限提高到 60%。",x=>{x.cooldownReduction=10;x.cooldownCap=60;}),
                Relic("bounty","赏金令","精英和首领的金币奖励翻倍。",x=>x.eliteGoldMultiplier=2)
            };
            string[] icons={"冰箭普攻","冰霜专注","嗜血之刃","火球","冰霜冲击","泰坦之心","冰晶巨箭","金币"};
            for(int i=0;i<expedition.relics.Length;i++)
            {
                expedition.relics[i].icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/UI/Icons/"+icons[i]+".png");
                if(!expedition.relics[i].icon) expedition.relics[i].icon=items[Mathf.Min(i,items.Length-1)].icon;
            }
            AssetDatabase.SaveAssets();NumericConfigIds.AssignMissing();
            using(var library=catalog.Compile(skills)) { }
            Debug.Log("EXPEDITION_EQUIPMENT_READY items=16 relics=8");
        }
    }
}
