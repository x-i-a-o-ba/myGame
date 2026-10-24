using System.Collections.Generic;
using System.Linq;
using Roguelike.Combat;
using Roguelike.Flow;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>远征界面在编辑器保存为 Prefab，运行时仅通过 UIManager 发布状态。</summary>
    public static partial class ExpeditionSceneAuthoring
    {
        static readonly Color Back=new Color(.025f,.045f,.065f,.97f),Panel=new Color(.06f,.095f,.12f,.98f),Gold=new Color(.95f,.8f,.48f),Ink=new Color(.89f,.93f,.96f);
        static Font font;
        sealed class Layout
        {
            public GameObject Root;public UIPanel Panel;
            public readonly List<UIPanel.TextBinding> Texts=new List<UIPanel.TextBinding>();
            public readonly List<UIPanel.ImageBinding> Images=new List<UIPanel.ImageBinding>();
            public readonly List<UIPanel.FillBinding> Fills=new List<UIPanel.FillBinding>();
            public readonly List<UIPanel.ToggleBinding> Toggles=new List<UIPanel.ToggleBinding>();
            public Text Text(Transform parent,string key,string value,float x,float y,float w,float h,int size=20,Color? color=null)
            {var label=Label(parent,key,value,x,y,w,h,size,color??Ink);if(!string.IsNullOrEmpty(key)) Texts.Add(new UIPanel.TextBinding{key=key,target=label});return label;}
            public void Picture(Transform parent,string key,float x,float y,float w,float h)
            {var rect=Rect(parent,key,x,y,w,h);var image=rect.gameObject.AddComponent<RawImage>();image.raycastTarget=false;Images.Add(new UIPanel.ImageBinding{key=key,target=image});}
            public void Toggle(string key,GameObject target) {Toggles.Add(new UIPanel.ToggleBinding{key=key,target=target});target.SetActive(false);}
            public void Save(string path)
            {
                Panel.texts=Texts.ToArray();Panel.images=Images.ToArray();Panel.fills=Fills.ToArray();Panel.toggles=Toggles.ToArray();
                PrefabUtility.SaveAsPrefabAsset(Root,path);Object.DestroyImmediate(Root);
            }
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
        }
        static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {var rect=Rect(parent,name,x,y,w,h);rect.gameObject.AddComponent<Image>().color=color;return rect;}
        static Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color)
        {
            var rect=Rect(parent,name,x,y,w,h);var text=rect.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.color=color;
            text.text=value;text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Truncate;text.alignment=TextAnchor.MiddleLeft;text.supportRichText=true;text.horizontalOverflow=HorizontalWrapMode.Wrap;return text;
        }
        static RectTransform Button(Layout layout,Transform parent,string key,string label,string command,int value,float x,float y,float w,float h,int size=20)
        {
            var rect=Skin(parent,string.IsNullOrEmpty(label)?command:label,x,y,w,h,"button");rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors;colors.highlightedColor=new Color(.7f,.85f,1);colors.pressedColor=new Color(.7f,.65f,.4f);button.colors=colors;
            var action=rect.gameObject.AddComponent<UIAction>();action.command=command;action.value=value;
            float padding=Mathf.Min(12,w*.14f);
            var text=layout.Text(rect,key,label,padding,2,w-2*padding,h-4,size);text.alignment=TextAnchor.MiddleCenter;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Min(13,size);text.resizeTextMaxSize=size;return rect;
        }
        static Layout New(string id,float width,float height,bool blocks=true)
        {
            var root=new GameObject(id,typeof(RectTransform),typeof(UIPanel));var rect=(RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,16);
            var panel=root.GetComponent<UIPanel>();panel.panelId=id;panel.blocksPointer=blocks;return new Layout{Root=root,Panel=panel};
        }
        static void Hover(RectTransform rect,int value)
        {var graphic=rect.GetComponent<Graphic>();if(graphic) graphic.raycastTarget=true;var hover=rect.gameObject.AddComponent<UIHoverAction>();hover.command=CombatHud.HoverCommand;hover.value=value;}
        public static void BuildInterface()
        {Apply();ExpeditionBuild.Build();}
        [MenuItem("Tools/Roguelike/远征/生成界面并装配战场")]
        public static void Apply()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/战斗反馈.mat");
            if(!material) {material=new Material(Shader.Find("Roguelike/Combat/Feedback"));AssetDatabase.CreateAsset(material,"Assets/Game/Materials/战斗反馈.mat");}
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansSC.ttf");BuildBottom();BuildOverlay();
            var bottom=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/Panels/UI.Game.BottomHUD.prefab");
            var overlay=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/Panels/UI.Expedition.prefab");
            foreach(string path in new[]{GameTools.ScenePath,GameTools.SecondBattlefieldPath})
            {
                var scene=EditorSceneManager.OpenScene(path);var game=Object.FindFirstObjectByType<GameCombatController>();var hud=Object.FindFirstObjectByType<CombatHud>();
                game.expedition=AssetDatabase.LoadAssetAtPath<ExpeditionCatalog>("Assets/GameData/目录/远征配置.asset");
                game.feedbackMaterial=material;
                hud.GetComponent<CanvasScaler>().screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                game.inputProfile=AssetDatabase.LoadAssetAtPath<CombatInputProfile>("Assets/GameData/操作/战斗键位.asset");
                foreach(var panel in hud.GetComponentsInChildren<UIPanel>(true).Where(p=>p.panelId!="UI.Game.AbilityTooltip").ToArray())
                    if(panel && panel.transform!=hud.transform) Object.DestroyImmediate(panel.gameObject);
                PrefabUtility.InstantiatePrefab(bottom,hud.transform);PrefabUtility.InstantiatePrefab(overlay,hud.transform);
                foreach(var old in Object.FindObjectsByType<GameCombatProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(old);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();Debug.Log("EXPEDITION_SCENES_AUTHORED scenes=2");
        }
    }
}
