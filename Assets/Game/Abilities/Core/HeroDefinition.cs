using System;
using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>英雄模板通过技能引用组装 loadout；输入按槽位绑定，技能定义不识别 Q/W/E/R。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/英雄定义")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [Tooltip("数字配置 ID：英雄使用 2000–2999；由编辑器编号登记分配，0 仅表示尚未分配的临时定义。")]
        public int configId;
        [InspectorName("稳定 ID")] public string stableId;
        [InspectorName("英雄名称")] public string displayName;
        [InspectorName("技能槽（按顺序）")] public SkillDefinition[] skills=Array.Empty<SkillDefinition>();
        [InspectorName("基础生命"), Min(1)] public float health=300;
        [InspectorName("移动速度（米/秒）"), Min(0)] public float speed=8;
        [InspectorName("普攻伤害"), Min(0)] public float attackDamage=18;
        [InspectorName("普攻间隔（秒）"), Min(0.01f)] public float attackInterval=0.65f;
        [InspectorName("普攻距离（米）"), Min(0)] public float range=16;
        [Tooltip("自动攻击被动模块：攻击距离、索敌距离、基础攻击间隔、前后摇与弹道。为空时用上方旧普攻间隔/距离生成无前后摇的弹道攻击。")]
        public AutoAttackDefinition basicAttack;
        [Min(0), Tooltip("转身速度（度/秒）：移动和开始攻击前需要先转向目标；0 表示瞬间转身。")] public float turnRate=1080;
        [Tooltip("移动中攻击：右键移动途中自动攻击攻击距离内的敌人且不停下，攻击前摇不定身，新的移动指令不打断攻击。关闭后恢复 Dota 2 式“移动取消攻击”。重开生效。")]
        public bool attackWhileMoving=true;
        [Tooltip("HUD 头像。为空时显示纯色底。")] public Texture2D portrait;
        [Tooltip("英雄展示预制体；只负责外形，战斗以 ECS 数据为准。为空沿用场景模型。")] public GameObject visualPrefab;
        [Min(0), Tooltip("开局最大法力；技能施放按技能逐级表消耗法力，局内由单位状态统一扣除。")] public float baseMana=100;
        [Min(0), Tooltip("开局每秒生命回复。")] public float baseHealthRegen=1.5f;
        [Min(0), Tooltip("开局法力回复（点/秒）；重开生效，由共享单位状态结算。")] public float baseManaRegen=3;
        [Min(0), Tooltip("每级增加的最大生命，当前生命同步增加。")] public float healthPerLevel=22;
        [Min(0), Tooltip("每级增加的最大法力。")] public float manaPerLevel=12;
        [Min(0), Tooltip("每级增加的普攻伤害。")] public float attackPerLevel=3;
        [Min(0), Tooltip("每级增加的攻击速度（百分点）：2.5 表示每级 +2.5%。")] public float attackSpeedPerLevel=2.5f;
        [Min(0), Tooltip("每级增加的移动速度（米/秒）。")] public float moveSpeedPerLevel;
        [Min(0), Tooltip("每级增加的每秒生命回复。")] public float regenPerLevel=0.1f;
        [Min(0), Tooltip("每升一级增加的法力回复（点/秒），与天赋和装备直接相加。")] public float manaRegenPerLevel;
        [Tooltip("按英雄等级升序排列的天赋层；每层二选一，不消耗技能点。最多 31 层。")]
        public HeroTalentTier[] talents=HeroTalentTier.Defaults();
        [Tooltip("直接加到英雄基础属性上的数值，例如 +10 暴击几率、+5 普攻吸血；与上方基础生命、普攻伤害等字段相加。最多 16 条。")]
        public StatModifier[] baseStats=Array.Empty<StatModifier>();

        /// <summary>英雄基础属性：上方基础字段 + 额外基础属性 + 默认暴击伤害 150%。法力回复由英雄配置给出。只读，不修改资源。</summary>
        public StatSheet BaseSheet(in AutoAttackData attack)
        {
            var sheet=new StatSheet {MaxHealth=health,AttackDamage=attackDamage,MoveSpeed=speed,HealthRegen=baseHealthRegen,
                MaxMana=baseMana,ManaRegen=baseManaRegen,AttackRange=attack.Range,CriticalDamage=HeroStatsLogic.DefaultCriticalDamage};
            if(baseStats!=null) foreach(var modifier in baseStats) sheet.Add(modifier.stat,modifier.value);
            return sheet;
        }
        /// <summary>每升 1 级直接相加的属性成长。</summary>
        public StatSheet GrowthSheet()=>new StatSheet {MaxHealth=healthPerLevel,MaxMana=manaPerLevel,AttackDamage=attackPerLevel,
            AttackSpeed=attackSpeedPerLevel,MoveSpeed=moveSpeedPerLevel,HealthRegen=regenPerLevel,ManaRegen=manaRegenPerLevel};

        /// <summary>验证英雄与给定库的引用。返回槽位到库索引的映射，不修改共享资源。</summary>
        public int[] ResolveSkills(SkillCatalog catalog)
        {
            if(configId!=0 && (configId<2000 || configId>2999))
                throw new InvalidOperationException("英雄数字 ID 必须为 2000–2999："+name);
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId)
                throw new InvalidOperationException("英雄稳定 ID 不能为空或带首尾空格："+name);
            if(!catalog || catalog.skills==null || skills==null || skills.Length==0 || skills.Length>32)
                throw new InvalidOperationException("英雄技能槽或技能库无效："+name);
            if(!Finite(health) || health<=0 || !Finite(speed) || speed<0 || !Finite(attackDamage) || attackDamage<0 ||
                !Finite(attackInterval) || attackInterval<=0 || !Finite(range) || range<0)
                throw new InvalidOperationException("英雄基础属性必须有限且在合法范围："+name);
            var result=new int[skills.Length];
            for(int i=0;i<skills.Length;i++)
            {
                if(!skills[i] || (result[i]=Array.IndexOf(catalog.skills,skills[i]))<0)
                    throw new InvalidOperationException("英雄技能未登记在库中："+name+" / 槽 "+i);
            }
            return result;
        }
        /// <summary>生成英雄的自动攻击快照：优先使用自动攻击模块，否则沿用旧普攻间隔与距离。</summary>
        public AutoAttackData ResolveAttack(float fallbackProjectileSpeed)
        {
            if(!Finite(turnRate) || turnRate<0) throw new InvalidOperationException("英雄转身速度必须是有限的非负数："+name);
            if(basicAttack) return basicAttack.Compile();
            return new AutoAttackData {Delivery=AttackDelivery.Projectile,Range=range,AcquisitionRange=range,BaseAttackTime=attackInterval,
                ProjectileSpeed=Mathf.Max(1,fallbackProjectileSpeed),RangeBuffer=1.5f,AutoAcquire=true,CountsAsBasicAttackHit=true};
        }
        /// <summary>验证成长与天赋配置；只读，不修改共享资源。</summary>
        public void ValidateProgression()
        {
            float[] values={baseMana,baseHealthRegen,baseManaRegen,healthPerLevel,manaPerLevel,attackPerLevel,attackSpeedPerLevel,moveSpeedPerLevel,regenPerLevel,manaRegenPerLevel};
            foreach(float value in values)
                if(!Finite(value) || value<0) throw new InvalidOperationException("英雄成长数值必须是有限的非负数："+name);
            if(baseStats!=null)
            {
                if(baseStats.Length>16) throw new InvalidOperationException("额外基础属性最多 16 条："+name);
                foreach(var modifier in baseStats)
                    if(!Enum.IsDefined(typeof(StatKind),modifier.stat) || !Finite(modifier.value))
                        throw new InvalidOperationException("额外基础属性未知或数值非法："+name);
            }
            var tiers=talents??Array.Empty<HeroTalentTier>();
            if(tiers.Length>31) throw new InvalidOperationException("天赋最多 31 层："+name);
            for(int i=0;i<tiers.Length;i++)
            {
                var tier=tiers[i];
                if(tier==null) throw new InvalidOperationException("天赋层为空："+name+" / 第 "+(i+1)+" 层");
                if(tier.heroLevel<1 || (i>0 && tiers[i-1]!=null && tier.heroLevel<tiers[i-1].heroLevel))
                    throw new InvalidOperationException("天赋层必须按英雄等级升序排列且不小于 1："+name+" / 第 "+(i+1)+" 层");
                Talent(tier.left,i); Talent(tier.right,i);
            }
        }
        private void Talent(TalentOption option,int tier)
        {
            if(!Enum.IsDefined(typeof(StatKind),option.stat) || !Finite(option.value))
                throw new InvalidOperationException("天赋属性未知或数值非法："+name+" / 第 "+(tier+1)+" 层");
            if(option.stat==StatKind.CooldownReduction && (option.value<0 || option.value>HeroStatsLogic.MaximumCooldownReduction))
                throw new InvalidOperationException("冷却缩减天赋必须在 0 到 80（百分点）之间："+name+" / 第 "+(tier+1)+" 层");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
