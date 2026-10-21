using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>编辑期数字编号登记。GUID/localID 与内部兼容键共同识别资源身份，删除后的历史编号永久保留；不进入战斗热路径。</summary>
    public static class NumericConfigIds
    {
        public const string LedgerPath = "Docs/游戏配置/编号登记.json";
        private static readonly Dictionary<string, int> Preferred = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "ability.frost_focus", 1000 }, { "ability.volley", 1001 },
            { "ability.frost_impact", 1002 }, { "ability.crystal_arrow", 1003 },
            { "hero.frost_archer", 2000 }, { "hero.frost_archer_alternate", 2001 }
        };

        [Serializable] public sealed class Ledger
        {
            public int schemaVersion = 1;
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable] public sealed class Entry
        {
            public int id;
            public string category, assetKey, stableId, path, name;
            public bool retired;
        }

        private sealed class Definition
        {
            public Object Asset;
            public string Category, Key, StableId, Path;
            public int Id;
            public int Start => Category == "skill" ? 1000 : 2000;
        }

        /// <summary>显式迁移全部缺失编号。先校验并规划整个批次，成功后才保存账本与资源；不会改动字符串键或 GUID。</summary>
        public static void AssignMissing()
        {
            var definitions = Scan();
            var ledger = ReadLedger();
            Reconcile(definitions, ledger, false);
            foreach (var definition in definitions.OrderBy(d => Preferred.TryGetValue(d.StableId ?? "", out var id) ? id : int.MaxValue).ThenBy(d => d.Path, StringComparer.Ordinal))
                Assign(definition, ledger);
            Commit(definitions, ledger);
            Debug.Log($"数字配置编号迁移完成：{definitions.Count(d => d.Category == "skill")} 个技能、{definitions.Count(d => d.Category == "hero")} 个英雄；账本 {LedgerPath}");
        }

        /// <summary>资源创建后调用；复制资源必须先将 configId 清零。返回登记后的数字 ID。</summary>
        public static int EnsureSkill(SkillDefinition skill) => Ensure(skill);
        public static int EnsureHero(HeroDefinition hero) => Ensure(hero);

        private static int Ensure(Object asset)
        {
            if (!asset || !EditorUtility.IsPersistent(asset)) throw new InvalidOperationException("请先保存英雄或技能资源，再分配数字编号。");
            var definitions = Scan();
            var selected = definitions.SingleOrDefault(d => d.Asset == asset);
            if (selected == null) throw new InvalidOperationException("资源未包含在当前编号扫描范围：" + AssetDatabase.GetAssetPath(asset));
            var ledger = ReadLedger();
            Reconcile(definitions, ledger, false);
            Assign(selected, ledger);
            Commit(new[] { selected }, ledger);
            return selected.Id;
        }

        /// <summary>只读校验：拒绝缺失、重复、越界与历史编号盗用；不自动修正开发者填写的数据。</summary>
        public static void ValidateAll()
        {
            Reconcile(Scan(), ReadLedger(), true);
        }

        public static SkillDefinition FindSkill(int id) => Find<SkillDefinition>(id, 1000);
        public static HeroDefinition FindHero(int id) => Find<HeroDefinition>(id, 2000);

        private static T Find<T>(int id, int start) where T : Object
        {
            if (id < start || id >= start + 1000) throw new ArgumentOutOfRangeException(nameof(id), "数字 ID 不在该类别范围内。");
            var definitions = Scan();
            Reconcile(definitions, ReadLedger(), true);
            return definitions.SingleOrDefault(d => d.Id == id)?.Asset as T;
        }

        private static List<Definition> Scan()
        {
            string source = Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT");
            if (!string.IsNullOrWhiteSpace(source) && !Directory.Exists(Path.Combine(source, "Assets")))
                throw new InvalidOperationException("数字编号来源项目不存在或缺少 Assets：" + source);
            var paths = AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:HeroDefinition", new[] { "Assets" }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal);
            var result = new List<Definition>();
            foreach (var path in paths)
            {
                // 来源路径只用于过滤；所有加载和保存始终通过当前 Editor 的 AssetDatabase，绝不写来源项目。
                if (!string.IsNullOrWhiteSpace(source) && !File.Exists(Path.Combine(source, path))) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is SkillDefinition) && !(asset is HeroDefinition)) continue;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                        throw new InvalidOperationException("无法读取资源 GUID：" + path);
                    var skill = asset as SkillDefinition;
                    var hero = asset as HeroDefinition;
                    result.Add(new Definition { Asset = asset, Key = guid + ":" + localId, Path = path,
                        Category = skill ? "skill" : "hero", Id = skill ? skill.configId : hero.configId,
                        StableId = skill ? skill.stableId : hero.stableId });
                }
            }
            return result;
        }

        private static Ledger ReadLedger()
        {
            var ledger = File.Exists(LedgerPath) ? JsonUtility.FromJson<Ledger>(File.ReadAllText(LedgerPath)) : new Ledger();
            if (ledger == null || ledger.schemaVersion != 1 || ledger.entries == null)
                throw new InvalidOperationException("数字编号登记格式无效：" + LedgerPath);
            var ids = new HashSet<int>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in ledger.entries)
            {
                int start = entry?.category == "skill" ? 1000 : entry?.category == "hero" ? 2000 : entry?.category == "monster" ? 3000 : 0;
                if (entry == null || start == 0 || entry.id < start || entry.id >= start + 1000 || !ids.Add(entry.id) || string.IsNullOrWhiteSpace(entry.assetKey) || !keys.Add(entry.assetKey))
                    throw new InvalidOperationException("编号登记含重复、越界或无所有者条目：" + LedgerPath);
            }
            return ledger;
        }

        private static void Reconcile(List<Definition> definitions, Ledger ledger, bool requireAssigned)
        {
            var occupied = new Dictionary<int, string>();
            var stableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.StableId) || definition.StableId.Trim() != definition.StableId || !stableIds.Add(definition.StableId))
                    throw new InvalidOperationException("内部兼容键为空、重复或含首尾空格：" + definition.Path);
                // Unity 删除并立即重建同路径时可能复用 GUID 缓存。旧键原样保留；兼容键已改变的
                // 新配置使用带身份后缀的独立登记，不能仅凭 GUID 取回已删除配置的编号。
                var originalOwner = ledger.entries.SingleOrDefault(e => e.assetKey == definition.Key);
                if (originalOwner != null && (!string.Equals(originalOwner.stableId, definition.StableId, StringComparison.Ordinal) || originalOwner.category != definition.Category))
                    definition.Key += "|" + definition.StableId;
                var owner = ledger.entries.SingleOrDefault(e => e.assetKey == definition.Key);
                if (definition.Id == 0)
                {
                    if (requireAssigned) throw new InvalidOperationException("数字 ID 未分配：" + definition.Path + "；请先执行编号迁移。");
                    continue;
                }
                if (definition.Id < definition.Start || definition.Id >= definition.Start + 1000)
                    throw new InvalidOperationException($"数字 ID 越界：{definition.Id}；{definition.Path}");
                if (occupied.TryGetValue(definition.Id, out string previous))
                    throw new InvalidOperationException($"数字 ID 重复：{definition.Id}；{previous}；{definition.Path}。复制技能请使用设计器的复制功能。");
                occupied.Add(definition.Id, definition.Path);
                var registered = ledger.entries.SingleOrDefault(e => e.id == definition.Id);
                if ((owner != null && owner.id != definition.Id) || (registered != null && registered.assetKey != definition.Key))
                    throw new InvalidOperationException($"数字 ID 与历史登记所有者不一致：{definition.Id}；{definition.Path}");
                if (Preferred.ContainsValue(definition.Id) && (!Preferred.TryGetValue(definition.StableId, out int preferred) || preferred != definition.Id))
                    throw new InvalidOperationException($"数字 ID {definition.Id} 为既有正式配置保留：{definition.Path}");
            }
            // 在内存登记所有已有编号，避免分配新编号时与尚未入账的旧资源冲突。
            foreach (var definition in definitions.Where(d => d.Id != 0)) Register(definition, ledger);
            var liveKeys = new HashSet<string>(definitions.Select(d => d.Key));
            foreach (var entry in ledger.entries.Where(e => e.category == "skill" || e.category == "hero")) entry.retired = !liveKeys.Contains(entry.assetKey);
        }

        private static void Assign(Definition definition, Ledger ledger)
        {
            if (definition.Id != 0) return;
            var previous = ledger.entries.SingleOrDefault(e => e.assetKey == definition.Key);
            if (previous != null) definition.Id = previous.id;
            else
            {
                int preferred = Preferred.TryGetValue(definition.StableId, out var value) ? value : 0;
                var occupied = new HashSet<int>(ledger.entries.Select(e => e.id));
                if (preferred != 0)
                {
                    if (occupied.Contains(preferred)) throw new InvalidOperationException("正式配置预留编号已被其他资源占用：" + preferred);
                    definition.Id = preferred;
                }
                else
                {
                    definition.Id = NextAvailable(definition.Start, occupied);
                }
            }
            Register(definition, ledger);
        }

        internal static int NextAvailable(int start, ISet<int> occupied)
        {
            for (int id = start; id < start + 1000; id++)
                if (!occupied.Contains(id) && !Preferred.ContainsValue(id)) return id;
            throw new InvalidOperationException($"数字编号区间 {start}–{start + 999} 已耗尽，不能跨类别分配。");
        }

        private static void Register(Definition definition, Ledger ledger)
        {
            var entry = ledger.entries.SingleOrDefault(e => e.assetKey == definition.Key);
            if (entry == null) { entry = new Entry { id = definition.Id, assetKey = definition.Key }; ledger.entries.Add(entry); }
            entry.category = definition.Category; entry.stableId = definition.StableId;
            entry.path = definition.Path; entry.name = definition.Asset.name; entry.retired = false;
        }

        private static void Commit(IEnumerable<Definition> definitions, Ledger ledger)
        {
            string path = Path.GetFullPath(LedgerPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ledger.entries = ledger.entries.OrderBy(e => e.id).ToList();
            // 先保留编号所有权，再保存资源；中断后按同一 GUID 和兼容键恢复编号，不把旧编号给新配置。
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(ledger, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            foreach (var definition in definitions)
            {
                int current = definition.Asset is SkillDefinition s ? s.configId : ((HeroDefinition)definition.Asset).configId;
                if (current == definition.Id) continue;
                if (definition.Asset is SkillDefinition skill) skill.configId = definition.Id;
                else ((HeroDefinition)definition.Asset).configId = definition.Id;
                EditorUtility.SetDirty(definition.Asset);
                AssetDatabase.SaveAssetIfDirty(definition.Asset);
            }
        }
    }
}
