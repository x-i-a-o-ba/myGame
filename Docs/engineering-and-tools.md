# 工程与工具

## 职责和所有权

| 模块 | 配置与职责 | 状态与生命周期 |
| --- | --- | --- |
| `HeroDefinition`、`SkillDefinition`、`SkillRecipe`、Buff / 效果 / 装备 / 怪物模块 | 可复用只读定义，GUID 与稳定编号决定身份 | 资源不写入当前生命、冷却、已选形态 |
| `SkillRecipeCompiler`、`SkillCatalog` | 验证组合语义，生成不可变执行表；不支持的配置拒绝编译 | 会话创建时编译，热换装保留仍有在途引用的旧版本 |
| `AbilityExecution` | 通用目标与形状查询、阶段调度、投递、共享伤害/治疗/状态/位移 | 单位组件与动态缓冲为权威；退出时 `ReleaseOwner` 清理程序和穿透命中集合 |
| `GameCombatSession` | 装配英雄、地图名册、成长、装备和怪物技能 | 拥有 Blob、原型、战斗实体与合并网格；重开先清理上局，场景退出释放 |
| `CombatSimulationSystem` | WASD 移动、自动普攻、学习、敌人任务、奖励和导演 | `AbilityUnit` 是生命、法力与属性权威，其他快照不反写结算 |
| `MonsterAbilities` | 七类怪物模块、优先级和数量预算 | 持有预警、区域、技能弹体、冲锋；`MonsterBrain` 保存单位冷却和词缀时间 |
| `RunState`、`CampState` | 三关流程、路线、跨场景快照、营地随机货架 | 仅内存；战场和交易后捕获，返回选择时清空；营地没有独立场景 |
| `CombatHud`、`CombatFeedbackView` | 消费快照、发出意图、合并表现 | 不决定命中；数字、碎片、预警网格均有上限 |

ECS 并行任务结束后才进入串行共享效果结算，避免同时写单位和 Transform。普攻、技能、装备及 Buff 使用同一个伤害/治疗入口。装备触发器只响应主普攻命中或主动技能施放事件，派生效果不递归触发自己。技能槽只代表输入，禁止按英雄名、技能 ID 或按键分支结算。

## 本轮增加的积木

原有固定阶段不能表达穿透去重、跟随持续区域、外圈、逐级投射数量、每 N 次主普攻触发和拉拽。因此新增独立配置及编译字段：`Pierce`、`FollowOwner`、`SeekDense`、`InnerRadius`、`RepeatAdvance`、`ForwardOffset`，逐级半径/数量/散射/附加目标，以及自动普攻计数、共享位移效果和装备触发器。360° 散射不重复首尾方向；每条穿透弹体保留独立命中集。

拉拽保留距离属于 `DisplacementEffect.stopDistance`，编译到 `EffectData.StopDistance`，只由共享位移执行器消费；战歌设置 2.5 米，避免把整群怪物压到英雄脚下。定义层校验有限非负数值。控制免疫和位移免疫在结算前抵抗，UI 不参与。

伤害可装配固定值、来源攻击倍率和目标最大生命倍率。Buff 可组合减伤、百分比生命回复、易伤、近战反伤、控制免疫、额外普攻目标、穿透和溅射；状态归拥有者，周期伤害保留来源快照。附加被动通过 `AbilityTraits` 合成，不硬编码遗物名称。

## 编辑配置

- 技能工作台：`Tools/Roguelike` 下的技能编辑菜单；配方步骤面板已包含穿透、跟随、密集点、圆环、起始偏移和重复推进。当前资源更改须显式保存；复制分配新 ID，修订增加，JSON 导入经过验证再应用。
- 正式技能、Buff、普攻、装备、英雄、地图、怪物模块、遗物及输入资源都有中文 Inspector。普通字段通过 CustomEditor / PropertyDrawer 绘制，枚举使用 `InspectorName`。旧属性枚举编号仍保留为“已停用”，编辑和编译均拒绝再次装配。
- `Tools/Roguelike/远征/配置字段检查` 将正式 CustomEditor 放进宽窗口，供逐项实际查看，并不是独立模拟表单。当前轮的人工可视检查状态见验证记录。
- `Tools/Roguelike/远征/生成界面并装配战场` 重新保存正式 HUD 与营地面板；它会重建布局，已有手工布局应先提交。旧 HUD Python 生成器与旧面板已删除，避免恢复被淘汰机制。
- `ExpeditionContent.Heroes`、`.Equipment`、`.Monsters` 是整套定稿内容的重建入口，会覆盖对应配置，日常局部调参优先直接编辑资源。
- 数字编号、中文配置清单位于 `Docs/游戏配置`；`HeroSkillDocumentation.Export` 更新导出快照。装备改名保留 `.meta` GUID，退休编号不复用。

## 构建和验证入口

在项目根目录执行（本机 Editor 路径见 Unity Hub）：

```powershell
& $unity -batchmode -nographics -quit -projectPath D:/UnityProject/myGame -executeMethod Roguelike.Editor.ExpeditionBuild.Build --expedition-build Builds/ExpeditionAccepted -logFile Logs/build.log
& $unity -batchmode -nographics -quit -projectPath D:/UnityProject/myGame -executeMethod Roguelike.Editor.ExpeditionBuild.ValidateTools -logFile Logs/tools.log
```

`Build` 先验证战斗规则和开局/战场引用，再构建四个游戏场景。`ValidateTools` 验证中文编辑器注册、配方编译、程序版本、编辑事务与编号，导出文档。实际 Inspector 观感与原生键鼠需要另外检查，不能由静态检查代替。

```powershell
& ./Builds/ExpeditionAccepted/Game.exe -batchmode -force-d3d11 --expedition-smoke --expedition-output Logs/flow
& ./Builds/ExpeditionAccepted/Game.exe -batchmode -force-d3d11 --expedition-smoke --expedition-hero warrior --director-map ember --expedition-output Logs/flow-warrior
& ./Builds/ExpeditionAccepted/Game.exe -batchmode -force-d3d11 --combat-stress --stress-count 4000 --stress-seconds 60 --stress-health-scale 8 --stress-natural-cooldowns --director-output Logs/stress
& ./Builds/ExpeditionAccepted/Game.exe -batchmode -force-d3d11 --game-balance --balance-hero warrior --game-output Logs/balance
```

流程探针补给并跳过首领等待，只证明流程；压力探针固定目标负载并补给，不证明难度；上例使用八倍敌人生命和正常冷却，省略这两个参数则使用正式生命并强制重置技能冷却，属于另一种极端清场测试。两种口径不能混报；平衡探针只发送合法操作，不改生命、法力、金币、经验或导演时钟，但自动走位不等于玩家手感。具体适用范围见 [验证记录](validation.md)。

## 兼容和迁移

正式英雄、八技能与原有装备尽量保留 GUID；中文改名同步生成器路径和编号台账。新增形态独立编号。11 项属性按稳定枚举值索引，不把被淘汰的四个编号重新解释成新属性。旧英雄天赋字段只用于检测旧资源并报迁移错误，不再编译天赋运行状态。

旧右键指令、转身路径、天赋选择、目标信息面板、固定商店、地面装备及拖放运行实现已删除；相应旧专用测试被当前形态、成长、营地与端到端回归替代。历史实现与验证归档，不作为现行规格。
