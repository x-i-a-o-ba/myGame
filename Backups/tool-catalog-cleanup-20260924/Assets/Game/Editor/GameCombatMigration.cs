using System;
using System.IO;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>显式将 Game 接入共享能力库。只填空引用，不覆盖已保存的英雄、数值或布局。</summary>
    public static class GameCombatMigration
    {
        private const string ScenePath="Assets/Scenes/Game.unity";
        private const string BackupPath="Backups/ability-foundation-20260924/Game.unity";
        [MenuItem("Tools/Roguelike/Migrate Game Combat")]
        public static void Migrate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 模式后迁移。");
            if(!File.Exists(BackupPath)) throw new IOException("缺少此次架构迁移前的场景备份："+BackupPath);
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AbilityFoundationAssets.Ensure(out var catalog,out var hero);
            using(var compiled=catalog.Compile()) { hero.ResolveSkills(catalog); }
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            // OpenScene may unload newly created assets that only local variables referenced.
            catalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>(AbilityFoundationAssets.CatalogPath);
            hero=AssetDatabase.LoadAssetAtPath<HeroDefinition>(AbilityFoundationAssets.HeroPath);
            GameCombatController controller=null;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var candidate in root.GetComponentsInChildren<GameCombatController>(true))
                {
                    if(controller) throw new InvalidOperationException("Game 有多个 GameCombatController。");
                    controller=candidate;
                }
            if(!controller) throw new InvalidOperationException("Game 缺少 GameCombatController。");
            var serialized=new SerializedObject(controller);
            FillMissing(serialized,"hero",hero); FillMissing(serialized,"skillCatalog",catalog);
            var indicator=serialized.FindProperty("abilityIndicator");
            if(indicator==null || !indicator.objectReferenceValue ||
                !(indicator.objectReferenceValue is GameAbilityIndicator view) || !view.HasBindings)
                throw new InvalidOperationException("Game 需要已绑定的 GameAbilityIndicator；本迁移不重建表现。");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var actualCatalog=serialized.FindProperty("skillCatalog").objectReferenceValue as SkillCatalog;
            var actualHero=serialized.FindProperty("hero").objectReferenceValue as HeroDefinition;
            using(var compiled=actualCatalog.Compile()) { actualHero.ResolveSkills(actualCatalog); }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存 Game 场景。");
            Debug.Log("ABILITY_FOUNDATION_MIGRATED: Game consumes shared catalog and hero loadout; existing assets/layout preserved.");
        }
        private static void FillMissing(SerializedObject serialized,string field,UnityEngine.Object value)
        {
            var property=serialized.FindProperty(field);
            if(property==null) throw new InvalidOperationException("Game 缺少序列化字段："+field);
            if(!property.objectReferenceValue) property.objectReferenceValue=value;
        }
    }
}
