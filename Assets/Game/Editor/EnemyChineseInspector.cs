using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>敌人种类预制体的中文 Inspector：按行为只显示相关参数，并实时校验配置。</summary>
    [CustomEditor(typeof(EnemyArchetype))]
    [CanEditMultipleObjects]
    internal sealed class EnemyArchetypeChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("敌人种类预制体：子物体的网格会在开局合并成一个 ECS 网格（第一个 MeshRenderer 的材质与颜色为该种类的颜色），"+
                "预制体不会被实例化成 GameObject。数值是乘在当前波次基础值上的倍率。加入“敌人名册”后才会出场，重开 Game 生效。",MessageType.Info);
            Section("身份");
            Field("configId","数字配置 ID","怪物使用 3000–3999，需登记在 Docs/游戏配置/编号登记.json。");
            Field("stableId","内部兼容键");
            Field("displayName","显示名称");
            Field("description","说明");
            Section("行为与攻击");
            Field("behavior","行为");
            Field("attack","自动攻击模块","与英雄共用的被动攻击模块：距离、前摇、后摇、基础攻击间隔。");
            Section("数值倍率（乘以当前波次基础值）");
            Field("healthMultiplier","生命倍率");
            Field("damageMultiplier","攻击力倍率");
            Field("speedMultiplier","移动速度倍率");
            Field("experienceMultiplier","击杀经验倍率");
            Field("bodyScale","体型倍率","影响模型缩放、碰撞半径与点选范围。");
            var behavior=serializedObject.FindProperty("behavior");
            bool mixed=behavior==null || behavior.hasMultipleDifferentValues;
            int value=behavior?.enumValueIndex??0;
            if(mixed || value==(int)EnemyBehavior.Ranged)
            {
                Section("远程游击");
                Field("preferredDistance","站位比例","停在攻击距离 × 该比例处射击。");
                Field("retreatDistance","后撤距离（米）","英雄靠近到该距离以内时后退。");
            }
            if(mixed || value==(int)EnemyBehavior.Charger)
            {
                Section("冲锋");
                Field("chargeTriggerRange","触发距离（米）");
                Field("chargeSpeedMultiplier","冲锋速度倍率");
                Field("chargeDuration","冲锋时长（秒）");
                Field("chargeCooldown","冲锋冷却（秒）");
                Field("chargeDamageMultiplier","撞击伤害倍率");
            }
            if(mixed || value==(int)EnemyBehavior.Exploder)
            {
                Section("自爆");
                Field("explosionRadius","爆炸半径（米）");
                Field("explosionDamageMultiplier","爆炸伤害倍率");
            }
            serializedObject.ApplyModifiedProperties();
            if(targets.Length==1) Report((EnemyArchetype)target);
        }

        private static void Report(EnemyArchetype archetype)
        {
            Section("校验");
            try
            {
                archetype.Compile(out var attack);
                int parts=archetype.GetComponentsInChildren<MeshFilter>(true).Length;
                string delivery=attack.Delivery==AttackDelivery.Projectile?$"弹道（{attack.ProjectileSpeed:0.#} 米/秒）":"近战";
                EditorGUILayout.HelpBox($"配置有效。{parts} 个网格部件；攻击：{delivery}，距离 {attack.Range:0.##} 米，"+
                    $"每秒 {1/attack.BaseAttackTime:0.##} 次，前摇 {attack.AttackPoint:0.##} 秒。",MessageType.None);
                if(!archetype.ResolveMaterial()) EditorGUILayout.HelpBox("缺少 MeshRenderer 材质。",MessageType.Error);
                if(parts==0) EditorGUILayout.HelpBox("缺少 MeshFilter 网格。",MessageType.Error);
            }
            catch(InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message,MessageType.Error); }
        }

        private void Field(string name,string label,string tooltip=null)
        {
            var property=serializedObject.FindProperty(name);
            if(property==null) { EditorGUILayout.HelpBox("缺少字段："+name,MessageType.Error); return; }
            EditorGUILayout.PropertyField(property,new GUIContent(label,tooltip??property.tooltip),true);
        }

        internal static void Section(string text) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(text,EditorStyles.boldLabel); }
    }

    /// <summary>敌人名册的中文 Inspector：逐条编辑出场规则，校验名册，并预览关键波次各档位的出场概率。</summary>
    [CustomEditor(typeof(EnemyRoster))]
    internal sealed class EnemyRosterChineseInspector : UnityEditor.Editor
    {
        private static readonly int[] PreviewWaves={1,3,5,10,20};

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("每波刷怪时，按档位（普通/精英/首领）筛选当前波次允许出场的条目，再按权重抽取种类。"+
                "某档位在当前波次没有可出场条目时，退回到允许该档位的第一个条目。至少需要一条第 1 波普通、一条精英、一条首领条目。重开 Game 生效。",MessageType.Info);
            var entries=serializedObject.FindProperty("entries");
            EnemyArchetypeChineseInspector.Section($"出场条目（{entries.arraySize}）");
            int remove=-1;
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);
                var prefab=entry.FindPropertyRelative("prefab");
                var archetype=prefab.objectReferenceValue as EnemyArchetype;
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{i+1}. {(archetype?archetype.Label:"（未指定）")}",EditorStyles.boldLabel);
                        if(GUILayout.Button("删除",GUILayout.Width(48))) remove=i;
                    }
                    Relative(entry,"prefab","敌人预制体");
                    Relative(entry,"weight","权重");
                    Relative(entry,"firstWave","最早波次");
                    Relative(entry,"lastWave","最晚波次（0=不限）");
                    Relative(entry,"tiers","允许档位");
                }
            }
            if(remove>=0) entries.DeleteArrayElementAtIndex(remove);
            if(GUILayout.Button("添加条目"))
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                var added=entries.GetArrayElementAtIndex(entries.arraySize-1);
                added.FindPropertyRelative("prefab").objectReferenceValue=null;
                added.FindPropertyRelative("weight").floatValue=1;
                added.FindPropertyRelative("firstWave").intValue=1;
                added.FindPropertyRelative("lastWave").intValue=0;
                added.FindPropertyRelative("tiers").intValue=(int)EnemyTierMask.Normal;
            }
            EnemyArchetypeChineseInspector.Section("档位色调");
            Field("eliteTint","精英色调"); Field("eliteTintStrength","精英色调强度");
            Field("bossTint","首领色调"); Field("bossTintStrength","首领色调强度");
            serializedObject.ApplyModifiedProperties();
            var roster=(EnemyRoster)target;
            EnemyArchetypeChineseInspector.Section("校验与出场预览");
            string failure=roster.Validate();
            if(failure!=null) { EditorGUILayout.HelpBox(failure,MessageType.Error); return; }
            EditorGUILayout.HelpBox("名册有效。",MessageType.None);
            foreach(int wave in PreviewWaves)
                EditorGUILayout.LabelField($"第 {wave} 波：普通 {Odds(roster,EnemyTier.Normal,wave)}；精英 {Odds(roster,EnemyTier.Elite,wave)}；首领 {Odds(roster,EnemyTier.Boss,wave)}",
                    EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>与 CombatWaveDirector.Pick 相同的规则：按波次与档位筛选后按权重抽取；没有时退回允许该档位的第一个条目，再退回普通档。</summary>
        internal static string Odds(EnemyRoster roster,EnemyTier tier,int wave)
        {
            var weights=new Dictionary<string,float>(); float total=0;
            foreach(var entry in roster.entries)
                if(entry.Allows(tier,wave)) { Add(weights,entry); total+=entry.weight; }
            if(total<=0)
            {
                foreach(var entry in roster.entries)
                    if(entry.prefab && (entry.tiers&EnemySpawnEntry.Mask(tier))!=0) return entry.prefab.Label+" 100%（退回）";
                return tier==EnemyTier.Normal?"无":Odds(roster,EnemyTier.Normal,wave);
            }
            var parts=new List<string>();
            foreach(var pair in weights) parts.Add($"{pair.Key} {pair.Value/total:P0}");
            return string.Join("、",parts);
        }

        private static void Add(Dictionary<string,float> weights,EnemySpawnEntry entry)
        {
            string key=entry.prefab.Label;
            weights[key]=(weights.TryGetValue(key,out float value)?value:0)+entry.weight;
        }

        private static void Relative(SerializedProperty parent,string name,string label)
        {
            var property=parent.FindPropertyRelative(name);
            if(property==null) { EditorGUILayout.HelpBox("缺少字段："+name,MessageType.Error); return; }
            EditorGUILayout.PropertyField(property,new GUIContent(label,property.tooltip));
        }

        private void Field(string name,string label)
        {
            var property=serializedObject.FindProperty(name);
            if(property==null) { EditorGUILayout.HelpBox("缺少字段："+name,MessageType.Error); return; }
            EditorGUILayout.PropertyField(property,new GUIContent(label,property.tooltip));
        }
    }
}
