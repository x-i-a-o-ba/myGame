# 敌人种类与 Dota 式操作、HUD 合并验证

日期：2026-10-21。对象：[PR #2](https://github.com/x-i-a-o-ba/myGame/pull/2)，原始提交 `ad8237d`。用户要求合并新的提交。

## 环境与修复

使用隔离工作区 `D:/UnityProject/myGame_merge_20261021`，Unity `6000.3.18f1`、URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`。主项目原有文件与本地导入日志保留。

首次 Unity 编译发现 `CombatWaveDirector` 的兼容刷怪入口，以及 `CombatValidation.Enemies` 的种类刷怪用例，直接修改了只读 `using` 变量 `NativeArray` 的元素，触发 `CS1654`。两处改为可写局部变量，在 `finally` 中确定释放。未改变刷怪、敌人属性或输入规则。

## 结果

| 检查 | 实际结果 |
| --- | --- |
| Unity 资源导入、脚本编译 | 修复后通过 |
| `Roguelike.Editor.CombatValidation.Validate` | 121 项通过，含成长回归、自动攻击节奏、转身、敌人配置和名册混刷 |
| `Roguelike.Editor.GameTools.Build` | Windows x64 构建成功 |
| `Game.exe --game-smoke` | 42 项通过，结果 `passed=true` |
| 实际画面 | 已查看 Player 离屏截图，新 HUD、英雄属性、技能栏、学习按钮、指令按钮及首波敌人可见 |

Player 检查覆盖攻击移动、鼠标射线选敌、指定目标攻击及伤害、待命不移动、HUD 停止与攻击瞄准按钮、多种敌人混刷，以及等级、技能学习、天赋、换英雄、重开和死亡结算回归。

证据：[精简检查输出](../Logs/EnemiesDotaHud/validation-checks.txt)、[Player 结果](../Logs/EnemiesDotaHud/Player/result.json)、[HUD 截图](../Logs/EnemiesDotaHud/Player/foundation-game.png)、[技能指示器截图](../Logs/EnemiesDotaHud/Player/point-indicator.png)。原始日志留在隔离工作区；Unity 构建产生的无关渲染配置序列化差异未纳入合并。

## 验证边界

命令检查调用实际游戏输入边界与 HUD 按钮，未进行原始键鼠事件注入或人工手感验收；未检查全部中文 Inspector 外观、天赋/首领面板所有状态、全部特殊敌人的完整实战、多分辨率布局、完整 30 波或万敌完整战斗性能。截图 FPS 属于低负载冒烟，不能代表正式性能结果。
