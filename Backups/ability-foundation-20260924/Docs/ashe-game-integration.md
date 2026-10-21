# Game 寒冰技能、配置与释放指示器

更新：2026-09-23。**状态：源码、四份技能资产、Game 场景指示器与 HUD 按钮已绑定；隔离副本最终版 Unity 编译、CombatValidation、Windows 构建和 3000 敌人 Player 技能冒烟通过。生成场景资产已复制回主项目；主项目 Editor 未单独运行，手工交互视觉和万敌完整战斗性能未验证。**本页描述现行 Game 目标与代码边界；此前 E 侦查和 Game 真实迷雾的测试结果属于历史版本。

## 用户确认与本轮取舍

用户此前确认：不做英雄被动；Q 自身蓄层，满层施放只增加攻速；W/R 各自添加减速；R 的箭命中第一只任意类型怪物后爆炸。用户最新要求：Game 不再称 Demo；技能改为可配置资源；接入实际释放指示器；E 改为目标点范围伤害；Game 暂时关闭所有视野、昼夜和隐藏机制，Game 旧视野代码集中保存在一个文件，独立 `FogOfWarDemo` 不变。最新方向替代此前“E 飞行侦查、Game 正常玩法有迷雾”的确认，不把旧需求继续写成当前行为。

主 AI 采用的项目数值与表现方案：Q/W/E/R 各有一份 ScriptableObject 配置；重开时校验并复制成 ECS 的 `CombatAbilitySettings` 会话快照。W 使用扇形、E 使用施法距离圈与落点范围圈、R 使用直线预览；Q 是自身施放，无落点预览。配置资产只保存静态参数；层数、冷却、充能及命中由本局 ECS 管理。以上具体数值和实现形状由 AI 选定，并非用户逐项确认或英雄联盟官方数值。

## Game 操作

打开 `Assets/Scenes/Game.unity`。当前使用旧 Input Manager；技能槽通过场景 `UI.Game.BottomHUD` 面板发送意图。完整场景操作与验证入口见[Game 战斗指南](game-combat.md)。

| 输入 | 预期行为 |
| --- | --- |
| 右键点击/按住地面 | 非瞄准时移动，移动期间继续自动攻击最近存活怪物 |
| Q 或 Q 技能槽 | 普攻命中蓄满后立即施放自身攻速强化，仍只发单箭 |
| W 或 W 技能槽 | 进入扇形瞄准；左键确认后发出九条扇形箭，命中造成伤害与减速 |
| E 或 E 技能槽 | 进入目标点瞄准；显示距离与伤害范围，超距落点截到边界；左键确认后弹体飞抵落点并造成一次范围伤害 |
| R 或 R 技能槽 | 进入直线瞄准；左键确认后箭飞行，命中首只怪物后对附近怪物爆炸、眩晕、减速 |
| 右键或 Esc（瞄准中） | 取消瞄准；右键按住取消后，松开前不发出移动命令 |
| 滚轮 / F5 | 缩放镜头 / 重开本局并清除技能与预览状态 |

指针位于 HUD、屏幕外或不能投影到地面时，W/E/R 不能确认旧落点；死亡后停止施法。E 的实际落点同时限制在施法距离和场地边界内。R 的直线表示可能飞行路径，实际爆炸点由首只命中的怪物决定。技能在冷却中或 E 无充能时不能进入新瞄准，ECS 仍是施法和结算的最终裁定者。右键按住取消后的移动抑制已在控制器静态修复，真实鼠标输入流程尚未测试。

## 可配置数值

在 `Assets/Game/Skills` 打开 Q Frost Focus、W Volley、E Frost Impact、R Crystal Arrow 资源，或沿场景 `01_Systems > Game Combat` 控制器的 Q/W/E/R 引用打开对应 ScriptableObject；**F5 重开或重新进入 Play 后生效**。当前项目选用的默认值如下，W 伤害保留本场景原有的 30：

| 技能 | 本项目默认数值 |
| --- | --- |
| Q | 4 次成功普攻命中蓄满；攻速强化 5 秒；1.6 倍；冷却 7 秒 |
| W | 9 箭；射程 22 米；扇形半角 35°；单箭命中半径 1 米；伤害 30；减速移速倍率 0.65、持续 2.5 秒；冷却 5 秒 |
| E | 目标点最远 25 米；飞行速度 42 米/秒；落点半径 5 米；范围伤害 18；最多 2 次充能，每次 15 秒恢复 |
| R | 最远 150 米；速度 48 米/秒；命中半径 1.15 米；爆炸半径 7 米；首怪额外伤害 20、范围伤害 16；眩晕 1.5 秒；减速移速倍率 0.5、持续 3 秒；冷却 20 秒 |

Game 场景现有 `enemyCount` 序列化值为 **3000**，`safeSpawnRadius` 为 **30**，本轮均已保留；控制器代码中的通用缺省值或旧 Player 曾生成 10000 怪物，均不改变场景值。完整战斗“一万只可见且参战”的目标仍须在显式配置一万后另做 Player 测量，不用旧短时检查推定 FPS 达标。

## 模块与生命周期

- `CombatQSkillConfig`、`CombatWSkillConfig`、`CombatESkillConfig`、`CombatRSkillConfig`：Unity 资源中的编辑期参数。`GameCombatController.RestartRun` 调用 `Apply`，写入同一局的 `CombatAbilitySettings`，资源不持有运行状态。
- `GameAbilityIndicator`：Game 专属瞄准和预览。复用独立指示器程序集的通用 `CircleIndicatorView` / `CircleTargeting`；独立演示的输入、HUD 和视觉反馈控制器不进入 Game。固定节点已放在 Game 的 `03_Presentation > Effects`：两个 `Assets/Game/Skills/Indicators` 下的无演示装饰圆形 Prefab 实例及一条 `LineRenderer`，三者均在 Inspector 显式绑定。
- `GameCombatController`：输入、场景引用和会话装配；确认后写 `CombatAbilityCommand`，读取小型状态快照刷新 `CombatHud`。`CombatHud` 只转发场景 UI 的重开和技能槽意图，不直接访问 EntityManager。
- `CombatSimulationSystem`：普攻蓄层、Q 攻速、W 首命中去重与减速、E 落点范围伤害、R 首怪爆炸及控制、生命与击杀统一结算；不读取 HUD 或指示器。重开销毁旧 `CombatOwned` 会话实体，退出释放本局资源。
- `CombatFogSession.cs`：此前 **Game 视野适配**代码集中在此，并以文件级条件编译封存；现行 Game 不创建它、不依据可见性显隐或索敌。Game 地面已改为 `GameGround.mat`，旧视野组件已从 Game 场景移除；独立 `FogOfWarDemo` 的场景、源码和资源保持原演示行为。未来要恢复时先重新设计 Game 接口与验证。

## 验证状态与历史证据

当前改造的四份配置类型与资源、E 范围弹体数据、`GameAbilityIndicator` 和 Game 控制器输入路径已写入。静态复核 `Game.unity`：`Game Combat` 绑定四份资源与指示器，指示器绑定距离圈、落点圈和轮廓线，HUD 四个技能槽通过 `UIAction` 发送 `combat.ability` 数值 0–3；场景保持敌人 3000 与出生安全半径 30。隔离副本 `D:\UnityProject\myGame_validation_20260923\migration2.log` 记录 `GAME_COMBAT_MIGRATED` 和正常退出；最终版 `build4.log` 记录 `GAME_COMBAT_LOGIC_VALIDATED`（Q/W/E/R 与一万敌人追逐）、`GAME_BUILD_SUCCEEDED` 和正常退出。最终版 `player3.log` 逐项记录 Q/W/E/R HUD 按钮、W 扇形、E 双圆、R 直线预览及确认命令的 `GAME_PASS`，并记录 `GAME_SMOKE_PASSED`；`Smoke3/result.json` 的 `passed=true`，3000 敌人、18 发、8 命中、8 击杀，移动、HUD 遮挡、死亡重开和离场清理通过。`Smoke3/e-aim.png` 的离屏画面可见两个 E 圆环。**以上只验证隔离副本；右键按住取消后的移动抑制是静态代码修复，尚未做真实鼠标输入测试。生成资产已复制回主项目，但主项目 Editor 未单独运行，手工交互视觉及万敌完整战斗性能尚未验证。**

此前版本在 `Logs/AsheBuildFinal.log`、`Logs/AsheAbilityValidation.log`、`Logs/AsheFogValidationRecheck.log` 和 `DemoReports/AsheSmokeFinal` 留有 Unity/Player 记录。它们验证的是 **E 提供视野且 Game 开启迷雾**的旧版本，不能作为本轮 E 范围伤害、指示器、关闭视野后的自动索敌或一万怪物性能的通过证据。旧代码备份与验证来龙去脉仍可在 `Backups/ashe-game-20260923` 等目录查阅。
