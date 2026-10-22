using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    public struct TalentData { public StatKind Stat; public float Value; }
    public struct TalentTierData { public int HeroLevel; public TalentData Left,Right; }

    /// <summary>会话装配时编译的只读成长表：累计经验、每级属性成长与天赋层。由会话拥有并释放。</summary>
    public struct ProgressionLibrary
    {
        public int MaxLevel,SkillPointsPerLevel;
        /// <summary>索引 level-1 为到达该等级所需累计经验。</summary>
        public BlobArray<int> CumulativeExperience;
        /// <summary>每升 1 级直接相加的属性成长（攻击速度为百分点）。</summary>
        public StatSheet Growth;
        public BlobArray<TalentTierData> Talents;
    }

    /// <summary>英雄局内成长的运行状态，归英雄实体所有。等级与天赋带来的属性由 HeroStatsLogic.Rebuild 统一写入 AbilityUnit。</summary>
    public struct HeroProgression : IComponentData
    {
        public BlobAssetReference<ProgressionLibrary> Library;
        public int Level,SkillPoints;
        /// <summary>本局累计经验（已乘经验倍率）。</summary>
        public float Experience;
        /// <summary>经验倍率 = 1 + 经验获取 / 100，由属性重算写入。</summary>
        public float ExperienceMultiplier;
        /// <summary>第 i 位表示第 i 层天赋已选择；TalentRightMask 第 i 位表示选择了右侧。</summary>
        public int TalentChosenMask,TalentRightMask;
        public int LevelUpSerial,CommandSerial;
        public ProgressionFailure LastFailure;
    }

    public enum ProgressionCommandKind { LearnSkill,ChooseTalent }
    /// <summary>输入层提交的成长意图，由战斗宿主在模拟帧内串行处理。</summary>
    [InternalBufferCapacity(4)]
    public struct ProgressionCommand : IBufferElementData
    {
        public ProgressionCommandKind Kind;
        /// <summary>学习技能时为槽位；选择天赋时为 0（左）或 1（右）。</summary>
        public int Value;
    }
    public enum ProgressionFailure { None,Dead,InvalidSlot,NoSkillPoints,HeroLevelTooLow,SkillMaxed,NoPendingTalent,InvalidTalent }

    /// <summary>经验、升级、学习技能与天赋的共同规则。不识别英雄名、技能名或 QWER。</summary>
    public static class HeroProgressionLogic
    {
        public const int MaximumTalentTiers=31;

        /// <summary>校验并编译成长表；调用方唯一拥有返回值。</summary>
        public static BlobAssetReference<ProgressionLibrary> Compile(HeroProgressionRules rules,HeroDefinition hero)
        {
            if(!rules) throw new InvalidOperationException("缺少经验与等级规则。");
            if(!hero) throw new InvalidOperationException("缺少英雄定义。");
            var cumulative=rules.CumulativeExperience();
            hero.ValidateProgression();
            var talents=hero.talents??Array.Empty<HeroTalentTier>();
            using(var builder=new BlobBuilder(Allocator.Temp))
            {
                ref var root=ref builder.ConstructRoot<ProgressionLibrary>();
                root.MaxLevel=rules.maxLevel; root.SkillPointsPerLevel=rules.skillPointsPerLevel;
                root.Growth=hero.GrowthSheet();
                var experience=builder.Allocate(ref root.CumulativeExperience,cumulative.Length);
                for(int i=0;i<cumulative.Length;i++) experience[i]=cumulative[i];
                var tiers=builder.Allocate(ref root.Talents,talents.Length);
                for(int i=0;i<talents.Length;i++)
                    tiers[i]=new TalentTierData {HeroLevel=talents[i].heroLevel,
                        Left=new TalentData{Stat=talents[i].left.stat,Value=talents[i].left.value},
                        Right=new TalentData{Stat=talents[i].right.stat,Value=talents[i].right.value}};
                return builder.CreateBlobAssetReference<ProgressionLibrary>(Allocator.Persistent);
            }
        }

        public static HeroProgression Create(BlobAssetReference<ProgressionLibrary> library,int startingSkillPoints)
            =>new HeroProgression {Library=library,Level=1,SkillPoints=math.max(0,startingSkillPoints),ExperienceMultiplier=1};

        /// <summary>按经验倍率累计经验；非有限或非正数被忽略。升级在 ApplyPendingLevels 中统一结算。</summary>
        public static void GainExperience(ref HeroProgression progression,float amount)
        {
            if(!(amount>0) || float.IsInfinity(amount)) return;
            progression.Experience+=amount*math.max(0,progression.ExperienceMultiplier);
        }

        /// <summary>
        /// 把累计经验兑换成等级：每级加技能点。返回本次升级数；大于 0 时调用方用 HeroStatsLogic.Rebuild 重算属性
        /// （成长 ×（等级 - 1）直接相加，最大生命增加时当前生命同步增加），再重算 Buff 聚合属性。
        /// </summary>
        public static int ApplyPendingLevels(ref HeroProgression progression)
        {
            if(!progression.Library.IsCreated) return 0;
            ref var library=ref progression.Library.Value;
            int gained=0;
            while(progression.Level<library.MaxLevel && progression.Experience>=library.CumulativeExperience[progression.Level])
            {
                progression.Level++; progression.SkillPoints+=library.SkillPointsPerLevel; gained++;
            }
            progression.LevelUpSerial+=gained;
            return gained;
        }

        /// <summary>当前等级的经验进度：已获得/本级所需；满级返回 (0,0)。</summary>
        public static float2 LevelProgress(in HeroProgression progression)
        {
            if(!progression.Library.IsCreated) return float2.zero;
            ref var library=ref progression.Library.Value;
            if(progression.Level>=library.MaxLevel) return float2.zero;
            float start=library.CumulativeExperience[progression.Level-1];
            float end=library.CumulativeExperience[progression.Level];
            return new float2(math.max(0,progression.Experience-start),end-start);
        }

        public static ProgressionFailure CanLearn(in HeroProgression progression,ref SkillData skill,int currentLevel)
        {
            if(currentLevel>=SkillLevels.MaxLevel(ref skill)) return ProgressionFailure.SkillMaxed;
            if(progression.SkillPoints<=0) return ProgressionFailure.NoSkillPoints;
            if(progression.Level<SkillLevels.RequiredHeroLevel(ref skill,currentLevel+1)) return ProgressionFailure.HeroLevelTooLow;
            return ProgressionFailure.None;
        }

        /// <summary>学习或升级一个技能槽。成功时只改槽等级与技能点；冷却、充能等运行状态保持不变。</summary>
        public static bool TryLearn(ref HeroProgression progression,DynamicBuffer<AbilitySlot> slots,int slot,ref AbilityLibrary library)
        {
            if(slot<0 || slot>=slots.Length) { progression.LastFailure=ProgressionFailure.InvalidSlot; return false; }
            var value=slots[slot];
            var failure=CanLearn(progression,ref library.Skills[value.Definition],value.Level);
            progression.LastFailure=failure;
            if(failure!=ProgressionFailure.None) return false;
            value.Level++; slots[slot]=value; progression.SkillPoints--;
            return true;
        }

        /// <summary>返回最早一层已解锁但尚未选择的天赋；没有则返回 -1。</summary>
        public static int PendingTalent(in HeroProgression progression)
        {
            if(!progression.Library.IsCreated) return -1;
            ref var talents=ref progression.Library.Value.Talents;
            for(int i=0;i<talents.Length && i<MaximumTalentTiers;i++)
                if(progression.Level>=talents[i].HeroLevel && (progression.TalentChosenMask&(1<<i))==0) return i;
            return -1;
        }

        /// <summary>
        /// 选择当前待选天赋层的左（0）或右（1）选项，只记录选择掩码；成功后调用方用 HeroStatsLogic.Rebuild 重算属性。
        /// 天赋属性与装备、等级成长一样直接相加（冷却缩减也直接相加，上限 80%）。
        /// </summary>
        public static bool TryChooseTalent(ref HeroProgression progression,int option)
        {
            int tier=PendingTalent(progression);
            if(tier<0) { progression.LastFailure=ProgressionFailure.NoPendingTalent; return false; }
            if(option!=0 && option!=1) { progression.LastFailure=ProgressionFailure.InvalidTalent; return false; }
            progression.TalentChosenMask|=1<<tier;
            if(option==1) progression.TalentRightMask|=1<<tier;
            progression.LastFailure=ProgressionFailure.None;
            return true;
        }

        public static string Describe(ProgressionFailure failure)
        {
            switch(failure)
            {
                case ProgressionFailure.None: return "";
                case ProgressionFailure.Dead: return "英雄已倒下";
                case ProgressionFailure.InvalidSlot: return "技能槽不存在";
                case ProgressionFailure.NoSkillPoints: return "没有可用技能点";
                case ProgressionFailure.HeroLevelTooLow: return "英雄等级不足";
                case ProgressionFailure.SkillMaxed: return "技能已满级";
                case ProgressionFailure.NoPendingTalent: return "没有待选天赋";
                case ProgressionFailure.InvalidTalent: return "天赋选项无效";
            }
            return failure.ToString();
        }
    }
}
