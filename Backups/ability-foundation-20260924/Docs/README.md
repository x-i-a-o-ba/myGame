# 万级同屏肉鸽开发文档

更新日期：2026-09-23。本文档区分「用户确认」「检查事实」「AI 决定」「验证状态」；旧 Demo 验证记录只说明当时版本。

## 已确认方向

- 从 Unity 2021 迁移到 Unity 6 系列。
- 使用 URP 渲染管线和 ECS 相关架构。
- 开发同屏一万以上的肉鸽游戏。
- 使用 3D 俯视角和 3D 模型（用户后续确认）。
- 首发 PC；一万指可战斗敌人，不含子弹与掉落物；上一轮已交付基础框架和独立性能验证场景。
- Game 是当前战斗场景：鼠标移动、自动索敌射击，Q/W/E/R 主动技能；技能操作/UI 参考英雄联盟和 DOTA2。
- 用户最新要求：Game 不再称为 Demo；四个技能各用 ScriptableObject 配置；接入 W/E/R 施放指示器；E 改为目标点范围伤害；Game 暂停全部视野、昼夜和隐藏机制，相关旧代码集中封存，独立 FogOfWarDemo 保持独立。
- 后续开发由用户给大方向，AI 团队自主补齐普通玩法、数值和实现细节，主 AI 裁决并落实功能；根据新需求同步维护开发文档。仅真正无法推断或不可逆的事项再问用户。

## 当前项目事实

| 项目 | 检查结果 |
| --- | --- |
| Editor | ProjectSettings/ProjectVersion.txt 为 6000.3.18f1，即 Unity 6.3 |
| URP | 17.3.0；Forward+ 与全局 GraphicsSettings 已绑定，各 Quality 继承全局设置；基础构建与 Player 初始化已验证 |
| ECS | Entities 1.4.8 + Entities Graphics 1.4.21；基础框架、独立压测及 Game 战斗系统已加入 |
| 现有代码 | Assets/Scripts/ProjectBase 与 Assets/_Scripts 两套 MonoBehaviour/管理工具 |
| 场景 | Main、SelectScene、Game 均在构建列表；独立演示场景保留。Game.unity 已绑定四份技能配置、W/E/R 指示器与 HUD 技能按钮，enemyCount 为 3000、safeSpawnRadius 为 30；不以旧 10000 Player 记录替代当前验证 |
| 版本管理 | 当前目录不是 Git 仓库；修改现有配置前需要保留可恢复副本 |

Game 本轮在隔离副本完成 Unity 场景迁移、最终版编译、CombatValidation、Windows Player 构建与 3000 敌人 Player 功能冒烟；日志和报告位于 `D:\UnityProject\myGame_validation_20260923` 的 `migration2.log`、`build4.log`、`player3.log` 和 `Smoke3/result.json`。生成的场景资产已复制回主项目；右键按住取消瞄准后的移动抑制已静态修复，但未做真实鼠标输入测试。主项目 Editor 未单独运行，Editor 交互视觉及一万敌人完整战斗性能尚未验证。详情见[现行接入说明](ashe-game-integration.md)。

## 阅读顺序

1. [需求与验收](requirements.md)：确认范围、追问清单、变更记录。
2. [架构与迁移](architecture.md)：渲染、ECS 边界、迁移步骤。
3. [性能验收](performance.md)：同屏数量定义、测量方法和报告格式。
4. [压测场景使用说明](benchmark-guide.md)：菜单、参数、输出和测量边界。
5. [本轮验证记录](validation-2026-09-22.md)：编译、构建、短时运行证据及未验证项。
6. [Game 场景战斗](game-combat.md)：当前操作、场景默认值与验证状态。
7. [Game 旧版验证记录](validation-game-demo-2026-09-22.md)：2026-09-22 战斗逻辑、Player 集成与画面；不覆盖当前改造。旧指南路径 `game-demo.md` 保留历史兼容入口。
8. [架构规范与编辑器说明](architecture-guidelines.md)：胶水层、注释、复用边界及现行 Game 职责。
9. [编辑器工具验证记录](validation-architecture-editor-2026-09-22.md)：skill 校验、包解析、构建与 Player 回归。
10. [Game 场景维护说明](scene-authoring.md)：场景节点、布局修改入口、动态属性、迁移与验证。
11. [技能指示器独立演示](skill-indicator-demo.md)：演示场景和历史验证；Game 接入状态以第 16 项为准。
12. [迷雾与视野 Demo](fog-of-war-demo.md)：独立双阵营场景、指定敌人视野预制体、万级显隐目标与验证。
13. [视野系统调研](fog-of-war-research.md)：Dota/RTS 参考资料、规则边界与性能设计。
14. [AI 主控与多代理游戏开发调研及操作手册（草案）](ai-multi-agent-game-dev-workflow-draft.md)：行业案例、角色分工和全流程研究；当前可执行规则以 AI 团队手册为准。
15. [Codex 原生 AI 团队操作手册](ai-team/README.md)：默认在 `myGame` 项目任务中给主 AI 方向，团队讨论后由主 AI 裁决、实现、验证并汇报功能。
16. [Game 寒冰技能与指示器](ashe-game-integration.md)：四份技能配置、W/E/R 瞄准、E 范围伤害、Game 视野封存及本轮验证状态。

## 文档维护规则

每次新需求记录用户方向、现有事实和 AI 自主作出的规则/数值/技术决定，再实施并更新 requirements 中的描述与状态。普通未定细节由策划及相关代理讨论、主 AI 裁决，不等待用户逐项确认；真正阻塞项按项目 skill 的边界提问，已有回答不重复追问。

实现完成后执行与改动相称的验证并返工，同步更新架构、验证证据和需求状态；未运行的检查明确写「未验证」及具体原因，不默认交给用户测试。遇到与旧需求冲突的新要求，记录差异、主 AI 裁决及依据，不静默覆盖或冒充用户确认。性能目标不得写成实测成绩。

## 技能架构基础演示

[使用与配置](skill-architecture-demo.md)：独立 SkillArchitectureDemo，三个主动技能、两种被动、共享 Buff 管线和万目标验证。设计依据见 [架构调研与批准方案](skill-architecture-proposal.md)。

## 统一场景与 UI 框架

[场景节点与 UI 使用规范](scene-ui-framework.md)：覆盖全部 7 个场景，单根 Canvas/UIManager、面板 Prefab、场景事件、集中资源文本清单与按需加载。当前节点和面板修改入口以此文档为准。
