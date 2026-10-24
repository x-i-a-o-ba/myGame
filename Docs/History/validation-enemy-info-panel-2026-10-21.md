> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 选中敌人信息面板合并验证

日期：2026-10-21。对象：[PR #3](https://github.com/x-i-a-o-ba/myGame/pull/3)，原始提交 `7795d18`。用户要求合并新改动。

## 环境与结果

独立工作区 `D:/UnityProject/myGame_merge_20261021`，Unity `6000.3.18f1`、URP `17.3.0`、Entities `1.4.8`、Entities Graphics `1.4.21`。主项目已有本地日志保留。

| 检查 | 结果 |
| --- | --- |
| Unity 导入与脚本编译 | 通过，无需修改功能代码 |
| `Roguelike.Editor.CombatValidation.Validate` | 137 项通过，含目标面板资源、全部绑定键、关闭命令、8 种头像及 ECS 快照规则 |
| `Roguelike.Editor.GameTools.Build` | Windows x64 构建成功 |
| `Game.exe --game-smoke` | 49 项通过，`passed=true` |
| 目标信息面板实际画面 | 已查看 Player 截图，头像、名称、生命条、属性、攻击模块、说明和关闭按钮可见 |

新增 Player 检查覆盖目标选择后面板显示、名称与生命文本、面板截图、关闭按钮清除选择并隐藏面板；同时运行上一轮成长、指令与敌人混刷回归。Editor 规则覆盖 Buff、属性修正、攻击状态，以及目标死亡/销毁后不可展示。未进行原始左键/Esc 事件注入、所有敌人/状态外观、多分辨率或完整一局人工验收；不包含万敌性能结论。

证据：[检查输出](../../Logs/EnemyInfoPanel/validation-checks.txt)、[Player 结果](../../Logs/EnemyInfoPanel/Player/result.json)、[目标面板截图](../../Logs/EnemyInfoPanel/Player/target-info.png)。原始日志保留在验证工作区，Unity 构建产生的无关渲染配置序列化差异未纳入合并。

Git 智能 HTTP 拉取曾出现连接重置，改用 GitHub API 获取对象；按 Git 原始字节重建并验证 SHA 后更新远程跟踪引用，取得的提交与 PR 头完全一致。
