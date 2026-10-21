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
save_guids(g); json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'icons.json'),'w'),ensure_ascii=False,indent=1); print(len(out),"icons")
