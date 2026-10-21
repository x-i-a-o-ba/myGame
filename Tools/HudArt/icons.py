import sys; import os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from common import *
g=load_guids(); D="Assets/Game/UI/Icons"; out={}
random.seed(7)
def emit(name,s):
    path="%s/%s.png"%(D,name); save(s,path); sprite_meta(path,g); out[name]=g[path]
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
def rot(p,o,a):
    x,y=p[0]-o[0],p[1]-o[1]; ca,sa=math.cos(a),math.sin(a); return (o[0]+x*ca-y*sa,o[1]+x*sa+y*ca)
def arrow(c,x0,y0,x1,y1,w,head,shaft=(0.85,0.95,1,1),tip=((0.95,1,1,1),(0.3,0.7,1,1)),fletch=(0.6,0.85,1,0.9)):
    a=math.atan2(y1-y0,x1-x0); L=math.hypot(x1-x0,y1-y0)
    c.save(); c.translate(x0,y0); c.rotate(a)
    # 冰霜拖尾
    c.rectangle(-L*0.25,-w*2.2,L*0.9,w*4.4); c.set_source(lin(-L*0.25,0,L*0.65,0,[(0,(0.4,0.8,1,0)),(1,(0.6,0.9,1,0.35))])); c.fill()
    c.rectangle(0,-w/2,L-head*0.8,w); c.set_source_rgba(*shaft); c.fill()
    crystal(c,[(L-head,-head*0.42),(L,0),(L-head,head*0.42),(L-head*0.78,0)],tip[0],tip[1],(1,1,1,0.9),1)
    for s in (-1,1):
        poly(c,[(0,0),(head*0.9,0),(head*0.25,s*head*0.42),(-head*0.15,s*head*0.42)]); c.set_source_rgba(*fletch); c.fill()
    c.restore()
def snowflake(c,x,y,r,lw,col=(0.95,1,1,1)):
    c.set_source_rgba(*col); c.set_line_width(lw); c.set_line_cap(cairo.LINE_CAP_ROUND)
    for k in range(6):
        a=k*math.pi/3; ex,ey=x+math.cos(a)*r,y+math.sin(a)*r
        c.move_to(x,y); c.line_to(ex,ey)
        for f in (0.45,0.7):
            bx,by=x+math.cos(a)*r*f,y+math.sin(a)*r*f
            for s in (-1,1):
                b=a+s*math.pi/4; c.move_to(bx,by); c.line_to(bx+math.cos(b)*r*0.28,by+math.sin(b)*r*0.28)
        c.stroke()
N=128
# Q 冰霜专注：凝视之眼中的雪花与聚焦光环
s,c=surface(N,N); bg(c,N,hexc('#2f7fc4'),hexc('#071528'))
for i,r in enumerate((54,44,35)):
    c.arc(64,64,r,0,2*math.pi); c.set_line_width(2.2-i*0.4); c.set_source_rgba(0.6,0.9,1,0.25+i*0.15); c.stroke()
for k in range(12):
    a=k*math.pi/6; c.move_to(64+math.cos(a)*38,64+math.sin(a)*38); c.line_to(64+math.cos(a)*56,64+math.sin(a)*56)
    c.set_line_width(2 if k%3==0 else 1); c.set_source_rgba(0.75,0.95,1,0.6 if k%3==0 else 0.3); c.stroke()
glow(c,64,64,40,(0.55,0.9,1),0.75); snowflake(c,64,64,27,4.5); glow(c,64,64,10,(1,1,1),1)
vignette(c,N); emit("冰霜专注",s)
# W 冰霜扇击：扇形齐射
s,c=surface(N,N); bg(c,N,hexc('#3a8fd0'),hexc('#08162b'),0.5,0.75)
c.move_to(64,116); c.arc(64,116,100,-math.pi*0.78,-math.pi*0.22); c.close_path(); c.set_source(rad(64,116,10,100,[(0,(0.7,0.95,1,0.35)),(1,(0.5,0.8,1,0))])); c.fill()
for k in range(7):
    a=-math.pi/2+(k-3)*0.25
    arrow(c,64+math.cos(a)*14,114+math.sin(a)*14,64+math.cos(a)*92,114+math.sin(a)*92,2.6,15)
glow(c,64,112,16,(0.8,0.97,1),0.9); vignette(c,N); emit("冰霜扇击",s)
# E 冰霜冲击：冰晶迸裂
s,c=surface(N,N); bg(c,N,hexc('#57b6e8'),hexc('#0a1b33'),0.5,0.55)
c.save(); c.translate(64,70); c.scale(1,0.45); c.arc(0,0,52,0,2*math.pi); c.restore(); c.set_source_rgba(0.75,0.95,1,0.25); c.fill()
for k in range(9):
    a=-math.pi/2+(k-4)*0.33+random.uniform(-0.06,0.06); L=random.uniform(34,52); w=random.uniform(6,10)
    base=(64,72); tipp=(64+math.cos(a)*L,72+math.sin(a)*L)
    l=rot((64-w,72),base,a+math.pi/2); r=rot((64+w,72),base,a+math.pi/2)
    m=(64+math.cos(a)*L*0.55,72+math.sin(a)*L*0.55)
    crystal(c,[l,(m[0]+math.cos(a+1.4)*w,m[1]+math.sin(a+1.4)*w),tipp,(m[0]+math.cos(a-1.4)*w,m[1]+math.sin(a-1.4)*w),r],(0.95,1,1,1),(0.25,0.6,0.95,1),(1,1,1,0.7),1)
glow(c,64,72,26,(1,1,1),0.95); vignette(c,N); emit("冰霜冲击",s)
# R 冰晶巨箭：对角巨型冰晶箭
s,c=surface(N,N); bg(c,N,hexc('#3c7dd6'),hexc('#060d24'),0.62,0.38)
for k in range(5):
    c.move_to(14+k*3,118-k*6); c.line_to(80+k*4,52-k*2); c.set_line_width(3-k*0.4); c.set_source_rgba(0.6,0.9,1,0.18+k*0.08); c.stroke()
c.save(); c.translate(22,106); c.rotate(-math.pi/4)
c.rectangle(0,-5,90,10); c.set_source(lin(0,-5,0,5,[(0,(0.9,1,1,1)),(1,(0.35,0.65,0.95,1))])); c.fill()
crystal(c,[(70,-24),(124,0),(70,24),(82,0)],(1,1,1,1),(0.2,0.5,0.95,1),(1,1,1,0.95),1.6)
poly(c,[(82,0),(124,0),(70,-24)]); c.set_source_rgba(1,1,1,0.35); c.fill()
for sgn in (-1,1):
    crystal(c,[(0,0),(26,0),(8,sgn*20),(-10,sgn*18)],(0.8,0.95,1,1),(0.25,0.55,0.9,1),(1,1,1,0.6),1)
c.restore()
glow(c,96,32,22,(0.85,0.97,1),0.85)
for _ in range(14):
    x,y=random.uniform(20,110),random.uniform(20,110); r=random.uniform(1,2.4); c.arc(x,y,r,0,2*math.pi); c.set_source_rgba(1,1,1,random.uniform(0.4,0.9)); c.fill()
vignette(c,N); emit("冰晶巨箭",s)
# 被动：冰箭普攻
s,c=surface(N,N); bg(c,N,hexc('#2b6f86'),hexc('#06141a'))
arrow(c,20,104,108,22,3.4,22,shaft=(0.82,0.88,0.92,1))
snowflake(c,98,32,10,2); vignette(c,N); emit("冰箭普攻",s)
# 天赋树
s,c=surface(N,N); bg(c,N,hexc('#3d3320'),hexc('#0b0906'))
c.set_line_cap(cairo.LINE_CAP_ROUND); c.set_source_rgba(0.95,0.8,0.45,1); c.set_line_width(7)
c.move_to(64,112); c.line_to(64,40); c.stroke(); c.set_line_width(5)
for y,dx in ((88,-28),(72,28),(56,-24),(44,22)):
    c.move_to(64,y+8); c.curve_to(64+dx*0.3,y,64+dx*0.8,y,64+dx,y-12); c.stroke()
for x,y in ((36,76),(92,60),(40,44),(86,32),(64,26)):
    glow(c,x,y,13,(1,0.85,0.4),0.7); c.arc(x,y,6,0,2*math.pi); c.set_source_rgba(1,0.93,0.65,1); c.fill()
vignette(c,N,0.6); emit("天赋树",s)
# 敌人攻击模块图标
def claws(c,col):
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    for k in range(3):
        c.move_to(34+k*20,26); c.curve_to(44+k*20,54,40+k*20,80,30+k*20,104); c.set_line_width(9-k); c.set_source_rgba(*col); c.stroke()
        c.move_to(34+k*20,26); c.curve_to(44+k*20,54,40+k*20,80,30+k*20,104); c.set_line_width(3); c.set_source_rgba(1,0.85,0.8,0.8); c.stroke()
s,c=surface(N,N); bg(c,N,hexc('#6b1d14'),hexc('#140504')); claws(c,(0.75,0.08,0.05,1)); vignette(c,N); emit("腐爪",s)
s,c=surface(N,N); bg(c,N,hexc('#5b5238'),hexc('#100e08'))
arrow(c,22,100,106,26,3.2,20,shaft=(0.9,0.86,0.72,1),tip=((1,0.97,0.88,1),(0.6,0.55,0.4,1)),fletch=(0.45,0.4,0.3,0.95))
c.arc(30,94,7,0,2*math.pi); c.set_source_rgba(0.93,0.9,0.8,1); c.fill(); vignette(c,N); emit("骨箭",s)
s,c=surface(N,N); bg(c,N,hexc('#7a4a1c'),hexc('#160b03'))
for sgn in (-1,1):
    c.move_to(64+sgn*10,88); c.curve_to(64+sgn*44,84,64+sgn*50,52,64+sgn*30,22); c.curve_to(64+sgn*38,54,64+sgn*30,70,64+sgn*6,72); c.close_path()
    c.set_source(lin(0,20,0,90,[(0,(1,0.95,0.85,1)),(1,(0.6,0.45,0.3,1))])); c.fill()
for k in range(4):
    c.move_to(20+k*6,104); c.line_to(48+k*10,100); c.set_line_width(2); c.set_source_rgba(1,0.8,0.5,0.4); c.stroke()
vignette(c,N); emit("蛮角冲撞",s)
s,c=surface(N,N); bg(c,N,hexc('#3d6b1a'),hexc('#081204'))
glow(c,64,70,50,(0.6,1,0.2),0.6); c.arc(64,72,30,0,2*math.pi); c.set_source(rad(56,62,4,32,[(0,(0.85,1,0.5,1)),(1,(0.25,0.5,0.05,1))])); c.fill()
for k in range(7):
    a=k*2*math.pi/7; c.arc(64+math.cos(a)*18,72+math.sin(a)*18,4,0,2*math.pi); c.set_source_rgba(0.95,1,0.6,0.7); c.fill()
c.move_to(64,42); c.curve_to(70,30,80,30,84,22); c.set_line_width(3); c.set_source_rgba(1,0.8,0.3,1); c.stroke(); glow(c,84,22,9,(1,0.9,0.3),1)
vignette(c,N); emit("毒囊自爆",s)
s,c=surface(N,N); bg(c,N,hexc('#5c5a57'),hexc('#0f0f0e'))
crystal(c,[(34,70),(50,36),(84,30),(102,58),(92,94),(52,100)],(0.75,0.72,0.68,1),(0.25,0.24,0.22,1),(0.1,0.1,0.1,1),2)
for p,q in (((50,36),(62,64)),((62,64),(92,94)),((62,64),(34,70)),((84,30),(62,64))):
    c.move_to(*p); c.line_to(*q); c.set_line_width(1.5); c.set_source_rgba(0,0,0,0.5); c.stroke()
vignette(c,N); emit("岩石重击",s)
s,c=surface(N,N); bg(c,N,hexc('#4a2f86'),hexc('#0a0618'))
glow(c,64,64,46,(0.55,0.75,1),0.8); c.arc(64,64,20,0,2*math.pi); c.set_source(rad(58,58,2,22,[(0,(1,1,1,1)),(1,(0.4,0.55,1,1))])); c.fill()
snowflake(c,64,64,34,2.2,(0.85,0.9,1,0.8)); vignette(c,N); emit("巫妖霜弹",s)
s,c=surface(N,N); bg(c,N,hexc('#7a1410'),hexc('#120202'))
c.set_line_cap(cairo.LINE_CAP_ROUND)
for k,w in enumerate((16,10,4)):
    c.arc(64,96,62,-math.pi*0.92,-math.pi*0.18); c.set_line_width(w); c.set_source_rgba(1,0.35+k*0.25,0.1+k*0.3,0.35+k*0.3); c.stroke()
glow(c,100,44,18,(1,0.7,0.3),0.9); vignette(c,N); emit("深渊横扫",s)
# 寒冰射手头像 256：兜帽、发光眼睛、长弓
N2=256; s,c=surface(N2,N2); bg(c,N2,hexc('#5aa8e0'),hexc('#08142a'),0.5,0.3)
for _ in range(40):
    x,y=random.uniform(0,256),random.uniform(0,256); c.arc(x,y,random.uniform(0.8,2.2),0,2*math.pi); c.set_source_rgba(1,1,1,random.uniform(0.2,0.7)); c.fill()
# 长弓
c.set_line_cap(cairo.LINE_CAP_ROUND)
c.move_to(206,26); c.curve_to(250,90,250,170,206,236); c.set_line_width(9); c.set_source(lin(200,20,240,240,[(0,(0.9,1,1,1)),(1,(0.3,0.6,0.95,1))])); c.stroke()
c.move_to(206,26); c.line_to(206,236); c.set_line_width(1.5); c.set_source_rgba(0.9,0.97,1,0.8); c.stroke()
# 斗篷与肩部
c.move_to(18,256); c.curve_to(30,190,70,170,128,166); c.curve_to(186,170,226,190,238,256); c.close_path()
c.set_source(lin(0,160,0,256,[(0,hexc('#3d5f9e')),(1,hexc('#141f3c'))])); c.fill()
c.move_to(128,166); c.line_to(112,256); c.line_to(144,256); c.close_path(); c.set_source_rgba(0.85,0.93,1,0.35); c.fill()
# 兜帽
c.move_to(128,30); c.curve_to(196,34,214,120,192,192); c.curve_to(170,214,86,214,64,192); c.curve_to(42,120,60,34,128,30); c.close_path()
c.set_source(lin(60,30,200,200,[(0,hexc('#6f9ad6')),(0.5,hexc('#2c4a86')),(1,hexc('#101a36'))])); c.fill_preserve()
c.set_line_width(3); c.set_source_rgba(0.75,0.9,1,0.6); c.stroke()
# 面部阴影与银发
c.move_to(128,66); c.curve_to(172,70,180,130,166,178); c.curve_to(150,194,106,194,90,178); c.curve_to(76,130,84,70,128,66); c.close_path()
c.set_source(rad(128,120,10,80,[(0,hexc('#1c2a44')),(1,hexc('#05080f'))])); c.fill()
for k in range(6):
    x=96+k*13; c.move_to(x,70); c.curve_to(x-6,110,x-10,150,x-18+k*3,200); c.set_line_width(4); c.set_source_rgba(0.88,0.95,1,0.75-k*0.06); c.stroke()
c.move_to(128,66); c.curve_to(100,80,92,110,96,140); c.curve_to(110,100,140,90,160,96); c.curve_to(156,80,146,70,128,66); c.close_path(); c.set_source_rgba(0.9,0.96,1,0.85); c.fill()
# 发光双眼
for x in (108,148):
    glow(c,x,128,22,(0.4,0.9,1),0.9); c.save(); c.translate(x,128); c.scale(1,0.45); c.arc(0,0,8,0,2*math.pi); c.restore(); c.set_source_rgba(0.9,1,1,1); c.fill()
# 冰晶胸针
crystal(c,[(128,188),(140,204),(128,224),(116,204)],(1,1,1,1),(0.3,0.65,1,1),(1,1,1,0.9),1.5); glow(c,128,204,20,(0.6,0.9,1),0.6)
vignette(c,N2,0.55); emit("寒冰射手",s)

# ---------- 2026-10-22 敌人头像 192：选中目标信息面板使用，文件名 = 敌人显示名 ----------
P=192
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
def specks(c,n,col,rmin=0.8,rmax=2.2):
    for _ in range(n):
        x,y=random.uniform(0,P),random.uniform(0,P); c.arc(x,y,random.uniform(rmin,rmax),0,2*math.pi); c.set_source_rgba(*col[:3],random.uniform(0.15,0.6)); c.fill()
def shoulders(c,top,stops):
    path(c,[(6,P),(14,top+30,40,top,96,top-4),(152,top,178,top+30,186,P)]); c.set_source(lin(0,top-4,0,P,stops)); c.fill()
def fangs(c,x0,x1,y,h,n,col=(0.96,0.93,0.82,1),down=True):
    w=(x1-x0)/n
    for k in range(n):
        x=x0+k*w; d=h*(0.7+0.3*((k%2)==0))
        path(c,[(x,y),(x+w/2,y+(d if down else -d)),(x+w,y)]); c.set_source_rgba(*col); c.fill()
# 腐化食尸鬼：秃头、尖耳、凹陷红眼、参差獠牙，腐烂斑点
s,c=surface(P,P); bg(c,P,hexc('#4a5a2c'),hexc('#0c1006'),0.5,0.35); specks(c,30,(0.6,0.8,0.3))
shoulders(c,150,[(0,hexc('#38452a')),(1,hexc('#10140a'))])
for sgn in (-1,1):
    path(c,[(96+sgn*40,84),(96+sgn*86,52),(96+sgn*48,108)]); c.set_source(lin(96+sgn*40,60,96+sgn*80,100,[(0,hexc('#8aa05a')),(1,hexc('#3d4c24'))])); c.fill()
path(c,[(96,30),(140,30,152,78,146,112),(140,150,118,170,96,172),(74,170,52,150,46,112),(40,78,52,30,96,30)])
c.set_source(rad(88,70,8,90,[(0,hexc('#a6b878')),(0.55,hexc('#6f8446')),(1,hexc('#2c3818'))])); c.fill()
for x,y,r in ((74,52,7),(118,60,5),(132,104,6),(62,122,4)): ell(c,x,y,r,r*0.8); c.set_source_rgba(0.25,0.3,0.1,0.55); c.fill()
path(c,[(54,82),(70,72,84,76,92,88),(100,76,114,72,138,82),(120,90,104,94,96,96),(88,94,70,90,54,82)]); c.set_source_rgba(0.12,0.15,0.06,0.9); c.fill()
for x in (76,116): ell(c,x,98,15,10); c.set_source_rgba(0.04,0.05,0.02,1); c.fill(); eye(c,x,98,5.5,(1,0.2,0.1),0.6,(1,0.75,0.5,1))
path(c,[(88,112),(96,126),(104,112)]); c.set_source_rgba(0.15,0.18,0.08,0.9); c.fill()
path(c,[(64,134),(80,128,112,128,128,134),(122,156,70,156,64,134)]); c.set_source_rgba(0.08,0.02,0.02,1); c.fill()
fangs(c,68,124,134,9,7); fangs(c,72,120,151,7,6,down=False)
vignette(c,P,0.6); emit("腐化食尸鬼",s)
# 迅捷猎犬：狼首正面，竖耳、琥珀色斜眼、长吻、龇牙
s,c=surface(P,P); bg(c,P,hexc('#8a4a22'),hexc('#160803'),0.5,0.3); specks(c,24,(1,0.7,0.4))
shoulders(c,158,[(0,hexc('#5c3016')),(1,hexc('#170a04'))])
for sgn in (-1,1):
    path(c,[(96+sgn*22,58),(96+sgn*58,10),(96+sgn*62,74)]); c.set_source(lin(0,10,0,74,[(0,hexc('#c27a44')),(1,hexc('#5e2e12'))])); c.fill()
    path(c,[(96+sgn*32,60),(96+sgn*55,24),(96+sgn*54,68)]); c.set_source_rgba(0.25,0.08,0.05,0.8); c.fill()
path(c,[(96,44),(140,44,166,72,162,104),(156,126,128,136,118,150),(110,176,82,176,74,150),(64,136,36,126,30,104),(26,72,52,44,96,44)])
c.set_source(rad(96,82,6,96,[(0,hexc('#d69458')),(0.6,hexc('#9a5428')),(1,hexc('#3c1a08'))])); c.fill()
path(c,[(74,110),(96,100),(118,110),(114,156),(96,170),(78,156)]); c.set_source(lin(0,100,0,170,[(0,hexc('#e8c49a')),(1,hexc('#a87a52'))])); c.fill()
for k in range(7):
    x=48+k*16; c.move_to(x,62+abs(k-3)*4); c.line_to(x+(k-3)*3,80+abs(k-3)*5); c.set_line_width(2); c.set_source_rgba(0.3,0.12,0.04,0.45); c.stroke()
for sgn in (-1,1):
    path(c,[(96+sgn*14,96),(96+sgn*44,82),(96+sgn*38,98)]); c.set_source_rgba(0.06,0.02,0,1); c.fill()
    eye(c,96+sgn*31,91,5,(1,0.75,0.15),0.55,(1,0.95,0.6,1))
ell(c,96,150,13,9); c.set_source(lin(0,142,0,158,[(0,hexc('#3a2a24')),(1,hexc('#050303'))])); c.fill()
path(c,[(80,162),(96,170),(112,162),(106,180),(86,180)]); c.set_source_rgba(0.1,0.02,0.02,1); c.fill()
for x in (84,108): path(c,[(x-3,162),(x,176),(x+3,162)]); c.set_source_rgba(0.98,0.95,0.86,1); c.fill()
vignette(c,P,0.6); emit("迅捷猎犬",s)
# 骷髅弓手：破旧兜帽下的骷髅、幽绿眼火，背后骨弓
s,c=surface(P,P); bg(c,P,hexc('#3c4a3a'),hexc('#080b08'),0.5,0.3); specks(c,30,(0.75,0.95,0.6))
c.set_line_cap(cairo.LINE_CAP_ROUND)
c.move_to(160,14); c.curve_to(192,70,192,124,160,182); c.set_line_width(7); c.set_source(lin(150,14,190,180,[(0,hexc('#efe6cc')),(1,hexc('#8a7c5a'))])); c.stroke()
c.move_to(160,14); c.line_to(160,182); c.set_line_width(1.2); c.set_source_rgba(0.9,0.88,0.8,0.7); c.stroke()
shoulders(c,150,[(0,hexc('#3a3022')),(1,hexc('#0e0b07'))])
path(c,[(96,18),(150,22,166,96,156,150),(140,170,52,170,36,150),(26,96,42,22,96,18)]); c.set_source(lin(40,18,160,160,[(0,hexc('#5a4a34')),(1,hexc('#16110a'))])); c.fill()
path(c,[(96,46),(130,46,142,78,138,108),(134,126,122,132,118,144),(110,156,82,156,74,144),(70,132,58,126,54,108),(50,78,62,46,96,46)])
c.set_source(rad(92,80,6,72,[(0,hexc('#f4ecd4')),(0.6,hexc('#cfc3a0')),(1,hexc('#7a6c4c'))])); c.fill()
for x in (78,114):
    ell(c,x,96,14,12); c.set_source_rgba(0.03,0.03,0.02,1); c.fill(); eye(c,x,98,4,(0.5,1,0.45),1,(0.85,1,0.8,1))
path(c,[(96,108),(103,124),(89,124)]); c.set_source_rgba(0.08,0.07,0.05,1); c.fill()
c.rectangle(76,134,40,14); c.set_source_rgba(0.06,0.05,0.04,1); c.fill()
for k in range(6):
    c.rectangle(77+k*6.6,134,5.4,6.5); c.rectangle(77+k*6.6,141.5,5.4,6); c.set_source_rgba(0.92,0.88,0.74,1); c.fill()
c.move_to(112,60); c.line_to(120,74); c.line_to(116,84); c.set_line_width(1.4); c.set_source_rgba(0.35,0.3,0.2,0.8); c.stroke()
vignette(c,P,0.6); emit("骷髅弓手",s)
# 蛮牛冲锋者：粗壮牛首、外弯巨角、怒红眼、金鼻环与鼻息
s,c=surface(P,P); bg(c,P,hexc('#8a3a16'),hexc('#170602'),0.5,0.32); specks(c,26,(1,0.6,0.3))
shoulders(c,140,[(0,hexc('#4a2412')),(1,hexc('#120704'))])
for sgn in (-1,1):
    path(c,[(96+sgn*40,70),(96+sgn*70,66,96+sgn*92,40,96+sgn*84,8),(96+sgn*80,34,96+sgn*66,52,96+sgn*44,90)])
    c.set_source(lin(96+sgn*40,8,96+sgn*84,90,[(0,hexc('#fff3dc')),(0.6,hexc('#cdb48a')),(1,hexc('#6e5634'))])); c.fill()
    path(c,[(96+sgn*34,72),(96+sgn*60,62),(96+sgn*48,86)]); c.set_source(lin(0,62,0,86,[(0,hexc('#7a4428')),(1,hexc('#3a1c0c'))])); c.fill()
path(c,[(96,48),(132,48,148,72,146,100),(144,124,132,140,124,156),(116,176,76,176,68,156),(60,140,48,124,46,100),(44,72,60,48,96,48)])
c.set_source(rad(96,86,8,88,[(0,hexc('#9a5a34')),(0.6,hexc('#6a3418')),(1,hexc('#2a1006'))])); c.fill()
path(c,[(80,52),(96,44),(112,52),(104,70),(88,70)]); c.set_source_rgba(0.2,0.08,0.03,0.85); c.fill()
for sgn in (-1,1):
    path(c,[(96+sgn*12,94),(96+sgn*42,84),(96+sgn*36,102)]); c.set_source_rgba(0.08,0.02,0,1); c.fill(); eye(c,96+sgn*28,94,5,(1,0.2,0.05),0.55,(1,0.8,0.5,1))
ell(c,96,150,30,20); c.set_source(lin(0,130,0,170,[(0,hexc('#c08a6e')),(1,hexc('#6a3c2a'))])); c.fill()
for sgn in (-1,1): ell(c,96+sgn*13,150,6,8); c.set_source_rgba(0.1,0.03,0.02,1); c.fill()
c.arc(96,166,11,0.15*math.pi,0.85*math.pi); c.set_line_width(4); c.set_source(lin(80,160,112,178,[(0,hexc('#fff0a0')),(1,hexc('#a07018'))])); c.stroke()
for sgn in (-1,1):
    for k in range(3): c.arc(96+sgn*(40+k*14),160+k*6,6+k*3,0,2*math.pi); c.set_source_rgba(1,1,1,0.16-k*0.04); c.fill()
vignette(c,P,0.6); emit("蛮牛冲锋者",s)
# 爆裂毒囊：鼓胀发光的毒囊、脓包、凶眼与引线火花
s,c=surface(P,P); bg(c,P,hexc('#3c6a14'),hexc('#060c02'),0.5,0.5); specks(c,40,(0.7,1,0.3),1,3)
glow(c,96,108,92,(0.55,1,0.15),0.55)
ell(c,96,112,70,66); c.set_source(rad(80,90,6,78,[(0,hexc('#e6ff9a')),(0.45,hexc('#8cc43a')),(1,hexc('#2c4a0c'))])); c.fill()
for x,y,r in ((54,74,16),(140,82,13),(150,128,10),(44,132,11),(112,52,9)):
    ell(c,x,y,r,r); c.set_source(rad(x-r*0.3,y-r*0.3,1,r,[(0,hexc('#f4ffc0')),(1,hexc('#6a9c1c'))])); c.fill()
    ell(c,x-r*0.35,y-r*0.35,r*0.25,r*0.2); c.set_source_rgba(1,1,1,0.7); c.fill()
for sgn in (-1,1):
    path(c,[(96+sgn*8,106),(96+sgn*36,96),(96+sgn*32,114)]); c.set_source_rgba(0.08,0.12,0.02,1); c.fill(); eye(c,96+sgn*24,106,4.5,(1,0.85,0.1),0.6,(1,1,0.75,1))
path(c,[(70,136),(84,130,108,130,122,136),(116,150,76,150,70,136)]); c.set_source_rgba(0.12,0.16,0.02,1); c.fill()
fangs(c,74,118,136,6,6,(0.95,1,0.8,1))
c.move_to(96,46); c.curve_to(100,30,116,28,122,14); c.set_line_width(3); c.set_source_rgba(0.45,0.3,0.15,1); c.stroke()
glow(c,122,14,16,(1,0.85,0.3),1); c.arc(122,14,3,0,2*math.pi); c.set_source_rgba(1,1,0.9,1); c.fill()
for x,y in ((60,176),(132,178),(98,184)): ell(c,x,y,4,7); c.set_source_rgba(0.7,1,0.3,0.7); c.fill()
vignette(c,P,0.55); emit("爆裂毒囊",s)
# 岩石魔像：多边形石块头颅、熔岩裂缝与发光双眼、苔藓
s,c=surface(P,P); bg(c,P,hexc('#4c4a48'),hexc('#0a0a0a'),0.5,0.32); specks(c,20,(1,0.7,0.4))
crystal(c,[(8,192),(20,150),(60,132),(132,132),(172,150),(184,192)],(0.42,0.4,0.38,1),(0.14,0.13,0.12,1),(0.05,0.05,0.05,1),2)
crystal(c,[(52,40),(94,22),(140,34),(156,78),(150,128),(120,160),(70,160),(40,128),(36,78)],(0.72,0.7,0.66,1),(0.26,0.25,0.23,1),(0.06,0.06,0.06,1),2.5)
for pts in (((52,40),(80,70),(36,78)),((140,34),(120,70),(156,78)),((40,128),(70,118),(70,160)),((150,128),(122,118),(120,160))):
    poly(c,pts); c.set_source_rgba(0,0,0,0.22); c.fill()
c.set_line_cap(cairo.LINE_CAP_ROUND)
for a,b in (((94,22),(98,58)),((98,58),(84,76)),((98,58),(112,72)),((70,160),(80,140)),((80,140),(96,146)),((120,160),(112,140))):
    c.move_to(*a); c.line_to(*b); c.set_line_width(4); c.set_source_rgba(1,0.45,0.1,0.35); c.stroke(); c.move_to(*a); c.line_to(*b); c.set_line_width(1.6); c.set_source_rgba(1,0.75,0.3,0.95); c.stroke()
c.rectangle(52,86,88,18); c.set_source_rgba(0.1,0.09,0.08,0.85); c.fill()
for x in (74,118): eye(c,x,95,6,(1,0.55,0.1),0.55,(1,0.92,0.6,1))
c.rectangle(74,124,44,9); c.set_source_rgba(0.1,0.04,0.02,1); c.fill(); c.rectangle(78,126,36,5); c.set_source_rgba(1,0.5,0.15,0.75); c.fill()
for x,y,r in ((60,46,9),(72,40,7),(142,46,6),(44,90,5)): ell(c,x,y,r,r*0.6); c.set_source_rgba(0.35,0.55,0.2,0.85); c.fill()
vignette(c,P,0.6); emit("岩石魔像",s)
# 深渊领主：烈焰背景前的恶魔首领，巨角、蝠翼轮廓、金色怒目
s,c=surface(P,P); bg(c,P,hexc('#7a1018'),hexc('#100204'),0.5,0.62)
for _ in range(50):
    x,y=random.uniform(0,P),random.uniform(60,P); r=random.uniform(0.8,2.6); c.arc(x,y,r,0,2*math.pi); c.set_source_rgba(1,random.uniform(0.4,0.8),0.2,random.uniform(0.3,0.9)); c.fill()
for sgn in (-1,1):
    path(c,[(96+sgn*30,110),(96+sgn*70,40),(96+sgn*96,30),(96+sgn*86,62),(96+sgn*96,80),(96+sgn*80,96),(96+sgn*92,118),(96+sgn*50,130)])
    c.set_source(lin(0,30,0,130,[(0,hexc('#3a0a10')),(1,hexc('#16040a'))])); c.fill()
shoulders(c,148,[(0,hexc('#4a0e18')),(1,hexc('#120306'))])
for sgn in (-1,1):
    path(c,[(96+sgn*28,56),(96+sgn*50,30,96+sgn*76,26,96+sgn*84,2),(96+sgn*92,34,96+sgn*68,56,96+sgn*40,74)])
    c.set_source(lin(96+sgn*30,2,96+sgn*84,74,[(0,hexc('#2a2224')),(0.5,hexc('#5a4a48')),(1,hexc('#141012'))])); c.fill()
path(c,[(96,40),(134,42,150,74,146,104),(142,130,124,150,110,166),(102,176,90,176,82,166),(68,150,50,130,46,104),(42,74,58,42,96,40)])
c.set_source(rad(96,92,8,90,[(0,hexc('#b4283a')),(0.55,hexc('#7a1424')),(1,hexc('#2a050c'))])); c.fill()
path(c,[(60,84),(80,74,90,80,96,90),(102,80,112,74,132,84),(116,92,104,96,96,98),(88,96,76,92,60,84)]); c.set_source_rgba(0.15,0.02,0.04,0.95); c.fill()
for sgn in (-1,1): eye(c,96+sgn*22,96,5.5,(1,0.75,0.1),0.5,(1,0.98,0.7,1))
path(c,[(70,134),(84,128,108,128,122,134),(116,160,76,160,70,134)]); c.set_source_rgba(0.12,0.01,0.02,1); c.fill()
glow(c,96,146,22,(1,0.45,0.1),0.7)
fangs(c,72,120,134,10,6); fangs(c,76,116,157,8,5,down=False)
crystal(c,[(96,52),(104,62),(96,72),(88,62)],(1,0.85,0.4,1),(0.8,0.3,0.05,1),(1,0.9,0.6,0.9),1)
vignette(c,P,0.5); emit("深渊领主",s)
# 霜骨巫妖：冰晶王冠下的骷髅，幽蓝眼火、紫色高领法袍、霜雾
s,c=surface(P,P); bg(c,P,hexc('#3a2c7a'),hexc('#07051a'),0.5,0.3); specks(c,40,(0.8,0.9,1),0.8,2.4)
glow(c,150,150,40,(0.5,0.7,1),0.5)
for sgn in (-1,1):
    path(c,[(96+sgn*20,140),(96+sgn*84,96),(96+sgn*92,192),(96,192)]); c.set_source(lin(0,96,0,192,[(0,hexc('#6a4ab8')),(1,hexc('#1c1240'))])); c.fill()
    path(c,[(96+sgn*24,146),(96+sgn*76,108),(96+sgn*70,124)]); c.set_source_rgba(0.85,0.8,1,0.35); c.fill()
path(c,[(96,52),(128,52,140,82,136,108),(132,124,120,132,118,144),(110,156,82,156,74,144),(72,132,60,124,56,108),(52,82,64,52,96,52)])
c.set_source(rad(92,86,6,72,[(0,hexc('#f2f4ff')),(0.6,hexc('#b8bce0')),(1,hexc('#4a4a7a'))])); c.fill()
for x in (80,112):
    ell(c,x,100,13,11); c.set_source_rgba(0.03,0.03,0.08,1); c.fill(); eye(c,x,101,4.2,(0.35,0.75,1),1,(0.85,0.95,1,1))
path(c,[(96,112),(102,126),(90,126)]); c.set_source_rgba(0.08,0.08,0.15,1); c.fill()
c.rectangle(80,134,32,12); c.set_source_rgba(0.06,0.06,0.12,1); c.fill()
for k in range(5): c.rectangle(81+k*6.2,134,5,5.5); c.rectangle(81+k*6.2,140.5,5,5); c.set_source_rgba(0.88,0.9,1,1); c.fill()
c.rectangle(58,50,76,8); c.set_source(lin(0,50,0,58,[(0,hexc('#cfe8ff')),(1,hexc('#4a78b8'))])); c.fill()
for k,(x,h) in enumerate(((60,18),(74,28),(96,44),(118,28),(132,18))):
    crystal(c,[(x-7,52),(x,52-h),(x+7,52)],(0.95,1,1,1),(0.3,0.6,0.95,1),(1,1,1,0.85),1)
glow(c,96,10,16,(0.7,0.9,1),0.9)
vignette(c,P,0.55); emit("霜骨巫妖",s)
save_guids(g); json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'icons.json'),'w'),ensure_ascii=False,indent=1); print(len(out),"icons")
