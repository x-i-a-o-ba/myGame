# 冰箭散射与磐锋卫士验证

日期：2026-10-23。环境：Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8、Windows Player。

用户要求及 AI 选择的玩法、数值、模块边界见 [功能说明](volley-and-vanguard.md)。

## 实际完成

- Editor 编译及 360 项规则检查通过，Windows Player 构建成功。
- 独立 Player 集成检查 149 项通过，无失败、无跳过，结果 `passed=true`。复核了英雄选择按钮、HUD、头像和刃阵的实际渲染截图。
- 实际打开冰霜扇击资源的 Inspector，确认“每次发射数量（支）”与“完整散射张角（度）”为可编辑中文字段，默认 7 和 70，并包含重开生效说明。
- 英雄选择组件实际 Inspector 显示中文英雄列表、战斗控制器和重开说明；补充该编辑器后再次完成编译和 360 项 Editor 检查。

## 本次新增覆盖

1. 冰箭在释放帧生成 7 条独立飞行状态，首末方向覆盖 70°；不再进行扇形瞬时伤害。
2. 中央箭命中前排造成 60 点伤害并被移除，剩余 6 支继续飞行。后排遮挡目标、箭间空隙目标不受伤，射程结束清理剩余箭。
3. 箭数 0、33 被编译拒绝，1 支/0° 配置通过，保留单发兼容。
4. 战士拥有四技能、近战普攻与独立外形。未蓄满不能重劈且不扣蓝，三次普攻命中后可释放、伤害和减攻生效并消耗蓄势。
5. 守势的即时治疗、持续恢复和移动代价生效。固定刃阵在英雄离开后仍在原点结算六次伤害，只扣一次法力，结束不再伤害。
6. 落剑飞行途中不伤害，到达后结算范围伤害与实际蓝耗。
7. Player 调用场景中真实按钮事件切换英雄，实际近战命中蓄势并依次释放 Q/W/E/R，核对技能序号和飞行状态回收。切回寒冰后等级、装备、地面物品与 Buff 已重置。

集成检查使用显式 `--game-smoke` 模式，通过测试经验与高生命敌人覆盖技能，不作为正常通关或平衡证据。正常游戏不会执行这些测试夹具。

## 证据

- [规则/构建/Player 检查摘要](../Logs/Vanguard/validation-checks.txt)
- [Player 结果](../Logs/Vanguard/Verified/result.json)
- [战士 HUD 与固定刃阵截图](../Logs/Vanguard/Verified/vanguard-field.png)
- [冰箭配置实际 Inspector](../Logs/Vanguard/w-inspector.png)
- [英雄选择实际 Inspector](../Logs/Vanguard/roster-inspector.png)

本机原始日志保留于 `Logs/Vanguard/build-verified.log`、`player-verified.log`、`inspector.log`。仓库只收录检查标记与必要截图。

## 验证范围与剩余限制

- 未重新完成新版寒冰 W / 战士的 30 波正常速度通关、不同装备策略胜率和人工手感评估；既有旧版 30 波记录不能替代本次平衡验收。
- 未实测万级完整战斗性能；飞行箭数、Buff 和区域排期增加后的性能仍需按真实负载验证。
- 战士使用独立程序化甲胄/重剑原型与徽记，没有正式角色动画、语音或完整特效。
- Player 退出仍有已有的 `GarbageCollector disposing of ComputeBuffer` 警告，本次没有宣称修复；检查期间未记录未处理异常。

开发、验证、提交均在 `D:/UnityProject/myGame` 的 `main` 完成，没有创建功能分支或额外 worktree。
