# 技能悬停提示与移动中攻击合并验证

日期：2026-10-22。对象：[PR #4](https://github.com/x-i-a-o-ba/myGame/pull/4)，原始提交 `3bc7374`。用户要求合并。

## 环境与处理

在独立工作区 `D:/UnityProject/myGame_merge_20261021` 使用 Unity `6000.3.18f1`、URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`，主项目原有日志保留。

功能代码首次编译和 Editor 检查通过。首轮 Player 有 58 项通过，但移动中攻击用例因附近敌人被普攻击杀、等待期间没有满足条件的随机目标而跳过，不能据此声称该路径通过。

将显式冒烟模式的 `MoveAttackChecks` 改为复制一个现有敌人作为静止、耐久、无攻击力的临时目标；沿横向移动，验证到达终点前实际发射弹体，最后在 `finally` 中销毁目标。缺少英雄开关或模板改为检查失败，避免静默跳过。正常游戏配置与战斗规则不变。

## 最终结果

| 检查 | 结果 |
| --- | --- |
| Unity 导入、脚本编译 | 通过 |
| Editor 战斗与成长规则 | 171 项通过，含移动攻击步进、施法动作锁、提示内容与排版 |
| Windows x64 构建 | `GAME_BUILD_SUCCEEDED` |
| Player 冒烟 | 62 项通过，`passed=true`，无失败、无跳过项 |
| 实际提示框画面 | 已查看 W 技能提示截图，包含名称、逐级数值、冷却、升级门槛、状态，位于技能图标上方 |

Player 实际执行主动技能/被动悬停显示、内容、位置与移开隐藏检查，并验证移动途中发射普攻；同时运行既有目标面板、指令、成长、混刷及死亡重开回归。

证据：[检查输出](../Logs/AbilityTooltip/validation-checks.txt)、[Player 结果](../Logs/AbilityTooltip/PlayerFinal/result.json)、[提示框截图](../Logs/AbilityTooltip/PlayerFinal/ability-tooltip.png)。原始日志保留在验证工作区，构建自动产生的无关渲染配置序列化差异不纳入合并。

## 边界

未进行原始鼠标事件注入、人工操作手感、所有技能和等级在全部分辨率下的视觉验收、完整 30 波与万敌完整战斗性能测量；低负载截图 FPS 不代表正式性能结果。
