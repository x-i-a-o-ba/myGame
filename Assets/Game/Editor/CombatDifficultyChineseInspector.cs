using System;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>关卡难度曲线的中文 Inspector：分组显示中文字段，并预览关键波次的数量与倍率。</summary>
    [CustomEditor(typeof(CombatDifficultyDefinition))]
    internal sealed class CombatDifficultyChineseInspector : UnityEditor.Editor
    {
        private static readonly int[] PreviewWaves={1,5,10,20,30};

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("每隔固定秒数刷一波敌人，数量、生命、伤害、移速和经验按波次增长；定期出现精英与首领，击杀最终波首领获胜。"+
                "第 1 波的生命、伤害和移速取自 Game 场景战斗控制器的敌人基础值。重开 Game 后生效。",MessageType.Info);
            Section("节奏");
            Field("waveInterval","波次间隔（秒）");
            Field("firstWaveAt","第一波时间（秒）");
            Field("finalWave","最终波");
            Field("spawnRingWidth","刷怪圆环宽度（米）");
            Section("普通敌人");
            Field("baseCount","第 1 波数量");
            Field("countGrowthPerWave","每波数量增长比例");
            Field("maxPerWave","单波数量上限");
            Field("healthGrowth","每波生命倍率");
            Field("damageGrowth","每波伤害倍率");
            Field("speedPerWave","每波移速增加（米/秒）");
            Field("maxSpeed","移速上限（米/秒）");
            Field("baseExperience","第 1 波击杀经验");
            Field("experienceGrowth","每波经验倍率");
            Section("精英");
            Field("eliteEvery","每隔几波出现");
            Field("eliteBaseCount","基础数量");
            Field("eliteWavesPerExtra","每隔几波数量 +1");
            Field("eliteHealth","生命倍率");
            Field("eliteDamage","伤害倍率");
            Field("eliteScale","体型倍率");
            Field("eliteExperience","经验倍率");
            Section("首领");
            Field("bossEvery","每隔几波出现");
            Field("bossHealth","生命倍率");
            Field("bossDamage","伤害倍率");
            Field("bossScale","体型倍率");
            Field("bossExperience","经验倍率");
            Field("bossSpeed","移速倍率");
            serializedObject.ApplyModifiedProperties();
            Preview((CombatDifficultyDefinition)target);
        }

        private void Field(string name,string label)
        {
            var property=serializedObject.FindProperty(name);
            if(property==null) { EditorGUILayout.HelpBox("缺少字段："+name,MessageType.Error); return; }
            EditorGUILayout.PropertyField(property,new GUIContent(label,property.tooltip));
        }

        private static void Section(string text) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(text,EditorStyles.boldLabel); }

        private static void Preview(CombatDifficultyDefinition definition)
        {
            Section("波次预览");
            CombatWaveSettings settings;
            try { settings=definition.ToSettings(); }
            catch(InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message,MessageType.Error); return; }
            // 以 1 为基础值，预览结果即相对第 1 波的倍率；移速以 0 为基础，显示累计增量。
            var unit=new CombatSettings{EnemyHealth=1,EnemyAttackDamage=1,EnemySpeed=0};
            float duration=settings.FinalWave>0?settings.FirstWaveAt+settings.WaveInterval*(settings.FinalWave-1):0;
            EditorGUILayout.LabelField(settings.FinalWave>0?$"最终波第 {settings.FinalWave} 波，约 {duration/60:0.#} 分钟出现":"无尽模式（没有最终波）",EditorStyles.miniLabel);
            foreach(int wave in PreviewWaves)
            {
                if(settings.FinalWave>0 && wave>settings.FinalWave) continue;
                var normal=CombatWaveDirector.Stats(settings,unit,wave,EnemyTier.Normal);
                var elite=CombatWaveDirector.Stats(settings,unit,wave,EnemyTier.Elite);
                var boss=CombatWaveDirector.Stats(settings,unit,wave,EnemyTier.Boss);
                string text=$"第 {wave} 波：普通 {normal.Count} 只，生命 ×{normal.Health:0.##}，伤害 ×{normal.Damage:0.##}，移速 +{normal.Speed:0.##}，经验 {normal.Experience:0.#}";
                if(elite.Count>0) text+=$"；精英 {elite.Count} 只（经验 {elite.Experience:0.#}）";
                if(boss.Count>0) text+=$"；首领（生命 ×{boss.Health:0.#}，经验 {boss.Experience:0}）";
                EditorGUILayout.LabelField(text,EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
