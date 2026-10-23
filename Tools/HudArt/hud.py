import sys, os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
PREVIEW=os.path.join(os.path.dirname(os.path.abspath(__file__)),'preview'); os.makedirs(PREVIEW,exist_ok=True)
def P(name): return os.path.join(PREVIEW,name)
import ui
from ui import *
GOLD=(0.97,0.83,0.52,1); LIGHT=(0.9,0.93,0.95,1); MUTED=(0.6,0.66,0.7,1); CYAN=(0.62,0.9,1,1); WHITE=(1,1,1,1)
HP=(0.27,0.8,0.24,1); MP=(0.22,0.48,0.98,1); RED=(0.88,0.2,0.14,1)
pv=ui.previews; tg={}; ui.fill_preview.clear()
import contextlib, random as _random
@contextlib.contextmanager
def extra(key):
    """新增节点用独立随机序列分配 fileID（按 key 播种），插入新对象时不改变已有对象的 ID，预制体差异最小。"""
    saved=ui.rng; ui.rng=_random.Random(key)
    try: yield
    finally: ui.rng=saved
EQUIP="Assets/Game/Equipment/Icons/%s.png"
def on(node,value=True): tg[node.go]=value; return node
def bar(parent,b,name,x,y,w,h,color,fillkey,textkey,textsize,regenkey=None,amount=1.0):
    back=at(parent,name,x,y,w,h); image(back,"bar_back",kind="sliced")
    inner=fill(back,"Inner",2,2,2,2)
    f=fill(inner,"Fill"); f.rect=((0,0),(1,1),(0,0),(0,0),(0,0.5))
    b.fill(fillkey,image(f,"bar_fill",color,kind="sliced"),0); ui.hfill[f.go]=amount
    t=fill(back,"Value"); b.text(textkey,text(t,"",textsize,WHITE,"MC",bold=True)); return back,t
# ================= 底部 HUD =================
b=Bindings()
root=Node("UI.Game.BottomHUD",None,(0.5,0),(0.5,0),(0,0),(1564,214),(0.5,0),go=7807990809045325555,rt=7026121659916554133)
body=at(root,"Body",0,32,1564,182); image(body,"hud_body",kind="sliced",raycast=1)
center=at(root,"Ability Section",438,2,580,212); image(center,"hud_body",kind="sliced",raycast=1)
# ---- 左侧：头像、等级、属性 ----
plate=at(root,"Portrait",14,44,168,158); image(plate,"hud_plate",kind="sliced",raycast=1)
por=fill(plate,"Image",3,3,3,3); b.image("hero.portrait",raw(por,preview="寒冰射手"))
shade=at(plate,"Name Shade",3,112,162,43); image(shade,"shade_bottom",kind="simple")
nm=at(plate,"HeroName",4,124,160,28); b.text("hero.name",text(nm,"寒冰射手",17,WHITE,"MC",bold=True)); pv[nm.go]="寒冰射手"
xpt=at(plate,"Experience",58,6,104,18); b.text("hero.xpText",text(xpt,"",12,LIGHT,"MR")); pv[xpt.go]="经验 120 / 240"
stats=at(root,"Stats",192,44,236,158); image(stats,"hud_plate",kind="sliced",raycast=1)
hover(plate,"combat.hover",20)  # 仅头像显示全部属性；每项属性单独悬停
for i,(label,key,sample) in enumerate((("攻击","stats.attack","51"),("攻速","stats.attackSpeed","9.4/秒"),("射程","stats.range","20"),("移速","stats.move","10"))):
    row=at(stats,"Stat "+label,8,8+i*28,112,26)
    with extra("hud.stat.left."+str(i)):
        image(fill(row,"Hover"),"white",(1,1,1,0),raycast=1); hover(row,"combat.hover",30+[1,2,10,3][i])
    text(at(row,"Label",0,0,44,26),label,13,MUTED,"ML")
    v=at(row,"Value",40,0,64,26); b.text(key,text(v,"",15,LIGHT,"MR",bold=True)); pv[v.go]=sample
with extra("hud.stats.right"):
    image(at(stats,"Column Divider",117,12,2,104),"white",(1,1,1,0.08))
    for i,(label,key,sample) in enumerate((("技伤","stats.amplify","30%"),("冷缩","stats.cooldown","20%"),("减伤","stats.reduction","10%"),("暴击","stats.critical","15%"))):
        row=at(stats,"Stat "+label,124,8+i*28,104,26)
        with extra("hud.stat.right."+str(i)):
            image(fill(row,"Hover"),"white",(1,1,1,0),raycast=1); hover(row,"combat.hover",30+[5,6,11,13][i])
        text(at(row,"Label",0,0,44,26),label,13,MUTED,"ML")
        v=at(row,"Value",40,0,64,26); b.text(key,text(v,"",15,LIGHT,"MR",bold=True)); pv[v.go]=sample
badge=at(root,"Level Badge",2,26,62,62); image(badge,"level_badge")
ringb=fill(badge,"Ring Back",-3,-3,-3,-3); image(ringb,"ring",(0,0,0,0.65))
ring=fill(badge,"Ring",-3,-3,-3,-3); b.fill("hero.xp",image(ring,"ring",(1,0.8,0.28,1),kind="filled",amount=0.0,origin=0),2); ui.fill_preview[ring.go]=0.5
lv=fill(badge,"Level"); b.text("hero.level",text(lv,"1",24,GOLD,"MC",bold=True,outline=True)); pv[lv.go]="7"
pts=on(b.toggle("hero.pointsVisible",at(stats,"Skill Points",58,122,120,30,active=0)))
image(at(pts,"Glow",-6,-6,132,42),"glow",(1,0.78,0.25,0.55)); image(fill(pts,"Back"),"hud_plate",kind="sliced")
pt=fill(pts,"Text"); b.text("hero.points",text(pt,"",14,(1,0.86,0.4,1),"MC",bold=True)); pv[pt.go]="技能点 ×2"
# ---- 中央：被动 + QWER ----
def slot(parent,name,x,y,size,key=None):
    s=at(parent,name,x,y,size,size)
    back=image(s,"slot_back",raycast=1)
    icon=fill(s,"Icon",7,7,7,7)
    return s,back,icon
ps,pback,picon=slot(center,"Passive",26,52,76)
hover(ps,"combat.hover",4)  # 悬停显示自动攻击被动提示（CombatHud.PassiveSlot）
b.image("passive.icon",raw(picon,preview="冰箭普攻"))
psh=at(ps,"Name Shade",7,46,62,23); image(psh,"shade_bottom")
pn=at(ps,"Name",7,48,62,20); b.text("passive.name",text(pn,"",11,LIGHT,"MC")); pv[pn.go]="冰箭普攻"
image(fill(ps,"Frame"),"slot_frame",kind="sliced")
tag=at(ps,"Tag",4,-14,68,16); text(tag,"被动",12,GOLD,"MC",bold=True)
pst=at(center,"Passive Status",14,134,100,18); b.text("passive.status",text(pst,"",12,CYAN,"MC")); pv[pst.go]="攻击中"
samples={"q":("冰霜专注",3,4,"","2/4 层",0),"w":("冰霜扇击",2,4,"4.2","",0.6),"e":("冰霜冲击",1,4,"","充能 1",0),"r":("冰晶巨箭",0,3,"","",0)}
for i,k in enumerate("qwer"):
    x=116+i*102; name,level,mx,cdt,status,cd=samples[k]
    s,back,icon=slot(center,"Skill "+k.upper(),x,44,92)
    button(s,back,"combat.ability",i); hover(s,"combat.hover",i)
    b.image("ability.%s.icon"%k,raw(icon,preview=name))
    nm_sh=at(s,"Name Shade",7,54,78,31); image(nm_sh,"shade_bottom")
    n2=at(s,"Name",7,54,78,17); b.text("ability.%s.name"%k,text(n2,"",12,LIGHT,"MC")); pv[n2.go]=name
    nomana=b.toggle("ability.%s.noMana"%k,fill(s,"No Mana",7,7,7,7,active=0)); image(nomana,"white",(0.1,0.2,0.65,0.5))
    locked=b.toggle("ability.%s.locked"%k,fill(s,"Locked",7,7,7,7,active=0)); image(locked,"white",(0.02,0.03,0.04,0.72)); on(locked,level==0)
    cooling=b.toggle("ability.%s.cooling"%k,fill(s,"Cooldown",7,7,7,7,active=0)); on(cooling,cd>0)
    b.fill("ability.%s.cd"%k,image(cooling,"white",(0,0,0,0.7),kind="filled",amount=1,origin=2,clockwise=0),2); ui.fill_preview[cooling.go]=cd
    ct=fill(s,"Cooldown Text"); b.text("ability.%s.cdText"%k,text(ct,"",28,WHITE,"MC",bold=True,outline=True)); pv[ct.go]=cdt
    image(fill(s,"Frame"),"slot_frame",kind="sliced")
    hk=at(s,"Hotkey",4,4,22,22); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),k.upper(),14,WHITE,"MC",bold=True,shadow=False)
    st=at(s,"Status",28,6,58,18); b.text("ability.%s.status"%k,text(st,"",12,CYAN,"MR",bold=True)); pv[st.go]=status
    mn=at(s,"Mana",7,72,78,14); b.text("ability.%s.mana"%k,text(mn,"",12,(0.65,0.82,1,1),"MC",bold=True))
    learn=on(b.toggle("ability.%s.learn"%k,at(s,"Learn",22,-40,48,32,active=0)),level<mx and k!="r")
    lg=image(learn,"learn_plus",raycast=1); button(learn,lg,"combat.learnSkill",i,0.8)
    pips=at(s,"Pips",0,96,92,10)
    for p in range(4):
        x0=4+p*21
        sl=on(b.toggle("ability.%s.pipSlot%d"%(k,p),at(pips,"Slot %d"%p,x0,0,20,9,active=1)),p<mx); image(sl,"pip_off")
        lit=on(b.toggle("ability.%s.pip%d"%(k,p),at(sl,"Lit",0,0,20,9,active=0)),p<level); image(lit,"pip_on")
bar(center,b,"Health",26,158,528,24,HP,"hero.health","hero.healthText",15,amount=0.82)
rg=at(center,"Regen",400,158,148,24); b.text("hero.regen",text(rg,"",12,(0.75,1,0.7,1),"MR")); pv[rg.go]="+2.2"
pv[center.children[-2].children[-1].go]="1027 / 1252"
bar(center,b,"Mana",26,184,528,18,MP,"hero.mana","hero.manaText",13,amount=1.0)
pv[center.children[-1].children[-1].go]="260 / 260"
with extra("hud.resource.regen"):
    mr=at(center,"Mana Regen",400,184,148,18)
    b.text("hero.manaRegen",text(mr,"",12,(0.7,0.85,1,1),"MR")); pv[mr.go]="+4.5/秒"
    for node,value in ((rg,34),(mr,39)):
        image(fill(node,"Hover"),"white",(1,1,1,0),raycast=1); hover(node,"combat.hover",value)
    for name,value in (("Health",30),("Mana",38)):
        node=next(n for n in center.children if n.name==name)
        image(fill(node,"Hover"),"white",(1,1,1,0),raycast=1); hover(node,"combat.hover",value)

# ---- 装备栏（Dota 2 式）：上方 3 × 2 共 6 格，下方金币；悬停看属性，拖到其他格换位、拖到地面丢弃、拖到商店或右键出售（商店打开时）。
# 键名与 CombatHud.EquipmentKeys 一一对应；拖动命令与 CombatHud.DragCommand / DragEndCommand / DropOnCommand 一致。
EQUIP_SAMPLE=["铁剑","迅捷手套","速度之靴","回复戒指"]
with extra("hud.equipment"):
    eq=at(root,"Equipment",1028,44,206,158); image(eq,"hud_plate",kind="sliced",raycast=1)
    text(at(eq,"Title",12,134,90,22),"装备",13,GOLD,"ML",bold=True)
    image(at(eq,"Gold Icon",112,135,20,20),"金币")
    gd=at(eq,"Gold",136,134,62,22); b.text("hero.gold",text(gd,"",15,(1,0.86,0.4,1),"MR",bold=True)); pv[gd.go]="820"
    for i in range(6):
        es=at(eq,"Slot %d"%i,10+(i%3)*64,8+(i//3)*63,60,60); image(es,"slot_back",raycast=1)
        hover(es,"combat.hover",10+i)            # CombatHud.EquipmentHoverBase + 格子序号
        pointer(es,"combat.equip.sell",i)         # 右键出售（商店打开时）
        it=on(b.toggle("equip.s%d.filled"%i,fill(es,"Item",active=0)),i<len(EQUIP_SAMPLE))
        ic=on(b.toggle("equip.s%d.hasIcon"%i,fill(it,"Icon",5,5,5,5,active=0)),True)
        icon_=b.image("equip.s%d.icon"%i,raw(ic,preview=EQUIP%EQUIP_SAMPLE[i] if i<len(EQUIP_SAMPLE) else None))
        nm=on(b.toggle("equip.s%d.hasIcon"%i,fill(it,"Name",5,5,5,5),invert=1),False); b.text("equip.s%d.name"%i,text(nm,"",11,LIGHT,"MC",wrap=True))
        lk=on(b.toggle("equip.s%d.locked"%i,fill(es,"Locked",5,5,5,5,active=0)),False); image(lk,"white",(0.02,0.03,0.04,0.72))
        image(fill(es,"Frame"),"slot_frame",kind="sliced")
        # 拖动源（格子里有装备时才能拖，拖动图标复制装备图标）与放置目标（换位）；拖到其他格上时该格显示金色高亮。
        drag(es,"combat.equip.drag","combat.equip.dragEnd",i,require=it,ghost=icon_)
        drop(es,"combat.equip.dropOn",i)
        with extra("hud.equipment.drop%d"%i):
            dt_=on(b.toggle("equip.s%d.drop"%i,fill(es,"Drop Target",-3,-3,-3,-3,active=0)),i==1)
            image(dt_,"highlight",(1,0.82,0.35,1),kind="sliced")
# ---- 右侧：指令、天赋树、商店 ----
rp=at(root,"Orders",1244,44,306,158); image(rp,"hud_plate",kind="sliced")
for i,(label,hot) in enumerate((("攻击","A"),("停止","S"),("待命","H"))):
    bt=at(rp,"Order "+hot,10+i*98,10,92,40); g=image(bt,"button",kind="sliced",raycast=1); button(bt,g,"combat.order",i)
    if i==0: armed=on(b.toggle("order.attackArmed",fill(bt,"Armed",-4,-4,-4,-4,active=0)),True); image(armed,"highlight",(1,0.45,0.3,1),kind="sliced")
    text(at(bt,"Label",8,0,56,40),label,15,LIGHT,"MC",bold=True)
    hk=at(bt,"Hotkey",62,10,22,20); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),hot,12,GOLD,"MC",bold=True,shadow=False)
oc=at(rp,"Current Order",12,54,282,22); b.text("order.current",text(oc,"",13,MUTED,"ML")); pv[oc.go]="指令：攻击移动"
tt=at(rp,"Talent Tree",10,80,140,44); tg_img=image(tt,"button",kind="sliced",raycast=1); button(tt,tg_img,"combat.talentTree",0)
pend=on(b.toggle("hud.talentPending",fill(tt,"Pending Glow",-6,-6,-6,-6,active=0)),True); image(pend,"highlight",(1,0.8,0.3,1),kind="sliced")
image(at(tt,"Icon",6,4,36,36),"天赋树")
text(at(tt,"Label",46,0,90,44),"天赋树",15,GOLD,"ML",bold=True)
pl=on(b.toggle("hud.talentPending",at(tt,"Pending",114,-8,30,22,active=0)),True)
with extra("hud.talent.pendingBadge"): image(pl,"badge",(0.95,0.55,0.15,1),kind="sliced")
text(fill(pl,"Text"),"!",15,WHITE,"MC",bold=True)
with extra("hud.orders.shop"):
    sh=at(rp,"Shop",156,80,140,44); sg=image(sh,"button",kind="sliced",raycast=1); button(sh,sg,"combat.shop",0)
    so=on(b.toggle("shop.open",fill(sh,"Open",-4,-4,-4,-4,active=0)),False); image(so,"highlight",(0.55,0.85,1,1),kind="sliced")
    image(at(sh,"Icon",6,4,36,36),"商店")
    text(at(sh,"Label",46,0,60,44),"商店",15,GOLD,"ML",bold=True)
    hk=at(sh,"Hotkey",108,11,22,22); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),"B",12,GOLD,"MC",bold=True,shadow=False)
with extra("hud.orders.shop.far"):
    # 不在商店范围内：按钮变暗，右上角显示到商店范围的距离（CombatHud.ShopKeys.Far / Distance）。
    far=on(b.toggle("shop.far",fill(sh,"Far",active=0)),True); image(far,"white",(0.02,0.03,0.05,0.5))
    dc=at(far,"Distance",84,-12,56,20); image(dc,"chip",(0.95,0.72,0.3,1),kind="sliced")
    dtx=fill(dc,"Text"); b.text("shop.distance",text(dtx,"",11,WHITE,"MC",bold=True)); pv[dtx.go]="6 米"
text(at(rp,"Help",12,128,282,24),"右键 移动/攻击/拾取 · 拖装备到地面丢弃 · B 商店",11,MUTED,"ML")
panel(root,"UI.Game.BottomHUD",b)
bottom=root; write("UI.Game.BottomHUD",root)
# ================= 顶栏 =================
b=Bindings()
root=Node("UI.Game.Title",None,(0.5,1),(0.5,1),(0,0),(720,96),(0.5,1),go=1789636718530517498,rt=3727400485391896698)
image(fill(root,"Bar"),"topbar",raycast=1)
ck=at(root,"Clock",260,4,200,42); b.text("top.clock",text(ck,"00:00",30,WHITE,"MC",bold=True)); pv[ck.go]="07:42"
wv=at(root,"Wave",260,44,200,24); b.text("top.wave",text(wv,"",16,GOLD,"MC",bold=True)); pv[wv.go]="第 8 / 20 波"
nx=at(root,"Next",260,68,200,20); b.text("top.next",text(nx,"",13,MUTED,"MC")); pv[nx.go]="下一波 12 秒"
text(at(root,"Kills Label",96,14,150,20),"击杀",13,MUTED,"MC")
kl=at(root,"Kills",96,34,150,36); b.text("top.kills",text(kl,"0",26,(1,0.86,0.56,1),"MC",bold=True)); pv[kl.go]="2,431"
text(at(root,"Alive Label",474,14,150,20),"存活敌人",13,MUTED,"MC")
al=at(root,"Alive",474,34,150,36); b.text("top.alive",text(al,"0",26,(1,0.5,0.45,1),"MC",bold=True)); pv[al.go]="8,964"
boss=on(b.toggle("top.boss",at(root,"Boss",110,102,500,42,active=0)))
bn=at(boss,"Name",0,0,500,18); b.text("top.bossName",text(bn,"",14,(1,0.62,0.5,1),"MC",bold=True)); pv[bn.go]="首领 · 深渊领主（第 5 波）"
fr=at(boss,"Frame",0,20,500,22); image(fr,"boss_frame",kind="sliced")
fi=fill(fr,"Inner",3,3,3,3); ff=fill(fi,"Fill"); b.fill("top.bossFill",image(ff,"bar_fill",RED,kind="sliced"),0); ui.hfill[ff.go]=0.64
panel(root,"UI.Game.Title",b)
title=root; write("UI.Game.Title",root)
# ================= 右上角 =================
b=Bindings()
root=Node("UI.Game.Controls",None,(1,1),(1,1),(-16,-14),(300,112),(1,1),go=7198065516410080699,rt=5117782400737888598)
image(fill(root,"Back"),"hud_body",kind="sliced",raycast=1)
fp=at(root,"FPS",14,8,130,26); b.text("top.fps",text(fp,"",16,(0.6,1,0.6,1),"ML",bold=True)); pv[fp.go]="60 FPS"
pj=at(root,"Projectiles",150,8,136,26); b.text("top.projectiles",text(pj,"",13,MUTED,"MR")); pv[pj.go]="弹体 1,204"
text(at(root,"Help",14,76,272,28),"F1 帮助  ·  F5 重开  ·  滚轮缩放视野",12,MUTED,"ML")
with extra("hud.controls.leave"):
    # 当前战场名（CombatHud 发布 top.map）与“返回选择”按钮：SceneNavigation 的 game.leave 路由需要 3 秒内再次点击才返回选择场景，
    # 首次点击发布 game.leave.confirm，按钮文字从“返回选择”切换为“确认返回？”。
    mp=at(root,"Map",14,40,156,30); b.text("top.map",text(mp,"",14,GOLD,"ML",bold=True)); pv[mp.go]="战场 · 幽暗荒原"
    lv=at(root,"Leave",176,40,110,30); lg=image(lv,"button",kind="sliced",raycast=1); button(lv,lg,"game.leave",0)
    ll=b.toggle("game.leave.confirm",fill(lv,"Label"),invert=1); text(ll,"返回选择",14,GOLD,"MC",bold=True)
    lc=b.toggle("game.leave.confirm",fill(lv,"Confirm",active=0)); text(lc,"确认返回？",14,(1,0.62,0.45,1),"MC",bold=True)
panel(root,"UI.Game.Controls",b)
controls=root; write("UI.Game.Controls",root)
# ================= 提示条（动态） =================
b=Bindings()
root=Node("UI.Game.Progression",None,(0.5,0),(0.5,0),(0,226),(760,40),(0.5,0),active=0)
hb=on(b.toggle("progression.hintVisible",fill(root,"Back",active=0))); image(hb,"hint_back",kind="sliced")
ht=fill(root,"Hint",20,0,20,0); b.text("progression.hint",text(ht,"",17,(1,0.9,0.6,1),"MC",bold=True)); pv[ht.go]="升到 7 级！按 Ctrl+Q/W/E/R 学习技能"
panel(root,"UI.Game.Progression",b,blocks=0)
hint=root; write("UI.Game.Progression",root)
# ================= 天赋树（动态） =================
b=Bindings()
root=Node("UI.Game.Talent",None,(0.5,0),(0.5,0),(0,278),(820,350),(0.5,0),active=0)
image(fill(root,"Back"),"hud_body",kind="sliced",raycast=1)
image(at(root,"Icon",20,14,36,36),"天赋树")
tl=at(root,"Title",64,12,690,40); b.text("talent.title",text(tl,"",17,GOLD,"ML",bold=True)); pv[tl.go]="天赋树  ·  10 级天赋可选（按 1 / 2 或点击，不消耗技能点）"
cl=at(root,"Close",770,14,36,36); cg=image(cl,"button",kind="sliced",raycast=1); button(cl,cg,"combat.talentTree",0); text(fill(cl,"X"),"×",22,LIGHT,"MC",bold=True)
rows=[("+20 攻击力","+25% 经验获取"),("+25% 攻击速度","+250 最大生命"),("+30% 技能伤害","-20% 技能冷却"),("+60 攻击力","+10 生命回复")]
for i in range(4):
    y=64+(3-i)*64
    row=on(b.toggle("talent.t%d.row"%i,at(root,"Tier %d"%i,18,y,784,56)))
    pg=on(b.toggle("talent.t%d.pending"%i,fill(row,"Pending",-5,-5,-5,-5,active=0)),i==0); image(pg,"highlight",(1,0.8,0.3,1),kind="sliced")
    for side,x,al_,val in (("left",0,"MR",i*2),("right",454,"ML",i*2+1)):
        bt=at(row,side.capitalize(),x,0,330,56); g=image(bt,"talent_option",kind="sliced",raycast=1); button(bt,g,"combat.talent",val)
        ch=on(b.toggle("talent.t%d.%sChosen"%(i,side),fill(bt,"Chosen",-3,-3,-3,-3,active=0)),i==1 and side=="left"); image(ch,"highlight",(0.55,0.85,1,1),kind="sliced")
        lb=fill(bt,"Label",16,0,16,0); b.text("talent.t%d.%s"%(i,side),text(lb,"",15,LIGHT,al_,bold=True)); pv[lb.go]=rows[i][0 if side=="left" else 1]
        if i==0:
            hk=at(bt,"Hotkey",8 if side=="left" else 300,17,22,22); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),"1" if side=="left" else "2",12,GOLD,"MC",bold=True,shadow=False)
    lvb=at(row,"Level",364,0,56,56); image(lvb,"level_badge")
    lt=fill(lvb,"Text"); b.text("talent.t%d.level"%i,text(lt,"",20,GOLD,"MC",bold=True,outline=True)); pv[lt.go]=str((10,15,20,25)[i])
    lk=on(b.toggle("talent.t%d.locked"%i,fill(row,"Locked",active=0)),i>=2); image(lk,"white",(0.01,0.02,0.03,0.55))
text(at(root,"Footer",18,322,784,22),"每层左右二选一 · 选择后立即生效且不可更改 · 点击“天赋树”按钮或 × 关闭",12,MUTED,"MC")
panel(root,"UI.Game.Talent",b)
talent=root; write("UI.Game.Talent",root)
# ================= 选中目标信息（动态，左上角） =================
# 左键点选敌人后显示；键名与 CombatHud.TargetKeys 一一对应。
b=Bindings()
TW,TH=440,444; ENEMY=(1,0.62,0.5,1); FROST=(0.62,0.86,1,1)
root=Node("UI.Game.TargetInfo",None,(0,1),(0,1),(16,-14),(TW,TH),(0,1),active=0)
image(fill(root,"Back"),"target_frame",kind="sliced",raycast=1)
image(at(root,"Header Glow",8,8,TW-16,132),"target_header")
on(b.toggle("target.tierElite",at(root,"Elite Glow",-6,-6,164,164,active=0)),False); image(root.children[-1],"glow",(1,0.78,0.28,0.6))
on(b.toggle("target.tierBoss",at(root,"Boss Glow",-6,-6,164,164,active=0)),True); image(root.children[-1],"glow",(1,0.22,0.12,0.65))
plate=at(root,"Portrait",18,18,116,116); image(plate,"hud_plate",kind="sliced",raycast=1)
por=fill(plate,"Image",7,7,7,7); b.image("target.portrait",raw(por,preview="深渊领主"))
image(fill(plate,"Frame"),"portrait_frame",kind="sliced")
tier=at(root,"Tier",36,124,80,24)
for key,colr,shown in (("target.tierNormal",(0.6,0.65,0.7,1),False),("target.tierElite",(1,0.76,0.25,1),False),("target.tierBoss",(0.92,0.24,0.18,1),True)):
    v=on(b.toggle(key,fill(tier,key.split(".")[1][4:]+" Badge",active=0)),shown); image(v,"badge",colr,kind="sliced")
tt=fill(tier,"Text"); b.text("target.tier",text(tt,"",13,WHITE,"MC",bold=True,outline=True)); pv[tt.go]="首领"
nm=at(root,"Name",148,16,240,34); b.text("target.name",text(nm,"",22,ENEMY,"ML",bold=True)); pv[nm.go]="深渊领主"
cl=at(root,"Close",TW-46,16,30,30); cg=image(cl,"button",kind="sliced",raycast=1); button(cl,cg,"combat.target.close",0); text(fill(cl,"X"),"×",20,LIGHT,"MC",bold=True)
sb=at(root,"Subtitle",148,50,276,20); b.text("target.subtitle",text(sb,"",13,MUTED,"ML")); pv[sb.go]="第 10 波 · 近战追击"
image(at(root,"Divider",148,74,276,4),"divider",kind="sliced")
bar(root,b,"Health",148,84,276,26,(0.8,0.17,0.11,1),"target.health","target.healthText",14,amount=0.64)
pv[root.children[-1].children[-1].go]="3,840 / 6,000"
inr=on(b.toggle("target.inRange",at(root,"In Range",148,114,276,20,active=0)),True); text(fill(inr,"Text"),"● 在你的攻击距离内",12,(0.55,1,0.5,1),"ML")
out=on(b.toggle("target.inRange",at(root,"Out Of Range",148,114,276,20),invert=1),False); text(fill(out,"Text"),"○ 超出你的攻击距离",12,MUTED,"ML")
st=at(root,"Stats",12,156,TW-24,100); image(st,"hud_plate",kind="sliced")
cells=(("攻击力","stat_damage","target.damage","58"),("攻击间隔","stat_interval","target.attackRate","1.30 秒"),
       ("攻击距离","stat_range","target.range","2.8 米"),("移动速度","stat_speed","target.move","1.6 (-30%)"),
       ("击杀经验","stat_xp","target.xp","180"),("距离","stat_distance","target.distance","8.4 米"))
for i,(label,icon,key,sample) in enumerate(cells):
    cell=at(st,"Stat "+label,10+(i%2)*208,8+(i//2)*30,198,28)
    image(at(cell,"Icon",0,2,24,24),icon)
    text(at(cell,"Label",30,0,80,28),label,13,MUTED,"ML")
    v=at(cell,"Value",88,0,106,28); b.text(key,text(v,"",15,LIGHT,"MR",bold=True)); pv[v.go]=sample
atk=at(root,"Attack",12,264,TW-24,66); image(atk,"hud_plate",kind="sliced")
sl=at(atk,"Icon Slot",8,8,50,50); image(sl,"slot_back"); b.image("target.attackIcon",raw(fill(sl,"Icon",4,4,4,4),preview="深渊横扫")); image(fill(sl,"Frame"),"slot_frame",kind="sliced")
an=at(atk,"Name",68,8,200,26); b.text("target.attackName",text(an,"",15,GOLD,"ML",bold=True)); pv[an.go]="深渊横扫"
ad=at(atk,"Detail",68,34,330,22); b.text("target.attackDetail",text(ad,"",12,MUTED,"ML")); pv[ad.go]="被动 · 近战 · 前摇 0.45 秒 · 后摇 0.50 秒"
ast=at(atk,"Status",TW-24-128,8,118,26); b.text("target.attackStatus",text(ast,"",13,CYAN,"MR",bold=True)); pv[ast.go]="前摇中"
tr=at(root,"Trait",18,338,TW-36,24); b.text("target.trait",text(tr,"",13,(1,0.84,0.55,1),"ML",bold=True)); pv[tr.go]="近战追击：贴身后用横扫攻击"
chips=(("冰晶减速 2.1 秒",True),("扇击减速 1.4 秒",True),("",False),("",False))
for i,(sample,shown) in enumerate(chips):
    ch=on(b.toggle("target.status%d"%i,at(root,"Status %d"%i,16+i*104,368,98,24,active=0)),shown); image(ch,"chip",(0.5,0.78,1,1),kind="sliced")
    ct=fill(ch,"Text",4,0,4,0); b.text("target.statusText%d"%i,text(ct,"",12,FROST,"MC",bold=True)); pv[ct.go]=sample
ns=on(b.toggle("target.noStatus",at(root,"No Status",18,368,300,24,active=0)),False); text(fill(ns,"Text"),"无状态效果",12,MUTED,"ML")
ds=at(root,"Description",18,398,TW-36,22); b.text("target.description",text(ds,"",12,(0.72,0.76,0.8,1),"UL",wrap=True)); pv[ds.go]="首领：攻击距离更长的近战巨兽，生命极高。"
text(at(root,"Footer",18,TH-24,TW-36,18),"左键空地 / Esc 取消选择  ·  右键攻击",11,(0.5,0.55,0.6,1),"MR")
panel(root,"UI.Game.TargetInfo",b)
target=root; write("UI.Game.TargetInfo",root)
# ================= 技能悬停提示（动态，Dota 2 式） =================
# 悬停 QWER 或被动图标时显示；键名与 CombatHud.TooltipKeys 一一对应。UITooltip 按顺序堆叠启用的分段并定位到悬停图标上方。
b=Bindings()
KW,KP,KG=420,14,48; KI=KW-2*KP; FROSTK=(0.62,0.86,1,1); LABEL="#9AA7B4"
root=Node("UI.Game.AbilityTooltip",None,(0.5,0.5),(0.5,0.5),(0,0),(KW,560),(0.5,0),active=0)
image(fill(root,"Back"),"tooltip_frame",kind="sliced")
image(Node("Header Band",root,(0,1),(1,1),(0,0),(0,108),(0.5,1)),"tooltip_header")
def sec(name,h,active=1): return Node(name,root,(0,1),(1,1),(0,0),(-2*KP,h),(0.5,1),active)
head=sec("Header",72)
isl=at(head,"Icon Slot",0,0,72,72); image(isl,"slot_back")
kic=fill(isl,"Icon",5,5,5,5); b.image("tooltip.icon",raw(kic,preview="冰霜扇击")); image(fill(isl,"Frame"),"slot_frame",kind="sliced")
kn=at(head,"Name",84,0,KI-84-44,34); b.text("tooltip.name",text(kn,"",22,WHITE,"ML",bold=True)); pv[kn.go]="冰霜扇击"
khk=on(b.toggle("tooltip.hasHotkey",Node("Hotkey",head,(1,1),(1,1),(0,-3),(34,28),(1,1),active=0)))
image(khk,"hotkey",kind="sliced"); kht=fill(khk,"Key"); b.text("tooltip.hotkey",text(kht,"",15,GOLD,"MC",bold=True,shadow=False)); pv[kht.go]="W"
kk=at(head,"Kind",84,34,KI-84,20); b.text("tooltip.kind",text(kk,"",13,FROSTK,"ML",bold=True)); pv[kk.go]="普通技能"
klv=on(b.toggle("tooltip.hasLevel",at(head,"Level",84,54,KI-84,18,active=0)))
kl=at(klv,"Text",0,0,KI-84-96,18); b.text("tooltip.level",text(kl,"",12,MUTED,"ML")); pv[kl.go]="等级 2 / 4"
kpips=Node("Pips",klv,(1,0.5),(1,0.5),(0,0),(88,9),(1,0.5))
for p_ in range(4):
    sl=on(b.toggle("tooltip.pipSlot%d"%p_,at(kpips,"Slot %d"%p_,p_*22,0,20,9,active=1)),True); image(sl,"pip_off")
    lit=on(b.toggle("tooltip.pip%d"%p_,at(sl,"Lit",0,0,20,9,active=0)),p_<2); image(lit,"pip_on")
kps=on(b.toggle("tooltip.passive",at(head,"Passive",84,54,84,20,active=0)),False); image(kps,"badge",(0.62,0.46,0.16,1),kind="sliced")
text(fill(kps,"Text"),"被动技能",12,WHITE,"MC",bold=True)
kdv=sec("Divider",4); image(kdv,"divider",kind="sliced")
kpr=sec("Properties",20); b.text("tooltip.properties",text(kpr,"",14,LIGHT,"UL",wrap=True,shadow=False))
pv[kpr.go]="<color=%s>技能：</color>指向方向\n<color=%s>影响：</color>敌方单位\n<color=%s>效果：</color>伤害、减速\n<color=%s>施法距离：</color>22 米"%((LABEL,)*4)
kds=on(b.toggle("tooltip.hasDescription",sec("Description",20,active=0)))
b.text("tooltip.description",text(kds,"",15,(0.87,0.9,0.93,1),"UL",wrap=True,spacing=1.05)); pv[kds.go]="向指定方向射出扇形冰霜箭雨，对扇形内的所有敌人造成伤害并施加扇击减速。"
kvl=on(b.toggle("tooltip.hasValues",sec("Values",40,active=0))); image(kvl,"tooltip_block",kind="sliced")
kvt=fill(kvl,"Text",14,9,10,9); b.text("tooltip.values",text(kvt,"",14,LIGHT,"UL",wrap=True,shadow=False))
pv[kvt.go]=("<color={L}>扇形：</color>长 <color=#FFFFFF><b>22</b></color> 米 · 角度 <color=#FFFFFF><b>70</b></color>°\n"
  "<color={L}>伤害：</color><color=#7D8892>40</color><color=#7D8892> / </color><color=#FFFFFF><b>76</b></color><color=#7D8892> / </color><color=#7CFF6B>112</color><color=#7D8892> / </color><color=#7D8892>148</color>\n"
  "<color={L}>扇击减速：</color>移动速度 -<color=#7D8892>35</color><color=#7D8892> / </color><color=#FFFFFF><b>42</b></color><color=#7D8892> / </color><color=#7CFF6B>49</color><color=#7D8892> / </color><color=#7D8892>56</color>% · 持续 <color=#FFFFFF><b>2.5</b></color> 秒").format(L=LABEL)
knt=on(b.toggle("tooltip.hasNotes",sec("Notes",20,active=0))); b.text("tooltip.notes",text(knt,"",12,(0.6,0.66,0.72,1),"UL",wrap=True,shadow=False))
pv[knt.go]="<color=%s>学习门槛：</color>英雄等级 <color=#7D8892>1</color><color=#7D8892> / </color><color=#FFFFFF><b>3</b></color><color=#7D8892> / </color><color=#7CFF6B>5</color><color=#7D8892> / </color><color=#7D8892>7</color>"%LABEL
kcd=on(b.toggle("tooltip.hasCost",sec("Cost Divider",4,active=0))); image(kcd,"divider",kind="sliced")
def cost(name,key,toggle,icon,sample):
    n_=on(b.toggle(toggle,sec(name,24,active=0))); image(at(n_,"Icon",0,0,22,22),icon)
    t_=fill(n_,"Text",30,1,0,1); b.text(key,text(t_,"",14,LIGHT,"UL",wrap=True,shadow=False)); pv[t_.go]=sample; return n_
kco=cost("Cooldown","tooltip.cooldown","tooltip.hasCooldown","tt_cooldown",
  "<color=%s>冷却：</color><color=#7D8892>5</color><color=#7D8892> / </color><color=#FFFFFF><b>4.6</b></color><color=#7D8892> / </color><color=#7CFF6B>4.2</color><color=#7D8892> / </color><color=#7D8892>3.8</color> 秒"%LABEL)
kmn=cost("Mana","tooltip.mana","tooltip.hasMana","tt_mana",
  "<color=%s>法力：</color><color=#7D8892>25</color><color=#7D8892> / </color><color=#FFFFFF><b>30</b></color><color=#7D8892> / </color><color=#7CFF6B>35</color><color=#7D8892> / </color><color=#7D8892>40</color>"%LABEL)
kst=on(b.toggle("tooltip.hasStatus",sec("Status",20,active=0))); b.text("tooltip.status",text(kst,"",13,LIGHT,"UL",wrap=True,shadow=False))
pv[kst.go]="<color=#7CFF6B>就绪：按 W 瞄准，左键确认，右键 / Esc 取消</color>\n<color=#7CFF6B>可升级：按 Ctrl+W 升到 3 级</color>"
ksections=[head,kdv,kpr,kds,kvl,knt,kcd,kco,kmn,kst]
tooltip(root,ksections,padding=KP,spacing=8,gap=KG,margin=8)  # gap 留出技能上方“+”学习按钮的位置
panel(root,"UI.Game.AbilityTooltip",b,blocks=0)
def stack(root,sections,state,pad,spacing):
    """预览用：与 UITooltip.Layout 相同的堆叠规则，按示例文字估算分段与根节点高度。"""
    y=pad; first=True; W_=root.rect[3][0]
    def shown(n): return (n.active or state.get(n.go,False)) and not (n.go in state and not state[n.go])
    for n in sections:
        if not shown(n): continue
        if not first: y+=spacing
        first=False
        h=n.rect[3][1]
        wraps=[d for d in n.draw if d[0]=="text" and len(d)>7 and d[7]]
        if wraps: h=math.ceil(ui.text_height(n,W_-2*pad))
        else:
            for ch in n.children:
                amin,amax,pos,size,_p=ch.rect
                if amin[1]==0 and amax[1]==1 and any(d[0]=="text" and len(d)>7 and d[7] for d in ch.draw):
                    h=math.ceil(ui.text_height(ch,W_-2*pad+size[0]))-size[1]; break
        n.rect=((0,1),(1,1),(0,-y),(-2*pad,h),(0.5,1)); y+=h
    root.rect=(root.rect[0],root.rect[1],root.rect[2],(W_,max(2*pad,y+pad)),root.rect[4])
stack(root,ksections,tg,KP,8)
tip=root; write("UI.Game.AbilityTooltip",root)
# ================= 商店（动态，右下角；B 键或“商店”按钮开关） =================
# 键名与 CombatHud.ShopKeys 一一对应：左键卡片购买，悬停卡片显示装备提示（CombatHud.ShopHoverBase + 卡片序号），12 张一页。
ITEM_NAMES=["铁剑","迅捷手套","皮甲背心","速度之靴","法力宝石","回复戒指","侦察镜","吸血獠牙","幸运匕首","贤者之书","守护铠甲","专注护符"]
ITEM_PRICES=[350,400,350,450,400,350,500,600,650,500,900,800]
with extra("panel.shop"):
    b=Bindings()
    SW,SH=520,500
    root=Node("UI.Game.Shop",None,(1,0),(1,0),(-16,226),(SW,SH),(1,0),active=0)
    image(fill(root,"Back"),"hud_body",kind="sliced",raycast=1)
    drop(root,"combat.shop.drop",0)  # 把装备栏中的装备拖到商店面板上出售（CombatHud.ShopDropCommand）
    image(at(root,"Icon",16,12,36,36),"商店")
    text(at(root,"Title",60,12,120,36),"商店",18,GOLD,"ML",bold=True)
    image(at(root,"Gold Icon",292,19,22,22),"金币")
    sgd=at(root,"Gold",320,12,130,36); b.text("hero.gold",text(sgd,"",18,(1,0.86,0.4,1),"ML",bold=True)); pv[sgd.go]="820"
    cl=at(root,"Close",SW-52,12,36,36); cg=image(cl,"button",kind="sliced",raycast=1); button(cl,cg,"combat.shop",0); text(fill(cl,"X"),"×",22,LIGHT,"MC",bold=True)
    image(at(root,"Divider",16,56,SW-32,4),"divider",kind="sliced")
    for c in range(12):
        name_,price_=ITEM_NAMES[c],ITEM_PRICES[c]; poor_=price_>820; owned_=name_=="速度之靴"
        card=on(b.toggle("shop.c%d.visible"%c,at(root,"Card %d"%c,16+(c%4)*124,70+(c//4)*120,116,112,active=0)),True)
        bg_=image(card,"hud_plate",kind="sliced",raycast=1); button(card,bg_,"combat.shop.buy",c); hover(card,"combat.hover",100+c)
        og=on(b.toggle("shop.c%d.owned"%c,fill(card,"Owned Glow",-3,-3,-3,-3,active=0)),owned_); image(og,"highlight",(0.55,0.85,1,1),kind="sliced")
        isl_=at(card,"Icon Slot",28,6,60,60); image(isl_,"slot_back")
        ic=on(b.toggle("shop.c%d.hasIcon"%c,fill(isl_,"Icon",4,4,4,4,active=0)),True); b.image("shop.c%d.icon"%c,raw(ic,preview=EQUIP%name_))
        image(fill(isl_,"Frame"),"slot_frame",kind="sliced")
        cn=at(card,"Name",4,68,108,20); b.text("shop.c%d.name"%c,text(cn,"",13,LIGHT,"MC",bold=True)); pv[cn.go]=name_
        ok=on(b.toggle("shop.c%d.poor"%c,at(card,"Price",0,88,116,20),invert=1),not poor_)
        image(at(ok,"Coin",30,2,16,16),"金币"); pt_=at(ok,"Text",48,0,64,20); b.text("shop.c%d.price"%c,text(pt_,"",13,(1,0.86,0.4,1),"ML",bold=True)); pv[pt_.go]="{:,}".format(price_)
        bad=on(b.toggle("shop.c%d.poor"%c,at(card,"Price Poor",0,88,116,20,active=0)),poor_)
        image(at(bad,"Coin",30,2,16,16),"金币",(1,1,1,0.45)); pp_=at(bad,"Text",48,0,64,20); b.text("shop.c%d.price"%c,text(pp_,"",13,(1,0.45,0.38,1),"ML",bold=True)); pv[pp_.go]="{:,}".format(price_)
        ob=on(b.toggle("shop.c%d.owned"%c,at(card,"Owned",60,4,52,18,active=0)),owned_); image(ob,"chip",(0.5,0.78,1,1),kind="sliced"); text(fill(ob,"Text"),"已拥有",11,WHITE,"MC",bold=True)
    pg=on(b.toggle("shop.paged",at(root,"Pager",16,432,SW-32,30,active=0)),True)
    for nm_,x_,val_ in (("Previous",0,0),("Next",SW-32-84,1)):
        bt=at(pg,nm_,x_,0,84,30); g_=image(bt,"button",kind="sliced",raycast=1); button(bt,g_,"combat.shop.page",val_)
        text(fill(bt,"Label"),"上一页" if val_==0 else "下一页",13,LIGHT,"MC",bold=True)
    pn=at(pg,"Page",84,0,SW-32-168,30); b.text("shop.page",text(pn,"",14,LIGHT,"MC",bold=True)); pv[pn.go]="1 / 2"
    hn=at(root,"Hint",16,470,SW-32,22); b.text("shop.hint",text(hn,"",12,MUTED,"ML")); pv[hn.go]="左键购买  ·  右键装备或把装备拖到这里出售（返还 50%）  ·  B / Esc 关闭"
    panel(root,"UI.Game.Shop",b)
    shop=root; write("UI.Game.Shop",root)
# ================= 预览 =================
from PIL import Image as I, ImageDraw as D
W,H=1920,1080
bg=I.new("RGBA",(W,H),(46,60,52,255)); d=D.Draw(bg)
import random as R; R.seed(3)
for _ in range(900):
    x,y=R.uniform(0,W),R.uniform(0,H); r=R.uniform(5,11); c=R.choice([(150,40,30),(120,110,90),(90,150,60),(140,90,40)])
    d.ellipse((x-r,y-r,x+r,y+r),fill=c+(255,))
d.ellipse((W/2-14,H/2-14,W/2+14,H/2+14),fill=(120,200,255,255))
bg.save(P('bg.png'))
import ui as U
def composite(path,roots,state):
    cv=I.open(P('bg.png')).convert("RGBA"); cv.save(P('acc.png'))
    for r in roots:
        U.render(r,W,H,(0,0,W,H),state,P('acc.png'),bg=P('acc.png'))
    I.open(P('acc.png')).save(path)
state=dict(tg)
for n in (hint,talent,target): n.active=1
composite(P('hud_preview.png'),[bottom,title,controls,hint,talent,target],state)
talent.active=0; tg2=dict(tg)
composite(P('hud_preview2.png'),[bottom,title,controls,hint,target],tg2)
# 技能提示预览：悬停 W（2/4 级，有技能点，下一级以绿色标出）。位置与 UITooltip.Place 相同：图标上边缘中点上方 gap。
def place_tip(slot_center_x,slot_top_y):
    w_,h_=tip.rect[3]; x_=min(max(slot_center_x,8+w_/2),W-8-w_/2); y_=min(slot_top_y+KG,H-8-h_)
    tip.rect=((0.5,0.5),(0.5,0.5),(x_-W/2,y_-H/2),(w_,h_),(0.5,0))
tip.active=1; tg3=dict(tg2)
place_tip(W/2-782+438+116+102+46,214-2-44)
composite(P('tooltip_preview.png'),[bottom,title,controls,hint,tip],tg3)
# 被动提示预览：隐藏热键与等级，显示“被动”徽章。
for key_,val_ in ((khk,False),(klv,False),(kps,True),(kmn,False),(kco,False),(kcd,False)): tg3[key_.go]=val_
pv[kn.go]="冰箭普攻"; pv[kk.go]="被动 · 自动攻击"; kic.draw=[("raw","冰箭普攻",(1,1,1,1))]
pv[kpr.go]="<color=%s>技能：</color>被动（自动施放）\n<color=%s>影响：</color>敌方单位\n<color=%s>结算：</color>弹道追踪 · 60 米/秒"%((LABEL,)*3)
pv[kds.go]="被动：自动向攻击距离内的敌人射出追踪冰箭。命中计入普攻命中，可为冰霜专注叠层。"
pv[kvt.go]=("<color={L}>攻击力：</color><color=#FFFFFF><b>51</b></color>\n<color={L}>攻击间隔：</color><color=#FFFFFF><b>0.10</b></color> 秒（基础 0.12 秒 ÷ 攻速 ×1.25）\n"
  "<color={L}>每秒攻击：</color><color=#FFFFFF><b>10.4</b></color> 次 · 理论每秒伤害 <color=#FFFFFF><b>530</b></color>\n<color={L}>攻击距离：</color><color=#FFFFFF><b>20</b></color> 米（另加目标体型）").format(L=LABEL)
pv[knt.go]="移动中攻击：右键移动途中自动攻击攻击距离内的敌人，前摇不定身。\n攻击间隔、前摇与后摇都随攻击速度缩短。"
pv[kst.go]="<color=#7CFF6B>攻击中</color>"
ui.previews.update(pv); stack(tip,ksections,tg3,KP,8)
place_tip(W/2-782+438+26+38,214-2-52)
composite(P('tooltip_preview_passive.png'),[bottom,title,controls,hint,tip],tg3)
# 商店 + 装备提示预览：悬停第 4 张卡片（速度之靴，已拥有）。上方放不下时与 UITooltip.Place 相同改放左侧。
def place_side(card_left_x,card_center_y):
    w_,h_=tip.rect[3]; x_=card_left_x-8-w_/2; y_=min(max(card_center_y-h_/2,8),H-8-h_)
    tip.rect=((0.5,0.5),(0.5,0.5),(x_-W/2,y_-H/2),(w_,h_),(0.5,0))
tg4=dict(tg3); shop.active=1
for key_,val_ in ((khk,False),(klv,False),(kps,False),(kmn,False),(kco,False),(kcd,False),(kvl,False)): tg4[key_.go]=val_
pv[kn.go]="速度之靴"; pv[kk.go]="<color=#C8D0D8>普通装备</color> · <color=#F7D485>450 金币</color>"; kic.draw=[("raw",EQUIP%"速度之靴",(1,1,1,1))]
pv[kpr.go]="<color=#7CFF6B>+1.5 移动速度</color>"
pv[kds.go]="轻快的长靴，提高移动速度。同名装备只能持有一件。"
pv[knt.go]="唯一：同一件装备最多持有一件。\n出售返还 225 金币。"
pv[kst.go]="<color=#9AA7B4>已拥有（唯一装备）</color>"
ui.previews.update(pv); stack(tip,ksections,tg4,KP,8)
card_x=W-16-SW+16+3*124; card_top=SH-70+226  # 第 4 张卡片左边缘与上边缘（自下而上坐标）
if card_top+KG+tip.rect[3][1]>H-8: place_side(card_x,card_top-56)
else: place_tip(card_x+58,card_top)
composite(P('shop_preview.png'),[bottom,title,controls,shop,tip],tg4)
# 属性提示预览：悬停属性面板。
tg5=dict(tg3)
for key_,val_ in ((khk,False),(klv,False),(kps,False),(kmn,False),(kco,False),(kcd,False),(kvl,True),(kds,False)): tg5[key_.go]=val_
pv[kn.go]="英雄属性"; pv[kk.go]="寒冰射手 · 等级 7"; kic.draw=[("raw","寒冰射手",(1,1,1,1))]
pv[kpr.go]="<color={L}>合计：</color>基础 + 成长 ×（等级 - 1）+ 天赋 + 装备\n<color={L}>规则：</color>所有来源直接相加，不做任何换算".format(L=LABEL)
def row_(name,val,src): return "<color={L}>{n}：</color><color=#FFFFFF><b>{v}</b></color><color={L}><size=12>（{s}）</size></color>".format(L=LABEL,n=name,v=val,s=src) if src else "<color={L}>{n}：</color><color=#FFFFFF><b>{v}</b></color>".format(L=LABEL,n=name,v=val)
pv[kvt.go]="\n".join([row_("最大生命","582","基础 300 · 成长 +132 · 装备 +150"),row_("生命回复","6.6/秒","基础 2 · 成长 +0.6 · 装备 +4"),row_("最大法力","172","基础 100 · 成长 +72"),
  row_("法力回复","1.5/秒",""),row_("攻击力","60","基础 30 · 成长 +18 · 装备 +12"),row_("攻击速度","30%","成长 +15% · 装备 +15%"),row_("攻击距离","20 米",""),
  row_("暴击几率","0%",""),row_("暴击伤害","150%",""),row_("普攻吸血","0%",""),row_("技能伤害","0%",""),row_("冷却缩减","0%",""),row_("伤害减免","0%",""),
  row_("移动速度","11.5 米/秒","基础 10 · 装备 +1.5"),row_("经验获取","0%","")])
pv[knt.go]="百分比类属性按百分点直接相加，例如 +20% 与 +30% 攻击速度合计 +50%。\n冷却缩减与伤害减免最高 80%；暴击只作用于普攻，暴击伤害是暴击时的伤害倍率。\n技能伤害只加成技能；普攻吸血按普攻实际造成的伤害回复生命。"
pv[kst.go]="<color={L}>当前（含状态效果）：</color>攻击力 <color=#FFFFFF><b>60</b></color> · 每秒攻击 <color=#FFFFFF><b>10.8</b></color> 次 · 移速 <color=#FFFFFF><b>11.5</b></color>".format(L=LABEL)
ui.previews.update(pv); stack(tip,ksections,tg5,KP,8)
place_tip(W/2-782+192+118,214-44)
composite(P('stats_preview.png'),[bottom,title,controls,hint,tip],tg5)
print("ok")
