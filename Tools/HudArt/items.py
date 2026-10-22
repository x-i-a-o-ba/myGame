# 装备图标、装备定义资源与装备库生成脚本（2026-10-22 新增）。
# 运行：cd Tools/HudArt && python3 items.py；GUID 保存在 guids.json，重复运行结果稳定。
# 图标 128×128，边框颜色 = 品质色（普通/精良/稀有/史诗/传说）；资源只保存配置，数值说明见 Docs/equipment-and-stats.md。
import sys, os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from common import *
from ui import us
g=load_guids(); random.seed(11); N=128
ICONS="Assets/Game/Equipment/Icons"; ITEMS="Assets/Game/Equipment/Items"; CATALOG="Assets/Game/Equipment/装备库.asset"
DEFINITION_SCRIPT="56e54fc344be4a05a6443f96e0f98886"; CATALOG_SCRIPT="17057a8115734595ba72fc838e743761"
SLOW_EFFECT="72282bc99f34428428ee275e7bff9ac4"  # Skills/Foundation/Effects/施加扇击减速.asset
QUALITY=['#C8D0D8','#6BE07A','#4FA8FF','#C77DFF','#FFB347']  # 与 CombatAbilityTooltip.QualityColor 一致

# ---------- 绘图辅助 ----------
def ell(c,cx,cy,rx,ry):
    c.save(); c.translate(cx,cy); c.scale(rx,ry); c.arc(0,0,1,0,2*math.pi); c.restore()
def path(c,pts,close=True):
    c.new_path(); c.move_to(*pts[0])
    for p in pts[1:]:
        if len(p)==6: c.curve_to(*p)
        else: c.line_to(*p)
    if close: c.close_path()
def glow(c,x,y,r,col,a=0.8):
    c.arc(x,y,r,0,2*math.pi); c.set_source(rad(x,y,0,r,[(0,col[:3]+(a,)),(1,col[:3]+(0,))])); c.fill()
def facet(c,pts,light,dark,edge=(1,1,1,0.8),lw=1.2):
    xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
    path(c,pts); c.set_source(lin(min(xs),min(ys),max(xs),max(ys),[(0,light),(1,dark)])); c.fill_preserve()
    c.set_source_rgba(*edge); c.set_line_width(lw); c.stroke()
def spark(c,x,y,r,col=(1,1,0.95,1)):
    glow(c,x,y,r*1.8,col,0.7)
    path(c,[(x,y-r),(x+r*0.22,y-r*0.22),(x+r,y),(x+r*0.22,y+r*0.22),(x,y+r),(x-r*0.22,y+r*0.22),(x-r,y),(x-r*0.22,y-r*0.22)]); c.set_source_rgba(*col); c.fill()
def backdrop(c,inner,outer,cx=0.5,cy=0.42):
    c.rectangle(0,0,N,N); c.set_source(rad(N*cx,N*cy,N*0.02,N*0.8,[(0,hexc(inner)),(1,hexc(outer))])); c.fill()
    for _ in range(150):
        x,y=random.random()*N,random.random()*N; c.rectangle(x,y,1,1); c.set_source_rgba(1,1,1,random.random()*0.05); c.fill()
def finish(c,q):
    """暗角 + 品质色边框（外沿 3px 实线、内侧柔光、角部高光）。"""
    c.rectangle(0,0,N,N); c.set_source(rad(N/2,N/2,N*0.38,N*0.78,[(0,(0,0,0,0)),(1,(0,0,0,0.6))])); c.fill()
    col=hexc(QUALITY[q])
    for k in range(6):
        rrect(c,4+k,4+k,N-8-2*k,N-8-2*k,12); c.set_line_width(1.2); c.set_source_rgba(col[0],col[1],col[2],0.22*(1-k/6)); c.stroke()
    rrect(c,2.5,2.5,N-5,N-5,13); c.set_line_width(3.4); c.set_source_rgba(col[0],col[1],col[2],1); c.stroke()
    rrect(c,1,1,N-2,N-2,14); c.set_line_width(1.2); c.set_source_rgba(0,0,0,0.7); c.stroke()
    for x,y in ((12,12),(N-12,12),(12,N-12),(N-12,N-12)): glow(c,x,y,6+q,col,0.35+0.08*q)
def steel(x0,y0,x1,y1,hi='#f2f6fc',mid='#9aa6b8',lo='#3a4250'):
    return lin(x0,y0,x1,y1,[(0,hexc(mid)),(0.45,hexc(hi)),(0.55,hexc(mid)),(1,hexc(lo))])
def sword(c,cx,cy,ang,L,W,blade=('#f2f6fc','#9aa6b8','#3a4250'),guard=('#e8c070','#6a4410'),grip='#5a3418',pommel=('#f6dc8a','#7a5010')):
    c.save(); c.translate(cx,cy); c.rotate(ang)
    tip,base=-L*0.6,L*0.12
    path(c,[(0,tip),(W/2,tip+W*1.3),(W/2,base),(-W/2,base),(-W/2,tip+W*1.3)])
    c.set_source(steel(-W/2,0,W/2,0,*blade)); c.fill()
    c.move_to(0,tip+W); c.line_to(0,base); c.set_line_width(1.3); c.set_source_rgba(1,1,1,0.55); c.stroke()
    rrect(c,-W*1.7,base,W*3.4,W*0.7,W*0.3); c.set_source(lin(0,base,0,base+W*0.7,[(0,hexc(guard[0])),(1,hexc(guard[1]))])); c.fill()
    gl=L*0.2; y=base+W*0.7
    c.rectangle(-W*0.3,y,W*0.6,gl); c.set_source(lin(-W*0.3,0,W*0.3,0,[(0,hexc(grip)),(0.5,(0.75,0.5,0.3,1)),(1,hexc(grip))])); c.fill()
    for k in range(4):
        c.move_to(-W*0.3,y+gl*(k+0.5)/4-1.5); c.line_to(W*0.3,y+gl*(k+0.5)/4+1.5); c.set_line_width(1.2); c.set_source_rgba(0.15,0.08,0.03,0.8); c.stroke()
    c.arc(0,y+gl+W*0.38,W*0.48,0,2*math.pi); c.set_source(rad(-W*0.15,y+gl+W*0.25,0,W*0.6,[(0,hexc(pommel[0])),(1,hexc(pommel[1]))])); c.fill()
    c.restore()

# ---------- 18 件装备图标 ----------
def draw_iron_sword(c):
    backdrop(c,'#56667a','#0c1016'); glow(c,64,64,46,(0.7,0.8,0.95),0.25)
    sword(c,66,62,math.pi/4,124,15)
def draw_swift_gloves(c):
    backdrop(c,'#6a5a2a','#120e04'); glow(c,70,64,44,(1,0.9,0.4),0.3)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    for y,x0,w in ((44,14,30),(62,8,40),(80,16,28)):
        c.move_to(x0,y); c.line_to(x0+w,y); c.set_line_width(4); c.set_source_rgba(1,0.92,0.5,0.75); c.stroke()
    c.save(); c.translate(72,66); c.rotate(0.28)
    for k,(x,h) in enumerate(((-16,34),(-5,40),(6,38),(17,30))):
        rrect(c,x-5,-24-h+16,10.5,h,5.2); c.set_source(lin(x-5,0,x+5,0,[(0,hexc('#8a3a1c')),(0.5,hexc('#d4763c')),(1,hexc('#6a2a12'))])); c.fill()
    path(c,[(-22,-14),(24,-14),(26,18),(-20,18)]); c.set_source(lin(-22,0,26,0,[(0,hexc('#7a3418')),(0.45,hexc('#cc6a34')),(1,hexc('#5a220e'))])); c.fill()
    path(c,[(-22,-4),(-38,-22),(-44,-14),(-30,8),(-20,12)]); c.set_source(lin(-44,-22,-20,12,[(0,hexc('#d4763c')),(1,hexc('#6a2a12'))])); c.fill()
    rrect(c,-26,16,56,18,5); c.set_source(lin(0,16,0,34,[(0,hexc('#f2cc6a')),(1,hexc('#8a5a14'))])); c.fill()
    for k in range(5):
        c.arc(-18+k*11,25,1.8,0,2*math.pi); c.set_source_rgba(0.35,0.2,0.04,1); c.fill()
    c.move_to(-18,-6); c.curve_to(-6,-2,8,-2,20,-6); c.set_line_width(1.4); c.set_dash([3,3]); c.set_source_rgba(1,0.85,0.5,0.8); c.stroke(); c.set_dash([])
    c.restore()
def draw_leather_vest(c):
    backdrop(c,'#6a4a2c','#120a04'); glow(c,64,66,48,(1,0.8,0.5),0.22)
    outline=[(34,20),(50,20),(52,34,76,34,78,20),(94,20),(110,42),(100,56),(100,106),(86,118,42,118,28,106),(28,56),(18,42)]
    path(c,outline); c.set_source(rad(54,58,6,76,[(0,hexc('#c08452')),(0.6,hexc('#8a5428')),(1,hexc('#3e2008'))])); c.fill_preserve()
    c.set_line_width(2.2); c.set_source_rgba(0.18,0.08,0.02,1); c.stroke()
    path(c,outline); c.set_line_width(1.2); c.set_dash([3,3.5]); c.save(); c.translate(0,0); c.set_source_rgba(1,0.85,0.6,0.55); c.stroke(); c.restore(); c.set_dash([])
    c.move_to(64,36); c.line_to(64,114); c.set_line_width(2.5); c.set_source_rgba(0.2,0.09,0.02,0.9); c.stroke()
    for k in range(5):
        y=46+k*12; c.move_to(58,y); c.line_to(70,y+6); c.move_to(70,y); c.line_to(58,y+6); c.set_line_width(1.6); c.set_source_rgba(0.95,0.85,0.6,0.85); c.stroke()
    for x in (36,92):
        rrect(c,x-6,60,12,14,2); c.set_source(lin(0,60,0,74,[(0,hexc('#e6c88a')),(1,hexc('#7a5a2a'))])); c.fill()
    rrect(c,28,86,72,8,2); c.set_source_rgba(0.25,0.12,0.04,0.9); c.fill()
    rrect(c,58,84,12,12,2); c.set_source(lin(0,84,0,96,[(0,hexc('#f2d488')),(1,hexc('#8a6420'))])); c.fill()
def draw_boots_of_speed(c):
    backdrop(c,'#7a3a22','#140804'); glow(c,66,70,46,(1,0.6,0.35),0.25)
    for k,(ang,L) in enumerate(((-2.2,40),(-2.55,44),(-2.9,38))):
        c.save(); c.translate(42,42); c.rotate(ang)
        path(c,[(0,-5),(L*0.6,-9,L*0.95,-5,L,0),(L*0.9,6,L*0.5,8,0,5)]); c.set_source(lin(0,0,L,0,[(0,(1,1,1,0.95)),(1,(0.75,0.85,0.95,0.9))])); c.fill()
        c.move_to(4,0); c.line_to(L*0.92,0); c.set_line_width(1); c.set_source_rgba(0.5,0.6,0.7,0.8); c.stroke()
        c.restore()
    boot=[(44,18),(80,18),(80,66),(94,70,108,80,110,94),(110,104),(36,104),(40,64)]
    path(c,boot); c.set_source(lin(36,0,110,0,[(0,hexc('#6a2010')),(0.35,hexc('#c0482a')),(0.7,hexc('#9a3418')),(1,hexc('#4a1208'))])); c.fill_preserve()
    c.set_line_width(2); c.set_source_rgba(0.15,0.04,0.01,1); c.stroke()
    rrect(c,40,14,44,14,4); c.set_source(lin(0,14,0,28,[(0,hexc('#f2d27a')),(1,hexc('#8a5c14'))])); c.fill()
    rrect(c,32,102,82,9,3); c.set_source(lin(0,102,0,111,[(0,hexc('#3a2418')),(1,hexc('#120a06'))])); c.fill()
    c.move_to(80,70); c.curve_to(70,78,62,90,62,102); c.set_line_width(1.6); c.set_source_rgba(1,0.75,0.55,0.6); c.stroke()
    for y in (40,52): c.move_to(48,y); c.line_to(76,y); c.set_line_width(1.4); c.set_source_rgba(1,0.8,0.55,0.45); c.stroke()
def draw_mana_gem(c):
    backdrop(c,'#2a4a9a','#040818'); glow(c,64,64,52,(0.35,0.6,1),0.55)
    o=[(64,16),(98,40),(90,98),(64,114),(38,98),(30,40)]
    path(c,o); c.set_source(lin(30,16,98,114,[(0,hexc('#b8e0ff')),(0.5,hexc('#3a7ae0')),(1,hexc('#0e2a7a'))])); c.fill()
    facet(c,[(64,16),(98,40),(64,50),(30,40)],(0.9,0.97,1,1),(0.45,0.7,1,1),(1,1,1,0.7),1)
    facet(c,[(30,40),(64,50),(64,114),(38,98)],(0.35,0.62,1,1),(0.08,0.2,0.6,1),(0.8,0.9,1,0.5),1)
    facet(c,[(98,40),(90,98),(64,114),(64,50)],(0.2,0.45,0.95,1),(0.04,0.12,0.45,1),(0.8,0.9,1,0.5),1)
    path(c,o); c.set_line_width(2); c.set_source_rgba(0.85,0.95,1,0.9); c.stroke()
    spark(c,48,32,8); spark(c,92,88,5,(0.8,0.95,1,1))
def draw_ring_of_regen(c):
    backdrop(c,'#2e5e2e','#061006'); glow(c,64,58,48,(0.45,1,0.45),0.35)
    c.set_fill_rule(cairo.FILL_RULE_EVEN_ODD)
    ell(c,64,80,40,28); ell(c,64,84,27,16); c.set_source(lin(24,0,104,0,[(0,hexc('#8a5a10')),(0.3,hexc('#ffe08a')),(0.6,hexc('#d8a030')),(1,hexc('#6a4008'))])); c.fill()
    c.set_fill_rule(cairo.FILL_RULE_WINDING)
    ell(c,64,80,40,28); c.set_line_width(1.2); c.set_source_rgba(0.3,0.18,0.02,0.9); c.stroke()
    for x in (52,76):
        path(c,[(x,62),(x+(64-x)*0.4,44),(x+(64-x)*0.6,46)]); c.set_source_rgba(0.95,0.78,0.3,1); c.fill()
    ell(c,64,44,18,20); c.set_source(rad(58,36,2,24,[(0,hexc('#e8ffd8')),(0.4,hexc('#5ee070')),(1,hexc('#0e5a1e'))])); c.fill()
    ell(c,64,44,18,20); c.set_line_width(2); c.set_source_rgba(0.95,0.8,0.35,1); c.stroke()
    for x,y,r in ((26,34,6),(100,30,7),(96,106,5)):
        c.rectangle(x-r*0.3,y-r,r*0.6,r*2); c.rectangle(x-r,y-r*0.3,r*2,r*0.6); c.set_source_rgba(0.6,1,0.6,0.85); c.fill()
    spark(c,56,36,5)
def draw_scout_scope(c):
    backdrop(c,'#2e5050','#060e0e'); glow(c,84,44,30,(0.7,1,1),0.35)
    c.save(); c.translate(64,66); c.rotate(-math.pi/4)
    for x0,x1,r,brass in ((-52,-20,9,False),(-22,10,12,True),(8,40,15,False),(38,52,19,True)):
        rrect(c,x0,-r,x1-x0,2*r,3); c.set_source(lin(0,-r,0,r,[(0,hexc('#ffe7a0') if brass else hexc('#7a4a2a')),(0.4,hexc('#c8902a') if brass else hexc('#4a2a14')),(1,hexc('#5a3a08') if brass else hexc('#1e0e04'))])); c.fill()
        c.rectangle(x0,-r,2.5,2*r); c.set_source_rgba(1,0.9,0.6,0.7); c.fill()
    ell(c,53,0,5,17); c.set_source(rad(53,-6,1,18,[(0,hexc('#f0ffff')),(0.5,hexc('#6ad0e0')),(1,hexc('#0a3a4a'))])); c.fill()
    c.restore()
    spark(c,96,30,9,(0.9,1,1,1))
def draw_vampire_fang(c):
    backdrop(c,'#661420','#100204'); glow(c,64,70,46,(1,0.2,0.25),0.4)
    fang=[(46,18),(70,14,88,28,86,44),(84,70,72,94,60,110),(62,88,56,62,44,46),(38,36,40,24,46,18)]
    path(c,fang); c.set_source(lin(40,14,88,110,[(0,hexc('#fffaf0')),(0.5,hexc('#e8dcc0')),(1,hexc('#9a8a6a'))])); c.fill_preserve()
    c.set_line_width(2); c.set_source_rgba(0.3,0.2,0.1,0.9); c.stroke()
    path(c,[(50,22),(66,20,78,30,78,42),(74,40,62,30,50,22)]); c.set_source_rgba(1,1,1,0.6); c.fill()
    path(c,[(76,66),(72,82,66,96,60,110),(62,96,64,84,64,76)]); c.set_source_rgba(0.75,0.04,0.08,0.9); c.fill()
    path(c,[(68,100),(74,110,76,116,70,120),(64,122,62,114,68,100)]); c.set_source(rad(68,114,1,9,[(0,hexc('#ff6a6a')),(1,hexc('#7a0008'))])); c.fill()
    for x,y in ((30,84),(96,96),(98,40)): glow(c,x,y,7,(1,0.25,0.3),0.6)
def draw_lucky_dagger(c):
    backdrop(c,'#2e6040','#06100a'); glow(c,60,60,46,(0.5,1,0.6),0.25)
    sword(c,62,66,-math.pi/4,104,13,blade=('#f6fff8','#a8c0b0','#3a5044'),guard=('#7ae08a','#1e6a2e'),grip='#3a2a18',pommel=('#9af0a8','#1e6a2e'))
    for k in range(4):
        a=k*math.pi/2+math.pi/4; x,y=96+math.cos(a)*8,96+math.sin(a)*8
        ell(c,x,y,7,7); c.set_source(rad(x-2,y-2,1,8,[(0,hexc('#b8ffb0')),(1,hexc('#1e8a2a'))])); c.fill()
    c.move_to(96,96); c.curve_to(100,104,104,108,110,110); c.set_line_width(2); c.set_source_rgba(0.2,0.6,0.25,1); c.stroke()
    spark(c,30,30,11,(1,0.95,0.6,1))
def draw_sage_tome(c):
    backdrop(c,'#4c3c70','#0c0814'); glow(c,64,62,50,(0.8,0.6,1),0.3)
    rrect(c,36,24,64,86,5); c.set_source(lin(0,24,0,110,[(0,hexc('#f2ead0')),(1,hexc('#b8a878'))])); c.fill()
    for k in range(5): c.move_to(96,30+k*16); c.line_to(100,30+k*16); c.set_line_width(1); c.set_source_rgba(0.5,0.4,0.25,0.7); c.stroke()
    rrect(c,26,20,68,90,6); c.set_source(lin(26,20,94,110,[(0,hexc('#5a3aa8')),(0.6,hexc('#2e1a6a')),(1,hexc('#140a34'))])); c.fill()
    rrect(c,26,20,12,90,4); c.set_source(lin(26,0,38,0,[(0,hexc('#1a0e40')),(1,hexc('#4a2e90'))])); c.fill()
    for y in (32,98): rrect(c,26,y,12,6,2); c.set_source(lin(0,y,0,y+6,[(0,hexc('#ffe08a')),(1,hexc('#9a6a14'))])); c.fill()
    for x,y in ((88,26),(88,104)): c.arc(x,y,4,0,2*math.pi); c.set_source_rgba(0.95,0.78,0.3,1); c.fill()
    glow(c,64,64,22,(1,0.85,0.4),0.6)
    c.arc(64,64,14,0,2*math.pi); c.set_line_width(2.4); c.set_source_rgba(1,0.85,0.4,1); c.stroke()
    path(c,[(64,52),(67,61),(76,64),(67,67),(64,76),(61,67),(52,64),(61,61)]); c.set_source_rgba(1,0.95,0.7,1); c.fill()
    spark(c,100,22,7,(1,0.95,0.75,1)); spark(c,108,48,4,(1,0.95,0.75,1))
def draw_guardian_armor(c):
    backdrop(c,'#3e4e62','#080c12'); glow(c,64,64,50,(0.6,0.75,1),0.3)
    plate=[(30,28),(48,22),(56,34,72,34,80,22),(98,28),(108,52),(96,58),(94,96),(84,112,44,112,34,96),(32,58),(20,52)]
    path(c,plate); c.set_source(lin(20,0,108,0,[(0,hexc('#4a5668')),(0.35,hexc('#e6eef8')),(0.5,hexc('#9aa8bc')),(1,hexc('#2a3240'))])); c.fill_preserve()
    c.set_line_width(2.2); c.set_source_rgba(0.08,0.1,0.14,1); c.stroke()
    for sgn in (-1,1):
        path(c,[(64+sgn*34,28),(64+sgn*46,52),(64+sgn*32,56),(64+sgn*24,36)]); c.set_source_rgba(0.12,0.16,0.24,0.55); c.fill()
    c.move_to(64,36); c.line_to(64,108); c.set_line_width(2); c.set_source_rgba(1,1,1,0.45); c.stroke()
    path(c,[(64,52),(80,58),(78,78,72,88,64,94),(56,88,50,78,48,58)]); c.set_source(lin(48,52,80,94,[(0,hexc('#7ab8ff')),(1,hexc('#1a3a8a'))])); c.fill_preserve()
    c.set_line_width(2); c.set_source_rgba(0.95,0.8,0.35,1); c.stroke()
    for x,y in ((40,64),(88,64),(42,92),(86,92)): c.arc(x,y,2.6,0,2*math.pi); c.set_source_rgba(0.9,0.94,1,0.9); c.fill()
def draw_focus_amulet(c):
    backdrop(c,'#2e3e72','#060a18'); glow(c,64,74,44,(0.45,0.8,1),0.45)
    for k in range(13):
        t=k/12; a=math.pi*(1.05+0.9*t); x,y=64+math.cos(a)*46,52+math.sin(a)*38
        ell(c,x,y,3.4,2.4); c.set_line_width(1.4); c.set_source_rgba(0.95,0.8,0.4,0.95); c.stroke()
    c.arc(64,76,30,0,2*math.pi); c.set_source(lin(34,46,94,106,[(0,hexc('#ffe9a0')),(0.5,hexc('#d29a2a')),(1,hexc('#6a4208'))])); c.fill()
    c.arc(64,76,22,0,2*math.pi); c.set_source(rad(58,68,2,26,[(0,hexc('#d8f6ff')),(0.5,hexc('#3aa0e0')),(1,hexc('#0a2a6a'))])); c.fill()
    path(c,[(54,62),(74,62),(64,76)]); path(c,[(64,76),(74,90),(54,90)]); c.set_source_rgba(1,0.95,0.75,0.95)
    path(c,[(54,62),(74,62),(64,76)]); c.fill(); path(c,[(64,76),(74,90),(54,90)]); c.fill()
    c.rectangle(52,59,24,3); c.rectangle(52,90,24,3); c.set_source_rgba(0.95,0.8,0.4,1); c.fill()
    rrect(c,58,40,12,10,3); c.set_source_rgba(0.95,0.8,0.4,1); c.fill()
    spark(c,50,66,5,(0.9,1,1,1))
def draw_frostwhisper_bow(c):
    backdrop(c,'#2e64b0','#040c1e',0.5,0.4); glow(c,64,64,52,(0.5,0.85,1),0.45)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    c.move_to(30,22); c.curve_to(64,30,98,64,106,98); c.set_line_width(1.6); c.set_source_rgba(0.9,0.97,1,0.85); c.stroke()
    c.move_to(30,22); c.curve_to(30,70,58,106,106,98); c.set_line_width(9); c.set_source(lin(30,22,106,98,[(0,hexc('#e8fbff')),(0.5,hexc('#5ab4f0')),(1,hexc('#1a4aa0'))])); c.stroke()
    c.move_to(30,22); c.curve_to(30,70,58,106,106,98); c.set_line_width(2.5); c.set_source_rgba(1,1,1,0.6); c.stroke()
    for x,y,a in ((34,52,-2.6),(52,84,-2.2),(80,100,-1.8)):
        c.save(); c.translate(x,y); c.rotate(a); facet(c,[(0,-3),(14,0),(0,3)],(1,1,1,1),(0.4,0.75,1,1),(1,1,1,0.8),0.8); c.restore()
    c.move_to(34,94); c.line_to(96,32); c.set_line_width(2.4); c.set_source_rgba(0.92,0.97,1,1); c.stroke()
    facet(c,[(96,32),(88,34),(94,40)],(1,1,1,1),(0.4,0.8,1,1),(1,1,1,0.9),1)
    path(c,[(96,32),(84,36),(92,44)]); c.set_source_rgba(0.95,1,1,1); c.fill()
    for s in (-1,1): path(c,[(34,94),(40+s*4,88-s*4),(44+s*4,92-s*4),(38,98)]); c.set_source_rgba(0.6,0.85,1,0.9); c.fill()
    c.set_line_width(2)
    for k in range(6):
        a=k*math.pi/3; c.move_to(96,96); c.line_to(96+math.cos(a)*10,96+math.sin(a)*10)
    c.set_source_rgba(0.95,1,1,1); c.stroke(); glow(c,96,96,14,(0.7,0.95,1),0.6)
def draw_arcane_staff(c):
    backdrop(c,'#3e2e80','#08051a'); glow(c,88,40,40,(0.75,0.5,1),0.6)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    c.move_to(26,110); c.line_to(80,52); c.set_line_width(8); c.set_source(lin(26,110,80,52,[(0,hexc('#2a1608')),(0.5,hexc('#8a5a30')),(1,hexc('#4a2a10'))])); c.stroke()
    for t in (0.3,0.62): x,y=26+54*t,110-58*t; c.arc(x,y,5.5,0,2*math.pi); c.set_source_rgba(0.95,0.78,0.35,1); c.fill()
    for a in (-0.4,0.6,1.6):
        c.move_to(80,52); c.curve_to(80+math.cos(a-1.2)*18,52+math.sin(a-1.2)*18,88+math.cos(a)*20,40+math.sin(a)*20,88+math.cos(a)*16,40+math.sin(a)*16)
        c.set_line_width(4); c.set_source_rgba(0.9,0.72,0.3,1); c.stroke()
    c.arc(88,40,14,0,2*math.pi); c.set_source(rad(84,35,1,16,[(0,hexc('#ffffff')),(0.35,hexc('#d6a8ff')),(0.8,hexc('#7a3ae0')),(1,hexc('#2a0a6a'))])); c.fill()
    c.set_line_width(1.6)
    for k in range(3):
        a=k*2*math.pi/3; c.arc(88,40,22+k*3,a,a+1.6); c.set_source_rgba(0.85,0.7,1,0.8-k*0.2); c.stroke()
    spark(c,108,22,6,(0.95,0.85,1,1)); spark(c,60,30,4,(0.95,0.85,1,1)); spark(c,104,64,4,(0.95,0.85,1,1))
def draw_berserker_axe(c):
    backdrop(c,'#701c1c','#120404'); glow(c,70,52,48,(1,0.35,0.2),0.45)
    c.save(); c.translate(64,64); c.rotate(math.pi/5)
    rrect(c,-4.5,-46,9,104,4); c.set_source(lin(-4,0,4,0,[(0,hexc('#2a1408')),(0.5,hexc('#8a5228')),(1,hexc('#2a1408'))])); c.fill()
    for y in (22,32,42): c.rectangle(-5,y,10,3); c.set_source_rgba(0.6,0.12,0.08,1); c.fill()
    for sgn in (-1,1):
        blade=[(sgn*4,-40),(sgn*26,-56,sgn*44,-48,sgn*50,-30),(sgn*56,-14,sgn*50,4,sgn*44,14),(sgn*34,0,sgn*20,-8,sgn*4,-12)]
        path(c,blade); c.set_source(lin(sgn*4,0,sgn*52,0,[(0,hexc('#3a4048')),(0.6,hexc('#c8d0dc')),(1,hexc('#f6f8ff'))])); c.fill_preserve()
        c.set_line_width(1.8); c.set_source_rgba(0.08,0.06,0.06,1); c.stroke()
        c.move_to(sgn*50,-30); c.curve_to(sgn*56,-14,sgn*50,4,sgn*44,14); c.set_line_width(2.6); c.set_source_rgba(1,0.35,0.25,0.95); c.stroke()
        path(c,[(sgn*18,-34),(sgn*28,-30),(sgn*22,-20)]); c.set_source_rgba(0.85,0.15,0.1,0.9); c.fill()
    path(c,[(0,-60),(6,-46),(-6,-46)]); c.set_source(steel(-6,0,6,0)); c.fill()
    c.restore()
    for x,y in ((24,30),(104,92),(100,26)): glow(c,x,y,8,(1,0.4,0.2),0.7)
def draw_titan_heart(c):
    backdrop(c,'#5e1c3c','#10040a'); glow(c,64,66,52,(1,0.3,0.2),0.45)
    heart=[(64,110),(20,82,14,44,38,32),(52,26,62,34,64,44),(66,34,76,26,90,32),(114,44,108,82,64,110)]
    path(c,heart); c.set_source(rad(52,54,4,64,[(0,hexc('#9a8a84')),(0.55,hexc('#5a4a48')),(1,hexc('#1e1414'))])); c.fill_preserve()
    c.set_line_width(2.4); c.set_source_rgba(0.08,0.04,0.04,1); c.stroke()
    c.save(); path(c,heart); c.clip()
    glow(c,64,70,34,(1,0.4,0.15),0.7)
    c.set_line_cap(cairo.LINE_CAP_ROUND); c.set_line_join(cairo.LINE_JOIN_ROUND)
    for pts in (((64,44),(58,58),(68,70),(60,86),(64,104)),((58,58),(40,56),(30,48)),((68,70),(86,62),(98,50)),((60,86),(46,84)),((68,70),(80,84),(84,92))):
        c.move_to(*pts[0])
        for p in pts[1:]: c.line_to(*p)
        c.set_line_width(5); c.set_source_rgba(1,0.45,0.1,0.5); c.stroke_preserve(); c.set_line_width(2); c.set_source_rgba(1,0.9,0.5,1); c.stroke()
    c.restore()
    path(c,[(36,40),(46,32,56,34,60,42),(50,38,42,40,36,48)]); c.set_source_rgba(1,1,1,0.35); c.fill()
def draw_bloodthirst_blade(c):
    backdrop(c,'#5e0a1c','#100206'); glow(c,60,60,48,(1,0.15,0.25),0.45)
    c.save(); c.translate(64,64); c.rotate(math.pi/4)
    blade=[(-6,30),(-8,0,-10,-30,-2,-56),(6,-62),(10,-40,12,-10,8,30)]
    path(c,blade); c.set_source(lin(-10,0,12,0,[(0,hexc('#4a0612')),(0.45,hexc('#e0384a')),(0.6,hexc('#a01428')),(1,hexc('#3a040c'))])); c.fill_preserve()
    c.set_line_width(1.6); c.set_source_rgba(0.12,0.01,0.03,1); c.stroke()
    c.move_to(9,30); c.curve_to(12,-10,10,-40,6,-62); c.set_line_width(1.6); c.set_source_rgba(1,0.75,0.75,0.9); c.stroke()
    path(c,[(-20,30),(22,30),(18,38),(-16,38)]); c.set_source(lin(0,30,0,38,[(0,hexc('#2a2a2e')),(1,hexc('#0a0a0c'))])); c.fill()
    for sgn in (-1,1): path(c,[(sgn*20,30),(sgn*28,22),(sgn*24,36)]); c.set_source_rgba(0.75,0.1,0.15,1); c.fill()
    c.rectangle(-4,38,8,18); c.set_source(lin(-4,0,4,0,[(0,hexc('#1a0a0a')),(0.5,hexc('#5a1a1a')),(1,hexc('#1a0a0a'))])); c.fill()
    c.arc(0,60,6,0,2*math.pi); c.set_source(rad(-2,58,1,7,[(0,hexc('#ff8a8a')),(1,hexc('#7a0010'))])); c.fill()
    c.restore()
    for x,y,r in ((92,98,5),(100,108,3.5),(30,40,4)):
        path(c,[(x,y-r*1.8),(x+r,y),(x,y+r),(x-r,y)]); c.set_source_rgba(0.85,0.05,0.1,1); c.fill(); ell(c,x,y+0.2,r,r); c.fill()
def draw_storm_crown(c):
    backdrop(c,'#6a4c14','#120a02',0.5,0.55); glow(c,64,58,54,(0.5,0.75,1),0.45)
    c.set_line_join(cairo.LINE_JOIN_ROUND)
    for pts,w in ((((40,8),(50,30),(42,32),(54,52)),3.2),(((90,6),(80,26),(88,28),(76,48)),3.2)):
        c.move_to(*pts[0])
        for p in pts[1:]: c.line_to(*p)
        c.set_line_width(w*2.6); c.set_source_rgba(0.5,0.8,1,0.35); c.stroke_preserve(); c.set_line_width(w); c.set_source_rgba(0.92,0.98,1,1); c.stroke()
    crown=[(22,96),(18,50),(40,70),(52,38),(64,60),(76,38),(88,70),(110,50),(106,96)]
    path(c,crown); c.set_source(lin(18,38,110,96,[(0,hexc('#fff2b0')),(0.4,hexc('#f2b838')),(0.8,hexc('#a86a0e')),(1,hexc('#5a3604'))])); c.fill_preserve()
    c.set_line_width(2); c.set_source_rgba(0.3,0.16,0.02,1); c.stroke()
    rrect(c,20,90,88,16,4); c.set_source(lin(0,90,0,106,[(0,hexc('#ffe48a')),(1,hexc('#8a5608'))])); c.fill_preserve(); c.set_line_width(1.6); c.set_source_rgba(0.3,0.16,0.02,1); c.stroke()
    for x,y,r in ((18,50,5),(52,38,5),(76,38,5),(110,50,5)): c.arc(x,y,r,0,2*math.pi); c.set_source(rad(x-1.5,y-1.5,0.5,r,[(0,hexc('#e8f6ff')),(1,hexc('#2a6ad0'))])); c.fill()
    facet(c,[(64,66),(74,78),(64,90),(54,78)],(0.85,0.95,1,1),(0.15,0.4,0.95,1),(1,1,1,0.9),1.2); glow(c,64,78,16,(0.5,0.8,1),0.6)
    for x in (36,92): c.arc(x,98,3.6,0,2*math.pi); c.set_source_rgba(0.85,0.15,0.2,1); c.fill()
    spark(c,104,24,6,(0.9,0.97,1,1))

# ---------- 装备表：数值直接加到英雄属性上（百分比属性填百分点）----------
# StatKind：0 最大生命 1 攻击力 2 攻击速度% 3 移动速度 4 生命回复 5 技能伤害% 6 冷却缩减% 7 经验获取% 8 最大法力 9 法力回复
#           10 攻击距离 11 伤害减免% 12 普攻吸血% 13 暴击几率% 14 暴击伤害%
ITEM_TABLE=[
 # 资源名, stableId, 品质, 价格, 唯一, 属性, 命中效果, 绘制, 说明
 ("铁剑","item.iron_sword",0,350,0,[(1,12)],[],draw_iron_sword,"朴实可靠的铁剑，直接提高攻击力。"),
 ("迅捷手套","item.swift_gloves",0,400,0,[(2,15)],[],draw_swift_gloves,"轻薄的鹿皮手套，拉弓放箭更快。"),
 ("皮甲背心","item.leather_vest",0,350,0,[(0,150)],[],draw_leather_vest,"鞣制皮革缝成的背心，提高最大生命。"),
 ("速度之靴","item.boots_of_speed",0,450,1,[(3,1.5)],[],draw_boots_of_speed,"轻快的长靴，提高移动速度。同名装备只能持有一件。"),
 ("法力宝石","item.mana_gem",0,400,0,[(8,100),(9,1.5)],[],draw_mana_gem,"蕴含法力的蓝宝石，提高法力上限与法力回复。"),
 ("回复戒指","item.ring_of_regen",0,350,0,[(4,4)],[],draw_ring_of_regen,"镶嵌翠玉的戒指，持续回复生命。"),
 ("侦察镜","item.scout_scope",1,500,0,[(10,3)],[],draw_scout_scope,"黄铜望远镜：看得更远，也射得更远。"),
 ("吸血獠牙","item.vampire_fang",1,600,0,[(12,8)],[],draw_vampire_fang,"夜行兽的獠牙。普攻命中时按造成的伤害回复生命。"),
 ("幸运匕首","item.lucky_dagger",1,650,0,[(13,10)],[],draw_lucky_dagger,"带着四叶草坠饰的匕首，普攻有几率暴击。"),
 ("贤者之书","item.sage_tome",1,500,1,[(7,25)],[],draw_sage_tome,"写满前人心得的古书，获得的经验更多。同名装备只能持有一件。"),
 ("守护铠甲","item.guardian_armor",1,900,0,[(11,10),(0,100)],[],draw_guardian_armor,"厚重的钢制胸甲，减免受到的所有伤害。"),
 ("专注护符","item.focus_amulet",1,800,0,[(6,10),(8,50)],[],draw_focus_amulet,"刻着沙漏纹的护符，缩短技能冷却。"),
 ("霜语长弓","item.frostwhisper_bow",2,2200,1,[(1,25),(2,20)],[SLOW_EFFECT],draw_frostwhisper_bow,"以永冻寒冰雕成的长弓，普攻命中时施加扇击减速。同名装备只能持有一件。"),
 ("奥术法杖","item.arcane_staff",2,2000,0,[(5,30),(8,150),(9,3)],[],draw_arcane_staff,"镶嵌奥术宝珠的法杖，大幅强化技能伤害。"),
 ("狂战巨斧","item.berserker_axe",3,2800,0,[(1,45),(13,15),(14,50)],[],draw_berserker_axe,"沉重的双刃巨斧，暴击更频繁、更致命。"),
 ("泰坦之心","item.titan_heart",3,3000,0,[(0,600),(4,12),(11,8)],[],draw_titan_heart,"远古泰坦的心脏化石，赋予惊人的生命力。"),
 ("嗜血之刃","item.bloodthirst_blade",3,2600,1,[(1,30),(12,18)],[],draw_bloodthirst_blade,"渴血的弯刃，普攻造成大量吸血。同名装备只能持有一件。"),
 ("风暴王冠","item.storm_crown",4,4500,1,[(6,20),(5,40),(2,20),(3,2)],[],draw_storm_crown,"雷霆加冕的王冠，全面强化技能、攻速与移速。同名装备只能持有一件。"),
]

def write(path,text):
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True)
    open(full,"w",encoding="utf-8",newline="\n").write(text)
def native_meta(path):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))
def num(v): return ("%d"%v) if float(v).is_integer() else ("%g"%v)
HEADER="""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
"""
for folder in ("Assets/Game/Equipment",ICONS,ITEMS):
    os.makedirs(os.path.join(ROOT,folder),exist_ok=True); folder_meta(folder,g)
ledger=[]; refs=[]
for index,(name,stable,quality,price,unique,stats,effects,draw,desc) in enumerate(ITEM_TABLE):
    s,c=surface(N,N); draw(c); finish(c,quality)
    icon="%s/%s.png"%(ICONS,name); save(s,icon); sprite_meta(icon,g)
    asset="%s/%s.asset"%(ITEMS,name); cid=6000+index
    body=HEADER+"  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"%DEFINITION_SCRIPT
    body+="  m_Name: %s\n  m_EditorClassIdentifier: Roguelike.Abilities.Core::Roguelike.Abilities.EquipmentDefinition\n"%us(name)
    body+="  configId: %d\n  stableId: %s\n  displayName: %s\n"%(cid,stable,us(name))
    body+="  icon: {fileID: 2800000, guid: %s, type: 3}\n"%guid_for(icon,g)
    body+="  quality: %d\n  price: %d\n  unique: %d\n  description: %s\n"%(quality,price,unique,us(desc))
    body+="  stats:\n"+"".join("  - stat: %d\n    value: %s\n"%(k,num(v)) for k,v in stats)
    body+=("  onHitEffects:\n"+"".join("  - {fileID: 11400000, guid: %s, type: 2}\n"%e for e in effects)) if effects else "  onHitEffects: []\n"
    write(asset,body); native_meta(asset); refs.append(guid_for(asset,g))
    ledger.append({"id":cid,"category":"item","assetKey":"guid:11400000","stableId":stable,"path":asset,"name":name,"retired":False,"guid":guid_for(asset,g)})
body=HEADER+"  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"%CATALOG_SCRIPT
body+="  m_Name: %s\n  m_EditorClassIdentifier: Roguelike.Abilities.Core::Roguelike.Abilities.EquipmentCatalog\n"%us("装备库")
body+="  items:\n"+"".join("  - {fileID: 11400000, guid: %s, type: 2}\n"%r for r in refs)
body+="  slotCount: 6\n  startingGold: 300\n  sellRatio: 0.5\n"
write(CATALOG,body); native_meta(CATALOG)
save_guids(g)
json.dump({"catalog":guid_for(CATALOG,g),"items":ledger},open(os.path.join(HERE,"items.json"),"w"),ensure_ascii=False,indent=1)
# 预览：所有图标排成一张总览图（preview/ 不入库）
os.makedirs(os.path.join(HERE,"preview"),exist_ok=True)
sheet=cairo.ImageSurface(cairo.FORMAT_ARGB32,6*136+8,3*136+8); sc=cairo.Context(sheet); sc.set_source_rgb(0.08,0.09,0.1); sc.paint()
for i,(name,*_) in enumerate(ITEM_TABLE):
    img=cairo.ImageSurface.create_from_png(os.path.join(ROOT,ICONS,name+".png")); sc.set_source_surface(img,8+(i%6)*136,8+(i//6)*136); sc.paint()
sheet.write_to_png(os.path.join(HERE,"preview","装备图标.png"))
print(len(ITEM_TABLE),"items")
