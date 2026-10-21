# 架构与迁移方案

## 2026-09-24 当前技能架构（优先于下方历史章节）

用户要求组合地基优先，已移除Game固定QWER实现，当前契约见 [ability-foundation.md](ability-foundation.md)。`HeroDefinition → SkillDefinition → Stage[] → EffectDefinition[]`，Buff独立；Core无UI依赖，Game采用AbilityUnit唯一属性权威。GameCombatSession负责实体与Blob生命周期，Controller只装配输入/展示，CombatSimulationSystem处理战斗宿主行为，技能由AbilityExecution调度。现有行为差异、未实现内容与本轮验证统一见新文档。下文关于四份专用配置和旧验证的段落为历史记录。


## 2026-10-21 自动攻击模块、Dota 2 式指令与敌人种类

- `Abilities/Core/AutoAttack`：`AutoAttackDefinition`（ScriptableObject，只存配置）编译为 `AutoAttackData` 组件；`AutoAttackLogic.Step` 是纯函数，推进 `AutoAttackState`（目标、阶段、计时、冷却），返回开始/出手/取消事件，英雄（主线程 `StepHero`）和敌人（Burst `EnemyJob`）共用。伤害结算仍由宿主负责：英雄弹道走共享效果入口并触发普攻命中事件，敌人近战/弹道/冲锋/自爆写入英雄伤害队列。
- 输入层只写 `HeroOrder`（Idle/Move/AttackTarget/AttackMove/Hold + 目标点 + 目标实体 + 序号），`StepHero` 处理新指令（打断前后摇，保留冷却）、目标有效性与牵引、自动索敌、按 `CombatSettings.TurnRate` 转身、移动与攻击节奏。鼠标点选敌人在 `GameCombatController.PickEnemy`（射线到敌人中心的距离，按体型放宽）。
- 敌人：`Runtime/Combat/Enemies` 的 `EnemyArchetype`（预制体配置组件）与 `EnemyRoster`（名册）。`GameCombatSession.Reset` 先校验整份名册再清空旧会话；每个种类合并预制体网格后建一个 ECS 原型（`CombatEnemy` + `AutoAttackData/State` + `EnemyBehaviorData` + 实例颜色），种类表存为会话实体上的 `CombatEnemyKind` 缓冲；`CombatWaveDirector` 每波按档位、波次、权重抽种类，再乘种类倍率实例化。敌方弹体是 `HostileProjectile` 实体，由并行任务追踪英雄。
- HUD：`CombatHud` 把 `CombatHudSnapshot` 转成面板键值，只在值变化时发布；`UIPanel` 新增填充方向（横向/纵向/径向）与显隐开关绑定。UI 不读取 ECS。

详见 [enemies-and-controls.md](enemies-and-controls.md)。Unity 编译与运行**未验证**。

后续开发遵循 [模块化与胶水层规范](architecture-guidelines.md)。Game 是持续开发的战斗场景，当前操作见[Game 战斗指南](game-combat.md)；旧文档中的 Game Demo 称谓仅指历史交付版本。

用户新增场景可维护性要求后，已完成其中的场景展示迁移：固定 UI、场地、主角外观、移动标记和 EventSystem 保存到 Game；HUD 通过展示快照与重开事件连接。迁移工具只在 Editor 中创建缺失展示，运行时不再拼装这些固定对象。修改入口和所有权见 [场景维护说明](scene-authoring.md)。

状态：3D 俯视角、PC、基础框架与独立压测场景已确认；URP 和基础 ECS 已实现。Game 已有追逐、自动索敌、追踪弹、伤害和死亡重开。本轮以四份 ScriptableObject 配置、W/E/R 指示器和 E 目标点范围伤害替换旧技能接入；Game 视野机制关闭，独立 FogOfWarDemo 保留。Game 场景资产已绑定，最终版隔离 Unity 编译、CombatValidation、Windows Player 构建与 3000 敌人 Player 技能冒烟通过；主项目 Editor 未单独运行，万敌完整战斗性能仍待验证，见[接入说明](ashe-game-integration.md)。

## 系统边界建议

| 层 | 职责 | 约束 |
| --- | --- | --- |
| Authoring / Baking | 编辑期配置和实体预制体烘焙 | 使用与实际 Entities 版本匹配的 Baker；不要混用早期 ConvertToEntity 教程 |
| ECS Simulation | EnemyTag / Health / MoveSpeed 及 Combat 数据；追逐、索敌、子弹与结算 | Burst + Jobs；明确依赖和系统更新顺序；避免每实体 MonoBehaviour.Update |
| ECS Presentation | 从模拟状态驱动批量显示 | 渲染路径取决于 2D/3D 选择；动画成本单独评估 |
| GameObject 边界 | UI、相机、输入、声音和少量交互对象 | 通过有限桥接读写 ECS，不为每只敌人创建桥接 GameObject |
| 配置与流程 | 关卡流程、刷怪参数、资源加载 | 配置与高频运行数据分离；新模块通过 asmdef 隔离 |

现有两套管理工具先保留。逐项确认调用者后再迁移，避免一次性重写 UI、场景和资源管理。现有 GameObjectPool/PoolMgr 不直接承担万级单位模拟。

## 渲染路线

- 已采用 URP Universal Renderer + Forward+ + Entities Graphics；材质需支持 DOTS Instancing。当前压测为 URP Unlit 共享立方体，实际 3D 模型和动画后续接入。
- 按目标平台设置阴影、附加光源、MSAA、深度/不透明纹理和后处理预算；不要只降低画质后宣称万级战斗达标。

官方参考：[Entities Graphics 功能矩阵](https://docs.unity.cn/Packages/com.unity.entities.graphics%401.4/manual/entities-graphics-versions.html)。包版本和限制以实际解析版本的文档为准。

## ECS 高频路径约束

- 纯数据组件；先实现有明确需求的字段，避免预建大量空框架。
- 批量生成/销毁与 EntityCommandBuffer；测量结构变化和同步点开销。
- 空间网格或其他局部查询处理邻居、命中和索敌，避免全体两两遍历。
- 根据玩法确认是否需要 Unity Physics；不默认给一万实体添加完整刚体。
- 避免高频托管分配、逐帧全量实体数据复制、每实体事件广播与 Transform 同步。
- 系统在没有必要单例/实体时不工作；关卡卸载和重新进入要释放 Native 容器和外部引用。

## 落地顺序

1. 已确认画面、平台、数量定义和本轮范围。
2. Unity Package Manager 已解析 Entities 1.4.8、Entities Graphics 1.4.21；锁文件由 Unity 生成。Burst、Collections、Mathematics 随依赖解析。
3. 已创建并绑定 URP 管线与 Renderer 资源，各 Quality 继承全局设置。
4. 已建立 Roguelike.Runtime / Roguelike.Editor 程序集与 EnemyAuthoring Baker。压测采用运行时批量实例化网格实体，无需等待 SubScene 烘焙。
5. 已建立独立 Benchmark10k 场景和专用系统；BenchmarkOwned 标记负责退出清理，BenchmarkSettings 控制合成负载。并行运动在 TransformSystemGroup 前更新。
6. 编译、万实体逻辑检查、Windows Release 构建和短时 Player 验证通过；具体玩法另行确认后接入。视觉验收、Editor 重进与正式性能测量待做。

## Game 战斗更新顺序

2026-10-21 起在下述顺序中加入成长与波次：英雄存活时先串行处理 `ProgressionCommand`（学习技能、选天赋），ECB 回放后累计击杀经验、结算升级并重算属性，再按 `CombatWaveState.NextWaveAt` 每帧最多刷一波；胜利后整局冻结，阵亡记为失败。详见 [hero-progression.md](hero-progression.md)。

（2026-10-21 起：输入桥接写入 HeroOrder → StepHero 转身/移动/自动攻击 → Burst 并行敌人行为与敌方弹体 → 汇总英雄伤害；下面为旧顺序。）输入桥接写入 CombatPlayer.Destination → 玩家移动 → Burst 并行敌人追逐/近战伤害队列 → 汇总玩家伤害与死亡 → 在途弹体追踪/命中 → 到开火间隔时搜索最近存活敌人并生成弹体 → ECB 批量播放结构变化 → TransformSystemGroup 更新渲染变换 → MonoBehaviour LateUpdate 同步玩家外观、镜头和 HUD。

CombatOwned 标记会话拥有的实体（含弹体 Prefab），重开和控制器销毁时集中清理。CombatEnemy 与 BenchmarkAgent 分离，各自系统不会处理对方实体。玩家死亡后冻结当前战斗状态，重开恢复配置和数量。

单玩家索敌采用每次开火 O(N) 扫描，避免每帧逐弹体遍历全部敌人。追踪弹只读取指定目标。W 扇形与 E/R 范围效果会增加对怪物集合的扫描成本；Game 完整万敌战斗仍须 Player 测量，再决定空间索引与批量查询优化。

## 验证状态

已从项目本地 URP 17.3.0 包内资源建立 Assets/Settings/Rendering/RoguelikeURP.asset 和 RoguelikeForwardPlusRenderer.asset；清除示例管线对多个示例 Renderer 的引用，只保留新建 Forward+ Renderer。已启用 SRP Batcher，关闭默认深度/不透明纹理请求和附加光阴影。其余画质为起步值，待平台与性能目标确定后调优。

GraphicsSettings 已绑定管线，现有 Quality 各档无独立覆盖，继承全局管线。原始 GraphicsSettings 和 QualitySettings 保存在 Backups/urp-migration-20260922。恢复时在 Unity 关闭后用备份覆盖同名 ProjectSettings 文件。

完成文件级配置和 GUID 检查，Unity 已导入管线并注册 URP 全局设置；BRG 变体保留为 KeepAll，项目使用 Linear 色彩空间。独立验证副本位于 Temp/AgentValidation，输出日志位于 Logs/AgentValidation.log。最终状态见 requirements.md 和性能记录。

## 独立技能指示器演示

SkillIndicatorDemo 使用独立 Roguelike.SkillIndicators 程序集，不依赖 ECS 战斗模块。CircleTargeting 处理平面距离规则，IndicatorDemoInput 适配桌面输入，IndicatorDemoController 连接配置、视图与 HUD。布尔值 clampOutOfRange 默认 true，公开属性可供未来设置界面接入。固定节点与 UI 保存到场景，圆形视图使用可编辑 Prefab，素材仅供演示。使用与验证见 skill-indicator-demo.md。

## 独立迷雾与视野演示

柔边更新：FogVisualMask 独立拥有三张 4 倍分辨率 RenderTexture 和滤波材质实例，完成双线性放大、两遍可分离高斯滤波及前后快照混合。只有逻辑掩码变化或显示参数变化才更新 GPU 目标纹理；世界 Shader 每帧按时间插值，不提高 Solver 刷新率，不向 CPU 回读。阵营切换、重置、探索模式和全知模式切换清除显示历史。敌人渲染/选择继续使用当前逻辑快照，滤波只影响地形迷雾。

FogOfWarDemo 使用独立 Roguelike.FogOfWar 程序集。VisionSource 配置少量来源，FogSolver 用 Burst 阴影投射写入阵营位图，FogTargetBatch 并行查询万级目标，FogDemoInput/Hud 适配输入与场景 UI，FogDemoPresentation 负责显示资源与安全信息展示，FogDemoSession 负责装配和模拟资源生命周期。固定 UI、眼槽、地形、树与来源保存为场景对象；普通敌人不会隐式成为来源。

独立演示的万级目标是 NativeArray 与 GPU instancing，不是 Entities 战斗单位。`FogOfWarDemo` 的 `FogSolver`、柔边显示、小地图与调试 UI 均保留在该独立场景。Game 先前复用这些能力的代码是历史方案，本轮关闭后保存在单个 `CombatFogSession.cs`，不参与 Game 当前战斗装配、索敌或渲染。详细现行边界见 ashe-game-integration.md。

## Game 技能与指示器适配（2026-09-23）

`GameCombatController` 是 Game 会话装配和输入桥接：四份 `CombatQ/W/E/RSkillConfig` ScriptableObject 在重开时填入 `CombatAbilitySettings` ECS 数值快照；冷却、层数和充能属于会话状态，不写回资源。`CombatSimulationSystem` 消费 `CombatAbilityCommand`，统一处理 Q/W/E/R、伤害/击杀和敌人减速、眩晕。E 使用 `CombatAreaProjectile` 飞向目标点，抵达后按半径造成一次范围伤害；R 仍命中第一只怪物后爆炸。

`GameAbilityIndicator` 只维护 W 扇形、E 距离与落点圆、R 直线的可视化及瞄准状态；指示器不结算伤害。Game 输入将左键确认的目标和方向提交给 ECS，右键或 Esc 取消，HUD 区域和无效地面投影不得复用旧落点。固定圆环与轮廓线由 Game 场景的 `03_Presentation/Effects` 持有；`CombatHud` 通过场景 UI 事件转发技能槽意图并展示快照，不读取 ECS。`Roguelike.Runtime` 复用独立指示器程序集中的通用圆形视图和截距规则，独立指示器演示输入/控制器不进入 Game。

Game 当前关闭迷雾、昼夜、真视、隐身和视野过滤，自动攻击面向全部存活怪物。旧 Game 迷雾适配的原生容器、纹理和逻辑被封存在 `CombatFogSession.cs` 单文件，不再创建会话对象；独立 `FogOfWarDemo` 仍按原规则运行。Game 场景已绑定四份 `Assets/Game/Skills` 配置资产、两只技能圆环、一条轮廓线和 HUD 技能按钮；地面改用 GameGround，旧视野组件已移除，`enemyCount=3000`、`safeSpawnRadius=30` 保留。隔离副本 Unity 迁移与最终版 CombatValidation、Windows Player 构建、3000 敌人技能冒烟通过；证据位于 `D:\UnityProject\myGame_validation_20260923` 的 `migration2.log`、`build4.log`、`player3.log`、`Smoke3/result.json` 和 `Smoke3/e-aim.png`。右键按住取消瞄准后的移动抑制已静态修复，但真实鼠标输入未测。生成场景资产已复制回主项目，主项目 Editor 未单独运行。旧 Game 迷雾 Player 记录是历史证据；一万可见参战是目标，完整战斗 FPS 未测。详见[接入说明](ashe-game-integration.md)。

## 独立技能架构模块（2026-09-23）

Assets/Game/Abilities 以 ScriptableObject 编写共享配置，启动时编译为 Blob；等级、冷却和 Buff 保存在拥有者 ECS 数据中。技能按投放类型、阵营、范围、效果列表组合，被动在学会/移除或普攻命中入口调用同一效果层。作业流水线为 Buff/移动 → 空间网格 → 命令校验与区域查询 → 请求排序与统一写回 → 摘要。效果写回为单 Burst Job；不为万单位创建事件订阅或 MonoBehaviour。输入、UI、展示与会话装配分开，静态节点保存到场景。详见 skill-architecture-demo.md；未接入 Game/Fog，不代表原战斗架构已迁移。

## 场景/UI 模块统一（2026-09-23）

所有 7 个场景已采用 Bootstrap、Systems、World、Presentation、UI、Debug 分组。新增独立 Roguelike.Foundation 程序集：场景级 UIManager 管理面板生命周期，UIPanel 只绑定内部控件，UIAction 通过 UiEvents 发出命令，模块 HUD 适配器发布展示快照。15 个常驻面板保存成 Prefab，另有 F1 按需加载的开发说明。ResourcePaths 统一解析 ProjectResourceCatalog.json，旧资源加载入口也转到该清单。世界常驻资源继续保留 Inspector 引用。迷雾 contentRoot 显式定义查找作用域，避免控制器对子节点排布的隐式依赖。详细使用、扩展和设计来源见 scene-ui-framework.md。

