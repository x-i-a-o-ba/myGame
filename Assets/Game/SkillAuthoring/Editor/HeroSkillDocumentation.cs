using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using Roguelike.Combat;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>只读扫描英雄与技能定义，输出 Excel 文档的数据快照；不修改资源，不参与运行时查找。</summary>
    public static class HeroSkillDocumentation
    {
        public const string OutputPath = "Logs/HeroSkillCatalog/source.json";
        /// <summary>开局选择目录：怪物文档按其中各战场的名册列出全部怪物。</summary>
        private const string SelectionCatalogPath = "Assets/GameData/目录/开局选择目录.asset";

        /// <summary>验证全部英雄和技能的全局稳定 ID 与装配引用，然后写入 UTF-8 JSON；失败时保留旧快照。</summary>
        public static void Export()
        {
            var snapshot = BuildSnapshot();
            var absolutePath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
            Debug.Log($"英雄技能文档导出完成：英雄 {snapshot.heroes.Length}，技能 {snapshot.skills.Length}，装配 {snapshot.loadouts.Length}，阶段 {snapshot.stages.Length}；{absolutePath}");
        }

        /// <summary>只验证，不写入任何项目文件；错误包含稳定 ID、资源路径或装配槽位。</summary>
        public static void Validate()
        {
            var snapshot = BuildSnapshot();
            Debug.Log($"英雄技能稳定 ID 检查通过：{snapshot.heroes.Length} 个英雄，{snapshot.skills.Length} 个技能。");
        }

        private static Snapshot BuildSnapshot()
        {
            var sourceProject = Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT");
            if (!string.IsNullOrWhiteSpace(sourceProject) && !Directory.Exists(Path.Combine(sourceProject, "Assets")))
                throw new InvalidOperationException("英雄技能文档来源项目不存在或缺少 Assets：" + sourceProject);
            NumericConfigIds.ValidateAll();
            var heroes = Find<HeroDefinition>();
            var skills = Find<SkillDefinition>();
            // Excel 查找与计数默认不区分大小写，因此文档 ID 采用更严格的同等判重规则。
            var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            foreach (var hero in heroes) CheckId(hero.stableId, hero, ids, errors);
            foreach (var skill in skills) CheckId(skill.stableId, skill, ids, errors);
            var knownSkills = new HashSet<SkillDefinition>(skills);
            foreach (var hero in heroes)
            {
                if (hero.skills == null) { errors.Add($"英雄装配为空数组引用：{AssetDatabase.GetAssetPath(hero)}"); continue; }
                for (var i = 0; i < hero.skills.Length; i++)
                    if (!hero.skills[i] || !knownSkills.Contains(hero.skills[i]))
                        errors.Add($"英雄技能引用缺失或不在扫描结果中：{AssetDatabase.GetAssetPath(hero)} / 槽位 {i + 1}");
            }
            foreach (var skill in skills)
                if (skill.stages != null)
                    for (var i = 0; i < skill.stages.Length; i++)
                        if (skill.stages[i] == null) errors.Add($"技能阶段为空：{AssetDatabase.GetAssetPath(skill)} / 阶段 {i + 1}");
            if (errors.Count > 0) throw new InvalidOperationException("英雄技能文档导出失败：\n" + string.Join("\n", errors));

            var loadouts = new List<LoadoutRow>();
            var stages = new List<StageRow>();
            foreach (var hero in heroes)
                for (var i = 0; i < hero.skills.Length; i++)
                    loadouts.Add(new LoadoutRow { heroId = hero.configId, slot = i + 1, skillId = hero.skills[i].configId });
            foreach (var skill in skills)
            {
                var effectiveStages=SkillRecipeAdapter.GetPreviewStages(skill);
                for (var i = 0; i < effectiveStages.Length; i++) stages.Add(new StageRow(skill.configId, i + 1, effectiveStages[i],SkillRecipeAdapter.IsRecipe(skill)?skill.recipe.nodes[i]:null));
            }
            return new Snapshot
            {
                sourceProject = Path.GetFullPath(string.IsNullOrWhiteSpace(sourceProject) ? "." : sourceProject),
                heroes = heroes.Select(h => new HeroRow(h)).ToArray(),
                skills = skills.Select(s => new SkillRow(s)).ToArray(),
                loadouts = loadouts.ToArray(), stages = stages.ToArray(), monsters = ReadMonsters()
            };
        }

        private static MonsterRow[] ReadMonsters()
        {
            const string path = "Assets/Scenes/Game.unity";
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var configs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameCombatController>(true)).ToArray();
                if (configs.Length != 1) throw new InvalidOperationException("怪物文档需要 Game 场景中唯一的战斗配置。");
                var c = configs[0];
                var rows = new List<MonsterRow> { new MonsterRow { id = GameCombatController.EnemyConfigId, name = "普通怪物", usage = "未配置敌人名册时的兼容怪物", path = path,
                    health = c.enemyHealth, speed = c.enemySpeed, attackDamage = c.enemyAttackDamage, attackInterval = c.enemyAttackInterval, range = c.enemyAttackRange } };
                // 名册种类：数值为第 1 波普通档（场景基础值 × 种类倍率），攻击节奏取自动攻击模块。
                // 先列 Game 场景本局使用的名册（战场定义优先），再列开局选择目录中各战场的名册；同一怪物只列一次，用途前标注所属战场。
                var rosters = new List<(string label, EnemyRoster roster)> { (c.map ? c.map.Label : "Game", c.ActiveRoster) };
                var selection = AssetDatabase.LoadAssetAtPath<Roguelike.Flow.StartSelectionCatalog>(SelectionCatalogPath);
                if (selection && selection.maps != null)
                    foreach (var map in selection.maps) if (map) rosters.Add((map.Label, map.enemyRoster));
                var seen = new HashSet<EnemyArchetype>();
                foreach (var (label, roster) in rosters)
                foreach (var entry in roster ? roster.entries : Array.Empty<EnemySpawnEntry>())
                {
                    var a = entry?.prefab;
                    if (!a || !seen.Add(a)) continue;
                    rows.Add(new MonsterRow { id = a.configId, name = a.Label, usage = label + "：" + MonsterUsage(a, entry), path = AssetDatabase.GetAssetPath(a),
                        health = c.enemyHealth * a.healthMultiplier, speed = c.enemySpeed * a.speedMultiplier, attackDamage = c.enemyAttackDamage * a.damageMultiplier,
                        attackInterval = a.attack ? a.attack.baseAttackTime : 0, range = a.attack ? a.attack.range : 0 });
                }
                return rows.OrderBy(r => r.id).ToArray();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static string MonsterUsage(EnemyArchetype a, EnemySpawnEntry entry)
        {
            string behavior = a.behavior == EnemyBehavior.Ranged ? "远程游击" : a.behavior == EnemyBehavior.Charger ? "冲锋" : a.behavior == EnemyBehavior.Exploder ? "自爆" : "近战追击";
            var tiers = new List<string>();
            if ((entry.tiers & EnemyTierMask.Normal) != 0) tiers.Add("普通");
            if ((entry.tiers & EnemyTierMask.Elite) != 0) tiers.Add("精英");
            if ((entry.tiers & EnemyTierMask.Boss) != 0) tiers.Add("首领");
            string waves = entry.lastSecond > 0 ? $"强度时间 {entry.firstSecond}–{entry.lastSecond} 秒" : $"强度时间 {entry.firstSecond} 秒起";
            return $"{behavior}；{string.Join("/", tiers)}；{waves}；权重 {entry.weight:0.##}";
        }

        [Serializable] private sealed class MonsterRow
        {
            public int id;
            public string name, usage, path;
            public float health, speed, attackDamage, attackInterval, range;
        }

        private static T[] Find<T>() where T : UnityEngine.Object
        {
            var sourceProject = Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT");
            // LoadAllAssetsAtPath includes sub-assets; a file GUID alone is not an object's stable identity.
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal)
                .Where(p => string.IsNullOrWhiteSpace(sourceProject) || File.Exists(Path.Combine(sourceProject, p)))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>().Distinct()
                .OrderBy(a => AssetDatabase.GetAssetPath(a), StringComparer.Ordinal).ThenBy(a => a.name, StringComparer.Ordinal).ToArray();
        }

        private static void CheckId(string id, UnityEngine.Object asset, Dictionary<string, string> ids, List<string> errors)
        {
            var path = AssetDatabase.GetAssetPath(asset) + " (" + asset.name + ")";
            if (string.IsNullOrWhiteSpace(id) || id.Trim() != id) { errors.Add("稳定 ID 为空或包含首尾空格：" + path); return; }
            if (ids.TryGetValue(id, out var previous)) errors.Add($"稳定 ID 重复：{id}；{previous}；{path}");
            else ids.Add(id, path);
        }

        private static string Usage(string path)
        {
            if (path.StartsWith("Assets/GameData/编辑示例/", StringComparison.Ordinal)) return path.Contains("换槽") ? "换槽示例" : "工作台示例";
            if (path.StartsWith("Assets/GameData/", StringComparison.Ordinal)) return "正式配置";
            return "未分类";
        }

        [Serializable] private sealed class Snapshot
        {
            public int schemaVersion = 2;
            public string sourceProject;
            public string distanceUnit = "Unity 世界单位";
            public string timeUnit = "秒";
            public MonsterRow[] monsters;
            public HeroRow[] heroes;
            public SkillRow[] skills;
            public LoadoutRow[] loadouts;
            public StageRow[] stages;
        }

        [Serializable] private sealed class HeroRow
        {
            public int id;
            public string legacyId, name, path, usage;
            public float health, speed, attackDamage, attackInterval, range;
            public HeroRow(HeroDefinition h)
            {
                id = h.configId; legacyId = h.stableId; name = h.displayName; path = AssetDatabase.GetAssetPath(h); usage = Usage(path);
                health = h.health; speed = h.speed; attackDamage = h.attackDamage; attackInterval = h.attackInterval; range = h.range;
            }
        }

        [Serializable] private sealed class SkillRow
        {
            public int id;
            public string legacyId, name, path, usage, aim;
            public float castRange, cooldown, castTime, recoveryTime, manaCost, rechargeSeconds;
            public int maxCharges, requiredHits, stageCount;
            public string legacyDelivery, legacyTargets;
            public float legacyRadius, legacyAreaDuration, legacyAreaInterval;
            public LegacyEffectRow[] legacyEffects;
            public SkillRecipe recipe;
            public SkillRow(SkillDefinition s)
            {
                id = s.configId; legacyId = s.stableId; name = s.displayName; path = AssetDatabase.GetAssetPath(s); usage = Usage(path); aim = s.aim.ToString();
                castRange = s.castRange; cooldown = s.cooldown; castTime = s.castTime; recoveryTime = s.recoveryTime;
                manaCost = s.manaCost; maxCharges = s.maxCharges; rechargeSeconds = s.rechargeSeconds; requiredHits = s.requiredHits;
                stageCount = SkillRecipeAdapter.GetPreviewStages(s).Length;
                recipe=SkillRecipeAdapter.IsRecipe(s)?s.recipe:null;
                legacyEffects = Array.Empty<LegacyEffectRow>();
                if (stageCount != 0) return;
                legacyDelivery = s.delivery.ToString(); legacyTargets = s.targets.ToString(); legacyRadius = s.radius;
                legacyAreaDuration = s.areaDuration; legacyAreaInterval = s.areaInterval;
                legacyEffects = (s.effects ?? Array.Empty<EffectEntry>()).Select(e => new LegacyEffectRow(e)).ToArray();
            }
        }

        [Serializable] private sealed class LoadoutRow { public int heroId, skillId, slot; }

        [Serializable] private sealed class StageRow
        {
            public int skillId; public string delivery, scope, shape, targets;
            public int index, repeatCount;
            public float delay, repeatInterval, range, radius, hitRadius, halfAngleDegrees, speed, arcHeight, flightSeconds, launchHeight, arrivalHeight;
            public EffectRow[] effects, impactEffects;
            public int nodeId,parentNodeId;
            public string trigger;
            public StageRow(int id, int number, SkillStage s,RecipeNode node=null)
            {
                skillId = id; index = number; delay = s.delay; repeatCount = s.repeatCount; repeatInterval = s.repeatInterval;
                delivery = s.delivery.ToString(); scope = s.scope.ToString(); shape = s.shape.ToString(); targets = s.targets.ToString();
                range = s.range; radius = s.radius; hitRadius = s.hitRadius; halfAngleDegrees = s.halfAngleDegrees; speed = s.speed;
                arcHeight = s.arcHeight; flightSeconds = s.flightSeconds; launchHeight = s.launchHeight; arrivalHeight = s.arrivalHeight;
                nodeId=node?.nodeId??number; parentNodeId=node?.parentNodeId??0;trigger=(node?.trigger??RecipeTrigger.OnRelease).ToString();
                effects = (s.effects ?? Array.Empty<EffectDefinition>()).Select((e,i) => new EffectRow(e,node?.effects[i])).ToArray();
                impactEffects = (s.impactEffects ?? Array.Empty<EffectDefinition>()).Select((e,i) => new EffectRow(e,node?.impactEffects[i])).ToArray();
            }
        }

        [Serializable] private sealed class EffectRow
        {
            public string name, type, path, parameters, buffId, buffPath, buffParameters;
            public int effectId,buffNumericId;
            public bool overrideAmount;
            public float effectiveAmount;
            public EffectRow(EffectDefinition e,RecipeEffectUse use=null)
            {
                if (!e) { name = "缺失效果引用"; return; }
                name = e.name; type = e.GetType().Name; path = AssetDatabase.GetAssetPath(e); parameters = JsonUtility.ToJson(e);
                effectId=e.configId;overrideAmount=use?.overrideAmount??false;effectiveAmount=overrideAmount?use.amount:e.Compile().Amount;
                if (!e.ReferencedBuff) return;
                buffNumericId=e.ReferencedBuff.configId;
                buffId = e.ReferencedBuff.stableId; buffPath = AssetDatabase.GetAssetPath(e.ReferencedBuff); buffParameters = JsonUtility.ToJson(e.ReferencedBuff);
            }
        }

        [Serializable] private sealed class LegacyEffectRow
        {
            public string kind, buffId, buffPath, buffParameters;
            public float amount, buffScale;
            public LegacyEffectRow(EffectEntry e)
            {
                kind = e.kind.ToString(); amount = e.amount; buffScale = e.buffScale;
                if (!e.buff) return;
                buffId = e.buff.stableId; buffPath = AssetDatabase.GetAssetPath(e.buff); buffParameters = JsonUtility.ToJson(e.buff);
            }
        }
    }
}
