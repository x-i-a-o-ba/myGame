# 架构与迁移方案

后续开发遵循 [模块化与胶水层规范](architecture-guidelines.md)。当前 Demo 的职责拆分方案与已落地功能分开记录，整体重构范围尚待用户确认。

用户新增场景可维护性要求后，已完成其中的场景展示迁移：固定 UI、场地、主角外观、移动标记和 EventSystem 保存到 Game；HUD 通过展示快照与重开事件连接。迁移工具只在 Editor 中创建缺失展示，运行时不再拼装这些固定对象。修改入口和所有权见 [场景维护说明](scene-authoring.md)。

状态：3D 俯视角、PC、基础框架与独立压测场景已确认；URP 和基础 ECS 已实现。新一轮 Game 战斗 Demo 已加入追逐、自动索敌、追踪弹、伤害和死亡重开，具体规则见 game-demo.md；主动技能仍待后续设计。

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

## Game Demo 战斗更新顺序

输入桥接写入 CombatPlayer.Destination → 玩家移动 → Burst 并行敌人追逐/近战伤害队列 → 汇总玩家伤害与死亡 → 在途弹体追踪/命中 → 到开火间隔时搜索最近存活敌人并生成弹体 → ECB 批量播放结构变化 → TransformSystemGroup 更新渲染变换 → MonoBehaviour LateUpdate 同步玩家外观、镜头和 HUD。

CombatOwned 标记会话拥有的实体（含弹体 Prefab），重开和控制器销毁时集中清理。CombatEnemy 与 BenchmarkAgent 分离，各自系统不会处理对方实体。玩家死亡后冻结当前战斗状态，重开恢复配置和数量。

该 Demo 单玩家索敌采用每次开火 O(N) 扫描，避免每帧逐弹体遍历全部敌人。追踪弹只读取指定目标。若后续增加范围伤害、大量武器或敌人间分离，应在明确需求和测量后引入空间索引。

## 验证状态

已从项目本地 URP 17.3.0 包内资源建立 Assets/Settings/Rendering/RoguelikeURP.asset 和 RoguelikeForwardPlusRenderer.asset；清除示例管线对多个示例 Renderer 的引用，只保留新建 Forward+ Renderer。已启用 SRP Batcher，关闭默认深度/不透明纹理请求和附加光阴影。其余画质为起步值，待平台与性能目标确定后调优。

GraphicsSettings 已绑定管线，现有 Quality 各档无独立覆盖，继承全局管线。原始 GraphicsSettings 和 QualitySettings 保存在 Backups/urp-migration-20260922。恢复时在 Unity 关闭后用备份覆盖同名 ProjectSettings 文件。

完成文件级配置和 GUID 检查，Unity 已导入管线并注册 URP 全局设置；BRG 变体保留为 KeepAll，项目使用 Linear 色彩空间。独立验证副本位于 Temp/AgentValidation，输出日志位于 Logs/AgentValidation.log。最终状态见 requirements.md 和性能记录。

## 独立技能指示器演示

SkillIndicatorDemo 使用独立 Roguelike.SkillIndicators 程序集，不依赖 ECS 战斗模块。CircleTargeting 处理平面距离规则，IndicatorDemoInput 适配桌面输入，IndicatorDemoController 连接配置、视图与 HUD。布尔值 clampOutOfRange 默认 true，公开属性可供未来设置界面接入。固定节点与 UI 保存到场景，圆形视图使用可编辑 Prefab，素材仅供演示。使用与验证见 skill-indicator-demo.md。

## 独立迷雾与视野演示

FogOfWarDemo 使用独立 Roguelike.FogOfWar 程序集。VisionSource 配置少量来源，FogSolver 用 Burst 阴影投射写入阵营位图，FogTargetBatch 并行查询万级目标，FogDemoInput/Hud 适配输入与场景 UI，FogDemoPresentation 负责显示资源与安全信息展示，FogDemoSession 负责装配和模拟资源生命周期。固定 UI、眼槽、地形、树与来源保存为场景对象；普通敌人不会隐式成为来源。

该演示的万级目标是 NativeArray 与 GPU instancing，不是 Entities 战斗单位。后续 ECS 桥接只传批量位置/阵营/隐身和显隐结果，不把世界模拟、联网权限、每个敌人的 Update 塞入迷雾模块。模型、小地图、选择统一查询同一快照；隐藏状态只影响显示和信息，不停止目标模拟。详细接口和已知边界见 fog-of-war-demo.md。
