> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 寒冰射手与 GameData 验证记录

日期：2026-10-22。工作目录：`D:/UnityProject/myGame`，分支 `main`。Unity 6000.3.18f1；所有导入、编译、构建与验证均在此主工作区完成。

## 已通过

| 项目 | 结果与证据 |
| --- | --- |
| Unity 导入与 C# 编译 | 主项目 Editor 导入新目录及改动脚本，未出现 C# 编译错误 |
| Editor 共享战斗检查 | 334 项通过；`CombatValidation.Validate`，含新增 31 项正式寒冰配置与执行检查；`Logs/AsheGameData/validate.txt` |
| 数字 ID 与导出 | `NumericConfigIds.ValidateAll` 通过，`HeroSkillDocumentation.Export` 成功生成 `Logs/HeroSkillCatalog/source.json` |
| GUID 与引用 | 78 份资源的 `.meta` 与迁移前逐字节一致；56 个数字 ID 不变；全部迁移资源内的外部 GUID 引用序列一致，旧路径资源已移走；`static-audit.json` |
| Windows Player 构建 | 主项目 `Assets/Scenes/Game.unity` 构建至 `Builds/Game/Game.exe` 成功；`Logs/AsheGameData/build.txt` |
| Player 集成 | 105 条 `GAME_CHECK_PASSED`，无失败与跳过项；`GAME_SMOKE_PASSED` 与 `Player/result.json` 的 `passed: true` |
| 画面与中文编辑 | 检查实际 Inspector、Player HUD 与技能提示截图：法力上限 240、按钮蓝耗、W 各级伤害/蓝耗/冷却正常；`inspector.png`、`Player/foundation-game.png`、`Player/ability-tooltip.png` |
| Excel | 更新原工作表中的资源路径、技能基础蓝耗/冷却/充能、效果文字及快照日期；Artifact Tool 重算、未检出公式错误；导出前后原公式、表、数据校验与冻结窗格保持一致；用 Excel 只读导出 PDF 复核表格样式 |
| 静态差异 | `git diff --check` 通过；场景没有改动；构建自动写入的渲染缓存与材质浮点尾数已清理 |

## 新增执行覆盖

- Q/W/E/R 的每级蓝耗、冷却/充能恢复与学习门槛来自正式资源并经共享编译库读取。
- Q 未满层不扣蓝；满层成功施放扣一次蓝，获得 60% 攻速并在持续时间结束后恢复。
- W 蓝量不足时不扣蓝、不造成伤害、不开始冷却；恰好足够时成功；四级造成 180 基础伤害，减速保持 35%。
- E 两次充能各消耗 60 法力、各造成 90 落点伤害；逐次恢复充能。
- R 首个目标结算 120 首中伤害 +100 爆炸伤害，附近敌人结算 100，后方范围外敌人不受伤；法力只扣一次，眩晕与减速生效。

开发过程曾因验证夹具在 W 充能恢复前重放四级 W 导致检查失败；已将验证时刻推进至充能恢复后，再次运行 334 项通过。此修正未改变技能执行代码。

## 边界与遗留

- 本次验证确认配置消费、资源引用和短时集成。完整 30 波数值体验、人工操作手感和万级敌人性能尚未验收。
- Player 退出时存在历史已有的 `ComputeBuffer` 未显式释放警告，旧验证日志也存在；本轮未修改其资源生命周期。没有把该警告记作“无警告”。
- Excel 仍是文档快照，未实现导入；现有怪物页保留 3000 兼容项的旧范围，完整模板请查 GameData/怪物及编号登记；寒冰逐级表完整说明见 `ashe-balance-and-gamedata.md`。
- 为操控正在打开的主项目 Editor，本轮临时使用了固定动作入口；完成迁移、验证和截图后删除该临时脚本及其 `.meta`，未新增常驻自动迁移或额外菜单。
