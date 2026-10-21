# 独立万实体场景使用说明

## 打开运行

等待 Unity Package Manager 完成解析并且 Console 编译错误清零。菜单 `Tools > Roguelike > Open 10k Benchmark` 打开 `Assets/Game/Scenes/Benchmark10k.unity`，进入 Play。

场景创建 10,000 个 ECS 单位，每个具有 EnemyTag、Health、MoveSpeed、LocalTransform 和压测专用数据。渲染使用 Entities Graphics、URP Unlit 和共享立方体网格。只有一个场景控制器和相机，不创建一万个 GameObject。

默认预热 10 秒、采样 60 秒，自动输出 CSV 原始帧耗时和 JSON 报告，Console 显示完整路径；默认目录是 `Application.persistentDataPath/Benchmarks`。报告生成后继续显示场景。退出 Play 或卸载场景清理本次实体并恢复 VSync、帧率上限和后台运行设置。

Inspector 可调整 `enemyCount`、`renderEntities`、`syntheticDamage`、预热与采样秒数。修改负载后重新运行，避免采样期间修改参数。单个 World 同时只允许一个压测场景；不要叠加在已有玩法场景中测量。

## 压测口径

- 默认：10k 立方体 + 并行运动，不含实际战斗。
- `renderEntities=false`：隔离纯模拟开销。
- `syntheticDamage=true`：额外加入每实体生命写入和数值重置；仅为合成数据负载，不是攻击、死亡、重生玩法。
- 相机自动覆盖整个阵列；报告说明这是几何覆盖，不声称已经测得 GPU 可见实体数。
- 最终游戏目标是一万只可战斗敌人，子弹与掉落物需要另计。本场景只建立测量基线，不能据此宣称完整战斗已达标。

## 构建与命令行

菜单 `Tools > Roguelike > Validate Foundation` 验证管线、场景引用与 10k 实体的运动边界/生命合成负载/清理后更新。它会将 BRG shader stripping 设为 KeepAll，保留运行时创建实体所需的 DOTS shader 变体。

菜单 `Tools > Roguelike > Build Windows Benchmark` 先验证，再只构建压测场景到 `Builds/Benchmark/RoguelikeBenchmark.exe`。不会改动原始 Main/SelectScene 的构建列表。默认 Release 构建；诊断用 Development Build 应另外记录。

PowerShell 示例（路径按实际位置调整）：

```powershell
& './Builds/Benchmark/RoguelikeBenchmark.exe' -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 --benchmark-count 10000 --benchmark-warmup 10 --benchmark-seconds 60 --benchmark-output './BenchmarkReports' --benchmark-quit
```

可添加 `--benchmark-no-render` 或 `--benchmark-synthetic-damage`。输出目录为进程工作目录相对路径或绝对路径；`--benchmark-quit` 仅在独立 Player 报告写入成功后退出，写入失败返回非零退出码。

## 读数限制

JSON 包含环境、实体数量、工作负载、平均 FPS、p50/p95/p99 帧耗时和可用时的 GC 分配统计。GC 计数器不可用时写 -1，不伪造为零。分配内存是结束时快照，不是峰值。

CPU 分线程耗时、GPU 帧耗时、Draw Call、可见数量和峰值内存仍需 Unity Profiler / Frame Debugger / 平台分析工具。编辑器数据不代表目标 PC；后台/隐藏窗口数据也仅用于功能检查。

## 框架扩展入口

- `Assets/Game/Runtime/Simulation`：基础数据组件。
- `Assets/Game/Runtime/Authoring/EnemyAuthoring.cs`：将 Mesh Prefab 放入 SubScene 烘焙流程时使用的 Baker。
- `Assets/Game/Runtime/Benchmark`：独立压测控制器和专用系统，不作为正式敌人 AI。
- `Assets/Game/Editor/BenchmarkTools.cs`：菜单、逻辑检查和独立构建。

正式敌人行为、索敌、命中检测、动画、掉落、升级，在澄清玩家预期后按模块接入；不把压测运动代码推广为游戏规则。
