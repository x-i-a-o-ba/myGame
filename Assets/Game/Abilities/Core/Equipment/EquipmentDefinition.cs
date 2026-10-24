using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>装备品质：只影响图标边框颜色、商店排序展示与提示文字，不改变结算。</summary>
    public enum EquipmentQuality
    {
        [InspectorName("普通")] Common,
        [InspectorName("精良")] Uncommon,
        [InspectorName("稀有")] Rare,
        [InspectorName("史诗")] Epic,
        [InspectorName("传说")] Legendary
    }

    /// <summary>
    /// 一件装备的配置，只保存数据：价格、属性加成（直接相加到英雄属性上）与可选的普攻命中效果。
    /// 新增装备 = 新建本资源并加入装备库；运行中的持有状态归英雄实体的 EquipmentSlot 缓冲。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/装备/装备定义")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        public const int MinimumConfigId=6000,MaximumConfigId=6999,MaximumStats=8,MaximumOnHitEffects=4,MaximumPrice=100000;
        [Tooltip("数字配置 ID：装备使用 6000–6999，并在 Docs/游戏配置/编号登记.json 登记；0 仅表示尚未分配的临时定义。")]
        public int configId;
        [Tooltip("稳定 ID：英文小写、数字与下划线，资源改名后保持不变，例如 item.iron_sword。")]
        public string stableId;
        [Tooltip("界面显示名称。")] public string displayName;
        [Tooltip("装备图标（HUD 装备栏与商店）。为空时装备栏显示装备名称。")] public Texture2D icon;
        [Tooltip("品质：影响图标边框颜色与提示文字，不改变数值。")] public EquipmentQuality quality;
        [Min(0), Tooltip("购买价格（金币）。出售按装备库的出售比例返还。")] public int price=500;
        [Tooltip("唯一：同一件装备最多持有一件（适合带命中效果或强力属性的装备）。")] public bool unique;
        [TextArea(2,5), Tooltip("界面说明文字。")] public string description;
        [Tooltip("直接加到英雄属性上的数值；百分比类属性填写百分点（25 = +25%）。最多 8 条。")]
        public StatModifier[] stats=Array.Empty<StatModifier>();
        [Tooltip("普攻命中时依次施加的效果模块（复用技能效果资源，例如“施加扇击减速”）；作用对象为“施法者”时作用于英雄自身。引用的 Buff 必须登记在技能库中。最多 4 个。")]
        public EffectDefinition[] onHitEffects=Array.Empty<EffectDefinition>();
        [Tooltip("普攻或施法触发的有限范围/连锁效果。")] public EquipmentProc[] procs=Array.Empty<EquipmentProc>();

        public string Label=>string.IsNullOrWhiteSpace(displayName)?name:displayName.Trim();

        /// <summary>按属性种类合并后的加成（同一属性多条时相加），用于界面展示。</summary>
        public StatSheet Sheet()
        {
            var sheet=default(StatSheet);
            if(stats!=null) foreach(var modifier in stats) sheet.Add(modifier.stat,modifier.value);
            return sheet;
        }

        /// <summary>校验单件装备自身的配置；跨资源引用（命中效果的 Buff）由装备库编译时检查。返回 null 表示通过。</summary>
        public string Validate()
        {
            if(procs==null || procs.Length>4) return "装备触发器最多四个且不可为空："+name;
            foreach(var proc in procs)
            {
                if(proc==null || !Enum.IsDefined(typeof(EquipmentTrigger),proc.trigger) || !float.IsFinite(proc.chance) || proc.chance<0 || proc.chance>1 ||
                    !float.IsFinite(proc.radius) || proc.radius<=0 || proc.maxTargets<1 || proc.maxTargets>32 || proc.effects==null || proc.effects.Length==0 || proc.effects.Length>4)
                    return "装备触发器参数无效："+name;
            }
            if(configId!=0 && (configId<MinimumConfigId || configId>MaximumConfigId)) return "装备数字 ID 必须为 6000–6999："+name;
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId) return "装备稳定 ID 不能为空或带首尾空格："+name;
            if(price<0 || price>MaximumPrice) return "装备价格必须在 0–"+MaximumPrice+"："+name;
            if(quality!=EquipmentQuality.Common && quality!=EquipmentQuality.Rare && quality!=EquipmentQuality.Legendary) return "当前仅支持普通、稀有和传说装备："+name;
            var list=stats??Array.Empty<StatModifier>();
            if(list.Length>MaximumStats) return "装备属性最多 "+MaximumStats+" 条："+name;
            foreach(var modifier in list)
                if(!StatText.IsCurrent(modifier.stat) || float.IsNaN(modifier.value) || float.IsInfinity(modifier.value))
                    return "装备属性未知或数值非法："+name;
            var effects=onHitEffects??Array.Empty<EffectDefinition>();
            if(effects.Length>MaximumOnHitEffects) return "装备命中效果最多 "+MaximumOnHitEffects+" 个："+name;
            foreach(var effect in effects) if(!effect) return "装备命中效果包含空引用："+name;
            if(list.Length==0 && effects.Length==0) return "装备至少需要一条属性或一个命中效果："+name;
            return null;
        }
    }
}
