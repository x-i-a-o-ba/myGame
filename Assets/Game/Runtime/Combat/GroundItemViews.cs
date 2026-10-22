using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.SkillIndicators;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>
    /// 地上装备的场景展示（Dota 2 式）：每件地上的装备显示一个品质色地面圈和一块朝向相机、轻微浮动的图标牌；
    /// 悬停或查看时地面圈与图标牌变亮放大。只读会话实体的 <see cref="GroundItem"/> 缓冲，不写入任何规则数据。
    /// 由控制器创建并每帧同步；地上的装备消失时销毁对应对象，<see cref="Dispose"/> 释放全部对象、网格与材质。
    /// </summary>
    public sealed class GroundItemViews : IDisposable
    {
        /// <summary>图标牌边长（米）随相机缩放：取 MinimumSize 与“正交尺寸 × ZoomFactor”的较大者，屏幕上的大小基本稳定。</summary>
        public const float MinimumSize=1.2f,ZoomFactor=0.06f;
        /// <summary>图标牌底边离地高度（米，按图标牌边长缩放）与地面圈高度。</summary>
        private const float Lift=0.45f,CircleHeight=0.045f,Bob=0.06f,HighlightScale=1.15f;
        private sealed class View
        {
            public int Id,Item; public Vector3 Position; public bool Seen;
            public CircleIndicatorView Circle; public GameObject Card; public Renderer Icon,Frame;
        }
        private readonly Dictionary<int,View> views=new Dictionary<int,View>();
        private readonly List<int> stale=new List<int>();
        private readonly Dictionary<int,Material> iconMaterials=new Dictionary<int,Material>();
        private readonly GameAbilityIndicator indicator;
        private readonly Material template;
        private readonly Transform parent;
        private Material frameMaterial;
        private Mesh quad;
        private Quaternion facing=Quaternion.identity;
        private float time;
        /// <summary>当前图标牌边长（米）。</summary>
        public float Size {get; private set;}=MinimumSize;
        /// <summary>当前显示的地上装备数量。</summary>
        public int Count=>views.Count;
        public GroundItemViews(GameAbilityIndicator indicator,Material template,Transform parent)
        { this.indicator=indicator; this.template=template; this.parent=parent; }

        /// <summary>按地上的装备增删并刷新展示；highlighted / inspected 为悬停与查看中的地上装备编号（0 表示无）。</summary>
        public void Sync(DynamicBuffer<GroundItem> ground,EquipmentDefinition[] items,Camera camera,int highlighted,int inspected)
        {
            time=Time.time;
            if(camera)
            {
                facing=camera.transform.rotation;
                Size=Mathf.Max(MinimumSize,(camera.orthographic?camera.orthographicSize:20)*ZoomFactor);
            }
            foreach(var view in views.Values) view.Seen=false;
            for(int i=0;i<ground.Length;i++)
            {
                var record=ground[i];
                if(!views.TryGetValue(record.Id,out var view) || view.Item!=record.Item)
                {
                    if(view!=null) Destroy(view);
                    view=Create(record,items);
                    views[record.Id]=view;
                }
                view.Seen=true; view.Position=new Vector3(record.Position.x,0,record.Position.z);
                bool lit=record.Id==highlighted || record.Id==inspected;
                Color color=QualityColor(items,record.Item);
                if(view.Circle) view.Circle.Show(new Vector3(view.Position.x,CircleHeight,view.Position.z),Size*(lit?0.62f:0.5f),color,lit?1f:0.7f);
                if(view.Card)
                {
                    float size=Size*(lit?HighlightScale:1);
                    view.Card.transform.SetPositionAndRotation(CardCenter(view),facing);
                    view.Card.transform.localScale=new Vector3(size,size,size);
                }
            }
            stale.Clear();
            foreach(var pair in views) if(!pair.Value.Seen) stale.Add(pair.Key);
            foreach(int id in stale) { Destroy(views[id]); views.Remove(id); }
        }

        /// <summary>地上装备图标牌中心的世界坐标（与 <see cref="Sync"/> 的浮动一致），供点选与提示框定位；不存在时返回 false。</summary>
        public bool TryGetCard(int id,out Vector3 center)
        {
            center=default;
            if(!views.TryGetValue(id,out var view)) return false;
            center=CardCenter(view); return true;
        }

        /// <summary>移除全部展示对象（重开时调用）；材质与网格保留复用。</summary>
        public void Clear()
        {
            foreach(var view in views.Values) Destroy(view);
            views.Clear();
        }

        public void Dispose()
        {
            Clear();
            foreach(var material in iconMaterials.Values) Release(material);
            iconMaterials.Clear();
            Release(frameMaterial); frameMaterial=null;
            Release(quad); quad=null;
        }

        private Vector3 CardCenter(View view)
            =>view.Position+Vector3.up*(Size*(Lift+0.5f)+Mathf.Sin(time*2.2f+view.Id*1.7f)*Bob*Size);

        private View Create(in GroundItem record,EquipmentDefinition[] items)
        {
            var view=new View{Id=record.Id,Item=record.Item};
            var definition=record.Item>=0 && items!=null && record.Item<items.Length?items[record.Item]:null;
            if(indicator) view.Circle=indicator.CreateCircle("Ground Item Circle "+record.Id);
            if(template)
            {
                view.Card=new GameObject("Ground Item "+record.Id+" "+(definition?definition.Label:"装备"));
                if(parent) view.Card.transform.SetParent(parent,false);
                view.Frame=Quad(view.Card.transform,"Frame",FrameMaterial(),1.12f,0.02f);
                view.Icon=Quad(view.Card.transform,"Icon",IconMaterial(record.Item,definition,QualityColor(items,record.Item)),1,0);
            }
            return view;
        }

        private Renderer Quad(Transform card,string objectName,Material material,float scale,float depth)
        {
            var child=new GameObject(objectName);
            child.transform.SetParent(card,false);
            child.transform.localPosition=new Vector3(0,0,depth);
            child.transform.localScale=new Vector3(scale,scale,1);
            child.AddComponent<MeshFilter>().sharedMesh=QuadMesh();
            var renderer=child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return renderer;
        }

        /// <summary>按装备缓存的图标材质：复制场景标记材质（URP Unlit），贴图换成装备图标、双面显示；没有图标时用品质色。</summary>
        private Material IconMaterial(int item,EquipmentDefinition definition,Color quality)
        {
            if(iconMaterials.TryGetValue(item,out var material) && material) return material;
            material=new Material(template){name="Ground Item Icon "+item};
            var icon=definition?definition.icon:null;
            Tint(material,icon?Color.white:quality,icon);
            iconMaterials[item]=material;
            return material;
        }

        private Material FrameMaterial()
        {
            if(frameMaterial) return frameMaterial;
            frameMaterial=new Material(template){name="Ground Item Frame"};
            Tint(frameMaterial,new Color(0.04f,0.035f,0.03f,1),null);
            return frameMaterial;
        }

        private static void Tint(Material material,Color color,Texture texture)
        {
            if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_MainTex")) material.SetTexture("_MainTex",texture);
            if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color")) material.SetColor("_Color",color);
            if(material.HasProperty("_Cull")) material.SetFloat("_Cull",(float)CullMode.Off);
        }

        /// <summary>单位四边形（XY 平面、中心在原点、正面朝 -Z），朝向与相机旋转一致时正对相机。</summary>
        private Mesh QuadMesh()
        {
            if(quad) return quad;
            quad=new Mesh{name="Ground Item Quad"};
            quad.SetVertices(new[]{new Vector3(-0.5f,-0.5f,0),new Vector3(-0.5f,0.5f,0),new Vector3(0.5f,0.5f,0),new Vector3(0.5f,-0.5f,0)});
            quad.SetUVs(0,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            quad.SetNormals(new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back});
            quad.SetTriangles(new[]{0,1,2,0,2,3},0);
            quad.RecalculateBounds();
            return quad;
        }

        /// <summary>装备品质颜色（与提示框、图标边框一致）。</summary>
        public static Color QualityColor(EquipmentDefinition[] items,int item)
        {
            var quality=item>=0 && items!=null && item<items.Length && items[item]?items[item].quality:EquipmentQuality.Common;
            return ColorUtility.TryParseHtmlString(AbilityTooltipBuilder.QualityColor(quality),out var color)?color:Color.white;
        }

        private static void Destroy(View view)
        {
            if(view.Circle) Release(view.Circle.gameObject);
            if(view.Card) Release(view.Card);
        }

        private static void Release(UnityEngine.Object target)
        {
            if(!target) return;
            if(Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
