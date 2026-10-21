# 万级同屏肉鸽开发文档

更新日期：2026-09-23。本文档区分「用户确认」「检查事实」「建议」「待确认」，建议不等于已批准或已实现。

## 已确认方向

- 从 Unity 2021 迁移到 Unity 6 系列。
- 使用 URP 渲染管线和 ECS 相关架构。
- 开发同屏一万以上的肉鸽游戏。
- 使用 3D 俯视角和 3D 模型（用户后续确认）。
- 首发 PC；一万指可战斗敌人，不含子弹与掉落物；上一轮已交付基础框架和独立性能验证场景。
- 新一轮以 Game 场景制作鼠标移动、自动索敌射击的战斗 Demo；长期技能设计与操作/UI 参考英雄联盟和 DOTA2，具体范围见 game-demo.md。
- 后续开发先追问、对齐功能理解，再修改相关实现；根据新需求同步维护开发文档。

## 当前项目事实

| 项目 | 检查结果 |
| --- | --- |
| Editor | ProjectSettings/ProjectVersion.txt 为 6000.3.18f1，即 Unity 6.3 |
| URP | 17.3.0；Forward+ 与全局 GraphicsSettings 已绑定，各 Quality 继承全局设置；基础构建与 Player 初始化已验证 |
| ECS | Entities 1.4.8 + Entities Graphics 1.4.21；基础框架、独立压测及 Game Demo 战斗系统已加入 |
| 现有代码 | Assets/Scripts/ProjectBase 与 Assets/_Scripts 两套 MonoBehaviour/管理工具 |
| 场景 | Main、SelectScene、Game 均在构建列表；Demo 直接打开 Game，独立 Demo 构建只包含 Game |
| 版本管理 | 当前目录不是 Git 仓库；修改现有配置前需要保留可恢复副本 |

## 阅读顺序

1. [需求与验收](requirements.md)：确认范围、追问清单、变更记录。
2. [架构与迁移](architecture.md)：渲染、ECS 边界、迁移步骤。
3. [性能验收](performance.md)：同屏数量定义、测量方法和报告格式。
4. [压测场景使用说明](benchmark-guide.md)：菜单、参数、输出和测量边界。
5. [本轮验证记录](validation-2026-09-22.md)：编译、构建、短时运行证据及未验证项。
6. [Game 场景 Demo](game-demo.md)：已确认玩法、操作说明、参数与验收条件。
7. [Game Demo 验证记录](validation-game-demo-2026-09-22.md)：战斗逻辑、Player 集成检查和画面验证。
8. [架构规范与编辑器说明](architecture-guidelines.md)：胶水层、注释、复用边界、插件及待确认的 Demo 拆分方案。
9. [编辑器工具验证记录](validation-architecture-editor-2026-09-22.md)：skill 校验、包解析、构建与 Player 回归。
10. [Game 场景维护说明](scene-authoring.md)：场景节点、布局修改入口、动态属性、迁移与验证。
11. [技能指示器 Demo](skill-indicator-demo.md)：独立场景、演示素材及交互确认进度。对照图库在 Assets/Art/技能指示器ui，尚未接入场景。
12. [迷雾与视野 Demo](fog-of-war-demo.md)：独立双阵营场景、指定敌人视野预制体、万级显隐目标与验证。
13. [视野系统调研](fog-of-war-research.md)：Dota/RTS 参考资料、规则边界与性能设计。

## 文档维护规则

每次新需求先更新 requirements 中的描述和状态。只读调查、问题整理和文档草案可以先做；影响实现的未决问题必须得到回答后才能修改对应代码、场景或设置。回答已覆盖的问题不重复追问。

实现完成后同步更新架构、验收结果和需求状态；未运行的验证明确写「未验证」。遇到与旧需求冲突的新要求，记录差异并确认取舍，不静默覆盖。性能目标不得写成实测成绩。

## 技能架构基础演示

[使用与配置](skill-architecture-demo.md)：独立 SkillArchitectureDemo，三个主动技能、两种被动、共享 Buff 管线和万目标验证。设计依据见 [架构调研与批准方案](skill-architecture-proposal.md)。
