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
    }

    /// <summary>会话装配时由 <see cref="EquipmentCatalog.Compile"/> 生成的装备表；由会话拥有并释放。下标 = 装备库列表下标。</summary>
    public struct EquipmentLibrary
    {
        public int SlotCount,StartingGold;
        /// <summary>丢弃距离与拾取距离（米）：英雄与落点 / 地上装备的平面距离不超过该值时才能丢弃 / 拾取。</summary>
        public float DropRange,PickupRange;
        public BlobArray<EquipmentItemData> Items;
    }

    /// <summary>英雄的金币与装备运行状态，归英雄实体所有；装备栏内容在 <see cref="EquipmentSlot"/> 缓冲。</summary>
    public struct HeroEquipment : IComponentData
    {
        public BlobAssetReference<EquipmentLibrary> Library;
        /// <summary>当前金币（击杀累计，可带小数，界面向下取整）与本局累计获得的金币。</summary>
        public float Gold,GoldEarned;
        /// <summary>每处理一条装备指令（购买、出售、换位、丢弃、拾取）递增；成功改变装备栏时 ChangeSerial 递增。</summary>
        public int CommandSerial,ChangeSerial;
        public EquipmentFailure LastFailure;
        public EquipmentCommandKind LastCommand;
        /// <summary>最近一次指令涉及的装备下标（-1 表示无）。</summary>
        public int LastItem;
        /// <summary>最近一次丢弃生成或拾取的地上装备编号（0 表示无）；下一件地上装备的编号为 NextGroundId + 1。</summary>
        public int LastGround,NextGroundId;
    }

    /// <summary>装备栏的一格；Item 为装备库下标，-1 表示空格。</summary>
    [InternalBufferCapacity(6)]
    public struct EquipmentSlot : IBufferElementData { public int Item; }

    /// <summary>地上的一件装备（Dota 2 式丢弃）：编号（本局唯一，从 1 开始）、装备库下标与落点。由丢弃生成、被拾取时移除；归会话实体所有，重开清空。</summary>
    [InternalBufferCapacity(0)]
    public struct GroundItem : IBufferElementData
    {
        public int Id,Item;
        public float3 Position;
    }

    /// <summary>
    /// 商店的固定位置与范围：英雄与商店中心的平面距离不超过 Radius 时才能打开商店、购买与出售。
    /// Limited 为假表示不限距离（场景没有配置商店位置时的兼容行为）。
    /// </summary>
    public struct ShopSite : IComponentData
    {
        public float3 Position;
        public float Radius;
        public bool Limited;
    }

    /// <summary>装备指令：购买、出售、换位（拖到另一格）、丢弃（拖到地面）与拾取（右键地上的装备）。</summary>
    public enum EquipmentCommandKind { Buy,Sell,Move,Drop,PickUp }
    /// <summary>输入层或英雄指令提交的装备意图，由战斗宿主在模拟帧内串行处理。</summary>
    [InternalBufferCapacity(4)]
    public struct EquipmentCommand : IBufferElementData
    {
        public EquipmentCommandKind Kind;
        /// <summary>购买：装备下标；出售 / 丢弃：装备栏格；换位：起始格；拾取：地上装备编号。</summary>
        public int Value;
        /// <summary>换位：目标格；丢弃：下令时该格的装备下标（装备已被换到其他格时按装备查找，-1 表示不核对）。</summary>
        public int Argument;
        /// <summary>丢弃：地面落点。</summary>
        public float3 Point;
    }
    public enum EquipmentFailure { None,Dead,InvalidItem,NotEnoughGold,InventoryFull,AlreadyOwned,InvalidSlot,EmptySlot,NotInShop,TooFar,GroundItemMissing }

    /// <summary>金币、购买、出售、换位、丢弃、拾取、商店范围与装备属性合计的共同规则。不识别具体装备或英雄。</summary>
    public static class EquipmentLogic
    {
        /// <summary>装备命中效果的来源编号起点（+ 装备下标）；与技能下标、旧被动 1000+ 区分，用于“按来源合并”的 Buff。</summary>
        public const int OnHitOrigin=3000;
        /// <summary>丢弃 / 拾取距离判断的浮点余量（米）。</summary>
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
        /// <summary>按编号查找地上的装备，返回缓冲下标；不存在时返回 -1。</summary>
        public static int FindGround(DynamicBuffer<GroundItem> ground,int id)
        {
            if(id<=0) return -1;
            for(int i=0;i<ground.Length;i++) if(ground[i].Id==id) return i;
            return -1;
        }
        /// <summary>英雄是否在商店范围内（平面距离）；不限距离的商店总是返回 true。</summary>
        public static bool InShop(in ShopSite shop,float3 position)
            =>!shop.Limited || math.distancesq(shop.Position.xz,position.xz)<=shop.Radius*shop.Radius;
        /// <summary>两点平面距离是否不超过 range（含 <see cref="RangeSlack"/> 余量）。</summary>
        public static bool Within(float3 a,float3 b,float range)
            =>math.distancesq(a.xz,b.xz)<=(range+RangeSlack)*(range+RangeSlack);
        /// <summary>记录一条被拒绝的指令（例如不在商店范围内买卖）：不改变金币、装备栏与地上装备。</summary>
        public static void Reject(ref HeroEquipment equipment,EquipmentCommandKind kind,int item,EquipmentFailure failure)
        {
            equipment.CommandSerial++; equipment.LastCommand=kind; equipment.LastItem=item; equipment.LastGround=0; equipment.LastFailure=failure;
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
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Buy; equipment.LastItem=item; equipment.LastGround=0;
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
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Sell; equipment.LastItem=-1; equipment.LastGround=0;
            if(slot<0 || slot>=slots.Length) { equipment.LastFailure=EquipmentFailure.InvalidSlot; return false; }
            int item=slots[slot].Item;
            if(item<0 || !equipment.Library.IsCreated || item>=equipment.Library.Value.Items.Length) { equipment.LastFailure=EquipmentFailure.EmptySlot; return false; }
            equipment.LastItem=item;
            equipment.Gold+=equipment.Library.Value.Items[item].SellPrice;
            slots[slot]=new EquipmentSlot{Item=-1};
            equipment.LastFailure=EquipmentFailure.None; equipment.ChangeSerial++;
            return true;
        }

        /// <summary>换位（Dota 2 拖动装备到另一格）：目标格为空时移过去，有装备时两格交换。属性合计不变。</summary>
        public static bool TryMove(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,int from,int to)
        {
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Move; equipment.LastItem=-1; equipment.LastGround=0;
            if(from<0 || from>=slots.Length || to<0 || to>=slots.Length) { equipment.LastFailure=EquipmentFailure.InvalidSlot; return false; }
            int item=slots[from].Item;
            if(item<0) { equipment.LastFailure=EquipmentFailure.EmptySlot; return false; }
            equipment.LastItem=item; equipment.LastFailure=EquipmentFailure.None;
            if(from==to) return false;
            slots[from]=slots[to]; slots[to]=new EquipmentSlot{Item=item};
            equipment.ChangeSerial++;
            return true;
        }

        /// <summary>
        /// 丢弃（Dota 2 拖动装备到地面）：英雄与落点的平面距离不超过丢弃距离时，把装备放到落点、生成一件地上的装备。
        /// expected 为下令时该格的装备下标：期间装备被换到其他格时按装备查找，已不在装备栏时失败。成功后调用方重算英雄属性。
        /// </summary>
        public static bool TryDrop(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,DynamicBuffer<GroundItem> ground,int slot,int expected,float3 point,float3 hero)
        {
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.Drop; equipment.LastItem=expected; equipment.LastGround=0;
            if(expected>=0 && (slot<0 || slot>=slots.Length || slots[slot].Item!=expected)) slot=SlotOf(slots,expected);
            if(slot<0 || slot>=slots.Length) { equipment.LastFailure=expected>=0?EquipmentFailure.EmptySlot:EquipmentFailure.InvalidSlot; return false; }
            int item=slots[slot].Item;
            if(item<0 || !equipment.Library.IsCreated || item>=equipment.Library.Value.Items.Length) { equipment.LastFailure=EquipmentFailure.EmptySlot; return false; }
            equipment.LastItem=item;
            if(!Within(hero,point,equipment.Library.Value.DropRange)) { equipment.LastFailure=EquipmentFailure.TooFar; return false; }
            int id=++equipment.NextGroundId;
            ground.Add(new GroundItem{Id=id,Item=item,Position=new float3(point.x,0,point.z)});
            slots[slot]=new EquipmentSlot{Item=-1};
            equipment.LastGround=id; equipment.LastFailure=EquipmentFailure.None; equipment.ChangeSerial++;
            return true;
        }

        /// <summary>
        /// 只读判断能否拾取（界面与指令处理共用）：地上的装备存在、装备栏有空格、唯一装备未拥有；checkRange 为真时还要求英雄在拾取距离内。
        /// </summary>
        public static EquipmentFailure CanPickUp(in HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,DynamicBuffer<GroundItem> ground,int id,float3 hero,bool checkRange)
        {
            int index=FindGround(ground,id);
            if(index<0) return EquipmentFailure.GroundItemMissing;
            int item=ground[index].Item;
            if(!equipment.Library.IsCreated || item<0 || item>=equipment.Library.Value.Items.Length) return EquipmentFailure.InvalidItem;
            if(equipment.Library.Value.Items[item].Unique && Owns(slots,item)) return EquipmentFailure.AlreadyOwned;
            if(FreeSlot(slots)<0) return EquipmentFailure.InventoryFull;
            if(checkRange && !Within(hero,ground[index].Position,equipment.Library.Value.PickupRange)) return EquipmentFailure.TooFar;
            return EquipmentFailure.None;
        }

        /// <summary>拾取（Dota 2 右键地上的装备）：放入第一个空格并从地上移除。成功后调用方重算英雄属性。</summary>
        public static bool TryPickUp(ref HeroEquipment equipment,DynamicBuffer<EquipmentSlot> slots,DynamicBuffer<GroundItem> ground,int id,float3 hero)
        {
            equipment.CommandSerial++; equipment.LastCommand=EquipmentCommandKind.PickUp; equipment.LastGround=id;
            int index=FindGround(ground,id);
            equipment.LastItem=index>=0?ground[index].Item:-1;
            var failure=CanPickUp(equipment,slots,ground,id,hero,true);
            equipment.LastFailure=failure;
            if(failure!=EquipmentFailure.None) return false;
            slots[FreeSlot(slots)]=new EquipmentSlot{Item=ground[index].Item};
            ground.RemoveAt(index);
            equipment.ChangeSerial++;
            return true;
        }

        /// <summary>装备栏全部装备的属性合计（直接相加）。</summary>
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
                case EquipmentFailure.NotInShop: return "不在商店范围内";
                case EquipmentFailure.TooFar: return "距离太远";
                case EquipmentFailure.GroundItemMissing: return "地上的装备已不存在";
            }
            return failure.ToString();
        }
    }
}
