using System;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;
namespace Roguelike.Editor
{
    public static class SpawnDirectorTools
    {
        [MenuItem("Tools/Roguelike/打开刷怪导演")]
        public static void Open()
        {
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<SpawnDirectorDefinition>("Assets/GameData/关卡/刷怪导演.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }
    }
    [CustomEditor(typeof(CombatDifficultyDefinition))]
    internal sealed class CombatDifficultyChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("t = 4.5 ×（关卡 − 1）+ 本关分钟。这里只配置数值，刷怪数量和节奏在刷怪导演资源。重开生效。",MessageType.Info);
            string[] names={"baseEnemyHealth","baseEnemyDamage","baseEnemySpeed","healthGrowth","damageGrowth","speedPerMinute","maxSpeed","baseExperience","baseGold","eliteHealth","eliteDamage","eliteScale","eliteExperience","eliteGold","bossHealth","bossDamage","bossScale","bossExperience","bossGold","bossSpeed"};
            string[] labels={"初始生命（点）","初始攻击（点）","初始移速（米/秒）","每分钟生命倍率","每分钟攻击倍率","每分钟移速增加（米/秒）","基准移速上限（米/秒）","杂兵经验","杂兵金币","精英生命倍率","精英攻击倍率","精英体型倍率","精英绝对经验","精英绝对金币","首领生命（第 1/2/3 关）","首领攻击倍率","首领体型倍率","首领经验（第 1/2/3 关）","首领金币（第 1/2/3 关）","首领移速倍率"};
            for(int i=0;i<names.Length;i++) EditorGUILayout.PropertyField(serializedObject.FindProperty(names[i]),new GUIContent(labels[i]),true);
            serializedObject.ApplyModifiedProperties();
            try
            {
                var d=(CombatDifficultyDefinition)target; var settings=d.ToSettings();
                var c=new CombatSettings{EnemyHealth=d.baseEnemyHealth,EnemyAttackDamage=d.baseEnemyDamage,EnemySpeed=d.baseEnemySpeed};
                foreach(float t in new[]{0,1,4.5f,9,13.5f})
                {
                    var v=CombatSpawnDirector.Stats(settings,c,t,1,EnemyTier.Normal);
                    EditorGUILayout.LabelField($"{t:0.#} 分钟：生命 {v.Health:0.#} / 攻击 {v.Damage:0.#} / 移速 {v.Speed:0.##}");
                }
            }
            catch(InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message,MessageType.Error); }
        }
    }
    [CustomEditor(typeof(SpawnDirectorDefinition))]
    internal sealed class SpawnDirectorChineseInspector : UnityEditor.Editor
    {
        private void OnEnable() { serializedObject.FindProperty("population").isExpanded=true; }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("目标曲线按累计强度时间线性插值。数量是存活目标；每批预算包含潮涌、精英和首领。X/Y/Z 分别对应第 1/2/3 关。重开生效。",MessageType.Info);
            string[] names={"population","tickSeconds","aliveLimit","batchBudget","stageSeconds","openingFraction","recoverySeconds","bossAt","bossPopulationFraction","enrageAt","tideAt","tideCounts","eliteFirstAt","eliteIntervals","ringWidth","cameraPadding","forwardBias","separationCell","separationSpeed","separationChecks"};
            string[] labels={"目标存活曲线","补怪间隔（秒）","存活硬上限（只）","每批生成预算（只）","每关强度偏移（秒）","开局目标比例","开局恢复时长（秒）","首领出现（本关秒）","首领阶段补充比例","首领狂暴（本关秒）","潮涌时刻（本关秒）","潮涌数量（第 1/2/3 关）","首次精英（本关秒）","精英间隔（秒）","镜头外环带宽（米）","镜头外留白（米）","移动前方生成概率","分离网格边长（米）","分离速度上限（米/秒）","每单位邻居检查上限"};
            for(int i=0;i<names.Length;i++)
            {
                var property=serializedObject.FindProperty(names[i]);
                EditorGUILayout.PropertyField(property,new GUIContent(labels[i],property.tooltip),true);
            }
            serializedObject.ApplyModifiedProperties();
            try { ((SpawnDirectorDefinition)target).Compile(); EditorGUILayout.HelpBox("导演配置有效。",MessageType.None); }
            catch(InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message,MessageType.Error); }
        }
    }
    [CustomPropertyDrawer(typeof(PopulationKey))]
    internal sealed class PopulationKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
            float width=position.width/2;
            float previous=EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth=100;
            EditorGUI.PropertyField(new Rect(position.x,position.y,width-8,position.height),property.FindPropertyRelative("minute"),new GUIContent("强度时间（分）"));
            EditorGUI.PropertyField(new Rect(position.x+width,position.y,width,position.height),property.FindPropertyRelative("alive"),new GUIContent("存活目标（只）"));
            EditorGUIUtility.labelWidth=previous; EditorGUI.EndProperty();
        }
    }
}
