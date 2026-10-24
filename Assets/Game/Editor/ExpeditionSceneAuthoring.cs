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
    public static class ExpeditionSceneAuthoring
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
            public void Text(Transform parent,string key,string value,float x,float y,float w,float h,int size=20,Color? color=null)
            {var label=Label(parent,key,value,x,y,w,h,size,color??Ink);if(!string.IsNullOrEmpty(key)) Texts.Add(new UIPanel.TextBinding{key=key,target=label});}
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
            text.text=value;text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Overflow;text.alignment=TextAnchor.MiddleLeft;text.supportRichText=true;text.horizontalOverflow=HorizontalWrapMode.Wrap;return text;
        }
        static RectTransform Button(Layout layout,Transform parent,string key,string label,string command,int value,float x,float y,float w,float h,int size=20)
        {
            var rect=Box(parent,label,x,y,w,h,new Color(.13f,.24f,.29f));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors;colors.highlightedColor=new Color(.7f,.85f,1);colors.pressedColor=new Color(.7f,.65f,.4f);button.colors=colors;
            var action=rect.gameObject.AddComponent<UIAction>();action.command=command;action.value=value;
            layout.Text(rect,key,label,12,4,w-24,h-8,size);return rect;
        }
        static Layout New(string id,float width,float height,bool blocks=true)
        {
            var root=new GameObject(id,typeof(RectTransform),typeof(UIPanel));var rect=(RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,16);
            var panel=root.GetComponent<UIPanel>();panel.panelId=id;panel.blocksPointer=blocks;return new Layout{Root=root,Panel=panel};
        }
        static void Hover(RectTransform rect,int value)
        {var hover=rect.gameObject.AddComponent<UIHoverAction>();hover.command=CombatHud.HoverCommand;hover.value=value;}
        static void Bar(Layout l,Transform parent,string key,string text,float x,float y,float w,Color color)
        {
            var background=Box(parent,key,x,y,w,22,new Color(.01f,.02f,.03f));
            var fill=Box(background,"填充",0,0,w,22,color);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            l.Fills.Add(new UIPanel.FillBinding{key=key,target=fill.GetComponent<Image>(),axis=FillAxis.Horizontal});
            l.Text(background,text,"",8,0,w-16,22,15);
        }
        static void BuildBottom()
        {
            var l=New("UI.Game.BottomHUD",1780,180);var root=l.Root.transform;
            Box(root,"底板",0,0,1780,180,Back);Box(root,"金线",0,0,1780,2,Gold);
            l.Picture(root,"hero.portrait",16,20,92,108);l.Text(root,"hero.name","英雄",122,12,250,32,24,Gold);
            l.Text(root,"hero.level","1",34,132,60,30,23);l.Text(root,"hero.xpText","",122,44,260,24,15);
            Bar(l,root,"hero.health","hero.healthText",122,78,265,new Color(.19f,.65f,.39f));
            Bar(l,root,"hero.mana","hero.manaText",122,106,265,new Color(.18f,.42f,.8f));
            l.Text(root,"hero.points","",122,137,270,28,16,Gold);
            string[] statLabels={"攻击","攻速","移速","减伤"},statKeys={"stats.attack","stats.attackSpeed","stats.move","stats.reduction"};
            for(int i=0;i<4;i++) {l.Text(root,"",statLabels[i],410,18+i*35,56,28,16);l.Text(root,statKeys[i],"",470,18+i*35,95,28,18,Gold);}
            var passive=Box(root,"普攻",580,41,80,80,Panel);Hover(passive,4);l.Picture(passive,"passive.icon",5,5,70,70);l.Text(root,"passive.name","",576,124,92,40,14);
            string[] letters={"q","w","e","r"};
            for(int i=0;i<4;i++)
            {
                float x=682+i*124;string p="ability."+letters[i]+".";
                var skill=Button(l,root,null,"","combat.ability",i,x,42,100,80);Hover(skill,i);l.Picture(skill,p+"icon",4,4,92,72);
                var cooling=Box(skill,"冷却遮罩",0,0,100,80,new Color(0,0,0,.65f));cooling.GetComponent<Image>().raycastTarget=false;l.Toggle(p+"cooling",cooling.gameObject);
                l.Text(skill,p+"cdText","",16,20,75,36,27,Gold);l.Text(root,p+"key","",x,13,42,26,20,Gold);
                l.Text(root,p+"status","",x+38,13,68,26,13);l.Text(root,p+"name","",x-7,125,116,43,15);
                var learn=Button(l,root,null,"+","combat.learnSkill",i,x+69,5,31,31,23);l.Toggle(p+"learn",learn.gameObject);
                for(int level=0;level<4;level++)
                {
                    var pip=Box(root,"等级点",x+level*25,116,20,5,new Color(.2f,.25f,.3f));
                    var lit=Box(pip,"已学",0,0,20,5,Gold);l.Toggle(p+"pip"+level,lit.gameObject);
                }
            }
            l.Text(root,"","金币",1200,16,55,30,18,Gold);l.Text(root,"hero.gold","300",1258,16,135,30,23,Gold);
            for(int i=0;i<6;i++)
            {
                var item=Box(root,"装备 "+i,1200+i*90,56,80,80,Panel);Hover(item,CombatHud.EquipmentHoverBase+i);
                l.Picture(item,"equip.s"+i+".icon",4,4,72,48);l.Text(item,"equip.s"+i+".name","",3,52,74,27,12);
            }
            for(int i=0;i<2;i++) {l.Picture(root,"expedition.relic.icon."+i,1415+i*175,8,32,32);l.Text(root,"expedition.relic."+i,"",1451+i*175,8,135,34,16,Gold);}
            l.Text(root,"progression.hint","",1200,142,560,28,15);
            l.Save("Assets/Resources/UI/Panels/UI.Game.BottomHUD.prefab");
        }
        static RectTransform Overlay(Layout l,string name,string key)
        {
            var rect=Box(l.Root.transform,name,0,0,1920,1080,new Color(.012f,.02f,.035f,1));
            var panel=rect.gameObject.AddComponent<UIPanel>();panel.panelId="UI.Expedition."+name;panel.blocksPointer=true;
            l.Toggle(key,rect.gameObject);return rect;
        }
        static void BuildOverlay()
        {
            var l=New("UI.Expedition",1920,1080,false);var rect=(RectTransform)l.Root.transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var top=Box(rect,"顶栏",410,18,1100,65,Back);
            l.Text(top,"top.wave","第 1 关",20,3,230,30,22,Gold);l.Text(top,"top.map","战场",255,3,310,30,19);
            l.Text(top,"top.clock","00:00",620,3,130,30,22,Gold);l.Text(top,"","击杀",785,3,65,30,18);l.Text(top,"top.kills","0",850,3,210,30,22);
            l.Text(top,"top.next","",20,34,1050,26,16);
            var boss=Box(rect,"首领血条",610,92,700,54,Back);l.Toggle("top.boss",boss.gameObject);l.Text(boss,"top.bossName","",12,0,676,28,18,Gold);
            Bar(l,boss,"top.bossFill","",12,29,676,new Color(.7f,.16f,.2f));
            l.Text(rect,"","WASD 移动 · Q/E/R/F 施法\nCtrl+技能键 升级 · Tab 属性 · Esc 暂停",22,20,360,70,17);
            var stats=Box(rect,"属性",25,115,330,560,Back);l.Toggle("expedition.stats",stats.gameObject);
            l.Text(stats,"","英雄属性",22,15,280,40,25,Gold);l.Text(stats,"expedition.stats.body","",22,65,282,480,23);
            var forms=Box(rect,"技能形态",1070,155,820,210,Back);l.Toggle("expedition.form",forms.gameObject);
            l.Text(forms,"expedition.form.title","",20,10,780,38,24,Gold);
            for(int i=0;i<2;i++) Button(l,forms,"expedition.form."+i,"","expedition.form",i,20+i*395,59,380,133,18);
            var notice=Box(rect,"过关提示",710,350,500,105,Back);l.Toggle("expedition.victoryNotice",notice.gameObject);l.Text(notice,"","首领已击败 · 正在前往营地",25,15,450,70,27,Gold);
            var pause=Overlay(l,"暂停","expedition.pause");
            l.Text(pause,"","远征暂停",740,270,440,75,40,Gold);
            Button(l,pause,null,"继续战斗","expedition.resume",0,740,385,440,68,26);
            Button(l,pause,null,"返回选择界面","expedition.return",0,740,478,440,68,26);
            var confirm=Label(pause,"确认","再次点击返回将结束本局",740,565,440,50,22,Gold);l.Toggle("expedition.return.confirm",confirm.gameObject);
            var result=Overlay(l,"结算","expedition.result");
            l.Text(result,"expedition.result.title","",640,230,640,90,44,Gold);l.Text(result,"expedition.result.body","",640,350,640,280,29);
            Button(l,result,null,"返回选择界面","expedition.return",0,640,690,640,70,26);
            var camp=Overlay(l,"营地","expedition.camp");
            l.Text(camp,"camp.title","",140,65,1200,75,40,Gold);l.Text(camp,"camp.gold","",1450,75,320,60,29,Gold);
            l.Text(camp,"camp.hint","",140,145,1640,45,23);
            var choices=Rect(camp,"遗物选择",140,230,1640,600);l.Toggle("camp.choices",choices.gameObject);
            for(int i=0;i<4;i++)
            {var card=Button(l,choices,"camp.relic."+i,"","camp.relic",i,i*412,0,390,420,27);l.Toggle("camp.relic.visible."+i,card.gameObject);}
            var store=Rect(camp,"整备",140,220,1640,790);l.Toggle("camp.store",store.gameObject);
            for(int i=0;i<5;i++)
            {
                var card=Button(l,store,"camp.stock."+i,"","camp.buy",i,i*330,0,310,330,20);
                // 卡片内容使用文字描述，避免图标遮挡长说明。
            }
            Button(l,store,"camp.refresh","刷新货架","camp.refresh",0,0,352,330,55,23);
            l.Text(store,"","点击已有装备出售 · 返还原价 50%",375,355,1250,52,20);
            for(int i=0;i<6;i++) Button(l,store,"camp.sell."+i,"","camp.sell",i,i*275,430,255,92,19);
            var routes=Rect(store,"路线",0,555,1260,150);l.Toggle("camp.routes",routes.gameObject);
            Button(l,routes,null,"稳健路线\n常规强度与奖励","camp.route",0,0,0,520,90,22);
            Button(l,routes,null,"凶险路线\n双倍精英 · 生命 +25% · 金币 ×1.6","camp.route",1,550,0,600,90,21);
            l.Text(routes,"camp.route.status","",0,95,1150,45,20,Gold);
            Button(l,store,null,"整备完成 · 出发","camp.depart",0,1260,650,370,75,27);
            l.Save("Assets/Resources/UI/Panels/UI.Expedition.prefab");
        }
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
