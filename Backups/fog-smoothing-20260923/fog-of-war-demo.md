# 迷雾与视野独立演示

日期：2026-09-23。用户确认本地双阵营，覆盖通用 Dota 风格视野规则；美术只用于区分状态。本场景不接入 Game 的伤害、寻路或联网同步。

## 打开与操作

打开 `Assets/Scenes/FogOfWarDemo.unity`，或菜单 `Tools > Roguelike > Fog of War > Open Demo`，点击 Play。独立构建菜单为 `Build Windows Demo`，输出 `Builds/FogOfWarDemo/FogOfWarDemo.exe`，不更改项目原构建场景列表。

- 右键地面：瞬移当前阵营的观察者，测试来源移动与视野消退。
- 左键：选择当前可见目标；进入插眼模式后放置眼；Esc 退出插眼模式。
- WASD 平移相机，滚轮缩放；指针位于 UI 上时拦截场景操作。
- 左侧按钮：阵营、昼夜/自动循环、地面/飞行、敌人来源开关、真视、目标 0 隐身、树木、眼、临时飞行视野、探索模式、重置、总览及全知调试。
- 小地图同步过滤敌方标记；失去视野的已选目标只显示最后看见的位置和时间。

全知模式是有明确标识的调试显示，不改变两个阵营的逻辑快照。该模式看到的隐藏目标仍不能通过正常选择接口选中。

## 敌人视野预制体

`Assets/Game/FogOfWar/Prefabs/敌人视野.prefab` 是可复用的来源入口，只有加了此来源的指定敌人才贡献视野。**普通敌人不会自动提供视野**。场景中的一万目标只有显隐数据，不创建一万个额外 GameObject、视野脚本或 Update。

在本场景根节点 `Fog of War Demo` 下，把预制体作为指定敌人的子节点，设置本地位置为零。它随父节点移动；Inspector 配置阵营、昼夜半径、飞行、真视和持续时间，选中时可见范围 Gizmo。`Demo marker - replace or remove` 子物体仅是演示胶囊，可删除或替换。默认演示有蓝方观察者和一个红方来源，不等于全体敌人提供视野。

运行时增删来源后调用 `FogDemoSession.RefreshSources()` 重建低数量来源缓存；已经登记的来源启停、移动、阵营、范围变化在下一次快照生效，销毁来源会安全跳过。有限时长按 `expiresAt` 绝对游戏时间判定，重置会重新开始计时；复用来源时显式设置新的到期时间。普通敌人数据接入 `FogTargetBatch.Targets`，不通过来源扫描隐式发现敌人。

## 配置与规则

默认地图 128×128 格、每格 1 米、10 Hz 更新、一万目标。地图高度是离散等级，每级 3 米；`FogHeightRegion` 轴对齐区域的尺寸/等级应与展示网格一起修改。普通来源默认白天 16 米、夜晚 9 米；自动昼夜每 30 秒切换。数值是本项目演示配置，不是 Dota 当前版本的单位数值。

| 功能 | 已实现行为 |
| --- | --- |
| 未探索/已探索/可见 | 阵营分别维护历史和当前位图；支持初始地形已知；每次重算保留探索记录 |
| 阵营共享 | 同队来源取并集，移除重叠来源不会清除其他来源贡献 |
| 昼夜 | 来源分别配置两个半径，手动/自动切换；静止来源也更新 |
| 高低地 | 地面来源看不到更高平台及其后方；高处可看向低处 |
| 树木 | 阴影投射遮挡，砍树/恢复重算；迷雾中的树只保留最后观察状态 |
| 飞行 | 忽略树木/高地遮挡，仍受半径限制 |
| 侦察眼 | 普通地面视野，可被敌方真视揭露；30 秒演示寿命 |
| 真视眼 | 仅侦测隐身，不提供普通视野；必须与普通视野交集才看见隐身目标 |
| 隐身 | 模型、小地图、选择共同使用“当前可见且具备侦测”的结果 |
| 临时视野 | 3 秒飞行来源，到期撤销；固定 8 个场景眼槽循环复用 |
| 敌人信息 | 隐藏目标不提交实例绘制、不能选中、不显示实时位置；记忆只在看见时更新 |
| 生命周期 | 来源失效、过期、销毁和重置清除当前贡献；会话销毁释放原生数组和纹理 |

`trueSightRadius` 的侦测圆不受树/高地阻挡，但它不能单独产生普通可见性。地面扫描使用格子阴影投射；边角、窄缝与亚格移动存在离散化，不声称逐像素或逐英雄特例与 Dota 完全一致。低数量树木/来源/眼槽、UI、地面与平台均保存为可编辑场景对象。

## 模块与扩展边界

- `VisionSource / FogObstacle / FogHeightRegion`：场景配置，无逐目标 Update。
- `FogSolver`：只处理原生高度/遮挡网格与来源数据，Burst Job 计算当前视野、真视和探索位图，不依赖 Camera/UI。
- `FogTargetBatch`：并行查询一万目标，为两个阵营维护显隐/最后可见记录；演示绘制用 GPU instancing。
- `FogDemoInput`：桌面输入适配；`FogDemoHud / FogDemoButton`：场景 UI 与命令。
- `FogDemoPresentation`：消费快照，更新标记、小地图、迷雾纹理和选中信息；拥有显示资源。
- `FogDemoSession`：演示装配、地图配置采集和统一调度；持有并释放模拟资源。
- `FogDemoTools / FogValidation / FogDemoProbe`：编辑器搭建、规则回归与命令行 Player 验证。

本模块使用 **NativeArray + Burst/Jobs**，一万目标不是 Entities 实体，也不带战斗/动画。未来接入 Game ECS 时，将存活目标的位置/阵营/隐身写入查询数据，把结果用于渲染、UI、索敌过滤；不能停掉隐藏敌人的模拟。`Schedule` 返回 JobHandle，调用者必须完成任务后读写/释放数组，多个批次不可并发写同一 Solver。当前 Demo 在每次快照明确 Complete。

每次快照重建低数量地形配置并重算来源并集，避免增量撤销的重叠错误。来源扫描单 Job，目标查询并行；没有多个 Job 无同步写同一位图。未来优化应先测来源数/半径、地图脏标记和上传成本，不把一万目标误当一万来源。

本地模拟持有完整数据，过滤的是玩家显示/选择；这不是联网安全隔离。没有本次范围以外的英雄专属视野、攻击暴露、反隐例外、战争迷雾联网协议或正式美术。

## 验证记录

Unity 6000.3.18f1 的独立 Windows x64 非 Development 构建成功；16 项规则断言通过。最终 Player 的 25 项探针检查通过，包括三张离屏画面、万级目标、隐身/真视、不可选中、最后可见记忆、昼夜/自动循环、砍树、高地/飞行、阵营、来源开关、眼生命周期、运行时来源登记/销毁、重置和高地鼠标投影。

原始日志在 `Logs/FogDemoBuild.log`、`Logs/FogDemoPlayer.log`；`DemoReports/FogOfWar/result.json` 为 `passed: true`。同目录保存 `blue-day.png`、`flying.png`、`red-team.png` 和最终运行时代码 SHA256 清单。已人工检查离屏截图中的场景、中文 UI、阵营及三态显示。程序化命令/接口已测，未做真实鼠标逐按钮的人工长时间游玩，也未在实际联网场景验证。

退出时仍出现项目原有的 `GarbageCollector disposing of ComputeBuffer` 警告。本模块显式释放自己拥有的 Native 容器和纹理；没有证据将该既有渲染退出警告认定为本次已修复。性能探针独立于正式战斗验收。

可重复运行的命令（从项目根目录启动）：

```powershell
& './Builds/FogOfWarDemo/FogOfWarDemo.exe' --fog-smoke --demo-output 'D:/UnityProject/myGame/DemoReports/FogOfWar' -logFile 'D:/UnityProject/myGame/Logs/FogDemoPlayer.log'
& './Builds/FogOfWarDemo/FogOfWarDemo.exe' --fog-benchmark --demo-output 'D:/UnityProject/myGame/DemoReports/FogBenchmark' -logFile 'D:/UnityProject/myGame/Logs/FogBenchmark.log'
```

测试开关默认不启用，正常启动不会自动运行探针或退出。构建菜单会先运行 `FogValidation.Validate`；它使用实际 Job 核心验证固定小地图，不依赖截图代替规则断言。

## 性能实测：视野模块

2026-09-23，Windows 11 / Ryzen 7 7745HX / RTX 4060 Laptop，D3D11、1920×1080 窗口、非 Development Player，后台运行。最终版本连续三轮，各预热 10 秒、采样 60 秒，每轮 576 次快照，总计 1,728 次。负载为 128 个移动来源、128×128 网格、普通半径 16 格、一万静态显隐目标；来源交替阵营，每 8 个含一个半径 12 格真视，每 9 个含一个飞行来源。

| 每次快照的墙钟耗时 | p50 | p95 | p99 |
| --- | ---: | ---: | ---: |
| 地图配置重建 + 来源求解 + 等待 | 2.448 ms | 3.267 ms | 3.365 ms |
| 一万目标查询 + 等待 | 0.040 ms | 0.058 ms | 0.083 ms |
| 显示数据/纹理上传/HUD | 1.570 ms | 1.868 ms | 1.946 ms |
| 总和（按每个样本合计后求分位数） | 4.080 ms | 5.164 ms | 5.319 ms |

三轮总耗时 p95 分别为 5.102 / 5.191 / 5.192 ms。阶段分位数不能直接相加替代总分位数。10 Hz 降低更新频度，但发生更新的那一帧仍承担完整快照开销；不把耗时除以十后伪装成峰值成本。

探针暂停普通会话 Update，以批量输入调用实际求解、查询、显示路径；该计时不含逐帧目标实例绘制、战斗、动画、GPU 帧耗时，也不含 Game ECS。每次约 100ms 后采下一个快照，因此包含处理时间后的实际采样率略低于 10Hz。未测端到端可见性延迟、GC 峰值、正式前台 FPS、低档机器和更大地图；此结果不构成完整万敌战斗达标或最低配置声明。下一步优化可优先观察地图配置重建与显示上传，而非为普通敌人增加来源。

原始文件：`DemoReports/FogBenchmark/performance.csv`、`performance.txt`、`summary.json`；日志 `Logs/FogBenchmark.log`。首次中途构建调整期间的采样已弃用，以上仅采用最终构建的三轮结果。

研究资料与取舍见 [视野研究](fog-of-war-research.md)。
