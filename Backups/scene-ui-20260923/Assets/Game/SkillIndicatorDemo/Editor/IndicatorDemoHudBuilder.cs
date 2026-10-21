using Roguelike.SkillIndicators;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.SkillIndicators.Editor
{
    /// <summary>首次生成可编辑 UI；不会在运行时构建或重置布局。</summary>
    internal static class IndicatorDemoHudBuilder
    {
        private static readonly Color Ink = new Color(0.8f, 0.88f, 0.9f);
        internal static IndicatorDemoHud Create(Transform parent)
        {
            var root = new GameObject("Indicator HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<IndicatorDemoHud>();
            var header = Panel(root.transform, "Title and demo notice", new Vector2(22,-22), new Vector2(640,116), Vector2.up);
            Label(header, "Title", "技能指示器 / CIRCLE TARGETING", new Vector2(22,-14), new Vector2(600,40), 28, Ink);
            Label(header, "Demo Notice", "演示素材 · 非最终效果\nKenney / CC0     |     平面范围测试 · 不接伤害", new Vector2(24,-60), new Vector2(595,48), 16, new Color(0.5f,0.65f,0.72f));
            var controls = Panel(root.transform, "Controls", new Vector2(-22,-22), new Vector2(370,116), Vector2.one);
            Label(controls,"Input help","Q / W / E  选择圆形技能\n左键确认 · 右键 / Esc 取消\n鼠标移到地面调整落点",new Vector2(20,-14),new Vector2(340,90),18,Ink);
            var bottom = Panel(root.transform, "Skill selection and status", new Vector2(0,24), new Vector2(1120,148), new Vector2(0.5f,0));
            var buttons = new Button[3];
            string[] names = {"Q   精准落点", "W   扩大范围", "E   远距投放"};
            for (int i=0;i<3;i++)
            {
                var panel = Panel(bottom,"Select " + (i+1),new Vector2(20+i*200,-18),new Vector2(184,64),Vector2.up);
                panel.GetComponent<Image>().color = new Color(0.08f,0.2f,0.25f,1);
                buttons[i] = panel.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = panel.GetComponent<Image>();
                Label(panel,"Caption",names[i],Vector2.zero,new Vector2(184,64),20,Ink).alignment = TextAnchor.MiddleCenter;
            }
            var status = Label(bottom,"Status","按 Q / W / E 开始瞄准",new Vector2(644,-18),new Vector2(450,42),22,new Color(0.2f,0.86f,1));
            var details = Label(bottom,"Parameters","施法距离与落点范围可在配置资产中调整",new Vector2(644,-64),new Vector2(450,62),16,Ink);
            Label(bottom,"Legend","大圈 = 施法距离    小圈 = 落点范围    确认后播放扩散反馈",new Vector2(22,-102),new Vector2(604,32),16,Ink);
            var serialized = new SerializedObject(hud);
            Bind(serialized,"qButton",buttons[0]); Bind(serialized,"wButton",buttons[1]); Bind(serialized,"eButton",buttons[2]);
            Bind(serialized,"status",status); Bind(serialized,"details",details);
            var blocks=serialized.FindProperty("blockingPanels"); blocks.arraySize=3;
            blocks.GetArrayElementAtIndex(0).objectReferenceValue=header;
            blocks.GetArrayElementAtIndex(1).objectReferenceValue=controls;
            blocks.GetArrayElementAtIndex(2).objectReferenceValue=bottom;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }
        internal static void Bind(SerializedObject serialized,string field,Object value) => serialized.FindProperty(field).objectReferenceValue=value;
        private static RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            go.GetComponent<Image>().color=new Color(0.025f,0.045f,0.065f,0.96f);
            return rect;
        }
        private static Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.text=value; text.fontSize=fontSize; text.color=color;
            text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf");
            text.raycastTarget=false; text.alignment=TextAnchor.MiddleLeft;
            return text;
        }
    }
}
