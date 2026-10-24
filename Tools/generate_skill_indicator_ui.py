# -*- coding: utf-8 -*-
"""生成原创技能指示器对照图和可染色底图。

这些图按英雄联盟和 DOTA2 的指示器类型整理，几何和配色是本项目自己的，
不是拳头或 Valve 的官方界面。运行：python Tools/generate_skill_indicator_ui.py
"""
from __future__ import annotations

import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

OUT = r"d:\UnityProject\myGame\Assets\Art\技能指示器ui"
W, H = 1640, 1120
SCALE = 2
CARD = (24, 20, 1000, 1100)
PANEL = (1024, 20, 1616, 1100)
# 从图片中心沿正上方走到边长的 44% 处，对应 9.6 米。描边留在图内。
MASK_METERS = 9.6
MASK_SPAN = 0.44

THEMES = {
    "self": ((72, 220, 242, 245), (72, 220, 242, 78)),
    "ally": ((86, 214, 150, 245), (86, 214, 150, 72)),
    "enemy": ((236, 84, 78, 245), (236, 84, 78, 82)),
    "invalid": ((214, 64, 58, 245), (176, 42, 40, 86)),
    "sweet": ((242, 198, 84, 245), (242, 198, 84, 78)),
    "block": ((214, 132, 64, 245), (214, 132, 64, 70)),
    "muted": ((158, 166, 174, 170), (120, 128, 136, 46)),
    "range": ((220, 232, 238, 220), (220, 232, 238, 28)),
    "white": ((255, 255, 255, 255), (255, 255, 255, 86)),
}
THEME_LABEL = {
    "self": "自身，可以施放（青）",
    "ally": "友方（绿）",
    "enemy": "敌方预警（红）",
    "invalid": "不合法或超出距离（红）",
    "sweet": "甜区，这段伤害或效果不同（金）",
    "block": "被墙或地形挡住（橙）",
    "muted": "这段不生效（灰）",
    "range": "只表示距离（白）",
    "white": "白色底图，使用时再染色",
}
LAYOUTS = {
    "forward": (512, 980, 46),
    "range": (512, 560, 34),
    "center": (512, 540, 36),
}
SCENES = []
MASKS = []


def font(size, bold=False):
    path = r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc"
    if not os.path.exists(path):
        path = r"C:\Windows\Fonts\msyh.ttc"
    return ImageFont.truetype(path, size)


FONT = {s: font(s) for s in (16, 18, 20, 22, 28, 34)}
FONTB = {s: font(s, True) for s in (22, 30, 34)}


def wrap(text, face, width):
    lines = []
    for para in str(text).split("\n"):
        buf = ""
        for ch in para:
            trial = buf + ch
            if face.getlength(trial) > width and buf:
                lines.append(buf)
                buf = ch
            else:
                buf = trial
        lines.append(buf)
    return lines


def punch(layer, box):
    r, g, b, a = layer.split()
    cut = Image.new("L", layer.size, 255)
    ImageDraw.Draw(cut).ellipse(box, fill=0)
    return Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, cut)))


class Stage:
    def __init__(self, image, ox, oy, ppm, scale, theme):
        self.image = image
        self.ox, self.oy, self.ppm, self.scale = ox, oy, ppm, scale
        self.theme_name = theme
        self.tags = []
        self.cursors = []

    def colors(self, theme=None):
        return THEMES[theme or self.theme_name]

    def pt(self, x, y):
        s = self.scale
        return (self.ox + x * self.ppm) * s, (self.oy - y * self.ppm) * s

    def rp(self, meters):
        return meters * self.ppm * self.scale

    def edge(self, meters=0.1):
        return max(int(2 * self.scale), int(round(self.rp(meters))))

    def tag(self, x, y, text):
        self.tags.append((x, y, text))

    def cursor(self, x, y, ghost=False):
        self.cursors.append((x, y, ghost))

    def _layer(self):
        return Image.new("RGBA", self.image.size, (0, 0, 0, 0))

    def _stamp(self, layer):
        self.image.alpha_composite(layer)

    def disk(self, x, y, radius, hole=0, theme=None):
        if radius <= 0:
            return
        edge, fill = self.colors(theme)
        cx, cy, radius_px = *self.pt(x, y), self.rp(radius)
        ew = self.edge()
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        box = [cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px]
        draw.ellipse(box, fill=fill)
        if hole > 0:
            inner = self.rp(hole)
            layer = punch(layer, [cx - inner, cy - inner, cx + inner, cy + inner])
            draw = ImageDraw.Draw(layer)
            draw.ellipse([cx - inner, cy - inner, cx + inner, cy + inner], outline=edge, width=ew)
        draw.ellipse(box, outline=edge, width=ew)
        self._stamp(layer)

    def ring(self, x, y, radius, theme=None, dashed=False, width_m=0.1, fill=False):
        edge, soft = self.colors(theme)
        cx, cy, radius_px = *self.pt(x, y), self.rp(radius)
        ew = max(self.edge(width_m), int(2 * self.scale))
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        box = [cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px]
        if fill:
            draw.ellipse(box, fill=(soft[0], soft[1], soft[2], max(18, soft[3] // 2)))
        if dashed:
            step = 12
            for i in range(0, 360, step):
                if (i // step) % 2 == 0:
                    draw.arc(box, i, i + step * 0.62, fill=edge, width=ew)
        else:
            draw.ellipse(box, outline=edge, width=ew)
        self._stamp(layer)

    def range_ring(self, radius, dashed=False, fill=False, theme=None):
        name = theme or self.theme_name
        self.ring(0, 0, radius, theme=name, dashed=dashed or name == "invalid", width_m=0.08, fill=fill)

    def beam(self, x, y, x2, y2, width, theme=None):
        edge, fill = self.colors(theme)
        p1, p2 = self.pt(x, y), self.pt(x2, y2)
        dx, dy = p2[0] - p1[0], p2[1] - p1[1]
        length = math.hypot(dx, dy)
        if length < 1:
            return
        hw = max(1.0, self.rp(width) / 2)
        nx, ny = -dy / length * hw, dx / length * hw
        poly = [(p1[0] + nx, p1[1] + ny), (p2[0] + nx, p2[1] + ny), (p2[0] - nx, p2[1] - ny), (p1[0] - nx, p1[1] - ny)]
        ew = self.edge(0.08)
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        draw.polygon(poly, fill=fill)
        draw.ellipse([p1[0] - hw, p1[1] - hw, p1[0] + hw, p1[1] + hw], fill=fill)
        draw.ellipse([p2[0] - hw, p2[1] - hw, p2[0] + hw, p2[1] + hw], fill=fill)
        draw.line([poly[0], poly[1]], fill=edge, width=ew)
        draw.line([poly[3], poly[2]], fill=edge, width=ew)
        draw.arc([p2[0] - hw, p2[1] - hw, p2[0] + hw, p2[1] + hw], 0, 360, fill=edge, width=ew)
        self._stamp(layer)

    def arrow(self, x, y, x2, y2, theme=None):
        edge, fill = self.colors(theme)
        p1, p2 = self.pt(x, y), self.pt(x2, y2)
        ang = math.atan2(p2[1] - p1[1], p2[0] - p1[0])
        size = self.rp(0.62)
        back = size * 0.92
        tip = p2
        left = (p2[0] - math.cos(ang) * back + math.sin(ang) * size * 0.55, p2[1] - math.sin(ang) * back - math.cos(ang) * size * 0.55)
        right = (p2[0] - math.cos(ang) * back - math.sin(ang) * size * 0.55, p2[1] - math.sin(ang) * back + math.cos(ang) * size * 0.55)
        layer = self._layer()
        ImageDraw.Draw(layer).polygon([tip, left, right], fill=edge)
        self._stamp(layer)

    def cone(self, heading, length, fov, inner=0, theme=None):
        edge, fill = self.colors(theme)
        cx, cy = self.pt(0, 0)
        radius_px = self.rp(length)
        center = 270 + heading
        half = fov / 2
        a0, a1 = center - half, center + half
        ew = self.edge()
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        box = [cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px]
        draw.pieslice(box, a0, a1, fill=fill)
        if inner > 0:
            hole = self.rp(inner)
            layer = punch(layer, [cx - hole, cy - hole, cx + hole, cy + hole])
            draw = ImageDraw.Draw(layer)
            draw.arc([cx - hole, cy - hole, cx + hole, cy + hole], a0, a1, fill=edge, width=ew)
        draw.arc(box, a0, a1, fill=edge, width=ew)
        for ang in (a0, a1):
            rad = math.radians(ang)
            outer = (cx + math.cos(rad) * radius_px, cy + math.sin(rad) * radius_px)
            start_r = self.rp(inner) if inner > 0 else 0
            inner_pt = (cx + math.cos(rad) * start_r, cy + math.sin(rad) * start_r)
            draw.line([inner_pt, outer], fill=edge, width=ew)
        self._stamp(layer)

    def pie(self, amount, radius, theme=None):
        if amount <= 0:
            return
        edge, fill = self.colors(theme)
        cx, cy = self.pt(0, 0)
        radius_px = self.rp(radius)
        box = [cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px]
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        draw.pieslice(box, 270, 270 + 360 * min(1, amount), fill=fill)
        draw.ellipse(box, outline=edge, width=self.edge())
        self._stamp(layer)

    def polyline(self, points, width, theme=None):
        for a, b in zip(points, points[1:]):
            self.beam(a[0], a[1], b[0], b[1], width, theme=theme)

    def curve(self, p0, p1, p2, width, theme=None):
        pts = []
        for i in range(28):
            t = i / 27
            u = 1 - t
            pts.append((u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]))
        self.polyline(pts, width, theme=theme)

    def polygon(self, pts, theme=None):
        edge, fill = self.colors(theme)
        pix = [self.pt(*p) for p in pts]
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        draw.polygon(pix, fill=fill)
        draw.line(pix + [pix[0]], fill=edge, width=self.edge())
        self._stamp(layer)

    def regular(self, x, y, radius, sides, rot=0, theme=None):
        pts = []
        for i in range(sides):
            ang = math.radians(rot + i * 360 / sides)
            pts.append((x + math.sin(ang) * radius, y + math.cos(ang) * radius))
        self.polygon(pts, theme=theme)

    def caster(self, x=0, y=0):
        edge, _ = self.colors()
        cx, cy = self.pt(x, y)
        radius_px = self.rp(0.38)
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        draw.ellipse([cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px], fill=(236, 246, 248, 255), outline=(36, 64, 74, 255), width=max(2, self.scale))
        tip, left, right = self.pt(x, y + 0.78), self.pt(x - 0.24, y + 0.28), self.pt(x + 0.24, y + 0.28)
        draw.polygon([tip, left, right], fill=edge)
        self._stamp(layer)

    def unit(self, x, y, kind):
        cx, cy = self.pt(x, y)
        radius_px = self.rp(0.46)
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        if kind == "enemy":
            draw.rounded_rectangle([cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px], radius=radius_px * 0.35, fill=(232, 86, 78, 255))
        elif kind == "ally":
            draw.ellipse([cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px], fill=(86, 214, 150, 255))
        elif kind == "tree":
            draw.polygon([self.pt(x, y + 0.7), self.pt(x - 0.46, y - 0.2), self.pt(x + 0.46, y - 0.2)], fill=(92, 168, 96, 255))
        else:
            self.regular(x, y, 0.5, 6, theme="sweet")
            return
        self._stamp(layer)

    def paint_cursors(self):
        for x, y, ghost in self.cursors:
            cx, cy = self.pt(x, y)
            radius_px = self.rp(0.34 if ghost else 0.28)
            edge = (220, 228, 232, 140) if ghost else (255, 255, 255, 235)
            layer = self._layer()
            draw = ImageDraw.Draw(layer)
            box = [cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px]
            if ghost:
                for i in range(0, 360, 24):
                    draw.arc(box, i, i + 12, fill=edge, width=max(2, self.scale))
            else:
                draw.ellipse(box, outline=edge, width=max(2, self.scale))
                arm = self.rp(0.55)
                width = max(2, self.scale)
                draw.line([(cx - arm, cy), (cx + arm, cy)], fill=edge, width=width)
                draw.line([(cx, cy - arm), (cx, cy + arm)], fill=edge, width=width)
            self._stamp(layer)

    def grid(self, half=14):
        layer = self._layer()
        draw = ImageDraw.Draw(layer)
        for i in range(-half, half + 1, 2):
            a, b = self.pt(-half, i), self.pt(half, i)
            c, d = self.pt(i, -half), self.pt(i, half)
            color = (255, 255, 255, 28 if i == 0 else 16)
            draw.line([a, b], fill=color, width=self.scale)
            draw.line([c, d], fill=color, width=self.scale)
        self._stamp(layer)

    def glow(self, x, y, radius):
        cx, cy, radius_px = *self.pt(x, y), self.rp(radius)
        layer = self._layer()
        ImageDraw.Draw(layer).ellipse([cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px], fill=(72, 220, 242, 90))
        self._stamp(layer.filter(ImageFilter.GaussianBlur(radius=10 * self.scale)))


def add(folder, title, lol, dota, note, draw, theme="self", layout="forward", masks="", caster=True, compass=None):
    SCENES.append(dict(folder=folder, title=title, lol=lol, dota=dota, note=note, draw=draw, theme=theme, layout=layout, masks=masks, caster=caster, compass=compass))


def mask(name, draw, note, colored=False):
    MASKS.append(dict(name=name, draw=draw, note=note, colored=colored))


def build_scenes():
    add("00_怎么看", "六种颜色各表示什么",
        "英雄联盟里，自己的技能多用蓝或角色色，放不了会变红；敌方关键范围用红色预警。",
        "DOTA2 里，自己和友方范围偏绿或蓝，敌方技能和禁区偏红。按住 Alt 时能同时看到多种范围。",
        "同一形状可以换色。青色不是伤害数字，只表示这是你自己、而且现在可以放。",
        lambda st: (
            st.disk(-6.2, 2.4, 1.7, theme="self"), st.tag(-6.2, 4.5, "自身"),
            st.disk(0, 2.4, 1.7, theme="ally"), st.tag(0, 4.5, "友方"),
            st.disk(6.2, 2.4, 1.7, theme="enemy"), st.tag(6.2, 4.5, "敌方"),
            st.disk(-6.2, -2.6, 1.7, theme="invalid"), st.tag(-6.2, -4.7, "不合法"),
            st.disk(0, -2.6, 1.7, theme="sweet"), st.tag(0, -4.7, "甜区"),
            st.disk(6.2, -2.6, 1.7, theme="block"), st.tag(6.2, -4.7, "被挡住"),
        ), layout="center", caster=False, compass="这张图没有施法者，六种颜色并排放。")
    add("00_怎么看", "实线才是范围，外发光不算",
        "英雄联盟技能圈外面常有一层柔光，真正的命中边界是清楚的那条边。",
        "DOTA2 的范围圈同样以边界为准，光晕只是为了看得见。",
        "做贴地指示时，判定用实线半径。外圈发光可以再叠一张，但不能把它算进伤害。",
        lambda st: (st.glow(0, 0, 4.6), st.ring(0, 0, 3.2), st.tag(0, 3.9, "实线边界"), st.tag(4.8, 0.2, "发光不算")),
        layout="center", masks="circle_r + ring_thin")

    add("01_范围圈", "悬停技能只显示最大距离",
        "鼠标放在技能上，或按住技能键还没进入瞄准时，先只看到最大施法距离。",
        "按住 Alt，会显示施法距离、攻击距离和部分光环，不必先按技能。",
        "这是信息圈，不是伤害圈。还没有落点和方向。",
        lambda st: (st.range_ring(10, fill=True), st.tag(0, 0.4, "10 m")),
        layout="range", masks="ring_fill")
    add("01_范围圈", "攻击距离",
        "开启攻击距离后，平时也能看到普攻能打到的圈，通常比技能圈更细。",
        "按住 Alt 或在设置里打开攻击距离。远程和近战半径差很多。",
        "和技能施法距离是两条数据，不要画成同一个圈。",
        lambda st: (st.range_ring(6.5, theme="range"), st.tag(0, 0.3, "攻击 6.5 m")),
        layout="range", theme="range", masks="ring_thin")
    add("01_范围圈", "常驻光环",
        "光环被动经常用很淡的圈，平时就在，不必按技能。例如团队光环、灼烧光环。",
        "水晶室女的奥术光环、暗影恶魔的伤害光环，都是跟着人走的圆。",
        "淡、细、跟着施法者。不要做成要点一下才出现的落点技能。",
        lambda st: st.ring(0, 0, 5.5, fill=True, width_m=0.06),
        layout="center", masks="ring_fill")
    add("01_范围圈", "同时看几种范围",
        "按住技能时，攻击距离和这个技能的距离可以叠在一起，线的粗细要能分开。",
        "Alt 是 DOTA2 的总开关：攻击、施法、警戒、真实视野可以一起出现。",
        "最多同时三四条。再多就看不清，尤其镜头拉远时。",
        lambda st: (st.range_ring(4.2, theme="range"), st.range_ring(7.2), st.range_ring(10, theme="ally"),
                    st.tag(0, 4.8, "攻击"), st.tag(0, 7.8, "技能"), st.tag(0, 10.6, "光环")),
        layout="range")
    add("01_范围圈", "太近不能放，太远也不能放",
        "少数技能有最小距离。内圈里放不了，外圈外面也放不了，真正能放的是中间那一环。",
        "跳刀没有最小距离，但超过最大距离会缩短。最小距离更常见于「不能贴脸放」的自定义技能。",
        "内圈用虚线或红色，表示这里是禁区，不是甜区。甜区要另用金色。",
        lambda st: (st.range_ring(10), st.range_ring(3.2, theme="invalid"), st.tag(0, 1.4, "太近"), st.tag(0, 6.4, "可以放")),
        layout="range", masks="donut")
    add("01_范围圈", "闪烁或位移的最大距离",
        "闪现、短位移会显示一个最大距离圈。落点是一个小点，不是大范围伤害圈。",
        "跳刀、敌法师闪烁、女王闪烁，都是最大距离内选一个点。",
        "小点表示人会出现的位置。圈只限制最远能去哪。",
        lambda st: (st.range_ring(8.5), st.disk(0, 5.2, 0.55), st.cursor(0, 5.2), st.tag(0, 6.3, "落点")),
        layout="range", masks="ring_thin + reticle")
    add("01_范围圈", "超出后落点缩短到圆上",
        "光标在圈外时，人不会跟着光标跑出圈，而是落在光标方向的最大距离上。闪现就是这样。",
        "跳刀超过最远距离时，会朝那个方向走到最大距离，而不是原地不动。",
        "实心点是实际落点，虚线圈是鼠标真正所在的位置。",
        lambda st: (st.range_ring(8), st.disk(0, 8, 0.55), st.cursor(0, 8), st.cursor(0, 11.2, ghost=True), st.tag(0, 11.8, "鼠标"), st.tag(2.3, 8, "实际")),
        layout="range")
    add("01_范围圈", "眼位和它提供的视野",
        "插眼时，除了能不能放到这个点，还要看到这个眼能照亮多大。",
        "侦查守卫给普通视野，岗哨守卫额外有真实视野，两个半径不要画成一个。",
        "小圈是眼本身，大圈是视野。这张用友方绿色。",
        lambda st: (st.disk(0, 0, 0.7, theme="ally"), st.ring(0, 0, 8.5, theme="ally", dashed=True), st.tag(0, 9.2, "视野")),
        layout="center", theme="ally", masks="reticle + ring_dashed")
    add("01_范围圈", "攻击距离和视野叠在一起",
        "真实视野通常比攻击距离大。两个圈叠在一起时，攻击圈更实，视野圈更虚。",
        "DOTA2 白天和夜晚视野半径不同。要换图或换半径，不要只改透明度。",
        "虚线代表信息，实线代表能打到。",
        lambda st: (st.range_ring(5.5, theme="range"), st.range_ring(9.2, theme="ally", dashed=True), st.tag(0, 5.9, "攻击"), st.tag(0, 9.8, "视野")),
        layout="range")
    add("01_范围圈", "两人之间的绳子有最大长度",
        "连接两人的技能会在两人之间画线。拉开到最大长度会断，或者把人拖住。",
        "艾欧的连接就是典型：有一条线，也有最远距离。",
        "线连接两个单位。圈表示从施法者算起还能拉多远。",
        lambda st: (st.range_ring(8, dashed=True), st.unit(0, 5.5, "ally"), st.beam(0, 0, 0, 5.5, 0.18, theme="ally"), st.tag(1.6, 2.6, "连接")),
        layout="range", theme="ally")
    add("01_范围圈", "同一技能有两档距离",
        "内圈满效果，外圈效果变弱，或者近处和远处根本是两种效果。",
        "有些光环、毒雾和爆炸会分成中心与边缘两档。",
        "两圈同心。内圈更亮，外圈更淡。",
        lambda st: (st.disk(0, 0, 7.6), st.disk(0, 0, 3.6, theme="sweet"), st.tag(0, 0.2, "满"), st.tag(0, 5.4, "衰减")),
        layout="center", masks="double_circle")
    add("01_范围圈", "升级以后距离变长",
        "技能升级后，施法距离或半径会变。预览可以同时画出旧圈和新圈。",
        "DOTA2 多数技能 4 级，大招常是 3 级；英雄联盟普通技能最多 5 级，大招 3 级。",
        "虚线是升级前，实线是升级后。",
        lambda st: (st.range_ring(6.2, dashed=True, theme="muted"), st.range_ring(9.4), st.tag(-3.2, 6.4, "现在"), st.tag(3.4, 9.6, "升级后")),
        layout="range")
    add("01_范围圈", "没蓝或被沉默时圈变灰",
        "魔法不够、被沉默、还在冷却时，不应该还显示成可以施放的青色。",
        "DOTA2 魔法不足时技能图标变暗，仍然可以看见范围，但颜色要告诉玩家放不了。",
        "灰色只表示现在不能放，形状和能放的时候一样。",
        lambda st: (st.range_ring(9, fill=True), st.tag(0, 0.2, "不能放")),
        layout="range", theme="muted", masks="ring_fill")

    add("02_点选落点", "小范围落点",
        "点一个地点，炸开的半径比较小。例如小延迟的单体区域控制。",
        "点数技能的作用半径和施法距离是分开的两个数。",
        "大圈是最远能点到哪，小圈才是会命中的范围。",
        lambda st: (st.range_ring(10), st.disk(0, 6.2, 1.15), st.cursor(0, 6.2)),
        layout="range", masks="ring_thin + circle_r")
    add("02_点选落点", "中等范围落点",
        "最常见的点地技能：距离圈加大约两三米的效果圈。",
        "冰霜新星、定点沉默、定点治疗，都是这种结构。",
        "效果圈的圆心跟着鼠标，可以一部分超出施法距离圈。",
        lambda st: (st.range_ring(10), st.disk(0, 6.5, 2.4), st.cursor(0, 6.5)),
        layout="range")
    add("02_点选落点", "大范围落点",
        "大招一类的点地范围。效果圈很大，施法距离可能不长。",
        "黑洞、毁灭、大范围沉默，玩家真正要看的是那个大圆，不是施法者脚底。",
        "镜头拉远时边线要比填充更清楚。",
        lambda st: (st.range_ring(8), st.disk(0, 4.2, 4.4), st.cursor(0, 4.2)),
        layout="range")
    add("02_点选落点", "标准点选：距离圈加效果圈",
        "这是英雄联盟点选技能最常见的样子。自己脚下一圈，鼠标处一圈。",
        "DOTA2 点数技能同样是：施法距离限制圆心，作用范围画在落点上。",
        "两个半径独立。以后做技能数据时要分成两个字段。",
        lambda st: (st.range_ring(10, fill=True), st.disk(2.2, 6.4, 2.2), st.cursor(2.2, 6.4)),
        layout="range", masks="ring_fill + circle_r")
    add("02_点选落点", "超出距离后变红并且不能放",
        "鼠标跑出施法距离，整块提示变红，松手或点击不会放出。",
        "有些单位目标技能点到范围外的人也是这样，直接无效。",
        "红色表示这一下不会发生。圆形 Demo 默认是贴到边缘再放；关掉开关时才用这张红色。",
        lambda st: (st.range_ring(8, fill=True), st.disk(0, 11, 2.1), st.cursor(0, 11), st.tag(0, 13.2, "放不了")),
        layout="range", theme="invalid")
    add("02_点选落点", "超出距离后贴到边缘再放",
        "鼠标可以在圈外，真正的圆心被拉回施法距离的边缘，颜色仍然是可以施放。",
        "DOTA2 多数点数技能会把落点限制在施法距离内。",
        "虚线小圈是鼠标，实心圆是真正会生效的位置。",
        lambda st: (st.range_ring(8, fill=True), st.disk(0, 8, 2.1), st.cursor(0, 8), st.cursor(0, 11.4, ghost=True), st.tag(0, 12.2, "鼠标")),
        layout="range")
    add("02_点选落点", "中心满额，边缘衰减",
        "同一个圆里，靠中心伤害更高，靠外只剩减速或较低伤害。",
        "不少爆炸和陨石是中心满额、边缘衰减。",
        "内圈用金色标出满额区，外圈仍是这个技能的总半径。",
        lambda st: (st.range_ring(10), st.disk(0, 5.5, 3.3), st.disk(0, 5.5, 1.5, theme="sweet"), st.cursor(0, 5.5)),
        layout="range", masks="double_circle")
    add("02_点选落点", "只有外环会命中",
        "诺克萨斯之手的挥砍是整圈甜区：贴太近打不中，要站在环上。",
        "玛尔斯的竞技场、干扰者的静滞结界，危险在环上，不在圆心。",
        "中间是空的。不要把空心填死，否则玩家会以为中心也有伤害。",
        lambda st: (st.disk(0, 0, 6.8, hole=4.2, theme="sweet"), st.tag(0, 0.2, "打不中"), st.tag(0, 5.5, "甜区")),
        layout="center", theme="sweet", masks="donut")
    add("02_点选落点", "外环危险，中心可以拿来躲避",
        "和甜区相反。敌人的圈正在收缩或爆开时，安全位置可能在正中间。",
        "一些环形结界是走出环才安全，另一些是留在中心才安全。这张是中心安全。",
        "红色只涂环带。中心留空，并写清这是躲避点。",
        lambda st: (st.disk(0, 0, 7.2, hole=3.3), st.tag(0, 0.2, "可躲避")),
        layout="center", theme="enemy", masks="donut")
    add("02_点选落点", "敌方延迟落下的预警圈",
        "敌方已经选定落点，圈先出现，过一会才生效。玩家靠这张图躲避。",
        "大范围点地技能在敌方视角和我方视角都应该看得见，颜色用红。",
        "预警圈不要跟着敌方角色的移动重新定位，除非这个技能本身会跟着走。",
        lambda st: (st.disk(0, 0, 4.2), st.tag(0, 0.2, "即将落下")),
        layout="center", theme="enemy", caster=False, compass="红圈是即将生效的位置，不一定在施法者脚下。", masks="circle_r")
    add("02_点选落点", "预警圈正在变小",
        "圈从大变小，缩到尽头才爆。玩家要看清最后剩下的那一圈。",
        "这种收缩在两边正作里都不是最常见，但躲避提示会用到。",
        "四条环是同一张图上的时间过程：外虚、内实。真正判定看最里面。",
        lambda st: (st.ring(0, 0, 7.4, dashed=True, theme="muted"), st.ring(0, 0, 5.6, theme="enemy"), st.ring(0, 0, 3.6, theme="enemy"), st.disk(0, 0, 1.8), st.tag(0, 8.2, "开始"), st.tag(0, 0.2, "结束")),
        layout="center", theme="enemy", masks="shrink_frames")
    add("02_点选落点", "范围正在变大",
        "技能已经放出来，圈还在往外长。例如持续扩大的领域。",
        "潮汐猎人的毁灭会明显变大。玩家要看的是当前边界，不是一开始的小圈。",
        "虚线是最终大小，实线是现在已经生效的大小。",
        lambda st: (st.ring(0, 0, 8.2, dashed=True, theme="muted"), st.disk(0, 0, 4.4), st.tag(0, 8.8, "最终"), st.tag(0, 0.2, "现在")),
        layout="center", theme="enemy", masks="grow_frames")
    add("02_点选落点", "圈跟着某个单位走",
        "范围挂在人身上，人走圈也走。不是点在地上就不动的那一种。",
        "跟随型光环、被标记的目标周围、需要贴着目标才生效的区域。",
        "圆心画在单位上，不画在鼠标上。",
        lambda st: (st.unit(0, 3.2, "enemy"), st.disk(0, 3.2, 2.6), st.tag(0, 6.2, "跟着他")),
        layout="range", theme="enemy", caster=False, compass="方块是被跟住的敌方单位。")
    add("02_点选落点", "效果在鼠标前方，不在鼠标上",
        "鼠标点到的是方向或参考点，真正的圆出现在那个点前方一段距离。",
        "矢量技能里很常见：先有一个点，再沿方向推出一块区域。",
        "小十字是鼠标，大圆才是会命中的地方。",
        lambda st: (st.range_ring(10), st.cursor(0, 4.2), st.beam(0, 4.2, 0, 7.2, 0.16, theme="range"), st.disk(0, 7.2, 2), st.tag(0, 3.4, "鼠标")),
        layout="range")
    add("02_点选落点", "超大或接近全图的范围",
        "卡尔萨斯、索拉卡这类技能覆盖很大，屏幕里往往只能看到一段弧。",
        "宙斯、先知、死亡先知的一部分大招范围很大，甚至按全图处理。",
        "圈可以超出镜头。图上用贴边的大弧表示「还没画完」。",
        lambda st: (st.ring(0, -2, 12.5), st.tag(0, 4, "只看得到一段")),
        layout="center")
    add("02_点选落点", "一块区域里有好几个固定小圈",
        "一次施放，在一片区域里落下多个小范围，位置可以预先看清。",
        "天穹守望者的飞弹、干扰者的雷暴，都是多个落点而不是一个大圆。",
        "不要合并成一个大圆。玩家躲的是每一个小圈。",
        lambda st: (st.range_ring(9, dashed=True, theme="range"), [st.disk(x, y, 0.85) for x, y in ((0, 2), (-2.2, 3.4), (2.2, 3.2), (-1.2, 5.4), (1.4, 5.6), (0, 7.2))]),
        layout="range", masks="meteor")
    add("02_点选落点", "区域内随机落下多个小圈",
        "玩家只能看到「会落在这片里面」，看不到每一发的精确点。",
        "适合流星、炮击、随机轰炸。和上一张「点是固定的」要分开。",
        "大圈是可能落下的总区域，用虚线。小圈只是举例，不要画成固定伤害点。",
        lambda st: (st.ring(0, 4.5, 4.2, dashed=True), st.disk(-1.4, 3.2, 0.7, theme="muted"), st.disk(1.6, 5.4, 0.7, theme="muted"), st.disk(0.2, 6.6, 0.7, theme="muted"), st.tag(0, 9.2, "只保证落在大圈内")),
        layout="range", masks="scatter")
    add("02_点选落点", "十字形范围",
        "命中区域是十字，不是圆。横竖两条都要算进判定。",
        "正作里不多，自定义技能和地形技能会用到。",
        "两条等宽的矩形交叉，中心重叠只算一次命中。",
        lambda st: (st.beam(-4.5, 0, 4.5, 0, 1.3), st.beam(0, -4.5, 0, 4.5, 1.3)),
        layout="center", masks="cross")
    add("02_点选落点", "斜向的叉形范围",
        "和十字一样，只是转了 45 度。",
        "用于斜向斩击或两道交叉弹道。",
        "旋转角度要跟着技能，不要永远横平竖直。",
        lambda st: (st.beam(-3.4, -3.4, 3.4, 3.4, 1.15), st.beam(-3.4, 3.4, 3.4, -3.4, 1.15)),
        layout="center", masks="x_shape")
    add("02_点选落点", "三角形范围",
        "顶点朝前的三角形。和扇形接近，但三条边都是直的。",
        "适合锥形喷吐的简化显示，尤其半径不大的时候。",
        "底边要画出来。扇形的外沿是弧，这张不是。",
        lambda st: st.regular(0, 1.2, 5.2, 3),
        layout="center", masks="triangle")
    add("02_点选落点", "菱形范围",
        "菱形判定。站在尖角外的单位打不中，即使看起来离中心不远。",
        "正作少见，作为形状备用，避免以后又去找菱形资源。",
        "四个顶点都要清楚。",
        lambda st: st.regular(0, 0, 5.4, 4),
        layout="center", masks="diamond")
    add("02_点选落点", "放下一整块矩形",
        "矩形不跟施法者连在一起，而是放在鼠标指定的位置。",
        "墙、网、格子、一片禁区，经常是矩形而不是圆。",
        "矩形可以旋转。这张是横着放的一块。",
        lambda st: (st.polygon([(-4.2, 2.2), (4.2, 2.2), (4.2, 5.6), (-4.2, 5.6)]), st.cursor(0, 3.9)),
        layout="range", masks="rect_flat")
    add("02_点选落点", "沿着圆周放一圈立柱",
        "不是填满的圆，而是圆上若干个点。点与点之间可以走过去。",
        "发条齿轮、竞技场边缘的柱子，都是这种。",
        "每个点单独算碰撞。虚线只帮助看出它们在同一个圆上。",
        lambda st: (st.ring(0, 0, 5.2, dashed=True, theme="range"), [st.disk(math.sin(math.radians(i)) * 5.2, math.cos(math.radians(i)) * 5.2, 0.55) for i in range(0, 360, 45)]),
        layout="center", masks="pillars")
    add("02_点选落点", "斜线标出的禁区",
        "这块地方不能放技能，或者放下去会无效。用斜线而不是普通填充。",
        "动能场里面、一些墙体外侧，都属于不能站或不该放的区域。",
        "斜线区域和普通伤害区域不要用同一张图。",
        lambda st: (st.disk(0, 0, 4.6, theme="block"), st.tag(0, 0.2, "不能放")),
        layout="center", theme="block", masks="hatch")
    add("02_点选落点", "只有一个准星，没有爆炸半径",
        "位移落点、传送落点、插眼点，重要的是那个点本身。",
        "跳刀和闪烁若没有额外伤害，就不要硬画一个大圆。",
        "准星中心才是落点。",
        lambda st: (st.range_ring(9), st.cursor(1.4, 5.2), st.tag(1.4, 6.2, "落点")),
        layout="range", masks="reticle")
    add("02_点选落点", "能点到的距离和会炸到的距离不是一回事",
        "施法距离决定圆心最远能放哪。作用半径决定放下去打到谁。",
        "DOTA2 的技能说明把施法距离和作用范围分成两项。英雄联盟也是这样。",
        "图上两条标注要同时在，避免以后做成一个数。",
        lambda st: (st.range_ring(9.5), st.disk(0, 6, 2.6), st.cursor(0, 6), st.tag(-4.6, 2.2, "施法距离"), st.tag(3.2, 6, "作用半径")),
        layout="range")

    add("03_直线弹道", "很细的直线",
        "伊泽瑞尔的魔法箭、女警的和平使者，宽度不大，但方向非常重要。",
        "米拉娜的月神之箭、力丸的飞镖，也是细长直线。",
        "线的长度就是飞行距离。线本身要细，末端用箭头表示方向。",
        lambda st: (st.beam(0, 0, 0, 10.5, 0.28), st.arrow(0, 0, 0, 10.5)),
        masks="line_thin")
    add("03_直线弹道", "有明显宽度的直线",
        "能打到线两侧一点点的单位。宽度要能看出来，不能只剩一条线。",
        "不少直线技能其实有碰撞宽度，说明里不一定写「矩形」。",
        "矩形两端是圆头，避免尖角让人误会打得更远。",
        lambda st: (st.beam(0, 0, 0, 10.2, 1.15), st.arrow(0, 0, 0, 10.2)),
        masks="line_mid")
    add("03_直线弹道", "很宽的一条走廊",
        "男枪的大招、俄洛伊的触手区域，宽度大到更像一条路，而不是箭。",
        "影魔身前的影压也更接近一条短矩形，而不是圆。",
        "宽度和长度都要按米缩放，不能只拉长贴图把宽度一起拉变形。若用同一张图等比缩放，宽度会跟着变，所以宽和窄要分成不同底图。",
        lambda st: st.beam(0, 0, 0, 10, 2.6),
        masks="line_wide")
    add("03_直线弹道", "直线飞到尽头再爆炸",
        "弹体沿直线飞，最后在终点炸一个圆。直线和圆的半径是两个数。",
        "箭矢飞到尽头，或飞到中途命中后再爆，都属于这一类。",
        "底图里有一张合成好的，也有分开的直线和圆，方便两个半径不一致时使用。",
        lambda st: (st.beam(0, 0, 0, 8.2, 0.7), st.disk(0, 8.2, 2.1), st.arrow(0, 0, 0, 7.2)),
        masks="line_end")
    add("03_直线弹道", "蓄力较短",
        "韦鲁斯、泽拉斯、派克的蓄力箭，按得越久飞得越远。这张是刚按下。",
        "DOTA2 直线技能多数长度固定。变长蓄力以英雄联盟更典型。",
        "三张蓄力图要连着看：短、中、满。满的时候顶到最大距离。",
        lambda st: (st.range_ring(10, dashed=True, theme="muted"), st.beam(0, 0, 0, 3.4, 0.8), st.tag(0, 4.2, "刚按下")),
        layout="range")
    add("03_直线弹道", "蓄力到一半",
        "还没到最大距离，松手就会按现在的长度飞出去。",
        "若技能没有蓄力，就不要使用这三张。",
        "灰色虚线圈是最大值，实线是当前值。",
        lambda st: (st.range_ring(10, dashed=True, theme="muted"), st.beam(0, 0, 0, 6.4, 0.8), st.tag(0, 7.2, "蓄力中")),
        layout="range")
    add("03_直线弹道", "蓄力已经拉满",
        "长度顶到最大距离。再按也不会更长。",
        "最大长度应和技能数据里的最大施法距离一致。",
        "满蓄力用实心颜色，不再是短的那一截。",
        lambda st: (st.range_ring(10), st.beam(0, 0, 0, 10, 0.9), st.arrow(0, 0, 0, 10)),
        layout="range", masks="line_mid")
    add("03_直线弹道", "穿透路径上的所有人",
        "拉克丝的终极激光、女警的和平使者会穿过单位，线不会在第一个人那里停住。",
        "有些箭会穿过小兵，但打到英雄会停。那是下一张，不要和这张混用。",
        "线穿过两个敌人，末端仍然有箭头。",
        lambda st: (st.beam(0, 0, 0, 10.4, 0.7), st.unit(0, 3.2, "enemy"), st.unit(0, 6.4, "enemy"), st.arrow(0, 0, 0, 10.4)),
        masks="line_mid")
    add("03_直线弹道", "打到第一个敌人就停",
        "伊泽瑞尔的魔法箭打到第一个单位就消失。帕吉的钩子、月神之箭打到英雄也会停。",
        "小兵和英雄能不能挡住，是技能规则，图上要用停住的位置表达。",
        "线画到被挡住的那个单位为止，不要再往后延伸。",
        lambda st: (st.beam(0, 0, 0, 4.6, 0.7), st.unit(0, 4.6, "enemy"), st.unit(0, 8.2, "enemy"), st.tag(2.2, 8.2, "打不到")),
        masks="line_mid")
    add("03_直线弹道", "被墙挡住，墙后面无效",
        "直线碰到地形就停。墙后的敌人打不到。",
        "月神之箭、肉钩、多数撞击物都会被高地或树木挡住。",
        "橙色短线是墙。墙后的灰色是本来想飞、但不会生效的部分。",
        lambda st: (st.beam(0, 0, 0, 4.8, 1.0), st.beam(0, 4.8, 0, 10, 1.0, theme="muted"), st.beam(-2.4, 4.8, 2.4, 4.8, 0.28, theme="block"), st.tag(3.2, 4.8, "墙")),
        masks="line_mid")
    add("03_直线弹道", "飞出去再飞回来",
        "阿狸的欺诈宝珠会沿原路返回。去和回都可能命中。",
        "DOTA2 里来回飞的直线较少，这张主要给回旋弹体。",
        "两条并列的线表示去程和回程，不是两发不同的技能。",
        lambda st: (st.beam(-0.35, 0, -0.35, 9.2, 0.38), st.beam(0.35, 9.2, 0.35, 0, 0.38), st.arrow(-0.35, 0, -0.35, 9.2), st.arrow(0.35, 9.2, 0.35, 0.4), st.tag(2.2, 4.6, "去和回")),
        masks="boomerang")
    add("03_直线弹道", "弯曲的弹道",
        "正作里真正拐弯的弹道不多。这张留给会绕向一侧的自定义弹体。",
        "不要把回旋、弹跳和弯曲画成同一张。它们的躲避方式不同。",
        "弯的是路径，宽度仍然要保持。",
        lambda st: (st.curve((0, 0), (3.4, 4.2), (0.6, 9.4), 0.7), st.tag(3.6, 5.2, "会拐弯")),
        masks="curve")
    add("03_直线弹道", "在地面上弹跳几次",
        "吉格斯的弹跳炸弹会落点、弹起、再落下，每次落点都可以画出来。",
        "地面弹跳和在敌人之间跳跃不是一回事。",
        "三个圆是三次落地，线表示弹起的轨迹。",
        lambda st: (st.curve((0, 0), (1.2, 2.4), (0, 3.2), 0.2), st.disk(0, 3.2, 1.05), st.curve((0, 3.2), (1.4, 5.4), (0.2, 6.2), 0.2), st.disk(0.2, 6.2, 1.05), st.disk(0.4, 8.6, 1.05)),
        layout="range")
    add("03_直线弹道", "在几个敌人之间跳跃",
        "品牌的火焰、韦鲁斯的扩散箭，会从一个敌人跳到附近另一个敌人。",
        "巫医的骨棒、闪电击也会在单位之间跳，每一次都有最大跳跃距离。",
        "细圈是这一跳还能跳多远。跳不到圈外的人。",
        lambda st: (st.unit(-1.2, 2.4, "enemy"), st.unit(2.2, 4.6, "enemy"), st.unit(-0.4, 7.2, "enemy"), st.beam(0, 0, -1.2, 2.4, 0.16), st.beam(-1.2, 2.4, 2.2, 4.6, 0.16), st.beam(2.2, 4.6, -0.4, 7.2, 0.16), st.ring(2.2, 4.6, 3.1, dashed=True)),
        layout="range", masks="chain")
    add("03_直线弹道", "三道并排的直线",
        "几发弹体平行飞出，中间有空隙。站在空隙里打不中。",
        "扇形散射是另一张。并排的特点是方向相同、左右分开。",
        "空隙必须看得见。",
        lambda st: [st.beam(x, 0, x, 10, 0.55) for x in (-1.3, 0, 1.3)],
        masks="parallel")
    add("03_直线弹道", "呈扇形散开的多道直线",
        "格雷福斯的枪、麦林炮手的一排子弹，是从一个点散开的很多条线。",
        "卓尔游侠的多重射击也是向前散开，而不是并排。",
        "每条线单独有宽度。散开角度和条数都要能改。",
        lambda st: [ (st.beam(0, 0, math.sin(math.radians(a)) * 10, math.cos(math.radians(a)) * 10, 0.28), st.arrow(0, 0, math.sin(math.radians(a)) * 10, math.cos(math.radians(a)) * 10)) for a in (-28, -14, 0, 14, 28) ],
        masks="spread")
    add("03_直线弹道", "持续不停的光束",
        "拉克丝的激光、维克托的射线，会在路径上停留一会儿，不是一闪而过。",
        "持续光束和飞出去的箭不同：它存在的时候，走进去也会被打到。",
        "用更亮的填充表示「现在就在造成效果」。",
        lambda st: st.beam(0, 0, 0, 10.2, 0.85),
        masks="line_mid")
    add("03_直线弹道", "光束由窄变宽",
        "蓄力或飞得越远，命中宽度越大。",
        "若宽度不变，就用普通直线，不要用这张。",
        "近处窄、远处宽。缩放时不要把整张图拉扁。",
        lambda st: st.polygon([( -0.25, 0), (0.25, 0), (1.5, 10), (-1.5, 10)]),
        masks="line_taper")
    add("03_直线弹道", "只有中间一段是甜区",
        "太近没有额外效果，太远也没有，只有中间一段伤害更高或才能命中。",
        "环状甜区更常见。这张是把甜区放在直线上的版本。",
        "金色是甜区，前后灰色仍然可能有普通效果，或者完全没有，要由技能说明决定。这里灰色表示效果较弱。",
        lambda st: (st.beam(0, 0, 0, 3.2, 1.0, theme="muted"), st.beam(0, 3.2, 0, 7.2, 1.0, theme="sweet"), st.beam(0, 7.2, 0, 10.2, 1.0, theme="muted"), st.tag(2.4, 5.2, "甜区")),
        masks="line_sweet")
    add("03_直线弹道", "钩子的最远长度",
        "锤石、派克、诺提勒斯的钩，以及帕吉的肉钩，都是一条有最大长度的线。",
        "钩子打到人或墙会停。这张先表示最远能钩多远。",
        "末端的标记是钩爪，不是爆炸。",
        lambda st: (st.range_ring(9.5, dashed=True), st.beam(0, 0, 0, 9.5, 0.34), st.disk(0, 9.5, 0.55), st.cursor(0, 9.5)),
        layout="range", masks="hook")
    add("03_直线弹道", "两个单位之间被线拴住",
        "两个人之间有一条线，和从自己脚底射出去的技能射击不同。",
        "艾欧连接队友，或者一个技能同时拴住两个敌人。",
        "线的两端是单位，不是鼠标。",
        lambda st: (st.unit(-2.4, 2.2, "enemy"), st.unit(2.6, 6.4, "enemy"), st.beam(-2.4, 2.2, 2.6, 6.4, 0.2)),
        layout="range", caster=False, compass="两端的方块是被连接的单位。")
    add("03_直线弹道", "横在前面的一道波",
        "有些技能打出去的不是沿视线的长条，而是横向扫过的一条。",
        "布隆的大招落地后更接近一条横线。干扰者的雷云则是沿一条路连续爆炸。",
        "波的朝向和施法者面朝垂直。",
        lambda st: (st.beam(0, 0, 0, 4.2, 0.2, theme="range"), st.beam(-3.6, 4.2, 3.6, 4.2, 1.15), st.tag(0, 5.6, "横向冲击")),
        masks="wall")
    add("03_直线弹道", "沿着前进方向的一条路",
        "路径会留在地上一段时间。走进这条路才会吃到效果。",
        "毒液、火焰、岩石路，都是有长度和宽度的走廊。",
        "走廊从施法者脚底开始，向面朝延伸。",
        lambda st: st.beam(0, 0, 0, 10, 2.2),
        masks="line_wide")
    add("03_直线弹道", "冲刺路径和停下的位置",
        "人会沿这条线移动，停在末端的圆里。末端的圆是人的落点，不一定是伤害。",
        "巨角冲、扑击、短距离冲刺，都要让玩家看到会停在哪。",
        "如果冲刺会被人挡住，应改用「打到第一个就停」那张。",
        lambda st: (st.beam(0, 0, 0, 7.4, 0.85), st.disk(0, 7.4, 1.15), st.arrow(0, 0, 0, 6.6)),
        masks="dash")
    add("03_直线弹道", "起点、路径和终点是三段",
        "维克托的死亡射线要先定起点，再拉出方向。起点、激光和终点都可以有不同半径。",
        "DOTA2 的矢量施法也是先有一个点，再决定方向，但形状不一定分成三段。",
        "三个部分都要看得见。这是合成比例，真实技能可能每个半径都不同。",
        lambda st: (st.disk(0, 1.2, 1.25), st.beam(0, 1.2, 0, 7.4, 0.55), st.disk(0, 7.4, 1.7)),
        masks="three_part")
    add("03_直线弹道", "贴脸的一段没有效果",
        "直线最靠脚底的一段不造成效果，从某一米之后才开始。",
        "和环状甜区类似，只是发生在直线上。",
        "灰色是无效段，青色才开始计算。",
        lambda st: (st.beam(0, 0, 0, 2.4, 1.05, theme="muted"), st.beam(0, 2.4, 0, 10, 1.05), st.tag(2.2, 1.1, "无效")),
        masks="line_gap")
    add("03_直线弹道", "敌方从远处射来的一条线",
        "男枪大招、远处飞来的钩子，应该在命中前就让玩家看到线。",
        "狙击手的瞄准、远处的冲击波，也要用红色提前显示。",
        "施法者不在这张图的中心。线从远处指向这边。",
        lambda st: (st.beam(0, 10.5, 0, 1.2, 0.8), st.arrow(0, 10.5, 0, 1.2), st.tag(0, 0.2, "飞向这里")),
        theme="enemy", caster=False, compass="这条线从画面远处射来，红点不是你自己。")
    add("03_直线弹道", "穿过小兵，被英雄挡住",
        "很多箭会穿过小兵，打到英雄才停。",
        "月神之箭可以穿过小兵，打到英雄会停住。",
        "绿色是友方或小兵，红色方块是会挡住的英雄。线停在英雄处。",
        lambda st: (st.beam(0, 0, 0, 6.2, 0.6), st.unit(-0.2, 2.6, "ally"), st.unit(0.2, 6.2, "enemy"), st.tag(-2.4, 2.6, "穿过"), st.tag(2.4, 6.2, "停下")))

    for fov in (15, 30, 45, 60, 90, 120, 180, 270):
        add("04_扇形弧线", f"{fov} 度扇形",
            "安妮的碎裂之火、卡西奥佩娅的石化凝视，都是面向鼠标的扇形。角度按技能改变。",
            "莉娜的龙破斩是锥形。斯温、马格纳斯的分裂是身前一块扇形。",
            "弧线是最远距离，两条直线是左右边界。角度标在图上。",
            lambda st, fov=fov: (st.cone(0, 10, fov), st.tag(0, 5, f"{fov}°")),
            layout="center" if fov >= 180 else "forward", masks=f"cone_{fov}")
    add("04_扇形弧线", "扇形外面还有一圈最大距离",
        "如果扇形可以不打满，或者施法距离比扇形长度更远，就要两个图形。",
        "施法距离和作用长度在 DOTA2 里经常是两个数。",
        "虚线圈是最远能放到哪，实心扇形才是这一下会打到的形状。",
        lambda st: (st.range_ring(10, dashed=True), st.cone(0, 6.4, 70)),
        layout="range", masks="cone_60")
    add("04_扇形弧线", "扇形贴脸的一段没有伤害",
        "太近打不中，要站在扇形中后段。",
        "和整圈甜区是同一类规则，只是只覆盖前方。",
        "内圈被挖空。",
        lambda st: (st.cone(0, 10, 70, inner=3.2), st.tag(0, 1.4, "打不中")),
        masks="cone_dead")
    add("04_扇形弧线", "只有扇环那一段是甜区",
        "诺克萨斯之手是整圈。这张是只挥前方、并且只有中距离能打中的版本。",
        "适合前方劈砍的甜区，不适合绕身一圈的挥击。",
        "金色环带才是判定。",
        lambda st: st.cone(0, 10, 80, inner=6.2),
        theme="sweet", masks="sector_ring")
    add("04_扇形弧线", "打向背后",
        "少数反击或背刺会覆盖身后，而不是鼠标所在的前方。",
        "面朝和图上的扇形方向相反时，要单独做一张，避免程序把扇形永远放在鼠标方向。",
        "箭头仍表示角色脸的朝向，扇形在相反方向。",
        lambda st: st.cone(180, 7.5, 70),
        masks="cone_back")
    add("04_扇形弧线", "扇形正在旋转",
        "引导过程中，扇形绕着施法者转。玩家要看到它下一刻会扫到哪。",
        "剑刃风暴更接近整圈。这张是还没合成整圈的旋转锥形。",
        "小箭头表示正在转动的方向。",
        lambda st: (st.cone(28, 8.5, 55), st.arrow(0, 0, 3.2, 1.2), st.tag(3.6, 2.4, "在转")),
        layout="center")
    add("04_扇形弧线", "普攻前方的劈砍",
        "近战分裂、顺劈，只影响身前一块弧，不会打到背后。",
        "斯温和马格纳斯的分裂是这一种。英雄联盟的盖伦旋转则是整圈，不要用这张。",
        "弧比较贴身，半径通常只比攻击距离再长一点。",
        lambda st: st.cone(0, 4.6, 110),
        layout="center", masks="cleave")
    add("04_扇形弧线", "只挡住正面的盾",
        "布隆把盾举在一个方向，那个扇形里的飞行物会被挡下。",
        "正面减伤、正面格挡，都应该让玩家看到盾覆盖的角度。",
        "这是防御范围，不是伤害范围。颜色仍用自身青色，说明里写的是挡住。",
        lambda st: (st.cone(0, 3.6, 90), st.tag(0, 2.2, "挡住")),
        layout="center", masks="cone_90")

    add("05_矢量双击", "先点起点，再拉方向",
        "维克托的死亡射线是拖出一条线。按下是起点，松开的方向是射线方向。",
        "DOTA2 的矢量施法更明确：暗影恶魔的崩裂禁锢、电炎绝手的大招、石鳞剑士的剑舞，都是先选点再选方向。",
        "1 是第一次点击，2 是方向。施法距离圈仍然在英雄脚底。",
        lambda st: (st.range_ring(10), st.disk(0.6, 3.4, 0.7), st.beam(0.6, 3.4, 3.4, 8.2, 0.7), st.arrow(0.6, 3.4, 3.4, 8.2), st.cursor(3.4, 8.2), st.tag(0.6, 2.4, "1"), st.tag(3.4, 9.1, "2")),
        layout="range", masks="line_mid")
    add("05_矢量双击", "矢量出来的区域有宽度",
        "方向定了以后，命中的不是一条没有宽度的线，而是一条带宽度的路。",
        "矢量技能如果会击退或留下火焰，通常都有宽度。",
        "宽度从第一点开始算，不是从英雄脚底开始算。",
        lambda st: (st.range_ring(9, dashed=True), st.beam(-1.5, 3.2, 2.4, 8.4, 1.6), st.arrow(-1.5, 3.2, 2.4, 8.4)),
        layout="range", masks="line_wide")
    add("05_矢量双击", "方向的长度被限制",
        "鼠标拉得很远，真正的矢量只到最大长度，多余的部分用虚线表示。",
        "矢量技能同样有最大距离，不能无限拉长。",
        "实线是会生效的长度，虚线鼠标表示你还想拉得更远。",
        lambda st: (st.beam(0, 2.2, 0, 8.2, 0.9), st.cursor(0, 8.2), st.cursor(0, 11.2, ghost=True), st.tag(0, 11.8, "鼠标还在外面")),
        layout="range")
    add("05_矢量双击", "沿方向放下一堵墙",
        "塔莉垭、安蓓萨一类技能会沿手势放一段墙。墙有长度，也有厚度。",
        "电狗的电网更接近一圈。这张是直的一段。",
        "墙是横向的厚线，方向箭头告诉你它朝哪延伸。",
        lambda st: (st.beam(0, 0, 0, 3.2, 0.16, theme="range"), st.beam(-3.4, 3.2, 3.4, 3.2, 0.55), st.arrow(-3.4, 3.2, 3.4, 3.2)),
        masks="wall")
    add("05_矢量双击", "沿矢量冲出去",
        "石鳞剑士会沿选定的方向突进，路径上的单位被打中。",
        "英雄联盟里要选方向的短冲刺，也可以用同一张：一条路加上终点。",
        "人停在箭头末端。",
        lambda st: (st.beam(0, 0, 2.6, 8.4, 1.0), st.disk(2.6, 8.4, 1.0), st.arrow(0, 0, 2.6, 7.6)),
        masks="dash")
    add("05_矢量双击", "只选择面朝哪边",
        "卡西奥佩娅的大招、布隆举盾，没有落点，只有一个方向。",
        "DOTA2 很多技能朝鼠标方向放出，但不需要先点一个地面点。",
        "不要再画第二个圆心。扇形或箭头就够了。",
        lambda st: (st.cone(0, 6.5, 50), st.cursor(0, 8.5)),
        layout="center", masks="cone_45")
    add("05_矢量双击", "两个点连成作用线",
        "第一次点起点，第二次点终点，两点之间就是技能。",
        "和「起点固定在英雄身上的直线」不同，这条线可以离开英雄一段距离。",
        "英雄仍要在施法距离内才能够到起点。",
        lambda st: (st.range_ring(10), st.disk(-1.2, 3.2, 0.8), st.disk(2.8, 7.4, 0.8), st.beam(-1.2, 3.2, 2.8, 7.4, 0.45), st.tag(-1.2, 2.2, "起点"), st.tag(2.8, 8.4, "终点")),
        layout="range")

    add("06_自身范围", "以自己为圆心立刻生效",
        "盖伦的旋转、阿木木和凯南的大招，圆心就是自己，不用再选点。",
        "斧王的咆哮、谜团的黑洞、潮汐的毁灭，也是以自己为中心。",
        "没有鼠标落点。圈跟着人走，直到技能结束。",
        lambda st: st.disk(0, 0, 4.6),
        layout="center", masks="circle_r")
    add("06_自身范围", "引导时圆才慢慢生效",
        "需要读条的自身范围。圆上的填充表示进度，不是另一块伤害。",
        "谜团的黑洞需要引导。断掉的话，进度应该停或消失。",
        "这张是大约四分之三。底图里有 0%、25%、50%、75%、100% 五档。",
        lambda st: (st.pie(0.75, 5.2), st.tag(0, 0.2, "75%")),
        layout="center", masks="channel_frames")
    add("06_自身范围", "以自己为中心的一圈环",
        "诺克萨斯之手的挥砍绕自己一圈，贴太近的人打不中。",
        "静滞结界若放在自己身上，也是一个环，而不是填满的圆。",
        "中间留空。",
        lambda st: (st.disk(0, 0, 6.4, hole=3.8, theme="sweet"), st.tag(0, 0.2, "太近")),
        layout="center", theme="sweet", masks="donut")
    add("06_自身范围", "一圈冲击波向外扩",
        "从身体向外推出的环，越往外圈越大，经过的地方才受影响。",
        "和一开始就铺满的圆不同。玩家看到的是正在移动的那一环。",
        "三环表示时间顺序，最外的实线是当前位置。",
        lambda st: (st.ring(0, 0, 2.2, theme="muted"), st.ring(0, 0, 4.2, theme="muted"), st.ring(0, 0, 6.4), st.tag(0, 7.2, "现在")),
        layout="center", masks="shockwave")
    add("06_自身范围", "同时存在好几层环",
        "不同距离有不同效果，而且同时存在，不是播动画的三个时刻。",
        "多层光环、多层爆炸，每一层半径都要单独配置。",
        "不要做成一张收缩动画。",
        lambda st: (st.ring(0, 0, 2.4), st.ring(0, 0, 4.4, theme="sweet"), st.ring(0, 0, 6.6, theme="ally")),
        layout="center")
    add("06_自身范围", "绕着自己转一整圈",
        "盖伦、孙悟空的旋转会打到四周，不只是前方。",
        "主宰的剑刃风暴也是绕自己，而不是面朝扇形。",
        "用整圆。若背后打不到，改用 270 度那张。",
        lambda st: (st.disk(0, 0, 4.2), st.arrow(0, 0, 2.8, 1.6)),
        layout="center", masks="circle_r")
    add("06_自身范围", "扇形锁在面朝方向",
        "扇形跟着角色的脸，不跟着鼠标重新选点。人转向，扇形才转。",
        "和「点击后朝鼠标放出一次」不同，这是持续挂在身上的朝向范围。",
        "箭头是脸的方向。",
        lambda st: st.cone(0, 5.4, 80),
        layout="center", masks="cone_90")
    add("06_自身范围", "把周围的人向中间拉",
        "奥莉安娜和纳尔可以把人拉向一个点。黑暗贤者的真空会把人吸进去。",
        "箭头指向圆心。圆心可能是自己，也可能是落点。这张圆心是自己。",
        "拉扯方向和击退方向必须相反，不能共用一张箭头。",
        lambda st: (st.ring(0, 0, 5.4), st.arrow(-3.2, 2.2, -1.2, 0.8), st.arrow(3.2, 2.2, 1.2, 0.8), st.arrow(0, 4.6, 0, 2.2), st.tag(0, 6.4, "向内")),
        layout="center", masks="pull")
    add("06_自身范围", "把周围的人向外推",
        "李青把人踢开、击退类技能，箭头从中心指向外面。",
        "蝙蝠骑士的烈焰破击、小小的投掷落地，都会把人推离中心。",
        "这张是推开，不是吸进来。",
        lambda st: (st.ring(0, 0, 5.2), st.arrow(-1.2, 1.2, -3.4, 3.2), st.arrow(1.2, 1.2, 3.4, 3.2), st.arrow(0, 1.6, 0, 4.6), st.tag(0, 6.2, "向外")),
        layout="center", masks="push")
    add("06_自身范围", "很淡的一圈一直跟着自己",
        "不是按下去才出现，而是站在旁边就能受到影响的光环。",
        "团队回蓝、护甲光环、伤病光环，都应该淡，避免和正在瞄准的技能抢颜色。",
        "瞄准别的技能时，这圈仍然可以留着，但要更淡。",
        lambda st: st.ring(0, 0, 5.8, fill=True, width_m=0.06),
        layout="center", theme="ally", masks="ring_fill")

    add("07_放置地形", "眼的位置和视野",
        "插眼要同时看到眼会落在哪，以及它能照亮的范围。",
        "侦查守卫与岗哨守卫的视野不同。岗哨还要额外标出真实视野。",
        "小点是眼，虚线大圈是视野。",
        lambda st: (st.range_ring(8), st.disk(1.5, 4.2, 0.45, theme="ally"), st.ring(1.5, 4.2, 3.6, theme="ally", dashed=True), st.cursor(1.5, 4.2)),
        layout="range", theme="ally", masks="reticle + ring_dashed")
    add("07_放置地形", "陷阱的触发圈",
        "吉格斯的炸弹、女警的夹子，本体很小，触发范围更大。",
        "炸弹人的地雷、圣堂刺客的陷阱也是本体小、触发圈大。",
        "小点是陷阱，大圈是踩进去会触发的范围。",
        lambda st: (st.disk(0, 4.5, 0.4), st.ring(0, 4.5, 1.8, dashed=True), st.cursor(0, 4.5), st.range_ring(9)),
        layout="range", masks="reticle + ring_dashed")
    add("07_放置地形", "放下一截直墙",
        "墙有长度和厚度，可以挡住走位。它不是伤害走廊。",
        "塔莉垭的石墙、安蓓萨的墙，玩家要看出墙从哪到哪。",
        "橙色表示这是地形，不只是伤害区域。",
        lambda st: (st.range_ring(8, dashed=True), st.beam(-2.8, 4.4, 2.8, 4.4, 0.62, theme="block"), st.cursor(0, 4.4)),
        layout="range", theme="block", masks="wall")
    add("07_放置地形", "召唤物要放在这个圈里",
        "炮台、士兵、治疗守卫，要显示能放多远，以及这个单位本身占多大。",
        "魅惑魔女、德鲁伊放熊，也要有一个合法放置点。",
        "大圈是施法距离，小圈是这个单位的占地或攻击范围。这里小圈表示占地。",
        lambda st: (st.range_ring(9), st.disk(0, 5, 1.1), st.cursor(0, 5), st.tag(0, 6.6, "放在这里")),
        layout="range")
    add("07_放置地形", "一块矩形障碍",
        "不是角色前方的攻击矩形，而是放在地上、别人走不过去的一块。",
        "网、格子、石块都可以用矩形表示。",
        "可以旋转。这张是斜放，避免以后只做横竖两种。",
        lambda st: st.polygon([(-1.2, 2.2), (3.4, 4.4), (2.2, 6.6), (-2.4, 4.4)]),
        theme="block", masks="rect_flat")
    add("07_放置地形", "把人困在一个环里",
        "玛尔斯的竞技场和发条的齿轮，都会形成一个圈，人要从边缘出去。",
        "圈内和圈外哪边安全，要看技能。这张强调的是环本身会挡住。",
        "环上有柱，柱之间用细线连接。",
        lambda st: (st.ring(0, 0, 5, theme="enemy"), [st.disk(math.sin(math.radians(i)) * 5, math.cos(math.radians(i)) * 5, 0.42, theme="enemy") for i in range(0, 360, 40)]),
        layout="center", theme="enemy", masks="pillars")
    add("07_放置地形", "几个点互相连起来",
        "发条的齿轮若彼此够近，会在中间连出电流。阿兹尔的士兵也可以连成一堵墙。",
        "点是实体，线是它们之间临时出现的连接。",
        "距离太远就不要连线。图上的虚线圈是能够连上的最远距离。",
        lambda st: (st.ring(-2.2, 2.4, 3.2, dashed=True, theme="range"), st.disk(-2.2, 2.4, 0.45), st.disk(1.4, 3.2, 0.45), st.disk(-0.2, 5.6, 0.45), st.beam(-2.2, 2.4, 1.4, 3.2, 0.16), st.beam(-2.2, 2.4, -0.2, 5.6, 0.16)),
        layout="range")
    add("07_放置地形", "一对可以穿梭的门",
        "进一个门，会从另一个门出来。两个点必须成对出现。",
        "正作里单独的传送落点更多。这张专门留给成对的门。",
        "两个圆用一条虚线连起来，表示它们是同一对。",
        lambda st: (st.disk(-3, 2.2, 1.3, theme="ally"), st.disk(3, 6.2, 1.3, theme="ally"), st.beam(-3, 2.2, 3, 6.2, 0.12, theme="range"), st.tag(-3, 3.8, "入口"), st.tag(3, 7.8, "出口")),
        layout="range", theme="ally", caster=False, compass="两个绿圈是一对门。")
    add("07_放置地形", "传送的入口和出口",
        "卡萨丁、闪现、敌法师，都是从现在的位置到另一个点。",
        "回城则是在自己脚底读条，出口在泉水或选定点。",
        "脚底是入口，远处的准星是出口。",
        lambda st: (st.range_ring(9), st.ring(0, 0, 1.1), st.cursor(0, 6.4), st.beam(0, 0, 0, 6.4, 0.12, theme="range"), st.tag(1.8, 6.4, "出口")),
        layout="range", masks="reticle")
    add("07_放置地形", "一片地雷",
        "一次放置多个陷阱，每个都有自己的小触发圈。",
        "炸弹人可以在一片区域里埋多个地雷。",
        "大虚线是允许埋放的区域，小圈才是每一颗雷。",
        lambda st: (st.ring(0, 4, 4, dashed=True), [st.disk(x, y, 0.7) for x, y in ((-1.6, 2.6), (1.2, 3.2), (-0.4, 4.8), (1.8, 5.4), (-1.8, 5.8))]),
        layout="range", masks="scatter")
    add("07_放置地形", "身后留下一条路径",
        "辛吉德的毒、塔莉垭走过的地面、毒龙的毒液，会在走过的地方留下带子。",
        "路径是已经发生的轨迹，不是还在瞄准的直线。",
        "带子可以是弯的，宽度保持不变。",
        lambda st: st.curve((0, 0), (-2.4, 3.5), (0.8, 8.2), 1.3),
        masks="curve")
    add("07_放置地形", "按住不放时区域跟着鼠标",
        "还没松手，区域就跟着鼠标移动。松手才定下来。",
        "矢量拖动和这种「跟着走的预览」是同一类操作反馈。",
        "用虚线表示还没确认，避免和已经生效的实心圈混淆。",
        lambda st: (st.range_ring(9), st.ring(2.4, 5.2, 2.2, dashed=True), st.cursor(2.4, 5.2), st.tag(2.4, 8, "还没松开")),
        layout="range")

    add("08_单位目标", "只能点敌方单位",
        "指向性控制、单体伤害，点在地上没有效果。",
        "DOTA2 的单位目标技能点空地不会施放。点到范围外的敌人也不会放。",
        "敌方在圈内用实色，圈外的敌人变灰，表示点不到。",
        lambda st: (st.range_ring(8), st.unit(0.4, 4.6, "enemy"), st.unit(3.4, 9.4, "enemy"), st.disk(3.4, 9.4, 0.7, theme="muted"), st.tag(3.6, 10.4, "太远")),
        layout="range", theme="enemy")
    add("08_单位目标", "只能点友方单位",
        "治疗和护盾指向队友。点到敌人无效。",
        "戴泽的治疗、光之守卫的查克拉，都是友方单位目标。",
        "友方用绿色。敌人即使站在圈里也不应被选中。",
        lambda st: (st.range_ring(8), st.unit(-1.2, 4.2, "ally"), st.unit(2.2, 3.4, "enemy"), st.tag(-1.2, 5.6, "可以"), st.tag(2.2, 4.6, "不行")),
        layout="range", theme="ally")
    add("08_单位目标", "敌方和友方都能点",
        "少数技能对两边都有效，但效果可能相反，例如治疗友方、伤害敌方。",
        "DOTA2 里这类技能很常见。光标或目标描边要能看出当前指着的是谁。",
        "两个都在距离内，所以都是实色。真正点中谁，用光标下的那一个表示。",
        lambda st: (st.range_ring(8), st.unit(-1.6, 4.4, "ally"), st.unit(1.8, 5.2, "enemy"), st.cursor(1.8, 5.2)),
        layout="range")
    add("08_单位目标", "双击或对自己施放",
        "有些治疗可以点自己。对自己时，高亮画在自己身上，不再需要地面落点。",
        "DOTA2 双击单位目标技能，通常就是对自己施放。",
        "脚底一圈细环表示目标是自己。",
        lambda st: (st.ring(0, 0, 1.5), st.tag(0, 2.4, "对自己")),
        layout="center", masks="ring_thin")
    add("08_单位目标", "这个单位不能作为目标",
        "点到无敌、不可选、阵营不对或距离不够的单位，用红色禁止，而不是普通敌方红圈。",
        "魔法不够时也是放不了，但那是施法者的状态。这张是目标不合法。",
        "红色斜向的感觉用虚线和禁止文字表达，避免和敌方预警圆混在一起。",
        lambda st: (st.range_ring(8), st.unit(0, 4.4, "enemy"), st.ring(0, 4.4, 1.3, theme="invalid", dashed=True), st.tag(0, 6.2, "不能点")),
        layout="range", theme="invalid")
    add("08_单位目标", "点地面或点单位都可以",
        "点在单位身上就锁定那个单位，点在空地上就按落点结算。",
        "有的 DOTA2 技能既可以选择单位，也可以点地。",
        "单位上有一圈，地面上还有一个淡的落点，表示两种都合法。",
        lambda st: (st.range_ring(9), st.unit(-1.4, 4.2, "enemy"), st.ring(-1.4, 4.2, 1.2), st.disk(2.2, 6.2, 1.4), st.cursor(2.2, 6.2)),
        layout="range")
    add("08_单位目标", "只能点树或特殊目标",
        "英雄联盟几乎没有树木目标。这张主要给 DOTA2：小小可以扔树，树精可以抓树。",
        "点到英雄或地面都无效，只有特殊目标会亮。",
        "绿色三角是树。英雄即使更近也不能选。",
        lambda st: (st.range_ring(8), st.unit(-1.5, 3.6, "enemy"), st.unit(1.2, 5.2, "tree"), st.ring(1.2, 5.2, 1.2, theme="ally"), st.tag(1.2, 6.8, "只能点树")),
        layout="range", theme="ally")
    add("08_单位目标", "一次选中好几个单位",
        "技能会在几个单位之间连接，所有被选中的都要有标记。",
        "暗影恶魔可以连续点，或一个技能跳过多个目标。",
        "线表示它们会被同一次技能连到，不是地形墙。",
        lambda st: (st.range_ring(9), st.unit(-2, 3, "enemy"), st.unit(1.6, 4.4, "enemy"), st.unit(-0.6, 7, "enemy"), st.beam(0, 0, -2, 3, 0.14), st.beam(-2, 3, 1.6, 4.4, 0.14), st.beam(1.6, 4.4, -0.6, 7, 0.14)),
        layout="range", theme="enemy", masks="chain")
    add("08_单位目标", "四种光标",
        "敌方、友方、不能选、点地面，光标应该一眼能分开。",
        "DOTA2 的鼠标会随目标类型变化。英雄联盟更多靠地面指示器，但点人时仍要有目标描边。",
        "这张是界面光标，不是地面范围。",
        lambda st: (st.unit(-4.5, 2, "enemy"), st.cursor(-4.5, 2), st.tag(-4.5, 3.6, "敌"),
                    st.unit(-1.5, 2, "ally"), st.cursor(-1.5, 2), st.tag(-1.5, 3.6, "友"),
                    st.unit(1.5, 2, "enemy"), st.ring(1.5, 2, 1.1, theme="invalid", dashed=True), st.tag(1.5, 3.8, "不行"),
                    st.cursor(4.5, 2), st.tag(4.5, 3.4, "地面")),
        layout="center", caster=False, compass="四个例子从左到右是敌方、友方、不合法、地面。")

    add("09_特殊形状", "从身上延伸出去的矩形",
        "和点在远处的矩形不同，这块矩形的一条边贴着施法者。",
        "影魔的影压就是身前一条矩形。男枪和俄洛伊的大招更长。",
        "近的一边短，远的一边可以更宽。这张两边一样宽。",
        lambda st: st.polygon([(-1.4, 0.2), (1.4, 0.2), (1.4, 8.8), (-1.4, 8.8)]),
        masks="rect_forward")
    add("09_特殊形状", "螺旋形",
        "范围绕着中心一圈圈扩出去。适合持续旋转的自定义技能。",
        "正作里整圆旋转更多。螺旋用来表示越转越远。",
        "线本身是路径。若每一段都有宽度，缩放时要保持线宽。",
        lambda st: st.polyline([(math.cos(t) * (0.35 + t * 0.72), math.sin(t) * (0.35 + t * 0.72)) for t in [i * 0.42 for i in range(0, 24)]], 0.38),
        layout="center", masks="spiral")
    add("09_特殊形状", "有东西绕着目标转",
        "发条的球、奥瑞利安索尔的星星，会绕着一个中心转，危险在轨道上。",
        "轨道是虚线，当前位置是实心点。",
        "不要把整条轨道都涂成已经命中。",
        lambda st: (st.ring(0, 0, 4.6, dashed=True), st.disk(4.6, 0, 0.7), st.arrow(3.2, 0.2, 4.4, 1.6), st.tag(0, 0.2, "中心")),
        layout="center", masks="orbit")
    add("09_特殊形状", "从空中砸下来的一个圈",
        "潘森大招、天降的打击，圈在落地前就应该出现。",
        "宙斯的落雷、大范围天降技能，都是先看到圈再受到伤害。",
        "中间的十字表示砸下的中心。",
        lambda st: (st.disk(0, 0, 3.6), st.beam(-0.7, 0, 0.7, 0, 0.12), st.beam(0, -0.7, 0, 0.7, 0.12), st.tag(0, 4.4, "从上方")),
        layout="center", theme="enemy", caster=False, compass="这是落点，不是施法者脚底。", masks="reticle")
    add("09_特殊形状", "恐惧后会往哪边跑",
        "恐惧不是随机跑。常见规则是远离恐惧来源。",
        "箭头要从被恐惧的人指向远离来源的方向。",
        "来源在中心，三个敌人朝外。",
        lambda st: (st.unit(-2.4, 1.6, "enemy"), st.unit(2.2, 2.4, "enemy"), st.unit(0.2, 4.2, "enemy"),
                    st.arrow(-2.4, 1.6, -4.2, 0.4), st.arrow(2.2, 2.4, 4.2, 1.6), st.arrow(0.2, 4.2, 0.4, 6.4)),
        layout="center", theme="enemy")
    add("09_特殊形状", "分身会出现在哪里",
        "乐芙兰、悟空、幻影长矛手的分身，玩家要知道分身相对自己的位置。",
        "虚线连接本体和分身。分身用更淡的圈。",
        "如果分身出现在鼠标处，还要再加施法距离圈。这张是固定在身侧。",
        lambda st: (st.ring(3.2, 1.4, 1.1, dashed=True), st.beam(0, 0, 3.2, 1.4, 0.12, theme="range"), st.tag(3.2, 2.8, "分身")),
        layout="center")
    add("09_特殊形状", "宠物活动范围",
        "宠物不能无限追。圈是它还能活动的范围，不是主人的攻击距离。",
        "德鲁伊的熊、治疗守卫、虚空虫，都可以用这种圈。",
        "小点是宠物，大圈是它的活动边界。",
        lambda st: (st.ring(0, 0, 6.2, theme="ally"), st.disk(2.2, 1.4, 0.45, theme="ally"), st.tag(2.2, 2.3, "宠物")),
        layout="center", theme="ally")
    add("09_特殊形状", "第二段换成另一种形状",
        "第一段是直线，第二段可以改成圆、改方向或再放一次。",
        "维克托拉出射线后，有的技能还能再点一次改变效果。图标亮起表示第二段准备好了。",
        "左边是第一段，右边是第二段。不要画成同一次命中的两个部分。",
        lambda st: (st.beam(-3.4, 0, -3.4, 6.5, 0.6), st.disk(3.2, 3.4, 2.2, theme="sweet"), st.tag(-3.4, 7.4, "第一段"), st.tag(3.2, 6.2, "第二段")),
        layout="center")
    add("09_特殊形状", "敌方、友方和自己叠在同一张图",
        "真正打架时，三种圈会同时存在。颜色必须在重叠后还能分开。",
        "自己的瞄准用青，队友的区域用绿，敌人的预警用红。",
        "重叠的地方不要融成一种新颜色。边线比填充更重要。",
        lambda st: (st.disk(-2.2, 1.2, 3.2, theme="self"), st.disk(2.4, 1.4, 3, theme="ally"), st.disk(0.2, 3.6, 2.4, theme="enemy")),
        layout="center", caster=False, compass="青是自己，绿是友方，红是敌方。三个圈故意叠在一起。")

    add("10_技能栏与小地图", "四个技能槽的正常样子",
        "英雄联盟和 DOTA2 都把技能放在下方正中。本项目目前是 Q、W、E、R，文字写着未实现。",
        "DOTA2 还会显示魔法消耗、冷却和等级。英雄联盟还会显示升级箭头。",
        "这张只定槽位，不代表已经做了技能。",
        draw_slots_normal, layout="ui", caster=False, compass="左侧是技能栏示意，不是地面范围。")
    add("10_技能栏与小地图", "鼠标放在技能上就出现距离圈",
        "还没点击，只是悬停。地面应该出现距离圈，鼠标不要同时去点地移动。",
        "DOTA2 悬停技能能看到说明和距离；按住 Alt 不用悬停也能看到。",
        "右下角这张小图就是此时地面上该出现的圈。",
        draw_slots_hover, layout="ui", caster=False, compass="左边是技能栏，右边小图是悬停时的地面。")
    add("10_技能栏与小地图", "冷却时的遮罩",
        "冷却用从上方开始、顺时针减少的扇形遮罩，中间写剩余秒数。",
        "DOTA2 也是图标变暗并显示剩余时间。转好之前不能显示成可以施放。",
        "底图提供 0% 到 100% 的白色遮罩，方向和 Unity 径向填充、从上方开始、顺时针一致。",
        draw_slots_cooldown, layout="ui", caster=False, compass="遮罩剩下的部分就是还要等的时间。", masks="cooldown")
    add("10_技能栏与小地图", "魔法不够",
        "图标压暗，消耗数字用红色。和冷却不同，它没有转圈。",
        "DOTA2 魔法不足时技能变灰，蓝量数字变红。",
        "不要和沉默用同一张。沉默是状态，没蓝是资源。",
        draw_slots_mana, layout="ui", caster=False, compass="红色数字是魔法不够。")
    add("10_技能栏与小地图", "被沉默",
        "人被沉默时，技能形状可以预览，但不能放。槽上要有明确的禁止。",
        "DOTA2 沉默会让多数主动技能无法释放，图标状态和普通冷却不同。",
        "地面指示若还显示，应使用灰色那张距离圈。",
        draw_slots_silence, layout="ui", caster=False, compass="四个槽一起禁止。")
    add("10_技能栏与小地图", "技能等级点",
        "英雄联盟普通技能通常最多 5 级，大招 3 级。DOTA2 多数是 4 级，大招常是 3 级。",
        "没学的技能不应该显示成可以施放。",
        "点亮的菱形是已经学了的等级。",
        draw_slots_ranks, layout="ui", caster=False, compass="上排按英雄联盟的 5 级和 3 级，下排按 DOTA2 常见的 4 级。")
    add("10_技能栏与小地图", "终极技能还不能学",
        "等级不够时，R 是锁住的，不是冷却。",
        "DOTA2 大招通常 6 级才开始学。英雄联盟也要到固定等级。",
        "锁和冷却遮罩不要混用。",
        draw_slots_ult_lock, layout="ui", caster=False, compass="只有 R 被锁住。")
    add("10_技能栏与小地图", "充能层数",
        "有些技能没有普通冷却，而是有层数。层数为 0 才不能放。",
        "黑默丁格的炮台、一些陷阱和 DOTA2 的充能技能，都用次数表示。",
        "数字放在右下角，避免挡住快捷键。",
        draw_slots_charges, layout="ui", caster=False, compass="右下角是剩余层数。")
    add("10_技能栏与小地图", "开关类技能",
        "辛吉德的毒、一些光环可以开着不关。开和关要能立刻看出来。",
        "DOTA2 可以开关的光环、法球，开启时图标更亮。",
        "左边是关，右边是开。",
        draw_slots_toggle, layout="ui", caster=False, compass="开着的时候边框是青色。")
    add("10_技能栏与小地图", "自动施法",
        "右键点技能后，攻击时会自动放出。边框用连续的亮环表示。",
        "DOTA2 的法球自动施放就是这种。英雄联盟也有少量自动施法。",
        "自动施放不等于正在冷却。",
        draw_slots_autocast, layout="ui", caster=False, compass="W 的亮边表示自动施放开着。", masks="autocast")
    add("10_技能栏与小地图", "引导读条",
        "技能已经开始，在读条结束前可以被打断。条是时间，不是距离。",
        "水晶室女冰封、谜团黑洞、回城，都有这种持续。",
        "条放在角色附近或屏幕下方，不要画进地面伤害圈里。",
        draw_castbar, layout="ui", caster=False, compass="这是时间条，不是地面范围。")
    add("10_技能栏与小地图", "蓄力条",
        "按住时条变长，松手才按照当前长度放出。和读条的差别是：读条结束会自己放，蓄力由玩家决定何时放。",
        "韦鲁斯和泽拉斯的蓄力属于这种。",
        "条满表示已经到最大距离，对应直线蓄力拉满的那张地面图。",
        draw_chargebar, layout="ui", caster=False, compass="松手才施放。")
    add("10_技能栏与小地图", "小地图上的危险圈",
        "范围大到屏幕里看不全时，小地图上也要有一个圈。",
        "潘森大招、全图或超大范围技能，玩家经常先在小地图上看到。",
        "小地图圈用红色，和地面敌方预警一致。",
        draw_minimap_circle, layout="ui", caster=False, compass="右边是小地图，不是地面特写。")
    add("10_技能栏与小地图", "小地图上的技能方向",
        "从很远的地方射来的直线，小地图上用一条短线表示方向。",
        "男枪大招这类跨屏幕技能，只看地面容易不知道从哪边来。",
        "线和地面上的红色直线是同一个技能。",
        draw_minimap_line, layout="ui", caster=False, compass="小地图上的线表示飞来的方向。")
    add("10_技能栏与小地图", "第二段已经可以再按",
        "第一段放完后，同一个键会亮起来，提示现在可以放第二段。",
        "有的技能第二段形状完全不同，所以地面指示也要换成下一张。",
        "亮边表示可以再按，不是自动施法。自动施法是另一张。",
        draw_slots_recast, layout="ui", caster=False, compass="E 亮起，表示第二段准备好了。")
    add("10_技能栏与小地图", "敌方正在放一个危险技能",
        "除了地面红圈，屏幕边缘或目标头上还可以有一个危险标记。",
        "被大招指中、被钩子瞄准时，不要只靠地面，因为镜头可能没看向那边。",
        "标记用红色三角，和友方信号分开。",
        draw_enemy_warning, layout="ui", caster=False, compass="红色三角是危险，不是技能按钮。")


def draw_panel_bg(image):
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle(CARD, radius=28, fill=(18, 28, 34))
    draw.rounded_rectangle(PANEL, radius=28, fill=(14, 22, 28))
    draw.rounded_rectangle([PANEL[0], PANEL[1], PANEL[2], PANEL[1] + 8], radius=4, fill=(196, 168, 99))


def clip_card(world, scale):
    mask_image = Image.new("L", world.size, 0)
    box = [v * scale for v in CARD]
    ImageDraw.Draw(mask_image).rounded_rectangle(box, radius=28 * scale, fill=255)
    r, g, b, a = world.split()
    return Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, mask_image)))


def draw_tags(image, stage):
    draw = ImageDraw.Draw(image)
    face = FONT[18]
    for x, y, text in stage.tags:
        px, py = stage.ox + x * stage.ppm, stage.oy - y * stage.ppm
        if not (CARD[0] + 8 <= px <= CARD[2] - 8 and CARD[1] + 8 <= py <= CARD[3] - 8):
            continue
        draw.text((px + 1, py + 1), text, font=face, fill=(0, 0, 0, 200), anchor="mm")
        draw.text((px, py), text, font=face, fill=(236, 244, 246), anchor="mm")


def draw_info(image, scene, code):
    draw = ImageDraw.Draw(image)
    x, y = PANEL[0] + 28, PANEL[1] + 28
    width = PANEL[2] - PANEL[0] - 56
    draw.text((x, y), code, font=FONTB[22], fill=(196, 168, 99))
    y += 36
    for line in wrap(scene["title"], FONTB[30], width):
        draw.text((x, y), line, font=FONTB[30], fill=(236, 244, 246))
        y += 38
    y += 8
    fields = [
        ("看到什么", scene["note"]),
        ("英雄联盟", scene["lol"]),
        ("DOTA2", scene["dota"]),
        ("颜色", THEME_LABEL[scene["theme"]]),
        ("怎么读图", scene["compass"] or "上方是面朝方向。圆点和箭头是施法者，箭头指向前方。"),
        ("对应底图", scene["masks"] or "这张是用法示意，形状用同目录里的可染色底图组合。"),
    ]
    for title, body in fields:
        draw.text((x, y), title, font=FONTB[22], fill=(196, 168, 99))
        y += 30
        for line in wrap(body, FONT[20], width):
            draw.text((x, y), line, font=FONT[20], fill=(214, 224, 222))
            y += 26
        y += 10
    footer = "原创示意图，不是官方素材。实线才是范围。"
    draw.text((x, PANEL[3] - 42), footer, font=FONT[16], fill=(150, 164, 170))


def render_scene(scene, code):
    composed = Image.new("RGBA", (W, H), (10, 16, 20, 255))
    draw_panel_bg(composed)
    if scene["layout"] != "ui":
        ox, oy, ppm = LAYOUTS[scene["layout"]]
        world = Image.new("RGBA", (W * SCALE, H * SCALE), (0, 0, 0, 0))
        stage = Stage(world, ox, oy, ppm, SCALE, scene["theme"])
        stage.grid()
        scene["draw"](stage)
        if scene["caster"]:
            stage.caster()
        stage.paint_cursors()
        composed.alpha_composite(clip_card(world, SCALE).resize((W, H), Image.Resampling.LANCZOS))
        draw_tags(composed, stage)
    else:
        scene["draw"](composed)
    draw_info(composed, scene, code)
    return composed.convert("RGB")


def ui_slot(draw, x, y, key, fill, edge, sub="", badge="", dim=False):
    box = [x, y, x + 132, y + 132]
    draw.rounded_rectangle(box, radius=16, fill=(28, 40, 46) if not dim else (22, 26, 30), outline=edge, width=4)
    draw.text((x + 66, y + 42), key, font=FONTB[34], fill=(196, 168, 99), anchor="mm")
    if sub:
        draw.text((x + 66, y + 92), sub, font=FONT[18], fill=fill, anchor="mm")
    if badge:
        draw.rounded_rectangle([x + 86, y + 92, x + 124, y + 122], radius=8, fill=(12, 18, 22))
        draw.text((x + 105, y + 107), badge, font=FONTB[22], fill=(236, 244, 246), anchor="mm")


def draw_slots_normal(image):
    draw = ImageDraw.Draw(image)
    draw.text((80, 70), "底部技能栏", font=FONTB[30], fill=(236, 244, 246))
    for i, key in enumerate("QWER"):
        ui_slot(draw, 80 + i * 168, 180, key, (214, 224, 222), (70, 86, 94), "可放")
    draw.text((80, 380), "快捷键在槽内上方，状态文字在下方。\n这和当前 Game 场景底部的 Q W E R 是同一排位置。", font=FONT[22], fill=(214, 224, 222))


def draw_slots_hover(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 80, 120, "Q", (72, 220, 242), (72, 220, 242), "悬停")
    for i, key in enumerate("WER"):
        ui_slot(draw, 80 + (i + 1) * 168, 120, key, (214, 224, 222), (70, 86, 94), "")
    draw.ellipse([620, 430, 900, 710], outline=(72, 220, 242), width=6)
    draw.ellipse([730, 520, 790, 580], fill=(236, 246, 248))
    draw.text((760, 760), "悬停 Q 时的地面", font=FONT[20], fill=(214, 224, 222), anchor="mm")


def draw_slots_cooldown(image):
    draw = ImageDraw.Draw(image)
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    ui_slot(draw, 250, 220, "Q", (214, 224, 222), (70, 86, 94), "")
    od.pieslice([258, 228, 374, 344], 270, 270 + 220, fill=(8, 10, 12, 170))
    image.alpha_composite(overlay)
    draw.text((316, 286), "4.2", font=FONTB[30], fill=(236, 244, 246), anchor="mm")
    draw.text((80, 80), "剩余冷却从上方按顺时针减少", font=FONT[22], fill=(214, 224, 222))


def draw_slots_mana(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 250, 220, "W", (214, 70, 70), (90, 70, 70), "80", dim=True)
    draw.text((80, 80), "魔法消耗显示为红色，槽整体变暗", font=FONT[22], fill=(214, 224, 222))


def draw_slots_silence(image):
    draw = ImageDraw.Draw(image)
    for i, key in enumerate("QWER"):
        ui_slot(draw, 80 + i * 168, 220, key, (214, 70, 70), (160, 64, 60), "沉默", dim=True)


def draw_slots_ranks(image):
    draw = ImageDraw.Draw(image)
    draw.text((80, 60), "英雄联盟：普通技能 5 级，大招 3 级", font=FONT[22], fill=(214, 224, 222))
    draw_rank_row(draw, 150, 140, [5, 5, 5, 3], [3, 5, 1, 2])
    draw.text((80, 430), "DOTA2：多数技能 4 级，大招常为 3 级", font=FONT[22], fill=(214, 224, 222))
    draw_rank_row(draw, 150, 510, [4, 4, 4, 3], [4, 2, 0, 1])


def draw_rank_row(draw, x, y, totals, learned):
    for i, key in enumerate("QWER"):
        ui_slot(draw, x + i * 168, y, key, (214, 224, 222), (70, 86, 94), "")
        for n in range(totals[i]):
            px = x + i * 168 + 18 + n * 22
            color = (196, 168, 99) if n < learned[i] else (60, 72, 78)
            draw.polygon([(px, y + 150), (px + 8, y + 142), (px + 16, y + 150), (px + 8, y + 158)], fill=color)


def draw_slots_ult_lock(image):
    draw = ImageDraw.Draw(image)
    for i, key in enumerate("QWE"):
        ui_slot(draw, 80 + i * 168, 220, key, (214, 224, 222), (70, 86, 94), "可放")
    ui_slot(draw, 80 + 3 * 168, 220, "R", (150, 160, 166), (90, 90, 96), "未解锁", dim=True)


def draw_slots_charges(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 180, 220, "Q", (214, 224, 222), (72, 220, 242), "", badge="3")
    ui_slot(draw, 420, 220, "W", (214, 70, 70), (90, 70, 70), "", badge="0", dim=True)
    draw.text((80, 80), "有层数就能放。0 层才是不能放。", font=FONT[22], fill=(214, 224, 222))


def draw_slots_toggle(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 180, 220, "Q", (214, 224, 222), (70, 86, 94), "关")
    ui_slot(draw, 460, 220, "Q", (72, 220, 242), (72, 220, 242), "开")


def draw_slots_autocast(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 300, 200, "W", (214, 224, 222), (242, 198, 84), "自动")
    draw.ellipse([292, 192, 440, 340], outline=(242, 198, 84), width=6)


def draw_castbar(image):
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([120, 300, 860, 360], radius=12, fill=(20, 28, 32))
    draw.rounded_rectangle([126, 306, 620, 354], radius=10, fill=(72, 220, 242))
    draw.text((490, 250), "引导中  1.4 / 2.0 秒", font=FONT[22], fill=(236, 244, 246), anchor="mm")
    draw.text((490, 420), "被打断则条消失，技能不生效", font=FONT[20], fill=(214, 224, 222), anchor="mm")


def draw_chargebar(image):
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([120, 300, 860, 360], radius=12, fill=(20, 28, 32))
    draw.rounded_rectangle([126, 306, 760, 354], radius=10, fill=(242, 198, 84))
    draw.text((490, 250), "蓄力中，松手施放", font=FONT[22], fill=(236, 244, 246), anchor="mm")


def draw_minimap_circle(image):
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([160, 120, 760, 720], radius=24, fill=(24, 48, 42))
    draw.ellipse([300, 250, 620, 570], outline=(236, 84, 78), width=8)
    draw.ellipse([430, 400, 470, 440], fill=(236, 246, 248))
    draw.text((460, 760), "你在小地图中的位置，红圈是危险范围", font=FONT[20], fill=(214, 224, 222), anchor="mm")


def draw_minimap_line(image):
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([160, 120, 760, 720], radius=24, fill=(24, 48, 42))
    draw.line([(220, 180), (470, 430)], fill=(236, 84, 78), width=10)
    draw.ellipse([448, 408, 492, 452], fill=(236, 246, 248))
    draw.text((460, 760), "红线从地图边缘指向你", font=FONT[20], fill=(214, 224, 222), anchor="mm")


def draw_slots_recast(image):
    draw = ImageDraw.Draw(image)
    ui_slot(draw, 120, 220, "Q", (120, 130, 136), (70, 80, 86), "已用", dim=True)
    ui_slot(draw, 360, 220, "E", (72, 220, 242), (72, 220, 242), "再按")


def draw_enemy_warning(image):
    draw = ImageDraw.Draw(image)
    draw.polygon([(460, 180), (560, 360), (360, 360)], fill=(236, 84, 78))
    draw.text((460, 300), "!", font=FONTB[34], fill=(255, 255, 255), anchor="mm")
    draw.text((460, 430), "敌方正在对你施放", font=FONT[22], fill=(236, 244, 246), anchor="mm")


def build_masks():
    def circle(st):
        st.disk(0, 0, MASK_METERS)

    def ring_thin(st):
        st.ring(0, 0, MASK_METERS, width_m=0.12)

    def ring_dashed(st):
        st.ring(0, 0, MASK_METERS, dashed=True)

    def ring_fill(st):
        st.ring(0, 0, MASK_METERS, fill=True)

    def donut(st):
        st.disk(0, 0, MASK_METERS, hole=MASK_METERS * 0.62)

    def double_circle(st):
        st.ring(0, 0, MASK_METERS, fill=True)
        st.disk(0, 0, MASK_METERS * 0.48)

    def cross(st):
        st.beam(-MASK_METERS, 0, MASK_METERS, 0, 1.6)
        st.beam(0, -MASK_METERS, 0, MASK_METERS, 1.6)

    def x_shape(st):
        d = MASK_METERS * 0.72
        st.beam(-d, -d, d, d, 1.4)
        st.beam(-d, d, d, -d, 1.4)

    def line(width):
        return lambda st: st.beam(0, 0, 0, MASK_METERS, width)

    def line_end(st):
        st.beam(0, 0, 0, MASK_METERS * 0.78, 1.0)
        st.disk(0, MASK_METERS * 0.78, 2.1)

    def line_gap(st):
        st.beam(0, MASK_METERS * 0.22, 0, MASK_METERS, 1.2)

    def line_sweet(st):
        st.beam(0, MASK_METERS * 0.34, 0, MASK_METERS * 0.72, 1.3)

    def line_taper(st):
        st.polygon([(-0.3, 0), (0.3, 0), (1.7, MASK_METERS), (-1.7, MASK_METERS)])

    def cone_of(fov, inner=0, heading=0):
        return lambda st: st.cone(heading, MASK_METERS, fov, inner=inner)

    def boomerang(st):
        st.beam(-0.45, 0, -0.45, MASK_METERS, 0.4)
        st.beam(0.45, MASK_METERS, 0.45, 0, 0.4)

    def curve(st):
        st.curve((0, 0), (3.2, MASK_METERS * 0.45), (0.4, MASK_METERS), 0.8)

    def parallel(st):
        for x in (-1.5, 0, 1.5):
            st.beam(x, 0, x, MASK_METERS, 0.7)

    def spread(st):
        for ang in (-28, -14, 0, 14, 28):
            st.beam(0, 0, math.sin(math.radians(ang)) * MASK_METERS, math.cos(math.radians(ang)) * MASK_METERS, 0.4)

    def chain(st):
        pts = [(0, 0), (-2.2, 3), (2, 5.4), (-0.4, MASK_METERS)]
        st.polyline(pts, 0.35)

    def hook(st):
        st.beam(0, 0, 0, MASK_METERS, 0.4)
        st.disk(0, MASK_METERS, 0.7)

    def dash(st):
        st.beam(0, 0, 0, MASK_METERS * 0.82, 1.0)
        st.disk(0, MASK_METERS * 0.82, 1.3)

    def three_part(st):
        st.disk(0, 1.2, 1.3)
        st.beam(0, 1.2, 0, MASK_METERS * 0.75, 0.6)
        st.disk(0, MASK_METERS * 0.75, 1.8)

    def wall(st):
        st.beam(-MASK_METERS * 0.72, 0, MASK_METERS * 0.72, 0, 0.8)

    def rect_flat(st):
        st.polygon([(-4.6, -1.6), (4.6, -1.6), (4.6, 1.6), (-4.6, 1.6)])

    def rect_forward(st):
        st.polygon([(-1.6, 0), (1.6, 0), (1.6, MASK_METERS), (-1.6, MASK_METERS)])

    def reticle(st):
        st.ring(0, 0, 1.5)
        st.beam(-0.7, 0, 0.7, 0, 0.12)
        st.beam(0, -0.7, 0, 0.7, 0.12)

    def pillars(st):
        st.ring(0, 0, MASK_METERS * 0.82, dashed=True)
        for i in range(8):
            ang = math.radians(i * 45)
            st.disk(math.sin(ang) * MASK_METERS * 0.82, math.cos(ang) * MASK_METERS * 0.82, 0.55)

    def scatter(st):
        for x, y in ((-2, 1), (2.4, 2), (-1, 3.4), (1.2, -2), (3, -1), (-3, -1.4), (0, 0)):
            st.disk(x, y, 0.7)

    def meteor(st):
        for ix in range(-2, 3):
            for iy in range(-2, 3):
                st.disk(ix * 2.6, iy * 2.6, 0.75)

    def hatch(st):
        st.disk(0, 0, MASK_METERS, theme="white")

    def pull(st):
        st.ring(0, 0, MASK_METERS * 0.8)
        st.arrow(-4, 2, -1.4, 0.7)
        st.arrow(4, 2, 1.4, 0.7)
        st.arrow(0, 5, 0, 1.6)

    def push(st):
        st.ring(0, 0, MASK_METERS * 0.8)
        st.arrow(-1.4, 0.8, -4.2, 2.4)
        st.arrow(1.4, 0.8, 4.2, 2.4)
        st.arrow(0, 1.4, 0, 5)

    def shockwave(st):
        st.ring(0, 0, MASK_METERS * 0.72, width_m=0.28)

    def spiral(st):
        pts = []
        for i in range(1, 36):
            t = i / 5.2
            pts.append((math.cos(t) * t * 0.55, math.sin(t) * t * 0.55))
        st.polyline(pts, 0.35)

    def orbit(st):
        st.ring(0, 0, MASK_METERS * 0.7, dashed=True)
        st.disk(MASK_METERS * 0.7, 0, 0.7)

    def cleave(st):
        st.cone(0, MASK_METERS * 0.7, 110)

    specs = [
        ("circle_r", circle, "填满的圆。半径 9.6 米对应边长的 44%。", True),
        ("ring_thin", ring_thin, "细边圆，用来表示最大距离或信息范围。", True),
        ("ring_dashed", ring_dashed, "虚线圆，用来表示视野、预览或还没确认。", True),
        ("ring_fill", ring_fill, "淡填充加细边，用来表示光环或悬停距离。", True),
        ("donut", donut, "外半径 9.6 米，内半径约 6.0 米。空心才是判定。", True),
        ("double_circle", double_circle, "外圆 9.6 米，内圆约 4.6 米。两档效果。", True),
        ("cross", cross, "横竖交叉，每条都穿过中心。", True),
        ("x_shape", x_shape, "斜向交叉。", False),
        ("line_thin", line(0.35), "细直线，从中心指向正上方，长 9.6 米。", True),
        ("line_mid", line(1.15), "中等宽度直线，长 9.6 米。", True),
        ("line_wide", line(2.5), "宽走廊，长 9.6 米。等比缩放会同时改变宽度。", True),
        ("line_end", line_end, "直线加末端圆。末端半径是写死的比例，半径不同时请拆开用。", True),
        ("line_gap", line_gap, "从 2 米之后才开始的直线。", False),
        ("line_sweet", line_sweet, "只保留中间一段，适合甜区。", False),
        ("line_taper", line_taper, "近窄远宽的光束。", False),
        ("boomerang", boomerang, "去程和回程两条线。", False),
        ("curve", curve, "弯曲路径。", False),
        ("parallel", parallel, "三道平行直线。", False),
        ("spread", spread, "五道散开的直线。", False),
        ("chain", chain, "折线，表示跳跃或连接。", False),
        ("hook", hook, "细线加末端钩爪点。", True),
        ("dash", dash, "冲刺路径加落点。", True),
        ("three_part", three_part, "起点圆、路径、终点圆的固定比例。", True),
        ("wall", wall, "水平墙，中心在图片中心，长约 13.8 米。", True),
        ("rect_flat", rect_flat, "平放的矩形，中心在图片中心。", True),
        ("rect_forward", rect_forward, "从中心向正上方延伸的矩形。", False),
        ("reticle", reticle, "没有爆炸半径的准星。", True),
        ("pillars", pillars, "圆周上的八个点。", False),
        ("scatter", scatter, "分散的小圆。", False),
        ("meteor", meteor, "整齐排列的小圆。", False),
        ("hatch", hatch, "填满的圆，禁区可在材质里再叠加斜线。", False),
        ("pull", pull, "向中心的箭头。", False),
        ("push", push, "向外的箭头。", False),
        ("shockwave", shockwave, "较粗的一圈环。", False),
        ("spiral", spiral, "螺旋线。", False),
        ("orbit", orbit, "虚线轨道加一个当前位置。", False),
        ("cleave", cleave, "较贴身的 110 度扇形。", False),
        ("triangle", lambda st: st.regular(0, 0, MASK_METERS * 0.8, 3), "顶点朝上的三角形。", False),
        ("diamond", lambda st: st.regular(0, 0, MASK_METERS * 0.8, 4), "顶点朝上的菱形。", False),
        ("hexagon", lambda st: st.regular(0, 0, MASK_METERS * 0.84, 6), "六边形。", False),
    ]
    for fov in (15, 30, 45, 60, 90, 120, 180, 270):
        specs.append((f"cone_{fov}", cone_of(fov), f"{fov} 度扇形，长 9.6 米，朝正上方。", fov in (45, 60, 90, 120)))
    specs.append(("cone_dead", cone_of(70, inner=3.2), "70 度扇形，靠近中心约 3.2 米没有填充。", False))
    specs.append(("sector_ring", cone_of(80, inner=6.0), "80 度扇环。", False))
    specs.append(("cone_back", cone_of(70, heading=180), "朝图片下方的扇形，表示打背后。", False))
    for name, draw, note, colored in specs:
        mask(name, draw, note, colored)


def render_mask(draw, theme, size=1024):
    scale = 2
    image = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    ppm = (size * MASK_SPAN) / MASK_METERS
    stage = Stage(image, size / 2, size / 2, ppm, scale, theme)
    draw(stage)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def save_masks(root):
    white_dir = os.path.join(root, "11_可染色底图")
    os.makedirs(white_dir, exist_ok=True)
    color_root = os.path.join(root, "12_状态色")
    notes = []
    for spec in MASKS:
        render_mask(spec["draw"], "white").save(os.path.join(white_dir, spec["name"] + ".png"))
        notes.append((spec["name"], spec["note"], spec["colored"]))
        if spec["colored"]:
            for theme in ("self", "ally", "enemy", "invalid", "sweet", "block"):
                folder = os.path.join(color_root, theme)
                os.makedirs(folder, exist_ok=True)
                render_mask(spec["draw"], theme, 512).save(os.path.join(folder, spec["name"] + ".png"))
    frames = os.path.join(white_dir, "帧")
    os.makedirs(frames, exist_ok=True)
    for i, amount in enumerate((0, 0.25, 0.5, 0.75, 1)):
        render_mask(lambda st, amount=amount: st.pie(amount, MASK_METERS), "white").save(os.path.join(frames, f"引导_{int(amount * 100):03d}.png"))
    for i, radius in enumerate((9.6, 7.2, 4.8, 2.4)):
        render_mask(lambda st, radius=radius: st.ring(0, 0, radius), "white").save(os.path.join(frames, f"收缩_{i:02d}.png"))
        render_mask(lambda st, radius=radius: st.ring(0, 0, radius), "white").save(os.path.join(frames, f"扩张_{3 - i:02d}.png"))
    for step in range(0, 11):
        amount = step / 10

        def draw_cd(st, amount=amount):
            if amount <= 0:
                return
            edge, fill = st.colors()
            cx, cy = st.pt(0, 0)
            radius_px = st.rp(8.8)
            ImageDraw.Draw(st.image).pieslice([cx - radius_px, cy - radius_px, cx + radius_px, cy + radius_px], 270, 270 + 360 * amount, fill=(255, 255, 255, 255))

        render_mask(draw_cd, "white", 256).save(os.path.join(frames, f"冷却遮罩_{int(amount * 100):03d}.png"))
    return notes


def write_index(root, scene_rows, mask_notes):
    lines = [
        "# 技能指示器图库",
        "",
        "这是一套原创示意图和白色底图，用来覆盖英雄联盟和 DOTA2 里常见的技能指示，以及以后自定义技能会用到的形状。",
        "图里出现的英雄和技能名只用来说明「这是哪一种指示」，不是官方界面，也不能当作那些游戏的素材。",
        "",
        "圆形 Demo 已确认：默认把落点限制在最大距离上再施放；关掉开关才变红并禁止。两种图都在 `02_点选落点`，分别对应这两种表现。",
        "",
        "## 目录",
        "",
        "- `00_怎么看` 到 `10_技能栏与小地图`：带说明的对照图。",
        "- `11_可染色底图`：透明背景的白色形状，给以后的地面指示用。",
        "- `12_状态色`：常用形状已经染好的青、绿、红、不合法、甜区、被挡住。",
        "- `00_总览`：每一类的缩略图。",
        "",
        "## 底图怎么摆",
        "",
        "底图轴心在正中心，方向类图案指向图片正上方。",
        "从中心走到边长 44% 的位置，等于 9.6 米。想让半径或长度变成 R 米，平面边长设为 `R / 0.44` 米。",
        "例如半径 5 米的圆，平面边长约 11.36 米。",
        "直线、扇形也按这个比例：图案尽头就是 9.6 米。墙是横着穿过中心的，轴心在墙的中点。",
        "带固定比例的合成图，例如直线加末端爆炸，只在技能比例和这张图一致时直接用。比例不同就把圆和线分开缩放。",
        "冷却遮罩从正上方按顺时针增加，对应 Unity 里 Image 的径向 360 度、起点在上方、顺时针。白色区域是剩余冷却。",
        "",
        "## 颜色",
        "",
        "| 颜色 | 含义 |",
        "| --- | --- |",
        "| 青 | 自己的技能，现在可以放 |",
        "| 绿 | 友方、治疗、眼、光环 |",
        "| 红 | 敌方预警，或者自己这一下不合法 |",
        "| 金 | 甜区或更高一档的效果 |",
        "| 橙 | 墙、地形、被挡住 |",
        "| 灰 | 信息、无效段、还没确认、不能施放 |",
        "",
        "外发光不算进范围。判定看实线。",
        "",
        "## 对照图",
        "",
        "| 编号 | 图 | 底图 |",
        "| --- | --- | --- |",
    ]
    for code, folder, title, masks in scene_rows:
        lines.append(f"| {folder} {code} | `{folder}/{code}_{title}.png` | {masks or '示意'} |")
    lines += ["", "## 可染色底图", "", "| 文件 | 内容 | 有状态色 |", "| --- | --- | --- |"]
    for name, note, colored in mask_notes:
        lines.append(f"| `11_可染色底图/{name}.png` | {note} | {'有' if colored else '无'} |")
    lines += [
        "",
        "`11_可染色底图/帧` 里还有引导填充、收缩、扩张和冷却遮罩。",
        "",
        "## 已经在项目里的相关素材",
        "",
        "Kenney Particle Pack 的圆环和星光在 `Assets/Art/effect/Kenney粒子`，许可是 CC0，只适合做发光和确认反馈，不能拿发光边缘当技能判定。",
        "",
        "## 没有收进来的东西",
        "",
        "没有下载英雄联盟或 DOTA2 的官方图标、截图和界面。付费资源包也没有购买。若以后要更华丽的贴地效果，再单独评估 URP Decal，当前这套图不要求改渲染管线。",
        "",
    ]
    with open(os.path.join(root, "索引.md"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines))


def write_contacts(root, paths_by_folder):
    overview = os.path.join(root, "00_总览")
    os.makedirs(overview, exist_ok=True)
    thumb_w, thumb_h = 320, 188
    for folder, paths in paths_by_folder.items():
        cols = 3
        rows = math.ceil(len(paths) / cols)
        sheet = Image.new("RGB", (cols * (thumb_w + 24) + 24, rows * (thumb_h + 58) + 70), (12, 18, 22))
        draw = ImageDraw.Draw(sheet)
        draw.text((24, 16), folder, font=FONTB[30], fill=(236, 244, 246))
        for index, (title, path) in enumerate(paths):
            image = Image.open(path).convert("RGB").resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            col, row = index % cols, index // cols
            x, y = 24 + col * (thumb_w + 24), 64 + row * (thumb_h + 58)
            sheet.paste(image, (x, y))
            label = title if len(title) <= 18 else title[:17] + "…"
            draw.text((x, y + thumb_h + 6), label, font=FONT[16], fill=(214, 224, 222))
        sheet.save(os.path.join(overview, folder + ".jpg"), quality=86)


def main():
    build_scenes()
    build_masks()
    os.makedirs(OUT, exist_ok=True)
    rows = []
    by_folder = {}
    counts = {}
    for scene in SCENES:
        counts[scene["folder"]] = counts.get(scene["folder"], 0) + 1
        code = f"{counts[scene['folder']]:02d}"
        folder = os.path.join(OUT, scene["folder"])
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, f"{code}_{scene['title']}.png")
        render_scene(scene, f"{scene['folder']}  {code}").save(path, optimize=True)
        rows.append((code, scene["folder"], scene["title"], scene["masks"]))
        by_folder.setdefault(scene["folder"], []).append((scene["title"], path))
        print("scene", scene["folder"], code, flush=True)
    notes = save_masks(OUT)
    write_contacts(OUT, by_folder)
    write_index(OUT, rows, notes)
    with open(os.path.join(OUT, "许可.txt"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("对照图、可染色底图和状态色由本项目生成，可在本项目中使用。\n它们参照了英雄联盟和 DOTA2 的技能指示类型，但不是这两个游戏的官方素材。\nKenney 粒子图不在这个目录里，许可见 Assets/Art/effect/Kenney粒子/License.txt。\n")
    print("DONE", len(SCENES), "scenes", len(MASKS), "masks")


if __name__ == "__main__":
    main()
