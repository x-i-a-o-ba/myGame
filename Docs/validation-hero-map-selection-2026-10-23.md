# 开局选择英雄与战场验证

## 2026-10-24 本机合并与 Unity 补验

在主工作目录 `D:/UnityProject/myGame` 的 `main` 快进同步 `609d43c`，保留本地未跟踪的日志与场景恢复备份，没有创建分支或 worktree。

- Unity 6000.3.18f1 实际导入、编译和 Windows 构建通过。
- 360 项战斗/成长 Editor 检查、345 项开局流程 Editor 检查通过。
- Windows Player 开局流程 60 项、战斗回归 149 项通过，没有失败或跳过。选择检查通过实际按钮事件完成英雄与战场选择、两个战场进入、名册归属、返回确认和超时取消、预选上局、回主菜单。
- 已查看 1920×1080 的英雄选择、战场选择和熔火裂谷运行截图。没有据此声称全部分辨率或人工键鼠操作验收。

首次真实构建发现 `GameFlowValidation` 的方法局部变量不能防止打开场景时卸载配置，导致 `GameMapDefinition` 的 `MissingReferenceException`。校验器现在用静态 `activeCatalog` 在跨场景检查期间持有目录及其依赖，并在 `finally` 释放引用。原配置、运行时选择与场景不修改；修复后从编译到构建、Player 流程重新验证。

证据：[检查标记](../Logs/Merge20261024/checks.txt)、[选择结果](../Logs/Merge20261024/select/result.json)、[战斗结果](../Logs/Merge20261024/game/result.json)、[英雄选择](../Logs/Merge20261024/select/select-hero.png)、[战场选择](../Logs/Merge20261024/select/select-map.png)、[熔火裂谷](../Logs/Merge20261024/select/map-ember_rift.png)。完整原始日志仅在本机保留。

本次未跑完整 30 波平衡、万敌性能或九场景 `--scene-ui-smoke`；未逐一人工查看新增 Inspector。Player 退出仍出现已有 ComputeBuffer 释放警告，没有将其算作已修复。下方为 10 月 23 日原始开发记录及当时的待验清单，以本节为最新验证状态。

## 2026-10-23 原始开发记录

日期：2026-10-23。环境：Linux 开发沙箱，**没有安装 Unity**。项目目标版本 Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8。

用户要求、AI 选择的流程和数值见 [功能说明](hero-and-map-selection.md)。

## 结论

本轮**没有任何 Unity 验证**：没有导入资源、编译、打开场景或 Inspector、运行 Editor 检查、构建或运行 Player。以下“已完成”的检查只能发现语法错误、项目自身类型或成员的引用错误，以及资源引用的明显断裂，不能代替 Unity 编译和实际运行。

## 已完成（不依赖 Unity）

1. 24 个新增或修改的 C# 文件通过 Roslyn 语法解析。
2. 将 `Assets/Game` 下全部脚本与自制的 Unity API 桩（只放在开发沙箱，不进仓库）一起做语义编译：改动文件中没有项目自身类型或成员的错误。剩余报错都来自桩没有覆盖的 Unity / Entities API（如 `Entity`、`math`、`Allocator`、部分 `Mathf` 方法、`Vector3` 到 `Vector2` 的隐式转换），不代表真实编译结果。
3. 资源静态核对：47 个新增或修改的场景、预制体、材质和配置资源引用的 GUID 都存在；`Assets` 下 67 个新增文件和 4 个新增文件夹都有 `.meta`（共 71 个），没有孤立的 `.meta` 或重复 GUID；4 个场景、3 个面板预制体和 16 个怪物预制体内部的 fileID 引用都能找到对应对象；删除的 `HeroRoster` 脚本及其 Inspector 的 GUID 不再被任何资源引用。
4. 资源清单、编号登记（76 条，ID 不重复，新增 3009–3016 与 7001、7002）和 HUD 生成脚本的 JSON 都能正常解析；Build Settings 和 `GameTools.BuildScenes` 都包含 Main、SelectScene、Game、EmberRift。

## 待在 Unity 中验证

按顺序：

1. 打开项目，确认资源导入和脚本编译没有错误（特别是新增的 `Roguelike.Flow` 代码、三份中文 Inspector 和 `Roguelike.Editor` 新增的 `Roguelike.Foundation.Editor` 引用）。
2. 运行 `Tools/Roguelike/维护/检查开局选择流程`，期望 `GAME_FLOW_VALIDATION_PASSED`；再运行 `Tools/Roguelike/验证/战斗与成长（不构建）` 和 `Tools/Roguelike/维护/检查场景`。
3. 打开以下 Inspector，确认中文字段、列表按钮和校验提示正常显示：开局选择目录、两个战场定义、选择场景的 `Select Flow` 与 `Hero Showcase`、各场景 Canvas 上的 `SceneNavigation`、Game / EmberRift 的 `GameCombatController`、Game 的 `GameCombatProbe`、英雄定义的“定位标签”“英雄简介”。
4. 打开 SelectScene 和 EmberRift，检查手写场景 YAML 是否有丢失引用或 Missing Script；检查熔火裂谷的地面、熔岩池、尖柱和灯光外观，以及选择面板在 1920×1080 和其他分辨率下的布局。
5. 从 Main 运行完整流程：选英雄（展示台模型、属性、技能文字）→ 选战场（怪物头像与标签）→ 进入两个战场各打几波，确认刷出的都是本战场的怪物 → 右上角两次点击“返回选择”、等待超时取消 → 结算面板的“返回选择” → Esc 回主菜单。
6. `Tools/Roguelike/构建/Windows 游戏` 构建后运行 Player：`--select-smoke`（期望 `SELECT_SMOKE_PASSED`，查看 `select-hero.png`、`select-map.png`、`map-shadow_wastes.png`、`map-ember_rift.png`），`--game-smoke`（验证 `GameStartupRouter` 从主菜单直接进入 Game，英雄切换检查改用开局选择目录），`--scene-ui-smoke`（现在 9 次场景访问，包含 EmberRift）。
7. 试玩熔火裂谷完整 30 波，调整 8 种新怪物的倍率；两个战场暂时共用一条难度曲线。

## 已知限制

- 新怪物、战场预览图、展示台和熔火裂谷环境都由程序生成或用内置几何体拼成，没有正式美术、动画或音效。
- 选择结果只保存在本次运行的内存中，不写存档；直接打开战场场景时使用场景上的配置。
- 界面最多显示 4 个英雄、3 个战场、每个战场 8 种怪物，超过时流程校验会报错或提示。
- 熔火裂谷的数值平衡没有试玩；万敌完整战斗性能仍未实测。
