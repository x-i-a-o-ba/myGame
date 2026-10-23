"""2026-10-23 开局选择面板 UI.SelectScene.SelectPanel：第 1 步选英雄（左侧英雄卡 + 右侧技能详情，中间露出 3D 展示台），
第 2 步选战场（三张战场卡，每张列出该战场的怪物）。绑定键与 SelectSceneController.RequiredKeys() 一一对应；
保留根对象与根组件的 fileID（SelectScene.unity 的预制体实例覆盖引用它们）。输出预览 preview/select_hero.png、select_map.png。"""
import sys, os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import json
import ui
from ui import *
PREVIEW=os.path.join(os.path.dirname(os.path.abspath(__file__)),'preview')
GOLD=(0.97,0.83,0.52,1); LIGHT=(0.9,0.93,0.95,1); MUTED=(0.6,0.66,0.7,1); CYAN=(0.62,0.9,1,1); WHITE=(1,1,1,1); DARK=(0.08,0.07,0.05,1)
pv=ui.previews; tg={}
def on(node,value=True): tg[node.go]=value; return node
def tr(parent,name,right,y,w,h,active=1): return Node(name,parent,(1,1),(1,1),(-right,-y),(w,h),(1,1),active)
def br(parent,name,right,bottom,w,h,active=1): return Node(name,parent,(1,0),(1,0),(-right,bottom),(w,h),(1,0),active)
def bl(parent,name,left,bottom,w,h,active=1): return Node(name,parent,(0,0),(0,0),(left,bottom),(w,h),(0,0),active)
def tc(parent,name,dx,y,w,h,active=1): return Node(name,parent,(0.5,1),(0.5,1),(dx,-y),(w,h),(0.5,1),active)
def framed(parent,name,x,y,size,key,b,sample,frame="slot_frame"):
    box=at(parent,name,x,y,size,size); image(box,"slot_back")
    b.image(key,raw(fill(box,"Image",3,3,3,3),preview=sample)); image(fill(box,"Frame"),frame,kind="sliced"); return box
def button_node(node,label,command,value,size,color):
    gfx=image(node,"button",kind="sliced",raycast=1); button(node,gfx,command,value)
    text(fill(node,"Label"),label,size,color,"MC",bold=True); return node

b=Bindings()
root=Node("UI.SelectScene.SelectPanel",None,(0,0),(1,1),(0,0),(0,0),(0.5,0.5),go=7244628370001952933,rt=3061317728065645930)
root.cr=4175884883136756894
image(root,None,(0,0,0,0)); cid,kind,body=root.comps[0]; root.comps[0]=(6856260264633756881,kind,body)  # 透明背景：露出 3D 展示台
# ---- 顶栏与底栏（在步骤组之前，步骤组的按钮画在它们上面） ----
top=Node("Top Bar",root,(0,1),(1,1),(0,0),(0,96),(0.5,1))
image(fill(top,"Back"),"white",(0.015,0.02,0.03,0.88)); image(Node("Line",top,(0,0),(1,0),(0,0),(0,2),(0.5,0)),"divider")
t=tc(top,"Title",0,12,900,46); b.text("select.title",text(t,"",32,GOLD,"MC",bold=True)); pv[t.go]="选择英雄"
t=tc(top,"Step Text",0,60,1100,26); b.text("select.stepText",text(t,"",15,MUTED,"MC")); pv[t.go]="第 1 步 / 共 2 步  ·  选择出战英雄"
for right,label in ((214,"1  选择英雄"),(40,"2  选择战场")):
    pill=tr(top,"Step "+label[0],right,26,160,44); image(pill,"chip",(0.55,0.6,0.66,1),kind="sliced")
    text(fill(pill,"Label"),label,16,MUTED,"MC",bold=True)
bottom=Node("Bottom Bar",root,(0,0),(1,0),(0,0),(0,100),(0.5,0))
image(fill(bottom,"Back"),"white",(0.015,0.02,0.03,0.88)); image(Node("Line",bottom,(0,1),(1,1),(0,0),(0,2),(0.5,1)),"divider")
t=bl(bottom,"Summary",40,50,1200,32); b.text("select.summary",text(t,"",20,LIGHT,"ML",bold=True)); pv[t.go]="已选英雄：寒冰射手  ·  远程 · 控制"
t=bl(bottom,"Hint",40,16,1200,26); b.text("select.hint",text(t,"",14,MUTED,"ML")); pv[t.go]="Esc 返回主菜单  ·  ← → 切换英雄  ·  Enter 下一步"
def step_pill(group,right,label):
    pill=tr(group,"Active Step",right,26,160,44); image(pill,"badge",GOLD,kind="sliced")
    text(fill(pill,"Label"),label,16,DARK,"MC",bold=True,shadow=False)

# ================= 第 1 步：选择英雄 =================
hero_step=on(b.toggle("select.step.hero",fill(root,"Hero Step")))
step_pill(hero_step,214,"1  选择英雄")
button_node(at(hero_step,"Menu Button",24,22,180,50),"返回主菜单","select.menu",0,17,LIGHT)
button_node(br(hero_step,"Next Button",40,20,300,62),"下一步：选择战场","select.next",0,21,GOLD)
lst=at(hero_step,"Hero List",40,116,440,600)
text(at(lst,"Header",0,0,440,30),"出战英雄",19,GOLD,"ML",bold=True)
text(at(lst,"Header Hint",0,0,440,30),"点击卡片查看技能",13,MUTED,"MR")
HEROES=[("寒冰射手","远程 · 控制","寒冰射手"),("磐锋卫士","近战 · 坦克","Assets/GameData/表现/磐锋/磐锋卫士.png")]
for i in range(4):
    slot=at(lst,"Slot %d"%i,0,40+i*136,440,124)
    card=on(b.toggle("select.hero.%d.visible"%i,fill(slot,"Hero")),i<len(HEROES))
    gfx=image(card,"hud_plate",kind="sliced",raycast=1); button(card,gfx,"select.hero",i)
    sel=on(b.toggle("select.hero.%d.selected"%i,fill(card,"Selected",-3,-3,-3,-3,active=0)),i==0)
    image(sel,"highlight",(1,0.8,0.35,1),kind="sliced"); image(at(sel,"Accent",3,14,5,102),"white",GOLD)
    framed(card,"Portrait",16,14,96,"select.hero.%d.portrait"%i,b,HEROES[i][2] if i<len(HEROES) else None,"portrait_frame")
    t=at(card,"Name",128,24,296,38); b.text("select.hero.%d.name"%i,text(t,"",24,WHITE,"ML",bold=True)); pv[t.go]=HEROES[i][0] if i<len(HEROES) else ""
    t=at(card,"Role",128,64,296,28); b.text("select.hero.%d.role"%i,text(t,"",16,CYAN,"ML")); pv[t.go]=HEROES[i][1] if i<len(HEROES) else ""
    empty=on(b.toggle("select.hero.%d.visible"%i,fill(slot,"Coming Soon"),1),i>=len(HEROES))
    image(empty,"hud_plate",(1,1,1,0.45),kind="sliced"); text(fill(empty,"Label"),"更多英雄 · 敬请期待",16,MUTED,"MC")
det=tr(hero_step,"Hero Detail",40,112,620,850); image(det,"hud_body",kind="sliced",raycast=1)
framed(det,"Portrait",20,20,104,"select.hero.portrait",b,"寒冰射手","portrait_frame")
t=at(det,"Name",142,24,458,44); b.text("select.hero.name",text(t,"",32,GOLD,"ML",bold=True)); pv[t.go]="寒冰射手"
t=at(det,"Role",142,72,458,28); b.text("select.hero.role",text(t,"",17,CYAN,"ML")); pv[t.go]="远程 · 控制"
t=at(det,"Description",20,138,580,68); b.text("select.hero.description",text(t,"",15,LIGHT,"UL",wrap=True)); pv[t.go]="远程冰霜射手，可以边移动边攻击：普攻叠层后用冰霜专注提升攻速，冰霜扇击散射减速，冰霜冲击用两层充能轰炸怪群，冰晶巨箭眩晕首个目标并减速大片敌人。适合拉开距离放风筝。"
image(at(det,"Divider 1",20,214,580,2),"divider")
STATS=[("生命","health","300"),("法力","mana","100"),("攻击力","attack","30"),("攻击间隔","interval","0.12 秒"),("攻击距离","range","20 米"),("移动速度","move","10 米/秒")]
for idx,(label,key,sample) in enumerate(STATS):
    cell=at(det,"Stat "+label,20+(idx%3)*198,226+(idx//3)*58,184,50); image(cell,"hud_plate",kind="sliced")
    text(at(cell,"Label",12,4,164,18),label,12,MUTED,"ML")
    t=at(cell,"Value",12,22,164,26); b.text("select.hero.stat."+key,text(t,"",19,WHITE,"ML",bold=True)); pv[t.go]=sample
image(at(det,"Divider 2",20,348,580,2),"divider")
atk=at(det,"Basic Attack",20,360,580,64)
framed(atk,"Icon",0,0,56,"select.attack.icon",b,"冰箭普攻")
t=at(atk,"Name",70,0,510,24); b.text("select.attack.name",text(t,"",16,LIGHT,"ML",bold=True)); pv[t.go]="冰箭普攻  ·  被动 · 远程弹道"
t=at(atk,"Text",70,26,510,38); b.text("select.attack.text",text(t,"",13,MUTED,"UL",wrap=True)); pv[t.go]="被动：自动向攻击距离内的敌人射出追踪冰箭。命中计入普攻命中，可为冰霜专注叠层。"
text(at(det,"Skills Header",20,432,580,24),"技能",16,GOLD,"ML",bold=True)
SKILLS=[("冰霜专注","Q  ·  普通技能  ·  无需目标  ·  普攻叠 4 层  ·  冷却 12 秒","普攻命中 4 次后可主动施放，消耗层数并提高自身攻速，持续 5 秒。等级提升增加攻速收益；不附加多箭或额外伤害。"),
        ("冰霜扇击","W  ·  普通技能  ·  指向方向  ·  冷却 8 秒  ·  法力 90","朝指定方向散射 7 支冰箭。每支箭命中首个敌人后消失，造成伤害并减速 35%，持续 2.5 秒；"),
        ("冰霜冲击","E  ·  普通技能  ·  指向地点  ·  2 层充能 · 每层 9 秒","向指定位置发射冰霜弹，在落点半径 5 Unity 单位内造成范围伤害。"),
        ("冰晶巨箭","R  ·  终极技能  ·  指向方向  ·  冷却 60 秒  ·  法力 150","终极技能。射出巨型冰晶箭，命中首个敌人后造成首中伤害并眩晕 1.5 秒，再对半径 7 Unity 单位内的敌人造成爆炸伤害和 50% 减速（3 秒）。")]
for j,(name,meta,desc) in enumerate(SKILLS):
    row=on(b.toggle("select.skill.%d.visible"%j,at(det,"Skill %d"%j,20,462+j*94,580,88)))
    framed(row,"Icon",0,6,64,"select.skill.%d.icon"%j,b,name)
    hk=at(row,"Hotkey",-4,2,24,24); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),"QWER"[j],14,WHITE,"MC",bold=True,shadow=False)
    t=at(row,"Name",78,0,502,24); b.text("select.skill.%d.name"%j,text(t,"",17,WHITE,"ML",bold=True)); pv[t.go]=name
    t=at(row,"Meta",78,24,502,20); b.text("select.skill.%d.meta"%j,text(t,"",12,CYAN,"ML")); pv[t.go]=meta
    t=at(row,"Text",78,46,502,40); b.text("select.skill.%d.text"%j,text(t,"",13,LIGHT,"UL",wrap=True)); pv[t.go]=desc[:74]

# ================= 第 2 步：选择战场 =================
map_step=on(b.toggle("select.step.map",fill(root,"Map Step",0,96,0,100,active=0)),False)
image(fill(map_step,"Backdrop"),"white",(0.012,0.016,0.024,0.96))
map_top=fill(root,"Map Step Bar",active=0); b.toggle("select.step.map",map_top)  # 顶栏 / 底栏上的第 2 步按钮与步骤高亮
step_pill(map_top,40,"2  选择战场")
button_node(at(map_top,"Back Button",24,22,180,50),"上一步","select.back",0,17,LIGHT)
start=br(map_top,"Start Button",40,20,300,62); image(at(start,"Glow",-30,-24,360,110),"glow",(1,0.7,0.25,0.35))
button_node(start,"开始战斗","select.start",0,24,GOLD)
MAPS=[("幽暗荒原","Assets/GameData/表现/战场/幽暗荒原.png","共 30 波  ·  8 种怪物  ·  2 种首领","月光下的灰绿荒原，亡灵与野兽从四面涌来：食尸鬼与猎犬贴身围攻，骷髅弓手远程放箭，蛮牛冲锋、毒囊自爆，岩石魔像堵路；首领波由深渊领主或霜骨巫妖坐镇。",
       [("腐化食尸鬼","开局出现"),("迅捷猎犬","第 2 波起"),("骷髅弓手","第 3 波起"),("蛮牛冲锋者","第 4 波起"),("爆裂毒囊","第 5 波起"),("岩石魔像","第 6 波起"),("深渊领主","首领"),("霜骨巫妖","精英 · 首领")]),
      ("熔火裂谷","Assets/GameData/表现/战场/熔火裂谷.png","共 30 波  ·  8 种怪物  ·  2 种首领","熔岩河横贯的火山裂谷：余烬小鬼与熔岩蜥蜴蜂拥而上，火焰术士投掷火球，熔岩犀牛远距离冲锋，爆燃火灵大范围自爆，黑曜石巨像堵路；首领波由熔火领主或灰烬女巫坐镇。",
       [("余烬小鬼","开局出现"),("熔岩蜥蜴","第 2 波起"),("火焰术士","第 3 波起"),("熔岩犀牛","第 4 波起"),("爆燃火灵","第 5 波起"),("黑曜石巨像","第 6 波起"),("熔火领主","首领"),("灰烬女巫","精英 · 首领")])]
for k in range(3):
    slot=tc(map_step,"Slot %d"%k,-600+600*k,16,560,846)
    card=on(b.toggle("select.map.%d.visible"%k,fill(slot,"Map")),k<len(MAPS))
    gfx=image(card,"hud_body",kind="sliced",raycast=1); button(card,gfx,"select.map",k)
    sel=on(b.toggle("select.map.%d.selected"%k,fill(card,"Selected",-4,-4,-4,-4,active=0)),k==1)
    image(sel,"highlight",(1,0.8,0.35,1),kind="sliced")
    badge=at(sel,"Badge",404,-12,140,30); image(badge,"badge",GOLD,kind="sliced"); text(fill(badge,"Label"),"已选择",14,DARK,"MC",bold=True,shadow=False)
    data=MAPS[k] if k<len(MAPS) else None
    pre=at(card,"Preview",20,22,520,292); b.image("select.map.%d.preview"%k,raw(fill(pre,"Image"),preview=data[1] if data else None)); image(fill(pre,"Frame",-3,-3,-3,-3),"slot_frame",kind="sliced")
    t=at(card,"Name",22,328,516,40); b.text("select.map.%d.name"%k,text(t,"",28,GOLD,"ML",bold=True)); pv[t.go]=data[0] if data else ""
    t=at(card,"Info",22,370,516,22); b.text("select.map.%d.info"%k,text(t,"",14,CYAN,"ML")); pv[t.go]=data[2] if data else ""
    t=at(card,"Description",22,398,516,88); b.text("select.map.%d.description"%k,text(t,"",14,LIGHT,"UL",wrap=True)); pv[t.go]=data[3] if data else ""
    image(at(card,"Divider",20,492,520,2),"divider")
    text(at(card,"Enemies Header",22,502,516,24),"出没怪物",16,GOLD,"ML",bold=True)
    for m in range(8):
        cell=on(b.toggle("select.map.%d.enemy.%d.visible"%(k,m),at(card,"Enemy %d"%m,20+(m%4)*130,534+(m//4)*150,122,144)),bool(data))
        image(cell,"hud_plate",kind="sliced")
        framed(cell,"Portrait",13,6,96,"select.map.%d.enemy.%d.portrait"%(k,m),b,data[4][m][0] if data else None)
        t=at(cell,"Name",2,104,118,20); b.text("select.map.%d.enemy.%d.name"%(k,m),text(t,"",13,LIGHT,"MC",bold=True)); pv[t.go]=data[4][m][0] if data else ""
        t=at(cell,"Tag",2,124,118,18); b.text("select.map.%d.enemy.%d.tag"%(k,m),text(t,"",11,MUTED,"MC")); pv[t.go]=data[4][m][1] if data else ""
    empty=on(b.toggle("select.map.%d.visible"%k,fill(slot,"Coming Soon"),1),k>=len(MAPS))
    image(empty,"hud_plate",(1,1,1,0.5),kind="sliced"); text(fill(empty,"Label"),"更多战场\n敬请期待",24,MUTED,"MC",spacing=1.3)

panel(root,"UI.SelectScene.SelectPanel",b)
cid,kind,body=root.comps[-1]; root.comps[-1]=(6737393901559443948,kind,body)
path=write("UI.SelectScene.SelectPanel",root)
json.dump(dict(texts=[k for k,_ in b.texts],images=[k for k,_ in b.images],toggles=[k for k,_,_ in b.toggles]),open(os.path.join(os.path.dirname(os.path.abspath(__file__)),"select_keys.json"),"w",newline="\n"),indent=0)

# ================= 预览 =================
from PIL import Image as I, ImageDraw as D
W,H=1920,1080
bg=I.new("RGBA",(W,H)); d=D.Draw(bg)
for y in range(H):
    v=y/H; d.line((0,y,W,y),fill=(int(14+18*v),int(18+20*v),int(26+22*v),255))
d.ellipse((W/2-260,700,W/2+260,800),fill=(42,46,56,255)); d.ellipse((W/2-240,690,W/2+240,780),fill=(58,62,74,255))
d.ellipse((W/2-250,700,W/2+250,790),outline=(250,200,90,255),width=4)
d.rounded_rectangle((W/2-70,330,W/2+70,740),radius=70,fill=(40,150,200,255))
bgp=os.path.join(PREVIEW,"select_bg.png"); bg.save(bgp)
state=dict(tg)
render(root,W,H,(0,0,W,H),state,os.path.join(PREVIEW,"select_hero.png"),bg=bgp)
state[hero_step.go]=False; state[map_step.go]=True; state[map_top.go]=True
pv[root.children[0].children[2].go]="选择战场"; pv[root.children[0].children[3].go]="第 2 步 / 共 2 步  ·  每个战场有不同的怪物"
pv[root.children[1].children[2].go]="出战英雄：寒冰射手    ·    战场：熔火裂谷"; pv[root.children[1].children[3].go]="Esc 上一步  ·  ← → 切换战场  ·  Enter 开始战斗"
render(root,W,H,(0,0,W,H),state,os.path.join(PREVIEW,"select_map.png"),bg=bgp)
print("ok",path,len(b.texts),"texts",len(b.images),"images",len(b.toggles),"toggles")
