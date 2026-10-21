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

save_guids(g)
json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'sprites.json'),'w'),indent=1)
print(len(out),"sprites")
