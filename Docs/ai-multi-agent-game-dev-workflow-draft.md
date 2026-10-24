# AI 主控与专职代理开发游戏：调研及操作手册（历史）

归档说明（2026-10-21 更新）：此文记录早期多代理方案与调研，不作为当前执行规则。当前约定见 [AGENTS.md](../AGENTS.md)；AI 按任务自主选择工具与协作方式，下文操作步骤仅用于理解历史方案。

更新：2026-09-23。适用范围：本项目的 Unity 6.3、URP、ECS、PC 肉鸽开发。本文是调研和流程建议；项目级 Codex 子代理当前配置见 [AI 团队架构与派工手册](ai-team/README.md)。**用户给出大方向后，主 AI 自主组织策划、实现和验证；由 AI 对实现的功能选择适用检查、修复问题并复验。QA 代理按需使用，用户可另外试玩并反馈体验。**本文不表示已安装外部工具或已完成性能目标。外部工具的功能、条款和价格在实际启用前要重新核对。

## 1. 先说结论

可行的组织形式是：**人担任游戏总监和发行责任人；一个主 AI 担任制片与集成协调；按任务调用策划、程序、美术、音频、QA、性能等专职代理。**“角色”是职责、输入、交付物和验收标准的组合，可以由同一个模型在不同任务中扮演，不要求购买十几个模型或让十几个代理一直运行。

主 AI 应维护一份共同的项目事实、任务依赖和风险台账；专职代理只做边界清楚的工作。普通玩法、审美和手感细节由团队讨论、主 AI 裁决并落实；用户提供方向，也可试玩后提出调整。权利争议、不可逆的数据损失、外部发布与付费等必须由用户决定。跨职能讨论可以并行，依赖同一场景或同一接口的实际修改必须排序。这里的“共享记忆”首先是版本化文档和构建证据，不假定代理之间天然共享数据库。

这是一套提高可控性的生产流程，不是“一句话自动生成完整商业游戏”的承诺。研究型多代理在可并行搜索中有优势，Anthropic 也明确指出强依赖的编码任务较难并行协调，且其系统相对普通聊天消耗更多 token；这不能直接换算为本项目的产能。[Anthropic 多代理研发复盘](https://www.anthropic.com/engineering/multi-agent-research-system)。

## 2. 网上调研：专业人士做到了什么

| 来源与证据类型 | 观察 | 对本项目的启示 |
| --- | --- | --- |
| Keywords Studios 游戏 AI 负责人 Stephen Peacock，[Project AVA 复盘](https://www.keywordsstudios.com/en/project-ava-exploring-genais-promising-future-in-game-production/)（2024，工作室研发实验） | 2023 年六个月、跨七家工作室、20 多名参与者尝试制作“可发行”的小型游戏，AI 最终没有完成显著比例的代码和计划资产；概念迭代、对白初稿和熟悉引擎比较有用。这是当时工具能力的记录。 | 不把早期原型与可发布质量画等号；每个专业产物须有可核对的验收证据。 |
| Electric Square 工作室负责人 Serkan Hassan，[Project KARA 复盘](https://www.keywordsstudios.com/en/about-us/news-events/news/lessons-from-project-kara/)（2025，真实游戏上的研发实验） | 按灯光、建模、动画等专业工作流并行，逐项对照传统流程，记录可用结果、清理工时、成本与质量；报告个别灯光配置减少 78%、碎片模型 8 小时变 2 小时，动画仍需人工修整。 | 先在一个真实资产链条上试，再决定是否自动化；这些数字不能外推到本项目。 |
| Wharton Generative AI Labs 资深研究员 Zimran Ahmed，[20 位行业人员访谈报告](https://gail.wharton.upenn.edu/wp-content/uploads/2026/04/Beyond-Copy-and-Paste-How-Game-Studios-Are-Reorganizing-Around-AI.pdf)（2026，定性研究） | 跨团队自动化常卡在没写下来的制作经验、交接和职责边界。受访的小团队把 GDD、操作手册和修正经验写成代理可读的 Markdown；作者另外建议以纵向可玩切片做端到端试点。速度收益主要来自受访者自报。 | 先把项目真实规则、接口和完成定义写清；不要直接复制匿名案例的速度结论。 |
| 独立开发者兼 QA 从业者 Julia Pottinger，[多代理工作流复盘](https://juliapottinger.com/how-i-run-three-ai-coding-agents-like-a-studio/)与[发行日志](https://juliapottinger.com/how-i-built-a-mobile-game-with-ai-agents/)（2026，本人记录） | 她把 ChatGPT 用于设计、Cursor 用于主要开发、Codex 用于仓库级任务、Claude 用于审查；规则文件、先计划后实现、真机 QA 和本人决定发布贯穿流程。作品 Tropic Tumble 可在 [Google Play](https://play.google.com/store/apps/details?id=com.jpott77.tropictumble) 和 [App Store](https://apps.apple.com/us/app/tropic-tumble/id6776932781) 查到；开发分工和代码占比仍是作者自述。 | 实际可照搬的是书面规则、计划、审查、设备验证和发布门槛，不是特定品牌组合。 |
| [GDC 2026 行业调查](https://gdconf.com/article/gdc-2026-state-of-the-game-industry-reveals-impact-of-layoffs-generative-ai-and-more/)（2300 多名受访者） | 36% 在工作中使用生成式 AI；**在使用者中**，研究/构思占 81%、编码辅助占 47%、原型占 35%；52% 认为其对行业有负面影响。调查覆盖不同职业，不能把使用率当成支持率。 | 先用 AI 做研究、规格和辅助实现，并认真对待美术质量及从业者关切。 |
| [GameDevBench v2 论文](https://arxiv.org/abs/2602.11103)（2026-06-30，333 个任务） | 最佳代理与方法完成 53.8% 的任务；游戏玩法任务平均 51.4%，2D 图形任务 33.0%；视觉反馈改善结果。这是任务级基准，不是整款游戏的成功率。 | 图形、动画和 UI 必须有视觉反馈；文字自报“完成”不算验收。 |
| [METR 早期 2025 随机试验](https://metr.org/blog/2025-07-10-early-2025-ai-experienced-os-dev-study/)（成熟开源仓库，16 名开发者、246 项任务） | 当时的 AI 工具使这些经验开发者完成任务平均慢 19%。[2026 年跟进](https://metr.org/blog/2026-02-24-uplift-update/)看到可能加速的迹象，但研究者明确说样本选择和多代理计时问题使当前加速幅度不可靠。 | 用本项目实际的总工时、返工率和缺陷率评价流程，不把旧结果或后续初步趋势直接外推。 |

GDC 2026 的[腾讯游戏设计代理议程](https://schedule.gdconf.com/session/the-ai-design-stack-agents-3d-generation-and-beyond-presented-by-tencent-games-ai/917957)称会演示从世界观和约束拆出任务、经济和内容简报，再把 3D 产物交技术美术审查；[IntelliScene 议程](https://schedule.gdconf.com/session/intelliscene-multi-agent-for-reasoning-driven-game-scene-layout-presented-by-tencent-games-ai/917891)聚焦多代理场景摆放。这些议程只表明局部管线的尝试，不能当作“整款游戏无人开发”的证据。育碧 [DocGPT 的 GDC 议程](https://schedule.gdconf.com/session/the-future-of-game-knowledge-search-less-ship-more/914982)称项目知识层可帮助测试员、新开发者与管理者查找资料与生成测试用例；这类知识入口对长期开发很重要。上述会议页面是主讲方介绍，并非独立效果审计。

### 从证据得出的工作原则

1. **先书面共识，再分工。**需求、资产规范、代码边界和验收标准要能被所有代理读到。
2. **先纵切片，后量产。**尽早得到玩家能实际游玩的一个完整循环，再扩充敌人、技能、场景与内容。
3. **并行的是独立工作，不是同一文件。**策划规格、素材候选、测试方案可同时产出；共用场景、Prefab、程序集接口和 ProjectSettings 设唯一写入者。
4. **演示、Editor 测试、Player 测试、正式性能是不同证据。**缺哪一级就标明未验证。
5. **角色按需求动态组合。**小任务由一个代理完成；复杂任务先让 2–4 个代理分别调查或做互不冲突的产物，再统一集成。代理数量不是质量指标。

## 3. 本项目现状与落地边界

检查事实来自 [项目文档入口](README.md)、[需求](requirements.md)、[架构](architecture.md)、[性能验收](performance.md)与本地只读核对：

| 项目 | 当前事实 | 工作流影响 |
| --- | --- | --- |
| 编辑器与包 | Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8、Entities Graphics 1.4.21。 | 所有建议按这些版本和现有代码核对，不把“Unity 6”当成单一 API 版本。 |
| 产品方向 | PC、3D 俯视角与 3D 模型；目标是 1 万以上可战斗敌人。 | 角色分工须覆盖 3D/技术美术、ECS 战斗、Player 性能和 PC 发布。 |
| 已有实现 | Game Demo、独立压测、技能/迷雾 Demo；部分技能与迷雾已接入 Game，状态以 [接入记录](ashe-game-integration.md) 为准。 | 不假设所有 Demo 已连成完整游戏；先评估现有边界，再规划正式纵切片。 |
| 版本管理 | 当前目录**不是 Git 仓库**；根目录尚无可用的并行 Git 工作树。现有 Backups、Logs、Builds 等目录不等于版本管理。 | 真正多写入代理之前，先备份、建立版本基线并明确资源归属。此项尚未执行。 |
| 测试 | 有模块 Editor 验证菜单、Player smoke 与历史验证记录；未发现项目自己的独立测试程序集或 CI 配置。 | 第一阶段可复用现有检查，后续才考虑自动 CI。历史通过不等于新改动已通过。 |
| Unity 项目配置 | ProjectSettings/EditorSettings.asset 和 VersionControlSettings.asset 已核对为 Visible Meta Files；前者记录 m_SerializationMode: 2，Editor 界面显示值仍可再核对。 | 提交资产时连同对应 .meta；不要重生 GUID、手改 Library 或盲目合并场景 YAML。 |

项目内 [需求对齐 skill](../Tools/Skills/unity-requirements-first/SKILL.md) 和 AGENTS.md 是执行依据：功能、架构、渲染和性能修改前先读规则及项目文档；普通玩法、体验和验收细节由策划提出完整方案，相关角色讨论，主 AI 裁决并实施。只有目标无法可靠推断、会违反用户明确约束，或涉及不可逆的数据损失、外部发布/付费等必须由用户决定的事项才提问。已确认事项继续有效，不重复索取许可。每次实现后同步需求、架构和验证状态。

## 4. 角色与交接契约

| 角色 | 主责与交付物 | 决策与验收边界 |
| --- | --- | --- |
| 人类总监 / 项目所有者 | 提供玩法方向、目标玩家、审美偏好与范围；可试玩并给体验反馈。 | 权利争议、不可逆的数据损失、外部发布与付费由人决定。 |
| 主 AI：制片兼集成协调 | 读取项目事实；把目标拆为有依赖的任务卡；挑选专职代理、分配文件归属、追踪风险、集成产物、汇总证据。 | 不把建议写成用户确认；不凭子代理自述宣布完成。 |
| 策划 / 数值 / 叙事代理 | 玩家行为、规则边界、状态表、默认数值、对局循环、任务与文本方案；定义可测试例子。 | 普通玩法与节奏由主 AI 审查后实施；授权或公开发布另按用户决策。 |
| 技术架构 / ECS 代理 | 系统边界、数据所有权、生命周期、Burst/Jobs 依赖、接口和性能预算；做架构评审。 | 不能未经范围确认重写旧系统或把压测当正式战斗。 |
| 玩法程序 / 工具代理 | 在获分配模块实现和自测；交变更清单、重现方法、日志与文档更新。 | 按用户方向及主 AI 已裁决方案实施；跨模块改动要先找真实所有者。 |
| 美术总监 / 概念代理 | 风格板、形状语言、色彩、敌我识别、资产清单与每类视觉验收规则。 | 候选图不是生产模型；最终风格和版权来源由人审。 |
| 3D / 技术美术 / 动画 / VFX 代理 | 模型、材质、动画、特效、LOD/实例化适配建议；交源文件、导入参数、画面与性能证据。 | 不未经测量承诺万级渲染；不擅改共用 Shader、场景和 Prefab。 |
| UI/UX / 可访问性代理 | 流程图、线框、面板 Prefab、视觉状态与键鼠可用性检查；按现有场景事件规范接入。 | 关键操作手感和易读性由人实机看。 |
| 音频 / 本地化代理 | 音效清单、情绪与混音规范、语言表、导入和授权记录。 | 不假设 AI 配音或音乐可任意商用；真人声音复制须核对授权与适用合同。 |
| QA / 性能代理（按需） | 风险测试矩阵、Editor/Player 回归、截图/录屏问题单；正式负载、Profiler 与帧时间报告。 | 专职 QA 可选，但实施团队仍须自行验证；性能报告不能用纯模拟结果证明完整战斗。 |
| 构建 / 发布 / 运营代理 | 版本号、构建、渠道清单、商店素材、披露和反馈分类草案。 | 商店提交、公开宣传、付费和发布由人批准。 |

按任务精简团队：例如纯规则 Bug 可由“主 AI + 玩法程序”，复杂新敌人可由“主 AI + 策划 + 程序 + 技术美术 + QA”。每次实现均由 AI 验证；QA 角色随风险与工作量增减。策划和美术可以先并行出方案；程序写入前由主 AI 确定接口和资源规格。

~~~mermaid
flowchart LR
  H[人类总监：大方向与反馈] --> M[主 AI：拆任务、裁决、集成]
  M --> D[策划：规则与验收方案]
  M --> A[架构/程序：接口与实现]
  M --> R[美术/音频/UI：资源与反馈]
  D --> G[主 AI 裁决与接口冻结]
  G --> A
  G --> R
  A --> I[唯一集成者：场景与构建]
  R --> I
  I --> Q[实施团队验证；QA/性能按需协作]
  Q --> M
  M --> H
~~~

### 权限矩阵

- **只读代理**：调研、策划草案、架构评审、代码审查、QA 计划。允许提出补丁建议，不改项目。
- **范围写入代理**：只改任务卡列出的模块与自己的新资源；交完整文件清单和相关 .meta。
- **唯一集成者**：负责共享场景、Prefab、Packages、ProjectSettings、构建配置和跨模块合并。任何同一资源同时被两个代理编辑的计划都先改成顺序执行。
- **用户决策边界**：目标无法可靠推断、违反明确约束、不可逆的数据损失、外部发布或付费等；普通玩法与验收细节由主 AI 裁决。

## 5. 工具链怎么搭

### A. 从当前状态起步

1. **建立可恢复基线。**关闭正在写入的 Unity Editor，检查已有备份，按 [GitHub 的 Unity.gitignore 模板](https://github.com/github/gitignore/blob/main/Unity.gitignore) 建立忽略规则，再审查本项目的 Backups、DemoReports、Logs、Builds 是否应纳入版本库。保存 Assets、Packages、ProjectSettings、Docs、Tools 和资源对应的 .meta。Unity 6.3 文档说明文本序列化有利于场景合并；本项目文件已记录 Visible Meta Files 与 m_SerializationMode: 2，仍可在 Editor 核对显示值。[Unity 6.3 Editor 设置](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-editor-manager)。
2. **本地 Git 基线。**核对 .gitignore 和大文件清单后，才初始化 Git、审查待提交文件并提交一次可恢复的起点。大型二进制资产按实际体积与团队托管方式选择 Git LFS 或 Unity Version Control；不要盲目把 Library、Temp、构建输出或私有素材上传远端。
3. **任务隔离。**有了 Git 基线后，Codex 桌面端的 Worktree 或原生 Git worktree 才可为独立代码任务提供分支与文件隔离；当前项目尚不能直接使用。Codex 官方说明 Worktree 依赖 Git 仓库；**同一任务中的子代理共享文件系统，并不会自动得到工作树**，因此仍需指定文件归属。[Codex Worktrees](https://learn.chatgpt.com/docs/environments/git-worktrees)、[Codex Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents)。
4. **先用已有验证，再补自动化。**现成的 Editor 验证菜单和 Player smoke 已见 [Game Demo](game-demo.md)、[压测指南](benchmark-guide.md)、技能架构 Demo（已于 2026-10-24 删除）、迷雾 Demo（已于 2026-10-24 删除） 与[场景 UI 框架](scene-ui-framework.md)。先把每个任务映射到对应检查；项目自己的 Test Framework 测试和 CI 是后续建设项。
5. **Editor 接入作为可选第二阶段。**Unity AI 助手现有 Beta 功能可读项目上下文与调用 Editor 工具，但本项目尚未安装该包；Unity CLI/Pipeline/MCP 也尚未在本机核实可用。若之后需要 AI 直接操作 Editor，先核对 Unity 6.3 兼容性、Unity Cloud/账户条件、费用、授权和 [Unity 官方代理接入条款](https://unity.com/legal/terms-of-service)，优先走明确授权的官方路径。[Unity AI 介绍](https://unity.com/blog/unity-ai-how-to-get-started)、[Unity CLI](https://docs.unity.com/en-us/unity-cli)、[Unity Pipeline 包](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)。MCP 是 Editor 操作入口，不替代设计、版本管理和测试。

版本管理选型先看资产规模和团队习惯。**Git + 按需 Git LFS**能直接使用 Codex Worktree，适合先搭本地任务隔离；大二进制文件仍需管理托管配额与合并冲突。**Unity Version Control**有针对模型、图片、音频等不可合并资源的 Smart Locks；若单用它，Codex 的 Git Worktree 流程就不直接适用，应改用其分支/锁和单文件所有者。[Codex Worktree 前提](https://learn.chatgpt.com/docs/environments/git-worktrees)、[Unity Version Control Smart Locks](https://docs.unity.com/en-us/unity-version-control/smart-locks)。当前建议先试本地 Git 基线，最终选型由项目所有者根据资产体量、协作人数与托管成本确定。

以下 PowerShell 仅是建立**本地** Git 基线的操作示意，执行前需先保留项目备份、审查版权与待提交文件；当前未执行。模板可能更新，应阅读下载的忽略规则，并针对本项目生成目录补充规则。

~~~powershell
Set-Location 'D:\UnityProject\myGame'
Invoke-WebRequest 'https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore' -OutFile '.gitignore'
Get-Content -LiteralPath '.gitignore'
git init
# 审查 Backups、DemoReports 等本项目目录，决定补充哪些忽略项。
# 按实际大文件体积决定是否先配置 Git LFS。
# 若选择 LFS 且已安装，可按资产审计结果运行示例：
# git lfs install
# git lfs track '*.psd' '*.fbx'
# git add .gitattributes
git add Assets Packages ProjectSettings Docs Tools AGENTS.md .gitignore
git status --short
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
# 确认没有 Library、Temp、Backups、Logs、Builds、私有密钥或不应托管的素材；提交前逐项审查。
git commit -m "Record Unity project baseline"
git worktree add -b feat/task-001 ..\myGame-task-001 HEAD
git worktree list
~~~

工作树交付后先审查、集成并确认已保存任务成果，再从原仓库用 git worktree remove 移除对应的**干净**工作树；不要用强制删除处理仍有未保存改动的工作树。[Git worktree 手册](https://git-scm.com/docs/git-worktree)。每个工作树有自己的 Unity 导入缓存和磁盘开销。Unity 官方指出同一**项目路径**不能同时打开两个 Editor；同一路径的批处理测试和交互 Editor 不要并行。[Unity 6.3 命令行参考](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)。代码研究可分树并行；涉及同一个 Game 场景的修改建议由集成者排队。

若后续确认要让 Codex 通过**官方 Unity CLI**连接 Editor，可按下面的顺序单独试点。Unity CLI 截至 2026-09-23 的官方发布说明仍列 1.0.0-beta.10，并标为实验性工具；本机目前没有在 PATH 中发现 unity 命令，项目 manifest 也没有 com.unity.pipeline，所以以下均是**未执行的可选步骤**。[CLI 安装](https://docs.unity.com/en-us/unity-cli/use-unity-cli)、[发布说明](https://docs.unity.com/en-us/unity-cli/release-notes)、[Pipeline 依赖](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)、[Unity 官方 Codex 集成说明](https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/references/integration-advanced.md)。

~~~powershell
# 先确认当前账户、工具和客户端满足 Unity 最新授权条款。
winget install Unity.CLI
# 安装后重开终端，并先在 Unity Editor 打开目标项目。
unity auth login
unity pipeline install --project-path 'D:\UnityProject\myGame'
# 等 Editor 完成包导入与编译，在目标项目运行时检查可用命令。
unity pipeline list
# dry-run 只预览 MCP 条目，并不展示全部可能的 Codex 沙箱网络配置变更。
unity mcp configure codex --local --project-path 'D:\UnityProject\myGame' --dry-run
~~~

预览 MCP 条目无误且项目已准备好后，才去掉最后一行的 --dry-run；正式配置后再核对 Codex 配置 diff、沙箱网络设置、Packages/manifest.json 与 Unity 的包导入状态。不要把任意第三方 Unity MCP 插件默认当成已获授权：Unity 2026-06-30 条款对 AI agent、CLI 与 MCP 访问 Unity 服务设有 Authorized Agentic Access 条件；最终适用范围应按当时账户、客户端和官方说明核对。[Unity Terms of Service](https://unity.com/legal/terms-of-service)。

### B. 最小目录与事实来源

不需先创建一套庞大新系统。本项目已有 AGENTS.md、Tools/Skills、Docs/requirements.md、Docs/architecture.md、Docs/performance.md 和模块验证指南。主 AI 每次从这些**当前文件**及代码/资源事实出发；在任务卡中记录“用户确认 / 已检查事实 / AI 决定与依据 / 未验证 / 确需用户决策”。大型游戏逐步补充：

- 游戏愿景与 GDD：玩法支柱、目标玩家、核心循环、失败/成长、内容边界。
- 美术与音频规范：视角、轮廓、颜色、材质、特效可读性、音量与资产来源。
- 架构决策记录：模块职责、跨 ECS/Mono 边界、场景和 Prefab 所有权。
- 测试与性能基线：可复现的测试负载、Player 构建、设备、日志和图像。
- 发行台账：版本、渠道要求、素材来源、披露、已知问题。

## 6. 从创意到发行的阶段门

| 阶段 | 主 AI 分派的主要工作 | 必交产物 | 主 AI 验收与用户决策边界 |
| --- | --- | --- | --- |
| 0. 方向与市场 | 策划研究同类玩法、目标人群和差异点；制片估范围与风险；发布代理提出渠道和玩家获取假设。 | 一页愿景、3 个玩法支柱、参考游戏拆解、明确“暂不做”、待验证的受众与分发假设。 | 依用户方向形成内部方案；发布渠道、付费等外部决定交用户，参考仅作分析，不复制受保护素材。 |
| 1. 预制作 | 策划起草 GDD、循环与默认数值；美术起风格板；架构审查现有代码。 | 可玩纵切片定义、任务依赖图、首批资产表、AI 决策记录。 | 主 AI 裁决普通规则与可观察验收条件，确定接口与资源规格后实施。 |
| 2. 原型与纵切片 | 程序做输入、敌人、攻击、反馈和失败/重开等范围内功能；美术交可替换资产；按需安排 QA。 | 一个 Player 构建、一段可玩的完整循环、场景截图/录屏、问题单。 | 实施团队运行适用的功能检查、修复并复验；用户可试玩后给手感和乐趣反馈。 |
| 3. 核心系统 | 架构/程序按用户方向和主 AI 裁决交付战斗、技能、掉落、成长、存档等模块；UI/音频按接口接入。 | 每项需求的实现、资源、适用的 Editor/Player 验证与文档。 | 主 AI 审核证据后集成；对共享场景、资源路径和存档兼容性做回归。 |
| 4. 内容生产 | 策划维护敌人/技能/关卡表；美术、动画、VFX、音频按规范批量制作；工具代理自动化重复导入。 | 内容批次、资产来源台账、源文件/单位/坐标/材质/动画规范、导入设置和视觉/听觉检查表。 | 每批抽样实机检查风格、敌我识别、特效遮挡、Entities Graphics 兼容与性能；LOD、贴图、模型和 VFX 预算由目标场景测量确定。 |
| 5. Alpha/Beta | QA 设计边界、长局、重进、存档兼容、回归和设备测试；性能代理测真实负载；发布代理协助招募目标玩家观察。 | 缺陷台账、可复现构建、设备/分辨率矩阵、Player Profiler 报告、试玩观察和反馈。 | 无阻断问题；目标数值与实测分开；若未来加入存档则验证版本迁移与恢复，检查键鼠可用性和可访问性。 |
| 6. 发行准备 | 发布代理核对平台条款、素材、版本、语言、商店说明、披露、崩溃收集和回滚；按已选渠道准备 Demo、宣传素材、社区与时间表。 | Release Candidate、构建哈希、许可清单、商店资料、已知问题、玩家获取与反馈指标草案。 | 人核对最终游戏和公开承诺，选择渠道并决定提交/发布；开发速度不等于玩家会自然发现游戏。 |
| 7. 上线后 | 主 AI 分流崩溃、玩家反馈和可取得的数据；策划提版本假设；程序/QA 按小批次修复。 | 补丁任务卡、回归证据、版本说明和下一轮计划。 | 主 AI 裁决范围内修复及玩法细节；对外沟通或付费变更由用户决定。 |

“纵切片”是让玩家从进入游戏到得到反馈的真实流程，优先验证是否有趣、是否看得懂、是否可维护；不要求一开始有完整内容量。对本项目，现有 Game Demo 是起点，技能、迷雾和压测等独立 Demo 接入 Game 时先核对现有实现，由主 AI 按用户方向裁决范围。[Game Demo 当前范围](game-demo.md)、技能架构现状（已于 2026-10-24 删除）、迷雾现状（已于 2026-10-24 删除）。

### 各专业交付到下一环节时要写什么

| 交付者 → 接收者 | 最小交接包 |
| --- | --- |
| 策划 → 程序/QA | 一页规则、状态变化表、数值单位和来源、三类正常例子与边界例子、失败行为、玩家可见验收。区分用户确认和 AI 默认决定。 |
| 架构 → 程序/技术美术 | 模块及数据所有者、ECS/Mono 边界、事件/资源接口、生命周期、预期负载和回滚路径。跨模块接口先冻结。 |
| 美术总监 → 3D/技术美术 | 风格图和原创参考来源、视角/屏幕占比、轮廓与敌我识别、尺寸单位、材质与色彩范围、动画/VFX 可读性目标。 |
| 3D/技术美术 → 集成/性能 | 源模型与导出文件、轴向/原点/缩放、贴图通道、材质/Shader、LOD/实例化假设、导入参数、.meta、许可记录，以及 Game 视角截图。面数和贴图预算要通过目标场景测量后定。 |
| UI/音频 → 集成/QA | 面板状态与交互流、键鼠路径、分辨率和语言扩展检查；音效事件表、响度/混音意图、循环与淡入淡出、素材来源与授权。 |
| 程序/集成 → 主 AI/QA（按需） | 任务卡、变更文件、测试构建、进入功能的最短操作、可观察预期、已知问题和日志位置。 |
| QA/性能 → 主 AI | 复现步骤、实际与预期、严重度、截图/录屏、构建版本与设备；性能另附负载、采样方法、帧时间分布和瓶颈证据。 |
| 发布 → 人 | 发行候选构建、哈希、各渠道所需资料、许可与披露清单、回滚方式、未解决问题及提交后支持安排。 |

## 7. 每个任务的一次实际运转

1. **接单与只读核对。**主 AI 先读项目规则、需求/架构/验证文档和目标模块；写出玩家可见行为、触发、输出、失败与生命周期。普通未定项由团队补齐，只有项目规则列出的真正用户决策事项才集中提问；已有确认不再问。
2. **任务卡。**确定一个有边界的交付物、文件所有者、依赖、验证方式与完成定义；标明用户确认、项目事实与 AI 决策依据。
3. **并行调查。**策划给规则方案，架构给接口和风险，美术给规格，需要时 QA 给测试矩阵；主 AI 比对冲突，追溯用户确认记录及当前代码/资源。普通玩法或体验分歧由主 AI 根据证据裁决，不按代理“多数票”改项目。
4. **串行锁定共享物。**先定组件契约、命名、资源路径和 scene/prefab 归属。独立代码/素材任务可并行；同一 Unity 场景、Prefab、manifest 和 ProjectSettings 排队给唯一写入者。
5. **实现与自检。**范围写入代理交实际 diff、日志、截图、源资产及 .meta。主 AI 检查是否改了任务外文件、是否保持可编辑的场景/Prefab、是否更新了需求/架构文档。
6. **反向审查。**另一个代理从“怎样会失败”角度审视规则、资源引用、生命周期、线程/Job 依赖、性能和许可；提出具体可复现问题。原执行者修复。
7. **集成验证。**由实施团队按改动选择静态检查、Unity 编译、Editor/Player 运行、游戏功能检查与必要的性能采样；发现问题先修复并复验。编译通过不等于玩家体验合格。
8. **交付记录。**主 AI 写明改了什么、遇到的未知项如何裁决、如何验证、结果与日志位置、未验证项/剩余风险；用户可另外试玩，发行等事项由用户决定。

建议看板状态为“需求核对 → 主 AI 裁决 → 可接单 → 实施中 → 评审 → 适用验证 → 完成”。同一时间只设一个共享场景/配置的写入任务，并把其他代理用于独立代码、素材候选、测试设计或审查。每轮由主 AI 汇总新事实、已验证产物与下一步；有可玩内容时提供 Player 构建供用户选做体验反馈。若多次返工来自同一种误解，把修正规则写回项目文档，而不只留在聊天历史。

### 可复制的任务卡

~~~markdown
# TASK-ID：一句话目标
状态：需求核对 / AI 裁决 / 开发中 / 验证中 / 完成 / 确需用户决策
来源：用户消息、需求编号与日期
用户已确认：逐条列出
检查事实：文件路径或可复现操作
AI 决定与依据：写明策划默认值、分歧和主 AI 裁决；不要冒充用户确认
玩家可见行为：输入/触发 -> 状态变化 -> 画面/声音/数值输出
边界与失败：无目标、死亡、切场景、重试、存档/恢复等与本任务相关项
不包含：避免顺手扩成大重构
目标平台/负载：设备、分辨率、单位/弹体/特效规模
依赖和接口：先完成的任务、数据所有者、资源路径、场景/Prefab
文件归属：本代理可写清单；共享资源由谁集成
确需用户决定的阻塞：仅列无法可靠推断、违反明确约束或不可逆/外部发布/付费事项
验证计划：按改动选择静态检查、Unity 编译、Editor/Player、功能操作与性能采样
交付：diff、资产及 .meta、截图/日志、文档更新
结果：通过 / 未通过 / 未运行；证据位置与剩余风险
~~~

### 主 AI 的启动指令

~~~text
你是本项目 AI 制片与集成协调。先读取 AGENTS.md、Tools/Skills/unity-requirements-first/SKILL.md、
Docs/README.md，以及本任务涉及的需求、架构、性能和验证文档。以当前代码和资源为事实。
把用户已确认、检查事实、AI 决定及依据、未验证分开写。先给玩家可观察的行为和验收方式。
由策划补齐普通玩法与默认数值，相关角色讨论冲突，由你裁决并实施；只在目标无法可靠
推断、违反用户约束或涉及不可逆数据损失、外部发布/付费时提问。按任务依赖安排策划、
程序、美术等专职代理，QA 按需加入；每人给明确产物和可写文件范围。
共享场景/Prefab/ProjectSettings/manifest 只能有一个写入者。集成时检查 diff、资源 GUID、
Editor/Player 结果和文档状态。你负责选择适用检查、运行、修复并复验；没有 Player 实测，
不宣称完整万级战斗达标。最后向我报告已完成内容、决策、验证证据和剩余限制。
~~~

### 专职代理的接单指令

~~~text
角色：<策划 / ECS 程序 / 技术美术 / UI / 音频 / QA / 性能 / 发布>
任务卡：<TASK-ID、用户方向与主 AI 已裁决方案>
先读共同规则和任务相关文档。只在“可写清单”内改动；其他地方只给建议。
输出：1) 发现或方案，2) 实际文件/资产清单，3) 验证步骤及真实结果，
4) 与规格差异，5) 不能独立解决的阻塞。不要把未运行的检查写成通过；
不要把候选资产、Editor 截图或纯模拟性能写成发行质量证据。
~~~

### 可选的小范围试运行：零玩法改动

任务示例：**只读核对 Game Demo 自动索敌在“无目标、目标死亡、目标离开范围、切场景”四种情形的当前行为**。策划代理对照文档列预期与缺口；程序代理定位数据所有者和现有逻辑；QA 代理给可复现步骤、现有验证入口和证据空缺；主 AI 汇总差异。此例仅用于校准代理能否读取项目、区分事实与 AI 决定，不是实际功能任务的必经阶段。若发现功能缺陷，主 AI 可据现有用户方向自行决定修复范围、实施并验证。

在 Codex 桌面端实际操作这一轮：

1. 选当前 Unity 项目和 **Local** 环境，给主 AI 一条任务：“按 AGENTS.md 与 Docs/README.md，仅调查 Game Demo 自动索敌在四种边界情形的现状。把工作并行交给策划、程序和 QA 三个只读子代理；每人给文件路径、可复现证据、未知项，不修改文件。汇总不一致之处和主 AI 对预期行为的判断。”子代理与主任务共享此项目目录，所以本示例统一只读。[Codex 子代理说明](https://learn.chatgpt.com/docs/agent-configuration/subagents)。
2. 主 AI 给策划代理 [game-demo.md](game-demo.md) 和 [requirements.md](requirements.md)，输出“文档已确认/尚未写明”；给程序代理战斗模块和代码定位任务，输出文件/方法与状态所有者；给 QA 代理现有验证指南，输出 Player 复现步骤和证据空缺。三份报告均列“事实/推断/待确认”。
3. 主 AI 以项目文件和用户方向核对三份报告，指出相互冲突的具体位置；普通玩法预期由主 AI 裁决并记录依据。
4. 若确有缺陷，主 AI 写范围明确的修复任务卡并安排实施。若已建立 Git，可在 Codex 新任务中选择 **Worktree** 与起始分支，让实现代理在独立目录改代码；Codex Worktree 默认可能处于 detached HEAD，按其界面创建分支或移交到 Local 集成。[Codex Worktree 操作](https://learn.chatgpt.com/docs/environments/git-worktrees)。
5. 主 AI 安排适用的 Editor 验证与 Game Demo Player smoke，修复失败项并复验，更新需求/架构/验证文档；用户可另行试玩并反馈体验。

这轮只读示例的验收是**报告准确、边界清楚、没有误改文件**；它不证明自动索敌没有 Bug。真实功能任务应继续实现、执行适用检查并记录结果。

## 8. 质量、性能与合规门槛

### 代码和构建

- 每个功能任务由实施团队按改动风险选择适用的包/编译、Editor 规则和 Player 行为检查；发现失败先修复并复验。现有可复用菜单见各模块指南。若新增 Unity Test Framework 测试，官方命令行支持 -runTests、-testPlatform、-testResults；**测试运行时不要加 -quit**，还要解析 XML 和日志，而非只看退出码。[Unity 6.3 Test Framework 命令行](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/test-framework-introduction/reference-command-line)。
- 若以后添加了项目自己的测试程序集，下面是 Unity Editor 命令行示意；先在 Unity Hub 核对 Editor 安装路径。目前主要应运行已有模块验证菜单与 Player smoke，不能因测试命令能启动就称已有完整测试覆盖。

~~~powershell
$EditorExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe' # 按实际安装路径替换
$ProjectDir = 'D:\UnityProject\myGame'
& $EditorExe -batchmode -projectPath $ProjectDir -runTests -testPlatform EditMode -testResults "$ProjectDir\Logs\ai-editmode.xml" -logFile "$ProjectDir\Logs\ai-editmode.log"
~~~

- Unity -batchmode 可跑定义好的验证或构建；同一项目路径不要并发打开 Editor。-nographics 不初始化图形设备，不用于 URP 画面/GPU 验收。[Unity 6.3 Editor 命令行](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)。
- 对共享资源做引用/GUID 检查；优先在 Editor 内通过显式工具与可编辑 Prefab 处理。不要让代理直接改 Library、PackageCache 或伪造 packages-lock.json。

### 画面、玩法与性能

- 画面与玩法验收：实施团队在固定镜头与分辨率下取图或录屏，检查敌我识别、遮挡、技能范围、UI 文本与交互、色彩/音效一致性，并运行适用的操作检查；用户可另外试玩并记录困惑点和误操作。育碧 La Forge 的 [生成式 PBR 材质研发复盘](https://www.ubisoft.com/en-us/studio/laforge/news/1i3YOvQX2iArLlScBPqBZs/generative-base-material-an-opensource-prototype-for-pbr-material-estimation-debuting-at-siggraph-asia-2025)也明确其原型尚未达到生产品质，说明资产生成后仍需技术美术把关。
- 性能验收按 [本项目 performance.md](performance.md) 的三层：纯模拟、模拟加渲染、完整战斗。目标硬件为 Ryzen 5 5600/i5-12400、RTX 3060、16GB、1080p；一万是**可见且参战的敌人**，不含弹体与掉落物。完整战斗目标平均不低于 60 FPS、p95 帧时间不高于 16.67ms、p99 不高于 25ms；这些是目标，不是已实测。记录构建配置、固定种子、负载、预热、三轮各 60 秒 Player 数据、CPU/GPU/GC/内存和 p50/p95/p99。不要用空实体或纯运动场景替代。
- 流程效果也要测：任务从开始到“可用 Player 结果”的实际工时、返工次数、缺陷数、资产清理时间、代理调用成本，以及玩家测试反馈。每个试点与原有人工流程尽量做同口径比较；KARA 的价值正是追踪这些成本。[Keywords KARA](https://www.keywordsstudios.com/en/about-us/news-events/news/lessons-from-project-kara/)。

### 素材、声音和平台披露

每个进入发行候选的素材保存：名称与用途、作者/工具/模型版本、输入参考及其许可、生成或购买日期、修改人、源文件、商用许可、第三方相似性审查结果、是否最终进入构建。生成候选不自动进入生产库。Unity 自身说明其 AI 生成物会标记元数据，但最终作品的第三方权利仍由开发者负责；不得擅自把 Asset Store 素材用于模型训练或作为生成模型输入，具体以素材许可和现行条款核对。[Unity AI 原则](https://unity.com/legal/unityai-guiding-principles)、[Asset Store 条款](https://unity.com/legal/as-terms)。

若将来选择 Steam，Steamworks 当前公开文档要求在内容调查中说明**借助 AI 工具制作、最终随游戏提供给玩家的内容**，区分开发时预先生成和游戏运行时生成；实时生成还要说明防止非法内容的措施。工具带来的普通效率提升不是该调查重点。正式填写以提交时最新页面和具体游戏内容为准。[Steamworks Content Survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey?language=english)。若复制真人声音，记录被模仿人的授权和适用合同；例如 [SAG-AFTRA 2025 互动媒体协议](https://www.sagaftra.org/contracts-industry-resources/interactive/2025-interactive-media-video-game-agreement)对其覆盖的数字替身有专门要求，不应泛化到所有项目。

## 9. 推进顺序与待确认决策

**第一轮：流程校准。**按需用第 7 节只读示例校准代理交接；盘点大文件、备份和 Git 忽略项；把现有 Editor/Player 检查映射到任务卡。该只读示例不是开发新玩法的前置条件。记录一次团队协作的真实工时。

**第二轮：一个小范围功能。**先有可恢复基线、文件归属与验收卡；有 Git 时可用独立工作树。若仍未采用版本管理，至少保留经检查的备份、仅由单个代理写入共享资源，并且不能声称具备 Git diff 或 worktree 隔离。主 AI 从用户方向选一个可在 Game 场景实际游玩的任务；策划规格、架构评审和按需 QA 方案并行，由主 AI 裁决后交程序与资源代理实施，主 AI 集成、验证、返工并记录 Player 证据。不要同时启动多个共用场景的功能。

**第三轮：纵切片与管线。**连续几个小任务稳定后，再把素材、音频、测试和构建的重复步骤自动化；建立每周可玩的 Player 构建、缺陷与性能趋势。只有当人工返工、集成冲突和成本数据支持，才增加并发代理数量或接入 Editor MCP/CI。

进入**实际项目修改**前，主 AI 先核对现有需求、代码与资源，再由团队补齐普通玩法、画面、负载和验收细节并记录依据。当前未定的版本管理、Git LFS、自动 Editor 接入和下一项具体功能，可按任务需要调查后由主 AI 提出并实施范围内的可恢复改动；发布渠道、外部付费、不可逆损失和有权利争议的素材使用须由用户决定。缺少这些非当前任务的选择，不应阻止已授权功能的实现和 AI 自测。
