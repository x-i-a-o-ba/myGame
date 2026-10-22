using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 商店与装备栏配置：出售的装备列表、装备栏格数、开局金币与出售返还比例。
    /// 编译成会话拥有的只读 <see cref="EquipmentLibrary"/>；不保存任何运行状态。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/装备/装备库")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        public const int MaximumItems=256,MaximumSlots=6;
        [Tooltip("商店出售的装备，按此顺序显示。新增装备只需新建装备定义并加入这里。")]
        public EquipmentDefinition[] items=Array.Empty<EquipmentDefinition>();
        [Range(1,MaximumSlots), Tooltip("装备栏格数（HUD 最多显示 6 格）。")] public int slotCount=MaximumSlots;
        [Min(0), Tooltip("开局金币。")] public int startingGold=300;
        [Range(0,1), Tooltip("出售返还比例：0.5 表示返还购买价格的 50%（向下取整）。")] public float sellRatio=0.5f;

        public int SellPrice(EquipmentDefinition item)=>item?Mathf.FloorToInt(item.price*Mathf.Clamp01(sellRatio)+0.0001f):0;

        /// <summary>校验整个装备库（不含命中效果的 Buff 登记检查）；返回 null 表示通过。</summary>
        public string Validate()
        {
            if(items==null) return "装备列表不能为 null："+name;
            if(items.Length>MaximumItems) return "装备库最多 "+MaximumItems+" 件装备："+name;
            if(slotCount<1 || slotCount>MaximumSlots) return "装备栏格数必须为 1–"+MaximumSlots+"："+name;
            if(startingGold<0 || startingGold>1000000) return "开局金币必须在 0–1000000："+name;
            if(float.IsNaN(sellRatio) || sellRatio<0 || sellRatio>1) return "出售返还比例必须在 0–1："+name;
            var seen=new HashSet<EquipmentDefinition>(); var ids=new HashSet<int>(); var keys=new HashSet<string>();
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];
                if(!item) return "装备库第 "+(i+1)+" 项为空："+name;
                if(!seen.Add(item)) return "装备重复登记："+item.name;
                string failure=item.Validate();
                if(failure!=null) return failure;
                if(item.configId!=0 && !ids.Add(item.configId)) return "装备数字 ID 重复："+item.configId+" / "+item.name;
                if(!keys.Add(item.stableId)) return "装备稳定 ID 重复："+item.stableId;
            }
            return null;
        }

        /// <summary>
        /// 校验并编译只读装备表：属性按种类合并，命中效果用技能库解析 Buff 下标（与本局技能库 Blob 一致）。
        /// 调用方唯一拥有返回值；失败抛出 InvalidOperationException，不返回半成品。
        /// </summary>
        public BlobAssetReference<EquipmentLibrary> Compile(SkillCatalog skills)
        {
            string failure=Validate();
            if(failure!=null) throw new InvalidOperationException(failure);
            var effects=new EffectData[items.Length][];
            for(int i=0;i<items.Length;i++)
            {
                var list=items[i].onHitEffects??Array.Empty<EffectDefinition>();
                if(list.Length>0 && !skills) throw new InvalidOperationException("装备命中效果需要技能库解析 Buff："+items[i].name);
                effects[i]=new EffectData[list.Length];
                for(int e=0;e<list.Length;e++) effects[i][e]=skills.CompileExternalEffect(list[e],"装备命中效果 "+items[i].name);
            }
            using(var builder=new BlobBuilder(Allocator.Temp))
            {
                ref var root=ref builder.ConstructRoot<EquipmentLibrary>();
                root.SlotCount=slotCount; root.StartingGold=startingGold;
                var output=builder.Allocate(ref root.Items,items.Length);
                for(int i=0;i<items.Length;i++)
                {
                    var item=items[i];
                    output[i].ConfigId=item.configId; output[i].Price=item.price; output[i].SellPrice=SellPrice(item);
                    output[i].Quality=item.quality; output[i].Unique=item.unique; output[i].Stats=item.Sheet();
                    var hits=builder.Allocate(ref output[i].OnHit,effects[i].Length);
                    for(int e=0;e<effects[i].Length;e++) hits[e]=effects[i][e];
                }
                return builder.CreateBlobAssetReference<EquipmentLibrary>(Allocator.Persistent);
            }
        }

        /// <summary>按数字 ID 查找装备下标；未找到返回 -1。</summary>
        public int IndexOf(int configId)
        {
            if(items!=null) for(int i=0;i<items.Length;i++) if(items[i] && items[i].configId==configId) return i;
            return -1;
        }
    }
}
