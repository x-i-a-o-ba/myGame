using System;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>由主 AI 串行运行的编辑期事务检查，不创建 World、不进入游戏、不改正式资源。</summary>
    public static class RecipeAuthoringValidation
    {
        public static void Validate()
        {
            int checks = 0;
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException("配方编辑器检查失败：" + label); checks++; }
            var source = AssetDatabase.FindAssets("t:SkillDefinition")
                .Select(id => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(id)))
                .FirstOrDefault(s => RecipeAuthoringCommands.Enabled(s) && s.modules && s.recipe.nodes.Length > 0);
            if (!source) throw new InvalidOperationException("请先生成至少一份正式配方，再运行编辑器事务检查。");
            string sourceBefore = EditorJsonUtility.ToJson(source);
            var skill = UnityEngine.Object.Instantiate(source);
            string file = Path.GetTempFileName();
            try
            {
                RecipeAuthoringCommands.BindOfficialIdentity(skill, true);
                Check(skill.recipe.authorNamespace == "official" && skill.recipe.recipeId == skill.configId && skill.recipe.revision == 1, "官方新建身份绑定数字技能 ID，修订从 1 开始");
                var identityCopy = UnityEngine.Object.Instantiate(skill);
                try
                {
                    identityCopy.configId = skill.configId == 1999 ? 1998 : 1999;
                    identityCopy.recipe.revision = 9;
                    RecipeAuthoringCommands.BindOfficialIdentity(identityCopy, true);
                    Check(identityCopy.recipe.recipeId != skill.recipe.recipeId && identityCopy.recipe.recipeId == identityCopy.configId && identityCopy.recipe.revision == 1, "复制重绑定新配方身份且修订重置");
                }
                finally { UnityEngine.Object.DestroyImmediate(identityCopy); }
                var firstSave = RecipeAuthoringCommands.PrepareOfficialSave(skill, false);
                Check(firstSave.revision == 1 && skill.recipe.revision == 1, "创建内部保存不增加修订");
                var nextSave = RecipeAuthoringCommands.PrepareOfficialSave(skill, true);
                Check(nextSave.revision == 2 && skill.recipe.revision == 1, "显式保存候选增加一版但不提前修改原对象");
                skill.recipe = nextSave;
                skill.recipe = RecipeAuthoringCommands.PrepareOfficialSave(skill, true);
                Check(skill.recipe.revision == 3, "再次显式保存恰好增加一版");
                skill.recipe.revision = int.MaxValue;
                bool overflowRejected = false;
                try { RecipeAuthoringCommands.PrepareOfficialSave(skill, true); } catch (OverflowException) { overflowRejected = true; }
                Check(overflowRejected && skill.recipe.revision == int.MaxValue, "修订溢出拒绝且保留原值");
                skill.recipe.revision = 3;
                int beforeCount = skill.recipe.nodes.Length;
                int copied = RecipeAuthoringCommands.Add(skill, 0, true);
                Check(copied == beforeCount && skill.recipe.nodes.Length == beforeCount + 1, "复制步骤增加一张卡片");
                Check(skill.recipe.nodes.Select(n => n.nodeId).Distinct().Count() == skill.recipe.nodes.Length, "复制步骤本地编号唯一");
                Check(!ReferenceEquals(skill.recipe.nodes[0], skill.recipe.nodes[copied]), "复制步骤使用独立参数对象");
                float originalRadius = skill.recipe.nodes[0].radius;
                skill.recipe.nodes[copied].radius += 1;
                Check(skill.recipe.nodes[0].radius == originalRadius, "改副本参数不污染原步骤");
                RecipeAuthoringCommands.Remove(skill, copied);
                int child = RecipeAuthoringCommands.Add(skill, 0);
                Check(skill.recipe.nodes[child].parentNodeId == skill.recipe.nodes[0].nodeId && skill.recipe.nodes[child].trigger == RecipeTrigger.OnResolved, "添加后续绑定父事件");
                bool parentRejected = false;
                try { RecipeAuthoringCommands.Remove(skill, 0); } catch (InvalidOperationException) { parentRejected = true; }
                Check(parentRejected && skill.recipe.nodes.Length == beforeCount + 1, "有后续的父步骤删除被拒绝");
                RecipeAuthoringCommands.Remove(skill, child);
                RecipeAuthoringCommands.Validate(skill);
                var preview = new SkillAuthoringPreview(); preview.Refresh(skill, true);
                Check(preview.Valid && preview.Stages.Length == skill.recipe.nodes.Length, "配方以正式编译结果生成预览");
                RecipeAuthoringCommands.Export(skill, file);
                string exported = File.ReadAllText(file);
                Check(!exported.Contains("instanceID") && exported.Contains("deliveryModuleId"), "配方文件使用数字引用");
                int id = skill.configId; string stableId = skill.stableId;
                var foreign = JsonUtility.FromJson<SkillRecipeDocument>(exported);
                foreign.recipe.authorNamespace = "another-save"; foreign.recipe.recipeId = 42; foreign.recipe.revision = 99;
                File.WriteAllText(file, JsonUtility.ToJson(foreign));
                RecipeAuthoringCommands.Import(skill, skill.modules, file);
                Check(skill.configId == id && skill.stableId == stableId, "导入保留技能数字身份与内部兼容键");
                Check(skill.recipe.authorNamespace == "official" && skill.recipe.recipeId == id && skill.recipe.revision == 3, "导入不盗用外来配方身份或修订");
                string beforeFailure = EditorJsonUtility.ToJson(skill);
                var invalid = JsonUtility.FromJson<SkillRecipeDocument>(exported);
                invalid.recipe.nodes[0].deliveryModuleId = -1;
                File.WriteAllText(file, JsonUtility.ToJson(invalid));
                bool importRejected = false;
                try { RecipeAuthoringCommands.Import(skill, skill.modules, file); } catch (InvalidOperationException) { importRejected = true; }
                Check(importRejected && EditorJsonUtility.ToJson(skill) == beforeFailure, "非法导入不覆盖现有技能");
                int validDelivery = skill.recipe.nodes[0].deliveryModuleId;
                skill.recipe.nodes[0].deliveryModuleId = -1;
                bool saveRejected = false;
                try { RecipeAuthoringCommands.PrepareOfficialSave(skill, true); } catch (InvalidOperationException) { saveRejected = true; }
                Check(saveRejected && skill.recipe.revision == 3, "非法保存不消耗配方修订");
                skill.recipe.nodes[0].deliveryModuleId = validDelivery;
                bool legacyRejected = false;
                try { SkillAuthoringWorkflow.AddStage(skill); } catch (InvalidOperationException) { legacyRejected = true; }
                Check(legacyRejected, "配方启用后禁止旧阶段编辑");
                Check(EditorJsonUtility.ToJson(source) == sourceBefore, "全过程不修改正式技能资源");
                Debug.Log("RecipeAuthoringValidation PASS: " + checks + " 项编辑器事务检查；未运行游戏。");
            }
            finally
            {
                if (skill) UnityEngine.Object.DestroyImmediate(skill);
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
