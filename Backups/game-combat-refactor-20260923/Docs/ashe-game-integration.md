# Game 寒冰技能与真实迷雾接入

更新：2026-09-23。状态：**代码、Game 场景与 HUD 已接入；Unity 编译、Windows 构建、Q/W/E/R ECS 验证、Game 迷雾显隐联动和 10,000 敌人基础 Player 检查已通过。**这里的技能是按用户给定方向制作的项目实现，默认数值由 AI 策划拟定、主 AI 裁决，均可在 Inspector 修改；不声称与英雄联盟现行版本数值完全一致。完整战斗性能与部分交互细项仍须按对应证据单独评估。

## 依据与 AI 裁决

用户此前在原生任务中已确认：不做被动；Q 由普攻命中自身蓄层，满层施放只提高攻速，不做多箭；W/R 各自附带减速；E 接入 Game 的真实迷雾，包括昼夜、高低地、树木等规则；R 命中第一只任意怪物后爆炸并影响周围怪物；自动普攻只选择当前可见敌人，手动 W/R 可以盲射并命中隐藏在迷雾里的敌人；正常玩法保留迷雾，另有全可见配置供万敌负载验收。用户又明确授权团队决定其余普通细节并实施，完成后汇报。

团队作出的实现决定：维持 Game 现有 ECS 战斗会话与独立 FogOfWarDemo 两个场景，复用 `FogSolver` 和 `FogVisualMask` 到 Game，避免将独立 Demo 的假目标、UI 与伤害结算直接搬进战斗。W 用九条扇形射线，每箭只命中首先碰到的怪，同一怪最多受到一次 W 伤害；视觉箭矢是施法反馈，命中由 ECS 同帧结算。E 有两次充能，飞行途中和落点都提供临时飞行视野。R 直线飞行，碰到首只怪后爆炸。剩余规则、数值和边界由主 AI 按现有普通怪 30 生命、场地半宽 125 和可维护性选定。

## 打开与操作

打开 `Assets/Scenes/Game.unity` 并 Play；旧的 `Tools > Roguelike > Open Game Demo` 入口仍可用。使用旧 Input Manager。

| 操作 | Game 中的行为 |
| --- | --- |
| 右键点击/按住 | 向鼠标地面位置移动；移动时继续自动攻击当前可见的最近怪物 |
| Q | 普攻成功命中累计到 4 层后，消耗层数进入限时攻速强化；仍为单发追踪普攻 |
| W | 向鼠标方向施放扇形寒冰箭，伤害并减速；可射中迷雾中的怪物 |
| E | 向鼠标地面位置发射鹰眼，沿途和落点揭示视野；最多两次充能 |
| R | 向鼠标方向发射直线魔法箭，命中首只怪后爆炸，范围伤害、眩晕和减速；可盲射 |
| N | 手动切换昼夜；默认每 30 秒自动切换 |
| 滚轮 / F5 | 缩放镜头 / 重开本局，清除旧会话技能状态与临时视野 |

技能键在指针位于 HUD 上时改用主角当前朝向，不把 HUD 坐标当作地面目标。Q/W/E/R 状态栏显示层数、冷却或充能。死亡后本局停止施法，重开恢复技能、迷雾探索与怪物会话。

## 默认可调数值

在场景 `01_Systems > Game Demo` 的 `CombatDemoController` Inspector 中修改 `abilitySettings`；本轮设置重开后生效。

| 技能 | 项目默认值 |
| --- | --- |
| Q | 4 次成功普攻命中蓄满；强化 5 秒；攻速 1.6 倍；冷却 7 秒 |
| W | 9 箭；射程 22；扇形半角 35°；命中半径 1；伤害 12；移速乘数 0.65、持续 2.5 秒；冷却 5 秒 |
| E | 最多 2 充能；射程 110；飞行速度 42；飞行视野半径 16；抵达后揭示 5 秒；每次充能恢复 15 秒 |
| R | 最大飞行 150、速度 48、碰撞半径 1.15；爆炸半径 7；直击额外伤害 20、范围伤害 16；眩晕 1.5 秒；移速乘数 0.5、持续 3 秒；冷却 20 秒 |

伤害、死亡与击杀由 Game 同一 ECS 战斗系统结算；同一目标在同一轮多个命中只计一次死亡。普通敌人基础生命 30，因此 W 和 R 对外围目标通常保留可观察的减速/眩晕状态。没有为一万怪物创建一万个视野 GameObject。

## 迷雾场景与配置

Game 地面使用 FogGround 着色器；`CombatFogSession` 复用原生 `FogSolver` 的探索、当前可见、昼夜、高地、树木、飞行视野、真视与阵营位图，并通过 `FogVisualMask` 输出柔边过渡。网格默认 256×256，覆盖场地 `[-arenaHalfSize,+arenaHalfSize]`，默认每秒刷新 10 次；敌人实际显隐和自动索敌依据逻辑快照，不依据柔边显示像素。

Game 的 `Environment` 下保存一个可编辑高地与三棵树。高地 `FogHeightRegion` 和树 `FogObstacle` 的尺寸、位置及开关均可在场景中查看。可在同一地形根下增加少量 `VisionSource`：支持阵营、白昼/夜间半径、飞行、真视、有效期和开关；普通 ECS 怪物默认不提供视野。`invisibleEnemyCount` 可把开局前 N 只怪物设为隐身，需同时处于普通视野和玩家阵营真视内才能显示与被自动索敌。移动或编辑树和来源后，视野适配器在刷新时采集；地形数量与高地布局变化需调用 `RefreshTerrain()` 或重开。

`allVisibleForLoadCheck` 默认关闭，正常 Game 有迷雾。仅在要观察/验收一万只可见战斗怪物时，在同一 Controller Inspector 勾选此项并重开；它是独立配置，不代表 10k FPS 已达标。Game 中不带独立 FogOfWarDemo 的调试按钮、小地图和双阵营观察者切换；该演示场景继续用于单独查看这些工具。Game 的敌人显隐、地面迷雾、索敌和 E 则使用同一真实逻辑快照。

现有 Game 追逐系统仍采用平面直线移动；高地和树木在这次接入中承担视野遮挡与可观察地形展示，没有新增敌人绕障、碰撞或登高寻路。视野规则与移动规则的这个边界由主 AI 保留，以免把寒冰技能接入扩成未规划的寻路重构。

## 文件边界与验证状态

- `CombatSimulationSystem`：敌人追逐、普攻与 Q/W/E/R 命令、统一伤害/击杀和控制状态。
- `CombatFogSession`：Game 对独立 `FogSolver` 的适配，读取真实 ECS 怪物位置，写可见标志和 Entities Graphics 显隐；管理迷雾纹理与资源。
- `CombatDemoController`：场景资源、键盘/鼠标输入、会话装配、昼夜、技能视觉反馈和 HUD 快照。
- `UI.Game.BottomHUD` / `UI.Game.Controls`：可编辑的技能槽状态和操作说明。
- `Game.unity`：技能默认值、迷雾材质、地形根、高地和树；场景 GUID 保持不变。

初次交付只做静态审查，随后 Unity 编译发现 `CombatFogSession.cs` 的 CS1648 与 `CombatSimulationSystem.cs` 的 CS1654。已通过迷雾 `NativeArray` 的可写局部副本更新同一像素缓冲，W 首命中缓冲改为可写局部变量并在 `finally` 中释放。此次主动检查还从主项目 Editor 日志发现 `CombatVisionFlare` 缓冲在迷雾显隐产生 ECS 结构变更后失效，已在绘制技能视效前重新取得缓冲。新的 EditMode 迷雾验证发现会话拥有的纹理在编辑模式使用延迟销毁产生警告，已改为仅在编辑模式立即释放、运行时仍按帧释放，复验不再出现该警告。原功能改动前副本位于 `Backups/ashe-game-20260923`，编译修复前副本位于 `Backups/ashe-compilefix-20260923`，本次清理修复前副本位于 `Backups/ashe-validation-cleanup-20260923`。

AI 在独立项目副本 `Temp/AgentValidation` 中使用 Unity 6000.3.18f1 验证，避免与正在打开主项目的 Editor 争用：

| 检查 | 结果与证据 |
| --- | --- |
| Unity 编译、基础逻辑、Game 场景校验、Windows Player 构建 | `Roguelike.Editor.GameDemoTools.Build` 退出码 0；基础和 Q/W/E/R 逻辑标记 `GAME_COMBAT_LOGIC_VALIDATED`；最终日志 `Logs/AsheBuildFinal.log`。最新构建已同步到 `Builds/GameDemo/GameDemo.exe`。 |
| Q/W/E/R ECS 专项逻辑 | `Roguelike.Editor.CombatValidation.Validate` 退出码 0；覆盖普攻蓄层、Q 强化攻速/冷却、W 首命中去重/减速、E 飞行/充能、R 首怪爆炸/眩晕/减速；独立日志 `Logs/AsheAbilityValidation.log`，最终构建中也复跑通过。 |
| Game 迷雾与 E 视野联动 | `Roguelike.Editor.CombatFogValidation.Validate` 退出码 0；核对近处可见、远处隐藏、E 临时视野揭示与过期再隐藏，且 ECS 索敌标志与渲染显隐一致；最终日志 `Logs/AsheFogValidationRecheck.log`，无编辑模式纹理销毁警告。 |
| 最新 Player 基础交互 | `--demo-smoke` 退出码 0，最终报告 `DemoReports/AsheSmokeFinal/result.json`：10,000 敌人，26 次发射、24 次命中/击杀，移动中战斗、HUD 拦截、死亡、重开和清理通过；`combat.png`、`overview.png`、`defeat.png` 已检查可见画面；日志 `Logs/AsheSmokeFinal.log` 无运行时异常。 |

上述 Player 检查是短时功能检查，截图证实默认迷雾与 HUD 能渲染；它没有对 Game 中夜晚、高地/树木、真视/隐身、手动盲射的每个交互组合逐项自动断言，也不是全可见一万敌人的帧率测试。独立 FogOfWarDemo 的历史结果不能代替这些 Game 场景细项；需要相应结论时由 AI 补做对应检查。用户如有额外试玩反馈，可直接给主 AI 作为调整依据。
