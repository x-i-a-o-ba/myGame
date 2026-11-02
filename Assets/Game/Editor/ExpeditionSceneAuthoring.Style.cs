using Roguelike.Combat;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>沿用原 HUD 的九宫格金属美术。仅生成展示与意图绑定，不引入结算规则。</summary>
    public static partial class ExpeditionSceneAuthoring
    {
        static readonly Color Muted=new Color(.60f,.67f,.72f),Cyan=new Color(.62f,.86f,1),White=new Color(.92f,.94f,.96f);
        /// <summary>血条、蓝条、首领条的填充色；比纯绿/纯蓝略收饱和度，与金属金边协调。</summary>
        static readonly Color HealthColor=new Color(.34f,.74f,.27f),ManaColor=new Color(.26f,.5f,.92f),BossColor=new Color(.8f,.2f,.17f);
        static Sprite Art(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Sprites/"+name+".png");
        static RectTransform Skin(Transform parent,string name,float x,float y,float w,float h,string art="hud_body",bool sliced=true)
        {
            var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=Art(art);
            if(!image.sprite) throw new System.InvalidOperationException("缺少原版 HUD 美术："+art);
            image.type=sliced?Image.Type.Sliced:Image.Type.Simple;image.raycastTarget=false;return r;
        }
        static void Center(RectTransform rect,float y=0)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,y);}
        static void TopCenter(RectTransform rect,float y)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-y);}
        static void BottomCenter(RectTransform rect,float y)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,y);}
        static Text Bold(Layout l,Transform p,string key,string value,float x,float y,float w,float h,int size=20,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=l.Text(p,key,value,x,y,w,h,size,color??White);t.fontStyle=FontStyle.Bold;t.alignment=align;
            // Noto Sans SC 的行高高于字号；固定高度的标题须按实际行高适配，避免整行被 UGUI 裁掉。
            t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(10,size-5);t.resizeTextMaxSize=size;return Shade(t);
        }
        /// <summary>给压在图标、血条或亮色地面上的文字加一层投影，避免与背景混在一起。</summary>
        static Text Shade(Text text,float alpha=.85f)
        {var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,alpha);shadow.effectDistance=new Vector2(1.2f,-1.2f);return text;}
        static void Icon(Transform parent,string path,float x,float y,float w,float h)
        {var r=Rect(parent,"装饰图标",x,y,w,h);var image=r.gameObject.AddComponent<RawImage>();image.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);image.raycastTarget=false;}
        static RectTransform Slot(Transform parent,string name,float x,float y,float size)
        {return Skin(parent,name,x,y,size,size,"slot_back");}
        static void Frame(RectTransform parent)
        {Skin(parent,"金属边框",0,0,parent.sizeDelta.x,parent.sizeDelta.y,"slot_frame");}
        static RectTransform Card(Layout l,Transform parent,string command,int value,float x,float y,float w,float h)
        {
            var r=Skin(parent,command+"."+value,x,y,w,h,"hud_body");var image=r.GetComponent<Image>();image.raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var colors=b.colors;
            colors.highlightedColor=new Color(1,.92f,.7f);colors.pressedColor=new Color(.65f,.75f,.85f);colors.selectedColor=Color.white;b.colors=colors;
            var a=r.gameObject.AddComponent<UIAction>();a.command=command;a.value=value;return r;
        }
        static void Bar(Layout l,Transform parent,string key,string label,float x,float y,float w,float h,Color color,int textSize=15)
        {
            var back=Skin(parent,key,x,y,w,h,"bar_back");var fill=Skin(back,"填充",2,2,w-4,h-4,"bar_fill");
            fill.GetComponent<Image>().color=color;fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;
            fill.offsetMin=new Vector2(2,2);fill.offsetMax=new Vector2(-2,-2);
            l.Fills.Add(new UIPanel.FillBinding{key=key,target=fill.GetComponent<Image>(),axis=FillAxis.Horizontal});
            if(!string.IsNullOrEmpty(label)) Bold(l,back,label,"",4,0,w-8,h,textSize,White,TextAnchor.MiddleCenter);
        }
        static void BuildBottom()
        {
            var l=New("UI.Game.BottomHUD",1440,198);var root=l.Root.transform;
            Skin(root,"底部金属框",0,24,1440,174);
            var portrait=Skin(root,"英雄肖像框",16,36,140,146,"hud_plate");
            l.Picture(portrait,"hero.portrait",4,4,132,138);
            Skin(portrait,"姓名渐变",4,100,132,42,"shade_bottom",false);
            Bold(l,portrait,"hero.name","英雄",6,112,128,26,18,White,TextAnchor.MiddleCenter);
            Shade(l.Text(portrait,"hero.xpText","",43,5,88,18,11,Cyan)).alignment=TextAnchor.MiddleRight;
            var badge=Skin(root,"等级徽章",0,22,58,58,"level_badge",false);
            var ring=Skin(badge,"经验环",-3,-3,64,64,"ring",false).GetComponent<Image>();ring.color=Gold;
            ring.type=Image.Type.Filled;ring.fillMethod=Image.FillMethod.Radial360;ring.fillOrigin=0;
            l.Fills.Add(new UIPanel.FillBinding{key="hero.xp",target=ring,axis=FillAxis.Amount});
            Bold(l,badge,"hero.level","1",0,0,58,58,26,Gold,TextAnchor.MiddleCenter);
            var stats=Skin(root,"战斗属性",166,36,152,146,"hud_plate");
            string[] names={"攻击","攻速","移速","减伤"},keys={"stats.attack","stats.attackSpeed","stats.move","stats.reduction"};
            for(int i=0;i<4;i++)
            {l.Text(stats,null,names[i],12,8+i*27,44,25,14,Muted);Bold(l,stats,keys[i],"",55,8+i*27,83,25,17,White,TextAnchor.MiddleRight);}
            Bold(l,stats,"hero.points","",8,116,136,24,14,Gold,TextAnchor.MiddleCenter);
            var center=Skin(root,"技能与资源",330,0,594,198);
            var passive=Slot(center,"普攻被动",22,47,74);Hover(passive,4);
            l.Picture(passive,"passive.icon",6,6,62,62);Frame(passive);
            Bold(l,center,null,"普攻",22,22,74,22,14,Gold,TextAnchor.MiddleCenter);
            l.Text(center,"passive.name","",13,122,92,19,13,Muted).alignment=TextAnchor.MiddleCenter;
            string[] letters={"q","w","e","r"};
            for(int i=0;i<4;i++)
            {
                float x=110+i*112;string key="ability."+letters[i]+".";
                var skill=Card(l,center,"combat.ability",i,x,38,98,90);Hover(skill,i);
                l.Picture(skill,key+"icon",7,7,84,76);
                Skin(skill,"名称渐变",7,44,84,39,"shade_bottom",false);
                Shade(l.Text(skill,key+"name","",6,52,86,20,13,White)).alignment=TextAnchor.MiddleCenter;
                var locked=Box(skill,"未学习",7,7,84,76,new Color(.02f,.03f,.05f,.72f));locked.GetComponent<Image>().raycastTarget=false;l.Toggle(key+"locked",locked.gameObject);
                var cooling=Skin(skill,"径向冷却",7,7,84,76,"white",false);var image=cooling.GetComponent<Image>();image.color=new Color(0,0,0,.7f);
                image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Radial360;image.fillOrigin=2;image.fillClockwise=false;
                l.Fills.Add(new UIPanel.FillBinding{key=key+"cd",target=image,axis=FillAxis.Amount});l.Toggle(key+"cooling",cooling.gameObject);
                Frame(skill);Bold(l,skill,key+"cdText","",15,17,68,45,29,White,TextAnchor.MiddleCenter);
                var hotkey=Skin(skill,"按键",3,3,24,24,"hotkey");Bold(l,hotkey,key+"key","",0,0,24,24,15,White,TextAnchor.MiddleCenter);
                Shade(l.Text(skill,key+"status","",29,5,61,18,11,Cyan)).alignment=TextAnchor.MiddleRight;
                Shade(l.Text(skill,key+"mana","",44,70,46,13,11,Cyan)).alignment=TextAnchor.MiddleRight;
                var learn=Button(l,center,null,"+","combat.learnSkill",i,x+31,4,36,28,23);l.Toggle(key+"learn",learn.gameObject);
                for(int n=0;n<4;n++)
                {
                    var pip=Skin(center,"等级 "+n,x+5+n*23,131,19,7,"pip_off",false);l.Toggle(key+"pipSlot"+n,pip.gameObject);
                    var lit=Skin(pip,"已学习",0,0,19,7,"pip_on",false);l.Toggle(key+"pip"+n,lit.gameObject);
                }
            }
            Bar(l,center,"hero.health","hero.healthText",22,145,550,24,HealthColor);
            Shade(l.Text(center,"hero.regen","",442,146,120,22,12,new Color(.82f,1,.76f))).alignment=TextAnchor.MiddleRight;
            Bar(l,center,"hero.mana","hero.manaText",22,172,550,17,ManaColor,12);
            Shade(l.Text(center,"hero.manaRegen","",442,172,120,17,11,Cyan)).alignment=TextAnchor.MiddleRight;
            var equipment=Skin(root,"装备",934,36,200,146,"hud_plate");
            for(int i=0;i<6;i++)
            {
                var slot=Slot(equipment,"装备 "+i,8+i%3*62,5+i/3*59,57);Hover(slot,CombatHud.EquipmentHoverBase+i);
                l.Picture(slot,"equip.s"+i+".icon",5,5,47,47);Frame(slot);
            }
            l.Text(equipment,null,"装备",10,121,57,21,13,Muted);Icon(equipment,"Assets/Game/UI/Icons/金币.png",76,124,16,16);
            Bold(l,equipment,"hero.gold","",98,121,90,21,16,Gold,TextAnchor.MiddleRight);
            var relics=Skin(root,"远征行囊",1144,36,280,146,"hud_plate");
            l.Text(relics,null,"远征遗物",12,7,250,22,14,Gold);
            for(int i=0;i<2;i++)
            {
                var slot=Slot(relics,"遗物 "+i,12+i*134,34,48);l.Picture(slot,"expedition.relic.icon."+i,5,5,38,38);Frame(slot);
                l.Text(relics,"expedition.relic."+i,"",65+i*134,34,67,48,13,White);
            }
            Button(l,relics,null,"属性  Tab","expedition.stats",0,12,97,122,33,14);
            Button(l,relics,null,"暂停  Esc","expedition.pause",0,145,97,122,33,14);
            l.Save("Assets/Resources/UI/Panels/UI.Game.BottomHUD.prefab");
        }
        static RectTransform Overlay(Layout l,string name,string key)
        {
            var r=Box(l.Root.transform,name,0,0,1920,1080,new Color(.008f,.012f,.025f,.82f));
            r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            var panel=r.gameObject.AddComponent<UIPanel>();panel.panelId="UI.Expedition."+name;panel.blocksPointer=true;
            l.Toggle(key,r.gameObject);return r;
        }
        static void BuildOverlay()
        {
            var l=New("UI.Expedition",1920,1080,false);var root=(RectTransform)l.Root.transform;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var top=Skin(root,"战况铭牌",0,0,720,98,"topbar",false);TopCenter(top,0);
            l.Text(top,null,"击杀",105,17,100,20,13,Muted).alignment=TextAnchor.MiddleCenter;
            Bold(l,top,"top.kills","0",105,36,100,32,25,Gold,TextAnchor.MiddleCenter);
            Bold(l,top,"top.clock","00:00",270,4,180,37,30,White,TextAnchor.MiddleCenter);
            Bold(l,top,"top.wave","第 1 关",240,42,240,23,16,Gold,TextAnchor.MiddleCenter);
            l.Text(top,"top.next","",220,67,280,18,12,Muted).alignment=TextAnchor.MiddleCenter;
            l.Text(top,null,"存活敌人",515,17,100,20,13,Muted).alignment=TextAnchor.MiddleCenter;
            Bold(l,top,"top.alive","0",515,36,100,32,25,new Color(1,.48f,.4f),TextAnchor.MiddleCenter);
            var map=Skin(root,"战场信息",22,18,280,76,"hint_back");
            Bold(l,map,"top.map","",14,8,252,27,17,White);
            Shade(l.Text(map,null,"WASD 移动  ·  Q / E / R / F 施法",14,38,252,21,13,Muted),.6f);
            var performance=Skin(root,"状态提示",0,0,250,69,"hint_back");
            performance.anchorMin=performance.anchorMax=performance.pivot=Vector2.one;performance.anchoredPosition=new Vector2(-22,-18);
            Bold(l,performance,"top.fps","",13,8,120,25,17,new Color(.6f,.95f,.47f));
            l.Text(performance,"top.projectiles","",130,8,105,25,12,Muted).alignment=TextAnchor.MiddleRight;
            Shade(l.Text(performance,null,"Ctrl + 技能键升级  ·  滚轮缩放",13,37,225,21,12,Muted),.6f);
            var boss=Skin(root,"首领",0,0,600,62,"boss_frame");TopCenter(boss,104);l.Toggle("top.boss",boss.gameObject);
            Bold(l,boss,"top.bossName","",15,4,570,24,17,Gold,TextAnchor.MiddleCenter);
            Bar(l,boss,"top.bossFill",null,15,32,570,18,BossColor);
            var hint=Skin(root,"操作提示",0,0,620,46,"hint_back");TopCenter(hint,177);l.Toggle("progression.hintVisible",hint.gameObject);
            Bold(l,hint,"progression.hint","",14,5,592,36,18,Gold,TextAnchor.MiddleCenter);
            var stats=Skin(root,"属性面板",24,145,330,440,"tooltip_frame");l.Toggle("expedition.stats",stats.gameObject);
            Skin(stats,"标题底",7,7,316,66,"tooltip_header");l.Picture(stats,"expedition.hero.icon",16,17,44,44);
            Bold(l,stats,"expedition.hero.name","英雄属性",72,17,210,28,22,White);
            l.Text(stats,null,"当前有效属性",72,44,210,20,13,Muted);
            Button(l,stats,null,"×","expedition.stats",0,289,15,25,25,17);
            for(int i=0;i<11;i++)
            {
                if(i%2==0) Skin(stats,"属性底 "+i,12,87+i*27,306,27,"tooltip_block");
                l.Text(stats,"expedition.stat."+i+".name","",24,87+i*27,190,27,16,Muted);
                Bold(l,stats,"expedition.stat."+i+".value","",214,87+i*27,91,27,17,White,TextAnchor.MiddleRight);
            }
            l.Text(stats,null,"Tab 收起 · 装备与状态已计入",22,399,286,25,13,Muted);
            var forms=Skin(root,"形态选择",0,0,820,208,"tooltip_frame");BottomCenter(forms,224);l.Toggle("expedition.form",forms.gameObject);
            Bold(l,forms,"expedition.form.title","",20,9,780,35,21,Gold,TextAnchor.MiddleCenter);
            for(int i=0;i<2;i++) Button(l,forms,"expedition.form."+i,"","expedition.form",i,18+i*398,55,386,132,17);
            var notice=Skin(root,"胜利提示",0,0,540,110);Center(notice,100);l.Toggle("expedition.victoryNotice",notice.gameObject);
            Bold(l,notice,null,"首领已击败",20,12,500,44,30,Gold,TextAnchor.MiddleCenter);
            l.Text(notice,null,"正在准备下一段远征",20,62,500,26,17,Muted).alignment=TextAnchor.MiddleCenter;
            var pause=Overlay(l,"暂停","expedition.pause");var dialog=Skin(pause,"暂停面板",0,0,510,350);Center(dialog,35);
            Bold(l,dialog,null,"远征暂停",35,28,440,64,34,Gold,TextAnchor.MiddleCenter);
            l.Text(dialog,null,"稍作休整，再次启程",35,91,440,28,17,Muted).alignment=TextAnchor.MiddleCenter;
            Button(l,dialog,null,"继续战斗","expedition.resume",0,65,146,380,58,22);
            Button(l,dialog,null,"返回选择界面","expedition.return",0,65,220,380,49,19);
            var confirm=l.Text(dialog,null,"再次点击返回将结束本局",35,282,440,34,16,Gold);confirm.alignment=TextAnchor.MiddleCenter;l.Toggle("expedition.return.confirm",confirm.gameObject);
            var result=Overlay(l,"结算","expedition.result");var results=Skin(result,"远征结算",0,0,620,620);Center(results,0);
            Bold(l,results,"expedition.result.title","",40,44,540,75,42,Gold,TextAnchor.MiddleCenter);
            Skin(results,"分割线",40,135,540,2,"divider",false);
            l.Text(results,"expedition.result.body","",90,174,440,275,26,White).alignment=TextAnchor.MiddleCenter;
            Button(l,results,null,"返回选择界面","expedition.return",0,90,512,440,58,22);
            BuildCamp(l,Overlay(l,"营地","expedition.camp"));
            l.Save("Assets/Resources/UI/Panels/UI.Expedition.prefab");
        }
        /// <summary>已选路线外圈的金色高亮框，由 camp.route.selected.N 显示。</summary>
        static void RouteHighlight(Layout l,RectTransform button,int index)
        {
            var size=button.sizeDelta;var glow=Skin(button,"已选高亮",-4,-4,size.x+8,size.y+8,"highlight");
            glow.GetComponent<Image>().color=Gold;l.Toggle("camp.route.selected."+index,glow.gameObject);
        }
        static void BuildCamp(Layout l,Transform overlay)
        {
            var camp=Skin(overlay,"营地金属框",0,0,1232,836);Center(camp);
            Skin(camp,"标题底",9,9,1214,96,"tooltip_header");Icon(camp,"Assets/Game/UI/Icons/商店.png",32,29,53,53);
            Bold(l,camp,"camp.title","",100,27,820,49,31,Gold);
            Bold(l,camp,"camp.gold","",937,30,260,43,25,Gold,TextAnchor.MiddleRight);
            l.Text(camp,"camp.hint","",35,102,1162,28,17,Muted);
            var choices=Rect(camp,"遗物选择",32,210,1168,372);l.Toggle("camp.choices",choices.gameObject);
            var layout=choices.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=24;layout.childAlignment=TextAnchor.MiddleCenter;
            layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            for(int i=0;i<4;i++)
            {
                var card=Card(l,choices,"camp.relic",i,0,0,260,372);l.Toggle("camp.relic.visible."+i,card.gameObject);
                var icon=Slot(card,"遗物图标",84,28,92);l.Picture(icon,"camp.relic.icon."+i,7,7,78,78);Frame(icon);
                Bold(l,card,"camp.relic.title."+i,"",18,137,224,36,25,Gold,TextAnchor.MiddleCenter);
                l.Text(card,"camp.relic.description."+i,"",23,186,214,123,18,White).alignment=TextAnchor.UpperCenter;
                Skin(card,"底部分割",24,321,212,2,"divider",false);
                l.Text(card,null,"选择此遗物",24,331,212,29,16,Gold).alignment=TextAnchor.MiddleCenter;
            }
            var store=Rect(camp,"整备",32,144,1168,650);l.Toggle("camp.store",store.gameObject);
            for(int i=0;i<5;i++)
            {
                var card=Card(l,store,"camp.buy",i,i*236,0,224,246);
                var slot=Slot(card,"装备图标",77,15,70);l.Picture(slot,"camp.stock.icon."+i,6,6,58,58);Frame(slot);
                Bold(l,card,"camp.stock.title."+i,"",13,94,198,29,21,White,TextAnchor.MiddleCenter);
                var description=l.Text(card,"camp.stock.description."+i,"",18,132,188,75,15,Muted);description.alignment=TextAnchor.UpperCenter;
                description.resizeTextForBestFit=true;description.resizeTextMinSize=13;description.resizeTextMaxSize=15;
                Bold(l,card,"camp.stock.price."+i,"",15,208,194,27,19,Gold,TextAnchor.MiddleCenter);
            }
            Button(l,store,"camp.refresh","刷新货架","camp.refresh",0,0,261,260,43,18);
            l.Text(store,null,"购入装备将在下一场战斗继续生效",282,262,880,42,16,Muted);
            Bold(l,store,null,"已有装备",0,317,140,27,18,Gold);
            l.Text(store,null,"点击出售 · 返还原价 50%",145,318,1020,26,15,Muted);
            for(int i=0;i<6;i++)
            {
                var sell=Card(l,store,"camp.sell",i,i*196,353,188,84);
                var slot=Slot(sell,"图标",8,14,54);l.Picture(slot,"camp.sell.icon."+i,5,5,44,44);Frame(slot);
                l.Text(sell,"camp.sell."+i,"",72,9,104,64,15,White);
            }
            var routes=Rect(store,"路线",0,466,1168,112);l.Toggle("camp.routes",routes.gameObject);
            var safe=Button(l,routes,null,"稳健路线\n常规强度与奖励","camp.route",0,0,0,420,78,18);
            var danger=Button(l,routes,null,"凶险路线\n双倍精英 · 生命 +25% · 金币 ×1.6","camp.route",1,438,0,448,78,18);
            RouteHighlight(l,safe,0);RouteHighlight(l,danger,1);
            Bold(l,routes,"camp.route.status","",0,82,890,27,17,Gold);
            Button(l,store,null,"整备完成 · 出发","camp.depart",0,900,567,268,58,22);
            var feedback=Rect(camp,"交易反馈",32,786,1168,32);l.Toggle("progression.hintVisible",feedback.gameObject);
            Bold(l,feedback,"progression.hint","",0,0,1168,32,17,Gold,TextAnchor.MiddleCenter);
        }
    }
}
