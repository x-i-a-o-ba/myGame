# myGame

Unity 6.3 + URP + ECS 的 3D 俯视角肉鸽游戏项目。

- Unity Editor：`6000.3.18f1`，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 依赖：URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`，以 `Packages/manifest.json` 为准。
- 开发文档：[Docs/README.md](Docs/README.md)。
- 开发约定：[AGENTS.md](AGENTS.md)。
- 游戏入口：`Assets/Scenes/Main.unity`（主菜单 → 选择英雄 → 选择战场）。战场场景：`Assets/Scenes/Game.unity`（幽暗荒原）、`Assets/Scenes/EmberRift.unity`（熔火裂谷，2026-10-23 新增，2026-10-24 已通过 Unity 构建与 Player 流程检查），见 [开局选择](Docs/hero-and-map-selection.md)。

## 恢复项目

```powershell
git clone https://github.com/x-i-a-o-ba/myGame.git
```

在 Unity Hub 中添加克隆后的项目目录，用上述版本的 Editor 打开。首次打开需要联网恢复 Package Manager 依赖并重新导入资源；`Library` 缓存会自动生成。

仓库保存 `Assets`（含 `.meta`）、`Packages`、`ProjectSettings`、开发工具、项目规则、文档、配置表导出和验证证据。原有的 `Backups/` 历史备份和 `DemoReports/` 演示报告已于 2026-10-24 随工程清理移除，需要时可从 git 历史提交 `9bd09b3` 恢复。Unity 缓存、构建产物、个人 IDE 状态及凭据不纳入版本管理，排除规则见 [.gitignore](.gitignore)。现有文件的换行与字节内容在首次入库时保持不变。

## 保存后续修改

```powershell
git status
git add .
git commit -m "说明本次修改"
git push
```

首次备份不代表完成游戏编译、功能回归或万敌完整战斗性能验收；历史验证范围以开发文档和报告为准。
