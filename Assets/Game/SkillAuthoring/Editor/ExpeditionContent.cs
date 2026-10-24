using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>显式内容迁移。复用原资产及 GUID，新增形态仍使用同一配方/效果编译边界。</summary>
    public static partial class ExpeditionContent
    {
        const string Root="Assets/GameData/";
        static SkillModuleCatalog modules;
        static readonly List<BuffDefinition> buffs=new List<BuffDefinition>();
        static readonly List<EffectDefinition> effects=new List<EffectDefinition>();
        static T Asset<T>(string path) where T:ScriptableObject
        {
            path=Root+path;
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!asset)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
                asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path);
            }
            EditorUtility.SetDirty(asset);return asset;
        }
        static T Find<T>(int id) where T:ScriptableObject
        {
            foreach(var guid in AssetDatabase.FindAssets("t:"+typeof(T).Name,new[]{Root.TrimEnd('/')}))
            {
                var asset=AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                var property=new SerializedObject(asset).FindProperty("configId");
                if(property!=null && property.intValue==id) {EditorUtility.SetDirty(asset);return asset;}
            }
            throw new InvalidOperationException("缺少配置 "+id);
        }
        static BuffDefinition Buff(int id,string name,float duration,Action<BuffDefinition> configure)
        {
            var b=Asset<BuffDefinition>($"Buff/{id}_{name}.asset");
            b.configId=id;b.stableId="buff.expedition_"+id;b.displayName=name;b.duration=duration;b.interval=1;
            b.modifiers=Array.Empty<AttributeModifier>();b.controls=ControlFlags.None;b.moveSpeedPercent=0;
            b.attackDamageFlat=0;b.healthRegen=0;b.periodicDamage=0;b.periodicAttackRatio=0;
            b.damageReduction=0;b.regenMaxHealth=0;b.damageTaken=0;b.reflect=0;b.extraTargets=0;
            b.controlImmune=false;b.pierceBasics=false;b.attackSplashRadius=0;b.attackSplashRatio=0;
            configure(b);buffs.Add(b);return b;
        }
        static int Apply(int id,string name,BuffDefinition buff,EffectTarget target=EffectTarget.Selected)
        {
            var effect=Asset<ApplyBuffEffect>($"效果/{id}_{name}.asset");
            effect.configId=id;effect.stableId="effect.expedition_"+id;effect.target=target;effect.buff=buff;effect.buffScale=1;
            effects.Add(effect);return id;
        }
        static RecipeEffectUse Use(int id,float amount=-1)=>new RecipeEffectUse{effectId=id,overrideAmount=amount>=0,amount=Mathf.Max(0,amount)};
        static RecipeNode Node(int query,params RecipeEffectUse[] uses)=>new RecipeNode{deliveryModuleId=10000,queryModuleId=query,effects=uses};
        static SkillDefinition Configure(int id,AimMode aim,float range,float cooldown,float mana,string description,params RecipeNode[] nodes)
        {
            var skill=Find<SkillDefinition>(id);
            skill.aim=aim;skill.castRange=range;skill.cooldown=cooldown;skill.manaCost=mana;skill.castTime=0;skill.recoveryTime=0;
            skill.requiredHits=0;skill.autoTriggerEveryHits=0;skill.maxCharges=1;skill.rechargeSeconds=cooldown;
            skill.description=description;skill.modules=modules;skill.stages=Array.Empty<SkillStage>();
            skill.recipe=new SkillRecipe{enabled=true,recipeId=id,authorNamespace="official",maxLifetime=20,maxDerivedExecutions=128,nodes=nodes};
            for(int i=0;i<nodes.Length;i++) nodes[i].nodeId=i+1;
            return skill;
        }
        static void Levels(SkillDefinition skill,float[] damage,float[] cooldown,float[] mana,float[] radius=null,float[] buff=null,int[] arrows=null,float[] spread=null,int[] extra=null)
        {
            var rows=new SkillLevelRow[damage.Length];
            for(int i=0;i<rows.Length;i++) rows[i]=new SkillLevelRow
            {
                requiredHeroLevel=rows.Length==3?SkillLevelGrowth.UltimateGates[i]:SkillLevelGrowth.BasicGates[i],
                effectScale=damage[i]/damage[0],cooldownScale=cooldown[0]>0?cooldown[i]/cooldown[0]:1,
                manaCostScale=mana[0]>0?mana[i]/mana[0]:1,radiusScale=radius!=null?radius[i]/radius[0]:1,
                buffScale=buff!=null?buff[i]/buff[0]:1,projectileBonus=arrows!=null?arrows[i]-arrows[0]:0,
                spreadBonus=spread!=null?spread[i]-spread[0]:0,extraTargets=extra!=null?extra[i]:0
            };
            skill.growth=new SkillLevelGrowth{track=SkillLevelTrack.Custom,customLevels=rows};
        }
        static SkillDefinition Form(SkillDefinition basis,int id,string name,string description,Action<SkillDefinition> change)
        {
            var variant=Asset<SkillDefinition>($"技能/形态/{id}_{name}.asset");
            EditorUtility.CopySerialized(basis,variant);
            variant.name=id+"_"+name;variant.configId=id;variant.stableId="ability.form_"+id;
            variant.displayName=name;variant.description=description;variant.forms=Array.Empty<SkillDefinition>();
            variant.recipe=JsonUtility.FromJson<SkillRecipe>(JsonUtility.ToJson(basis.recipe));
            variant.recipe.recipeId=id;change(variant);EditorUtility.SetDirty(variant);return variant;
        }
        [MenuItem("Tools/Roguelike/远征/迁移英雄与共享模块")]
        public static void Heroes()
        {
            buffs.Clear();effects.Clear();modules=Asset<SkillModuleCatalog>("目录/组合模块目录.asset");
            var damage=Asset<DamageEffect>("效果/4050_远征通用伤害.asset");damage.configId=4050;damage.stableId="effect.expedition_damage";
            damage.amount=1;damage.sourceAttackRatio=0;damage.targetMaxHealthRatio=0;effects.Add(damage);
            var quake=Asset<DamageEffect>("效果/4051_攻击倍率伤害.asset");quake.configId=4051;quake.stableId="effect.attack_scaled";
            quake.amount=0;quake.sourceAttackRatio=2;quake.targetMaxHealthRatio=0;effects.Add(quake);
            var pull=Asset<DisplacementEffect>("效果/4052_聚拢敌群.asset");pull.configId=4052;pull.stableId="effect.pull";pull.pull=true;pull.distance=20;pull.stopDistance=2.5f;effects.Add(pull);
            int slow=Apply(4053,"寒箭减速",Buff(5050,"寒箭减速",2,b=>b.moveSpeedPercent=-.35f));
            int freeze=Apply(4054,"冰晶冻结",Buff(5051,"冰晶冻结",2,b=>b.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked));
            int longFreeze=Apply(4055,"严冬易伤",Buff(5052,"严冬易伤",3,b=>{b.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked;b.damageTaken=.3f;}));
            int stun=Apply(4056,"短暂眩晕",Buff(5053,"短暂眩晕",1,b=>b.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked));
            int iceGround=Apply(4057,"冰面迟缓",Buff(5054,"冰面迟缓",1.1f,b=>b.moveSpeedPercent=-.5f));
            int bladeSlow=Apply(4058,"刃阵迟缓",Buff(5055,"刃阵迟缓",1.1f,b=>b.moveSpeedPercent=-.3f));
            Action<BuffDefinition> focus=b=>b.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.AttackSpeed,operation=ModifierOperation.Percent,value=.5f}};
            int focusEffect=Apply(4059,"冰霜专注状态",Buff(5056,"冰霜专注状态",6,focus));
            int splashFocus=Apply(4060,"霜爆专注状态",Buff(5057,"霜爆专注状态",6,b=>{focus(b);b.attackSplashRadius=2;b.attackSplashRatio=.4f;}));
            int pierceFocus=Apply(4061,"穿霜专注状态",Buff(5058,"穿霜专注状态",6,b=>{focus(b);b.pierceBasics=true;}));
            Action<BuffDefinition> warcry=b=>{b.damageReduction=.2f;b.regenMaxHealth=.02f;};
            int guard=Apply(4062,"磐石战歌状态",Buff(5059,"磐石战歌状态",5,warcry));
            int reflect=Apply(4063,"不屈反击状态",Buff(5060,"不屈反击状态",5,b=>{warcry(b);b.controlImmune=true;b.reflect=.3f;}));
            var archer=Find<HeroDefinition>(2000);var warrior=Find<HeroDefinition>(2002);
            archer.health=420;archer.healthPerLevel=36;archer.baseHealthRegen=1.5f;archer.regenPerLevel=.15f;
            archer.attackDamage=22;archer.attackPerLevel=2.5f;archer.attackSpeedPerLevel=3;archer.speed=7.5f;archer.range=18;
            archer.baseMana=300;archer.manaPerLevel=20;archer.baseManaRegen=8;archer.manaRegenPerLevel=.5f;
            archer.baseStats=Array.Empty<StatModifier>();archer.talents=Array.Empty<HeroTalentTier>();archer.turnRate=0;archer.attackWhileMoving=true;
            archer.basicAttack.range=archer.basicAttack.acquisitionRange=18;archer.basicAttack.baseAttackTime=.3f;archer.basicAttack.attackPoint=0;
            archer.basicAttack.backswing=0;archer.basicAttack.splitTargets=2;archer.basicAttack.splitDamageRatio=.5f;
            archer.basicAttack.description="自动攻击攻击距离内最近的敌人，并向另外两个目标发射 50% 伤害的分裂箭；移动不停止攻击。";
            EditorUtility.SetDirty(archer.basicAttack);
            warrior.health=650;warrior.healthPerLevel=60;warrior.baseHealthRegen=3;warrior.regenPerLevel=.3f;
            warrior.attackDamage=40;warrior.attackPerLevel=4;warrior.attackSpeedPerLevel=2;warrior.speed=7;warrior.range=2.8f;
            warrior.baseMana=240;warrior.manaPerLevel=14;warrior.baseManaRegen=6;warrior.manaRegenPerLevel=.4f;
            warrior.baseStats=new[]{new StatModifier(StatKind.DamageReduction,12)};warrior.talents=Array.Empty<HeroTalentTier>();warrior.turnRate=0;warrior.attackWhileMoving=true;
            warrior.basicAttack.range=warrior.basicAttack.acquisitionRange=2.8f;warrior.basicAttack.baseAttackTime=.75f;warrior.basicAttack.attackPoint=0;
            warrior.basicAttack.backswing=0;warrior.basicAttack.cleaveRadius=3.5f;warrior.basicAttack.cleaveHalfAngle=75;
            warrior.basicAttack.cleaveDamageRatio=.6f;warrior.basicAttack.cleaveTargets=4;
            warrior.basicAttack.description="攻击最近敌人，并对前方 150°、3.5 米内最多四个其他目标造成 60% 顺劈伤害。";
            EditorUtility.SetDirty(warrior.basicAttack);

            var q=Configure(1000,AimMode.Self,0,14,25,"提高攻击速度并增加分裂箭目标，持续 6 秒。",Node(10106,Use(focusEffect)));
            Levels(q,new[]{1f,1,1,1},new[]{14f,13,12,11},new[]{25f,30,35,40},buff:new[]{50f,70,90,110},extra:new[]{1,1,2,2});
            q.forms=new[]{Form(q,1100,"霜爆箭","专注期间，所有普攻命中造成 2 米范围、40% 攻击力的霜爆。",s=>s.recipe.nodes[0].effects=new[]{Use(splashFocus)}),
                Form(q,1101,"穿霜箭","专注期间，主箭和分裂箭沿发射方向穿透敌人，每箭对同一目标只命中一次。",s=>s.recipe.nodes[0].effects=new[]{Use(pierceFocus)})};
            var volley=Node(10101,Use(4050,40),Use(slow));volley.deliveryModuleId=10002;volley.projectileCount=5;volley.spreadDegrees=60;volley.range=22;volley.hitRadius=.55f;volley.speed=40;
            var e=Configure(1001,AimMode.Direction,22,7,30,"发射 5/7/9/11 支箭；每支箭碰到首个敌人消失并造成伤害和 35% 减速（2 秒）。",volley);
            Levels(e,new[]{40f,70,100,130},new[]{7f,6.5f,6,5.5f},new[]{30f,35,40,45},arrows:new[]{5,7,9,11},spread:new[]{60f,70,80,90});
            e.forms=new[]{Form(e,1102,"贯穿齐射","箭矢穿透整条路径；每支箭对同一敌人只造成一次伤害。",s=>s.recipe.nodes[0].pierce=true),
                Form(e,1103,"三重齐射","连续三轮齐射，后两轮造成 60% 伤害。",s=>
                {
                    var first=s.recipe.nodes[0];var second=JsonUtility.FromJson<RecipeNode>(JsonUtility.ToJson(first));second.nodeId=2;second.delay=.25f;
                    second.repeatCount=2;second.repeatInterval=.25f;second.effects=new[]{Use(4050,24),Use(slow)};s.recipe.nodes=new[]{first,second};
                })};
            var impact=Node(10103,Use(4050,80));impact.radius=4.5f;
            var rr=Configure(1002,AimMode.Point,18,0,35,"对鼠标落点造成范围冰霜伤害；储存两次充能。",impact);rr.maxCharges=2;rr.rechargeSeconds=10;
            Levels(rr,new[]{80f,140,200,260},new[]{10f,9,8,7},new[]{35f,40,45,50},new[]{4.5f,5,5.5f,6});
            rr.forms=new[]{Form(rr,1104,"连环冰爆","沿施法方向连续三次冰爆，每次间隔 0.2 秒并前进 4 米。",s=>{s.recipe.nodes[0].repeatCount=3;s.recipe.nodes[0].repeatInterval=.2f;s.recipe.nodes[0].repeatAdvance=4;}),
                Form(rr,1105,"永冻之地","冰爆后留下 4 秒冰面，每秒造成技能伤害的 30%，并减速 50%。",s=>
                {
                    var zone=Node(10103,Use(4050,24),Use(iceGround));zone.nodeId=2;zone.radius=4.5f;zone.delay=1;zone.repeatCount=4;zone.repeatInterval=1;s.recipe.nodes=new[]{s.recipe.nodes[0],zone};
                })};
            var arrow=Node(10103,Use(4050,200));arrow.deliveryModuleId=10002;arrow.range=60;arrow.radius=8;arrow.speed=32;arrow.hitRadius=.9f;arrow.impactEffects=new[]{Use(4050,300),Use(freeze)};
            var f=Configure(1003,AimMode.Direction,60,45,100,"巨箭首碰造成 300/450/600 伤害与 2 秒冻结，并爆炸造成 8 米范围伤害。",arrow);
            Levels(f,new[]{300f,450,600},new[]{45f,40,35},new[]{100f,120,140});
            f.forms=new[]{Form(f,1106,"冰晶迸裂","巨箭爆炸后向四周发射十二支寒箭。",s=>
                {var burst=Node(10101,Use(4050,60),Use(slow));burst.nodeId=2;burst.parentNodeId=1;burst.trigger=RecipeTrigger.OnResolved;burst.deliveryModuleId=10002;burst.projectileCount=12;burst.spreadDegrees=360;burst.range=16;burst.speed=32;s.recipe.nodes=new[]{s.recipe.nodes[0],burst};}),
                Form(f,1107,"极寒禁锢","主目标冻结延长到 3 秒，并在期间额外承受 30% 伤害。",s=>s.recipe.nodes[0].impactEffects=new[]{Use(4050,300),Use(longFreeze)})};

            var cleave=Node(10104,Use(4050,90));cleave.range=6;cleave.halfAngleDegrees=60;
            var wq=Configure(1008,AimMode.Direction,10,0,0,"被动：每第三次主普攻命中，向前方 120°、6 米范围释放重劈。",cleave);wq.autoTriggerEveryHits=3;
            Levels(wq,new[]{90f,150,210,270},new[]{0f,0,0,0},new[]{0f,0,0,0});
            wq.forms=new[]{Form(wq,1108,"裂地剑波","每第三次普攻释放一道 10 米长、4 米宽的剑波。",s=>{s.recipe.nodes[0].queryModuleId=10105;s.recipe.nodes[0].range=10;s.recipe.nodes[0].radius=2;}),
                Form(wq,1109,"回身重斩","每第三次普攻重斩周围 5 米内所有敌人。",s=>{s.recipe.nodes[0].queryModuleId=10103;s.recipe.nodes[0].radius=5;})};
            var gathering=Node(10103,Use(4052));gathering.radius=7;
            var protection=Node(10106,Use(guard));
            var we=Configure(1009,AimMode.Self,0,16,50,"聚拢周围敌人；获得减伤和每秒最大生命回复，持续 5 秒。",gathering,protection);
            Levels(we,new[]{1f,1,1,1},new[]{16f,15,14,13},new[]{50f,50,50,50},new[]{7f,8,9,10},new[]{20f,25,30,35});
            we.forms=new[]{Form(we,1110,"不屈反击","战歌期间免疫控制，并反射 30% 实际近战伤害。",s=>s.recipe.nodes[1].effects=new[]{Use(reflect)}),
                Form(we,1111,"震地战歌","聚拢后造成 5 米震击：200% 攻击伤害与 1 秒眩晕。",s=>
                {var shock=Node(10103,Use(4051),Use(stun));shock.nodeId=3;shock.delay=.1f;shock.radius=3.5f;s.recipe.nodes=new[]{s.recipe.nodes[0],s.recipe.nodes[1],shock};})};
            var field=Node(10103,Use(4050,25));field.radius=4.5f;field.repeatCount=6;field.repeatInterval=.5f;
            var wr=Configure(1010,AimMode.Self,0,11,45,"在原地留下持续 3 秒的刃阵，每 0.5 秒造成一次范围伤害。",field);
            Levels(wr,new[]{25f,40,55,70},new[]{11f,10,9,8},new[]{45f,50,55,60},new[]{4.5f,5,5.5f,6});
            wr.forms=new[]{Form(wr,1112,"随行刃阵","刃阵跟随卫士移动，持续时间延长至 5 秒。",s=>{s.recipe.nodes[0].followOwner=true;s.recipe.nodes[0].repeatCount=10;}),
                Form(wr,1113,"双环刃阵","额外生成半径 6–9 米的外环刃阵。",s=>
                {var ring=Node(10112,Use(4050,25));ring.nodeId=2;ring.radius=6.75f;ring.innerRadius=4.5f;ring.repeatCount=6;ring.repeatInterval=.5f;s.recipe.nodes=new[]{s.recipe.nodes[0],ring};})};
            var blade=Node(10103,Use(4050,300),Use(stun));blade.delay=.65f;blade.radius=6;
            var wf=Configure(1011,AimMode.Point,12,40,100,"标记落点，0.65 秒后重剑砸落，造成 6 米范围伤害并眩晕 1 秒。",blade);
            Levels(wf,new[]{300f,450,600},new[]{40f,35,30},new[]{100f,120,140});
            wf.forms=new[]{Form(wf,1114,"群峰落刃","主剑落地后，在附近密集敌群处追加两把 50% 伤害的小剑。",s=>
                {var small=Node(10103,Use(4050,150),Use(stun));small.nodeId=2;small.delay=.95f;small.repeatCount=2;small.repeatInterval=.3f;small.radius=3;small.range=10;small.seekDense=true;s.recipe.nodes=new[]{s.recipe.nodes[0],small};}),
                Form(wf,1115,"裂地余威","落点留下 6 秒领域，每秒造成技能伤害的 30%，并减速 30%。",s=>
                {var zone=Node(10103,Use(4050,90),Use(bladeSlow));zone.nodeId=2;zone.delay=1.65f;zone.repeatCount=6;zone.repeatInterval=1;zone.radius=6;s.recipe.nodes=new[]{s.recipe.nodes[0],zone};})};
            archer.skills=new[]{q,e,rr,f};warrior.skills=new[]{wq,we,wr,wf};
            var queries=modules.queries.ToList();
            if(!queries.Any(x=>x.id==10112)) queries.Add(new QueryModule{id=10112,displayName="敌方圆环",playerAllowed=true,scope=TargetScope.Area,shape=AreaShape.Ring,targets=TargetRelation.Enemies,maxRadius=40,maxRange=150});
            modules.queries=queries.ToArray();
            modules.buffs=modules.buffs.Concat(buffs).Distinct().ToArray();
            var all=modules.effects.Where(x=>x!=null && x.definition && !effects.Contains(x.definition)).ToList();
            all.AddRange(effects.Select(x=>new EffectModule{definition=x,playerAllowed=true,allowAmountOverride=x is DamageEffect || x is HealEffect,maxAmount=100000}));
            modules.effects=all.ToArray();
            var catalog=Asset<SkillCatalog>("目录/游戏技能库.asset");catalog.buffs=modules.buffs;catalog.skills=new[]{q,e,rr,f,wq,we,wr,wf};
            var growth=Asset<HeroProgressionRules>("成长/经验与等级规则.asset");growth.maxLevel=15;growth.experienceToNextLevel=HeroProgressionRules.ExpeditionExperienceTable();growth.startingSkillPoints=1;growth.skillPointsPerLevel=1;
            Asset<CombatInputProfile>("操作/战斗键位.asset").Validate();
            AssetDatabase.SaveAssets();NumericConfigIds.AssignMissing();
            using(var library=catalog.Compile()) { }
            foreach(var skill in catalog.skills) foreach(var form in skill.forms) SkillRecipeCompiler.Validate(form);
            Debug.Log("EXPEDITION_HERO_CONTENT_READY heroes=2 skills=8 forms=16");
        }
    }
}
