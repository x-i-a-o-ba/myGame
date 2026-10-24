> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 装备丢弃、拾取与固定商店合并验证

日期：2026-10-22。对象：[PR #6](https://github.com/x-i-a-o-ba/myGame/pull/6)，原始提交 `b52d580ca53176e84870bfc800e1b2231947e98a`。用户要求合并，并统一使用一个 `main` 分支和主工作目录。

## 环境与实现

最后一次复用已有验证工作区 `D:/UnityProject/myGame_merge_20261021`，使用 Unity `6000.3.18f1`、URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`。本次没有创建分支或新工作区，主项目 Editor 会话保留。

装备换位、丢弃和拾取通过共享装备指令串行结算；地上物品数据归会话所有，显示对象与提示消费该数据。商店位置及半径由场景配置提供，实际购买与出售再次在结算层校验范围。丢弃与拾取改变装备栏后走已有统一属性重算。本次验证无需修正功能源码。

## 结果

| 检查 | 结果 |
| --- | --- |
| Unity 导入与脚本编译 | 通过 |
| Editor 全套规则 | 303 项通过 |
| Windows x64 构建 | `GAME_BUILD_SUCCEEDED` |
| Player 冒烟 | 105 项通过，`passed=true`，无失败、无跳过 |
| 实际截图 | 已查看 1920×1080 商店模型及范围圈、商店面板、3×2 装备栏和地上装备提示 |

Player 实际验证范围外拒绝开店及购买、进入范围买卖、拖动图标与目标高亮、装备换位、拖到商店出售、拖到地面后走到距离内放下、属性移除、离开范围自动关店、点选地上装备查看提示、走近拾取并恢复属性、重开清空地上装备。同时回归技能、成长、移动中攻击、目标面板和死亡重开。

证据：[检查输出](../../Logs/ItemDropShop/validation-checks.txt)、[Player 结果](../../Logs/ItemDropShop/Player/result.json)、[商店](../../Logs/ItemDropShop/Player/shop.png)、[地上装备](../../Logs/ItemDropShop/Player/ground-item.png)。构建自动产生的无关渲染配置差异已恢复。

## 工作区收敛与边界

按本次用户要求，`AGENTS.md` 已约定后续开发、验证和提交统一在 `D:/UnityProject/myGame` 的 `main` 完成，提交标题和说明使用中文。此次合并同步后，核对其他本地及远程分支均已包含在 `main`，再删除这些分支；原验证工作区的文件移至 `D:/UnityProjectArchive/myGame-validation-20261022` 归档，并撤销额外 worktree。已有提交历史保留。

Player 通过 UI 事件处理器和控制器执行集成检查，未进行原始键鼠事件注入、人工拖动手感、实际 Inspector 全字段视觉验收、全部分辨率与装备组合、完整 30 波和万敌完整战斗性能测量；低负载截图 FPS 不代表正式性能结果。
