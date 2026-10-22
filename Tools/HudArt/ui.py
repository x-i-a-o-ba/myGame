"""UGUI 预制体 YAML 生成器 + PIL 预览渲染。"""
import sys, os, json, uuid, random, math
import os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFont, ImageChops
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,"..",".."))
IMG="fe87c0e1cc204ed48ad3b37840f39efc"; TXT="5f7201a12d95ffc409449d95f23cf332"; BTN="4e29b1a8efbd4b44bb3f3716e73f07ff"
RAW="1344c3c82d62a2a41a3576d8abb8e3ea"; SHD="cfabb0440166ab443bba8876756fdfa9"; OUT="e19747de3f5aca642ab2be37e372fb86"
ACT="44e671ea4d15e5a4daaeaa73ccca3f35"; PNL="8fd3e243fd60e234fbe71139379cfa0e"
HOV="f7a1cd7eec5746c2b30856b6ee994c01"; TIP="30ff4094440a49f184efbf220926c74e"
FONT="{fileID: 12800000, guid: ab0786ada57caea46b95753ec47d65c0, type: 3}"
SPR=json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'sprites.json'))); ICO=json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'icons.json')))
SPRITE_DIR=ROOT+"/Assets/Game/UI/Sprites"; ICON_DIR=ROOT+"/Assets/Game/UI/Icons"
BORDERS={}
import re, hashlib
for name in SPR:
    meta=open("%s/%s.png.meta"%(SPRITE_DIR,name)).read()
    m=re.search(r"spriteBorder: \{x: (\d+), y: (\d+), z: (\d+), w: (\d+)\}",meta); BORDERS[name]=tuple(int(v) for v in m.groups())
rng=random.Random(20250707)
used=set()
def fid():
    while True:
        v=rng.randrange(10**15,2**62)
        if v not in used: used.add(v); return v
def sfid(key):
    """基于键哈希的稳定 fileID：不消耗全局随机序列，给已有面板追加组件时不改变其他对象的 ID。"""
    v=10**15+int(hashlib.sha1(key.encode("utf-8")).hexdigest(),16)%(2**62-10**15)
    while v in used: v+=1
    used.add(v); return v
def us(s): return '"'+''.join('\\u%04X'%ord(c) if ord(c)>127 else ('\\n' if c=='\n' else ('\\"' if c=='"' else c)) for c in s)+'"'
def col(c): return "{r: %s, g: %s, b: %s, a: %s}"%tuple(round(x,4) for x in c)
def v2(a): return "{x: %s, y: %s}"%tuple(round(x,4) for x in a)
HDR="  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
class Node:
    def __init__(s,name,parent=None,amin=(0,1),amax=(0,1),pos=(0,0),size=(0,0),pivot=(0,1),active=1,go=None,rt=None):
        s.name=name; s.go=go or fid(); s.rt=rt or fid(); s.cr=None; s.comps=[]; s.children=[]; s.parent=parent; s.active=active
        s.rect=(amin,amax,pos,size,pivot); s.draw=[]  # 预览指令
        if parent: parent.children.append(s)
    def add(s,kind,body):
        cid=fid(); s.comps.append((cid,kind,body)); return cid
    def need_cr(s):
        if s.cr is None: s.cr=fid()
def at(parent,name,x,y,w,h,active=1):
    return Node(name,parent,(0,1),(0,1),(x,-y),(w,h),(0,1),active)
def fill(parent,name,l=0,t=0,r=0,b=0,active=1):
    return Node(name,parent,(0,0),(1,1),((l-r)/2,(b-t)/2),(-(l+r),-(t+b)),(0.5,0.5),active)
def mono(go,script,ident,body):
    return HDR+"  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: %s\n%s"%(go,script,ident,body)
def sprite_ref(name):
    if name is None: return "{fileID: 0}"
    guid=SPR.get(name) or ICO.get(name)
    return "{fileID: 21300000, guid: %s, type: 3}"%guid
def image(n,sprite=None,color=(1,1,1,1),kind="simple",amount=1,origin=2,clockwise=1,raycast=0,preserve=0):
    n.need_cr()
    t={"simple":0,"sliced":1,"filled":3}[kind]
    cid=n.add("114",mono(n.go,IMG,"UnityEngine.UI::UnityEngine.UI.Image",
      "  m_Material: {fileID: 0}\n  m_Color: %s\n  m_RaycastTarget: %d\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: %s\n  m_Type: %d\n  m_PreserveAspect: %d\n  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: %s\n  m_FillClockwise: %d\n  m_FillOrigin: %d\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n"%(col(color),raycast,sprite_ref(sprite),t,preserve,amount,clockwise,origin)))
    n.draw.append(("image",sprite,color,kind,amount,origin,clockwise)); return cid
def raw(n,color=(1,1,1,1),preview=None,raycast=0):
    n.need_cr()
    cid=n.add("114",mono(n.go,RAW,"UnityEngine.UI::UnityEngine.UI.RawImage",
      "  m_Material: {fileID: 0}\n  m_Color: %s\n  m_RaycastTarget: %d\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Texture: {fileID: 0}\n  m_UVRect:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n"%(col(color),raycast)))
    n.draw.append(("raw",preview,color)); return cid
ALIGN={"UL":0,"UC":1,"UR":2,"ML":3,"MC":4,"MR":5,"LL":6,"LC":7,"LR":8}
def text(n,value,size,color,align="MC",bold=False,shadow=True,outline=False,wrap=False,spacing=1.0):
    n.need_cr()
    cid=n.add("114",mono(n.go,TXT,"UnityEngine.UI::UnityEngine.UI.Text",
      "  m_Material: {fileID: 0}\n  m_Color: %s\n  m_RaycastTarget: 0\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_FontData:\n    m_Font: %s\n    m_FontSize: %d\n    m_FontStyle: %d\n    m_BestFit: 0\n    m_MinSize: 10\n    m_MaxSize: 40\n    m_Alignment: %d\n    m_AlignByGeometry: 0\n    m_RichText: 1\n    m_HorizontalOverflow: %d\n    m_VerticalOverflow: 1\n    m_LineSpacing: %s\n  m_Text: %s\n"%(col(color),FONT,size,1 if bold else 0,ALIGN[align],0 if wrap else 1,spacing,us(value))))
    if outline: effect(n,OUT,(0,0,0,1),(1.5,-1.5))
    elif shadow: effect(n,SHD,(0,0,0,0.85),(1.2,-1.2))
    n.draw.append(("text",value,size,color,align,bold,spacing,wrap)); return cid
def effect(n,script,color,dist):
    ident="UnityEngine.UI::UnityEngine.UI.Outline" if script==OUT else "UnityEngine.UI::UnityEngine.UI.Shadow"
    n.add("114",mono(n.go,script,ident,"  m_EffectColor: %s\n  m_EffectDistance: %s\n  m_UseGraphicAlpha: 1\n"%(col(color),v2(dist))))
def button(n,graphic,command,value,pressed=0.72):
    n.add("114",mono(n.go,BTN,"UnityEngine.UI::UnityEngine.UI.Button",
      "  m_Navigation:\n    m_Mode: 0\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n  m_Transition: 1\n  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n    m_HighlightedColor: {r: 1.15, g: 1.1, b: 0.95, a: 1}\n    m_PressedColor: {r: %s, g: %s, b: %s, a: 1}\n    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}\n    m_DisabledColor: {r: 0.5, g: 0.5, b: 0.5, a: 0.6}\n    m_ColorMultiplier: 1\n    m_FadeDuration: 0.08\n  m_SpriteState:\n    m_HighlightedSprite: {fileID: 0}\n    m_PressedSprite: {fileID: 0}\n    m_SelectedSprite: {fileID: 0}\n    m_DisabledSprite: {fileID: 0}\n  m_AnimationTriggers:\n    m_NormalTrigger: Normal\n    m_HighlightedTrigger: Highlighted\n    m_PressedTrigger: Pressed\n    m_SelectedTrigger: Selected\n    m_DisabledTrigger: Disabled\n  m_Interactable: 1\n  m_TargetGraphic: {fileID: %d}\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"%(pressed,pressed,pressed,graphic)))
    n.add("114",mono(n.go,ACT,"Roguelike.Foundation::Roguelike.Foundation.UI.UIAction","  command: %s\n  value: %d\n"%(command,value)))
def hover(n,command,value):
    """悬停提示源 UIHoverAction；组件 ID 由节点与命令哈希得到，不影响同面板其他对象的 ID。"""
    cid=sfid("hover:%d:%s:%d"%(n.go,command,value))
    n.comps.append((cid,"114",mono(n.go,HOV,"Roguelike.Foundation::Roguelike.Foundation.UI.UIHoverAction","  command: %s\n  value: %d\n"%(command,value))))
    return cid
def tooltip(n,sections,padding=14,spacing=8,gap=12,margin=8):
    """提示框排版组件 UITooltip：sections 按显示顺序列出直接子节点。"""
    items="".join("\n  - {fileID: %d}"%x.rt for x in sections) if sections else " []"
    return n.add("114",mono(n.go,TIP,"Roguelike.Foundation::Roguelike.Foundation.UI.UITooltip",
      "  sections:%s\n  padding: %s\n  spacing: %s\n  gap: %s\n  margin: %s\n"%(items,padding,spacing,gap,margin)))
class Bindings:
    def __init__(s): s.texts=[]; s.images=[]; s.fills=[]; s.toggles=[]
    def text(s,key,cid): s.texts.append((key,cid)); return cid
    def image(s,key,cid): s.images.append((key,cid)); return cid
    def fill(s,key,cid,axis): s.fills.append((key,cid,axis)); return cid
    def toggle(s,key,node,invert=0): s.toggles.append((key,node.go,invert)); return node
def panel(root,pid,b,blocks=1):
    body="  panelId: %s\n  blocksPointer: %d\n"%(pid,blocks)
    def arr(name,items,fmt):
        return "  %s:%s\n"%(name," []" if not items else "\n"+"".join(fmt(i) for i in items).rstrip("\n"))
    body+=arr("texts",b.texts,lambda i:"  - key: %s\n    target: {fileID: %d}\n"%i)
    body+=arr("images",b.images,lambda i:"  - key: %s\n    target: {fileID: %d}\n"%i)
    body+=arr("fills",b.fills,lambda i:"  - key: %s\n    target: {fileID: %d}\n    axis: %d\n"%i)
    body+=arr("toggles",b.toggles,lambda i:"  - key: %s\n    target: {fileID: %d}\n    invert: %d\n"%i)
    root.add("114",mono(root.go,PNL,"Roguelike.Foundation::Roguelike.Foundation.UI.UIPanel",body))
def emit(n,out):
    comps=[("224",n.rt)]+([("222",n.cr)] if n.cr else [])+[(k,c) for c,k,_ in n.comps]
    out.append("--- !u!1 &%d\nGameObject:\n%s  serializedVersion: 6\n  m_Component:\n%s  m_Layer: 5\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: %d\n"%(
        n.go,HDR,"".join("  - component: {fileID: %d}\n"%c for _,c in comps),n.name,n.active))
    amin,amax,pos,size,pivot=n.rect
    out.append("--- !u!224 &%d\nRectTransform:\n%s  m_GameObject: {fileID: %d}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:%s\n  m_Father: {fileID: %d}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: %s\n  m_AnchorMax: %s\n  m_AnchoredPosition: %s\n  m_SizeDelta: %s\n  m_Pivot: %s\n"%(
        n.rt,HDR,n.go,("".join("\n  - {fileID: %d}"%c.rt for c in n.children) if n.children else " []"),n.parent.rt if n.parent else 0,v2(amin),v2(amax),v2(pos),v2(size),v2(pivot)))
    if n.cr: out.append("--- !u!222 &%d\nCanvasRenderer:\n%s  m_GameObject: {fileID: %d}\n  m_CullTransparentMesh: 1\n"%(n.cr,HDR,n.go))
    for c,k,body in n.comps: out.append("--- !u!%s &%d\nMonoBehaviour:\n%s"%(k,c,body))
    for ch in n.children: emit(ch,out)
def write(name,root):
    out=[]; emit(root,out)
    path=ROOT+"/Assets/Resources/UI/Panels/%s.prefab"%name
    open(path,"w").write("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"+"".join(out))
    meta=path+".meta"
    if not os.path.exists(meta):
        open(meta,"w").write("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%uuid.uuid4().hex)
    return path
# ---------------- 预览 ----------------
FONT_R="/usr/share/fonts/google-noto-cjk/NotoSansSC-Regular.otf"; FONT_B="/usr/share/fonts/google-noto-cjk/NotoSansSC-Bold.otf"
if not os.path.exists(FONT_R): FONT_R=FONT_B="/usr/share/fonts/google-noto-cjk/NotoSansSC-Medium.otf"
_cache={}
def load(name):
    if name in _cache: return _cache[name]
    p="%s/%s.png"%(SPRITE_DIR,name) if name in SPR else "%s/%s.png"%(ICON_DIR,name)
    im=Image.open(p).convert("RGBA"); _cache[name]=im; return im
def tint(im,c):
    r,g,b,a=im.split()
    f=lambda ch,k: ch.point(lambda v: min(255,int(v*k)))
    return Image.merge("RGBA",(f(r,c[0]),f(g,c[1]),f(b,c[2]),f(a,c[3])))
def sliced(im,w,h,border):
    l,b,r,t=border; W,H=im.size; w=max(1,int(round(w))); h=max(1,int(round(h)))
    if l+r>w: s=w/(l+r); l=int(l*s); r=int(r*s)
    if t+b>h: s=h/(t+b); t=int(t*s); b=int(b*s)
    out=Image.new("RGBA",(w,h))
    xs=[(0,l,0,l),(l,W-r,l,w-r),(W-r,W,w-r,w)]; ys=[(0,t,0,t),(t,H-b,t,h-b),(H-b,H,h-b,h)]
    for sx0,sx1,dx0,dx1 in xs:
        for sy0,sy1,dy0,dy1 in ys:
            if sx1<=sx0 or sy1<=sy0 or dx1<=dx0 or dy1<=dy0: continue
            out.paste(im.crop((sx0,sy0,sx1,sy1)).resize((dx1-dx0,dy1-dy0),Image.BILINEAR),(dx0,dy0))
    return out
_fonts={}
def font(bold,size):
    k=(bool(bold),int(size))
    if k not in _fonts: _fonts[k]=ImageFont.truetype(FONT_B if bold else FONT_R,int(size))
    return _fonts[k]
RICH=re.compile(r"(<color=#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?>|</color>|<b>|</b>|<size=\d+>|</size>|\n)")
def rich_lines(value,size,color,bold,width=None,spacing=1.0):
    """把 Unity 富文本（color / b / size）拆成逐行文字段；width 非空时按字符宽度自动换行。返回 (行列表, 行高列表)。"""
    cs=[color]; bs=[bold]; ss=[size]; lines=[[]]
    for t in RICH.split(value or ""):
        if not t: continue
        if t.startswith("<color="):
            h_=t[8:-1]; cs.append(tuple(int(h_[i:i+2],16)/255 for i in (0,2,4))+(1,))
        elif t=="</color>": cs.pop() if len(cs)>1 else None
        elif t=="<b>": bs.append(True)
        elif t=="</b>": bs.pop() if len(bs)>1 else None
        elif t.startswith("<size="): ss.append(int(t[6:-1]))
        elif t=="</size>": ss.pop() if len(ss)>1 else None
        elif t=="\n": lines.append([])
        else: lines[-1].append((t,cs[-1],bs[-1],ss[-1]))
    if width:
        probe=ImageDraw.Draw(Image.new("RGBA",(1,1))); wrapped=[]
        for line in lines:
            cur=[]; used_w=0
            for t,c_,b_,sz in line:
                f=font(b_,sz); buf=""
                for ch in t:
                    cw=probe.textlength(ch,font=f)
                    if used_w+cw>width and (cur or buf):
                        if buf: cur.append((buf,c_,b_,sz))
                        wrapped.append(cur); cur=[]; buf=""; used_w=0
                        if ch==" ": continue
                    buf+=ch; used_w+=cw
                if buf: cur.append((buf,c_,b_,sz))
            wrapped.append(cur)
        lines=wrapped
    heights=[max([sz for _t,_c,_b,sz in line] or [size])*1.448*spacing for line in lines]
    return lines,heights
def text_height(n,width):
    """预览用：按 Unity Text 的首选高度估算节点文字在给定宽度下的高度。"""
    for d in n.draw:
        if d[0]=="text":
            _,value,size,color,align,bold,spacing=d[:7]
            return sum(rich_lines(previews.get(n.go,value),size,color,bold,width,spacing)[1])
    return None
def render(root,W,H,parent_rect,toggles,path,bg=None):
    canvas=Image.open(bg).convert("RGBA").resize((W,H)) if bg else Image.new("RGBA",(W,H),(58,74,62,255))
    def rect_of(n,pr):
        amin,amax,pos,size,pivot=n.rect; px,py,pw,ph=pr
        if n.go in hfill: amax=(amin[0]+(amax[0]-amin[0])*hfill[n.go],amax[1])
        ax0,ay0=px+amin[0]*pw,py+amin[1]*ph; ax1,ay1=px+amax[0]*pw,py+amax[1]*ph
        w=ax1-ax0+size[0]; h=ay1-ay0+size[1]
        cx=ax0+(ax1-ax0)*pivot[0]+pos[0]; cy=ay0+(ay1-ay0)*pivot[1]+pos[1]
        return (cx-pivot[0]*w,cy-pivot[1]*h,w,h)
    def visit(n,pr,fills):
        if not n.active and not toggles.get(n.go,False): return
        if n.go in toggles and not toggles[n.go]: return
        r=rect_of(n,pr); x,y,w,h=r; top=H-(y+h)
        for d in n.draw:
            if d[0]=="image":
                _,spr,color,kind,amount,origin,cw=d
                amount=fills.get(n.go,amount)
                if w<1 or h<1: continue
                im=load(spr) if spr else Image.new("RGBA",(4,4),(255,255,255,255))
                im=sliced(im,w,h,BORDERS.get(spr,(0,0,0,0))) if kind=="sliced" else im.resize((max(1,int(w)),max(1,int(h))),Image.BILINEAR)
                im=tint(im,color)
                if kind=="filled" and amount<0.999:
                    mask=Image.new("L",im.size,0); dr=ImageDraw.Draw(mask)
                    start={2:-90,0:90,1:0,3:180}[origin]; ext=360*amount
                    dr.pieslice((-w,-h,2*w,2*h),start,start+ext,fill=255)
                    im.putalpha(ImageChops.multiply(im.split()[3],mask))
                canvas.alpha_composite(im,(int(x),int(top)))
            elif d[0]=="raw":
                _,prev,color=d
                if prev: im=tint(load(prev).resize((max(1,int(w)),max(1,int(h)))),color); canvas.alpha_composite(im,(int(x),int(top)))
            elif d[0]=="text":
                _,value,size,color,align,bold,spacing=d[:7]; wrap=d[7] if len(d)>7 else False
                value=previews.get(n.go,value)
                lines,heights=rich_lines(value,size,color,bold,w if wrap else None,spacing)
                th=sum(heights)
                dr=ImageDraw.Draw(canvas)
                v=align[0]; hz=align[1]
                y0=top if v=="U" else top+(h-th)/2 if v=="M" else top+h-th
                for line,lh in zip(lines,heights):
                    tw=sum(dr.textlength(t,font=font(b_,sz)) for t,_c,b_,sz in line)
                    x0=x if hz=="L" else x+(w-tw)/2 if hz=="C" else x+w-tw
                    big=max([sz for _t,_c,_b,sz in line] or [size])
                    for t,c_,b_,sz in line:
                        f=font(b_,sz); yy=y0+(lh-sz*1.2)/2+(big-sz)*0.9
                        dr.text((x0+1.2,yy+1.2),t,font=f,fill=(0,0,0,200))
                        dr.text((x0,yy),t,font=f,fill=tuple(int(min(1,c)*255) for c in c_))
                        x0+=dr.textlength(t,font=f)
                    y0+=lh
        for ch in n.children: visit(ch,(x,y,w,h),fills)
    toggles_map=[]
    visit(root,parent_rect,fill_preview)
    canvas.convert("RGB").save(path)
previews={}; fill_preview={}; hfill={}
