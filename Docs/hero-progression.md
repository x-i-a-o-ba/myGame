# 英雄成长、技能等级与波次难度

**2026-10-24 当前覆盖说明：** 成长和天赋暂保留；波次难度已迁移为[持续刷怪导演](spawn-director.md)，敌人按累计分钟成长，单关击杀首领结束。本文波次配置、波数增长、最终波胜利规则是历史；15 级新成长由远征第 3 步继续。

更新：2026-10-22。本文记录 Game 场景的局内成长闭环：打怪拿经验 → 升级拿技能点 → 学习/升级技能 → 10/15/20/25 级选天赋 → 敌人按波次变强 → 击杀第 30 波最终首领获胜或阵亡结算。

## 用户要求与 AI 决定

用户要求：参照 Dota 2 做升级系统；先用一个角色（寒冰射手）跑通打怪升级、解锁技能等完整流程，难度随时间增加。

以下数值和规则由 AI 按 Dota 2 习惯确定，均为可在 Inspector 中修改的配置；当前平衡与实跑证据见 [30 波平衡](wave-balance-and-stat-details.md)：

| 规则 | 当前值 | 配置位置 |
| --- | --- | --- |
| 等级与经验 | 1–30 级，采用 Dota 2 经验表（2 级 240，6 级累计 2440，30 级累计 63900） | `Assets/GameData/成长/经验与等级规则.asset` |
| 技能点 | 1 级 1 点，每升 1 级 +1 点；开局四个技能均未学习 | 同上 |
| 普通技能（Q/W/E） | 最高 4 级，学习等级门槛 1/3/5/7；2026-10-22 起寒冰射手使用独立逐级表，见 [当前数值](ashe-balance-and-gamedata.md) | SkillDefinition 的“技能等级成长” |
| 终极技能（R 冰晶巨箭） | 最高 3 级，门槛 6/12/18；独立逐级伤害/蓝耗/冷却表，控制强度固定 | `1003_冰晶巨箭.asset` 的自定义逐级表 |
| 属性成长（寒冰射手） | 每级 +38 生命、+17 法力、+2.8 攻击、+2.8% 攻速、+0.19 生命回复、+0.25 法力回复；开局生命 340、法力 260、生命回复 1.8/秒、法力回复 4.5/秒 | `Assets/GameData/英雄/2000_寒冰射手.asset` |
| 天赋树 | 10 级：+20 攻击 / +25% 经验；15 级：+25% 攻速 / +250 生命；20 级：+30% 技能伤害 / -20% 冷却；25 级：+60 攻击 / +10 回复。每层二选一，不耗技能点。2026-10-22 起天赋与装备共用 15 种属性（`StatKind`），数值直接相加，百分比填百分点（原小数 0.25 已迁移为 25） | `寒冰射手.asset` 的“天赋树” |
| 波次 | 开局第 1 波，之后每 20 秒一波，共 30 波（约 9.7 分钟出最终首领） | `Assets/GameData/关卡/关卡难度曲线.asset` |
| 普通敌人增长 | 数量 45 × (1 + 0.10 × (波次-1))；基准生命 50、攻击 6、移速 2.5；生命逐波 ×1.075、攻击 ×1.045、移速 +0.025（上限 4）、经验 5 ×1.03^(波次-1) | GameData 关卡难度曲线 |
| 精英 | 每 5 波，数量 2 + floor(波次/5)，生命 ×5、攻击 ×1.6、经验 ×12 | 同上 |
| 首领 | 第 10/20/30 波，生命 ×22、攻击 ×2.5、移速 ×0.8、经验 ×80；第 30 波停止刷怪并等待首领被击杀 | 同上 |

参考值与逐波实跑见 [30 波平衡](wave-balance-and-stat-details.md)。同屏存活上限仍由 GameCombatController.enemyCount（3000）控制；超过上限被略过的数量必须在验证中单独报告。此配置不是万级同屏性能验收配置。

## 操作

（2026-10-21 起操作与 HUD 改为 Dota 2 式，完整说明见 [enemies-and-controls.md](enemies-and-controls.md)。）

- 右键地面移动、右键敌人攻击、A 攻击移动、S 停止、H 待命，待机时自动攻击；Q/W/E/R 施放已学习的技能，未学习的技能不能进入瞄准。
- **Ctrl+Q/W/E/R** 或点击技能图标上方的“+”学习/升级技能；技能下方的等级点显示当前等级。
- 达到天赋等级时技能栏上方自动打开天赋树（10/15/20/25 级四层），按 **1/2** 或点击左右选项；也可以点 HUD 右侧的天赋树按钮随时查看。
- 底部 HUD 的头像显示等级与经验环、未用技能点；技能栏上方提示条显示升级、技能点和天赋提示；顶栏显示波次、下一波倒计时和首领血条。
- 胜利或阵亡后显示结算（波次、等级、击杀、用时），F5 或按钮重开，重开后等级、技能和波次全部重置。

## 数据所有权与生命周期

- **配置**：`HeroProgressionRules`（经验表、技能点）、`HeroDefinition`（属性成长、天赋）、`SkillDefinition.growth`（技能等级档）、`CombatDifficultyDefinition`（波次曲线）都只存配置，不保存运行状态。运行中修改需重开生效。
- **编译**：重开时 `SkillCatalog` 把技能等级编译进只读技能 Blob（`SkillData.Levels`）；`HeroProgressionLogic.Compile` 把经验表和天赋编译成 `ProgressionLibrary` Blob，随会话释放。非法配置（经验表不足、门槛递减、NaN、天赋层乱序、未知属性）在编译时报错。
- **运行状态**：英雄等级、经验、技能点、已选天赋在会话实体的 `HeroProgression`；每个技能的等级在拥有者的 `AbilitySlot.Level`；波次进度与胜负在 `CombatWaveState`。生命和所有属性仍以 `AbilityUnit` 为唯一权威。2026-10-22 起属性来源拆分在 `HeroStats`：等级、天赋或装备变化后 `HeroStatsLogic.Rebuild` 按“基础 + 成长 ×（等级 - 1）+ 天赋 + 装备”重算合计并写入单位基础值与经验倍率，再由 `AbilityEffects.Recalculate` 统一合成 Buff（不再逐项增量修改），见 [equipment-and-stats.md](equipment-and-stats.md)。
- **输入**：Controller 只把“学习第 N 个槽”“选第 N 个天赋”写入 `ProgressionCommand` 缓冲；`CombatSimulationSystem` 在主线程串行处理，失败原因写回 `HeroProgression.LastFailure`。
- **技能强度**：技能等级不按 QWER 或英雄分支。伤害效果 × 等级伤害倍率 × (1 + 技能增强)，治疗 × 等级倍率，Buff 修饰/周期效果 × Buff 倍率；冷却和充能恢复 × 等级冷却倍率 × (1 - 冷却缩减)，冷却缩减属性上限 80%；普攻保持原值。施放时等级写入已排程阶段和飞行物，飞行中升级不会改变已发出的技能。

## 更新顺序

在原 [Game 战斗更新顺序](architecture.md) 基础上：Buff 更新 → 英雄阵亡判定 → **处理成长命令与装备指令（2026-10-22）** → 移动 / 敌人追逐 / 普攻 / 技能 → 汇总死亡、经验与金币 → ECB 回放 → **结算金币、经验与升级并重算属性** → **按时间刷下一波（每帧最多一波）** → HUD 快照。胜利后整局冻结；阵亡记为失败。

## 实现文件

| 位置 | 内容 |
| --- | --- |
| `Abilities/Core/Progression/` | 技能等级成长、经验规则 SO、天赋定义、`HeroProgression` 组件与 `HeroProgressionLogic` |
| `Abilities/Core`（修改） | SkillData 等级表；AbilityUnit 增加基础回复、攻速加成、技能增强、冷却缩减；施放按等级缩放消耗、冷却与效果；未学习返回 `NotLearned` |
| `Runtime/Combat/CombatDifficultyDefinition.cs`、`CombatWaveDirector.cs` | 难度曲线配置与刷波 |
| `Runtime/Combat`（修改） | 会话建立成长与波次状态；系统结算经验、升级与胜负；Controller 输入；HUD 成长/天赋面板与结算 |
| `Resources/UI/Panels/UI.Game.Progression`、`UI.Game.Talent` | 成长面板、天赋面板（按需加载，已登记资源目录） |
| `Abilities/Editor/ProgressionChineseInspector.cs`、`Editor/CombatDifficultyChineseInspector.cs` | 中文 Inspector 和绘制器，难度曲线带波次预览 |
| `Editor/CombatValidation.Progression.cs` | 编辑器验证：技能等级、英雄成长、天赋、波次（2026-10-22 改用 `HeroStatsLogic` 新属性 API） |
| `Abilities/Core/Stats/`（2026-10-22） | 15 种属性、`HeroStats` 与 `HeroStatsLogic`（属性合成），见 [equipment-and-stats.md](equipment-and-stats.md) |

## 验证

- 菜单 **Tools/Roguelike/验证/战斗与成长（不构建）**：运行原战斗检查和新增的技能等级、经验表、升级属性、天赋、难度曲线、刷波检查。
- 2026-10-22 已在 Unity 运行 343 项 Editor 战斗/成长/配置检查，Windows Player 集成检查 135 项通过；正常速度自动操作完整 30 波，608.63 秒获胜。覆盖新属性 API、回蓝成长、单项属性悬停、头像总览及最终波边界；见 [本轮验证记录](validation-wave-balance-2026-10-22.md)。人工手感和万级完整战斗性能尚未验证。
- Player 冒烟：带 `--game-smoke [--game-output 目录]` 启动，`GameCombatProbe` 检查首波刷出、初始 1 级 1 技能点、未学习不能瞄准、升到 7 级、按钮学习四个技能、大招门槛、10 级天赋、第 2 波、换英雄、重开重置、阵亡结算；结果写入 `result.json`（新增 wave、level 字段）。

合并前复验（2026-10-21）：在独立 Git 工作区使用 Unity 6000.3.18f1 完成资源导入、脚本编译、89 项 Editor 战斗/成长规则检查、Windows Player 构建与 31 项 Player 集成检查，全部通过；成长面板和技能指示器离屏截图已检查。修复新增验证脚本的 CS1612、重复施法测试未补足充能，以及旧直线弹体测试未推进释放后时间的问题。Inspector 实际外观、天赋面板布局、完整 30 波实玩与数值手感仍未验收；未进行万敌完整战斗性能测量。证据见 [合并验证记录](validation-hero-progression-2026-10-21.md)。

## 限制与后续

- 正式寒冰射手与换槽示例使用同一套基础成长，英雄可分别配置。
- 经验直接在击杀时获得，没有经验球掉落、拾取范围或共享经验；没有死亡惩罚和复活。
- 敌人已包含近战、远程、冲锋和自爆行为；精英/首领仍主要按档位倍率强化，尚无复杂多阶段首领技能。
- 技能等级只缩放数值；没有“升级改变形态”（如增加弹体数量），可后续用自定义等级行或配方扩展。
- 万级敌人下的帧率需在 Player 中实测；后期波次数量可能需按性能结果调整。
