using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Abilities
{
    [Serializable]
    public sealed class DeliveryModule
    {
        public int id;
        public string displayName;
        public bool playerAllowed=true;
        public DeliveryKind kind;
        public float maxRange=150,maxSpeed=100,maxFlightSeconds=10,maxArcHeight=30;
    }

    [Serializable]
    public sealed class QueryModule
    {
        public int id;
        public string displayName;
        public bool playerAllowed=true;
        public TargetScope scope=TargetScope.Area;
        public AreaShape shape=AreaShape.Circle;
        public TargetRelation targets=TargetRelation.Enemies;
        public float maxRange=150,maxRadius=40;
    }

    /// <summary>定义哪些参数允许本次使用覆盖；新效果仍通过已有 EffectDefinition.Compile 实现。</summary>
    [Serializable]
    public sealed class EffectModule
    {
        public EffectDefinition definition;
        public bool playerAllowed=true;
        public bool allowAmountOverride;
        public float minAmount,maxAmount=1000;
        [HideInInspector] public bool allowBuffScaleOverride;
        [HideInInspector] public float minBuffScale=.1f,maxBuffScale=3;
        public int Id => definition ? definition.configId : 0;
    }

    /// <summary>固定版本的数字模块目录。编译时解析身份，执行时只消费已有的投递/查询/效果数据。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/组合模块目录")]
    public sealed class SkillModuleCatalog : ScriptableObject
    {
        public int catalogVersion=1;
        public DeliveryModule[] deliveries=Array.Empty<DeliveryModule>();
        public QueryModule[] queries=Array.Empty<QueryModule>();
        public EffectModule[] effects=Array.Empty<EffectModule>();
        public BuffDefinition[] buffs=Array.Empty<BuffDefinition>();

        public DeliveryModule FindDelivery(int id)
        { foreach(var item in deliveries) if(item!=null && item.id==id) return item; throw Missing("投递",id); }
        public QueryModule FindQuery(int id)
        { foreach(var item in queries) if(item!=null && item.id==id) return item; throw Missing("查询",id); }
        public EffectModule FindEffect(int id)
        { foreach(var item in effects) if(item!=null && item.definition && item.definition.configId==id) return item; throw Missing("效果",id); }
        public BuffDefinition FindBuff(int id)
        { foreach(var item in buffs) if(item && item.configId==id) return item; throw Missing("Buff",id); }

        public void Validate()
        {
            if(catalogVersion<1 || deliveries==null || queries==null || effects==null || buffs==null)
                throw new InvalidOperationException("模块目录版本必须为正数，数组不能为 null。");
            var ids=new HashSet<int>();
            void Id(int id,int minimum,int maximum,string name)
            { if(id<minimum || id>maximum || !ids.Add(id)) throw new InvalidOperationException(name+" ID 越界或重复："+id); }
            foreach(var d in deliveries)
            {
                if(d==null) throw new InvalidOperationException("投递模块不能为 null。");
                Id(d.id,10000,99999,"投递模块"); Defined(d.kind,"投递方式");
                Positive(d.maxRange,"投递长度上限"); Positive(d.maxSpeed,"弹速上限");
                Positive(d.maxFlightSeconds,"飞行时间上限"); Positive(d.maxArcHeight,"弧线上限");
            }
            foreach(var q in queries)
            {
                if(q==null) throw new InvalidOperationException("查询模块不能为 null。");
                Id(q.id,10000,99999,"查询模块"); Defined(q.scope,"目标范围"); Defined(q.shape,"区域形状"); Defined(q.targets,"目标关系");
                Positive(q.maxRange,"查询长度上限"); Positive(q.maxRadius,"查询半径上限");
            }
            foreach(var b in buffs)
            { if(!b) throw new InvalidOperationException("Buff 模块引用为空。"); Id(b.configId,5000,5999,"Buff"); }
            foreach(var e in effects)
            {
                if(e==null || !e.definition) throw new InvalidOperationException("效果模块引用为空。");
                Id(e.Id,4000,4999,"效果");
                Range(e.minAmount,e.maxAmount,"数值覆盖范围",true);
                Range(e.minBuffScale,e.maxBuffScale,"Buff 强度覆盖范围",false);
                var data=e.definition.Compile();
                Defined(data.Kind,"效果类型"); Defined(data.Target,"效果作用对象");
                if(e.allowAmountOverride && data.Kind!=EffectKind.Damage && data.Kind!=EffectKind.Heal)
                    throw new InvalidOperationException("仅伤害/治疗模块支持数值覆盖："+e.Id);
                if(e.allowBuffScaleOverride)
                    throw new InvalidOperationException("当前版本尚不支持 Buff 规格独立合并，不能开放强度覆盖："+e.Id);
                if(data.Kind==EffectKind.ApplyBuff && (!e.definition.ReferencedBuff ||
                    FindBuff(e.definition.ReferencedBuff.configId)!=e.definition.ReferencedBuff))
                    throw new InvalidOperationException("效果依赖 Buff 未登记："+e.Id);
            }
        }

        internal static void Defined<T>(T value,string context) where T:struct
        { if(!Enum.IsDefined(typeof(T),value)) throw new InvalidOperationException(context+"：不支持的枚举值 "+value); }
        internal static void Positive(float value,string context)
        { if(float.IsNaN(value)||float.IsInfinity(value)||value<=0) throw new InvalidOperationException(context+" 必须为有限正数。"); }
        private static void Range(float min,float max,string context,bool zero)
        { if(float.IsNaN(min)||float.IsInfinity(min)||float.IsNaN(max)||float.IsInfinity(max)||min<0||(!zero&&min==0)||max<min) throw new InvalidOperationException(context+" 无效。"); }
        private static Exception Missing(string category,int id) => new InvalidOperationException(category+"模块 ID 未登记："+id);
    }
}
