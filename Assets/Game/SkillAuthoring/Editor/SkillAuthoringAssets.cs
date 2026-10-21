using System;
using System.Collections.Generic;
using System.IO;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>显式创建、复制、登记及保存操作。仅技能本体复制，效果与 Buff 继续共享；登记失败不写入技能库。</summary>
    internal static class SkillAuthoringAssets
    {
        internal static SkillDefinition CreateAtPath(string requestedPath, SkillDefinition source = null, bool includeDefaultEffect = true)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            var skill = source ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<SkillDefinition>();
            skill.name = Path.GetFileNameWithoutExtension(path);
            skill.displayName = skill.name;
            skill.configId = 0; // 复制和新建都重新分配数字编号。
            skill.stableId = "ability." + Guid.NewGuid().ToString("N");
            string effectPath = null;
            DamageEffect damage = null;
            try
            {
                if (!source && includeDefaultEffect)
                {
                    effectPath = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\','/') + "/" + skill.name + "·伤害效果.asset");
                    damage = ScriptableObject.CreateInstance<DamageEffect>();
                    damage.name = Path.GetFileNameWithoutExtension(effectPath); damage.amount = 10;
                    damage.stableId = "effect." + Guid.NewGuid().ToString("N");
                    AssetDatabase.CreateAsset(damage, effectPath);
                    NumericConfigIds.EnsureEffect(damage);
                    skill.castRange = 12; skill.manaCost = 0; skill.levelPower = 0;
                    skill.indicatorColor = new Color(0.18f,0.85f,1,0.9f);
                    skill.stages = new[] { new SkillStage { radius = 3, range = 12, effects = new EffectDefinition[] { damage } } };
                }
                AssetDatabase.CreateAsset(skill, path);
                NumericConfigIds.EnsureSkill(skill);
                if (RecipeAuthoringCommands.Enabled(skill))
                {
                    RecipeAuthoringCommands.BindOfficialIdentity(skill, true);
                    EditorUtility.SetDirty(skill);
                }
                AssetDatabase.SaveAssetIfDirty(skill);
                return skill;
            }
            catch
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == skill) AssetDatabase.DeleteAsset(path);
                else if (skill) UnityEngine.Object.DestroyImmediate(skill);
                if (!string.IsNullOrEmpty(effectPath) && AssetDatabase.LoadMainAssetAtPath(effectPath) == damage) AssetDatabase.DeleteAsset(effectPath);
                if (damage && !EditorUtility.IsPersistent(damage)) UnityEngine.Object.DestroyImmediate(damage);
                throw;
            }
        }

        internal static void CreateForWorkspace(SkillAuthoringWorkspace workspace, bool copy)
        {
            string path = EditorUtility.SaveFilePanelInProject(copy ? "另存一份技能（效果与 Buff 保持共享）" : "创建技能配置",
                copy && workspace.skill ? workspace.skill.name + "·副本" : "新技能", "asset", "请选择技能配置资源保存位置。", SkillAuthoringSceneTools.ContentRoot);
            if (string.IsNullOrEmpty(path)) return;
            var skill = CreateAtPath(path, copy ? workspace.skill : null);
            Undo.RecordObject(workspace, "切换工作台技能");
            workspace.skill = skill; workspace.stageIndex = 0;
            EditorUtility.SetDirty(workspace); EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);
            EditorGUIUtility.PingObject(skill);
        }

        internal static void Register(SkillAuthoringWorkspace workspace)
        {
            Register(workspace.skill, workspace.catalog);
        }

        internal static void Register(SkillDefinition skill, SkillCatalog catalog)
        {
            if (!catalog || !skill) throw new InvalidOperationException("请先指定技能与目标技能库。");
            var candidate = UnityEngine.Object.Instantiate(catalog);
            candidate.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var skills = new List<SkillDefinition>(candidate.skills ?? Array.Empty<SkillDefinition>());
                var buffs = new List<BuffDefinition>(candidate.buffs ?? Array.Empty<BuffDefinition>());
                if (!skills.Contains(skill)) skills.Add(skill);
                foreach (var buff in SkillAuthoringPreview.CollectBuffs(skill)) if (!buffs.Contains(buff)) buffs.Add(buff);
                candidate.skills = skills.ToArray(); candidate.buffs = buffs.ToArray();
                using (var compiled = candidate.Compile()) { }
                Undo.RecordObject(catalog, "登记技能及依赖 Buff");
                catalog.skills = candidate.skills; catalog.buffs = candidate.buffs;
                EditorUtility.SetDirty(catalog);
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        internal static void Save(SkillAuthoringWorkspace workspace)
        {
            Save(workspace.skill, workspace.catalog);
            if (!EditorSceneManager.SaveScene(workspace.gameObject.scene)) throw new IOException("技能已保存，但工作场景保存失败或被取消。");
        }

        internal static void Save(SkillDefinition skill, SkillCatalog catalog = null, bool advanceRecipeRevision = true)
        {
            if (skill)
            {
                SkillRecipe nextRecipe = null;
                if (RecipeAuthoringCommands.Enabled(skill))
                {
                    if (skill.configId != 0) nextRecipe = RecipeAuthoringCommands.PrepareOfficialSave(skill, advanceRecipeRevision);
                    else
                    {
                        RecipeAuthoringCommands.Validate(skill);
                        if (advanceRecipeRevision && skill.recipe.revision == int.MaxValue) throw new OverflowException("配方修订号已达上限，保存被拒绝。");
                    }
                }
                NumericConfigIds.EnsureSkill(skill);
                if (RecipeAuthoringCommands.Enabled(skill))
                {
                    nextRecipe ??= RecipeAuthoringCommands.PrepareOfficialSave(skill, advanceRecipeRevision);
                    Undo.RecordObject(skill, "保存技能配方修订");
                    skill.recipe = nextRecipe;
                    EditorUtility.SetDirty(skill);
                }
                AssetDatabase.SaveAssetIfDirty(skill);
                if (RecipeAuthoringCommands.Enabled(skill))
                {
                    // 配方参数保存于技能；这里仅保存显式编辑过的目录依赖，不回写旧阶段。
                    AssetDatabase.SaveAssetIfDirty(skill.modules);
                    foreach (var effect in skill.modules.effects ?? Array.Empty<EffectModule>())
                        if (effect != null && effect.definition) AssetDatabase.SaveAssetIfDirty(effect.definition);
                }
                else foreach (var stage in skill.stages ?? Array.Empty<SkillStage>())
                {
                    if (stage == null) continue;
                    SaveEffects(stage.effects); SaveEffects(stage.impactEffects);
                }
                foreach (var buff in SkillAuthoringPreview.CollectBuffs(skill)) AssetDatabase.SaveAssetIfDirty(buff);
            }
            if (catalog) AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static void SaveEffects(EffectDefinition[] effects)
        {
            if (effects == null) return;
            foreach (var effect in effects) if (effect) AssetDatabase.SaveAssetIfDirty(effect);
        }
    }
}
