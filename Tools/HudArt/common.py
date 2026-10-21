import cairo, math, json, os, uuid, random
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(HERE,"..",".."))
GUIDS=os.path.join(HERE,"guids.json")
def load_guids():
    return json.load(open(GUIDS)) if os.path.exists(GUIDS) else {}
def save_guids(g):
    json.dump(g,open(GUIDS,"w"),ensure_ascii=False,indent=1)
def guid_for(path,g):
    if path not in g: g[path]=uuid.uuid4().hex
    return g[path]
def folder_meta(path,g):
    meta=os.path.join(ROOT,path)+".meta"
    if os.path.exists(meta): return
    open(meta,"w").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))
TEMPLATE=open(ROOT+"/Assets/Art/effect/Kenney粒子/Rotated/flame_05_rotated.png.meta").read()
def sprite_meta(path,g,border=(0,0,0,0),wrap=1):
    """border: (left,bottom,right,top) 像素，对应 Unity spriteBorder x,y,z,w。"""
    text=TEMPLATE.replace("guid: 80cfff09f03245edadbf286adb38f8b9","guid: "+guid_for(path,g))
    text=text.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}","spriteBorder: {x: %d, y: %d, z: %d, w: %d}"%border)
    text=text.replace("spriteMeshType: 1","spriteMeshType: 0")
    text=text.replace("textureCompression: 1","textureCompression: 0")
    text=text.replace("spriteID: 5e97eb03825dee720800000000000000","spriteID: "+uuid.uuid5(uuid.NAMESPACE_URL,path).hex)
    if wrap!=1: text=text.replace("    wrapU: 1\n    wrapV: 1","    wrapU: %d\n    wrapV: %d"%(wrap,wrap))
    open(os.path.join(ROOT,path)+".meta","w").write(text)
def surface(w,h):
    s=cairo.ImageSurface(cairo.FORMAT_ARGB32,w,h); c=cairo.Context(s); c.set_antialias(cairo.ANTIALIAS_BEST); return s,c
def hexc(h,a=1.0):
    h=h.lstrip('#'); return (int(h[0:2],16)/255,int(h[2:4],16)/255,int(h[4:6],16)/255,a)
def rrect(c,x,y,w,h,r):
    r=min(r,w/2,h/2)
    c.new_sub_path(); c.arc(x+w-r,y+r,r,-math.pi/2,0); c.arc(x+w-r,y+h-r,r,0,math.pi/2)
    c.arc(x+r,y+h-r,r,math.pi/2,math.pi); c.arc(x+r,y+r,r,math.pi,1.5*math.pi); c.close_path()
def lin(x0,y0,x1,y1,stops):
    p=cairo.LinearGradient(x0,y0,x1,y1)
    for o,col in stops: p.add_color_stop_rgba(o,*col)
    return p
def rad(cx,cy,r0,r1,stops):
    p=cairo.RadialGradient(cx,cy,r0,cx,cy,r1)
    for o,col in stops: p.add_color_stop_rgba(o,*col)
    return p
def save(s,path):
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True); s.write_to_png(full)
