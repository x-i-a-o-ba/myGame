using System;
using System.Collections.Generic;
using System.IO;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    internal enum SkillTemplateKind
    {
        [InspectorName("目标点范围伤害")] AreaBurst, [InspectorName("扇形攻击")] ConeAttack,
        [InspectorName("直线碰撞爆炸")] LinearExplosion, [InspectorName("抛物线落点爆炸")] ArcExplosion,
        [InspectorName("自身治疗")] SelfHeal, [InspectorName("单体伤害")] UnitDamage
    }
    internal enum EffectPreset
    {
        [InspectorName("伤害")] Damage, [InspectorName("治疗")] Heal, [InspectorName("减速")] Slow,
        [InspectorName("眩晕")] Stun, [InspectorName("攻速提升")] AttackSpeed, [InspectorName("驱散")] Dispel
    }

    /// <summary>编辑器组合命令。阶段为技能自有数据；效果默认共享，仅显式独立化时复制资源。</summary>
    internal static class SkillAuthoringWorkflow
    {
        internal static string TemplateName(SkillTemplateKind kind) => kind switch
        {
            SkillTemplateKind.AreaBurst => "目标点范围伤害", SkillTemplateKind.ConeAttack => "扇形攻击",
            SkillTemplateKind.LinearExplosion => "直线碰撞爆炸", SkillTemplateKind.ArcExplosion => "抛物线落点爆炸",
            SkillTemplateKind.SelfHeal => "自身治疗", SkillTemplateKind.UnitDamage => "单体伤害",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "未知技能模板。")
        };

        internal static string TemplateDescription(SkillTemplateKind kind) => kind switch
        {
            SkillTemplateKind.AreaBurst => "选择目标点，对半径 3 Unity 单位内的敌人造成 10 点伤害。",
            SkillTemplateKind.ConeAttack => "朝指定方向，对长度 12 Unity 单位、半角 35 度的扇形造成伤害。",
            SkillTemplateKind.LinearExplosion => "直线弹体命中第一名敌人后，对半径 3 Unity 单位内造成伤害。",
            SkillTemplateKind.ArcExplosion => "弹体沿三维抛物线飞行 1 秒，在目标点造成范围伤害；高度可编辑。",
            SkillTemplateKind.SelfHeal => "立即为施法者恢复 10 点生命。",
            SkillTemplateKind.UnitDamage => "选择一名敌人，立即造成 10 点伤害。",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "未知技能模板。")
        };

        internal static SkillDefinition CreateTemplate(string path, SkillTemplateKind kind)
        {
            TemplateName(kind); // 先拒绝未知类型，避免生成半成品文件。
            using var creation = new Creation();
            var skill = SkillAuthoringAssets.CreateAtPath(path, includeDefaultEffect: false);
            creation.Track(skill);
            skill.castRange = 12; skill.cooldown = 4; skill.manaCost = 0; skill.levelPower = 0;
            skill.indicatorColor = new Color(0.18f, 0.85f, 1, 0.9f);
            var stage = new SkillStage { range = 12, radius = 3 };
            skill.stages = new[] { stage };
            switch (kind)
            {
                case SkillTemplateKind.ConeAttack: skill.aim = AimMode.Direction; stage.shape = AreaShape.Cone; break;
                case SkillTemplateKind.LinearExplosion: skill.aim = AimMode.Direction; stage.delivery = DeliveryKind.LinearProjectile; break;
                case SkillTemplateKind.ArcExplosion:
                    stage.delivery = DeliveryKind.ParabolicProjectile; stage.arcHeight = 3; stage.flightSeconds = 1;
                    stage.launchHeight = 1; stage.arrivalHeight = 0; break;
                case SkillTemplateKind.SelfHeal: skill.aim = AimMode.Self; stage.scope = TargetScope.Self; stage.targets = TargetRelation.Allies; break;
                case SkillTemplateKind.UnitDamage: skill.aim = AimMode.Unit; stage.scope = TargetScope.Single; break;
            }
            stage.effects = new[] { CreateEffect(skill, kind == SkillTemplateKind.SelfHeal ? EffectPreset.Heal : EffectPreset.Damage, creation) };
            EditorUtility.SetDirty(skill);
            SkillAuthoringAssets.Save(skill);
            creation.Commit();
            return skill;
        }

        internal static int AddStage(SkillDefinition skill)
        {
            RequireSkill(skill);
            var stages = new List<SkillStage>(skill.stages ?? Array.Empty<SkillStage>()) { new SkillStage { range = skill.castRange } };
            SetStages(skill, stages, "添加技能阶段"); return stages.Count - 1;
        }

        internal static int DuplicateStage(SkillDefinition skill, int index)
        {
            var source = Stage(skill, index);
            // Unity JSON 深复制所有序列化阶段字段；随后显式保留共享效果引用并复制数组。
            var copy = JsonUtility.FromJson<SkillStage>(JsonUtility.ToJson(source));
            copy.effects = (EffectDefinition[])(source.effects ?? Array.Empty<EffectDefinition>()).Clone();
            copy.impactEffects = (EffectDefinition[])(source.impactEffects ?? Array.Empty<EffectDefinition>()).Clone();
            var stages = new List<SkillStage>(skill.stages); stages.Insert(index + 1, copy);
            SetStages(skill, stages, "复制技能阶段"); return index + 1;
        }

        internal static void RemoveStage(SkillDefinition skill, int index)
        {
            Stage(skill, index);
            if (skill.stages.Length == 1) throw new InvalidOperationException("请保留至少一个阶段；可以移除阶段内的效果以创建空白草稿。");
            var stages = new List<SkillStage>(skill.stages); stages.RemoveAt(index); SetStages(skill, stages, "移除技能阶段");
        }

        internal static void MoveStage(SkillDefinition skill, int index, int delta)
        {
            var stage = Stage(skill, index); int destination = index + delta;
            if (destination < 0 || destination >= skill.stages.Length) return;
            var stages = new List<SkillStage>(skill.stages); stages.RemoveAt(index); stages.Insert(destination, stage);
            SetStages(skill, stages, "移动技能阶段");
        }

        internal static EffectDefinition AddEffect(SkillDefinition skill, int stageIndex, bool impact, EffectPreset preset)
        {
            Stage(skill, stageIndex);
            using var creation = new Creation();
            var effect = CreateEffect(skill, preset, creation);
            AddExistingEffect(skill, stageIndex, impact, effect);
            creation.Commit(); return effect;
        }

        internal static void AddExistingEffect(SkillDefinition skill, int stageIndex, bool impact, EffectDefinition effect)
        {
            if (!effect || !EditorUtility.IsPersistent(effect)) throw new InvalidOperationException("请选择已保存的效果资源。");
            var stage = Stage(skill, stageIndex);
            var effects = new List<EffectDefinition>(Effects(stage, impact)) { effect };
            SetEffects(skill, stage, impact, effects.ToArray(), "添加效果引用");
        }

        internal static void RemoveEffect(SkillDefinition skill, int stageIndex, bool impact, int effectIndex)
        {
            var stage = Stage(skill, stageIndex); var effects = new List<EffectDefinition>(Effects(stage, impact));
            RequireIndex(effectIndex, effects.Count); effects.RemoveAt(effectIndex);
            SetEffects(skill, stage, impact, effects.ToArray(), "移除效果引用");
        }

        internal static void MoveEffect(SkillDefinition skill, int stageIndex, bool impact, int effectIndex, int delta)
        {
            var stage = Stage(skill, stageIndex); var effects = new List<EffectDefinition>(Effects(stage, impact));
            RequireIndex(effectIndex, effects.Count); int destination = effectIndex + delta;
            if (destination < 0 || destination >= effects.Count) return;
            var effect = effects[effectIndex]; effects.RemoveAt(effectIndex); effects.Insert(destination, effect);
            SetEffects(skill, stage, impact, effects.ToArray(), "移动效果顺序");
        }

        internal static EffectDefinition MakeEffectIndependent(SkillDefinition skill, int stageIndex, bool impact, int effectIndex)
        {
            var stage = Stage(skill, stageIndex); var effects = (EffectDefinition[])Effects(stage, impact).Clone();
            RequireIndex(effectIndex, effects.Length);
            if (!effects[effectIndex]) throw new InvalidOperationException("效果引用为空，无法创建独立副本。");
            using var creation = new Creation();
            var copy = UnityEngine.Object.Instantiate(effects[effectIndex]); creation.Track(copy);
            Persist(skill, copy, effects[effectIndex].name + "·独立副本");
            if (copy is ApplyBuffEffect apply && apply.buff)
            {
                var buff = UnityEngine.Object.Instantiate(apply.buff); creation.Track(buff);
                buff.stableId = "buff." + Guid.NewGuid().ToString("N");
                Persist(skill, buff, apply.buff.name + "·独立副本"); apply.buff = buff;
                EditorUtility.SetDirty(apply); AssetDatabase.SaveAssetIfDirty(apply);
            }
            effects[effectIndex] = copy;
            SetEffects(skill, stage, impact, effects, "效果及状态独立化");
            creation.Commit(); return copy;
        }

        private static EffectDefinition CreateEffect(SkillDefinition skill, EffectPreset preset, Creation creation)
        {
            EffectDefinition effect; string label;
            switch (preset)
            {
                case EffectPreset.Damage: effect = ScriptableObject.CreateInstance<DamageEffect>(); label = "伤害效果"; break;
                case EffectPreset.Heal: effect = ScriptableObject.CreateInstance<HealEffect>(); label = "治疗效果"; break;
                case EffectPreset.Dispel: effect = ScriptableObject.CreateInstance<DispelEffect>(); label = "驱散效果"; break;
                case EffectPreset.Slow:
                case EffectPreset.Stun:
                case EffectPreset.AttackSpeed:
                    var buff = ScriptableObject.CreateInstance<BuffDefinition>(); creation.Track(buff);
                    label = preset == EffectPreset.Slow ? "减速" : preset == EffectPreset.Stun ? "眩晕" : "攻速提升";
                    buff.stableId = "buff." + Guid.NewGuid().ToString("N"); buff.displayName = label;
                    buff.duration = preset == EffectPreset.Slow ? 2.5f : preset == EffectPreset.Stun ? 1.5f : 5;
                    if (preset == EffectPreset.Stun) buff.controls = ControlFlags.MovementBlocked | ControlFlags.AttackBlocked | ControlFlags.CastBlocked;
                    else buff.modifiers = new[] { new AttributeModifier { attribute = preset == EffectPreset.Slow ? AttributeKind.MoveSpeed : AttributeKind.AttackSpeed,
                        operation = ModifierOperation.Percent, value = preset == EffectPreset.Slow ? -0.35f : 0.6f } };
                    Persist(skill, buff, skill.name + "·" + label + "状态");
                    var apply = ScriptableObject.CreateInstance<ApplyBuffEffect>(); apply.buff = buff; effect = apply; label += "效果"; break;
                default: throw new ArgumentOutOfRangeException(nameof(preset), "未知效果预设。");
            }
            creation.Track(effect); Persist(skill, effect, skill.name + "·" + label); return effect;
        }

        private static void Persist(SkillDefinition skill, ScriptableObject asset, string name)
        {
            string skillPath = AssetDatabase.GetAssetPath(skill);
            if (string.IsNullOrEmpty(skillPath)) throw new InvalidOperationException("请先保存技能资源，再创建效果与状态。");
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(skillPath).Replace('\\', '/') + "/" + name + ".asset");
            asset.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void RequireSkill(SkillDefinition skill) { if (!skill) throw new InvalidOperationException("请先选择技能。"); }
        private static void RequireIndex(int index, int count) { if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index), "所选阶段或效果不存在。"); }
        private static SkillStage Stage(SkillDefinition skill, int index)
        {
            RequireSkill(skill); RequireIndex(index, skill.stages?.Length ?? 0);
            return skill.stages[index] ?? throw new InvalidOperationException("所选阶段为空，请先修复配置。");
        }
        private static EffectDefinition[] Effects(SkillStage stage, bool impact) => (impact ? stage.impactEffects : stage.effects) ?? Array.Empty<EffectDefinition>();
        private static void SetStages(SkillDefinition skill, List<SkillStage> stages, string label)
        { Undo.RecordObject(skill, label); skill.stages = stages.ToArray(); EditorUtility.SetDirty(skill); }
        private static void SetEffects(SkillDefinition skill, SkillStage stage, bool impact, EffectDefinition[] effects, string label)
        { Undo.RecordObject(skill, label); if (impact) stage.impactEffects = effects; else stage.effects = effects; EditorUtility.SetDirty(skill); }

        /// <summary>只追踪本次命令创建的对象；失败时按对象身份删除资源，不清理同目录其他文件。</summary>
        private sealed class Creation : IDisposable
        {
            private readonly List<ScriptableObject> objects = new List<ScriptableObject>();
            private bool committed;
            internal void Track(ScriptableObject asset) => objects.Add(asset);
            internal void Commit() => committed = true;
            public void Dispose()
            {
                if (committed) return;
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    var asset = objects[i]; if (!asset) continue;
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadMainAssetAtPath(path) == asset) AssetDatabase.DeleteAsset(path);
                    else if (!EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }
    }
}
