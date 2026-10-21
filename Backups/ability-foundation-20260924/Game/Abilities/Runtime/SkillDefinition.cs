using UnityEngine;

namespace Roguelike.Abilities
{
    public enum SkillDelivery { InstantCircle, PersistentCircle, SelfCircle }
    public enum TargetRelation { Enemies, Allies }
    public enum EffectKind { Damage, Heal, ApplyBuff }
    public enum BuffStackRule { Refresh, AddAndRefresh }
    public enum PassiveTrigger { WhileLearned, BasicAttackHit }

    /// <summary>编辑期效果条目；Buff 引用共享定义，倍率属于此组合，运行时不修改资源。</summary>
    [System.Serializable]
    public struct EffectEntry
    {
        public EffectKind kind;
        [Min(0), Tooltip("即时伤害/治疗数值。演示暂不计算护甲、暴击。")] public float amount;
        public BuffDefinition buff;
        [Min(0.01f), Tooltip("Buff 强度倍率；不修改共享 Buff 本身。")] public float buffScale;
    }

    /// <summary>主动技能的共享配置；冷却与等级存储在拥有者实体上。</summary>
    [CreateAssetMenu(menuName="Roguelike/Abilities/Skill")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        public SkillDelivery delivery;
        public TargetRelation targets;
        [Min(0)] public float castRange=18;
        [Min(0.1f)] public float radius=5;
        [Min(0)] public float cooldown=4;
        [Min(0)] public float manaCost=15;
        [Min(0.1f), Tooltip("持续区域时长，秒。")] public float areaDuration=5;
        [Min(0.05f), Tooltip("持续区域查询间隔，秒。")] public float areaInterval=0.25f;
        [Range(0,1), Tooltip("每级伤害和 Buff 强度增幅；范围每级增加 10%。")] public float levelPower=0.25f;
        public Color indicatorColor=Color.cyan;
        public EffectEntry[] effects;
    }
}
