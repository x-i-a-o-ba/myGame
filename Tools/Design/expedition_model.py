# -*- coding: utf-8 -*-
"""远征闯关（15 分钟、3 关）击杀 / 经验 / 金币粗估，用于定 Docs/redesign-expedition.md 的经验表和金币规则。

运行：python3 Tools/Design/expedition_model.py
假设（都是设计目标，不是实测）：
- 击杀速度按强度时间 t 插值（见 KILL_RATE），乘以每关开头的恢复系数（40% → 100%，60 秒）
  和首领阶段的补充系数（本关 3:30 起 50%）。
- 普通怪经验 / 金币按出场后的种类占比加权（TYPES）。
- 精英按各关节奏出现到首领出现为止；首领在本关 4:30 前被击杀。
"""

STAGE_MINUTES = 4.5
BOSS_AT = 3.5
KILL_RATE = [(0, 0.5), (1, 3), (3, 8), (5, 16), (6, 20), (10, 40), (13, 60), (13.5, 60)]  # (t 分钟, 只/秒)
# 名称、占比、出场 t、经验、金币
TYPES = [("近战", 0.55, 0, 1, 0.5), ("疾行", 0.15, 0.75, 1, 0.5), ("远程", 0.12, 1.5, 1.5, 1),
         ("自爆", 0.08, 2.5, 1, 0.5), ("冲锋", 0.06, 3.5, 2, 1), ("坦克", 0.04, 5, 4, 2)]
ELITE = {"first": [90, 30, 20], "period": [60, 30, 20], "xp": 30, "gold": 30}
BOSS = {"xp": [150, 300, 0], "gold": [500, 800, 0]}
GOLD_PER_SECOND = 2
START_GOLD = 300
LEVEL_XP = [15, 45, 80, 130, 190, 280, 480, 800, 1200, 1800, 2600, 3600, 4300, 4500]  # 升到 2..15 级


def interp(points, x):
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if x <= x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return points[-1][1]


def per_kill(t):
    active = [(share, xp, gold) for _, share, start, xp, gold in TYPES if t >= start]
    total = sum(share for share, _, _ in active)
    return sum(s * x for s, x, _ in active) / total, sum(s * g for s, _, g in active) / total


def simulate(step=1 / 240):
    cumulative = [sum(LEVEL_XP[:i + 1]) for i in range(len(LEVEL_XP))]
    xp, gold, kills, level, events, camps = 0.0, float(START_GOLD), 0.0, 1, [], []

    def level_up(stage, stage_minutes):
        nonlocal level
        while level < 15 and xp >= cumulative[level - 1]:
            level += 1
            events.append((level, stage + 1, stage_minutes, STAGE_MINUTES * stage + stage_minutes))

    for stage in range(3):
        seconds, next_elite = 0.0, ELITE["first"][stage]
        while seconds < STAGE_MINUTES * 60:
            t = STAGE_MINUTES * stage + seconds / 60
            ramp = min(1.0, 0.4 + 0.6 * seconds / 60)
            refill = 0.5 if seconds >= BOSS_AT * 60 else 1.0
            rate = interp(KILL_RATE, t) * ramp * refill
            avg_xp, avg_gold = per_kill(t)
            kills += rate * step
            xp += rate * step * avg_xp
            gold += rate * step * avg_gold + GOLD_PER_SECOND * step
            if next_elite <= seconds < BOSS_AT * 60:
                xp += ELITE["xp"]; gold += ELITE["gold"]; next_elite += ELITE["period"][stage]
            seconds += step
            level_up(stage, seconds / 60)
        xp += BOSS["xp"][stage]; gold += BOSS["gold"][stage]
        level_up(stage, STAGE_MINUTES)
        camps.append((stage + 1, round(kills), round(xp), round(gold)))
    return events, camps


if __name__ == "__main__":
    events, camps = simulate()
    for level, stage, stage_minutes, total in events:
        print(f"{level:>2} 级：第 {stage} 关 {int(stage_minutes)}:{round(stage_minutes % 1 * 60):02d}"
              f"（总战斗 {int(total)}:{round(total % 1 * 60):02d}）")
    previous = START_GOLD
    for stage, kills, xp, gold in camps:
        print(f"第 {stage} 关结束：累计击杀 {kills}、经验 {xp}、金币 {gold}（本关收入约 {gold - previous}）")
        previous = gold
