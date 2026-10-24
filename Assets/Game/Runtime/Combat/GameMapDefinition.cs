using System;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 战场定义：选择界面显示的名称、简介与预览图，开始战斗时加载的场景 ID，以及该战场的敌人名册与难度曲线。
    /// 只存配置；本局所选战场记录在 GameSelection，战斗运行状态归 GameCombatSession。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/战斗/战场定义")]
    public sealed class GameMapDefinition : ScriptableObject
    {
        public const int FirstConfigId=7000, LastConfigId=7999;
        [Tooltip("数字配置 ID：战场使用 7000–7999，登记在 Docs/游戏配置/编号登记.json。")] public int configId;
        [Tooltip("内部兼容键，例如 map.shadow_wastes；日志使用，不随名称变化。")] public string stableId;
        [Tooltip("战场名称：选择界面与战斗右上角显示。")] public string displayName;
        [TextArea(2,5), Tooltip("选择界面战场卡片上的简介。")] public string description;
        [Tooltip("选择界面战场卡片的预览图；为空时显示纯色底。")] public Texture2D preview;
        [Tooltip("战场场景在资源清单（Resources/ProjectResourceCatalog.json）中的场景 ID，例如 Scene.Game；开始战斗时加载它。")] public string sceneId;
        [Tooltip("本战场的敌人名册（必填）：选择界面展示这些怪物，战斗按它刷怪，覆盖战场场景控制器上的名册。")] public EnemyRoster enemyRoster;
        [Tooltip("本战场的关卡难度曲线；为空时使用战场场景控制器上的曲线。")] public CombatDifficultyDefinition difficulty;

        [Tooltip("间歇生成有预警的熔岩区域，伤害双方单位。")] public bool lavaHazards;
        [Min(.01f)] public float enemyHealthMultiplier=1,enemyDamageMultiplier=1;
        public string Label=>string.IsNullOrWhiteSpace(displayName)?name:displayName;

        /// <summary>校验编号、展示字段、场景 ID 与敌人名册；返回第一个错误，没有错误时返回 null。不修改资源。</summary>
        public string Validate()
        {
            if(configId<FirstConfigId || configId>LastConfigId) return $"战场数字 ID 必须为 {FirstConfigId}–{LastConfigId}：{name}";
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId) return "战场稳定 ID 不能为空或带首尾空格："+name;
            if(string.IsNullOrWhiteSpace(displayName)) return "战场名称不能为空："+name;
            if(string.IsNullOrWhiteSpace(sceneId) || !sceneId.StartsWith("Scene.",StringComparison.Ordinal) || sceneId.Trim()!=sceneId)
                return "战场场景 ID 必须是资源清单中的 Scene.<场景名>："+name;
            if(float.IsNaN(enemyHealthMultiplier) || float.IsInfinity(enemyHealthMultiplier) || enemyHealthMultiplier<=0 ||
                float.IsNaN(enemyDamageMultiplier) || float.IsInfinity(enemyDamageMultiplier) || enemyDamageMultiplier<=0)
                return "战场生命和伤害倍率必须是大于零的有限数值："+name;
            if(!enemyRoster) return "战场缺少敌人名册："+name;
            string roster=enemyRoster.Validate();
            return roster==null?null:name+" 的敌人名册无效："+roster;
        }
    }
}
