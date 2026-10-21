using System.IO;
using Roguelike.SkillIndicators;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.SkillIndicators.Editor
{
    /// <summary>首次创建演示材质和预制体；存在的资源原样返回，保留开发者调试结果。</summary>
    internal static class IndicatorDemoAssets
    {
        internal const string Root = "Assets/Game/SkillIndicatorDemo";

        internal static Material Material(string name, Color color, bool transparent, Texture texture = null)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            Directory.CreateDirectory(Root + "/Materials");
            material = new Material(Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.name = name;
            material.SetColor("_BaseColor", color);
            if (texture) material.SetTexture("_BaseMap", texture);
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static CircleSkillPreset Preset(string name, float range, float radius, Color color)
        {
            string path = Root + "/Settings/" + name + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<CircleSkillPreset>(path);
            if (preset) return preset;
            Directory.CreateDirectory(Root + "/Settings");
            preset = ScriptableObject.CreateInstance<CircleSkillPreset>();
            preset.displayName = name; preset.castRange = range; preset.areaRadius = radius; preset.color = color;
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        internal static GameObject CirclePrefab(string name, bool filled, bool decorated, bool feedback)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            Directory.CreateDirectory(Root + "/Prefabs");
            var root = new GameObject(name);
            var view = root.AddComponent<CircleIndicatorView>();
            var cyan = new Color(0.12f, 0.8f, 1, 0.95f);
            var outline = MeshObject(root.transform, "Boundary", RingMesh(), Material("Boundary", cyan, true));
            var outlines = new System.Collections.Generic.List<Renderer> { outline };
            if (decorated)
            {
                // 下载的圆环仅作内侧柔光装饰，几何边界仍由半径为 1 的网格准确表示。
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/DemoOnly/Kenney/" + (feedback ? "circle_03.png" : "circle_02.png"));
                var glow = MeshObject(root.transform, "DemoOnly Kenney Ring", QuadMesh(), Material(feedback ? "FeedbackGlow" : "AimGlow", cyan, true, texture));
                glow.transform.localScale = new Vector3(1.9f, 1, 1.9f);
                glow.transform.localPosition = Vector3.up * 0.005f;
                outlines.Add(glow);
            }
            if (feedback)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/DemoOnly/Kenney/star_04.png");
                var spark = MeshObject(root.transform, "DemoOnly Kenney Flash", QuadMesh(), Material("CastFlash", cyan, true, texture));
                spark.transform.localScale = new Vector3(1.3f, 1, 1.3f);
                spark.transform.localPosition = Vector3.up * 0.01f;
                outlines.Add(spark);
            }
            Renderer fill = null;
            if (filled) fill = MeshObject(root.transform, "Area Fill", DiskMesh(), Material("AreaFill", new Color(cyan.r, cyan.g, cyan.b, 0.08f), true));
            var serialized = new SerializedObject(view);
            var array = serialized.FindProperty("outlines");
            array.arraySize = outlines.Count;
            for (int i = 0; i < outlines.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = outlines[i];
            serialized.FindProperty("fill").objectReferenceValue = fill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static MeshRenderer MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Mesh RingMesh() => MeshAsset("UnitRing", false, false);
        private static Mesh DiskMesh() => MeshAsset("UnitDisk", true, false);
        private static Mesh QuadMesh() => MeshAsset("UnitQuad", false, true);

        private static Mesh MeshAsset(string name, bool disk, bool quad)
        {
            string path = Root + "/Art/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            var mesh = new Mesh { name = name };
            if (quad)
            {
                mesh.vertices = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(-0.5f,0,0.5f), new Vector3(0.5f,0,0.5f), new Vector3(0.5f,0,-0.5f) };
                mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                mesh.triangles = new[] { 0,1,2,0,2,3 };
            }
            else
            {
                const int segments = 192;
                var vertices = new Vector3[(segments + 1) * 2];
                var triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    vertices[i * 2] = direction * (disk ? 0 : 0.992f);
                    vertices[i * 2 + 1] = direction;
                    if (i == segments) break;
                    int v = i * 2, t = i * 6;
                    triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+3;
                    triangles[t+3] = v; triangles[t+4] = v+3; triangles[t+5] = v+2;
                }
                mesh.vertices = vertices; mesh.triangles = triangles;
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
