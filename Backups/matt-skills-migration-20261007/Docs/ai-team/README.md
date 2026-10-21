当前状态补充（2026-09-24）：Game视野已关闭，正式组合技能复用Abilities/Core；当前来源见 Docs/ability-foundation.md，以下旧Fog接入说明不代表当前启用。

# Codex 游戏开发 AI 团队：自主实施流程

状态：**默认使用 Codex 原生 `myGame` 项目任务；独立[网页工作台](../../Tools/AiTeamDashboard/README.md)仅保留为可选原型。** 更新：2026-09-23。用户给大方向，团队自主决定普通玩法与实现细节，主 AI 裁决并实际实施功能、安排验证和返工，在同一任务里验收和汇报。测试代理按任务需要加入。旧的只读试运行是历史阶段，不限制后续功能实施。完整开发全流程、专业人士案例与来源见 [调研及操作手册](../ai-multi-agent-game-dev-workflow-draft.md)。

## 一句话理解

**你在 `myGame` 的 Codex 主任务中给方向，主 AI 按需派工，团队定方案、实现并验证功能；主 AI 验收后向你汇报。**子代理活动可在原生界面逐个展开查看。角色文件是可重复使用的岗位说明，不是常驻 AI；小任务由主 AI 直接处理。[Codex 官方子代理文档](https://learn.chatgpt.com/docs/agent-configuration/subagents)支持项目级自定义角色与结果汇总，也指出子代理会增加 token 消耗，写入并行要特别谨慎。

~~~mermaid
flowchart TD
  U[人：给大方向] --> M[主 AI：拆解与派工]
  M --> D[game_designer：完整规则与默认数值]
  M --> T[unity_architect：接口与功能实现]
  M --> A[art_director：视觉方案与分配资源]
  M --> P[performance_analyst：性能预算与测量]
  D --> R[事实、方案与分歧]
  T --> R
  A --> R
  P --> R
  R --> J[主 AI：裁决与集成]
  J --> V[实现者／主 AI／按需 QA：验证并返工]
  V --> C[主 AI：验收和实现汇报]
  C --> U2[人：查看结果并反馈]
~~~

一次最多启用三个子代理，不必所有角色全上。通常按任务调用策划、技术、美术；性能代理审查负载与技术预算，必要时优化独占代码并测量适用的负载。测试任务可由实现者、主 AI 或按需派出的 QA/测试代理负责。代理可以给另一个代理提出问题，由主 AI 转交必要事实并定案，不靠多数票。小问题由主 AI 直接处理。[项目配置](../../.codex/config.toml)的并发上限为 3，指主 AI 之外同时打开的子代理数。

## 角色、输入与输出

| 角色 | 什么时候派 | 给它什么 | 必须交回什么 |
| --- | --- | --- | --- |
| [game_designer](../../.codex/agents/game_designer.toml) | 新玩法、规则冲突、数值与成长循环 | 用户方向、需求文档、当前行为 | 可直接实现的完整规则、默认数值、边界处理、选择依据与玩家可观察目标；可写独占的设计文档 |
| [unity_architect](../../.codex/agents/unity_architect.toml) | ECS/URP/输入/UI/资源调用链与功能实现 | 策划规则、相关模块、包版本、架构文档 | 文件与符号证据、接口与数据所有权、分配路径中的实际实现、风险与未验证项 |
| [art_director](../../.codex/agents/art_director.toml) | 新敌人/场景/技能的视觉语言与资源制作 | 玩法目标、相机/平台、现有素材 | 风格和辨识规则、资源规格；受派工时制作分配的资产，并说明来源和授权情况 |
| [performance_analyst](../../.codex/agents/performance_analyst.toml) | 万级负载与渲染预算评审、独占代码优化及适用的测量 | [性能目标](../performance.md)、系统代码 | 预算假设、潜在瓶颈、已分配的优化文件、测量条件与结果；未测部分明确标记 |
| [qa_tester](../../.codex/agents/qa_tester.toml) | 改动涉及多系统、运行回归或需要独立复测时 | 可观察目标、场景、步骤与已实现文件 | 已执行步骤、环境、日志、缺陷复现与复测结果；未执行项说明原因 |

主 AI 本身是制片与集成协调：确定本次范围、派发不重叠的问题与文件、传递新事实、核对交接、裁决分歧、安排返工和最终汇报。UI、音频、发行等岗位先按 [完整手册的角色表](../ai-multi-agent-game-dev-workflow-draft.md)写成任务卡；只有形成反复出现的独立工作后再新增角色文件。

## 共享事实与交接

1. **给方向就启动。**主 AI 使用[任务卡模板](task-card-template.md)或在聊天中记录用户方向、已有确认、代码事实、范围、文件所有权和玩家可见目标。小任务可省略正式任务卡；不把填写模板变成开工门槛。
2. **策划补齐规则。**策划以现有项目和用户方向为约束，独立设计输入、目标、状态、反馈、失败/边界和默认数值，写明选择依据。技术、美术、性能角色可以指出约束或提出替代方案。
3. **主 AI 裁决并派写入。**主 AI 汇总分歧，按玩家体验、现有架构、可维护性和资源成本决定方案，不靠代理多数票，也不把 AI 决定写成“用户确认”。只有目标无法可靠推断、方案违反明确约束或涉及不可逆数据损失、外部发布/付费时才集中向用户提问；其他问题边做边记录取舍。
4. **实际实现。**每个写入者只修改派工指定的文件或独立模块；共享场景、Prefab、资源清单、Packages、ProjectSettings 由主 AI 指定唯一写入者并串行处理。子代理按“用户确认 / 检查事实 / AI 决定及依据 / 改动文件 / 验证证据与未验证原因”交接，事实附文件路径、符号和行号。主 AI 把关键新事实转交受影响的代理，必要时组织一轮短讨论。
5. **验证、返工和汇报。**主 AI 按改动和风险安排静态检查、Unity 编译、Editor/Player 运行、具体游戏功能验证与必要的性能测量；可按需派测试代理。Editor、构建和测试占用同一项目时由主 AI 排队。发现问题先修复并重测，再核对实现范围、引用、代理结论和文档。最终报告实际实现、关键取舍、已执行的检查及结果、剩余限制。无法运行的检查说明具体阻塞和替代证据，不以“等用户测试”结束任务。

任务状态：**方向 → 方案与默认值 → 主 AI 裁决 → 已派工 → 实现中 → AI 验证/返工 → 主 AI 验收 → 完成并汇报**。只有真正阻塞时进入“待用户回答”；每张卡都要能回答当前谁负责下一步。

## 依赖、并发和文件所有权

真实实现时，主 AI 先定跨模块接口和每条路径的唯一写入者，再安排代码与资产制作。当前项目**不是 Git 仓库**，不能直接用 Codex Worktree；子代理共享工作目录，不能同时修改同一文件。修改共享场景、Prefab 或全局配置前保留可恢复状态，串行写入并检查对应的 `.meta`/GUID。[Codex Worktrees](https://learn.chatgpt.com/docs/environments/git-worktrees)

| 项目路径或操作 | 协同规则 |
| --- | --- |
| `Assets/Game/Runtime/{Combat,Benchmark,Simulation,Authoring}`、独立 `Abilities/FogOfWar/SkillIndicatorDemo` | 调查可并行；实现时按模块和具体文件分配。公共数据接口、`.asmdef` 先确定后串行修改。 |
| `Assets/Game/Foundation/**`、`Assets/Resources/ProjectResourceCatalog.json` | 公共依赖与资源入口；主 AI 指定唯一集成者。 |
| `Assets/Scenes/*.unity`、`Assets/Game/Scenes/*.unity`、共用 Canvas/EventSystem 与面板 Prefab | 场景、Prefab 及其 `.meta` 不能让两个写入者同时改。 |
| `Assets/Settings/Rendering/**`、`Packages/**`、`ProjectSettings/**` | 全项目配置锁；经明确需求和恢复方案后由唯一集成者修改。 |
| `Tools > Roguelike > Migrate All Scenes`、资源清单刷新、Build 菜单、同一路径的 Unity Editor | 会保存多个场景/资源或占用项目；由主 AI 排队验证并保留可恢复状态。 |

上述边界来自当前 [架构](../architecture.md)与[场景 UI 框架](../scene-ui-framework.md)及项目代码的盘点。Game 现在复用独立迷雾 Demo 的 `FogSolver`/`FogVisualMask` 核心，独立场景与调试 UI 仍单独维护；具体接入见[寒冰技能与迷雾说明](../ashe-game-integration.md)。资源要按 Unity 的 `.meta`/GUID 一起审查，不能盲目拼接场景 YAML。

## 在 Codex 桌面端使用

1. 在 Codex 左侧打开 **`myGame` 项目**，在这个项目中打开或新建 **Local 主任务**。工作目录应为 `D:\UnityProject\myGame`。不需要启动本地网页服务。
2. 直接告诉主 AI 目标和约束，例如“在 Game 场景实现一种寒冰技能，你们自行设计规则和默认数值、讨论冲突，完成代码、资源和验证后向我汇报”。不需要复制一大段派工模板；项目 [AGENTS.md](../../AGENTS.md) 已说明分工。[项目配置](../../.codex/config.toml)已把可信项目的新任务默认设为 `workspace-write`；如果某个任务被界面或启动参数明确改成只读，该任务仍只能调查和起草，需在任务权限处改为可写。
3. 运行时展开主任务中的 **Subagents／子代理活动**，点开单个子代理任务看工作过程和交付。回到主任务看主 AI 的裁决、实现与验证汇总。[OpenAI 官方说明](https://learn.chatgpt.com/docs/agent-configuration/subagents)确认桌面端会呈现每个子代理任务；CLI 的 `/agent` 只用于终端版。

原生界面将代理对话保留在各自任务中，由主 AI 在主聊天转交事实并统一汇报；它没有把所有代理合成一个可回复群聊的官方功能。需要简洁总览时，让主 AI 在聊天里给出分工表和状态即可。

五个项目角色位于 [`.codex/agents`](../../.codex/agents/game_designer.toml)，并发上限见[项目配置](../../.codex/config.toml)。本机 CLI 曾在此非 Git 项目成功调用 `game_designer`；桌面端是否实际加载这些自定义 TOML 角色仍待按下一次明确点名的派工核验。角色文件为可写任务开放工作区写入，但主任务若选只读，角色配置不能越过主任务权限。[官方角色与权限说明](https://learn.chatgpt.com/docs/agent-configuration/subagents)

此前使用 QA 的自动索敌试运行是[历史记录](pilot-2026-09-23.md)。以后 QA/测试代理按改动风险和并发情况派出，不成为每次实现的必经岗位。

## 从方向到实际交付

主 AI 收到方向后直接推进：策划规则与默认数值 → 技术/美术意见 → 主 AI 裁决 → 分路径实现代码、场景与资源 → AI 执行适用验证并返工 → 主 AI 验收 → 同步需求与架构文档 → 汇报实际改动、验证证据与剩余限制。没有反复等用户批准的阶段；真正不可代选的阻塞才问用户。UI、音频与发行按具体任务加入。目标“万级可战斗敌人”需要在目标硬件的完整 Player 战斗中测量后才能宣称达标；[性能目标](../performance.md)不能当成已完成结果。

