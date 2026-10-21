using System;
using Roguelike.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Roguelike.Editor
{
    /// <summary>将少量展示对象持久化到场景；迁移再次运行时保留已有布局和引用。</summary>
    internal static class CombatSceneAuthoring
    {
        public static void EnsureViews(CombatDemoController demo)
        {
            var serialized = new SerializedObject(demo);
            var root = demo.transform.Find("Scene Views");
            if (root == null)
            {
                root = new GameObject("Scene Views").transform;
                root.SetParent(demo.transform, false);
                var arena = Primitive(root, "Arena", PrimitiveType.Plane, demo.groundMaterial);
                arena.localScale = Vector3.one * (demo.arenaHalfSize * 2 / 10);
                var player = Primitive(root, "Player", PrimitiveType.Capsule, demo.playerMaterial);
                player.localPosition = new Vector3(0, 0.95f, 0);
                player.localScale = new Vector3(0.8f, 0.95f, 0.8f);
                var facing = Primitive(player, "Facing", PrimitiveType.Cube, demo.projectileMaterial);
                facing.localPosition = new Vector3(0, 0.25f, 0.65f);
                facing.localScale = new Vector3(0.2f, 0.2f, 0.6f);
                var marker = Primitive(root, "Move destination", PrimitiveType.Cylinder, demo.markerMaterial);
                marker.localPosition = new Vector3(0, 0.035f, 0);
                marker.localScale = new Vector3(0.65f, 0.025f, 0.65f);
                marker.gameObject.SetActive(false);
                serialized.FindProperty("playerView").objectReferenceValue = player;
                serialized.FindProperty("destinationMarker").objectReferenceValue = marker;
            }
            if (serialized.FindProperty("hud").objectReferenceValue == null)
            {
                var existing = demo.GetComponentInChildren<CombatHud>(true);
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf");
                if (font == null) throw new InvalidOperationException("缺少场景中文字体资源。");
                serialized.FindProperty("hud").objectReferenceValue = existing != null
                    ? existing : new CombatHudSceneBuilder().Build(demo.transform, font);
            }
            BindMeshIfMissing(serialized, "enemyMesh", PrimitiveType.Cube);
            BindMeshIfMissing(serialized, "projectileMesh", PrimitiveType.Sphere);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Demo EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(demo.transform, false);
            }
            Validate(demo);
        }

        /// <summary>构建前检查持久化引用，避免进入游戏后静默生成缺失对象。</summary>
        public static void Validate(CombatDemoController demo)
        {
            var serialized = new SerializedObject(demo);
            foreach (var field in new[] { "playerView", "destinationMarker", "hud", "enemyMesh", "projectileMesh" })
                if (serialized.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("缺少场景引用：" + field);
            var hud = (CombatHud)serialized.FindProperty("hud").objectReferenceValue;
            if (!hud.HasBindings) throw new InvalidOperationException("HUD 序列化引用不完整。");
        }

        private static void BindMeshIfMissing(SerializedObject serialized, string field, PrimitiveType primitive)
        {
            var property = serialized.FindProperty(field);
            if (property.objectReferenceValue != null) return;
            var temporary = GameObject.CreatePrimitive(primitive);
            property.objectReferenceValue = temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
        }

        /// <summary>仅用于批处理回归：重复迁移不增加节点，并保留手动布局修改。</summary>
        public static void ValidateMigrationPreservesEdits()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameDemoTools.ScenePath);
            var demo = UnityEngine.Object.FindFirstObjectByType<CombatDemoController>();
            Validate(demo);
            int count = demo.GetComponentsInChildren<Transform>(true).Length;
            var hud = demo.GetComponentInChildren<CombatHud>(true);
            var title = hud.transform.Find("Title").GetComponent<RectTransform>();
            var original = title.anchoredPosition;
            var edited = original + new Vector2(13, -17);
            try
            {
                title.anchoredPosition = edited;
                EnsureViews(demo);
                if (title.anchoredPosition != edited || demo.GetComponentsInChildren<Transform>(true).Length != count)
                    throw new InvalidOperationException("重复迁移覆盖布局或增加节点。");
                foreach (var label in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if (label.font == null || !AssetDatabase.Contains(label.font))
                        throw new InvalidOperationException("HUD 字体不是持久化资源。");
            }
            finally { title.anchoredPosition = original; }
            Debug.Log("SCENE_AUTHORING_VALIDATED: persisted bindings, font, idempotent migration, manual layout preserved.");
        }

        private static Transform Primitive(Transform parent, string name, PrimitiveType type, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
            var renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return instance.transform;
        }
    }
}
