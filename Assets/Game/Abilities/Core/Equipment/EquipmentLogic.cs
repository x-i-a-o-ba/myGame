using Unity.Entities;
using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>一件装备的只读编译数据：价格、合并后的属性加成与普攻命中效果。</summary>
    public struct EquipmentItemData
    {
        public int ConfigId,Price,SellPrice;
        public EquipmentQuality Quality;
        public bool Unique;
        public StatSheet Stats;
        public BlobArray<EffectData> OnHit;
        public BlobArray<EquipmentProcData> Procs;
    }

    /// <summary>会话装配时由 <see cref="EquipmentCatalog.Compile"/> 生成的装备表；由会话拥有并释放。下标 = 装备库列表下标。</summary>
    public struct EquipmentLibrary
    {
        public int SlotCount,StartingGold;
        public BlobArray<EquipmentItemData> Items;
    }

    /// <summary>英雄的金币与装备运行状态，归英雄实体所有；装备栏内容在 <see cref="EquipmentSlot"/> 缓冲。</summary>
    public struct HeroEquipment : IComponentData
    {
        public BlobAssetReference<EquipmentLibrary> Library;
        /// <summary>当前金币（击杀累计，可带小数，界面向下取整）与本局累计获得的金币。</summary>
        public float Gold,GoldEarned;
        public int CommandSerial,ChangeSerial;
        public EquipmentFailure LastFailure;
        public EquipmentCommandKind LastCommand;
        /// <summary>最近一次指令涉及的装备下标（-1 表示无）。</summary>
        public int LastItem;
    }

    /// <summary>装备栏的一格；Item 为装备库下标，-1 表示空格。</summary>
    [InternalBufferCapacity(6)]
    public struct EquipmentSlot : IBufferElementData { public int Item; }

    public enum EquipmentCommandKind { Buy,Sell }
    public enum EquipmentFailure { None,Dead,InvalidItem,NotEnoughGold,InventoryFull,AlreadyOwned,InvalidSlot,EmptySlot }

    public static class EquipmentLogic
    {
        /// <summary>装备命中效果的来源编号起点（+ 装备下标）；与技能下标、旧被动 1000+ 区分，用于“按来源合并”的 Buff。</summary>
        public const int OnHitOrigin=3000;
        public const float RangeSlack=0.05f;
        public static HeroEquipment Create(BlobAssetReference<EquipmentLibrary> library)
            =>new HeroEquipment {Library=library,Gold=library.IsCreated?library.Value.StartingGold:0,LastItem=-1};

        /// <summary>按装备库格数填充空格。</summary>
        public static void InitializeSlots(DynamicBuffer<EquipmentSlot> slots,in HeroEquipment equipment)
        {
            slots.Clear();
            int count=equipment.Library.IsCreated?equipment.Library.Value.SlotCount:0;
            for(int i=0;i<count;i++) slots.Add(new EquipmentSlot{Item=-1});
        }

        /// <summary>击杀获得金币；非有限或非正数被忽略。</summary>
        public static void GainGold(ref HeroEquipment equipment,float amount)
        {
            if(!(amount>0) || float.IsInfinity(amount)) return;
            equipment.Gold+=amount; equipment.GoldEarned+=amount;
        }

        public static bool Owns(DynamicBuffer<EquipmentSlot> slots,int item)
        {
            for(int i=0;i<slots.Length;i++) if(slots[i].Item==item) return true;
            return false;
        }
        public static int FreeSlot(DynamicBuffer<EquipmentSlot> slots)
        {
            for(int i=0;i<slots.Length;i++) if(slots[i].Item<0) return i;
            return -1;
        }
        /// <summary>装着该装备的第一格；没有时返回 -1。</summary>
        public static int SlotOf(DynamicBuffer<EquipmentSlot> slots,int item)
        {
            if(item<0) return -1;
            for(int i=0;i<slots.Length;i++) if(slots[i].Item==item) return i;
            return -1;
        }
        /// <summary>只读判断能否购买（界面与指令处理共用）。</summary>
        public static EquipmentFailure CanBuy(in HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,int item)
        {
            if(!equipment.Library.IsCreated || item<0 || item>=equipment.Library.Value.Items.Length) return EquipmentFailure.InvalidItem;
            ref var data=ref equipment.Library.Value.Items[item];
            if(data.Unique && Owns(slots,item)) return EquipmentFailure.AlreadyOwned;
            if(FreeSlot(slots)<0) return EquipmentFailure.InventoryFull;
            if(equipment.Gold+0.0001f<data.Price) return EquipmentFailure.NotEnoughGold;
            return EquipmentFailure.None;
        }

        /// <summary>购买：扣除金币并放入第一个空格。成功后调用方重算英雄属性。</summary>
        public static bool TryBuy(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,int item)
        {
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Buy; equipment.LastItem=item;
            var failure=CanBuy(equipment,slots,item);
            equipment.LastFailure=failure;
            if(failure!=EquipmentFailure.None) return false;
            equipment.Gold=math.max(0,equipment.Gold-equipment.Library.Value.Items[item].Price);
            slots[FreeSlot(slots)]=new EquipmentSlot{Item=item};
            equipment.ChangeSerial++;
            return true;
        }

        /// <summary>出售：清空该格并按出售价返还金币。成功后调用方重算英雄属性。</summary>
        public static bool TrySell(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,int slot)
        {
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Sell; equipment.LastItem=-1;
            if(slot<0 || slot>=slots.Length) { equipment.LastFailure=EquipmentFailure.InvalidSlot; return false; }
            int item=slots[slot].Item;
            if(item<0 || !equipment.Library.IsCreated || item>=equipment.Library.Value.Items.Length) { equipment.LastFailure=EquipmentFailure.EmptySlot; return false; }
            equipment.LastItem=item;
            equipment.Gold+=equipment.Library.Value.Items[item].SellPrice;
            slots[slot]=new EquipmentSlot{Item=-1};
            equipment.LastFailure=EquipmentFailure.None; equipment.ChangeSerial++;
            return true;
        }

        public static StatSheet Sheet(DynamicBuffer<EquipmentSlot> slots,in HeroEquipment equipment)
        {
            var sheet=default(StatSheet);
            if(!equipment.Library.IsCreated) return sheet;
            ref var items=ref equipment.Library.Value.Items;
            for(int i=0;i<slots.Length;i++)
            {
                int item=slots[i].Item;
                if(item>=0 && item<items.Length) sheet=sheet+items[item].Stats;
            }
            return sheet;
        }

        public static string Describe(EquipmentFailure failure)
        {
            switch(failure)
            {
                case EquipmentFailure.None: return "";
                case EquipmentFailure.Dead: return "英雄已倒下";
                case EquipmentFailure.InvalidItem: return "装备不存在";
                case EquipmentFailure.NotEnoughGold: return "金币不足";
                case EquipmentFailure.InventoryFull: return "装备栏已满";
                case EquipmentFailure.AlreadyOwned: return "唯一装备已拥有";
                case EquipmentFailure.InvalidSlot: return "装备栏格不存在";
                case EquipmentFailure.EmptySlot: return "这一格没有装备";
            }
            return failure.ToString();
        }
    }
}
