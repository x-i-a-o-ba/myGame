import sys, os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
PREVIEW=os.path.join(os.path.dirname(os.path.abspath(__file__)),'preview'); os.makedirs(PREVIEW,exist_ok=True)
def P(name): return os.path.join(PREVIEW,name)
import ui
from ui import *
GOLD=(0.97,0.83,0.52,1); LIGHT=(0.9,0.93,0.95,1); MUTED=(0.6,0.66,0.7,1); CYAN=(0.62,0.9,1,1); WHITE=(1,1,1,1)
HP=(0.27,0.8,0.24,1); MP=(0.22,0.48,0.98,1); RED=(0.88,0.2,0.14,1)
pv=ui.previews; tg={}; ui.fill_preview.clear()
def on(node,value=True): tg[node.go]=value; return node
def bar(parent,b,name,x,y,w,h,color,fillkey,textkey,textsize,regenkey=None,amount=1.0):
    back=at(parent,name,x,y,w,h); image(back,"bar_back",kind="sliced")
    inner=fill(back,"Inner",2,2,2,2)
    f=fill(inner,"Fill"); f.rect=((0,0),(1,1),(0,0),(0,0),(0,0.5))
    b.fill(fillkey,image(f,"bar_fill",color,kind="sliced"),0); ui.hfill[f.go]=amount
    t=fill(back,"Value"); b.text(textkey,text(t,"",textsize,WHITE,"MC",bold=True)); return back,t
# ================= 底部 HUD =================
b=Bindings()
root=Node("UI.Game.BottomHUD",None,(0.5,0),(0.5,0),(0,0),(1240,214),(0.5,0),go=7807990809045325555,rt=7026121659916554133)
body=at(root,"Body",0,32,1240,182); image(body,"hud_body",kind="sliced",raycast=1)
center=at(root,"Ability Section",330,2,580,212); image(center,"hud_body",kind="sliced",raycast=1)
# ---- 左侧：头像、等级、属性 ----
plate=at(root,"Portrait",14,44,168,158); image(plate,"hud_plate",kind="sliced")
por=fill(plate,"Image",3,3,3,3); b.image("hero.portrait",raw(por,preview="寒冰射手"))
shade=at(plate,"Name Shade",3,112,162,43); image(shade,"shade_bottom",kind="simple")
nm=at(plate,"HeroName",4,124,160,28); b.text("hero.name",text(nm,"寒冰射手",17,WHITE,"MC",bold=True)); pv[nm.go]="寒冰射手"
xpt=at(plate,"Experience",58,6,104,18); b.text("hero.xpText",text(xpt,"",12,LIGHT,"MR")); pv[xpt.go]="经验 120 / 240"
stats=at(root,"Stats",192,44,128,158); image(stats,"hud_plate",kind="sliced")
for i,(label,key,sample) in enumerate((("攻击","stats.attack","51"),("攻速","stats.attackSpeed","9.4/秒"),("射程","stats.range","20"),("移速","stats.move","10"))):
    row=at(stats,"Stat "+label,8,8+i*28,112,26)
    text(at(row,"Label",0,0,44,26),label,13,MUTED,"ML")
    v=at(row,"Value",40,0,72,26); b.text(key,text(v,"",15,LIGHT,"MR",bold=True)); pv[v.go]=sample
badge=at(root,"Level Badge",2,26,62,62); image(badge,"level_badge")
ringb=fill(badge,"Ring Back",-3,-3,-3,-3); image(ringb,"ring",(0,0,0,0.65))
ring=fill(badge,"Ring",-3,-3,-3,-3); b.fill("hero.xp",image(ring,"ring",(1,0.8,0.28,1),kind="filled",amount=0.0,origin=0),2); ui.fill_preview[ring.go]=0.5
lv=fill(badge,"Level"); b.text("hero.level",text(lv,"1",24,GOLD,"MC",bold=True,outline=True)); pv[lv.go]="7"
pts=on(b.toggle("hero.pointsVisible",at(stats,"Skill Points",4,122,120,30,active=0)))
image(at(pts,"Glow",-6,-6,132,42),"glow",(1,0.78,0.25,0.55)); image(fill(pts,"Back"),"hud_plate",kind="sliced")
pt=fill(pts,"Text"); b.text("hero.points",text(pt,"",14,(1,0.86,0.4,1),"MC",bold=True)); pv[pt.go]="技能点 ×2"
# ---- 中央：被动 + QWER ----
def slot(parent,name,x,y,size,key=None):
    s=at(parent,name,x,y,size,size)
    back=image(s,"slot_back",raycast=1)
    icon=fill(s,"Icon",7,7,7,7)
    return s,back,icon
ps,pback,picon=slot(center,"Passive",26,52,76)
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
    button(s,back,"combat.ability",i)
    b.image("ability.%s.icon"%k,raw(icon,preview=name))
    nm_sh=at(s,"Name Shade",7,60,78,25); image(nm_sh,"shade_bottom")
    n2=at(s,"Name",7,63,78,20); b.text("ability.%s.name"%k,text(n2,"",12,LIGHT,"MC")); pv[n2.go]=name
    nomana=b.toggle("ability.%s.noMana"%k,fill(s,"No Mana",7,7,7,7,active=0)); image(nomana,"white",(0.1,0.2,0.65,0.5))
    locked=b.toggle("ability.%s.locked"%k,fill(s,"Locked",7,7,7,7,active=0)); image(locked,"white",(0.02,0.03,0.04,0.72)); on(locked,level==0)
    cooling=b.toggle("ability.%s.cooling"%k,fill(s,"Cooldown",7,7,7,7,active=0)); on(cooling,cd>0)
    b.fill("ability.%s.cd"%k,image(cooling,"white",(0,0,0,0.7),kind="filled",amount=1,origin=2,clockwise=0),2); ui.fill_preview[cooling.go]=cd
    ct=fill(s,"Cooldown Text"); b.text("ability.%s.cdText"%k,text(ct,"",28,WHITE,"MC",bold=True,outline=True)); pv[ct.go]=cdt
    image(fill(s,"Frame"),"slot_frame",kind="sliced")
    hk=at(s,"Hotkey",4,4,22,22); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),k.upper(),14,WHITE,"MC",bold=True,shadow=False)
    st=at(s,"Status",28,6,58,18); b.text("ability.%s.status"%k,text(st,"",12,CYAN,"MR",bold=True)); pv[st.go]=status
    mn=at(s,"Mana",44,44,42,16); b.text("ability.%s.mana"%k,text(mn,"",12,(0.55,0.75,1,1),"MR",bold=True))
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
pv[center.children[-1].children[-1].go]="172 / 172"
# ---- 右侧：指令、天赋树 ----
rp=at(root,"Orders",920,44,306,158); image(rp,"hud_plate",kind="sliced")
for i,(label,hot) in enumerate((("攻击","A"),("停止","S"),("待命","H"))):
    bt=at(rp,"Order "+hot,10+i*98,10,92,40); g=image(bt,"button",kind="sliced",raycast=1); button(bt,g,"combat.order",i)
    if i==0: armed=on(b.toggle("order.attackArmed",fill(bt,"Armed",-4,-4,-4,-4,active=0)),True); image(armed,"highlight",(1,0.45,0.3,1),kind="sliced")
    text(at(bt,"Label",8,0,56,40),label,15,LIGHT,"MC",bold=True)
    hk=at(bt,"Hotkey",62,10,22,20); image(hk,"hotkey",kind="sliced"); text(fill(hk,"Key"),hot,12,GOLD,"MC",bold=True,shadow=False)
oc=at(rp,"Current Order",12,54,282,22); b.text("order.current",text(oc,"",13,MUTED,"ML")); pv[oc.go]="指令：攻击移动"
tt=at(rp,"Talent Tree",10,80,286,44); tg_img=image(tt,"button",kind="sliced",raycast=1); button(tt,tg_img,"combat.talentTree",0)
pend=on(b.toggle("hud.talentPending",fill(tt,"Pending Glow",-6,-6,-6,-6,active=0)),True); image(pend,"highlight",(1,0.8,0.3,1),kind="sliced")
image(at(tt,"Icon",6,4,36,36),"天赋树")
text(at(tt,"Label",48,0,150,44),"天赋树",16,GOLD,"ML",bold=True)
pl=on(b.toggle("hud.talentPending",at(tt,"Pending",190,0,90,44,active=0)),True); text(fill(pl,"Text"),"可选择！",13,(1,0.86,0.4,1),"MR",bold=True)
text(at(rp,"Help",12,128,282,24),"右键 移动/攻击  ·  A 攻击移动  ·  S 停止  ·  H 待命",11,MUTED,"ML")
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
root=Node("UI.Game.Controls",None,(1,1),(1,1),(-16,-14),(300,74),(1,1),go=7198065516410080699,rt=5117782400737888598)
image(fill(root,"Back"),"hud_body",kind="sliced",raycast=1)
fp=at(root,"FPS",14,8,130,26); b.text("top.fps",text(fp,"",16,(0.6,1,0.6,1),"ML",bold=True)); pv[fp.go]="60 FPS"
pj=at(root,"Projectiles",150,8,136,26); b.text("top.projectiles",text(pj,"",13,MUTED,"MR")); pv[pj.go]="弹体 1,204"
text(at(root,"Help",14,38,272,28),"F1 帮助  ·  F5 重开  ·  滚轮缩放视野",12,MUTED,"ML")
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
plate=at(root,"Portrait",18,18,116,116); image(plate,"hud_plate",kind="sliced")
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
print("ok")
