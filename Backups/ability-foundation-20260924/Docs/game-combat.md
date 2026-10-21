# Game 场景战斗指南

更新：2026-09-23。Game 是持续开发的主战斗场景。当前指南对应四份寒冰技能配置、释放指示器、E 落点范围伤害和关闭 Game 视野后的版本。旧版战斗规则与验证记录保留在[历史入口](game-demo.md)和[旧版验证记录](validation-game-demo-2026-09-22.md)。

## 运行与操作

在 Unity 打开 `Assets/Scenes/Game.unity` 并 Play。玩家、怪物和弹体目前使用引擎基础网格与项目 URP 材质。当前场景序列化为 `enemyCount=3000`、`safeSpawnRadius=30`；控制器代码的通用缺省值或旧版一万敌人报告不改变场景设置。

| 输入 | 当前行为 |
| --- | --- |
| 右键点击或按住地面 | 普通状态移动；移动中继续自动攻击最近存活怪物 |
| 滚轮 | 缩放俯视镜头 |
| Q 键或 Q 技能槽 | 普攻命中蓄满后立即施放自身攻速强化，不增加连射箭数 |
| W 键或 W 技能槽 | 打开扇形预览，左键确认后发出减速箭 |
| E 键或 E 技能槽 | 打开施法距离与落点范围双圆，左键确认后弹体飞抵目标点并造成一次范围伤害 |
| R 键或 R 技能槽 | 打开直线预览，左键确认后箭命中第一只怪物并爆炸、眩晕、减速 |
| 瞄准中右键或 Esc | 取消瞄准；右键按住取消后，松开前不触发移动 |
| F5 或死亡后的重新开始按钮 | 清理旧会话，重开并重新读取四份技能配置 |

HUD 显示生命、射程、存活敌人、击杀、弹体、时间、FPS 和 Q/W/E/R 状态。鼠标落在 HUD、屏幕外或无法投影到地面时不能确认旧落点；冷却中或 E 无充能不能开启对应瞄准。E 目标点被限制在施法距离和场地边界内。具体技能默认数值、场景资源和系统边界见[寒冰技能接入说明](ashe-game-integration.md)。

## 场景与系统边界

`01_Systems > Game Combat` 是会话装配、输入与 HUD 桥接；`03_Presentation > Effects > GameAbilityIndicator` 持有两个圆形 Prefab 实例和轮廓线。Q/W/E/R 的数值分别由 `Assets/Game/Skills` 下四份 ScriptableObject 提供，在重开时成为 ECS 会话快照。冷却、层数、充能、伤害与怪物状态由 ECS 结算。HUD 技能槽经 `UIAction` 发出技能意图，不直接访问 ECS。场景对象修改入口见[场景维护说明](scene-authoring.md)。

Game 当前没有迷雾、昼夜、真视、隐身、视野过滤或 E 侦查揭示；自动索敌面向所有存活怪物。Game 旧视野适配封存在 `CombatFogSession.cs` 单文件中，独立 `FogOfWarDemo` 保留原功能。玩家死亡后战斗冻结，重开清理旧敌人和弹体；离开场景释放当前会话资源。

## 验证状态

主 AI 在隔离副本执行 Unity 场景迁移、最终版 CombatValidation、Windows Player 构建和 Player 功能冒烟，证据位于 `D:\UnityProject\myGame_validation_20260923`：

- `migration2.log`：`GAME_COMBAT_MIGRATED`，批处理正常退出。生成场景资产已复制回主项目。
- `build4.log`：`GAME_COMBAT_LOGIC_VALIDATED`、`GAME_BUILD_SUCCEEDED`，批处理正常退出；逻辑检查覆盖 Q/W/E/R 和一万敌人追逐。
- `player3.log`：`GAME_SMOKE_PASSED`；Q/W/E/R HUD 按钮、W 扇形、E 双圆、R 直线和确认命令逐项 `GAME_PASS`；移动、HUD 遮挡、攻击、死亡、重开及离场清理也通过。
- `Smoke3/result.json`：`passed=true`、敌人 3000、18 发、8 命中、8 击杀。`Smoke3/e-aim.png` 可见 E 两个圆环；这是离屏画面检查。

控制器已静态修复右键按住取消后直到松开前的移动抑制，但未做真实鼠标输入测试。主项目 Editor 尚未单独运行；其他手工输入感受、Editor 交互视觉，以及一万敌人可见参战时的完整战斗帧时间仍未测量。旧版或独立压测数据不能充当此目标的结果。
