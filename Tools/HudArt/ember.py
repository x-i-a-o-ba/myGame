"""2026-10-23 第二战场“熔火裂谷”的敌人资源：8 张 192×192 怪物头像、8 张 128×128 普攻图标、8 个自动攻击模块、
8 个怪物材质与预制体、“熔火弹体”敌方弹体材质和“熔火名册”。输出名称→GUID 表写入 ember.json。
不修改 enemies.py / icons.py 生成的资源；重跑会覆盖本脚本的输出（在 Unity 里手工调整过后不要再运行）。"""
import os, sys, json, math, uuid, re, random
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import cairo
from common import ROOT, load_guids, save_guids, guid_for, folder_meta, sprite_meta, surface, hexc, lin, rad, save
from ui import us
HERE=os.path.dirname(os.path.abspath(__file__))
g=load_guids()
random.seed(2310)

def write(path,text):
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True)
    open(full,"w",newline="\n").write(text)
def native_meta(path,main=11400000):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: %d\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%(guid_for(path,g),main))
def prefab_meta(path):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))
def script_guid(path):
    return re.search(r"guid: (\w+)",open(os.path.join(ROOT,path)+".meta").read()).group(1)
SCRIPTS={
 "AutoAttackDefinition":script_guid("Assets/Game/Abilities/Core/AutoAttack/AutoAttackDefinition.cs"),
 "EnemyArchetype":script_guid("Assets/Game/Runtime/Combat/Enemies/EnemyArchetype.cs"),
 "EnemyRoster":script_guid("Assets/Game/Runtime/Combat/Enemies/EnemyRoster.cs"),
}

# ---------- 图标（与 icons.py 同风格的绘图辅助） ----------
ICON_DIR="Assets/Game/UI/Icons"; icons={}
def emit(name,s):
    path="%s/%s.png"%(ICON_DIR,name)
    if os.path.exists(os.path.join(ROOT,path)) and path not in g: raise SystemExit("图标重名，会覆盖其他脚本的资源："+path)
    save(s,path); sprite_meta(path,g); icons[name]=g[path]
def bg(c,n,inner,outer,cx=0.5,cy=0.42):
    c.rectangle(0,0,n,n); c.set_source(rad(n*cx,n*cy,n*0.02,n*0.78,[(0,inner),(1,outer)])); c.fill()
    for _ in range(int(n*1.2)):
        x,y=random.random()*n,random.random()*n; c.rectangle(x,y,1,1); c.set_source_rgba(1,1,1,random.random()*0.05); c.fill()
def vignette(c,n,a=0.75):
    c.rectangle(0,0,n,n); c.set_source(rad(n/2,n/2,n*0.36,n*0.75,[(0,(0,0,0,0)),(1,(0,0,0,a))])); c.fill()
def glow(c,x,y,r,col,a=0.8):
    c.arc(x,y,r,0,2*math.pi); c.set_source(rad(x,y,0,r,[(0,col[:3]+(a,)),(1,col[:3]+(0,))])); c.fill()
def poly(c,pts):
    c.new_path(); c.move_to(*pts[0])
    for p in pts[1:]: c.line_to(*p)
    c.close_path()
def crystal(c,pts,light,dark,edge=(1,1,1,0.85),lw=1.2):
    xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
    poly(c,pts); c.set_source(lin(min(xs),min(ys),max(xs),max(ys),[(0,light),(1,dark)])); c.fill_preserve()
    c.set_source_rgba(*edge); c.set_line_width(lw); c.stroke()
def ell(c,cx,cy,rx,ry):
    c.save(); c.translate(cx,cy); c.scale(rx,ry); c.arc(0,0,1,0,2*math.pi); c.restore()
def path(c,pts,close=True):
    c.new_path(); c.move_to(*pts[0])
    for p in pts[1:]:
        if len(p)==6: c.curve_to(*p)
        else: c.line_to(*p)
    if close: c.close_path()
def eye(c,x,y,r,col,squint=0.45,core=(1,1,0.85,1)):
    glow(c,x,y,r*2.6,col,0.85)
    c.save(); c.translate(x,y); c.scale(1,squint); c.arc(0,0,r,0,2*math.pi); c.restore(); c.set_source_rgba(*core); c.fill()
P=192
def specks(c,n,col,rmin=0.8,rmax=2.2,top=0):
    for _ in range(n):
        x,y=random.uniform(0,P),random.uniform(top,P); c.arc(x,y,random.uniform(rmin,rmax),0,2*math.pi); c.set_source_rgba(*col[:3],random.uniform(0.15,0.6)); c.fill()
def embers(c,n,size=P,top=0):
    for _ in range(n):
        x,y=random.uniform(0,size),random.uniform(top,size); r=random.uniform(0.8,2.4)
        c.arc(x,y,r,0,2*math.pi); c.set_source_rgba(1,random.uniform(0.45,0.85),0.15,random.uniform(0.3,0.9)); c.fill()
def shoulders(c,top,stops):
    path(c,[(6,P),(14,top+30,40,top,96,top-4),(152,top,178,top+30,186,P)]); c.set_source(lin(0,top-4,0,P,stops)); c.fill()
def fangs(c,x0,x1,y,h,n,col=(0.96,0.93,0.82,1),down=True):
    w=(x1-x0)/n
    for k in range(n):
        x=x0+k*w; d=h*(0.7+0.3*((k%2)==0))
        path(c,[(x,y),(x+w/2,y+(d if down else -d)),(x+w,y)]); c.set_source_rgba(*col); c.fill()
def cracks(c,segments,w=4):
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    for a,b in segments:
        c.move_to(*a); c.line_to(*b); c.set_line_width(w); c.set_source_rgba(1,0.45,0.1,0.35); c.stroke()
        c.move_to(*a); c.line_to(*b); c.set_line_width(w*0.4); c.set_source_rgba(1,0.8,0.35,0.95); c.stroke()
def flame(c,x,y,w,h,inner=(1,0.95,0.6,1),outer=(1,0.35,0.05,0.9)):
    path(c,[(x-w/2,y),(x-w/2,y-h*0.45,x-w*0.1,y-h*0.6,x,y-h),(x+w*0.1,y-h*0.6,x+w/2,y-h*0.45,x+w/2,y),(x+w*0.3,y+w*0.35,x-w*0.3,y+w*0.35,x-w/2,y)])
    c.set_source(lin(x,y-h,x,y,[(0,outer),(1,inner)])); c.fill()

# 怪物头像 192（文件名 = 怪物显示名）
# 余烬小鬼：火红小恶魔，短角、黄色大眼、咧嘴獠牙，周身火星
s,c=surface(P,P); bg(c,P,hexc('#a8341a'),hexc('#1a0503'),0.5,0.4); embers(c,40)
shoulders(c,156,[(0,hexc('#6a1c0c')),(1,hexc('#180503'))])
for sgn in (-1,1):
    path(c,[(96+sgn*30,66),(96+sgn*46,34,96+sgn*58,24,96+sgn*66,8),(96+sgn*60,36,96+sgn*54,56,96+sgn*46,78)]); c.set_source(lin(0,8,0,78,[(0,hexc('#2a1a14')),(1,hexc('#6a3a24'))])); c.fill()
    path(c,[(96+sgn*48,92),(96+sgn*84,70),(96+sgn*60,108)]); c.set_source(lin(0,70,0,108,[(0,hexc('#e0582a')),(1,hexc('#7a2010'))])); c.fill()
path(c,[(96,50),(136,50,152,82,148,110),(144,138,124,160,96,164),(68,160,48,138,44,110),(40,82,56,50,96,50)])
c.set_source(rad(90,94,8,84,[(0,hexc('#ff8a4a')),(0.55,hexc('#c83c1a')),(1,hexc('#4a0e06'))])); c.fill()
for sgn in (-1,1):
    ell(c,96+sgn*24,100,15,12); c.set_source_rgba(0.12,0.02,0,1); c.fill(); eye(c,96+sgn*24,100,8,(1,0.85,0.2),0.75,(1,1,0.7,1))
    c.arc(96+sgn*24,100,2.6,0,2*math.pi); c.set_source_rgba(0.15,0.03,0,1); c.fill()
path(c,[(66,128),(84,140,108,140,126,128),(118,152,74,152,66,128)]); c.set_source_rgba(0.12,0.02,0.01,1); c.fill()
glow(c,96,140,18,(1,0.6,0.1),0.6)
fangs(c,70,122,128,9,8)
vignette(c,P,0.55); emit("余烬小鬼",s)
# 熔岩蜥蜴：玄武岩鳞片的蜥蜴头，熔岩裂纹、竖瞳、分叉舌
s,c=surface(P,P); bg(c,P,hexc('#7a2a10'),hexc('#120402'),0.5,0.35); embers(c,26)
shoulders(c,160,[(0,hexc('#3a1a10')),(1,hexc('#0e0604'))])
path(c,[(96,36),(132,36,150,66,148,96),(146,122,130,140,120,168),(112,186,80,186,72,168),(62,140,46,122,44,96),(42,66,60,36,96,36)])
c.set_source(rad(96,80,8,96,[(0,hexc('#5a3a30')),(0.6,hexc('#2e1c18')),(1,hexc('#0e0806'))])); c.fill()
for k in range(5):
    for j in range(4):
        x=62+j*22+(k%2)*11; y=52+k*20; ell(c,x,y,9,6); c.set_source_rgba(0.6,0.4,0.32,0.18); c.fill()
cracks(c,(((72,50),(86,72)),((86,72),(80,96)),((120,48),(108,74)),((108,74),(116,98)),((90,130),(96,150)),((96,150),(104,170))),3.5)
for sgn in (-1,1):
    ell(c,96+sgn*30,92,13,10); c.set_source_rgba(0.05,0.02,0.01,1); c.fill(); eye(c,96+sgn*30,92,8,(1,0.75,0.1),0.7,(1,0.9,0.3,1))
    c.save(); c.translate(96+sgn*30,92); c.scale(0.35,1); c.arc(0,0,6,0,2*math.pi); c.restore(); c.set_source_rgba(0.08,0.02,0,1); c.fill()
for sgn in (-1,1): ell(c,96+sgn*10,154,3,4); c.set_source_rgba(0.05,0.02,0.01,1); c.fill()
c.set_line_cap(cairo.LINE_CAP_ROUND)
c.move_to(96,178); c.line_to(96,190); c.set_line_width(3); c.set_source_rgba(1,0.4,0.2,1); c.stroke()
vignette(c,P,0.55); emit("熔岩蜥蜴",s)
# 火焰术士：深红兜帽下的阴影面孔，双眼发光，手托火球
s,c=surface(P,P); bg(c,P,hexc('#7a1a14'),hexc('#140303'),0.5,0.3); embers(c,30)
shoulders(c,148,[(0,hexc('#5a1210')),(1,hexc('#160404'))])
path(c,[(96,14),(150,20,166,92,158,150),(140,170,52,170,34,150),(26,92,42,20,96,14)]); c.set_source(lin(40,14,160,160,[(0,hexc('#a8241c')),(1,hexc('#2a0606'))])); c.fill()
path(c,[(96,44),(128,46,138,80,134,110),(128,132,64,132,58,110),(54,80,64,46,96,44)]); c.set_source(rad(96,96,4,60,[(0,hexc('#2a0a06')),(1,hexc('#080202'))])); c.fill()
for sgn in (-1,1): eye(c,96+sgn*16,92,4.5,(1,0.55,0.1),0.55,(1,0.9,0.5,1))
glow(c,96,160,44,(1,0.5,0.1),0.9); c.arc(96,160,18,0,2*math.pi); c.set_source(rad(90,154,2,20,[(0,(1,1,0.85,1)),(1,(1,0.45,0.05,1))])); c.fill()
for k,(dx,h) in enumerate(((-10,26),(0,36),(10,24))): flame(c,96+dx,150,12,h)
vignette(c,P,0.55); emit("火焰术士",s)
# 熔岩犀牛：正面犀牛头，熔岩发光的巨角尖、厚皮裂纹、小红眼
s,c=surface(P,P); bg(c,P,hexc('#6a2a14'),hexc('#120503'),0.5,0.35); embers(c,24)
shoulders(c,140,[(0,hexc('#3a2018')),(1,hexc('#0e0604'))])
for sgn in (-1,1):
    path(c,[(96+sgn*44,58),(96+sgn*66,40),(96+sgn*70,72)]); c.set_source(lin(0,40,0,72,[(0,hexc('#6a4a3a')),(1,hexc('#2a1a12'))])); c.fill()
path(c,[(96,52),(134,52,152,80,150,110),(148,140,128,160,112,176),(104,186,88,186,80,176),(64,160,44,140,42,110),(40,80,58,52,96,52)])
c.set_source(rad(96,96,8,92,[(0,hexc('#7a5444')),(0.6,hexc('#4a2e24')),(1,hexc('#160a06'))])); c.fill()
cracks(c,(((60,96),(74,110)),((74,110),(70,128)),((132,96),(118,112)),((118,112),(124,130))),3.2)
path(c,[(84,120),(96,24),(108,120)]); c.set_source(lin(0,24,0,120,[(0,hexc('#fff0b0')),(0.25,hexc('#ff8a2a')),(0.6,hexc('#8a4a2a')),(1,hexc('#4a2a1c'))])); c.fill()
glow(c,96,30,22,(1,0.6,0.1),0.95)
path(c,[(86,150),(96,128),(106,150)]); c.set_source(lin(0,128,0,150,[(0,hexc('#d08a4a')),(1,hexc('#5a3020'))])); c.fill()
for sgn in (-1,1): eye(c,96+sgn*36,104,4,(1,0.2,0.05),0.6,(1,0.7,0.4,1))
for sgn in (-1,1): ell(c,96+sgn*14,168,6,7); c.set_source_rgba(0.08,0.03,0.02,1); c.fill()
vignette(c,P,0.55); emit("熔岩犀牛",s)
# 爆燃火灵：明亮火核与翻卷火舌，暗色凶眼
s,c=surface(P,P); bg(c,P,hexc('#8a3a0a'),hexc('#140502'),0.5,0.5); embers(c,50)
glow(c,96,108,92,(1,0.55,0.1),0.6)
for k in range(9):
    a=-math.pi/2+(k-4)*0.32; x=96+math.cos(a)*44; y=108+math.sin(a)*44
    c.save(); c.translate(x,y); c.rotate(a+math.pi/2); flame(c,0,6,26,46+random.uniform(-8,10)); c.restore()
ell(c,96,112,56,54); c.set_source(rad(88,98,6,60,[(0,hexc('#fffbe0')),(0.35,hexc('#ffd04a')),(0.75,hexc('#ff7a1a')),(1,hexc('#a82a06'))])); c.fill()
for sgn in (-1,1):
    path(c,[(96+sgn*8,104),(96+sgn*36,92),(96+sgn*30,112)]); c.set_source_rgba(0.35,0.06,0,1); c.fill()
path(c,[(74,132),(86,140,106,140,118,132),(110,150,82,150,74,132)]); c.set_source_rgba(0.4,0.08,0,1); c.fill()
vignette(c,P,0.5); emit("爆燃火灵",s)
# 黑曜石巨像：棱角分明的黑曜石头颅，紫色反光与熔岩缝隙，发光双眼
s,c=surface(P,P); bg(c,P,hexc('#3a2a4a'),hexc('#08060c'),0.5,0.32); specks(c,20,(1,0.6,0.3))
crystal(c,[(8,192),(20,150),(60,132),(132,132),(172,150),(184,192)],(0.3,0.26,0.36,1),(0.08,0.06,0.1,1),(0.6,0.5,0.8,0.6),2)
crystal(c,[(56,36),(96,16),(136,36),(158,80),(148,130),(118,162),(74,162),(44,130),(34,80)],(0.46,0.4,0.58,1),(0.1,0.08,0.14,1),(0.75,0.65,0.95,0.7),2.5)
for pts in (((56,36),(84,64),(34,80)),((136,36),(108,64),(158,80)),((44,130),(74,118),(74,162)),((148,130),(118,118),(118,162))):
    poly(c,pts); c.set_source_rgba(0,0,0,0.3); c.fill()
for pts in (((96,16),(110,40),(96,58),(82,40)),((60,48),(70,60),(58,70))):
    poly(c,pts); c.set_source_rgba(0.85,0.8,1,0.35); c.fill()
cracks(c,(((96,58),(90,80)),((74,162),(84,140)),((84,140),(98,146)),((118,162),(110,142))),3.6)
c.rectangle(54,88,84,18); c.set_source_rgba(0.06,0.04,0.08,0.9); c.fill()
for x in (76,116): eye(c,x,97,6,(0.85,0.45,1),0.55,(1,0.85,1,1))
c.rectangle(76,126,40,8); c.set_source_rgba(0.1,0.03,0.02,1); c.fill(); c.rectangle(80,128,32,4); c.set_source_rgba(1,0.5,0.15,0.8); c.fill()
vignette(c,P,0.6); emit("黑曜石巨像",s)
# 熔火领主：火焰背景前的恶魔首领，巨大弯角、熔岩王冠、金色怒目
s,c=surface(P,P); bg(c,P,hexc('#a8280c'),hexc('#160302'),0.5,0.62); embers(c,60,top=40)
for k in range(7): flame(c,20+k*25,96,30,70+random.uniform(-10,16),(1,0.75,0.3,0.55),(1,0.3,0.05,0.0))
shoulders(c,148,[(0,hexc('#4a1206')),(1,hexc('#140402'))])
for sgn in (-1,1):
    path(c,[(96+sgn*30,60),(96+sgn*58,46,96+sgn*86,34,96+sgn*88,6),(96+sgn*98,42,96+sgn*72,62,96+sgn*44,80)])
    c.set_source(lin(96+sgn*30,6,96+sgn*88,80,[(0,hexc('#1a1210')),(0.5,hexc('#4a3a34')),(1,hexc('#120c0a'))])); c.fill()
path(c,[(96,42),(134,44,150,76,146,106),(142,132,124,152,110,168),(102,178,90,178,82,168),(68,152,50,132,46,106),(42,76,58,44,96,42)])
c.set_source(rad(96,94,8,90,[(0,hexc('#d24a1a')),(0.55,hexc('#8a1e0a')),(1,hexc('#2a0602'))])); c.fill()
cracks(c,(((64,70),(78,86)),((128,70),(114,86)),((96,150),(96,168))),3)
path(c,[(60,86),(80,76,90,82,96,92),(102,82,112,76,132,86),(116,94,104,98,96,100),(88,98,76,94,60,86)]); c.set_source_rgba(0.15,0.02,0.0,0.95); c.fill()
for sgn in (-1,1): eye(c,96+sgn*22,98,5.5,(1,0.8,0.1),0.5,(1,1,0.7,1))
path(c,[(70,136),(84,130,108,130,122,136),(116,160,76,160,70,136)]); c.set_source_rgba(0.12,0.02,0.0,1); c.fill()
glow(c,96,148,22,(1,0.55,0.1),0.8)
fangs(c,72,120,136,10,6); fangs(c,76,116,158,8,5,down=False)
for k,(x,h) in enumerate(((70,16),(83,24),(96,34),(109,24),(122,16))):
    crystal(c,[(x-7,48),(x,48-h),(x+7,48)],(1,0.92,0.5,1),(0.9,0.35,0.05,1),(1,0.95,0.7,0.9),1)
vignette(c,P,0.5); emit("熔火领主",s)
# 灰烬女巫：灰白长发与尖帽、橙色眼火、飘散的灰烬
s,c=surface(P,P); bg(c,P,hexc('#5a4a46'),hexc('#0c0a0a'),0.5,0.3); specks(c,50,(0.8,0.78,0.75),0.8,2.6)
embers(c,16)
for sgn in (-1,1):
    path(c,[(96+sgn*20,92),(96+sgn*70,110),(96+sgn*84,192),(96+sgn*30,192)]); c.set_source(lin(0,92,0,192,[(0,hexc('#b0aaa4')),(1,hexc('#3a3634'))])); c.fill()
shoulders(c,156,[(0,hexc('#3a3230')),(1,hexc('#0e0c0c'))])
path(c,[(96,62),(126,62,136,90,132,114),(128,132,116,142,96,146),(76,142,64,132,60,114),(56,90,66,62,96,62)])
c.set_source(rad(94,96,6,60,[(0,hexc('#d8d2cc')),(0.6,hexc('#a49c96')),(1,hexc('#4a4442'))])); c.fill()
for sgn in (-1,1):
    path(c,[(96+sgn*6,92),(96+sgn*30,86),(96+sgn*28,98)]); c.set_source_rgba(0.12,0.1,0.1,1); c.fill(); eye(c,96+sgn*18,94,4,(1,0.55,0.1),0.6,(1,0.9,0.5,1))
path(c,[(84,126),(96,130),(108,126),(102,134),(90,134)]); c.set_source_rgba(0.3,0.1,0.08,1); c.fill()
path(c,[(30,72),(96,52),(162,72),(140,64),(96,58),(52,64)]); c.set_source(lin(0,52,0,72,[(0,hexc('#3a302c')),(1,hexc('#16120f'))])); c.fill()
path(c,[(66,62),(84,30,92,14,118,4),(108,24,116,44,126,62)]); c.set_source(lin(66,4,126,62,[(0,hexc('#4a3e38')),(1,hexc('#16120f'))])); c.fill()
glow(c,118,6,12,(1,0.6,0.2),0.8)
vignette(c,P,0.55); emit("灰烬女巫",s)

# 普攻图标 128（文件名 = 普攻名称）
N=128
s,c=surface(N,N); bg(c,N,hexc('#8a2a0e'),hexc('#160402'))
c.set_line_cap(cairo.LINE_CAP_ROUND)
for k in range(3):
    c.move_to(30+k*22,24); c.curve_to(42+k*22,54,38+k*22,80,28+k*22,106); c.set_line_width(10-k); c.set_source_rgba(1,0.45,0.08,0.9); c.stroke()
    c.move_to(30+k*22,24); c.curve_to(42+k*22,54,38+k*22,80,28+k*22,106); c.set_line_width(3); c.set_source_rgba(1,0.95,0.7,0.9); c.stroke()
embers(c,14,N); vignette(c,N); emit("灼爪",s)
s,c=surface(N,N); bg(c,N,hexc('#6a2410'),hexc('#120402'))
glow(c,64,64,40,(1,0.5,0.1),0.75)
path(c,[(18,40),(64,30),(110,40),(100,58),(28,58)]); c.set_source(lin(0,30,0,58,[(0,hexc('#4a2e24')),(1,hexc('#1c0e0a'))])); c.fill()
path(c,[(18,88),(28,70),(100,70),(110,88),(64,98)]); c.set_source(lin(0,70,0,98,[(0,hexc('#1c0e0a')),(1,hexc('#4a2e24'))])); c.fill()
for k in range(6):
    x=30+k*13; path(c,[(x,58),(x+6,74),(x+12,58)]); c.set_source_rgba(1,0.9,0.75,1); c.fill()
    path(c,[(x,70),(x+6,56),(x+12,70)]); c.set_source_rgba(1,0.85,0.65,1); c.fill()
vignette(c,N); emit("熔牙撕咬",s)
s,c=surface(N,N); bg(c,N,hexc('#8a3a0a'),hexc('#140502'),0.6,0.4)
for k in range(5):
    c.move_to(20+k*4,108-k*2); c.line_to(70,58); c.set_line_width(14-k*2.2); c.set_source_rgba(1,0.4+k*0.1,0.05,0.25+k*0.08); c.stroke()
glow(c,78,50,40,(1,0.6,0.1),0.9); c.arc(78,50,20,0,2*math.pi); c.set_source(rad(72,44,2,22,[(0,(1,1,0.85,1)),(1,(1,0.45,0.05,1))])); c.fill()
vignette(c,N,0.6); emit("火球",s)
s,c=surface(N,N); bg(c,N,hexc('#7a3414'),hexc('#160803'))
path(c,[(36,104),(60,20),(84,104)]); c.set_source(lin(0,20,0,104,[(0,hexc('#fff0b0')),(0.3,hexc('#ff8a2a')),(0.7,hexc('#8a4a2a')),(1,hexc('#3a2016'))])); c.fill()
glow(c,60,26,20,(1,0.65,0.15),0.95)
for k in range(4):
    c.move_to(92,40+k*16); c.line_to(118,36+k*16); c.set_line_width(2.5); c.set_source_rgba(1,0.75,0.4,0.55); c.stroke()
vignette(c,N); emit("熔角冲撞",s)
s,c=surface(N,N); bg(c,N,hexc('#9a3a08'),hexc('#140402'),0.5,0.5)
for k in range(14):
    a=k*2*math.pi/14; r0,r1=18,52+(k%2)*10
    path(c,[(64+math.cos(a-0.12)*r0,64+math.sin(a-0.12)*r0),(64+math.cos(a)*r1,64+math.sin(a)*r1),(64+math.cos(a+0.12)*r0,64+math.sin(a+0.12)*r0)])
    c.set_source_rgba(1,0.6+(k%2)*0.25,0.15,0.9); c.fill()
glow(c,64,64,34,(1,0.95,0.6),1); c.arc(64,64,14,0,2*math.pi); c.set_source_rgba(1,1,0.9,1); c.fill()
vignette(c,N,0.55); emit("熔核自爆",s)
s,c=surface(N,N); bg(c,N,hexc('#3a2e4a'),hexc('#08060c'))
c.save(); c.translate(64,64); c.rotate(-0.6)
c.rectangle(-5,-6,10,62); c.set_source(lin(0,-6,0,56,[(0,hexc('#6a4a3a')),(1,hexc('#2a1a12'))])); c.fill()
crystal(c,[(-34,-34),(-10,-46),(14,-46),(34,-30),(30,-8),(-30,-8)],(0.55,0.48,0.7,1),(0.1,0.08,0.14,1),(0.85,0.75,1,0.8),2)
c.restore()
cracks(c,(((44,40),(56,48)),((70,30),(76,42))),3)
vignette(c,N); emit("黑曜重锤",s)
s,c=surface(N,N); bg(c,N,hexc('#9a1e0a'),hexc('#140202'))
c.set_line_cap(cairo.LINE_CAP_ROUND)
for k,w in enumerate((18,11,5)):
    c.arc(64,98,60,-math.pi*0.92,-math.pi*0.16); c.set_line_width(w); c.set_source_rgba(1,0.3+k*0.28,0.05+k*0.25,0.35+k*0.3); c.stroke()
for k in range(6):
    a=-math.pi*0.88+k*0.28; x=64+math.cos(a)*60; y=98+math.sin(a)*60
    flame(c,x,y,10,18,(1,0.95,0.6,0.9),(1,0.35,0.05,0.0))
glow(c,104,46,16,(1,0.75,0.3),0.9); vignette(c,N); emit("炎狱横扫",s)
s,c=surface(N,N); bg(c,N,hexc('#4a4240'),hexc('#0c0a0a'),0.6,0.4)
for k in range(6):
    x,y=24+k*8+random.uniform(-3,3),104-k*8+random.uniform(-3,3); c.arc(x,y,7-k*0.6,0,2*math.pi); c.set_source_rgba(0.75,0.72,0.7,0.25); c.fill()
glow(c,76,52,36,(1,0.55,0.15),0.7); c.arc(76,52,18,0,2*math.pi); c.set_source(rad(70,46,2,20,[(0,(0.95,0.92,0.9,1)),(0.6,(0.6,0.56,0.54,1)),(1,(0.3,0.26,0.25,1))])); c.fill()
cracks(c,(((68,46),(80,56)),((80,56),(86,48))),2.6)
vignette(c,N,0.6); emit("灰烬弹",s)

# ---------- 自动攻击模块 ----------
def attack_asset(path,stable,name,icon,desc,delivery,rng,acq,bat,point,back,speed,buffer):
    text=f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['AutoAttackDefinition']}, type: 3}}
  m_Name: {us(os.path.basename(path)[:-6])}
  m_EditorClassIdentifier: Roguelike.Abilities.Core::Roguelike.Abilities.AutoAttackDefinition
  stableId: {stable}
  displayName: {us(name)}
  icon: {{fileID: 2800000, guid: {icons[icon]}, type: 3}}
  description: {us(desc)}
  delivery: {delivery}
  range: {num(rng)}
  acquisitionRange: {num(acq)}
  baseAttackTime: {num(bat)}
  attackPoint: {num(point)}
  backswing: {num(back)}
  projectileSpeed: {num(speed)}
  rangeBuffer: {num(buffer)}
  autoAcquire: 1
  countsAsBasicAttackHit: 0
"""
    write(path,text); native_meta(path)
    return guid_for(path,g)
def num(v):
    v=round(v,4)
    return str(int(v)) if v==int(v) else str(v)
A="Assets/GameData/普攻/怪物/"
attacks={
 "灼爪":attack_asset(A+"灼爪.asset","attack.scorch_claw","灼爪","灼爪","近战：前摇结束立即用灼热的爪子抓伤目标。",0,1.25,14,1.0,0.35,0.4,0,0.5),
 "熔牙撕咬":attack_asset(A+"熔牙撕咬.asset","attack.magma_bite","熔牙撕咬","熔牙撕咬","近战：短前摇、短间隔的快速撕咬。",0,1.1,14,0.75,0.2,0.3,0,0.5),
 "火球":attack_asset(A+"火球.asset","attack.fireball","火球","火球","弹道：前摇结束掷出追踪火球，飞得比骨箭慢，命中后结算伤害。",1,12,16,1.7,0.45,0.4,12,1.5),
 "熔角冲撞":attack_asset(A+"熔角冲撞.asset","attack.molten_horn","熔角冲撞","熔角冲撞","近战：冲锋间隙用熔岩犄角顶撞贴身目标。",0,1.4,14,1.2,0.4,0.5,0,0.5),
 "熔核自爆":attack_asset(A+"熔核自爆.asset","attack.core_burst","熔核自爆","熔核自爆","自爆：贴近后开始前摇，前摇结束对爆炸半径内的英雄造成伤害并死亡；前摇期间被拉开距离会取消。",0,1.6,14,1.0,0.8,0,0,0.8),
 "黑曜重锤":attack_asset(A+"黑曜重锤.asset","attack.obsidian_maul","黑曜重锤","黑曜重锤","近战：慢速但沉重的锤击。",0,1.6,14,1.6,0.5,0.6,0,0.5),
 "炎狱横扫":attack_asset(A+"炎狱横扫.asset","attack.inferno_sweep","炎狱横扫","炎狱横扫","近战：首领的大范围横扫，攻击距离更长。",0,2.2,18,1.3,0.45,0.5,0,0.6),
 "灰烬弹":attack_asset(A+"灰烬弹.asset","attack.ash_bolt","灰烬弹","灰烬弹","弹道：远距离射出灰烬弹，命中后结算伤害。",1,16,20,1.4,0.5,0.5,16,1.5),
}

# ---------- 材质 ----------
enemy_mat=open(ROOT+"/Assets/Game/Materials/GameEnemy.mat").read()
proj_mat=open(ROOT+"/Assets/Game/Materials/GameProjectile.mat").read()
def material(template,old_name,name,color,old_colors):
    text=template.replace("m_Name: "+old_name,"m_Name: "+us(name))
    r,gg,b=color
    for old in old_colors:
        if old not in text: raise SystemExit("材质模板颜色已变化："+old)
        text=text.replace(old,"{r: %s, g: %s, b: %s, a: 1}"%(r,gg,b))
    path="Assets/Game/Enemies/Materials/%s.mat"%name
    write(path,text); native_meta(path,2100000)
    return guid_for(path,g)
ENEMY_COLORS=["{r: 0.72, g: 0.18, b: 0.13, a: 1}","{r: 0.72, g: 0.17999998, b: 0.12999997, a: 1}"]
def emat(name,color): return material(enemy_mat,"GameEnemy",name,color,ENEMY_COLORS)
hostile=material(proj_mat,"GameProjectile","熔火弹体",(1.0,0.48,0.12),["{r: 1, g: 0.79, b: 0.19, a: 1}","{r: 1, g: 0.78999996, b: 0.18999997, a: 1}"])

# ---------- 预制体 ----------
MESH={"cube":10202,"cylinder":10206,"sphere":10207,"capsule":10208}
def fid(key): return int(uuid.uuid5(uuid.NAMESPACE_URL,key).int%(2**62))+1000000
def quat(ex,ey,ez):
    def q(axis,deg):
        h=math.radians(deg)/2; s_=math.sin(h); c_=math.cos(h)
        return [s_ if axis==0 else 0, s_ if axis==1 else 0, s_ if axis==2 else 0, c_]
    def mul(a,b):
        ax,ay,az,aw=a; bx,by,bz,bw=b
        return [aw*bx+ax*bw+ay*bz-az*by, aw*by-ax*bz+ay*bw+az*bx, aw*bz+ax*by-ay*bx+az*bw, aw*bw-ax*bx-ay*by-az*bz]
    r=mul(mul(q(1,ey),q(0,ex)),q(2,ez))
    return [round(v,6)+0.0 for v in r]
def v3(a): return "{x: %s, y: %s, z: %s}"%tuple(num(x) for x in a)
def renderer(go,rid,mat):
    return f"""--- !u!23 &{rid}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mat}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 1
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""
def gameobject(go,components,name):
    comps="".join("  - component: {fileID: %d}\n"%c for c in components)
    return f"""--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}  m_Layer: 0
  m_Name: {us(name) if any(ord(ch)>127 for ch in name) else name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
def transform(tid,go,pos,rot,scale,children,father):
    q=quat(*rot)
    kids="".join("  - {fileID: %d}\n"%ch for ch in children) if children else None
    return f"""--- !u!4 &{tid}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {num(q[0])}, y: {num(q[1])}, z: {num(q[2])}, w: {num(q[3])}}}
  m_LocalPosition: {v3(pos)}
  m_LocalScale: {v3(scale)}
  m_ConstrainProportionsScale: 0
  m_Children:{chr(10)+kids if kids else ' []'+chr(10)}  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {v3(rot)}
"""
PREFABS={}
def enemy(cid,name,stable,desc,color,behavior,attack,parts,**stats):
    mat=emat(name,color)
    path="Assets/GameData/怪物/%d_%s.prefab"%(cid,name)
    key="ember:"+path
    root_go,root_tf,root_mb=fid(key+"go"),fid(key+"tf"),fid(key+"mb")
    blocks=[]; child_tfs=[]
    for i,(pname,shape,pos,rot,scale) in enumerate(parts):
        go,tf,mf,mr=(fid("%s/%d/%s"%(key,i,s_)) for s_ in ("go","tf","mf","mr"))
        child_tfs.append(tf)
        blocks.append(gameobject(go,[tf,mf,mr],pname))
        blocks.append(transform(tf,go,pos,rot,scale,[],root_tf))
        blocks.append(f"""--- !u!33 &{mf}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Mesh: {{fileID: {MESH[shape]}, guid: 0000000000000000e000000000000000, type: 0}}
""")
        blocks.append(renderer(go,mr,mat))
    s_=dict(healthMultiplier=1,damageMultiplier=1,speedMultiplier=1,experienceMultiplier=1,goldMultiplier=1,bodyScale=1,preferredDistance=0.85,retreatDistance=5,
           chargeTriggerRange=10,chargeSpeedMultiplier=4,chargeDuration=0.7,chargeCooldown=5,chargeDamageMultiplier=2.5,explosionRadius=3.5,explosionDamageMultiplier=5)
    s_.update(stats)
    if "goldMultiplier" not in stats: s_["goldMultiplier"]=s_["experienceMultiplier"]  # 击杀金币倍率默认与经验倍率一致
    fields="".join("  %s: %s\n"%(k,num(v)) for k,v in s_.items())
    head=gameobject(root_go,[root_tf,root_mb],"%d_%s"%(cid,name))+transform(root_tf,root_go,(0,0,0),(0,0,0),(1,1,1),child_tfs,0)
    mb=f"""--- !u!114 &{root_mb}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['EnemyArchetype']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Roguelike.Runtime::Roguelike.Combat.EnemyArchetype
  configId: {cid}
  stableId: {stable}
  displayName: {us(name)}
  description: {us(desc)}
  portrait: {{fileID: 2800000, guid: {icons[name]}, type: 3}}
  behavior: {behavior}
  attack: {{fileID: 11400000, guid: {attacks[attack]}, type: 2}}
{fields}"""
    write(path,"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"+head+mb+"".join(blocks))
    prefab_meta(path)
    PREFABS[cid]=dict(path=path,guid=guid_for(path,g),component=root_mb,name=name,stable=stable)

C,S,Y,K="cube","sphere","cylinder","capsule"
enemy(3009,"余烬小鬼","enemy.ember_imp","熔火裂谷最常见的小型火魔：成群扑向英雄，贴身后用灼热的爪子攻击；比食尸鬼稍快、稍脆。",(0.86,0.32,0.12),0,"灼爪",[
    ("躯干",K,(0,-0.1,0),(10,0,0),(0.5,0.32,0.45)),
    ("头",S,(0,0.26,0.12),(0,0,0),(0.4,0.36,0.38)),
    ("左角",Y,(-0.14,0.46,0.12),(0,0,25),(0.06,0.12,0.06)),
    ("右角",Y,(0.14,0.46,0.12),(0,0,-25),(0.06,0.12,0.06)),
    ("左爪",C,(-0.3,-0.1,0.18),(30,0,10),(0.12,0.4,0.12)),
    ("右爪",C,(0.3,-0.1,0.18),(30,0,-10),(0.12,0.4,0.12)),
    ("尾巴",C,(0,-0.2,-0.34),(-50,0,0),(0.06,0.06,0.3))],
    healthMultiplier=0.9,damageMultiplier=1.05,speedMultiplier=1.1,bodyScale=0.9)
enemy(3010,"熔岩蜥蜴","enemy.magma_lizard","贴地疾行的蜥蜴：移动极快、生命与伤害较低，经常从侧翼包抄。第 2 波起出现。",(0.55,0.22,0.1),0,"熔牙撕咬",[
    ("躯干",K,(0,-0.2,0),(90,0,0),(0.4,0.5,0.32)),
    ("头",C,(0,-0.12,0.52),(0,0,0),(0.3,0.2,0.34)),
    ("背脊",C,(0,0.02,0),(0,0,45),(0.12,0.12,0.7)),
    ("左前腿",C,(-0.24,-0.36,0.24),(0,0,30),(0.08,0.24,0.08)),
    ("右前腿",C,(0.24,-0.36,0.24),(0,0,-30),(0.08,0.24,0.08)),
    ("左后腿",C,(-0.24,-0.36,-0.24),(0,0,30),(0.08,0.24,0.08)),
    ("右后腿",C,(0.24,-0.36,-0.24),(0,0,-30),(0.08,0.24,0.08)),
    ("尾巴",K,(0,-0.24,-0.62),(80,0,0),(0.12,0.3,0.12))],
    healthMultiplier=0.55,damageMultiplier=0.7,speedMultiplier=1.7,experienceMultiplier=0.8,bodyScale=0.85)
enemy(3011,"火焰术士","enemy.flame_caster","远程游击：停在攻击距离边缘投掷火球，英雄靠近时后撤；火球比骨箭慢，但伤害更高。第 3 波起出现。",(0.78,0.2,0.16),1,"火球",[
    ("长袍",Y,(0,-0.14,0),(0,0,0),(0.44,0.36,0.44)),
    ("躯干",S,(0,0.16,0),(0,0,0),(0.38,0.3,0.34)),
    ("兜帽",S,(0,0.4,0),(0,0,0),(0.3,0.32,0.3)),
    ("帽尖",Y,(0,0.58,-0.06),(-25,0,0),(0.1,0.12,0.1)),
    ("法杖",Y,(0.32,0,0.12),(0,0,0),(0.05,0.5,0.05)),
    ("火球",S,(0.32,0.54,0.12),(0,0,0),(0.2,0.2,0.2))],
    healthMultiplier=0.7,damageMultiplier=0.9,speedMultiplier=0.9,preferredDistance=0.85,retreatDistance=5)
enemy(3012,"熔岩犀牛","enemy.lava_rhino","进入冲锋距离（比蛮牛更远）后直线突进，撞到英雄造成高额伤害；冲锋间隙用熔角近战。第 4 波起出现。",(0.4,0.22,0.16),2,"熔角冲撞",[
    ("躯干",C,(0,-0.1,-0.06),(0,0,0),(0.74,0.58,0.92)),
    ("头",C,(0,-0.08,0.56),(8,0,0),(0.44,0.36,0.36)),
    ("巨角",Y,(0,0.14,0.76),(35,0,0),(0.1,0.22,0.1)),
    ("小角",Y,(0,0.08,0.6),(20,0,0),(0.07,0.1,0.07)),
    ("背甲",C,(0,0.24,-0.04),(0,0,45),(0.3,0.3,0.7))],
    healthMultiplier=1.8,damageMultiplier=1.2,speedMultiplier=0.8,experienceMultiplier=1.6,bodyScale=1.25,
    chargeTriggerRange=12,chargeSpeedMultiplier=4,chargeDuration=0.8,chargeCooldown=5,chargeDamageMultiplier=2.5)
enemy(3013,"爆燃火灵","enemy.blaze_wisp","冲向英雄并在贴近后自爆，爆炸半径比毒囊更大、伤害略低；在前摇结束前击杀或拉开距离即可避免。第 5 波起出现。",(1.0,0.55,0.12),3,"熔核自爆",[
    ("火核",S,(0,0,0),(0,0,0),(0.72,0.72,0.72)),
    ("火舌1",C,(0.18,0.36,0.06),(0,0,-20),(0.14,0.34,0.14)),
    ("火舌2",C,(-0.2,0.32,-0.04),(0,0,25),(0.12,0.3,0.12)),
    ("火舌3",C,(0.02,0.42,-0.16),(-20,0,0),(0.1,0.28,0.1)),
    ("火星1",S,(0.36,0.06,0.2),(0,0,0),(0.16,0.16,0.16)),
    ("火星2",S,(-0.34,-0.08,-0.18),(0,0,0),(0.14,0.14,0.14))],
    healthMultiplier=0.5,damageMultiplier=1,speedMultiplier=1.25,experienceMultiplier=0.6,bodyScale=0.9,
    explosionRadius=4,explosionDamageMultiplier=4.5)
enemy(3014,"黑曜石巨像","enemy.obsidian_colossus","高生命、低速度的坦克怪物，会挡住其他怪物的去路，黑曜石重锤缓慢但沉重。第 6 波起出现。",(0.36,0.3,0.42),0,"黑曜重锤",[
    ("躯干",C,(0,0.06,0),(0,0,-5),(0.8,0.6,0.56)),
    ("头",C,(0,0.44,0.06),(0,-15,0),(0.3,0.26,0.28)),
    ("左臂",C,(-0.54,-0.04,0.04),(0,0,-8),(0.28,0.74,0.32)),
    ("右臂",C,(0.54,-0.04,0.04),(0,0,8),(0.28,0.74,0.32)),
    ("左腿",C,(-0.2,-0.38,0),(0,0,0),(0.28,0.3,0.3)),
    ("右腿",C,(0.2,-0.38,0),(0,0,0),(0.28,0.3,0.3)),
    ("左肩晶",C,(-0.42,0.42,0),(45,0,45),(0.16,0.3,0.16)),
    ("右肩晶",C,(0.42,0.42,0),(45,0,-45),(0.16,0.3,0.16))],
    healthMultiplier=3,damageMultiplier=1.5,speedMultiplier=0.65,experienceMultiplier=2.5,bodyScale=1.4)
enemy(3015,"熔火领主","enemy.molten_lord","首领：手持熔岩巨斧的近战巨兽，攻击距离更长，生命极高。",(0.72,0.16,0.08),0,"炎狱横扫",[
    ("躯干",K,(0,-0.06,0),(0,0,0),(0.68,0.44,0.6)),
    ("头",S,(0,0.38,0.1),(0,0,0),(0.4,0.36,0.4)),
    ("左角",Y,(-0.22,0.56,0.04),(-30,0,40),(0.08,0.16,0.08)),
    ("右角",Y,(0.22,0.56,0.04),(-30,0,-40),(0.08,0.16,0.08)),
    ("火冠",Y,(0,0.6,0.1),(0,0,0),(0.3,0.05,0.3)),
    ("左肩",S,(-0.42,0.18,0),(0,0,0),(0.3,0.26,0.3)),
    ("右肩",S,(0.42,0.18,0),(0,0,0),(0.3,0.26,0.3)),
    ("巨斧柄",C,(0.46,-0.02,0.3),(70,0,0),(0.08,0.86,0.14)),
    ("斧刃",C,(0.46,0.12,0.66),(70,0,0),(0.06,0.24,0.34))],
    healthMultiplier=1.2,damageMultiplier=1.2,speedMultiplier=0.9,experienceMultiplier=1.5,bodyScale=1.2)
enemy(3016,"灰烬女巫","enemy.ash_witch","远程游击的精英与首领：远距离发射灰烬弹，英雄靠近时后撤。第 10 波起出现。",(0.62,0.58,0.56),1,"灰烬弹",[
    ("长袍",Y,(0,-0.12,0),(0,0,0),(0.5,0.38,0.5)),
    ("躯干",S,(0,0.2,0),(0,0,0),(0.4,0.32,0.36)),
    ("头",S,(0,0.42,0.02),(0,0,0),(0.28,0.3,0.28)),
    ("帽檐",Y,(0,0.6,0.02),(0,0,0),(0.4,0.03,0.4)),
    ("帽尖",Y,(0,0.74,-0.02),(-15,0,0),(0.14,0.16,0.14)),
    ("法杖",Y,(0.36,-0.02,0.12),(0,0,0),(0.05,0.5,0.05)),
    ("灰烬球",S,(0.36,0.52,0.12),(0,0,0),(0.18,0.18,0.18))],
    healthMultiplier=1.4,damageMultiplier=1.3,speedMultiplier=0.85,experienceMultiplier=2,bodyScale=1.1,preferredDistance=0.8,retreatDistance=6)

# ---------- 名册（权重、出场波次与档位与“敌人名册”逐条对位） ----------
ROSTER=[(3009,10,1,0,3),(3010,6,2,0,3),(3011,5,3,0,3),(3012,3,4,0,3),(3013,3,5,0,1),(3014,1.5,6,0,3),(3015,1,1,0,4),(3016,1,10,0,6)]
entries="".join(f"""  - prefab: {{fileID: {PREFABS[c]['component']}, guid: {PREFABS[c]['guid']}, type: 3}}
    weight: {num(w)}
    firstWave: {f}
    lastWave: {l}
    tiers: {t}
""" for c,w,f,l,t in ROSTER)
rpath="Assets/GameData/目录/熔火名册.asset"
write(rpath,f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['EnemyRoster']}, type: 3}}
  m_Name: {us("熔火名册")}
  m_EditorClassIdentifier: Roguelike.Runtime::Roguelike.Combat.EnemyRoster
  entries:
{entries}  eliteTint: {{r: 1, g: 0.78, b: 0.28, a: 1}}
  eliteTintStrength: 0.45
  bossTint: {{r: 1, g: 0.35, b: 0.3, a: 1}}
  bossTintStrength: 0.15
""")
native_meta(rpath)
save_guids(g)
json.dump(dict(roster=guid_for(rpath,g),hostile=hostile,attacks=attacks,prefabs={str(k):v for k,v in PREFABS.items()},icons=icons),
          open(os.path.join(HERE,'ember.json'),"w",newline="\n"),ensure_ascii=False,indent=1)
print("ok",len(PREFABS),"prefabs",len(icons),"icons")
