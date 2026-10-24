> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 工程清理增量包合并验证

日期：2026-10-24。环境：Windows、Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8。

用户要求将 `myGame-redesign-dd9ce6b.bundle` 提交合并。包的前置提交为 `9bd09b3`，目标为 `dd9ce6b`；在主工作目录 `D:/UnityProject/myGame` 的 `main` 快进合并，没有创建分支或 worktree。本地日志与场景恢复备份保留。

## 已完成

| 检查 | 实际结果 |
| --- | --- |
| Unity 导入与编译 | 通过 |
| 战斗 / 成长 Editor 检查 | 360 项通过 |
| 开局流程 Editor 检查 | 345 项通过 |
| 数字编号只读校验 | 测试前、临时资源清理后均通过 |
| 技能工作台 | 110 项通过，覆盖当前配方编辑、保存、重新导入与预览，以及工作环境保存重开、旧阶段兼容和范围几何 |
| 技能编辑工作流 | 44 项通过，六类配方模板复用目录效果，旧阶段复制 / Undo / 共享效果独立化分别验证 |
| 配方编辑事务 | 20 项通过，包含导入、保存修订和失败回滚 |
| 场景结构 | Main、SelectScene、Game、EmberRift、Benchmark10k 五场景通过，无 Missing Script |
| Windows 构建 | 游戏构建与包含五场景的场景框架验证构建通过 |
| 开局 Player | 68 项通过；英雄与战场切换、返回与超时确认、两战场范围预览 |
| 战斗 Player | 149 项通过，无失败或跳过 |
| 跨场景 UI Player | 37 项通过，五个场景共六次访问（最后返回 Main），结果 `passed=true` |

已检查 Game 的技能目标圈与 EmberRift 的范围圈截图，迁移后的圆形视图和网格能实际渲染。新增的跨战场预览检查只将编译快照交给表现层，不施法、不修改学习或战斗资源状态，不能作为未学习技能可实际释放的证据。

## 校验中修正的问题

1. 旧工作台检查直接编辑 `stages`，但正式示例已经启用数字配方。补充当前配方的保存、重新导入、修订和预览检查，再将临时副本明确切到旧阶段模式验证兼容；正式配置未改动。
2. 六类模板已改为引用共享目录效果，旧检查仍期望每份模板新建一份效果。现在核对解析后的当前配方和没有孤立效果资源，再单独覆盖旧编辑命令。
3. UI 检查以总面板数 `before + 1` 判断帮助面板请求合并。实测 Game / EmberRift 同时加载战斗 HUD，面板总数从 4 到 6，但帮助面板始终只有 1 个。现在直接核对帮助面板唯一且保持隐藏，仍检查重开、按钮关闭、状态回放与场景间销毁。

以上修改仅涉及显式验证入口，没有修改正常游戏规则。临时验证资源与驱动已清理，数字编号账本恢复为测试前内容；Unity 自动改写的无关材质和渲染配置未纳入提交。

## 证据与运行入口

- [检查标记与计数](../../Logs/BundleMerge20261024/checks.txt)
- [开局结果](../../Logs/BundleMerge20261024/select-verified/result.json)、[战斗结果](../../Logs/BundleMerge20261024/game/result.json)、[场景 UI 结果](../../Logs/BundleMerge20261024/scene-ui-result.json)
- [Game 目标圈](../../Logs/BundleMerge20261024/game/point-indicator.png)、[EmberRift 范围圈](../../Logs/BundleMerge20261024/select-verified/map-ember_rift-indicator.png)

编译/构建入口：`Roguelike.Editor.GameTools.Build`、`Roguelike.Foundation.Editor.SceneFrameworkMigration.BuildVerificationPlayer`。工作台入口：`SkillAuthoringValidation.Run`、`SkillAuthoringWorkflowValidation.Run`、`RecipeAuthoringValidation.Validate`、`NumericConfigIds.ValidateAll`（命名空间为 `Roguelike.SkillAuthoring.Editor`）。Player 参数分别为 `--select-smoke`、`--game-smoke`、`--scene-ui-smoke`，跨场景检查使用五场景验证构建。完整原始日志和临时驱动副本仅在本机 `Logs/BundleMerge20261024` 保留。

## 边界

本轮只完成重设计第 1 步清理的合并与回归，第 2–7 步仍未实现；当前仍为 30 波、右键操作版本。未做刷怪导演、怪物分离、4,000 同屏完整战斗压测或新的整局平衡。Benchmark10k 在 UI 检查中仅短暂访问，不能视为性能验收。Player 退出仍有既有 ComputeBuffer 释放警告，没有宣称已修复。
