# Game 旧版战斗记录（历史兼容入口）

本路径保留给既有文档链接。当前 Game 场景的运行、操作、技能、场景默认值与验证状态见[Game 场景战斗指南](game-combat.md)和[寒冰技能接入说明](ashe-game-integration.md)。

2026-09-22 基础战斗版曾使用右键移动、自动攻击最近敌人、追踪弹、敌人追逐、死亡与重开；当时 Q/W/E/R 仍是占位，旧默认/检查曾使用一万敌人。其隔离逻辑、Windows Player 集成和画面记录见[旧版验证报告](validation-game-demo-2026-09-22.md)。这些记录只适用于当时版本，不能证明当前技能、指示器、E 范围伤害、关闭视野后的行为或万敌完整战斗性能。

当前 `Game.unity` 序列化为 `enemyCount=3000`、`safeSpawnRadius=30`。旧 `Builds/GameDemo/GameDemo.exe` 及历史 `--demo-smoke` 证据不用于验收现行 Game；现行隔离构建与 Player 冒烟证据见[新指南](game-combat.md)。
