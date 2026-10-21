using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>隔离编辑器命令验证：模板编译、撤销和共享资源所有权；不进入游戏模式。</summary>
    public static class SkillAuthoringWorkflowValidation
    {
        private static int checks;

        public static void Run()
        {
            checks = 0;
            string folderName = "技能流程校验临时_" + Guid.NewGuid().ToString("N");
            string folderGuid = AssetDatabase.CreateFolder("Assets", folderName);
            string root = AssetDatabase.GUIDToAssetPath(folderGuid);
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("无法创建隔离校验目录。");
            var created = new List<SkillDefinition>();
            try
            {
                var ids = new HashSet<string>();
                foreach (SkillTemplateKind kind in Enum.GetValues(typeof(SkillTemplateKind)))
                {
                    var skill = SkillAuthoringWorkflow.CreateTemplate(root + "/" + SkillAuthoringWorkflow.TemplateName(kind) + ".asset", kind);
                    created.Add(skill);
                    Check(ids.Add(skill.stableId), "模板稳定 ID 独立");
                    Check(skill.stages.Length == 1 && skill.stages[0].effects.Length == 1, "模板只创建所需效果");
                    Compile(skill);
                    if (kind == SkillTemplateKind.ArcExplosion)
                    {
                        Check(skill.stages[0].delivery == DeliveryKind.ParabolicProjectile, "抛物线模板投递方式");
                        Check(skill.stages[0].arcHeight == 3 && skill.stages[0].flightSeconds == 1 &&
                              skill.stages[0].launchHeight == 1 && skill.stages[0].arrivalHeight == 0, "抛物线默认三维参数");
                    }
                    if (kind == SkillTemplateKind.SelfHeal)
                        Check(skill.aim == AimMode.Self && skill.stages[0].scope == TargetScope.Self && skill.stages[0].effects[0] is HealEffect, "自身治疗模板");
                    if (kind == SkillTemplateKind.UnitDamage)
                        Check(skill.aim == AimMode.Unit && skill.stages[0].scope == TargetScope.Single && skill.stages[0].effects[0] is DamageEffect, "单体伤害模板");
                }
                Check(AssetDatabase.FindAssets("t:EffectDefinition", new[] { root }).Length == 6, "六模板没有孤立默认效果文件");

                var target = created[0];
                bool rejected = false;
                try { SkillAuthoringWorkflow.RemoveStage(target, 0); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && target.stages.Length == 1, "最后阶段拒删且数据不变");

                Undo.IncrementCurrentGroup();
                int duplicateIndex = SkillAuthoringWorkflow.DuplicateStage(target, 0);
                Undo.FlushUndoRecordObjects();
                Check(duplicateIndex == 1 && target.stages.Length == 2, "阶段复制插入后方");
                Check(!ReferenceEquals(target.stages[0], target.stages[1]) && !ReferenceEquals(target.stages[0].effects, target.stages[1].effects), "阶段及数组独立");
                Check(target.stages[0].effects[0] == target.stages[1].effects[0], "复制阶段保留共享效果");
                Undo.PerformUndo(); Check(target.stages.Length == 1, "Undo 撤销阶段复制");
                Undo.PerformRedo(); Check(target.stages.Length == 2, "Redo 恢复阶段复制");
                float originalRadius = target.stages[0].radius;
                target.stages[1].radius = originalRadius + 2;
                Check(target.stages[0].radius == originalRadius, "副本范围修改不污染源阶段");

                foreach (EffectPreset preset in Enum.GetValues(typeof(EffectPreset)))
                {
                    SkillAuthoringWorkflow.AddEffect(target, 0, false, preset);
                    Compile(target);
                }
                int slowIndex = -1;
                for (int i = 0; i < target.stages[0].effects.Length; i++)
                    if (target.stages[0].effects[i] is ApplyBuffEffect candidate && candidate.buff.duration == 2.5f) slowIndex = i;
                Check(slowIndex >= 0, "减速预设引用已创建");
                var sharedEffect = (ApplyBuffEffect)target.stages[0].effects[slowIndex];
                var sharedBuff = sharedEffect.buff;
                string originalBuffId = sharedBuff.stableId;
                Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
                var independent = (ApplyBuffEffect)SkillAuthoringWorkflow.MakeEffectIndependent(target, 0, false, slowIndex);
                Undo.FlushUndoRecordObjects();
                Check(independent != sharedEffect && independent.buff != sharedBuff, "独立化复制效果及 Buff");
                Check(independent.buff.stableId != originalBuffId && sharedBuff.stableId == originalBuffId, "独立 Buff 新 ID 且原状态不变");
                Check(independent.buff.modifiers[0].value == -0.35f, "复制保留减速数值");
                Compile(target);
                Undo.PerformUndo(); Check(target.stages[0].effects[slowIndex] == sharedEffect, "Undo 独立化恢复共享引用");
                Undo.PerformRedo(); Check(target.stages[0].effects[slowIndex] == independent, "Redo 独立化恢复副本引用");
                string independentPath = AssetDatabase.GetAssetPath(independent);
                SkillAuthoringWorkflow.RemoveEffect(target, 0, false, slowIndex);
                Check(AssetDatabase.LoadAssetAtPath<EffectDefinition>(independentPath) == independent, "移除引用保留资源文件");
                SkillAuthoringAssets.Save(target);
                Debug.Log("SKILL_AUTHORING_WORKFLOW_VALIDATION_PASSED checks=" + checks);
            }
            finally
            {
                foreach (var skill in created) if (skill) Undo.ClearUndo(skill);
                // 目录由本次随机命名创建，并核验 GUID，绝不清理用户既有资源目录。
                if (AssetDatabase.AssetPathToGUID(root) == folderGuid) AssetDatabase.DeleteAsset(root);
            }
        }

        private static void Compile(SkillDefinition skill)
        {
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            try
            {
                catalog.skills = new[] { skill };
                catalog.buffs = new List<BuffDefinition>(SkillAuthoringPreview.CollectBuffs(skill)).ToArray();
                using (var compiled = catalog.Compile()) Check(compiled.Value.Skills.Length == 1, "模板或组合编译通过");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        private static void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException("技能配置流程检查失败：" + label);
            checks++;
        }
    }
}
