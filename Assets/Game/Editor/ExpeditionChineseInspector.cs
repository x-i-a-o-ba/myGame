using System.Collections.Generic;
using Roguelike.Combat;
using Roguelike.Flow;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    internal abstract class ExpeditionChineseInspector:UnityEditor.Editor
    {
        internal static readonly Dictionary<string,string> Labels=new Dictionary<string,string>
        {
            {"m_Script","脚本"},{"stableId","稳定编号"},{"displayName","显示名称"},{"description","说明"},{"icon","图标"},
            {"template","执行模板"},{"deathAction","死亡后的动作"},{"shape","查询形状"},{"cooldown","冷却（秒，从预警开始计算）"},
            {"warningSeconds","预警时间（秒）"},{"triggerRange","触发距离（米）"},{"radius","半径 / 直线半宽（米）"},{"range","长度 / 最大飞行距离（米）"},
            {"halfAngle","扇形半角（度）"},{"innerRadius","圆环内半径（米）"},{"minimumStage","最早使用关卡"},{"atHero","锁定英雄当前位置"},
            {"sacrifice","释放后自身死亡"},{"damageRatio","攻击力伤害倍率（持续区域按每秒）"},{"healMaxHealth","每秒回复最大生命比例（0.03 = 3%）"},
            {"status","命中施加状态"},{"knockback","命中击退（米）"},{"duration","持续时间（秒）"},{"interval","持续结算间隔（秒）"},
            {"speed","投射 / 冲锋速度（米/秒）"},{"projectileCount","弹体数量"},{"spreadDegrees","完整散射角度（度）"},{"projectileAtPoint","弹体到锁定落点爆炸"},
            {"trailSeconds","路径 / 落点残留时间（秒，0 关闭）"},{"trailDamageRatio","残留每秒攻击力倍率"},{"summon","召唤或分裂预制体"},{"summonCount","召唤数量"},
            {"summonHealthRatio","子怪生命占来源比例（0 使用基础值）"},{"repeatCount","重复次数"},{"repeatInterval","重复间隔（秒）"},
            {"repeatOffset","重复落点分布半径（米）"},{"expandingRing","圆环随时间向外扩散"},{"color","预警与区域颜色"},
            {"extraBasicTargets","普攻附加目标数"},{"echoChance","施法返还冷却概率（0–1）"},{"killHealMaxHealth","击杀回复最大生命比例（0–1）"},
            {"skillHitBuff","技能命中附加状态"},{"controlDurationMultiplier","减速和控制持续时间倍率"},{"bodyScale","体型倍率"},
            {"maxHealthMultiplier","最大生命倍率"},{"basicRangeMultiplier","普攻距离倍率"},{"eliteGoldMultiplier","精英与首领金币倍率"},
            {"cooldownReduction","冷却缩减加成（百分点）"},{"cooldownCap","冷却缩减上限（%）"},
            {"forward","向前移动"},{"backward","向后移动"},{"left","向左移动"},{"right","向右移动"},{"abilities","技能槽键位（顺序 1–4）"},
            {"statistics","属性面板键"},{"pause","暂停键"},{"selection","开局选择目录"},{"relics","遗物池"},{"passiveGoldPerSecond","战斗每秒金币"},
            {"victoryNoticeSeconds","战斗胜利后过场等待（秒）"},
            {"route","路线图"},{"events","特殊事件"},{"floors","每幕层数（含首领层）"},{"paths","生成路线数"},
            {"combatWeight","战斗节点权重"},{"eliteWeight","精英节点权重"},{"shopWeight","商店节点权重"},{"eventWeight","事件节点权重"},{"restWeight","营火节点权重"},
            {"eliteMinFloor","精英最早出现层"},{"shopMinFloor","商店最早出现层"},{"restMinFloor","营火最早出现层"},
            {"actMinutes","每幕强度时间间隔（分钟）"},{"minutesPerFloor","每层增加强度时间（分钟）"},{"killGoals","击杀目标（x 强度分钟，y 击杀数）"},
            {"elitesPerAct","精英战精英数（按幕）"},{"combatGold","普通战斗额外金币"},{"eliteGold","精英战额外金币"},
            {"restHeal","营火休息回复最大生命比例"},{"restTraining","营火训练经验比例（本级所需 × 比例）"},
            {"id","事件编号"},{"title","标题"},{"body","描述"},{"choices","选项"},{"label","选项文字"},{"goldCost","金币花费"},
            {"chance","成功概率（0–1）"},{"success","成功结果"},{"failure","失败结果"},{"gold","金币变化（负数为失去）"},
            {"health","生命变化（最大生命比例，负数为损失）"},{"experience","获得经验"},{"relic","获得随机遗物"},{"item","获得随机装备"},
            {"itemQuality","装备品质"},{"nextFightHealth","下一场战斗敌人生命加成"},{"text","结果说明"}
        };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("配置在新会话中生效。运行状态由单位和局内状态持有，不会写回资源。距离单位为米，概率 0–1。",MessageType.Info);
            var property=serializedObject.GetIterator();bool child=true;
            while(property.NextVisible(child))
            {
                child=false;
                using(new EditorGUI.DisabledScope(property.name=="m_Script"))
                    EditorGUILayout.PropertyField(property,new GUIContent(Labels.TryGetValue(property.name,out string label)?label:property.displayName,property.tooltip),true);
            }
            serializedObject.ApplyModifiedProperties();
            try {if(target is CombatInputProfile input) input.Validate();if(target is RelicDefinition relic) relic.Validate();}
            catch(System.Exception e) {EditorGUILayout.HelpBox(e.Message,MessageType.Error);}
        }
    }
    [CustomEditor(typeof(MonsterAbilityDefinition))] internal sealed class MonsterAbilityInspector:ExpeditionChineseInspector { }
    [CustomEditor(typeof(RelicDefinition))] internal sealed class RelicInspector:ExpeditionChineseInspector { }
    [CustomEditor(typeof(CombatInputProfile))] internal sealed class CombatInputInspector:ExpeditionChineseInspector { }
    [CustomEditor(typeof(ExpeditionCatalog))] internal sealed class ExpeditionCatalogInspector:ExpeditionChineseInspector { }
    /// <summary>远征配置中嵌套的路线图与事件字段：逐项使用中文标签绘制。</summary>
    [CustomPropertyDrawer(typeof(RouteMapSettings)),CustomPropertyDrawer(typeof(ExpeditionEvent)),CustomPropertyDrawer(typeof(EventChoice)),CustomPropertyDrawer(typeof(EventOutcome))]
    internal sealed class ExpeditionNestedDrawer:PropertyDrawer
    {
        static float Spacing=>EditorGUIUtility.standardVerticalSpacing;
        public override float GetPropertyHeight(SerializedProperty property,GUIContent label)
        {
            float height=EditorGUIUtility.singleLineHeight;
            if(!property.isExpanded) return height;
            var iterator=property.Copy();var end=property.GetEndProperty();bool enter=true;
            while(iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator,end))
            {enter=false;height+=EditorGUI.GetPropertyHeight(iterator,true)+Spacing;}
            return height;
        }
        public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
        {
            var line=new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
            var title=property.FindPropertyRelative("title");
            var caption=title!=null && !string.IsNullOrEmpty(title.stringValue)?new GUIContent(title.stringValue,label.tooltip):label;
            property.isExpanded=EditorGUI.Foldout(line,property.isExpanded,caption,true);
            if(!property.isExpanded) return;
            EditorGUI.indentLevel++;
            float y=line.yMax+Spacing;
            var iterator=property.Copy();var end=property.GetEndProperty();bool enter=true;
            while(iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator,end))
            {
                enter=false;float height=EditorGUI.GetPropertyHeight(iterator,true);
                var text=ExpeditionChineseInspector.Labels.TryGetValue(iterator.name,out string chinese)?chinese:iterator.displayName;
                EditorGUI.PropertyField(new Rect(position.x,y,position.width,height),iterator,new GUIContent(text,iterator.tooltip),true);
                y+=height+Spacing;
            }
            EditorGUI.indentLevel--;
        }
    }
}
