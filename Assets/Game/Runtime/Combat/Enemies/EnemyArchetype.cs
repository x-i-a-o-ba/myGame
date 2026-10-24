using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>敌人行为。每一项都在 CombatSimulationSystem 的敌人任务中有独立执行分支。</summary>
    public enum EnemyBehavior
    {
        [InspectorName("近战追击")] Melee,
        [InspectorName("远程游击（保持距离射击）")] Ranged,
        [InspectorName("已停用：请装配冲锋技能模块")] Charger,
        [InspectorName("已停用：请装配自爆技能模块")] Exploder
    }

    /// <summary>
    /// 敌人种类预制体的配置组件。外形取自预制体内全部 MeshFilter（会话开始时合并为一个网格），材质取第一个 MeshRenderer；
    /// 预制体本身不会被实例化成 GameObject，而是转换成 ECS 原型实体，以支持万人同屏。只存配置，运行状态归敌人实体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArchetype : MonoBehaviour
    {
        [Tooltip("数字配置 ID：怪物使用 3000–3999，登记在 Docs/游戏配置/编号登记.json。")] public int configId;
        [Tooltip("内部兼容键，例如 enemy.ghoul。")] public string stableId;
        [Tooltip("界面显示名称。")] public string displayName;
        [TextArea(2,4)] public string description;
        [Tooltip("选中敌人时左上角信息面板显示的头像；为空时使用攻击模块图标。")] public Texture2D portrait;
        [Tooltip("行为决定移动与出手方式。")] public EnemyBehavior behavior;
        [Tooltip("自动攻击被动模块，与英雄共用同一套前摇/后摇/攻击间隔规则。")] public AutoAttackDefinition attack;
        [Header("数值倍率（乘以当前强度时间的基础数值）")]
        [Min(0.01f)] public float healthMultiplier=1;
        [Min(0)] public float damageMultiplier=1;
        [Min(0)] public float speedMultiplier=1;
        [Min(0)] public float experienceMultiplier=1;
        [Min(0), Tooltip("击杀金币倍率。")] public float goldMultiplier=1;
        [Min(0.1f), Tooltip("体型倍率：影响模型缩放与额外攻击距离。")] public float bodyScale=1;
        [Header("远程游击")]
        [Range(0.2f,1), Tooltip("停在攻击距离 × 该比例处射击。")] public float preferredDistance=0.85f;
        [Min(0), Tooltip("英雄靠近到该距离（米）以内时后退。")] public float retreatDistance=5;
        [Header("冲锋")]
        [Min(0), Tooltip("与英雄距离小于该值（米）时开始冲锋。")] public float chargeTriggerRange=10;
        [Min(1), Tooltip("冲锋速度 = 移动速度 × 该倍率。")] public float chargeSpeedMultiplier=4;
        [Min(0.05f), Tooltip("单次冲锋持续时间（秒）。")] public float chargeDuration=0.7f;
        [Min(0), Tooltip("两次冲锋之间的冷却（秒）。")] public float chargeCooldown=5;
        [Min(0), Tooltip("冲锋撞到英雄时的伤害 = 攻击力 × 该倍率。")] public float chargeDamageMultiplier=2.5f;
        [Header("自爆")]
        [Min(0.1f), Tooltip("爆炸半径（米），从自身中心计算。")] public float explosionRadius=3.5f;
        [Min(0), Tooltip("爆炸伤害 = 攻击力 × 该倍率。")] public float explosionDamageMultiplier=5;

        [Header("模块化怪物技能（最多四个）")]
        public MonsterAbilityDefinition[] abilities=Array.Empty<MonsterAbilityDefinition>();
        public ApplyBuffEffect elementalSlow;
        public bool fireElement;
        [Range(0,1)] public float armoredAboveHalf;

        public string Label=>string.IsNullOrWhiteSpace(displayName)?name:displayName;

        /// <summary>行为的简短中文名称，供信息面板等界面显示。</summary>
        public static string BehaviorLabel(EnemyBehavior behavior)
        {
            switch(behavior)
            {
                case EnemyBehavior.Melee: return "近战追击";
                case EnemyBehavior.Ranged: return "远程游击";
                case EnemyBehavior.Charger: return "冲锋";
                case EnemyBehavior.Exploder: return "自爆";
                default: return behavior.ToString();
            }
        }

        /// <summary>校验配置并生成行为快照；不修改资源。</summary>
        public EnemyBehaviorData Compile(out AutoAttackData attackData)
        {
            if(configId<3000 || configId>3999) throw new InvalidOperationException("怪物数字 ID 必须为 3000–3999："+name);
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId) throw new InvalidOperationException("怪物内部兼容键为空或含首尾空格："+name);
            if(behavior!=EnemyBehavior.Melee && behavior!=EnemyBehavior.Ranged) throw new InvalidOperationException("旧冲锋和自爆行为已停用，请装配怪物技能模块："+name);
            if(!attack) throw new InvalidOperationException("怪物缺少自动攻击模块："+name);
            attackData=attack.Compile();
            if(behavior==EnemyBehavior.Ranged && attackData.Delivery!=AttackDelivery.Projectile)
                throw new InvalidOperationException("远程游击怪物必须使用弹道自动攻击："+name);
            float[] values={healthMultiplier,damageMultiplier,speedMultiplier,experienceMultiplier,goldMultiplier,bodyScale,preferredDistance,retreatDistance,
                chargeTriggerRange,chargeSpeedMultiplier,chargeDuration,chargeCooldown,chargeDamageMultiplier,explosionRadius,explosionDamageMultiplier};
            foreach(float value in values)
                if(float.IsNaN(value) || float.IsInfinity(value) || value<0) throw new InvalidOperationException("怪物数值必须是有限的非负数："+name);
            if(healthMultiplier<0.01f || bodyScale<0.1f) throw new InvalidOperationException("怪物生命倍率至少 0.01、体型倍率至少 0.1："+name);
            return new EnemyBehaviorData {Behavior=behavior,PreferredDistance=Mathf.Clamp(preferredDistance,0.2f,1),RetreatDistance=retreatDistance};
        }

        /// <summary>预制体使用的材质：第一个 MeshRenderer 的共享材质。</summary>
        public Material ResolveMaterial()
        {
            var renderer=GetComponentInChildren<MeshRenderer>(true);
            return renderer?renderer.sharedMaterial:null;
        }

        /// <summary>
        /// 把预制体内全部 MeshFilter 按相对根节点的变换合并成一个网格。只有根节点一个网格时直接返回原网格（created=false）；
        /// 合并结果由调用方拥有并负责销毁。
        /// </summary>
        public Mesh BuildMesh(out bool created)
        {
            created=false;
            var filters=GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length==1 && filters[0].transform==transform) return filters[0].sharedMesh;
            var root=transform.worldToLocalMatrix;
            var parts=new List<CombineInstance>(filters.Length);
            foreach(var filter in filters)
            {
                if(!filter.sharedMesh) continue;
                // Player 中合并需要网格可读；不可读时退回第一个网格（丢失部件相对位置），并提示开启 Read/Write。
                if(!filter.sharedMesh.isReadable)
                {
                    Debug.LogWarning("敌人预制体的网格不可读，无法合并，只使用第一个网格；请在模型导入设置中开启 Read/Write："+name,this);
                    return filters[0].sharedMesh;
                }
                parts.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=0,transform=root*filter.transform.localToWorldMatrix});
            }
            if(parts.Count==0) return null;
            var mesh=new Mesh{name=Label+"（合并网格）"};
            mesh.CombineMeshes(parts.ToArray(),true,true);
            mesh.RecalculateBounds();
            created=true;
            return mesh;
        }
    }
}
