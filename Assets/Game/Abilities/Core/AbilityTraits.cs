using Unity.Entities;

namespace Roguelike.Abilities
{
    /// <summary>宿主装配的额外规则贡献；来源可为遗物等配置，执行器只消费语义，不识别来源身份。</summary>
    public struct AbilityTraits : IComponentData
    {
        public int ExtraBasicTargets;
        public float FreeCooldownChance,KillHealRatio,ControlDurationMultiplier,BodyScale,MaxHealthMultiplier,BasicRangeMultiplier;
        public float ExtraCooldownReduction,CooldownCap,EliteGoldMultiplier;
        public bool HasSkillHitEffect;
        public EffectData SkillHitEffect;
        public static AbilityTraits Identity=>new AbilityTraits{ControlDurationMultiplier=1,BodyScale=1,MaxHealthMultiplier=1,
            BasicRangeMultiplier=1,CooldownCap=.5f,EliteGoldMultiplier=1};
    }
}
