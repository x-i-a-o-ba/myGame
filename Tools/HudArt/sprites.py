import sys; import os; sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from common import *
g=load_guids()
D="Assets/Game/UI/Sprites"
BRONZE_HI=hexc('#e2c47c'); BRONZE=hexc('#a8823f'); BRONZE_LO=hexc('#5a4220'); DARK=hexc('#07090b')
out={}
def emit(name,s,border=(0,0,0,0)):
    path="%s/%s.png"%(D,name); save(s,path); sprite_meta(path,g,border); out[name]=g[path]

# 1. HUD 主体：深色石板渐变 + 青铜描边 + 内高光，9 宫格
s,c=surface(128,128)
rrect(c,0,0,128,128,10); c.set_source(lin(0,0,0,128,[(0,hexc('#262b31')),(0.45,hexc('#15191d')),(1,hexc('#0b0d10'))])); c.fill()
# 细微横纹质感
for y in range(4,124,3):
    c.set_source_rgba(1,1,1,0.012 if y%2 else 0.02); c.rectangle(4,y,120,1); c.fill()
rrect(c,1,1,126,126,9.5); c.set_line_width(2); c.set_source_rgba(*DARK); c.stroke()
rrect(c,3,3,122,122,8); c.set_line_width(2.4); c.set_source(lin(0,0,0,128,[(0,BRONZE_HI),(0.5,BRONZE),(1,BRONZE_LO)])); c.stroke()
rrect(c,5.5,5.5,117,117,6.5); c.set_line_width(1); c.set_source_rgba(1,0.95,0.8,0.10); c.stroke()
emit("hud_body",s,(20,20,20,20))

# 2. 内嵌底板：更暗、内阴影
s,c=surface(64,64)
rrect(c,0,0,64,64,6); c.set_source(lin(0,0,0,64,[(0,hexc('#06080a')),(1,hexc('#11151a'))])); c.fill()
rrect(c,0.5,0.5,63,63,6); c.set_line_width(1); c.set_source_rgba(0,0,0,0.9); c.stroke()
rrect(c,1.5,1.5,61,61,5); c.set_line_width(1); c.set_source(lin(0,0,0,64,[(0,(0,0,0,0.6)),(1,(0.55,0.6,0.65,0.22))])); c.stroke()
emit("hud_plate",s,(10,10,10,10))

# 3. 技能槽框：透明中心，青铜斜面
s,c=surface(128,128)
c.set_fill_rule(cairo.FILL_RULE_EVEN_ODD)
rrect(c,0,0,128,128,8); rrect(c,9,9,110,110,3); c.set_source(lin(0,0,128,128,[(0,BRONZE_HI),(0.35,BRONZE),(0.7,BRONZE_LO),(1,hexc('#c39b52'))])); c.fill()
c.set_fill_rule(cairo.FILL_RULE_WINDING)
rrect(c,0.75,0.75,126.5,126.5,8); c.set_line_width(1.5); c.set_source_rgba(0,0,0,0.95); c.stroke()
rrect(c,8.5,8.5,111,111,3); c.set_line_width(1.6); c.set_source_rgba(0,0,0,0.95); c.stroke()
rrect(c,3,3,122,122,6); c.set_line_width(1); c.set_source_rgba(1,0.93,0.7,0.35); c.stroke()
# 内阴影
for i in range(6):
    rrect(c,10+i,10+i,108-2*i,108-2*i,3); c.set_line_width(1); c.set_source_rgba(0,0,0,0.35-i*0.055); c.stroke()
emit("slot_frame",s,(16,16,16,16))

# 4. 技能槽底：空槽深色径向
s,c=surface(64,64)
c.rectangle(0,0,64,64); c.set_source(rad(32,26,2,46,[(0,hexc('#1f2a33')),(1,hexc('#07090c'))])); c.fill()
emit("slot_back",s)

# 5. 纯白：纯色填充与 Filled 冷却遮罩
s,c=surface(8,8); c.rectangle(0,0,8,8); c.set_source_rgba(1,1,1,1); c.fill(); emit("white",s,(2,2,2,2))

# 6. 条形槽：深色凹槽
s,c=surface(64,20)
rrect(c,0,0,64,20,4); c.set_source(lin(0,0,0,20,[(0,hexc('#020304')),(1,hexc('#0e1114'))])); c.fill()
rrect(c,0.5,0.5,63,19,4); c.set_line_width(1); c.set_source_rgba(0.6,0.5,0.3,0.55); c.stroke()
emit("bar_back",s,(6,6,6,6))

# 7. 条形填充：白色竖向光泽，运行时由 Image 颜色着色
s,c=surface(64,20)
c.rectangle(0,0,64,20); c.set_source(lin(0,0,0,20,[(0,(1,1,1,1)),(0.18,(0.97,0.97,0.97,1)),(0.5,(0.82,0.82,0.82,1)),(0.52,(0.72,0.72,0.72,1)),(1,(0.55,0.55,0.55,1))])); c.fill()
c.rectangle(0,1,64,1.5); c.set_source_rgba(1,1,1,0.55); c.fill()
c.rectangle(63,0,1,20); c.set_source_rgba(1,1,1,0.9); c.fill()
emit("bar_fill",s,(3,3,3,3))

# 8. 圆环与圆盘：经验环（径向 Filled）与通用圆形
s,c=surface(128,128); c.arc(64,64,58,0,2*math.pi); c.arc_negative(64,64,49,2*math.pi,0); c.set_source_rgba(1,1,1,1); c.fill(); emit("ring",s)
s,c=surface(128,128); c.arc(64,64,62,0,2*math.pi); c.set_source_rgba(1,1,1,1); c.fill(); emit("disc",s)

# 9. 等级徽章：深色圆盘 + 青铜边
s,c=surface(96,96)
c.arc(48,48,46,0,2*math.pi); c.set_source(lin(0,0,0,96,[(0,BRONZE_HI),(0.5,BRONZE),(1,BRONZE_LO)])); c.fill()
c.arc(48,48,40,0,2*math.pi); c.set_source(rad(48,40,4,42,[(0,hexc('#2a3038')),(1,hexc('#0a0c0f'))])); c.fill()
c.arc(48,48,40,0,2*math.pi); c.set_line_width(1.5); c.set_source_rgba(0,0,0,0.9); c.stroke()
emit("level_badge",s)

# 10. 等级点：亮/暗
s,c=surface(24,10)
rrect(c,1,1,22,8,2); c.set_source(lin(0,0,0,10,[(0,hexc('#fff1b0')),(0.5,hexc('#f2c14e')),(1,hexc('#a8771f'))])); c.fill()
rrect(c,1,1,22,8,2); c.set_line_width(1); c.set_source_rgba(0,0,0,0.8); c.stroke(); emit("pip_on",s)
s,c=surface(24,10)
rrect(c,1,1,22,8,2); c.set_source(lin(0,0,0,10,[(0,hexc('#0b0d10')),(1,hexc('#1c2127'))])); c.fill()
rrect(c,1,1,22,8,2); c.set_line_width(1); c.set_source_rgba(0.5,0.42,0.25,0.8); c.stroke(); emit("pip_off",s)

# 11. 按钮：青铜斜面暗色按钮
s,c=surface(96,40)
rrect(c,0,0,96,40,6); c.set_source(lin(0,0,0,40,[(0,BRONZE_HI),(1,BRONZE_LO)])); c.fill()
rrect(c,2,2,92,36,5); c.set_source(lin(0,0,0,40,[(0,hexc('#33383f')),(0.5,hexc('#1d2126')),(1,hexc('#101317'))])); c.fill()
rrect(c,2.5,2.5,91,35,5); c.set_line_width(1); c.set_source_rgba(1,1,1,0.08); c.stroke()
rrect(c,0.5,0.5,95,39,6); c.set_line_width(1); c.set_source_rgba(0,0,0,0.9); c.stroke()
emit("button",s,(10,10,10,10))

# 12. 学习“+”按钮：金色箭头底 + 加号（Dota 2 升级技能按钮）
s,c=surface(48,32)
rrect(c,2,2,44,28,6); c.set_source(lin(0,0,0,32,[(0,hexc('#fff0a8')),(0.45,hexc('#f0b93a')),(1,hexc('#9a6512'))])); c.fill()
rrect(c,2,2,44,28,6); c.set_line_width(1.5); c.set_source_rgba(0.25,0.15,0.02,1); c.stroke()
c.set_source_rgba(0.22,0.12,0.02,1)
c.rectangle(21,7,6,18); c.fill(); c.rectangle(15,13,18,6); c.fill()
c.set_source_rgba(1,1,1,0.35); rrect(c,4,4,40,9,4); c.fill()
emit("learn_plus",s)

# 13. 柔光：待选天赋、可学习技能的提示光晕
s,c=surface(128,128); c.rectangle(0,0,128,128); c.set_source(rad(64,64,0,64,[(0,(1,1,1,0.9)),(0.45,(1,1,1,0.35)),(1,(1,1,1,0))])); c.fill(); emit("glow",s)

# 14. 顶栏：梯形深色牌匾 + 青铜边
s,c=surface(640,120)
def trap(c,inset):
    c.new_path(); c.move_to(0+inset,0); c.line_to(640-inset,0); c.line_to(560-inset*0.6,120-inset); c.line_to(80+inset*0.6,120-inset); c.close_path()
trap(c,0); c.set_source(lin(0,0,0,120,[(0,BRONZE_LO),(0.6,BRONZE),(1,BRONZE_HI)])); c.fill()
trap(c,4); c.set_source(lin(0,0,0,120,[(0,hexc('#0b0d10')),(0.6,hexc('#181c21')),(1,hexc('#222830'))])); c.fill()
trap(c,4); c.set_line_width(1); c.set_source_rgba(0,0,0,0.9); c.stroke()
emit("topbar",s)

# 15. 底部渐暗：名称条
s,c=surface(16,64); c.rectangle(0,0,16,64); c.set_source(lin(0,0,0,64,[(0,(0,0,0,0)),(1,(0,0,0,0.92))])); c.fill(); emit("shade_bottom",s,(0,0,0,0))

# 16. 快捷键角标
s,c=surface(32,32)
rrect(c,1,1,30,30,4); c.set_source_rgba(0.02,0.03,0.04,0.88); c.fill()
rrect(c,1.5,1.5,29,29,4); c.set_line_width(1); c.set_source_rgba(0.85,0.75,0.5,0.7); c.stroke()
emit("hotkey",s,(8,8,8,8))

# 17. 天赋选项：深色条 + 细青铜边
s,c=surface(128,48)
rrect(c,0,0,128,48,6); c.set_source(lin(0,0,0,48,[(0,hexc('#1d2329')),(1,hexc('#0c0f12'))])); c.fill()
rrect(c,0.5,0.5,127,47,6); c.set_line_width(1); c.set_source_rgba(0.66,0.51,0.25,0.9); c.stroke()
rrect(c,2,2,124,44,5); c.set_line_width(1); c.set_source_rgba(1,1,1,0.05); c.stroke()
emit("talent_option",s,(14,14,14,14))

# 18. 提示条：两端渐隐的深色底
s,c=surface(256,48)
c.rectangle(0,0,256,48); c.set_source(lin(0,0,256,0,[(0,(0,0,0,0)),(0.25,(0.02,0.03,0.04,0.82)),(0.75,(0.02,0.03,0.04,0.82)),(1,(0,0,0,0))])); c.fill()
c.rectangle(0,0,256,1.5); c.set_source(lin(0,0,256,0,[(0,(0.9,0.75,0.4,0)),(0.5,(0.9,0.75,0.4,0.85)),(1,(0.9,0.75,0.4,0))])); c.fill()
c.rectangle(0,46.5,256,1.5); c.set_source(lin(0,0,256,0,[(0,(0.9,0.75,0.4,0)),(0.5,(0.9,0.75,0.4,0.85)),(1,(0.9,0.75,0.4,0))])); c.fill()
emit("hint_back",s,(80,4,80,4))

# 19. 首领血条框
s,c=surface(128,32)
rrect(c,0,0,128,32,5); c.set_source(lin(0,0,0,32,[(0,BRONZE_HI),(1,BRONZE_LO)])); c.fill()
rrect(c,3,3,122,26,3); c.set_source_rgba(0.02,0.02,0.03,1); c.fill()
emit("boss_frame",s,(12,12,12,12))

# 20. 选中高亮：金色描边发光框（天赋已选、攻击瞄准中）
s,c=surface(64,64)
for i,a in enumerate((0.12,0.22,0.4,0.9)):
    rrect(c,4-i,4-i,56+2*i,56+2*i,7+i); c.set_line_width(2); c.set_source_rgba(1,1,1,a); c.stroke()
rrect(c,5,5,54,54,6); c.set_source_rgba(1,1,1,0.12); c.fill()
emit("highlight",s,(16,16,16,16))

# ---------- 2026-10-22 选中目标信息面板 ----------
CRIMSON_HI=hexc('#d0624a'); CRIMSON=hexc('#8a2418'); CRIMSON_LO=hexc('#3a0c08')
# 21. 目标面板底：深色石板 + 外青铜内暗红双描边，9 宫格
s,c=surface(128,128)
rrect(c,0,0,128,128,12); c.set_source(lin(0,0,0,128,[(0,hexc('#241a1a')),(0.35,hexc('#16131a')),(1,hexc('#0a0b0e'))])); c.fill()
for y in range(4,124,3):
    c.set_source_rgba(1,1,1,0.012 if y%2 else 0.02); c.rectangle(4,y,120,1); c.fill()
rrect(c,1,1,126,126,11.5); c.set_line_width(2); c.set_source_rgba(*DARK); c.stroke()
rrect(c,3,3,122,122,10); c.set_line_width(2.4); c.set_source(lin(0,0,0,128,[(0,BRONZE_HI),(0.5,BRONZE),(1,BRONZE_LO)])); c.stroke()
rrect(c,6.5,6.5,115,115,7.5); c.set_line_width(1.4); c.set_source(lin(0,0,0,128,[(0,CRIMSON_HI[:3]+(0.85,)),(0.5,CRIMSON[:3]+(0.6,)),(1,CRIMSON_LO[:3]+(0.4,))])); c.stroke()
rrect(c,8.5,8.5,111,111,6); c.set_line_width(1); c.set_source_rgba(1,0.9,0.8,0.06); c.stroke()
emit("target_frame",s,(22,22,22,22))

# 22. 目标面板头部光带：左侧暗红辉光向右渐隐（直接拉伸）
s,c=surface(256,64)
c.rectangle(0,0,256,64); c.set_source(lin(0,0,256,0,[(0,(0.62,0.1,0.06,0.55)),(0.45,(0.4,0.06,0.04,0.22)),(1,(0.2,0.03,0.02,0))])); c.fill()
c.rectangle(0,0,256,64); c.set_source(lin(0,0,0,64,[(0,(0,0,0,0)),(0.7,(0,0,0,0)),(1,(0,0,0,0.35))])); c.fill()
emit("target_header",s)

# 23. 头像框：透明中心，青铜外框 + 暗红内沿
s,c=surface(128,128)
c.set_fill_rule(cairo.FILL_RULE_EVEN_ODD)
rrect(c,0,0,128,128,10); rrect(c,10,10,108,108,5); c.set_source(lin(0,0,128,128,[(0,BRONZE_HI),(0.35,BRONZE),(0.7,BRONZE_LO),(1,hexc('#c39b52'))])); c.fill()
c.set_fill_rule(cairo.FILL_RULE_WINDING)
rrect(c,0.75,0.75,126.5,126.5,10); c.set_line_width(1.5); c.set_source_rgba(0,0,0,0.95); c.stroke()
rrect(c,3,3,122,122,8); c.set_line_width(1); c.set_source_rgba(1,0.93,0.7,0.4); c.stroke()
rrect(c,10.5,10.5,107,107,5); c.set_line_width(2.2); c.set_source(lin(0,10,0,118,[(0,CRIMSON_HI),(1,CRIMSON_LO)])); c.stroke()
for i in range(7):
    rrect(c,12+i,12+i,104-2*i,104-2*i,4); c.set_line_width(1); c.set_source_rgba(0,0,0,0.42-i*0.055); c.stroke()
emit("portrait_frame",s,(18,18,18,18))

# 24. 档位徽章：浅色胶囊（运行时由 Image 颜色着色）
s,c=surface(64,24)
rrect(c,0,0,64,24,12); c.set_source_rgba(0,0,0,0.9); c.fill()
rrect(c,1.5,1.5,61,21,10.5); c.set_source(lin(0,0,0,24,[(0,(1,1,1,1)),(0.5,(0.82,0.82,0.82,1)),(1,(0.58,0.58,0.58,1))])); c.fill()
rrect(c,3,2.5,58,8,4); c.set_source_rgba(1,1,1,0.35); c.fill()
rrect(c,1.5,1.5,61,21,10.5); c.set_line_width(1); c.set_source_rgba(0,0,0,0.55); c.stroke()
emit("badge",s,(11,11,11,11))

# 25. 状态标签：半透明暗底 + 浅色描边（运行时着色）
s,c=surface(64,24)
rrect(c,0.5,0.5,63,23,6); c.set_source(lin(0,0,0,24,[(0,(0.2,0.22,0.24,0.9)),(1,(0.08,0.09,0.1,0.92))])); c.fill_preserve()
c.set_line_width(1); c.set_source_rgba(1,1,1,0.85); c.stroke()
rrect(c,2,2,60,9,4); c.set_source_rgba(1,1,1,0.08); c.fill()
emit("chip",s,(8,8,8,8))

# 26. 分隔线：中间亮金、两端渐隐
s,c=surface(256,4)
c.rectangle(0,1,256,1.5); c.set_source(lin(0,0,256,0,[(0,(0.9,0.72,0.4,0)),(0.2,(0.9,0.72,0.4,0.8)),(0.8,(0.9,0.72,0.4,0.8)),(1,(0.9,0.72,0.4,0))])); c.fill()
c.rectangle(0,2.5,256,1); c.set_source(lin(0,0,256,0,[(0,(0,0,0,0)),(0.5,(0,0,0,0.7)),(1,(0,0,0,0))])); c.fill()
emit("divider",s,(60,0,60,0))

# 27–32. 属性小图标（40×40，自带颜色，不着色）
GOLD_HI=hexc('#f6dc9a'); GOLD=hexc('#c99a45'); GOLD_LO=hexc('#6e4e1c')
def stat_icon(name,draw):
    s,c=surface(40,40); c.set_line_cap(cairo.LINE_CAP_ROUND); c.set_line_join(cairo.LINE_JOIN_ROUND); draw(c); emit(name,s)
def damage_icon(c):
    c.save(); c.translate(20,20); c.rotate(-math.pi/4)
    c.move_to(-2.6,-16); c.line_to(0,-19.5); c.line_to(2.6,-16); c.line_to(2.6,7); c.line_to(-2.6,7); c.close_path()
    c.set_source(lin(-3,0,3,0,[(0,hexc('#f4f7fa')),(0.5,hexc('#c9d2da')),(1,hexc('#7d8a96'))])); c.fill_preserve(); c.set_source_rgba(0,0,0,0.8); c.set_line_width(1); c.stroke()
    c.rectangle(-8,7,16,3.2); c.set_source(lin(0,7,0,10,[(0,GOLD_HI),(1,GOLD_LO)])); c.fill_preserve(); c.set_source_rgba(0,0,0,0.8); c.stroke()
    c.rectangle(-1.8,10.2,3.6,7); c.set_source_rgba(*hexc('#6a3b1c')); c.fill()
    c.arc(0,18.6,2.4,0,2*math.pi); c.set_source(lin(0,16,0,21,[(0,GOLD_HI),(1,GOLD)])); c.fill()
    c.restore()
def interval_icon(c):
    for sgn in (-1,1):
        c.move_to(9,20+sgn*14); c.line_to(31,20+sgn*14); c.set_line_width(3); c.set_source(lin(9,0,31,0,[(0,GOLD_LO),(0.5,GOLD_HI),(1,GOLD_LO)])); c.stroke()
    c.move_to(12,7); c.line_to(28,7); c.curve_to(28,14,21,17,21,20); c.curve_to(21,23,28,26,28,33); c.line_to(12,33); c.curve_to(12,26,19,23,19,20); c.curve_to(19,17,12,14,12,7); c.close_path()
    c.set_source_rgba(0.75,0.88,1,0.25); c.fill_preserve(); c.set_line_width(1.6); c.set_source_rgba(0.85,0.93,1,0.9); c.stroke()
    c.move_to(14.5,31.5); c.line_to(25.5,31.5); c.curve_to(25,27,20.5,25.5,20,24); c.curve_to(19.5,25.5,15,27,14.5,31.5); c.close_path(); c.set_source(lin(0,24,0,32,[(0,GOLD_HI),(1,GOLD)])); c.fill()
    c.move_to(16,11); c.line_to(24,11); c.line_to(20,15); c.close_path(); c.set_source(lin(0,11,0,15,[(0,GOLD_HI),(1,GOLD)])); c.fill()
def range_icon(c):
    for r,w,a in ((15,2.2,0.95),(9,2,0.85)):
        c.arc(20,20,r,0,2*math.pi); c.set_line_width(w); c.set_source(lin(0,4,0,36,[(0,GOLD_HI),(1,GOLD_LO)])); c.stroke()
    for a in range(4):
        ang=a*math.pi/2; c.move_to(20+math.cos(ang)*11,20+math.sin(ang)*11); c.line_to(20+math.cos(ang)*19,20+math.sin(ang)*19)
        c.set_line_width(2); c.set_source_rgba(1,0.93,0.75,1); c.stroke()
    c.arc(20,20,3,0,2*math.pi); c.set_source_rgba(1,0.45,0.32,1); c.fill()
def speed_icon(c):
    c.move_to(13,7); c.line_to(22,7); c.line_to(22,22); c.curve_to(28,23,34,25,34,30); c.line_to(34,33); c.line_to(11,33); c.line_to(11,26); c.curve_to(13,22,13,14,13,7); c.close_path()
    c.set_source(lin(0,7,0,33,[(0,hexc('#c58a52')),(1,hexc('#5e3518'))])); c.fill_preserve(); c.set_source_rgba(0,0,0,0.85); c.set_line_width(1.2); c.stroke()
    c.rectangle(11,30,23,3); c.set_source(lin(0,30,0,33,[(0,GOLD_HI),(1,GOLD_LO)])); c.fill()
    c.move_to(13,10); c.curve_to(7,9,4,13,3,17); c.curve_to(7,14,10,14,13,15); c.close_path(); c.set_source_rgba(0.95,0.97,1,0.95); c.fill()
    for i,y in enumerate((17,22,27)):
        c.move_to(2+i,y); c.line_to(9,y); c.set_line_width(1.6); c.set_source_rgba(0.8,0.92,1,0.7-i*0.15); c.stroke()
def xp_icon(c):
    pts=[]
    for k in range(10):
        r=15 if k%2==0 else 6.5; a=-math.pi/2+k*math.pi/5; pts.append((20+math.cos(a)*r,21+math.sin(a)*r))
    c.move_to(*pts[0])
    for p in pts[1:]: c.line_to(*p)
    c.close_path(); c.set_source(rad(17,16,1,16,[(0,hexc('#fff6c8')),(0.5,hexc('#f2c24e')),(1,hexc('#9a6416'))])); c.fill_preserve()
    c.set_source_rgba(0.25,0.15,0.02,0.9); c.set_line_width(1.2); c.stroke()
    c.arc(16,16,2.2,0,2*math.pi); c.set_source_rgba(1,1,1,0.85); c.fill()
def distance_icon(c):
    c.set_line_width(2.4); c.set_source(lin(6,0,34,0,[(0,GOLD_LO),(0.5,GOLD_HI),(1,GOLD_LO)]))
    c.move_to(8,20); c.line_to(32,20); c.stroke()
    for sgn,x in ((-1,8),(1,32)):
        c.move_to(x-sgn*7,13); c.line_to(x,20); c.line_to(x-sgn*7,27); c.set_line_width(2.4); c.set_source_rgba(1,0.9,0.65,1); c.stroke()
    for x in (14,20,26):
        c.move_to(x,16.5); c.line_to(x,23.5); c.set_line_width(1.2); c.set_source_rgba(1,0.95,0.8,0.7); c.stroke()
stat_icon("stat_damage",damage_icon); stat_icon("stat_interval",interval_icon); stat_icon("stat_range",range_icon)
stat_icon("stat_speed",speed_icon); stat_icon("stat_xp",xp_icon); stat_icon("stat_distance",distance_icon)

save_guids(g)
json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'sprites.json'),'w'),indent=1)
print(len(out),"sprites")
