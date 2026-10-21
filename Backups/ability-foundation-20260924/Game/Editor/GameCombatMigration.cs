using System;
using System.IO;
using Roguelike.Combat;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>
    /// 显式迁移 Game 的技能配置、施法预览和场景展示。再次执行会复用资源与节点，
    /// 不触碰独立视野场景、共享迷雾材质和已保存的 HUD 排版。
    /// </summary>
    public static class GameCombatMigration
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string HudPrefabPath = "Assets/Resources/UI/Panels/UI.Game.BottomHUD.prefab";
        private const string SkillRoot = "Assets/Game/Skills";
        private const string IndicatorRoot = SkillRoot + "/Indicators";
        private const string OriginalRangePath = "Assets/Game/SkillIndicatorDemo/Prefabs/CastRange.prefab";
        private const string OriginalAreaPath = "Assets/Game/SkillIndicatorDemo/Prefabs/TargetArea.prefab";
        private const string RangePath = IndicatorRoot + "/GameCastRange.prefab";
        private const string AreaPath = IndicatorRoot + "/GameTargetArea.prefab";
        private const string OverlayShaderPath = IndicatorRoot + "/GameIndicatorOverlay.shader";
        private const string OverlayMaterialPath = IndicatorRoot + "/GameIndicatorOverlay.mat";
        private const string BackupRoot = "Backups/game-combat-refactor-20260923";

        [MenuItem("Tools/Roguelike/Migrate Game Combat")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play 模式后迁移 Game 战斗场景。");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RequireBackups();
            RequireAsset<GameObject>(OriginalRangePath);
            RequireAsset<GameObject>(OriginalAreaPath);
            RequireAsset<GameObject>(HudPrefabPath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = FindController(scene);
            RequireControllerFields(controller);

            EnsureFolder(SkillRoot);
            EnsureFolder(IndicatorRoot);
            var overlayMaterial = EnsureOverlayMaterial();
            var q = EnsureSkill<CombatQSkillConfig>(SkillRoot + "/Q Frost Focus.asset");
            var w = EnsureSkill<CombatWSkillConfig>(SkillRoot + "/W Volley.asset");
            var e = EnsureSkill<CombatESkillConfig>(SkillRoot + "/E Frost Impact.asset");
            var r = EnsureSkill<CombatRSkillConfig>(SkillRoot + "/R Crystal Arrow.asset");
            // The old Game scene stores WDamage=30. A later manual asset edit must not be silently overwritten.
            if (!Mathf.Approximately(w.damage, 30f))
                throw new InvalidOperationException("W 配置资产伤害不是旧 Game 场景的 30；请检查手工改动后再迁移。");

            var serialized = new SerializedObject(controller);
            SetReference(serialized, "qSkill", q);
            SetReference(serialized, "wSkill", w);
            SetReference(serialized, "eSkill", e);
            SetReference(serialized, "rSkill", r);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var ground = EnsureLitMaterial("GameGround", new Color(0.24f, 0.30f, 0.29f));
            var highGround = EnsureLitMaterial("GameHighGround", new Color(0.31f, 0.38f, 0.37f));
            var tree = EnsureLitMaterial("GameTree", new Color(0.17f, 0.39f, 0.32f));
            ApplyWorldMaterials(scene, controller, ground, highGround, tree);
            RequireNoFogGroundRenderers(scene);
            int removedVisionComponents = RemoveGameVisionComponents(scene);

            var rangePrefab = EnsureGameCirclePrefab(OriginalRangePath, RangePath, "Game Cast Range", overlayMaterial);
            var areaPrefab = EnsureGameCirclePrefab(OriginalAreaPath, AreaPath, "Game Target Area", overlayMaterial);
            var indicator = EnsureIndicator(scene, rangePrefab, areaPrefab, overlayMaterial);
            serialized = new SerializedObject(controller);
            SetReference(serialized, "abilityIndicator", indicator);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureHudButtons();
            controller.gameObject.name = "Game Combat";
            var eventSystem = FindNamed(scene, "Demo EventSystem");
            if (eventSystem != null) eventSystem.name = "Game EventSystem";

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Game 场景保存失败：" + ScenePath);
            Debug.Log($"GAME_COMBAT_MIGRATED skills=4 indicator=ready hudButtons=4 visionComponentsRemoved={removedVisionComponents}");
        }

        private static void RequireBackups()
        {
            if (!File.Exists(BackupRoot + "/" + ScenePath) ||
                !File.Exists(BackupRoot + "/" + HudPrefabPath))
                throw new IOException("缺少 Game 场景或 BottomHUD 的迁移前备份：" + BackupRoot);
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException("Game 战斗迁移缺少资源：" + path);
            return asset;
        }

        private static GameCombatController FindController(Scene scene)
        {
            GameCombatController result = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<GameCombatController>(true))
                {
                    if (result != null) throw new InvalidOperationException("Game 场景含多个 GameCombatController。");
                    result = candidate;
                }
            return result != null ? result : throw new InvalidOperationException("Game 场景缺少 GameCombatController。");
        }

        private static void RequireControllerFields(GameCombatController controller)
        {
            var serialized = new SerializedObject(controller);
            foreach (var name in new[] { "qSkill", "wSkill", "eSkill", "rSkill", "abilityIndicator" })
                if (serialized.FindProperty(name) == null)
                    throw new InvalidOperationException("GameCombatController 缺少序列化字段：" + name);
        }

        private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Game 序列化字段不存在：" + name);
            if (property.objectReferenceValue != value) property.objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new DirectoryNotFoundException("资源父目录不存在：" + path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T EnsureSkill<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("技能资产路径已被其他类型占用：" + path);
            var skill = ScriptableObject.CreateInstance<T>();
            skill.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(skill, path);
            return skill;
        }

        private static Material EnsureLitMaterial(string name, Color color)
        {
            string path = "Assets/Game/Materials/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Game 战斗迁移需要 URP Lit Shader。");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader)
                    throw new InvalidOperationException("已有 Game 材质不是 URP Lit，请手工检查：" + path);
                return material;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Game 材质路径已被其他资源占用：" + path);
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EnsureOverlayMaterial()
        {
            var shader = RequireAsset<Shader>(OverlayShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(OverlayMaterialPath);
            if (material != null)
            {
                if (material.shader != shader)
                    throw new InvalidOperationException("Game 指示器材质使用了错误 Shader：" + OverlayMaterialPath);
                return material;
            }
            if (AssetDatabase.LoadMainAssetAtPath(OverlayMaterialPath) != null)
                throw new InvalidOperationException("Game 指示器材质路径已被其他资源占用：" + OverlayMaterialPath);
            material = new Material(shader) { name = "GameIndicatorOverlay", enableInstancing = true };
            material.SetColor("_BaseColor", new Color(0.2f, 0.83f, 1f, 0.9f));
            AssetDatabase.CreateAsset(material, OverlayMaterialPath);
            return material;
        }

        private static void ApplyWorldMaterials(Scene scene, GameCombatController controller,
            Material ground, Material highGround, Material tree)
        {
            controller.groundMaterial = ground;
            SetRendererMaterial(scene, "Arena", ground);
            SetRendererMaterial(scene, "High Ground Region - Game", highGround);
            for (int i = 1; i <= 3; i++) SetRendererMaterial(scene, "Tree - Game " + i, tree);
        }

        private static void SetRendererMaterial(Scene scene, string objectName, Material material)
        {
            var target = FindNamed(scene, objectName);
            if (target == null) throw new InvalidOperationException("Game 场景缺少展示对象：" + objectName);
            var renderer = target.GetComponent<Renderer>() ?? target.GetComponentInChildren<Renderer>(true);
            if (renderer == null) throw new InvalidOperationException("Game 对象缺少 Renderer：" + objectName);
            if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
        }

        private static int RemoveGameVisionComponents(Scene scene)
        {
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    string typeName = behaviour.GetType().FullName;
                    if (typeName != "Roguelike.FogOfWar.FogHeightRegion" &&
                        typeName != "Roguelike.FogOfWar.FogObstacle") continue;
                    UnityEngine.Object.DestroyImmediate(behaviour);
                    removed++;
                }
            return removed;
        }

        private static void RequireNoFogGroundRenderers(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && material.shader != null &&
                            material.shader.name == "Roguelike/FogDemo/WorldVisibility")
                            throw new InvalidOperationException("Game 仍有 Renderer 使用迷雾地面材质：" + renderer.name);
        }

        private static GameObject EnsureGameCirclePrefab(string originalPath, string path, string name,
            Material overlayMaterial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("指示器 Prefab 路径已被其他资源占用：" + path);
            var contents = PrefabUtility.LoadPrefabContents(existing != null ? path : originalPath);
            try
            {
                bool changed = existing == null;
                if (contents.name != name) { contents.name = name; changed = true; }
                foreach (var node in contents.GetComponentsInChildren<Transform>(true))
                    if (node != null && node != contents.transform &&
                        node.name.StartsWith("DemoOnly ", StringComparison.Ordinal))
                    {
                        UnityEngine.Object.DestroyImmediate(node.gameObject);
                        changed = true;
                    }
                // The source Area prefab lists its decorative ring in outlines. Keep only the
                // exact geometry boundary so the Game copy has no missing or demo-only references.
                var view = contents.GetComponent("CircleIndicatorView");
                var boundary = contents.transform.Find("Boundary")?.GetComponent<Renderer>();
                if (view == null || boundary == null)
                    throw new InvalidOperationException("独立指示器 Prefab 缺少 CircleIndicatorView/Boundary：" + originalPath);
                var serializedView = new SerializedObject(view);
                var outlines = serializedView.FindProperty("outlines");
                if (outlines == null) throw new InvalidOperationException("CircleIndicatorView 缺少 outlines 字段。");
                if (outlines.arraySize != 1 || outlines.GetArrayElementAtIndex(0).objectReferenceValue != boundary)
                {
                    outlines.arraySize = 1;
                    outlines.GetArrayElementAtIndex(0).objectReferenceValue = boundary;
                    serializedView.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial != overlayMaterial)
                    {
                        renderer.sharedMaterial = overlayMaterial;
                        changed = true;
                    }
                if (!changed) return existing;
                var saved = PrefabUtility.SaveAsPrefabAsset(contents, path);
                return saved != null ? saved : throw new IOException("指示器 Prefab 保存失败：" + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static GameAbilityIndicator EnsureIndicator(Scene scene, GameObject rangePrefab, GameObject areaPrefab,
            Material overlayMaterial)
        {
            var effects = FindNamed(scene, "Effects");
            if (effects == null) throw new InvalidOperationException("Game 场景缺少 03_Presentation/Effects 节点。");
            var node = effects.transform.Find("GameAbilityIndicator");
            if (node == null)
            {
                node = new GameObject("GameAbilityIndicator").transform;
                node.SetParent(effects.transform, false);
            }
            var indicator = node.GetComponent<GameAbilityIndicator>();
            if (indicator == null) indicator = node.gameObject.AddComponent<GameAbilityIndicator>();
            var range = EnsureCircleChild(node, "Game Cast Range", rangePrefab);
            var area = EnsureCircleChild(node, "Game Target Area", areaPrefab);
            var outline = EnsureOutline(node, overlayMaterial);
            var serialized = new SerializedObject(indicator);
            SetReference(serialized, "rangeCircle", range);
            SetReference(serialized, "areaCircle", area);
            SetReference(serialized, "outline", outline);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!indicator.HasBindings) throw new InvalidOperationException("Game 技能指示器引用未绑定完整。");
            return indicator;
        }

        private static Component EnsureCircleChild(Transform parent, string name, GameObject prefab)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (instance == null) throw new InvalidOperationException("无法实例化技能指示器：" + name);
                instance.name = name;
                child = instance.transform;
                instance.SetActive(false);
            }
            var circle = child.GetComponent("CircleIndicatorView");
            if (circle == null) throw new InvalidOperationException("指示器缺少 CircleIndicatorView：" + name);
            return circle;
        }

        private static LineRenderer EnsureOutline(Transform parent, Material overlayMaterial)
        {
            var node = parent.Find("Directional Outline");
            if (node == null)
            {
                node = new GameObject("Directional Outline").transform;
                node.SetParent(parent, false);
            }
            var line = node.GetComponent<LineRenderer>();
            if (line == null) line = node.gameObject.AddComponent<LineRenderer>();
            if (line.sharedMaterial != overlayMaterial) line.sharedMaterial = overlayMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = 0.10f;
            line.endWidth = 0.10f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void EnsureHudButtons()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                const string command = "combat.ability";
                var keys = new[] { "Q", "W", "E", "R" };
                for (int i = 0; i < keys.Length; i++)
                {
                    var slot = FindChild(root.transform, "Skill " + keys[i]);
                    if (slot == null) throw new InvalidOperationException("BottomHUD 缺少技能槽：" + keys[i]);
                    var rect = slot.GetComponent<RectTransform>();
                    var image = slot.GetComponent<Image>();
                    if (rect == null || image == null) throw new InvalidOperationException("技能槽缺少 RectTransform/Image：" + keys[i]);
                    rect.sizeDelta = new Vector2(Mathf.Max(90f, rect.sizeDelta.x), Mathf.Max(90f, rect.sizeDelta.y));
                    image.raycastTarget = true;
                    foreach (var label in slot.GetComponentsInChildren<Text>(true)) label.raycastTarget = false;
                    var button = slot.GetComponent<Button>();
                    if (button == null) button = slot.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    var action = slot.GetComponent<UIAction>();
                    if (action == null) action = slot.gameObject.AddComponent<UIAction>();
                    action.command = command;
                    action.value = i;
                }
                var title = FindChild(root.transform, "AbilityTitle")?.GetComponent<Text>();
                if (title != null && title.text.Contains("后续开放")) title.text = "寒冰主动技能";
                if (PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath) == null)
                    throw new IOException("BottomHUD Prefab 保存失败：" + HudPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static GameObject FindNamed(Scene scene, string name)
        {
            GameObject result = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != name) continue;
                    if (result != null) throw new InvalidOperationException("Game 场景重复节点：" + name);
                    result = child.gameObject;
                }
            return result;
        }
    }
}
