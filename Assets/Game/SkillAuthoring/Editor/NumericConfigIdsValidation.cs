using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>主 AI 串行调用的编号迁移与拒绝规则验证；不运行战斗、不添加工具菜单。</summary>
    public static class NumericConfigIdsValidation
    {
        /// <summary>隔离副本中新建/复制的持久化集成验证。生成独立临时目录，最终清理资源但保留已消费编号。</summary>
        public static void RunCreation()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HERO_SKILL_SOURCE_PROJECT")))
                throw new InvalidOperationException("创建路径验证必须清除 HERO_SKILL_SOURCE_PROJECT，避免新增临时路径被来源过滤排除。");
            NumericConfigIds.AssignMissing();
            string folderName = "__NumericIds_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
            int assertions = 0;
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
            try
            {
                var first = SkillAuthoringAssets.CreateAtPath(folder + "/新建技能.asset");
                int firstId = first.configId;
                Check(firstId >= 1000 && firstId < 2000, "新建技能未得到正确类别数字编号");
                var copy = SkillAuthoringAssets.CreateAtPath(folder + "/复制技能.asset", first);
                int copyId = copy.configId;
                Check(copyId != firstId && copyId >= 1000 && copyId < 2000, "复制技能继承旧编号或越界");
                Check(copy.stableId != first.stableId, "复制技能内部兼容键未更新");
                Check(copy.stages[0].effects[0] == first.stages[0].effects[0], "复制技能意外破坏共享效果引用合同");
                string firstPath = AssetDatabase.GetAssetPath(first), copyPath = AssetDatabase.GetAssetPath(copy);
                AssetDatabase.ImportAsset(firstPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceUpdate);
                first = AssetDatabase.LoadAssetAtPath<SkillDefinition>(firstPath);
                copy = AssetDatabase.LoadAssetAtPath<SkillDefinition>(copyPath);
                Check(first.configId == firstId && copy.configId == copyId, "重新导入丢失持久化编号");
                Check(NumericConfigIds.FindSkill(firstId) == first && NumericConfigIds.FindSkill(copyId) == copy, "保存后按编号查找失败");
                Check(NumericConfigIds.EnsureSkill(first) == firstId, "重复登记改变已有编号");
                Check(AssetDatabase.DeleteAsset(firstPath), "测试技能删除失败");
                var replacement = SkillAuthoringAssets.CreateAtPath(firstPath);
                Check(replacement.configId != firstId && replacement.configId != copyId, "删除并重建同路径复用了历史编号");
                Check(NumericConfigIds.FindSkill(firstId) == null, "已删除编号仍指向新资源");
                var ledger = JsonUtility.FromJson<NumericConfigIds.Ledger>(File.ReadAllText(NumericConfigIds.LedgerPath));
                Check(ledger.entries.Single(e => e.id == firstId).retired, "删除资源未登记为历史编号");

                // 直接保留同一资源 GUID 改变逻辑身份，确保测试不依赖 Unity 是否复用删除路径缓存。
                int replacedId = replacement.configId;
                string replacedAlias = replacement.stableId;
                replacement.stableId = "ability." + Guid.NewGuid().ToString("N");
                bool aliasRejected = false;
                try { NumericConfigIds.EnsureSkill(replacement); }
                catch (InvalidOperationException) { aliasRejected = true; }
                Check(aliasRejected, "同 GUID 改变兼容键后仍允许携带旧编号");
                replacement.configId = 0;
                int newIdentityId = NumericConfigIds.EnsureSkill(replacement);
                Check(newIdentityId != replacedId && newIdentityId != firstId && newIdentityId != copyId, "同 GUID 新身份复用历史编号");
                Check(NumericConfigIds.EnsureSkill(replacement) == newIdentityId, "带身份后缀登记不幂等");
                ledger = JsonUtility.FromJson<NumericConfigIds.Ledger>(File.ReadAllText(NumericConfigIds.LedgerPath));
                var retiredIdentity = ledger.entries.Single(e => e.id == replacedId);
                Check(retiredIdentity.retired && retiredIdentity.stableId == replacedAlias, "同 GUID 新身份覆盖了旧登记");
                Check(NumericConfigIds.FindSkill(replacedId) == null && NumericConfigIds.FindSkill(newIdentityId) == replacement, "同 GUID 新身份查找错误");

                var missingAlias = ScriptableObject.CreateInstance<SkillDefinition>();
                string missingPath = folder + "/缺少兼容键.asset";
                AssetDatabase.CreateAsset(missingAlias, missingPath);
                bool rejected = false;
                try { NumericConfigIds.EnsureSkill(missingAlias); }
                catch (InvalidOperationException) { rejected = true; }
                finally { AssetDatabase.DeleteAsset(missingPath); }
                Check(rejected, "空内部兼容键未被拒绝");
                NumericConfigIds.ValidateAll(); assertions++;
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
            NumericConfigIds.AssignMissing(); // 将清理后的测试编号标记为历史占用。
            NumericConfigIds.ValidateAll(); assertions++;
            Debug.Log($"数字配置创建与复制验证通过：{assertions} 项断言；测试资源已清理，历史编号保留。");
        }

        /// <summary>主 AI 在隔离副本调用：迁移、规则验证、导出数字主键快照。环境变量仅过滤来源，不写来源项目。</summary>
        public static void MigrateValidateAndExport()
        {
            Run();
            NumericConfigIds.ValidateAll();
            HeroSkillDocumentation.Export();
        }

        public static void Run()
        {
            int assertions = 0;
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; }
            void Reject(Action action, string message)
            {
                bool failed = false;
                try { action(); } catch (InvalidOperationException) { failed = true; }
                Check(failed, message);
            }

            NumericConfigIds.AssignMissing();
            NumericConfigIds.ValidateAll(); assertions++;
            var expected = new[] { "ability.frost_focus", "ability.volley", "ability.frost_impact", "ability.crystal_arrow" };
            for (int i = 0; i < expected.Length; i++)
                Check(NumericConfigIds.FindSkill(1000 + i)?.stableId == expected[i], "正式技能编号映射错误");
            Check(NumericConfigIds.FindHero(2000)?.stableId == "hero.frost_archer", "主英雄编号错误");
            Check(NumericConfigIds.FindHero(2001)?.stableId == "hero.frost_archer_alternate", "换槽英雄编号错误");
            string ledger = File.ReadAllText(NumericConfigIds.LedgerPath);
            NumericConfigIds.AssignMissing();
            Check(File.ReadAllText(NumericConfigIds.LedgerPath) == ledger, "重复迁移改变账本");

            var skill = NumericConfigIds.FindSkill(1000);
            int original = skill.configId;
            try
            {
                skill.configId = 1001;
                Reject(NumericConfigIds.ValidateAll, "未拒绝重复或盗用编号");
                skill.configId = 2000;
                Reject(NumericConfigIds.ValidateAll, "未拒绝跨类别编号");
                skill.configId = 0;
                Reject(NumericConfigIds.ValidateAll, "未拒绝未分配编号");
            }
            finally { skill.configId = original; }
            var used = new HashSet<int>(Enumerable.Range(1000, 1000));
            Reject(() => NumericConfigIds.NextAvailable(1000, used), "已耗尽区间发生跨区分配");
            used.Remove(1099);
            Check(NumericConfigIds.NextAvailable(1000, used) == 1099, "空闲编号分配错误");
            Check(NumericConfigIds.NextAvailable(1000, new HashSet<int> { 1004, 1005 }) == 1006, "未保留预留编号或已退役占用编号");
            Check(NumericConfigIds.NextAvailable(2000, new HashSet<int>()) == 2002, "未保留正式英雄编号");
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            var first = ScriptableObject.CreateInstance<SkillDefinition>();
            var second = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                first.stableId = "validation.numeric.first"; first.configId = 1097;
                second.stableId = "validation.numeric.second"; second.configId = 1098;
                first.effects = second.effects = new[] { new EffectEntry { kind = EffectKind.Damage, amount = 1 } };
                catalog.skills = new[] { first, second };
                Check(catalog.FindSkillIndex(1098) == 1, "运行库按编号查找错误");
                Check(catalog.FindSkillIndex(1099) == -1, "不存在编号未返回 -1");
                using (var compiled = catalog.Compile()) { assertions++; }
                second.configId = first.configId;
                Reject(() => { using (var compiled = catalog.Compile()) { } }, "编译接受重复编号");
                Reject(() => catalog.FindSkillIndex(1097), "查找接受重复编号");
                second.configId = 3000;
                Reject(() => { using (var compiled = catalog.Compile()) { } }, "编译接受越界编号");
                first.configId = second.configId = 0;
                using (var compiled = catalog.Compile()) { assertions++; }
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
            NumericConfigIds.ValidateAll(); assertions++;
            Debug.Log($"数字配置编号验证通过：{assertions} 项断言。");
        }
    }
}
