using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Foundation.UI
{
    /// <summary>
    /// 悬停提示框的排版与定位：按顺序竖向堆叠启用的分段（带自动换行 Text 的分段按文字首选高度伸缩），
    /// 再把整个提示框放到当前悬停源（<see cref="UIManager.HoverSource"/>）上方并限制在画布内；上方放不下时（例如悬停屏幕上半部的商店卡片）
    /// 改放到悬停源左侧（左侧也放不下则右侧），避免盖住悬停源本身。只负责位置，不读业务数据。
    /// 根节点与分段的锚点要求：根节点锚点居中、轴心在底边中点；分段锚点为顶边横向拉伸。
    /// 带背景或图标的分段可把自动换行 Text 放在纵向拉伸的子节点上，分段高度随文字伸缩并保留上下内边距。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITooltip : MonoBehaviour
    {
        [Tooltip("按显示顺序排列的分段；未启用的分段不占高度。")] public RectTransform[] sections = System.Array.Empty<RectTransform>();
        [Min(0), Tooltip("四周内边距（画布单位）。")] public float padding = 14;
        [Min(0), Tooltip("分段之间的间距（画布单位）。")] public float spacing = 8;
        [Min(0), Tooltip("与悬停源上边缘之间的距离（画布单位）。")] public float gap = 12;
        [Min(0), Tooltip("距离画布边缘的最小留白（画布单位）。")] public float margin = 8;
        private UIManager manager;
        private readonly Vector3[] corners = new Vector3[4];
        /// <summary>最近一次排版得到的总高度（画布单位），供检查使用。</summary>
        public float Height { get; private set; }
        // 提示框总是画在其它动态面板（例如稍后才加载的商店）之上。
        private void OnEnable() { manager = GetComponentInParent<UIManager>(true); transform.SetAsLastSibling(); Refresh(); }
        private void LateUpdate() => Refresh();

        /// <summary>立即重新排版并定位；数据刚写入时可手动调用，避免等到 LateUpdate。</summary>
        public void Refresh()
        {
            Layout();
            Place(manager ? manager.HoverSource : null);
        }

        private void Layout()
        {
            var root = (RectTransform)transform;
            float width = root.rect.width - padding * 2;
            float y = padding; bool first = true;
            if (sections != null)
                foreach (var section in sections)
                {
                    if (!section || !section.gameObject.activeSelf) continue;
                    if (!first) y += spacing;
                    first = false;
                    section.anchorMin = new Vector2(0, 1); section.anchorMax = new Vector2(1, 1); section.pivot = new Vector2(0.5f, 1);
                    float height = section.rect.height;
                    var label = WrapText(section, out float inset);
                    if (label)
                    {
                        // 先确定宽度，首选高度才会按实际换行计算。
                        section.sizeDelta = new Vector2(-padding * 2, height);
                        height = Mathf.Ceil(label.preferredHeight) + inset;
                    }
                    section.sizeDelta = new Vector2(-padding * 2, height);
                    section.anchoredPosition = new Vector2(0, -y);
                    y += height;
                }
            Height = Mathf.Max(padding * 2, y + padding);
            if (width > 0) root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
        }

        /// <summary>
        /// 分段的自动换行文字：分段自身的 Text，或纵向拉伸填充分段的第一个启用子 Text（带背景或图标的分段）。
        /// inset 为子文字上下内边距之和，分段高度 = 文字首选高度 + inset。
        /// </summary>
        private static Text WrapText(RectTransform section, out float inset)
        {
            inset = 0;
            var own = section.GetComponent<Text>();
            if (own) return own.horizontalOverflow == HorizontalWrapMode.Wrap ? own : null;
            for (int i = 0; i < section.childCount; i++)
            {
                var child = section.GetChild(i) as RectTransform;
                if (!child || !child.gameObject.activeSelf || child.anchorMin.y != 0 || child.anchorMax.y != 1) continue;
                var text = child.GetComponent<Text>();
                if (!text || text.horizontalOverflow != HorizontalWrapMode.Wrap) continue;
                inset = Mathf.Max(0, -child.sizeDelta.y);
                return text;
            }
            return null;
        }

        private void Place(RectTransform source)
        {
            var root = (RectTransform)transform;
            var parent = root.parent as RectTransform;
            if (!source || !parent) return;
            source.GetWorldCorners(corners);
            // 角点顺序：左下、左上、右上、右下。取上边缘中点作为锚定位置。
            Vector2 top = parent.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            Rect area = parent.rect;
            Vector2 size = root.rect.size;
            float minX = area.xMin + margin + size.x * 0.5f, maxX = Mathf.Max(minX, area.xMax - margin - size.x * 0.5f);
            float x = Mathf.Clamp(top.x, minX, maxX);
            float y = top.y + gap;
            if (y + size.y > area.yMax - margin)
            {
                // 上方放不下：放到悬停源侧面并与其垂直居中。左侧放不下时改放右侧。
                Vector2 left = parent.InverseTransformPoint((corners[0] + corners[1]) * 0.5f);
                Vector2 right = parent.InverseTransformPoint((corners[2] + corners[3]) * 0.5f);
                float side = Mathf.Max(margin, 8);
                x = left.x - side - size.x * 0.5f;
                if (x < minX) x = right.x + side + size.x * 0.5f;
                x = Mathf.Clamp(x, minX, maxX);
                y = left.y - size.y * 0.5f;
            }
            y = Mathf.Clamp(y, area.yMin + margin, Mathf.Max(area.yMin + margin, area.yMax - margin - size.y));
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0);
            root.anchoredPosition = new Vector2(x, y) - area.center;
        }
    }
}
