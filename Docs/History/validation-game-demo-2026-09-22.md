> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# Game Demo 验证记录 · 2026-09-22

## 结果

Game 场景战斗 Demo 已完成编译、Windows x64 Release 构建、隔离 World 逻辑检查、Player 交互集成检查和离屏截图检查。

构建：`Builds/GameDemo/GameDemo.exe`。场景：`Assets/Scenes/Game.unity`。原 Main / SelectScene 顺序保留；独立 Demo 构建直接进入 Game。

## 验证环境与证据

- Unity 6000.3.18f1，Entities 1.4.8，Entities Graphics 1.4.21，URP 17.3.0。
- Windows Player，Direct3D11，Ryzen 7 7745HX、RTX 4060 Laptop GPU，1920×1080。
- 构建日志：`Logs/GameDemoBuild.log`，包含 `GAME_COMBAT_LOGIC_VALIDATED`、`GAME_DEMO_BUILD_SUCCEEDED`，Unity 最终退出码 0。
- Player 日志：`Logs/GameDemoOffscreen.log`，包含所有 `DEMO_PASS` 与 `GAME_DEMO_SMOKE_PASSED`。
- 机器可读结果：`DemoReports/Offscreen/result.json`。
- 画面：`DemoReports/Offscreen/combat.png`、`overview.png`、`defeat.png`，已实际查看。

## 逻辑检查

1. 射程内选择最近敌人，生成一枚指定目标的追踪弹。
2. 弹体命中只对其目标扣血，命中、击杀和剩余敌人数正确。
3. 射程外不发射，短距离移动不越过目的地。
4. 目标被销毁后回收弹体；多个弹体同帧命中不会重复计杀。
5. 近身攻击按间隔扣血；玩家生命归零后停止移动和射击。
6. 10,000 个战斗敌人更新后数量正确、位置为有限数值。

## Player 集成检查

使用与实际鼠标输入相同的屏幕坐标 → 地面投影 → 目的地设置路径，验证玩家移动与 UI 拦截。检查并非通过物理鼠标操作；键盘/鼠标手感仍以实际游玩为准。

- 开始生成 10,000 个敌人。
- 输入移动命令后玩家移动超过 5 米，同时正常射击、命中和击杀。
- 短时场景中产生 26 次开火、25 次命中、25 次击杀；存活数 + 击杀数保持 10,000。
- 底部 HUD 拦截地面输入，不改变已有目的地。
- 测试探针临时将玩家生命改为 1、敌人近身范围改为 300，以快速触发死亡；验证死亡后停止射击、拒绝移动。此配置只在 `--demo-smoke` 测试中使用，常规 Demo 仍为生命 300、近身范围 1.25。
- 恢复正常参数并重开，玩家生命与击杀恢复初始状态，实际查询恰好 10,000 个敌人。
- 销毁控制器后，CombatOwned 查询（含 Prefab）为空。

## 画面检查方式

首次隐藏窗口的 backbuffer 截图为全黑，不能用作画面验收。随后使用 URP StandardRequest 渲染到 RenderTexture，并在测试期间临时将同一 HUD 设为 ScreenSpaceCamera，读回三张实际画面；正常游戏 HUD 仍为 ScreenSpaceOverlay。

截图检查确认主角/敌人材质、底部中文状态栏、技能占位和失败按钮可见，无明显文本重叠。全景可以覆盖整个敌群；正常操作视角只覆盖局部，所有敌人仍参与模拟。

截图和隐藏运行中的 HUD FPS 会受窗口状态、离屏截图读回与编码影响，不能视为可见窗口性能。尚未完成前台持续采样、最低配置或 p95/p99 的正式验收。

## 已知限制

- 没有怪物间避让/物理阻挡、障碍寻路、经验升级、掉落物或主动技能。本轮技能为 Q/W/E/R 占位。
- 玩家输入采用现有 Input Manager；正式美术、动画和更复杂武器需要后续评估。
- Player 退出仍有上一轮出现的 `GarbageCollector disposing of ComputeBuffer` 警告。本轮实体、Prefab 和 NativeQueue 生命周期有检查，但该图形资源警告尚未定位，不能宣称整个渲染层已无资源问题。
- 原场景及构建配置备份在 `Backups/game-demo-20260922`；验证副本位于 `Temp/AgentValidation`。可执行程序和报告已复制到非 Temp 目录。
