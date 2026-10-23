using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>有限事件树的真实执行端口；不包含尚未实现的反伤、周期或任意脚本事件。</summary>
    public enum RecipeTrigger
    {
        [InspectorName("施法释放")] OnRelease,
        [InspectorName("到达并完成查询后（整组一次）")] OnResolved,
        [InspectorName("直线弹体首碰后")] OnProjectileHit,
        [InspectorName("造成实际伤害后")] OnDamage,
        [InspectorName("击杀后")] OnKill
    }

    /// <summary>可持久化的配方正文；只含数字引用和类型化值，不含 Unity 对象、Entity 或执行器名称。</summary>
    [Serializable]
    public sealed class SkillRecipe
    {
        public const int CurrentSchemaVersion=1;
        public bool enabled;
        public int schemaVersion=CurrentSchemaVersion;
        public int catalogVersion=1;
        public int revision=1;
        public int recipeId=1;
        public string authorNamespace="local";
        public int maxDerivedExecutions=32;
        public float maxLifetime=10;
        public RecipeNode[] nodes=Array.Empty<RecipeNode>();
    }

    /// <summary>一次投递/查询/效果步骤。parentNodeId=0 为释放根；其余节点从父端口位置继承上下文。</summary>
    [Serializable]
    public sealed class RecipeNode
    {
        public int nodeId=1;
        public int parentNodeId;
        public RecipeTrigger trigger;
        public int deliveryModuleId;
        public int queryModuleId;
        public float delay,repeatInterval;
        public int repeatCount=1;
        public float range=18,radius=5,hitRadius=.5f,halfAngleDegrees=35,speed=40;
        public int projectileCount=1;
        public float spreadDegrees;
        public float arcHeight=3,flightSeconds=1,launchHeight=1,arrivalHeight;
        public RecipeEffectUse[] effects=Array.Empty<RecipeEffectUse>();
        public RecipeEffectUse[] impactEffects=Array.Empty<RecipeEffectUse>();
    }

    /// <summary>同一效果可以在多个节点使用；覆盖只写入编译数据，绝不修改共享定义。</summary>
    [Serializable]
    public sealed class RecipeEffectUse
    {
        public int effectId;
        public bool overrideAmount;
        public float amount;
        [HideInInspector] public bool overrideBuffScale;
        [HideInInspector] public float buffScale=1;
    }

    /// <summary>持久化施法外壳与配方，装备时由同一 SkillCatalog 编译；没有运行冷却/实体数据。</summary>
    [Serializable]
    public sealed class SkillRecipeDocument
    {
        public int schemaVersion=1;
        public string name="新配方";
        public AimMode aim=AimMode.Point;
        public float castRange=18,cooldown=4,castTime,recoveryTime,manaCost;
        public int maxCharges=1,requiredHits;
        public float rechargeSeconds;
        public SkillRecipe recipe=new SkillRecipe();
    }
}
