using System;
using Roguelike.Abilities;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>事件选项的结果。所有数值由远征宿主通过共享的金币、经验、装备与遗物规则结算。</summary>
    [Serializable]
    public sealed class EventOutcome
    {
        [Tooltip("获得（正）或失去（负）金币。")] public int gold;
        [Range(-1,1), Tooltip("按最大生命比例回复（正）或损失（负）；损失最多降到 1 点生命，不会致死。")] public float health;
        [Min(0), Tooltip("获得经验。")] public float experience;
        [Tooltip("获得一件尚未拥有的随机遗物。")] public bool relic;
        [Tooltip("免费获得一件指定品质的随机装备；装备栏已满时改为返还等价金币。")] public bool item;
        public EquipmentQuality itemQuality=EquipmentQuality.Rare;
        [Range(0,1), Tooltip("下一场战斗的敌人生命加成（0.2 = 20%），只生效一次。")] public float nextFightHealth;
        [TextArea(1,3), Tooltip("选择后显示的结果说明。")] public string text;
    }

    /// <summary>事件的一个选项：可要求金币，可按概率在成功与失败结果之间二选一。</summary>
    [Serializable]
    public sealed class EventChoice
    {
        public string label;
        [Min(0), Tooltip("选择该项需要并消耗的金币；金币不足时选项不可用。")] public int goldCost;
        [Range(0,1), Tooltip("成功概率；1 表示必定成功。")] public float chance=1;
        public EventOutcome success=new EventOutcome();
        public EventOutcome failure=new EventOutcome();
    }

    /// <summary>路线图上的特殊事件：标题、描述与最多三个选项。同一局内不会重复遇到同一事件，直到全部用完。</summary>
    [Serializable]
    public sealed class ExpeditionEvent
    {
        public const int MaxChoices=3;
        public string id,title;
        [TextArea(2,6)] public string body;
        public EventChoice[] choices=Array.Empty<EventChoice>();

        static EventChoice Choice(string label,EventOutcome success,int cost=0,float chance=1,EventOutcome failure=null)
            =>new EventChoice{label=label,goldCost=cost,chance=chance,success=success,failure=failure??new EventOutcome()};
        static EventOutcome Leave(string text)=>new EventOutcome{text=text};

        /// <summary>默认事件表；配置资源未保存该字段时使用，保存后可在 Inspector 中直接修改。</summary>
        public static ExpeditionEvent[] Defaults()=>new[]
        {
            new ExpeditionEvent{id="event.blood_altar",title="鲜血祭坛",body="荒野里立着一座染血的石坛，低沉的声音许诺：以血为价，赐你力量。",
                choices=new[]{Choice("献血：失去 25% 最大生命，获得一件随机遗物",new EventOutcome{health=-.25f,relic=true,text="鲜血渗入石缝，一件遗物浮现在你手中。"}),
                    Choice("离开",Leave("你没有理会祭坛的低语。"))}},
            new ExpeditionEvent{id="event.wandering_merchant",title="流浪商人",body="一个背着巨大行囊的商人拦住去路：“只要 120 金币，包里的东西就归你。”",
                choices=new[]{Choice("花 120 金购买神秘包裹：获得一件随机稀有装备",new EventOutcome{item=true,itemQuality=EquipmentQuality.Rare,text="包裹里是一件做工精良的装备。"},120),
                    Choice("离开",Leave("商人耸耸肩，消失在雾里。"))}},
            new ExpeditionEvent{id="event.gambler_dice",title="赌徒的骰子",body="篝火旁的赌徒晃着骰子：“押 100 金，掷出大点就翻倍还多。”",
                choices=new[]{Choice("押 100 金：50% 机会赢回 260 金",new EventOutcome{gold=260,text="骰子停在六点！你赢回 260 金。"},100,.5f,
                        new EventOutcome{text="骰子停在一点，金币打了水漂。"}),
                    Choice("不赌了",Leave("你摆摆手，继续赶路。"))}},
            new ExpeditionEvent{id="event.moon_spring",title="月光泉水",body="林间的泉水在月光下泛着银光，水面倒映出你疲惫的样子。",
                choices=new[]{Choice("饮下泉水：回复 40% 最大生命",new EventOutcome{health=.4f,text="清凉的泉水抚平了伤口。"}),
                    Choice("在泉边冥想：获得 120 经验",new EventOutcome{experience=120,text="你在宁静中领悟了新的战斗技巧。"})}},
            new ExpeditionEvent{id="event.wounded_adventurer",title="受伤的冒险者",body="一名冒险者倒在路边，身旁散落着一袋金币。",
                choices=new[]{Choice("护送他回营地：失去 15% 最大生命，获得 150 金",new EventOutcome{health=-.15f,gold=150,text="路上遭遇伏击，但他兑现了酬谢。"}),
                    Choice("拿走金币：获得 60 金，下一场战斗敌人生命 +20%",new EventOutcome{gold=60,nextFightHealth=.2f,text="你拿走了金币，远处传来愤怒的嚎叫。"}),
                    Choice("离开",Leave("你绕开了这片是非之地。"))}},
            new ExpeditionEvent{id="event.sealed_chest",title="封印宝箱",body="一只缠满锁链的宝箱静静躺在废墟中，锁链上刻着警告的符文。",
                choices=new[]{Choice("强行撬开：获得 200 金，下一场战斗敌人生命 +25%",new EventOutcome{gold=200,nextFightHealth=.25f,text="锁链断裂，金币倾泻而出，封印的力量却惊动了怪物。"}),
                    Choice("离开",Leave("你决定不去碰它。"))}},
            new ExpeditionEvent{id="event.veteran_trainer",title="老兵教官",body="一位独臂老兵坐在路边磨刀：“付我 80 金，我教你几招保命的本事。”",
                choices=new[]{Choice("支付 80 金接受训练：获得 150 经验",new EventOutcome{experience=150,text="一番苦练后，你的出手更加凌厉。"},80),
                    Choice("离开",Leave("老兵继续低头磨刀。"))}},
        };
    }
}
