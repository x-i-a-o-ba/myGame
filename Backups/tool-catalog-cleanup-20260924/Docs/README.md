# 万级同屏肉鸽开发文档

更新日期：2026-09-24。当前技能架构入口为 [组合技能与 Buff 基础架构](ability-foundation.md)。旧 Demo 验证记录只说明当时版本。

## 已确认方向

- 从 Unity 2021 迁移到 Unity 6 系列。
- 使用 URP 渲染管线和 ECS 相关架构。
- 开发同屏一万以上的肉鸽游戏。
- 使用 3D 俯视角和 3D 模型（用户后续确认）。
- 首发 PC；一万指可战斗敌人，不含子弹与掉落物；上一轮已交付基础框架和独立性能验证场景。
- Game 是当前战斗场景：鼠标移动、自动索敌射击，Q/W/E/R 主动技能；技能操作/UI 参考英雄联盟和 DOTA2。
- 用户最新要求：以可选英雄、组合技能及通用 Buff 为底层；允许暂缓具体功能，优先架构。配置采用 ScriptableObject，技能由目标/形状/投递/多段/效果组合，不能按 QWER 固定类。E 为目标点范围伤害；Game 视野继续关闭。
- 编辑器配置的字段、枚举、提示、菜单及 Project 配置资源文件名能用中文清楚表达时优先中文；程序稳定 ID、序列化字段名和资源 GUID 保持不变。技能与 Buff 的当前中文 Inspector 和资源命名见 [组合技能架构](ability-foundation.md)。
- 后续开发由用户给大方向，AI 团队自主补齐普通玩法、数值和实现细节，主 AI 裁决并落实功能；根据新需求同步维护开发文档。仅真正无法推断或不可逆的事项再问用户。

## 当前项目事实

| 项目 | 检查结果 |
| --- | --- |
| Editor | ProjectSettings/ProjectVersion.txt 为 6000.3.18f1，即 Unity 6.3 |
| URP | 17.3.0；Forward+ 与全局 GraphicsSettings 已绑定，各 Quality 继承全局设置；基础构建与 Player 初始化已验证 |
| ECS | Entities 1.4.8 + Entities Graphics 1.4.21；基础框架、独立压测及 Game 战斗系统已加入 |
| 现有代码 | Assets/Scripts/ProjectBase 与 Assets/_Scripts 两套 MonoBehaviour/管理工具 |
| 场景 | Game.unity 绑定 HeroDefinition、SkillCatalog 和按配置工作的指示器；enemyCount=3000、safeSpawnRadius=30。Main、SelectScene及独立历史场景保留；完整选人UI尚未接入 |
| 版本管理 | 当前目录不是 Git 仓库；修改现有配置前需要保留可恢复副本 |

当前 Game 使用 `Roguelike.Abilities.Core`：旧固定技能类及 QWER 状态已移除，共享核心已实际接入。最新验证以 [基础架构记录](ability-foundation.md) 为准。`build4.log/player3.log/Smoke3` 属于上一版固定技能实现，不能证明本轮重构通过。万敌完整战斗性能仍未验收。

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
17. [技能配置工作台与分类设计器](skill-authoring-workbench.md)：独立窗口、六种模板、效果/阶段组合、Unity 单位范围句柄、三维抛物线投递与共享预览、保存和技能库登记。

## 文档维护规则

每次新需求记录用户方向、现有事实和 AI 自主作出的规则/数值/技术决定，再实施并更新 requirements 中的描述与状态。普通未定细节由策划及相关代理讨论、主 AI 裁决，不等待用户逐项确认；真正阻塞项按项目 skill 的边界提问，已有回答不重复追问。

实现完成后执行与改动相称的验证并返工，同步更新架构、验证证据和需求状态；未运行的检查明确写「未验证」及具体原因，不默认交给用户测试。遇到与旧需求冲突的新要求，记录差异、主 AI 裁决及依据，不静默覆盖或冒充用户确认。性能目标不得写成实测成绩。

## 技能架构基础演示

[使用与配置](skill-architecture-demo.md)：独立 SkillArchitectureDemo，三个主动技能、两种被动、共享 Buff 管线和万目标验证。设计依据见 [架构调研与批准方案](skill-architecture-proposal.md)。

## 统一场景与 UI 框架

[场景节点与 UI 使用规范](scene-ui-framework.md)：覆盖全部 7 个场景，单根 Canvas/UIManager、面板 Prefab、场景事件、集中资源文本清单与按需加载。当前节点和面板修改入口以此文档为准。

## 2026-09-24 组合架构验证

最终Unity编译、41项新规则、23项旧核心回归、Windows构建及3000敌人Player检查通过；主AI核对URP离屏指示器/HUD截图。证据已保存到 Logs/AbilityFoundation，详见 ability-foundation.md。未进行万敌完整战斗性能测量。


