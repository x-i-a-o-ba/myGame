using System;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Combat
{
    public enum MonsterAbilityTemplate
    {
        [InspectorName("预警范围攻击")] TelegraphArea,
        [InspectorName("直线冲锋或跳跃")] Dash,
        [InspectorName("弹幕投射")] ProjectileBarrage,
        [InspectorName("召唤怪物")] Summon,
        [InspectorName("持续光环")] Aura,
        [InspectorName("死亡触发")] OnDeath,
        [InspectorName("地面持续区域")] GroundZone
    }
    public enum MonsterDeathAction
    {
        [InspectorName("范围爆炸")] Explosion,
        [InspectorName("留下持续区域")] GroundZone,
        [InspectorName("分裂子怪")] Split,
        [InspectorName("引爆同类")] Chain
    }
    /// <summary>七类敌人宿主模块。只配置 AI 触发、预警和投递；命中查询及效果复用技能核心。</summary>
    [CreateAssetMenu(menuName="Roguelike/怪物/技能模板")]
    public sealed class MonsterAbilityDefinition : ScriptableObject
    {
        public string stableId,displayName;
        public MonsterAbilityTemplate template;
        public MonsterDeathAction deathAction;
        public AreaShape shape;
        [Min(0)] public float cooldown=6,warningSeconds=.6f;
        [Min(.1f)] public float triggerRange=12,radius=3,range=10;
        [Range(.1f,180)] public float halfAngle=60;
        [Min(0)] public float innerRadius;
        [Range(1,3)] public int minimumStage=1;
        [Tooltip("区域锁定英雄施法时的落点；关闭时以怪物起点为中心。")] public bool atHero;
        [Tooltip("执行后自身死亡，用于有预警的自爆。")] public bool sacrifice;
        [Min(0),Tooltip("伤害 = 施法时攻击力 × 倍率。")] public float damageRatio=1;
        [Range(0,1)] public float healMaxHealth;
        public ApplyBuffEffect status;
        [Min(0),Tooltip("命中时击退距离（米）。")] public float knockback;
        [Min(.05f)] public float duration=3,interval=1;
        [Min(.1f)] public float speed=14;
        [Range(1,32)] public int projectileCount=1;
        [Range(0,360)] public float spreadDegrees;
        [Tooltip("弹体在锁定落点爆炸，关闭时沿直线首碰。")] public bool projectileAtPoint;
        [Min(0),Tooltip("弹体落地或冲锋经过时留下持续区域的秒数，0 关闭。")] public float trailSeconds;
        [Min(0)] public float trailDamageRatio=.5f;
        [Tooltip("召唤/分裂的怪物预制体，仅作为稳定配置引用。")] public EnemyArchetype summon;
        [Range(1,60)] public int summonCount=4;
        [Range(0,1),Tooltip("大于 0 时子怪最大生命为来源最大生命的该比例；0 按导演基础值。")] public float summonHealthRatio;
        [Range(1,8)] public int repeatCount=1;
        [Min(0)] public float repeatInterval=.4f,repeatOffset;
        [Tooltip("持续圆环从内向外扩展到范围半径。")] public bool expandingRing;
        public Color color=new Color(1,.24f,.08f,.65f);
        public MonsterAbilityData Compile(SkillCatalog catalog)
        {
            if(string.IsNullOrWhiteSpace(stableId) || !Enum.IsDefined(typeof(MonsterAbilityTemplate),template) || !Enum.IsDefined(typeof(MonsterDeathAction),deathAction) || !Enum.IsDefined(typeof(AreaShape),shape))
                throw new InvalidOperationException("怪物技能缺少编号或包含未实现类型："+name);
            foreach(float value in new[]{cooldown,warningSeconds,triggerRange,radius,range,halfAngle,innerRadius,damageRatio,healMaxHealth,knockback,duration,interval,speed,spreadDegrees,trailSeconds,trailDamageRatio,summonHealthRatio,repeatOffset,repeatInterval})
                if(!float.IsFinite(value) || value<0) throw new InvalidOperationException("怪物技能参数必须有限且非负："+name);
            if(healMaxHealth>1 || summonHealthRatio>1 || radius<=0 || range<=0 || triggerRange<=0 || duration<=0 || interval<.05f || speed<=0 || halfAngle>180 || projectileCount<1 || projectileCount>32 || spreadDegrees>360 || minimumStage<1 || minimumStage>3 || repeatCount<1 || repeatCount>8 || repeatInterval<0 || repeatOffset<0)
                throw new InvalidOperationException("怪物技能的范围、时间、数量或阶段不合法："+name);
            if(shape==AreaShape.Ring && innerRadius>=radius) throw new InvalidOperationException("圆环内半径必须小于外半径："+name);
            if((template==MonsterAbilityTemplate.Summon || template==MonsterAbilityTemplate.OnDeath && deathAction==MonsterDeathAction.Split) && (!summon || summonCount<1 || summonCount>60))
                throw new InvalidOperationException("召唤模块缺少合法怪物或数量："+name);
            return new MonsterAbilityData{Template=template,DeathAction=deathAction,Shape=shape,Cooldown=cooldown,Warning=warningSeconds,
                TriggerRange=triggerRange,Radius=radius,Range=range,HalfAngle=halfAngle,InnerRadius=innerRadius,MinimumStage=minimumStage,
                AtHero=atHero,Sacrifice=sacrifice,DamageRatio=damageRatio,HealMaxHealth=healMaxHealth,HasStatus=status,
                Status=status?catalog.CompileExternalEffect(status,name):default,Knockback=knockback,Duration=duration,Interval=interval,
                Speed=speed,ProjectileCount=projectileCount,Spread=spreadDegrees,AtPoint=projectileAtPoint,TrailSeconds=trailSeconds,TrailDamageRatio=trailDamageRatio,
                RepeatCount=repeatCount,RepeatInterval=repeatInterval,RepeatOffset=repeatOffset,ExpandingRing=expandingRing,
                SummonId=summon?summon.configId:0,SummonCount=summonCount,SummonHealthRatio=summonHealthRatio,Color=new Unity.Mathematics.float4(color.r,color.g,color.b,color.a)};
        }
    }
    public struct MonsterAbilityData
    {
        public MonsterAbilityTemplate Template;public MonsterDeathAction DeathAction;public AreaShape Shape;
        public float Cooldown,Warning,TriggerRange,Radius,Range,HalfAngle,InnerRadius;
        public int MinimumStage,ProjectileCount,SummonId,SummonCount,RepeatCount;
        public float RepeatInterval,RepeatOffset; public bool ExpandingRing;
        public bool AtHero,Sacrifice,HasStatus,AtPoint;
        public float DamageRatio,HealMaxHealth,Knockback,Duration,Interval,Speed,Spread,TrailSeconds,TrailDamageRatio,SummonHealthRatio;
        public EffectData Status;public Unity.Mathematics.float4 Color;
    }
    public struct MonsterAbilityLibrary {public BlobArray<MonsterAbilityData> Abilities;}
    [Flags]
    public enum MonsterAffix
    {
        [InspectorName("无")] None=0,[InspectorName("狂怒")] Rage=1,[InspectorName("护盾")] Shield=2,
        [InspectorName("召唤")] Summon=4,[InspectorName("分裂")] Split=8,[InspectorName("寒霜或余烬")] Elemental=16,[InspectorName("坚韧")] Fortified=32
    }
    /// <summary>单个怪物的技能冷却、词缀与死亡状态。技能定义 Blob 由会话唯一持有。</summary>
    public struct MonsterBrain : IComponentData
    {
        public BlobAssetReference<MonsterAbilityLibrary> Library;
        public Unity.Mathematics.float4 ReadyAt;
        public MonsterAffix Affixes;
        public float ShieldAt,SummonAt,BusyUntil,ArmoredAboveHalf;
        public bool Summoned,DeathHandled,Enraged,ElementIsFire;
        public int SummonKind;
        public EffectData ElementEffect;
    }
}
