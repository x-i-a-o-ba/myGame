using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>一组英雄属性数值（字段与 <see cref="StatKind"/> 一一对应）。所有来源直接相加；百分比类属性为百分点（25 = 25%）。</summary>
    public struct StatSheet
    {
        public const int Count=15;
        public float MaxHealth,AttackDamage,AttackSpeed,MoveSpeed,HealthRegen,SkillAmplify,CooldownReduction,ExperienceGain,
            MaxMana,ManaRegen,AttackRange,DamageReduction,LifeSteal,CriticalChance,CriticalDamage;

        public float this[StatKind stat]
        {
            get
            {
                switch(stat)
                {
                    case StatKind.MaxHealth: return MaxHealth;
                    case StatKind.AttackDamage: return AttackDamage;
                    case StatKind.AttackSpeed: return AttackSpeed;
                    case StatKind.MoveSpeed: return MoveSpeed;
                    case StatKind.HealthRegen: return HealthRegen;
                    case StatKind.SkillAmplify: return SkillAmplify;
                    case StatKind.CooldownReduction: return CooldownReduction;
                    case StatKind.ExperienceGain: return ExperienceGain;
                    case StatKind.MaxMana: return MaxMana;
                    case StatKind.ManaRegen: return ManaRegen;
                    case StatKind.AttackRange: return AttackRange;
                    case StatKind.DamageReduction: return DamageReduction;
                    case StatKind.LifeSteal: return LifeSteal;
                    case StatKind.CriticalChance: return CriticalChance;
                    case StatKind.CriticalDamage: return CriticalDamage;
                }
                return 0;
            }
            set
            {
                switch(stat)
                {
                    case StatKind.MaxHealth: MaxHealth=value; break;
                    case StatKind.AttackDamage: AttackDamage=value; break;
                    case StatKind.AttackSpeed: AttackSpeed=value; break;
                    case StatKind.MoveSpeed: MoveSpeed=value; break;
                    case StatKind.HealthRegen: HealthRegen=value; break;
                    case StatKind.SkillAmplify: SkillAmplify=value; break;
                    case StatKind.CooldownReduction: CooldownReduction=value; break;
                    case StatKind.ExperienceGain: ExperienceGain=value; break;
                    case StatKind.MaxMana: MaxMana=value; break;
                    case StatKind.ManaRegen: ManaRegen=value; break;
                    case StatKind.AttackRange: AttackRange=value; break;
                    case StatKind.DamageReduction: DamageReduction=value; break;
                    case StatKind.LifeSteal: LifeSteal=value; break;
                    case StatKind.CriticalChance: CriticalChance=value; break;
                    case StatKind.CriticalDamage: CriticalDamage=value; break;
                }
            }
        }

        /// <summary>把一条加成直接加到对应属性上；未知属性被忽略。</summary>
        public void Add(StatKind stat,float value) { this[stat]=this[stat]+value; }

        public static StatSheet operator+(StatSheet a,StatSheet b)
        {
            for(int i=0;i<Count;i++) a[(StatKind)i]+=b[(StatKind)i];
            return a;
        }
        public static StatSheet operator*(StatSheet a,float scale)
        {
            for(int i=0;i<Count;i++) a[(StatKind)i]*=scale;
            return a;
        }
    }

    /// <summary>英雄属性的来源拆分与合计，归英雄实体所有。等级、天赋或装备变化后由 <see cref="HeroStatsLogic.Rebuild"/> 统一重算。</summary>
    public struct HeroStats : IComponentData
    {
        /// <summary>英雄定义的基础属性（基础生命、普攻伤害等字段 + 额外基础属性 + 默认暴击伤害）。</summary>
        public StatSheet Base;
        /// <summary>每升 1 级的成长；等级贡献 = 成长 ×（等级 - 1）。</summary>
        public StatSheet PerLevel;
        /// <summary>最近一次重算时的天赋贡献、装备贡献与合计；HUD 与结算读取同一份合计。</summary>
        public StatSheet Talents,Equipment,Total;
        /// <summary>自动攻击模块的索敌距离；攻击距离加成同时加到攻击距离和索敌距离上。</summary>
        public float BaseAcquisitionRange;
        /// <summary>每次重算递增，展示层据此判断属性是否变化。</summary>
        public int Version;
    }

    /// <summary>
    /// 英雄属性的唯一合成规则：合计 = 基础 + 成长 ×（等级 - 1）+ 天赋 + 装备，全部直接相加。
    /// 合计写入 <see cref="AbilityUnit"/> 的基础值、经验倍率与自动攻击距离；Buff 仍由 <see cref="AbilityEffects.Recalculate"/> 在其上叠加。
    /// </summary>
    public static class HeroStatsLogic
    {
        /// <summary>英雄默认暴击伤害（%）：暴击时普攻造成 150% 伤害。</summary>
        public const float DefaultCriticalDamage=150;
        /// <summary>冷却缩减与伤害减免的上限（%）。</summary>
        public const float MaximumCooldownReduction=80,MaximumDamageReduction=80;
        /// <summary>攻击速度下限（%）：负攻速最多让攻击变慢到 20%。</summary>
        public const float MinimumAttackSpeed=-80;

        public static HeroStats Create(in StatSheet baseSheet,in StatSheet perLevel,float acquisitionRange)
            =>new HeroStats {Base=baseSheet,PerLevel=perLevel,BaseAcquisitionRange=math.max(0,acquisitionRange)};

        /// <summary>按天赋选择掩码累加已选天赋的属性。</summary>
        public static StatSheet Talents(in HeroProgression progression)
        {
            var sheet=default(StatSheet);
            if(!progression.Library.IsCreated) return sheet;
            ref var tiers=ref progression.Library.Value.Talents;
            for(int i=0;i<tiers.Length && i<HeroProgressionLogic.MaximumTalentTiers;i++)
            {
                if((progression.TalentChosenMask&(1<<i))==0) continue;
                var talent=(progression.TalentRightMask&(1<<i))!=0?tiers[i].Right:tiers[i].Left;
                sheet.Add(talent.Stat,talent.Value);
            }
            return sheet;
        }

        /// <summary>合计 = 基础 + 成长 ×（等级 - 1）+ 天赋 + 装备。</summary>
        public static StatSheet Compose(in HeroStats stats,int level)
            =>stats.Base+stats.PerLevel*math.max(0,level-1)+stats.Talents+stats.Equipment;

        /// <summary>
        /// 重算天赋与装备贡献及合计，并写入单位基础值、经验倍率与自动攻击距离。
        /// 调用方随后调用 <see cref="AbilityEffects.Recalculate"/> 叠加 Buff。
        /// </summary>
        public static void Rebuild(ref HeroStats stats,in StatSheet equipment,ref HeroProgression progression,ref AbilityUnit unit,ref AutoAttackData attack)
        {
            stats.Talents=Talents(progression); stats.Equipment=equipment;
            stats.Total=Compose(stats,progression.Level);
            Apply(stats.Total,ref unit);
            progression.ExperienceMultiplier=math.max(0,1+stats.Total.ExperienceGain/100);
            float bonusRange=stats.Total.AttackRange-stats.Base.AttackRange;
            attack.Range=math.max(0,stats.Total.AttackRange);
            attack.AcquisitionRange=math.max(0,stats.BaseAcquisitionRange+bonusRange);
            stats.Version++;
        }

        /// <summary>
        /// 把属性合计写入单位基础值。最大生命 / 法力增加时当前值同步增加（升级、购买装备不损失已有生命）；
        /// 减少时当前值只截到新上限，存活单位不会因此死亡。百分点在这里换成执行器使用的小数。
        /// </summary>
        public static void Apply(in StatSheet total,ref AbilityUnit unit)
        {
            float maxHealth=math.max(1,total.MaxHealth);
            if(unit.Health>0) unit.Health=math.clamp(unit.Health+math.max(0,maxHealth-unit.MaxHealth),1,maxHealth);
            unit.MaxHealth=maxHealth;
            float maxMana=math.max(0,total.MaxMana);
            unit.Mana=math.clamp(unit.Mana+math.max(0,maxMana-unit.MaxMana),0,maxMana);
            unit.MaxMana=maxMana;
            unit.BaseAttack=math.max(0,total.AttackDamage);
            unit.AttackSpeedBonus=math.max(MinimumAttackSpeed,total.AttackSpeed)/100;
            unit.BaseSpeed=math.max(0,total.MoveSpeed);
            unit.BaseRegen=total.HealthRegen;
            unit.ManaRegenBonus=total.ManaRegen-AbilityEffects.BaseManaRegen;
            unit.SkillAmplify=math.max(0,total.SkillAmplify)/100;
            unit.CooldownReduction=math.clamp(total.CooldownReduction,0,MaximumCooldownReduction)/100;
            unit.DamageReduction=math.clamp(total.DamageReduction,0,MaximumDamageReduction)/100;
        }

        /// <summary>暴击判定：用会话种子与出手序号做确定性哈希，同一局同一次攻击结果固定。返回伤害倍率（未暴击为 1）。</summary>
        public static float CriticalMultiplier(in StatSheet total,uint seed,int attackIndex,out bool critical)
        {
            float chance=math.clamp(total.CriticalChance,0,100)/100;
            critical=chance>0 && (Mix(seed,(uint)attackIndex)>>8)/(float)0x1000000<chance;
            return critical?math.max(1,total.CriticalDamage/100):1;
        }

        /// <summary>
        /// 整数混合哈希（lowbias32 终混）：相邻出手序号的结果互不相关。math.hash(uint2) 是线性组合，
        /// 连续序号会形成固定步长的序列（例如 25% 暴击永远不会连续两次暴击），因此这里不用它。
        /// </summary>
        private static uint Mix(uint seed,uint index)
        {
            uint h=(seed|1u)*0x9E3779B9u+index*0x85EBCA6Bu+0x27D4EB2Fu;
            h^=h>>16; h*=0x7FEB352Du; h^=h>>15; h*=0x846CA68Bu; h^=h>>16;
            return h;
        }

        /// <summary>普攻吸血：按实际造成的伤害回复生命。</summary>
        public static float LifeStealHeal(in StatSheet total,float actualDamage)
            =>math.max(0,actualDamage)*math.max(0,total.LifeSteal)/100;
    }
}
