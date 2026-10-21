# 技能架构基础演示

日期：2026-09-23。用户已确认采用推荐架构，并补充被动技能；最后确认“先借鉴，实现基础功能就行”。本轮采用以下演示默认值，不复刻 Dota 2 英雄数值或全部规则。

## 场景与使用

打开 `Assets/Scenes/SkillArchitectureDemo.unity`，或菜单 `Tools > Roguelike > Abilities > Open Demo`。独立 Windows 程序为 `Builds/SkillArchitectureDemo/SkillArchitectureDemo.exe`。

- 右键移动；默认自动攻击最近敌人。普攻为即时命中加短线反馈，用来验证命中被动，不复用 Game 的弹道演示。
- Q/W/E 进入瞄准，左键确认，右键/Esc 取消。超距默认截到施法距离边界；根组件 `clampOutOfRange` 关闭后标红拒绝。
- 方向键平移镜头，滚轮缩放。W 不承担镜头移动，避免与技能冲突。
- 未瞄准时左键选中单位，右侧查看 HP、速度和 Buff；左侧可升级技能、切换两个被动、普攻和敌人移动、驱散、扣血测试与重置。
- 默认一万 ECS 敌方训练目标，两个移动友方目标；敌人默认静止，可开启向主角靠近。敌人本轮不攻击主角，扣血按钮用于观察常驻回复。

## 默认技能组合

| 配置 | 范围/触发 | 首级效果 |
| --- | --- | --- |
| Q 爆裂 | 18m 施法，5m 圆形，敌方 | 45 伤害 + 4 秒灼烧，每秒 5 伤害，最多 3 层；冷却 3 秒，消耗 16 |
| W 寒霜区域 | 18m 施法，6m 圆形，敌方 | 区域持续 5 秒，每 0.2 秒刷新 0.6 秒减速，移速 -35%；冷却 6 秒，消耗 20 |
| E 鼓舞 | 自身中心 8m 圆形，友方含自己 | 5 秒移速 +50%；冷却 7 秒，消耗 15 |
| 常驻被动 | 学会时生效 | 攻击 +5、生命回复 +3/秒；不可驱散，关闭被动撤销 |
| 中毒被动 | 普攻命中 | 3 秒中毒，每秒 4 伤害、移速 -20%；重复命中刷新时间 |

技能升级至多 5 级：范围每级 +10%，效果强度默认每级 +25%；消耗和冷却暂不随等级变化。配置资源是共享只读定义，每个拥有者独立保存等级、冷却、属性与 Buff。

## 修改入口与复用

配置目录 `Assets/Game/Abilities/Settings`，`Demo Skill Catalog.asset` 引用主动技能、Buff 和被动资源。Project 右键 `Create > Roguelike > Abilities` 可创建配置；进入会话时编译成 Blob，编辑资源后重新进入 Play 生效。稳定 ID 不可重复，引用的 Buff 必须登记到 Catalog。当前演示 UI 固定 Q/W/E 和两个被动，新增展示槽需要显式扩展场景绑定。

`SkillDefinition` 组合投放类型、阵营、范围、冷却/消耗与效果列表；`BuffDefinition` 定义持续时间、周期、属性贡献和叠层；`PassiveDefinition` 组合触发条件与同一效果列表。即时伤害、治疗与施加 Buff 走共享执行器。新的内容优先组合资源；新机制才增加数据和执行分支。

核心数据和结算在 `AbilityData`、`AbilityEffects`、`AbilityGeometry`、`AbilityJobs`、`SkillSimulationSystem`。`SkillDemoWorld` 拥有本会话实体和 Blob，销毁前完成作业；`SkillDemoController` 连接输入、命令、实体状态和视图，`SkillDemoHud` 只发出意图并显示文本。核心规则不引用 UI。现有圆形指示器 Prefab 复用为范围/落点/区域视图。

Canvas、按钮、相机、主角外观、场地、攻击反馈和 8 个区域视图槽均保存到场景，开发者可在编辑态修改。运行时只启用和更新这些展示节点；一万目标批量创建 ECS 实体，不创建一万个 GameObject。菜单 `Create Demo If Missing` 已有场景时仅打开，不覆盖布局。重置只销毁 `AbilityOwned` 会话实体。

## 结算约定

1. 更新时间、Buff 周期/到期和移动；重建 8m 单元的空间网格。
2. 处理施法命令，校验存活、冷却、资源、距离、持续区域容量，全部通过后才扣消耗和提交冷却。
3. 范围网格查询生成效果请求；普攻命中生成普通伤害和已启用的命中被动效果。
4. 请求按目标与原始顺序排序，由一个 Burst Job 统一写回，避免多个区域同时写同一单位。
5. 批量统计，UI 每秒 10 次刷新摘要；不向 UI 复制万级单位列表。

Buff 按定义、来源实体、来源技能/被动分组。同源灼烧共享到期时间并叠层；不同来源独立实例。刷新不重置下次周期时刻，避免连续攻击使中毒永远不跳伤害。到期前应结算的末次周期包含在内；属性从基础值加全部有效贡献重算，避免移除时累积误差。移速百分比加算，最终倍率限制 0.1–4。

中毒/灼烧伤害不会生成普攻命中事件，因此不会递归触发中毒。关闭中毒被动停止后续施加，已施加的中毒正常到期；关闭常驻被动立即移除自身贡献。死亡目标不继续接受伤害、Buff 或治疗复活，训练场尸体实体保留至重置，外观隐藏。

持续区域首版限定为刷新型 Buff，不允许即时伤害条目和累加层数型 Buff；周期伤害由 Buff 本身负责。错过的区域扫描不补发多次；同时最多 8 个区域，满额拒绝并不扣费。当前有明确作业同步点，统一效果写回仍为单 Job；这便于校验结果，后续根据实际分析再分片。

## 验证状态与边界

Editor 规则检查 23 项通过，涵盖即时伤害/灼烧、失败施法不重复扣费、被动去重/撤销、命中与周期隔离、叠层上限、多来源、末次跳伤/到期恢复、驱散、离开区域、阵营过滤、拥有者等级/冷却隔离、超距策略、销毁目标与重置。日志：`Logs/AbilityBuild.log`。

Windows Release 构建成功，Player 集成检查通过：一万目标初始化、Q 瞄准/消耗/Buff、W 区域、E 实际加速、被动撤销、数值升级、默认截距/禁止超距、UI 拦截、主角移动和重置。`DemoReports/Abilities/result.json` 为通过，`Logs/AbilityPlayer.log` 含 `ABILITY_SMOKE_PASSED`。离屏截图 `start.png`、`aim.png`、`burst.png`、`field-and-buffs.png` 验证真实 URP 场景/UI，已检查最终画面。退出仍出现项目已有的 ComputeBuffer GC 释放警告，未将其标记为已解决。

未实现完整 Dota 2 规则、沉默/眩晕/禁用被动、攻击修饰器优先级、法术抗性、护甲、暴击、驱散强弱、光环驻留、自动施法、引导、联网预测和动态技能图。没有将此模块接入原 Game 战斗或迷雾过滤；这些需要后续具体需求。万目标技能试验不等于完整万敌战斗达到 60 FPS。

复验方式：菜单 `Tools > Roguelike > Abilities > Validate Rules`；构建菜单 `Build Windows Demo` 先执行规则检查。Player 参数 `--ability-smoke --demo-output <绝对输出目录>` 执行集成检查；`--ability-benchmark` 执行三轮负载采样。未传这些参数时不启用自动探针。

## Player 负载采样

2026-09-23，Windows Release、Unity 6000.3.18f1，AMD Ryzen 7 7745HX / NVIDIA GeForce RTX 4060 Laptop GPU，1920×1080 后台窗口。每轮 10 秒预热 + 60 秒采样，共三轮，每约 0.1 秒取一次当前模拟帧耗时，共 1782 条记录。

负载为 10000 个高血量静止敌方目标（全程保持存活）、两个移动友方目标、每 0.8 秒重复 Q/W/E。探针主动清空冷却并补满能量以维持压力；自动普攻关闭，常驻被动开启，中毒机制由功能检查覆盖。最多同时 190 个 Buff 实例、7 个持续区域。不是一万单位全部同时挂 Buff，也不是完整战斗或最坏重叠负载。

| 指标 | 模拟 CPU 墙钟耗时 |
| --- | --- |
| 汇总 P50 | 0.185 ms |
| 汇总 P95 | 0.244 ms |
| 汇总 P99 | 0.332 ms |
| 各轮 P95 | 0.242 / 0.248 / 0.246 ms |

时间范围为 SkillSimulationSystem 更新，包含本模块 Job 等待、网格、Buff、查询和结算，排除后续 Transforms、Entities Graphics、UI 和渲染。不是前台 FPS 结果。原始证据为 `DemoReports/Abilities/performance.csv`、`performance.txt` 和 `Logs/AbilityPerformance.log`。

## 2026-09-23 场景/UI 维护更新

节点、面板和资源入口已统一，详见 scene-ui-framework.md。技能控制器位于 01_Systems；UI 位于 04_UI 的单根 Canvas，三个面板为 Prefab，通过 UiEvents 与 SkillDemoHud 适配器交换数据/命令。技能规则及配置保持不变。
