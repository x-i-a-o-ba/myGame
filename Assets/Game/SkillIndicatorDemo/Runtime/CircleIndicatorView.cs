using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>可复用的场景/Prefab 圆形显示；只接收位置、半径与颜色，不处理施法规则。</summary>
    public sealed class CircleIndicatorView : MonoBehaviour
    {
        [SerializeField, Tooltip("轮廓与装饰层，使用共享材质，通过属性块着色。")] private Renderer[] outlines;
        [SerializeField, Tooltip("可选的低透明度范围填充。")] private Renderer fill;
        [SerializeField, Range(0, 1)] private float fillOpacity = 0.08f;
        private MaterialPropertyBlock properties;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>预制体网格半径为 1，根节点缩放到世界半径；父节点应保持单位缩放。</summary>
        public void Show(Vector3 center, float radius, Color color, float opacity = 1)
        {
            gameObject.SetActive(true);
            transform.position = center;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, radius);
            if (properties == null) properties = new MaterialPropertyBlock();
            foreach (var outline in outlines) Tint(outline, color, opacity);
            Tint(fill, color, opacity * fillOpacity);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Tint(Renderer target, Color color, float alpha)
        {
            if (target == null) return;
            target.GetPropertyBlock(properties);
            color.a *= alpha;
            properties.SetColor(BaseColor, color);
            target.SetPropertyBlock(properties);
        }
    }
}
