using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Flow
{
    /// <summary>
    /// 路线图面板的连线绘制。节点的显隐、图标与可达高亮由界面绑定处理；本组件只按路线图快照在节点之间画线，
    /// 不决定可达性，也不发出任何命令。节点引用由界面生成工具写入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteMapView : MonoBehaviour
    {
        [Tooltip("普通节点，下标 = 层 × 4 + 列。")] public RectTransform[] nodes=new RectTransform[(RouteMap.MaxFloors-1)*RouteMap.Lanes];
        [Tooltip("首领节点。")] public RectTransform boss;
        [Tooltip("连线所在层，应位于节点下方。")] public RectTransform lineLayer;
        [Min(1)] public float lineWidth=4;
        public Color traveled=new Color(.95f,.8f,.48f,1),available=new Color(.92f,.95f,1f,.85f),dormant=new Color(.55f,.6f,.66f,.35f);
        private readonly List<Image> lines=new List<Image>(48);

        public void Draw(RouteMap route)
        {
            if(!lineLayer || route==null) return;
            int used=0;
            foreach(var node in route.Nodes)
                foreach(int next in node.Next)
                {
                    var target=route.Nodes[next];
                    var from=Find(node);var to=Find(target);
                    if(!from || !to) continue;
                    bool wasTraveled=node.Visited && target.Visited;
                    bool isAvailable=node.Id==route.Current && route.Cleared;
                    Place(Line(used++),Center(from),Center(to),wasTraveled?traveled:isAvailable?available:dormant);
                }
            for(int i=used;i<lines.Count;i++) lines[i].gameObject.SetActive(false);
        }
        private RectTransform Find(RouteNode node)
        {
            if(node.Kind==RouteNodeKind.Boss) return boss;
            int slot=node.Floor*RouteMap.Lanes+node.Lane;
            return nodes!=null && slot>=0 && slot<nodes.Length?nodes[slot]:null;
        }
        private Vector3 Center(RectTransform rect)=>lineLayer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        private Image Line(int index)
        {
            while(lines.Count<=index)
            {
                var go=new GameObject("连线 "+lines.Count,typeof(RectTransform),typeof(Image));
                var rect=(RectTransform)go.transform;rect.SetParent(lineLayer,false);rect.pivot=new Vector2(.5f,.5f);
                var image=go.GetComponent<Image>();image.raycastTarget=false;lines.Add(image);
            }
            var line=lines[index];line.gameObject.SetActive(true);return line;
        }
        private void Place(Image line,Vector3 from,Vector3 to,Color color)
        {
            var rect=line.rectTransform;Vector3 delta=to-from;
            rect.localPosition=(from+to)*.5f;
            rect.sizeDelta=new Vector2(delta.magnitude,lineWidth);
            rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            line.color=color;
        }
    }
}
