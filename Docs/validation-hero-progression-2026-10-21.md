# 英雄成长分支合并验证

日期：2026-10-21。对象：[PR #1](https://github.com/x-i-a-o-ba/myGame/pull/1)，原始提交 `31cc5d9`。用户要求拉取最新改动并合并到 `main`。

## 环境与范围

在独立 Git 工作区 `D:/UnityProject/myGame_merge_20261021` 使用 Unity `6000.3.18f1`，URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`。主项目原有 Unity 窗口保持打开；验证工作区复用旧验证目录的缓存副本，不共享正在运行的 Library。

## 修复

- 新增验证脚本对 `GetBuffer` 临时返回值写入导致 `CS1612`，改为先取得局部缓冲变量。
- 技能增强用例连续施法时只清冷却、未恢复充能，导致施法被正确拒绝；补齐该用例的施法准备状态。
- 旧直线弹体用例在提交施法的同一时刻就要求命中，与现有“只推进释放后时间”的执行规则不符；显式分开发射与飞行，并检查释放时不会提前伤害。

## 执行结果

| 检查 | 实际结果 |
| --- | --- |
| 资源导入与脚本编译 | 通过；最终运行无编译错误 |
| `Roguelike.Editor.CombatValidation.Validate` | 89 项通过，含原战斗回归与新增成长、技能等级、天赋、刷波检查 |
| `Roguelike.Editor.GameTools.Build` | Windows x64 构建成功，含场景装配验证 |
| `Game.exe --game-smoke --game-output ...` | 31 项通过，结果 `passed=true` |
| Player 实际渲染 | 已检查成长面板、学习按钮、未学习状态、技能指示器与重开画面截图 |

Player 检查覆盖开局首波与存活上限、1 级/1 技能点、未学习拒绝施法、经验升至 7 级、属性成长、HUD 学习四技能、大招后续等级门槛、普攻、技能提交、10 级天赋选择、第二波刷怪、换英雄装配、重开清理、死亡与失败状态。

精简检查输出保存在 [validation-checks.txt](../Logs/HeroProgression/validation-checks.txt)，Player 结果和截图在 [Player](../Logs/HeroProgression/Player/result.json)。原始 Unity 日志仅保留在验证工作区；导入/构建自动产生的无关渲染配置序列化差异未纳入合并。

## 剩余范围

未进行完整 30 波人工实玩、最终首领胜利流程的 Player 专项验证、中文 Inspector 实际外观检查、天赋面板视觉检查或数值平衡调整。离屏冒烟只有低负载首波，不代表万敌完整战斗性能达标。已有 obsolete API 与 Blob 构造分析警告仍需按后续任务处理。
