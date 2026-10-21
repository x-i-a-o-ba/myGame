using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Abilities;
using UnityEditor;
using UnityEngine;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>中文步骤卡片只修改配方自有数据；模块目录和效果定义始终是只读选择项。</summary>
    internal sealed class RecipeAuthoringPanels
    {
        private SkillModuleCatalog conversionCatalog;
        private string moduleSearch = "";
        internal static string StepLabel(SkillRecipe recipe, int index)
        {
            var node = recipe.nodes[index]; if (node == null) return "步骤 " + (index + 1) + "（空配置）";
            if (node.parentNodeId == 0) return "步骤 " + (index + 1) + " · 根释放";
            int parent = Array.FindIndex(recipe.nodes, n => n != null && n.nodeId == node.parentNodeId);
            string port = node.trigger switch { RecipeTrigger.OnResolved => "到达结算后", RecipeTrigger.OnProjectileHit => "首碰后", RecipeTrigger.OnDamage => "造成伤害后", RecipeTrigger.OnKill => "击杀后", _ => "未知事件" };
            return "步骤 " + (index + 1) + " · 在步骤 " + (parent + 1) + port;
        }
        internal void DrawLegacy(SkillDefinition skill, Action<Action> run)
        {
            EditorGUILayout.HelpBox("当前是旧阶段配置。显式转换会保留技能数字 ID 和旧阶段备份；转换后仅配方参与执行。", MessageType.Info);
            conversionCatalog = (SkillModuleCatalog)EditorGUILayout.ObjectField("转换目标模块目录", conversionCatalog ? conversionCatalog : skill.modules,
                typeof(SkillModuleCatalog), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("查找已有模块目录")) conversionCatalog = RecipeAuthoringCommands.FindCatalog();
                using (new EditorGUI.DisabledScope(!conversionCatalog))
                    if (GUILayout.Button("将当前技能转换为自由组合配方")) run(() => RecipeAuthoringCommands.Convert(skill, conversionCatalog));
            }
        }

        internal void Draw(SkillDefinition skill, int selected, Action<Action> run, Action<int> select, Action changed)
        {
            var so = new SerializedObject(skill); so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("modules"), new GUIContent("数字模块目录"));
            if (so.ApplyModifiedProperties()) changed();
            if (!skill.modules) { EditorGUILayout.HelpBox("请先指定模块目录。目录提供已实现的投递、目标查询和效果选项。", MessageType.Warning); return; }
            try { skill.modules.Validate(); }
            catch (Exception error) { EditorGUILayout.HelpBox("模块目录待修正：" + error.Message, MessageType.Error); return; }
            EditorGUILayout.HelpBox("每张卡片依次选择：何时触发 → 怎么到达 → 影响谁 → 做哪些效果。数值覆盖仅影响当前配方；共享效果资源不被修改。", MessageType.Info);
            moduleSearch = EditorGUILayout.TextField("筛选模块（中文名称 / 数字 ID）", moduleSearch);
            so.Update();
            var recipe = so.FindProperty("recipe");
            var nodes = recipe.FindPropertyRelative("nodes");
            if (nodes.arraySize == 0)
            {
                if (GUILayout.Button("添加第一个释放步骤")) run(() => select(RecipeAuthoringCommands.Add(skill)));
                return;
            }
            int index = Mathf.Clamp(selected, 0, nodes.arraySize - 1);
            var node = nodes.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("步骤 " + (index + 1) + " · 本地节点编号 " + node.FindPropertyRelative("nodeId").intValue, EditorStyles.boldLabel);
                DrawTrigger(skill, node, index);
                DrawDelivery(skill.modules, node);
                DrawQuery(skill.modules, node);
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("4. 对目标做什么？", EditorStyles.boldLabel);
                DrawEffects(skill.modules, node.FindPropertyRelative("effects"), "查询目标效果");
                var delivery = skill.modules.deliveries?.FirstOrDefault(d => d != null && d.id == node.FindPropertyRelative("deliveryModuleId").intValue);
                var impact = node.FindPropertyRelative("impactEffects");
                if (delivery?.kind == DeliveryKind.LinearProjectile || impact.arraySize > 0)
                {
                    if (delivery?.kind != DeliveryKind.LinearProjectile) EditorGUILayout.HelpBox("首碰效果仅限直线弹体。请移除残留效果或改用直线弹体。", MessageType.Warning);
                    DrawEffects(skill.modules, impact, "首个碰撞单位效果（先于查询目标效果）");
                }
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("5. 延迟与重复", EditorStyles.boldLabel);
                Field(node, "delay", "收到触发事件后的延迟（秒）");
                Field(node, "repeatCount", "执行次数（有限次数）");
                if (node.FindPropertyRelative("repeatCount").intValue > 1) Field(node, "repeatInterval", "重复间隔（秒）");
            }
            if (so.ApplyModifiedProperties()) changed();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加独立释放步骤")) run(() => select(RecipeAuthoringCommands.Add(skill)));
                if (GUILayout.Button("添加当前步骤的后续")) run(() => select(RecipeAuthoringCommands.Add(skill, index)));
                if (GUILayout.Button("复制当前步骤")) run(() => select(RecipeAuthoringCommands.Add(skill, index, true)));
                bool hasChildren = skill.recipe.nodes.Any(n => n != null && n.parentNodeId == skill.recipe.nodes[index].nodeId);
                using (new EditorGUI.DisabledScope(nodes.arraySize <= 1 || hasChildren))
                    if (GUILayout.Button(new GUIContent("删除步骤", hasChildren ? "请先调整依赖当前步骤的后续卡片。" : "删除当前步骤")))
                        run(() => { RecipeAuthoringCommands.Remove(skill, index); select(Math.Max(0, index - 1)); });
            }
            EditorGUILayout.HelpBox("配方范围预览使用实际编译结果；此版本请在卡片修改参数，Scene 中范围拖动已禁用。衍生步骤的命中点为静态示意，不能保证战斗中一定命中。", MessageType.None);
        }

        private static void DrawTrigger(SkillDefinition skill, SerializedProperty node, int index)
        {
            EditorGUILayout.LabelField("1. 什么时候执行？", EditorStyles.boldLabel);
            var parent = node.FindPropertyRelative("parentNodeId");
            var parents = new List<int> { 0 }; var names = new List<string> { "根释放（前摇结束）" };
            // 只提供前序步骤，编辑时无需建立任意回连图；导入数据保留原引用交给编译器校验。
            for (int i = 0; i < index; i++) if (skill.recipe.nodes[i] != null)
            { parents.Add(skill.recipe.nodes[i].nodeId); names.Add("步骤 " + (i + 1) + " · 节点 " + skill.recipe.nodes[i].nodeId); }
            if (!parents.Contains(parent.intValue)) { parents.Add(parent.intValue); names.Add("现有来源节点 " + parent.intValue + "（请检查顺序）"); }
            int selection = parents.IndexOf(parent.intValue);
            parent.intValue = parents[EditorGUILayout.Popup("触发来源", selection, names.ToArray())];
            var trigger = node.FindPropertyRelative("trigger");
            if (parent.intValue == 0)
            {
                trigger.intValue = (int)RecipeTrigger.OnRelease;
                EditorGUILayout.HelpBox("位置/方向来自本次施法输入；延迟从前摇结束算起。", MessageType.None); return;
            }
            var source = skill.recipe.nodes.FirstOrDefault(n => n != null && n.nodeId == parent.intValue);
            var delivery = skill.modules.deliveries.FirstOrDefault(d => d != null && d.id == source?.deliveryModuleId);
            bool damage = source != null && (source.effects ?? Array.Empty<RecipeEffectUse>()).Concat(source.impactEffects ?? Array.Empty<RecipeEffectUse>())
                .Any(use => use != null && skill.modules.effects.Any(e => e != null && e.definition && e.definition.configId == use.effectId && e.definition is DamageEffect));
            var choices = new List<RecipeTrigger> { RecipeTrigger.OnResolved };
            var labels = new List<string> { "到达并完成查询后（整组一次）" };
            if (delivery?.kind == DeliveryKind.LinearProjectile) { choices.Add(RecipeTrigger.OnProjectileHit); labels.Add("直线首碰后"); }
            if (damage) { choices.Add(RecipeTrigger.OnDamage); labels.Add("造成实际伤害后（生命损失大于零）"); choices.Add(RecipeTrigger.OnKill); labels.Add("击杀后（每个击杀结果）"); }
            int current = choices.IndexOf((RecipeTrigger)trigger.intValue);
            if (current < 0) { choices.Add((RecipeTrigger)trigger.intValue); labels.Add("当前事件不被来源支持（请重新选择）"); current = choices.Count - 1; }
            trigger.intValue = (int)choices[EditorGUILayout.Popup("来源步骤的事件", current, labels.ToArray())];
            EditorGUILayout.HelpBox((RecipeTrigger)trigger.intValue == RecipeTrigger.OnResolved ?
                "继承来源步骤完成时的位置/方向；该事件没有指定单位，区域为空也会发生。" :
                "继承该结果的目标与位置；击杀后保留位置快照。子步骤使用同一次根施法的衍生预算。", MessageType.None);
        }

        private void DrawDelivery(SkillModuleCatalog catalog, SerializedProperty node)
        {
            EditorGUILayout.Space(5); EditorGUILayout.LabelField("2. 怎么到达？", EditorStyles.boldLabel);
            Pick(node.FindPropertyRelative("deliveryModuleId"), "投递模块", catalog.deliveries.Where(d => d != null).Select(d => (d.id, d.displayName)));
            var delivery = catalog.deliveries.FirstOrDefault(d => d != null && d.id == node.FindPropertyRelative("deliveryModuleId").intValue);
            if (delivery == null) return;
            if (delivery.kind == DeliveryKind.ParabolicProjectile)
            {
                Field(node, "flightSeconds", "完整飞行时间（秒）"); Field(node, "arcHeight", "连线以上弧高（Unity 单位）");
                Field(node, "launchHeight", "发射高度偏移（Unity 单位）"); Field(node, "arrivalHeight", "落点高度偏移（Unity 单位）");
            }
            else if (delivery.kind != DeliveryKind.Instant) Field(node, "speed", "飞行速度（Unity 单位 / 秒）");
            if (delivery.kind == DeliveryKind.LinearProjectile) { Field(node, "range", "最大飞行距离（Unity 单位）"); Field(node, "hitRadius", "弹体碰撞半径（Unity 单位）"); }
        }

        private void DrawQuery(SkillModuleCatalog catalog, SerializedProperty node)
        {
            EditorGUILayout.Space(5); EditorGUILayout.LabelField("3. 影响谁？", EditorStyles.boldLabel);
            Pick(node.FindPropertyRelative("queryModuleId"), "目标查询模块", catalog.queries.Where(q => q != null).Select(q => (q.id, q.displayName)));
            var query = catalog.queries.FirstOrDefault(q => q != null && q.id == node.FindPropertyRelative("queryModuleId").intValue);
            if (query == null) return;
            if (query.scope == TargetScope.Area)
            {
                if (query.shape == AreaShape.Circle) Field(node, "radius", "区域半径（Unity 单位）");
                else
                {
                    Field(node, "range", "区域长度（Unity 单位）");
                    if (query.shape == AreaShape.Cone) { var angle = node.FindPropertyRelative("halfAngleDegrees"); angle.floatValue = EditorGUILayout.Slider("完整扇形张角（度）", angle.floatValue * 2, 0, 360) * .5f; }
                    else { var width = node.FindPropertyRelative("radius"); width.floatValue = EditorGUILayout.FloatField("区域完整宽度（Unity 单位）", width.floatValue * 2) * .5f; }
                }
                EditorGUILayout.HelpBox("区域按 XZ 地面平面筛选；三维抛物线高度不等于球形伤害范围。", MessageType.None);
            }
            if (query.scope == TargetScope.All) EditorGUILayout.HelpBox("全体查询覆盖符合阵营的存活单位；衍生执行次数预算不截断本次目标集合。", MessageType.Info);
            if (query.scope == TargetScope.Single) EditorGUILayout.HelpBox("单体需要单位瞄准或事件提供有效目标；命中来源和击杀后的有效性由编译器/执行器校验。", MessageType.None);
        }

        private void DrawEffects(SkillModuleCatalog catalog, SerializedProperty list, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < list.arraySize; i++)
            {
                var use = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    Pick(use.FindPropertyRelative("effectId"), "效果 " + (i + 1), catalog.effects.Where(e => e != null && e.definition).Select(e => (e.definition.configId, e.definition.name)));
                    var module = catalog.effects.FirstOrDefault(e => e != null && e.definition && e.definition.configId == use.FindPropertyRelative("effectId").intValue);
                    if (module != null)
                    {
                        EditorGUILayout.LabelField("作用对象", module.definition.target == EffectTarget.Source ? "施法者（每步一次）" : "本步骤选定目标");
                        var defaults = module.definition.Compile();
                        if (defaults.Kind == EffectKind.Damage || defaults.Kind == EffectKind.Heal)
                            EditorGUILayout.LabelField("模块默认数值", defaults.Amount + " 点");
                        if (module.definition.ReferencedBuff)
                            EditorGUILayout.LabelField("固定 Buff", module.definition.ReferencedBuff.configId + " · " + module.definition.ReferencedBuff.displayName + "（" + module.definition.ReferencedBuff.duration + " 秒）");
                        if (module.allowAmountOverride)
                        {
                            var flag = use.FindPropertyRelative("overrideAmount"); bool before = flag.boolValue;
                            Field(use, "overrideAmount", "为当前配方覆盖数值");
                            if (!before && flag.boolValue) use.FindPropertyRelative("amount").floatValue = Mathf.Clamp(defaults.Amount, module.minAmount, module.maxAmount);
                            if (use.FindPropertyRelative("overrideAmount").boolValue) Field(use, "amount", $"伤害 / 治疗（点；{module.minAmount}～{module.maxAmount}）");
                        }
                        else if (use.FindPropertyRelative("overrideAmount").boolValue)
                        { EditorGUILayout.HelpBox("当前模块不允许数值覆盖，请关闭残留覆盖。", MessageType.Warning); Field(use, "overrideAmount", "数值覆盖（不支持）"); }
                        if (use.FindPropertyRelative("overrideBuffScale").boolValue)
                        { EditorGUILayout.HelpBox("此版本使用固定 Buff 规格，请关闭残留强度覆盖。", MessageType.Warning); Field(use, "overrideBuffScale", "强度覆盖（不支持，请关闭）"); }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("上移")) { list.MoveArrayElement(i, i - 1); return; }
                        using (new EditorGUI.DisabledScope(i == list.arraySize - 1)) if (GUILayout.Button("下移")) { list.MoveArrayElement(i, i + 1); return; }
                        if (GUILayout.Button("移除此效果使用")) { list.DeleteArrayElementAtIndex(i); return; }
                    }
                }
            }
            if (GUILayout.Button("添加一个效果"))
            {
                int at = list.arraySize; list.InsertArrayElementAtIndex(at); var use = list.GetArrayElementAtIndex(at);
                use.FindPropertyRelative("effectId").intValue = catalog.effects.FirstOrDefault(e => e != null && e.definition)?.definition.configId ?? 0;
                use.FindPropertyRelative("overrideAmount").boolValue = false; use.FindPropertyRelative("amount").floatValue = 0;
                use.FindPropertyRelative("overrideBuffScale").boolValue = false; use.FindPropertyRelative("buffScale").floatValue = 1;
            }
        }

        private void Pick(SerializedProperty value, string label, IEnumerable<(int id, string name)> options)
        {
            var all = options.ToList();
            var filtered = all.Where(o => o.id == value.intValue || string.IsNullOrWhiteSpace(moduleSearch) ||
                ($"{o.id} {o.name}").Contains(moduleSearch, StringComparison.OrdinalIgnoreCase)).ToList();
            int current = filtered.FindIndex(o => o.id == value.intValue);
            if (current < 0) { filtered.Insert(0, (value.intValue, value.intValue == 0 ? "请选择" : "未登记模块")); current = 0; }
            int next = EditorGUILayout.Popup(label, current, filtered.Select(o => o.id + " · " + o.name).ToArray());
            value.intValue = filtered[next].id;
        }
        private static void Field(SerializedProperty parent, string key, string label) => EditorGUILayout.PropertyField(parent.FindPropertyRelative(key), new GUIContent(label));
    }
}
