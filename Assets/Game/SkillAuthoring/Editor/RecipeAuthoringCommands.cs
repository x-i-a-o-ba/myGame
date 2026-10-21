using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>配方编辑事务：候选数据先编译，成功才替换；共享模块与数字技能身份不受覆盖影响。</summary>
    internal static class RecipeAuthoringCommands
    {
        internal static bool Enabled(SkillDefinition skill) => skill && skill.recipe != null && skill.recipe.enabled;
        /// <summary>官方资源使用官方技能编号作为配方身份；玩家存档身份不通过此编辑器入口分配。</summary>
        internal static void BindOfficialIdentity(SkillDefinition skill, bool resetRevision = false)
        {
            if (!Enabled(skill)) return;
            if (skill.configId < 1000 || skill.configId > 1999) throw new InvalidOperationException("请先为官方技能分配 1000–1999 的数字 ID。");
            skill.recipe.authorNamespace = "official";
            skill.recipe.recipeId = skill.configId;
            if (resetRevision) skill.recipe.revision = 1;
        }

        /// <summary>显式保存每次递增一版；创建的首次内部保存不递增。校验失败不改变调用者。</summary>
        internal static SkillRecipe PrepareOfficialSave(SkillDefinition skill, bool advanceRevision)
        {
            var candidate = UnityEngine.Object.Instantiate(skill);
            try
            {
                BindOfficialIdentity(candidate);
                if (advanceRevision) candidate.recipe.revision = checked(candidate.recipe.revision + 1);
                Validate(candidate);
                return candidate.recipe;
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }
        internal static SkillModuleCatalog FindCatalog() => AssetDatabase.FindAssets("t:SkillModuleCatalog")
            .Select(id => AssetDatabase.LoadAssetAtPath<SkillModuleCatalog>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(value => value).OrderBy(value => AssetDatabase.GetAssetPath(value), StringComparer.Ordinal).FirstOrDefault();

        internal static void Validate(SkillDefinition skill)
        {
            var preview = new SkillAuthoringPreview(); preview.Refresh(skill, true);
            if (!preview.Valid) throw new InvalidOperationException(preview.Error);
        }

        internal static void Convert(SkillDefinition skill, SkillModuleCatalog modules)
        {
            if (!modules) throw new InvalidOperationException("请选择数字模块目录后再转换。");
            var candidate = UnityEngine.Object.Instantiate(skill);
            try
            {
                candidate.modules = modules;
                candidate.recipe = SkillRecipeAdapter.FromLegacy(skill, modules);
                candidate.recipe.enabled = true;
                BindOfficialIdentity(candidate, true);
                Validate(candidate);
                Undo.RecordObject(skill, "转换为数字模块配方");
                skill.modules = modules; skill.recipe = candidate.recipe;
                EditorUtility.SetDirty(skill);
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        internal static void Export(SkillDefinition skill, string path)
        {
            Validate(skill);
            File.WriteAllText(path, SkillRecipeJson.Export(skill), new System.Text.UTF8Encoding(false));
        }

        internal static void Import(SkillDefinition skill, SkillModuleCatalog modules, string path)
        {
            if (!modules) throw new InvalidOperationException("导入前请选择数字模块目录。");
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("配方文件超过 1 MB，请检查文件内容。");
            var candidate = SkillRecipeJson.Import(File.ReadAllText(path), modules, false);
            try
            {
                candidate.configId = skill.configId;
                BindOfficialIdentity(candidate);
                candidate.recipe.revision = Enabled(skill) ? skill.recipe.revision : 1;
                Validate(candidate);
                // 身份、资源名与旧阶段均归目标资源；导入仅替换配方和文档支持的施法外壳。
                Undo.RecordObject(skill, "导入技能配方");
                skill.modules = modules; skill.recipe = candidate.recipe;
                skill.displayName = candidate.displayName; skill.aim = candidate.aim;
                skill.castRange = candidate.castRange; skill.cooldown = candidate.cooldown;
                skill.castTime = candidate.castTime; skill.recoveryTime = candidate.recoveryTime;
                skill.manaCost = candidate.manaCost; skill.maxCharges = candidate.maxCharges;
                skill.requiredHits = candidate.requiredHits; skill.rechargeSeconds = candidate.rechargeSeconds;
                EditorUtility.SetDirty(skill);
            }
            finally { if (candidate) UnityEngine.Object.DestroyImmediate(candidate); }
        }

        internal static int Add(SkillDefinition skill, int parentIndex = -1, bool duplicate = false)
        {
            var list = new List<RecipeNode>(skill.recipe.nodes ?? Array.Empty<RecipeNode>());
            if (list.Count >= 32) throw new InvalidOperationException("开发者配方最多 32 个步骤；请先移除或合并现有步骤。");
            int nextId = list.Count == 0 ? 1 : checked(list.Max(n => n.nodeId) + 1);
            RecipeNode node;
            if (duplicate)
            {
                node = JsonUtility.FromJson<RecipeNode>(JsonUtility.ToJson(list[parentIndex]));
                node.nodeId = nextId;
            }
            else
            {
                var delivery = skill.modules?.deliveries?.FirstOrDefault(d => d.kind == DeliveryKind.Instant);
                var query = skill.modules?.queries?.FirstOrDefault(q => q.scope == TargetScope.Area && q.shape == AreaShape.Circle);
                node = new RecipeNode { nodeId = nextId, deliveryModuleId = delivery?.id ?? 0, queryModuleId = query?.id ?? 0 };
                if (parentIndex >= 0) { node.parentNodeId = list[parentIndex].nodeId; node.trigger = RecipeTrigger.OnResolved; }
            }
            Undo.RecordObject(skill, duplicate ? "复制配方步骤" : "添加配方步骤");
            list.Add(node); skill.recipe.nodes = list.ToArray(); EditorUtility.SetDirty(skill);
            return list.Count - 1;
        }

        internal static void Remove(SkillDefinition skill, int index)
        {
            var list = new List<RecipeNode>(skill.recipe.nodes);
            if (list.Count <= 1) throw new InvalidOperationException("请保留至少一个步骤。");
            int id = list[index].nodeId;
            if (list.Any(n => n.parentNodeId == id)) throw new InvalidOperationException("该步骤仍有后续步骤。请先调整后续步骤的触发来源，再删除。");
            Undo.RecordObject(skill, "删除配方步骤"); list.RemoveAt(index); skill.recipe.nodes = list.ToArray(); EditorUtility.SetDirty(skill);
        }

        internal static SkillDefinition CreateTemplate(string path, SkillTemplateKind kind, SkillModuleCatalog modules)
        {
            if (!modules) throw new InvalidOperationException("请先选择模块目录。");
            modules.Validate();
            var skill = SkillAuthoringAssets.CreateAtPath(path, includeDefaultEffect: false);
            try
            {
                skill.modules = modules; skill.recipe = new SkillRecipe { enabled = true, catalogVersion = modules.catalogVersion };
                BindOfficialIdentity(skill, true);
                skill.castRange = 12; skill.cooldown = 4; skill.manaCost = 0;
                var deliveryKind = kind == SkillTemplateKind.ArcExplosion ? DeliveryKind.ParabolicProjectile :
                    kind == SkillTemplateKind.LinearExplosion ? DeliveryKind.LinearProjectile : DeliveryKind.Instant;
                var scope = kind == SkillTemplateKind.SelfHeal ? TargetScope.Self : kind == SkillTemplateKind.UnitDamage ? TargetScope.Single : TargetScope.Area;
                var shape = kind == SkillTemplateKind.ConeAttack ? AreaShape.Cone : AreaShape.Circle;
                var relation = kind == SkillTemplateKind.SelfHeal ? TargetRelation.Allies : TargetRelation.Enemies;
                var delivery = modules.deliveries.FirstOrDefault(d => d.kind == deliveryKind);
                var query = modules.queries.FirstOrDefault(q => q.scope == scope && q.targets == relation && (scope != TargetScope.Area || q.shape == shape));
                var effect = modules.effects.FirstOrDefault(e => e.definition &&
                    (kind == SkillTemplateKind.SelfHeal ? e.definition is HealEffect : e.definition is DamageEffect));
                if (delivery == null || query == null || effect == null) throw new InvalidOperationException("模块目录缺少此模板需要的投递、查询或效果，请先补齐目录。");
                skill.aim = kind == SkillTemplateKind.SelfHeal ? AimMode.Self : kind == SkillTemplateKind.UnitDamage ? AimMode.Unit :
                    kind == SkillTemplateKind.ConeAttack || kind == SkillTemplateKind.LinearExplosion ? AimMode.Direction : AimMode.Point;
                skill.recipe.nodes = new[] { new RecipeNode { nodeId = 1, deliveryModuleId = delivery.id, queryModuleId = query.id,
                    range = 12, radius = 3, effects = new[] { new RecipeEffectUse { effectId = effect.definition.configId,
                        overrideAmount = effect.allowAmountOverride, amount = Mathf.Clamp(10, effect.minAmount, effect.maxAmount) } } } };
                Validate(skill); EditorUtility.SetDirty(skill); SkillAuthoringAssets.Save(skill, advanceRecipeRevision: false); return skill;
            }
            catch { string assetPath = AssetDatabase.GetAssetPath(skill); if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath); else UnityEngine.Object.DestroyImmediate(skill); throw; }
        }
    }
}
