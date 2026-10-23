"""2026-10-23 战场与选择界面的表现资源：两张战场预览图（1016×572，选择界面战场卡片）、熔火裂谷的地面 / 熔岩池纹理与
材质（熔岩地面、熔岩池、黑曜石、玄武岩），以及选择界面展示台的材质。输出名称→GUID 表写入 battlefields.json。
只写本脚本列出的文件；重跑会覆盖它们（在 Unity 里手工调整过后不要再运行）。"""
import os, sys, json, math, random, re
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import cairo
import numpy as np
from common import ROOT, load_guids, save_guids, guid_for, folder_meta, sprite_meta, surface, lin, rad, save
from ui import us
HERE=os.path.dirname(os.path.abspath(__file__))
g=load_guids(); out={}
def write(path,text):
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True)
    open(full,"w",newline="\n").write(text)
for folder in ("Assets/GameData/表现/战场","Assets/GameData/表现/选择界面"):
    os.makedirs(os.path.join(ROOT,folder),exist_ok=True); folder_meta(folder,g)

# ---------- 可平铺噪声（numpy） ----------
def value_noise(n,period,rng):
    """周期为 period 格的值噪声，n×n 像素，四边可无缝平铺。"""
    grid=rng.random((period,period))
    t=np.arange(n)/n*period; i0=np.floor(t).astype(int); f=t-i0; f=f*f*(3-2*f); i1=(i0+1)%period
    a=grid[np.ix_(i0,i0)]; b=grid[np.ix_(i0,i1)]; c=grid[np.ix_(i1,i0)]; d=grid[np.ix_(i1,i1)]
    fy=f[:,None]; fx=f[None,:]
    return (a*(1-fx)+b*fx)*(1-fy)+(c*(1-fx)+d*fx)*fy
def fbm(n,base,octaves,rng,gain=0.5):
    total=np.zeros((n,n)); amp=1; norm=0
    for k in range(octaves):
        total+=value_noise(n,base*2**k,rng)*amp; norm+=amp; amp*=gain
    return total/norm
def voronoi(n,count,rng):
    """周期 Voronoi：返回到最近与次近种子点的距离（UV 单位）。"""
    pts=rng.random((count,2)); f1=np.empty((n,n)); f2=np.empty((n,n))
    xs=(np.arange(n)+0.5)/n
    for r0 in range(0,n,64):
        ys=(np.arange(r0,min(n,r0+64))+0.5)/n
        dx=np.abs(xs[None,:,None]-pts[None,None,:,0]); dx=np.minimum(dx,1-dx)
        dy=np.abs(ys[:,None,None]-pts[None,None,:,1]); dy=np.minimum(dy,1-dy)
        d=np.sqrt(dx*dx+dy*dy); part=np.partition(d,1,axis=-1)
        f1[r0:r0+len(ys)]=part[...,0]; f2[r0:r0+len(ys)]=part[...,1]
    return f1,f2
def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def save_rgb(arr,path):
    from PIL import Image
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True)
    Image.fromarray((np.clip(arr,0,1)**(1/1.0)*255+0.5).astype(np.uint8),"RGB").save(full,optimize=True)
TEXTURE_META=open(ROOT+"/Assets/GameData/表现/磐锋/磐锋卫士.png.meta").read()
def texture_meta(path):
    """默认纹理导入（重复平铺、生成 Mipmap），用于材质贴图；模板来自 Unity 生成的磐锋卫士.png.meta。"""
    text=re.sub(r"guid: \w+","guid: "+guid_for(path,g),TEXTURE_META,count=1)
    if "wrapU: 0" not in text or "enableMipMap: 1" not in text: raise SystemExit("纹理导入模板已变化")
    write(path+".meta",text)

# 熔岩地面：玄武岩板块 + 板块缝隙里的熔岩裂纹（部分裂纹已冷却变暗），1024×1024 可平铺。
N=1024; rng=np.random.default_rng(2310)
f1,f2=voronoi(N,46,rng); edge=f2-f1
heat=smooth(0.38,0.62,fbm(N,3,3,rng))
grain=fbm(N,16,4,rng); blotch=fbm(N,4,3,rng)
core=np.exp(-(edge/0.0028)**2)*heat; halo=np.exp(-edge/0.018)*heat
rim=1-np.exp(-edge/0.01)
plate=(0.085+0.05*grain+0.03*blotch)*(0.55+0.45*rim)
rgb=np.stack([plate*1.05,plate*0.86,plate*0.8],-1)
rgb+=core[...,None]*np.array([1.0,0.72,0.28])+halo[...,None]*np.array([0.75,0.2,0.04])*0.55
ground="Assets/GameData/表现/战场/熔岩地面.png"; save_rgb(rgb,ground); texture_meta(ground)
# 熔岩池：内切圆内是翻滚的熔岩与漂浮的暗色岩壳，圆边是冷却的岩壳（贴在扁圆柱顶面，圆柱顶面 UV 正好是内切圆）。
M=512; rng=np.random.default_rng(77)
y,x=np.mgrid[0:M,0:M]; r=np.hypot((x+0.5)/M-0.5,(y+0.5)/M-0.5)*2
flow=fbm(M,4,5,rng,0.55); crust=fbm(M,6,4,rng)
t=np.clip(flow*1.25-0.1,0,1)
lava=np.stack([0.55+0.45*smooth(0.1,0.5,t),0.08+0.5*smooth(0.3,0.85,t)+0.3*smooth(0.75,1,t),0.02+0.35*smooth(0.7,1,t)],-1)
crust_mask=np.clip(smooth(0.62,0.7,crust)*(0.35+0.65*smooth(0.4,0.85,r))+smooth(0.82,0.95,r),0,1)
crust_col=np.stack([0.16+0.06*crust,0.07+0.03*crust,0.05+0.02*crust],-1)
glow_edge=np.exp(-np.abs(crust-0.62)/0.02)[...,None]*np.array([0.9,0.35,0.05])*(r<0.9)[...,None]
pool=lava*(1-crust_mask[...,None])+crust_col*crust_mask[...,None]+glow_edge*0.6*crust_mask[...,None]
pool_tex="Assets/GameData/表现/战场/熔岩池.png"; save_rgb(pool,pool_tex); texture_meta(pool_tex)

# ---------- 材质（URP Lit / Unlit 模板来自 Assets/Game/Materials） ----------
LIT=open(ROOT+"/Assets/Game/Materials/GameShopStone.mat").read()
UNLIT=open(ROOT+"/Assets/Game/Materials/GameMarker.mat").read()
def c4(c): return "{r: %s, g: %s, b: %s, a: 1}"%tuple(round(v,4) for v in c)
def tex_ref(path): return "{fileID: 2800000, guid: %s, type: 3}"%guid_for(path,g)
def set_tex(text,prop,path,scale):
    old="    - %s:\n        m_Texture: {fileID: 0}\n        m_Scale: {x: 1, y: 1}\n"%prop
    if old not in text: raise SystemExit("材质模板缺少贴图属性 "+prop)
    return text.replace(old,"    - %s:\n        m_Texture: %s\n        m_Scale: {x: %s, y: %s}\n"%(prop,tex_ref(path),scale,scale))
def material(path,template,name,color,smoothness=None,metallic=None,base=None,scale=1,emission=None):
    old_name="GameShopStone" if template is LIT else "GameMarker"
    text=template.replace("m_Name: "+old_name,"m_Name: "+us(name))
    olds=["{r: 0.42, g: 0.44, b: 0.5, a: 1}"] if template is LIT else ["{r: 0.2, g: 0.9, b: 0.65, a: 1}","{r: 0.19999996, g: 0.9, b: 0.65, a: 1}"]
    for old in olds:
        if old not in text: raise SystemExit("材质模板颜色已变化："+old)
        text=text.replace(old,c4(color))
    if base:
        text=set_tex(text,"_BaseMap",base,scale); text=set_tex(text,"_MainTex",base,scale)
    if smoothness is not None: text=text.replace("    - _Smoothness: 0.2\n","    - _Smoothness: %s\n"%smoothness)
    if metallic is not None: text=text.replace("    - _Metallic: 0\n","    - _Metallic: %s\n"%metallic)
    if emission:
        tex,col=emission
        text=set_tex(text,"_EmissionMap",tex,scale)
        text=text.replace("    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}\n","    - _EmissionColor: %s\n"%c4(col))
        text=text.replace("  m_ValidKeywords: []\n","  m_ValidKeywords:\n  - _EMISSION\n").replace("  m_LightmapFlags: 4\n","  m_LightmapFlags: 2\n")
        if "_EMISSION" not in text or "m_LightmapFlags: 2" not in text: raise SystemExit("自发光设置失败："+path)
    write(path,text)
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))
    out[name]=guid_for(path,g)
B="Assets/GameData/表现/战场/"; S="Assets/GameData/表现/选择界面/"
# 地面 250 米见方（Plane 缩放 25），贴图平铺 5 次 = 每张 50 米；自发光用同一张贴图，只有熔岩裂纹明显发光。
material(B+"熔岩地面.mat",LIT,"熔岩地面",(1,1,1),smoothness=0.12,base=ground,scale=5,emission=(ground,(1,0.62,0.45)))
material(B+"熔岩池.mat",UNLIT,"熔岩池",(1,1,1),base=pool_tex)
material(B+"黑曜石.mat",LIT,"黑曜石",(0.13,0.09,0.17),smoothness=0.82,metallic=0.15)
material(B+"玄武岩.mat",LIT,"玄武岩",(0.2,0.15,0.14),smoothness=0.08)
material(S+"展示台.mat",LIT,"展示台",(0.2,0.21,0.25),smoothness=0.55,metallic=0.2)
material(S+"展示光环.mat",UNLIT,"展示光环",(1,0.78,0.32))
material(S+"展示地面.mat",LIT,"展示地面",(0.075,0.085,0.105),smoothness=0.35)

# ---------- 战场预览图（cairo，1016×572） ----------
W,H=1016,572
random.seed(611)
def poly(c,pts):
    c.new_path(); c.move_to(*pts[0])
    for p in pts[1:]: c.line_to(*p)
    c.close_path()
def ridge(c,y0,amp,step,color,seed,top=None):
    r=random.Random(seed); pts=[(-10,H)]; x=-10; y=y0
    while x<W+20:
        pts.append((x,y)); x+=r.uniform(step*0.5,step*1.3); y=y0-r.uniform(0,amp)
    pts.append((W+20,H)); poly(c,pts); c.set_source_rgba(*color); c.fill()
def glow(c,x,y,r,col,a):
    c.arc(x,y,r,0,2*math.pi); c.set_source(rad(x,y,0,r,[(0,col+(a,)),(1,col+(0,))])); c.fill()
def vignette(c,a=0.7):
    c.rectangle(0,0,W,H); c.set_source(rad(W/2,H*0.55,H*0.35,W*0.72,[(0,(0,0,0,0)),(1,(0,0,0,a))])); c.fill()
def tree(c,x,y,h,lw,angle=-90,depth=0,r=None):
    r=r or random.Random(int(x*7+y))
    x2=x+math.cos(math.radians(angle))*h; y2=y+math.sin(math.radians(angle))*h
    c.move_to(x,y); c.line_to(x2,y2); c.set_line_width(lw); c.set_line_cap(cairo.LINE_CAP_ROUND); c.set_source_rgba(0.02,0.035,0.04,1); c.stroke()
    if depth<4:
        for da in (-r.uniform(18,38),r.uniform(14,34)):
            tree(c,x2,y2,h*r.uniform(0.55,0.75),lw*0.62,angle+da,depth+1,r)
def grave(c,x,y,w,h):
    c.new_path(); c.move_to(x,y); c.line_to(x,y-h+w/2); c.arc(x+w/2,y-h+w/2,w/2,math.pi,0); c.line_to(x+w,y); c.close_path()
    c.set_source(lin(x,y-h,x+w,y,[(0,(0.3,0.36,0.36,1)),(1,(0.13,0.16,0.17,1))])); c.fill_preserve()
    c.set_source_rgba(0,0,0,0.6); c.set_line_width(1.5); c.stroke()
    c.move_to(x+w*0.5,y-h*0.7); c.line_to(x+w*0.5,y-h*0.35); c.move_to(x+w*0.3,y-h*0.58); c.line_to(x+w*0.7,y-h*0.58)
    c.set_source_rgba(0.06,0.08,0.09,0.8); c.set_line_width(2); c.stroke()
def eyes(c,x,y,col,s=3):
    for dx in (-s*1.6,s*1.6): glow(c,x+dx,y,s*3,col,0.7); c.arc(x+dx,y,s*0.6,0,2*math.pi); c.set_source_rgba(1,1,0.9,0.95); c.fill()

# 幽暗荒原：月夜下的灰绿荒原，枯树、墓碑与暗处的眼睛；中景是游戏里的商店（暖色灯光）。
s,c=surface(W,H)
c.rectangle(0,0,W,H); c.set_source(lin(0,0,0,300,[(0,(0.02,0.035,0.05,1)),(1,(0.11,0.19,0.19,1))])); c.fill()
for _ in range(160):
    c.arc(random.uniform(0,W),random.uniform(0,220),random.uniform(0.4,1.3),0,2*math.pi); c.set_source_rgba(0.85,0.95,1,random.uniform(0.2,0.7)); c.fill()
glow(c,760,96,150,(0.6,0.85,0.8),0.25); c.arc(760,96,40,0,2*math.pi); c.set_source(rad(748,86,4,42,[(0,(0.95,1,0.96,1)),(1,(0.7,0.82,0.78,1))])); c.fill()
for cx,cy,rr in ((748,84,7),(772,108,5),(760,74,4)): c.arc(cx,cy,rr,0,2*math.pi); c.set_source_rgba(0.55,0.68,0.64,0.45); c.fill()
ridge(c,250,70,46,(0.06,0.1,0.11,1),1)
ridge(c,290,46,36,(0.045,0.075,0.08,1),2)
c.rectangle(0,300,W,H-300); c.set_source(lin(0,300,0,H,[(0,(0.13,0.18,0.17,1)),(1,(0.07,0.095,0.095,1))])); c.fill()
for yy,a in ((304,0.16),(336,0.1),(380,0.07)):
    c.rectangle(0,yy-14,W,28); c.set_source(lin(0,yy-14,0,yy+14,[(0,(0.6,0.8,0.75,0)),(0.5,(0.6,0.8,0.75,a)),(1,(0.6,0.8,0.75,0))])); c.fill()
for _ in range(240):
    x,y=random.uniform(0,W),random.uniform(310,H); c.arc(x,y,random.uniform(0.6,2),0,2*math.pi); c.set_source_rgba(0.3,0.4,0.36,random.uniform(0.2,0.6)); c.fill()
# 商店：石台、木柱、布篷与金色灯光
sx,sy=520,372
glow(c,sx+40,sy-10,120,(1,0.75,0.35),0.35)
c.rectangle(sx-20,sy,120,16); c.set_source_rgba(0.32,0.34,0.38,1); c.fill()
for px in (sx-10,sx+82): c.rectangle(px,sy-58,7,58); c.set_source_rgba(0.36,0.24,0.14,1); c.fill()
poly(c,[(sx-24,sy-56),(sx+104,sy-56),(sx+86,sy-80),(sx-6,sy-80)]); c.set_source(lin(0,sy-80,0,sy-56,[(0,(0.55,0.16,0.14,1)),(1,(0.35,0.08,0.08,1))])); c.fill()
c.rectangle(sx+8,sy-26,64,26); c.set_source_rgba(0.42,0.29,0.17,1); c.fill()
glow(c,sx+40,sy-40,22,(1,0.85,0.4),0.9)
for gx in (sx+14,sx+30,sx+50): c.rectangle(gx,sy-34,10,8); c.set_source_rgba(0.95,0.78,0.3,1); c.fill()
for x,y,h,lw in ((90,420,120,10),(250,350,70,6),(880,450,140,12),(700,340,60,5),(40,330,55,5),(960,330,50,4)): tree(c,x,y,h,lw)
for x,y,w_,h_ in ((170,470,38,52),(226,488,30,40),(640,500,34,46),(760,420,26,34),(330,410,24,30),(930,520,40,54)): grave(c,x,y,w_,h_)
for x,y,col in ((140,380,(0.3,1,0.45)),(300,440,(1,0.25,0.2)),(820,390,(0.3,1,0.45)),(600,450,(1,0.25,0.2)),(410,360,(0.6,0.8,1)),(980,400,(1,0.25,0.2))): eyes(c,x,y,col,2.6)
for _ in range(36):
    x,y=random.uniform(0,W),random.uniform(320,H-20); glow(c,x,y,9,(0.5,1,0.6),0.5); c.arc(x,y,1.4,0,2*math.pi); c.set_source_rgba(0.85,1,0.8,0.9); c.fill()
c.rectangle(0,H-120,W,120); c.set_source(lin(0,H-120,0,H,[(0,(0,0,0,0)),(1,(0,0,0,0.45))])); c.fill()
vignette(c,0.72)
shadow="Assets/GameData/表现/战场/幽暗荒原.png"; save(s,shadow); sprite_meta(shadow,g); out["幽暗荒原"]=g[shadow]

# 熔火裂谷：暗红天空下的火山、熔岩河与黑曜石尖柱，空中飘着火星。
s,c=surface(W,H)
c.rectangle(0,0,W,H); c.set_source(lin(0,0,0,320,[(0,(0.05,0.015,0.015,1)),(0.7,(0.3,0.07,0.03,1)),(1,(0.55,0.17,0.05,1))])); c.fill()
for _ in range(14):
    x,y,rw=random.uniform(-50,W),random.uniform(20,200),random.uniform(80,200)
    c.save(); c.translate(x,y); c.scale(rw,rw*0.28); c.arc(0,0,1,0,2*math.pi); c.restore(); c.set_source_rgba(0.08,0.03,0.03,random.uniform(0.35,0.6)); c.fill()
# 火山与火山口光
glow(c,640,128,190,(1,0.4,0.1),0.4)
poly(c,[(380,330),(590,128),(690,128),(920,330)]); c.set_source(lin(0,128,0,330,[(0,(0.13,0.05,0.04,1)),(1,(0.08,0.03,0.03,1))])); c.fill()
c.save(); c.translate(640,128); c.scale(52,9); c.arc(0,0,1,0,2*math.pi); c.restore(); c.set_source_rgba(1,0.62,0.2,1); c.fill()
c.set_line_cap(cairo.LINE_CAP_ROUND)
for path_,w_ in (([(628,132),(612,190),(590,240),(560,318)],8),([(656,132),(670,180),(700,236),(742,320)],6)):
    for lw,col in ((w_*2.6,(1,0.35,0.05,0.35)),(w_,(1,0.6,0.15,1)),(w_*0.4,(1,0.9,0.5,1))):
        c.move_to(*path_[0]); c.curve_to(*path_[1],*path_[2],*path_[3]); c.set_line_width(lw); c.set_source_rgba(*col); c.stroke()
ridge(c,300,50,40,(0.09,0.035,0.03,1),5)
c.rectangle(0,320,W,H-320); c.set_source(lin(0,320,0,H,[(0,(0.14,0.06,0.045,1)),(1,(0.05,0.025,0.02,1))])); c.fill()
glow(c,W/2,330,420,(1,0.35,0.08),0.18)
# 熔岩河（近大远小）
for pts_,w_ in (([(-20,520),(200,470),(330,430),(520,400)],34),([(520,400),(640,380),(760,360),(1040,340)],22),([(330,430),(380,470),(470,520),(560,600)],26),([(760,360),(820,400),(900,430),(1040,470)],18)):
    for lw,col in ((w_*2.4,(1,0.3,0.05,0.3)),(w_,(0.95,0.36,0.06,1)),(w_*0.55,(1,0.66,0.18,1)),(w_*0.2,(1,0.92,0.6,1))):
        c.move_to(*pts_[0]); c.curve_to(*pts_[1],*pts_[2],*pts_[3]); c.set_line_width(lw); c.set_source_rgba(*col); c.stroke()
# 黑曜石尖柱
def spike(x,y,h,w_,lean):
    pts_=[(x-w_/2,y),(x+lean-w_*0.12,y-h),(x+lean+w_*0.12,y-h*0.96),(x+w_/2,y)]
    poly(c,pts_); c.set_source(lin(x-w_/2,y-h,x+w_/2,y,[(0,(0.28,0.2,0.36,1)),(0.5,(0.1,0.06,0.13,1)),(1,(0.04,0.02,0.05,1))])); c.fill_preserve()
    c.set_source_rgba(0.8,0.55,1,0.35); c.set_line_width(1.2); c.stroke()
    c.move_to(x-w_*0.05,y); c.line_to(x+lean,y-h*0.98); c.set_source_rgba(0.75,0.6,0.95,0.4); c.set_line_width(1); c.stroke()
for x,y,h,w_,lean in ((60,560,190,46,10),(110,560,120,34,-8),(20,560,90,30,4),(930,560,210,52,-12),(980,560,130,36,6),(880,560,100,30,10),(250,420,60,18,4),(770,410,70,20,-4),(430,380,40,12,2)):
    spike(x,y,h,w_,lean)
for _ in range(140):
    x,y=random.uniform(0,W),random.uniform(60,H); rr=random.uniform(0.8,2.6)
    glow(c,x,y,rr*4,(1,0.5,0.1),0.35); c.arc(x,y,rr,0,2*math.pi); c.set_source_rgba(1,random.uniform(0.5,0.85),0.2,random.uniform(0.5,1)); c.fill()
c.rectangle(0,H-110,W,110); c.set_source(lin(0,H-110,0,H,[(0,(0,0,0,0)),(1,(0,0,0,0.4))])); c.fill()
vignette(c,0.7)
ember="Assets/GameData/表现/战场/熔火裂谷.png"; save(s,ember); sprite_meta(ember,g); out["熔火裂谷"]=g[ember]
out["熔岩地面贴图"]=g[ground]; out["熔岩池贴图"]=g[pool_tex]
save_guids(g)
json.dump(out,open(os.path.join(HERE,"battlefields.json"),"w",newline="\n"),ensure_ascii=False,indent=1)
print("ok",len(out))
