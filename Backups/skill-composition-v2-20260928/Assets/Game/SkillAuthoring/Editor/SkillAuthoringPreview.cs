using System;
using System.Collections.Generic;
using System.Text;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>编辑器独占的编译快照缓存。编译后立即释放 Blob，只保留展示数值；不创建 ECS World。</summary>
    internal sealed class SkillAuthoringPreview
    {
        private SkillDefinition source;
        private string fingerprint;
        internal SkillAimPreview[] Stages { get; private set; } = Array.Empty<SkillAimPreview>();
        internal string Error { get; private set; }
        internal bool Valid => Stages.Length > 0 && string.IsNullOrEmpty(Error);

        internal void Refresh(SkillDefinition skill, bool force = false)
        {
            string current = Fingerprint(skill);
            if (!force && source == skill && fingerprint == current) return;
            source = skill; fingerprint = current;
            Stages = Array.Empty<SkillAimPreview>(); Error = null;
            if (!skill) { Error = "请先选择或创建技能配置。"; return; }
            if (skill.stages == null || skill.stages.Length == 0)
            { Error = "工作台使用组合技能；请先添加至少一个阶段。"; return; }
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                catalog.skills = new[] { skill };
                catalog.buffs = CollectBuffs(skill).ToArray();
                using (var library = catalog.Compile())
                {
                    ref var definition = ref library.Value.Skills[0];
                    var snapshots = new SkillAimPreview[definition.Stages.Length];
                    for (int i = 0; i < snapshots.Length; i++) snapshots[i] = SkillAimPreview.From(ref definition, i);
                    Stages = snapshots;
                }
            }
            catch (Exception exception) { Error = exception.Message; }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        private static string Fingerprint(SkillDefinition skill)
        {
            if (!skill) return string.Empty;
            var text = new StringBuilder(EditorJsonUtility.ToJson(skill));
            foreach (var stage in skill.stages ?? Array.Empty<SkillStage>())
            {
                if (stage == null) continue;
                Add(stage.effects); Add(stage.impactEffects);
            }
            return text.ToString();
            void Add(EffectDefinition[] effects)
            {
                if (effects == null) return;
                foreach (var effect in effects)
                {
                    if (!effect) continue;
                    text.Append('|').Append(effect.GetInstanceID()).Append(':').Append(EditorUtility.GetDirtyCount(effect));
                    if (effect.ReferencedBuff) text.Append('|').Append(effect.ReferencedBuff.GetInstanceID()).Append(':').Append(EditorUtility.GetDirtyCount(effect.ReferencedBuff));
                }
            }
        }

        internal static List<BuffDefinition> CollectBuffs(SkillDefinition skill)
        {
            var result = new List<BuffDefinition>();
            if (!skill || skill.stages == null) return result;
            foreach (var stage in skill.stages)
            {
                if (stage == null) continue;
                Add(stage.effects); Add(stage.impactEffects);
            }
            return result;
            void Add(EffectDefinition[] effects)
            {
                if (effects == null) return;
                foreach (var effect in effects)
                    if (effect && effect.ReferencedBuff && !result.Contains(effect.ReferencedBuff)) result.Add(effect.ReferencedBuff);
            }
        }
    }
}
