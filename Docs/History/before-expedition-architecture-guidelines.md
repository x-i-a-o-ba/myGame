> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 模块化、胶水层与编辑器说明

## 2026-09-24 当前技能架构（优先于下方历史章节）

用户要求组合地基优先，已移除Game固定QWER实现，当前契约见 [ability-foundation.md](../ability-foundation.md)。`HeroDefinition → SkillDefinition → Stage[] → EffectDefinition[]`，Buff独立；Core无UI依赖，Game采用AbilityUnit唯一属性权威。GameCombatSession负责实体与Blob生命周期，Controller只装配输入/展示，CombatSimulationSystem处理战斗宿主行为，技能由AbilityExecution调度。现有行为差异、未实现内容与本轮验证统一见新文档。下文关于四份专用配置和旧验证的段落为历史记录。


更新：2026-09-23。用户要求职责分离、低耦合、复用和注释，并进一步要求 Game 不再称 Demo、技能变成 ScriptableObject 配置、释放指示器接入实际战斗。本文把已写入的职责变化与仍属后续优化的拆分分别记录；最终版隔离 Unity 编译、CombatValidation、Windows 构建及 3000 敌人 Player 技能冒烟通过。

## 方案选择

采用轻量模块化：功能模块维护规则和数据，薄组合根连接模块，ECS 负责大规模战斗模拟。当前先手动装配，不额外引入运行时架构框架。

| 一手参考 | 适用部分 | 本项目选择 |
| --- | --- | --- |
| [Unity ECS Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples) | 数据组件、系统、Jobs 与 Entities Graphics 示例 | 核心参考；使用与本项目 Entities 1.4 对应的 API |
| [Entities 系统顺序](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/systems-update-order.html) | 显式系统组与更新顺序 | 把输入、模拟、变换和表现的时序写清楚 |
| [EntityCommandBuffer](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/systems-entity-command-buffers.html) | 延迟执行结构变化 | 说明播放时机及 Job 依赖，不隐式增加同步点 |
| [VContainer](https://github.com/hadashiA/VContainer) | 托管对象装配和生命周期 | 复杂度增加时再考虑；当前无需依赖注入容器 |
| [QFramework](https://github.com/liangxiegame/QFramework) | Unity 应用架构参考 | 可借鉴模块分工，不整套叠加到 ECS 状态模型上 |

上述选择是针对独立开发规模的工程判断，不是官方对本项目的性能保证。完整编码规则见 [项目 skill](../../Tools/Skills/unity-requirements-first/SKILL.md) 及其架构参考。

## Game 当前职责与后续拆分

`GameCombatController` 仍承担会话实体生成/清理、输入、相机、外观和 HUD 状态转换；它不再持有技能内联参数或 Game 迷雾会话。四份技能配置各有 ScriptableObject，重开时成为 ECS 数值快照。`GameAbilityIndicator` 负责 W/E/R 预览与瞄准，不触碰伤害；`CombatHud` 只转发重开和技能槽意图、展示快照，不直接引用控制器或 ECS。当前职责集中度仍有后续拆分空间，不能把改名本身当作完成架构解耦。

若以后输入、相机或会话复杂度继续增长，可在保留场景引用和行为的前提下逐步提取：

| 模块 | 职责与边界 |
| --- | --- |
| CombatSession | 管理 World 会话实体、生成与清理；提供移动/重开命令和小型展示快照 |
| CombatSimulation | ECS 追逐、索敌、弹体与伤害；不依赖相机、HUD、Inspector |
| MouseMoveInput | 鼠标采样、屏幕投影和 UI 遮挡；产生移动意图 |
| FollowCamera / PlayerView | 镜头与少量主角外观；消费展示状态 |
| CombatHud | 渲染展示数据，发出重开与技能意图；不读取 EntityManager |
| GameAbilityIndicator | W/E/R 瞄准与可编辑场景指示器；不读写 ECS 或配置资源中的运行状态 |
| GameCombatController | 当前组合入口；后续只在有具体维护问题时继续提取职责 |
| Editor 工具 | 中文参数、范围标注、验证菜单；不进入运行时程序集 |

已有 Benchmark 独立保留。先按真实变化提取类，形成清晰依赖后再拆程序集；不为每个类型新建接口。专用机制留在所属模块，确认公共使用场景再抽取复用。

本轮重构验收应保持右键移动、移动中自动攻击、怪物追逐、追踪弹结算、死亡停止、重开及资源清理行为；同时检查 W/E/R 左键确认、右键/Esc 取消、E 落点范围伤害、配置重开快照和关闭 Game 视野。运行适用的 Unity 编译、CombatValidation 与 Player 检查；旧 probe/验证结果不自动覆盖新版本。不得将改名或移文件当作解耦完成。

## 已接入的编辑器说明

选择 [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes)，UPM 包版本 2.1.6，Git 提交固定为 `b11d62b28c5fcbe242988f9f8d451deb925cf23b`，上游 MIT 许可证。源码下载在 Temp/NaughtyAttributes-source，项目依赖通过 Packages/manifest.json 声明，锁文件交由 Unity 生成。编辑器实现位于包的 Editor 程序集，运行时仅属性定义；ECS 组件和系统不使用插件属性。

打开 Game，选中带 `GameCombatController` 的对象：Inspector 展示基础战斗参数及 Q/W/E/R 四份配置资产引用；打开对应资源修改技能数值。Scene 开启 Gizmos 并选中控制器可查看攻击配置范围、初始安全区和场地边界；指示器常驻节点位于 `03_Presentation > Effects`，可在编辑态调整。战斗与技能配置运行中修改后需重开，才能与会话快照一致。仅绘制少量标记，不逐敌人绘制。

Scene 绘制由项目 Editor 工具 CombatSceneVisualization 使用 Unity Handles 实现，不是 NaughtyAttributes 的功能。移除插件前需删除控制器中的 NaughtyAttributes 属性和 using、运行时 asmdef 中的 Core 引用，再移除 manifest 依赖并由 Unity 重新解析。不要同时安装覆盖同一 Inspector 的其他插件。

验证状态：以前版本的 NaughtyAttributes/Scene 范围工具已有验证记录；本轮 Game 四份技能资产与指示器场景接线已经迁移，最终版隔离 Unity 编译、CombatValidation、Windows Player 构建和 3000 敌人 Player 技能冒烟通过；E 离屏截图显示双圆。生成资产已复制回主项目，主项目 Editor 未单独运行；交互式 Inspector/Scene 视觉检查和完整战斗性能测量未做。

## 开发者优先与当前落地

所有实现优先便于维护、使用、查看和修改。2026-09-23 已统一全部 7 个场景的节点、UIManager、面板 Prefab、事件通信和资源清单；新代码不得恢复跨面板拖拽依赖、全局常驻重复 Canvas 或散落资源路径。当前规范、修改入口和验证见 scene-ui-framework.md；项目 skill 与全局副本同步。
