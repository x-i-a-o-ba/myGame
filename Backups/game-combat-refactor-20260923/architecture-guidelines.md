# 模块化、胶水层与编辑器说明

日期：2026-09-22。用户已要求职责分离、低耦合、复用和注释；Inspector 中文说明与 Scene 可视化已确认。现有 Demo 整体重构范围仍待回答，下述拆分是方案，不代表已完成。

## 方案选择

采用轻量模块化：功能模块维护规则和数据，薄组合根连接模块，ECS 负责大规模战斗模拟。当前先手动装配，不额外引入运行时架构框架。

| 一手参考 | 适用部分 | 本项目选择 |
| --- | --- | --- |
| [Unity ECS Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples) | 数据组件、系统、Jobs 与 Entities Graphics 示例 | 核心参考；使用与本项目 Entities 1.4 对应的 API |
| [Entities 系统顺序](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/systems-update-order.html) | 显式系统组与更新顺序 | 把输入、模拟、变换和表现的时序写清楚 |
| [EntityCommandBuffer](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/systems-entity-command-buffers.html) | 延迟执行结构变化 | 说明播放时机及 Job 依赖，不隐式增加同步点 |
| [VContainer](https://github.com/hadashiA/VContainer) | 托管对象装配和生命周期 | 复杂度增加时再考虑；当前无需依赖注入容器 |
| [QFramework](https://github.com/liangxiegame/QFramework) | Unity 应用架构参考 | 可借鉴模块分工，不整套叠加到 ECS 状态模型上 |

上述选择是针对独立开发规模的工程判断，不是官方对本项目的性能保证。完整编码规则见 [项目 skill](../Tools/Skills/unity-requirements-first/SKILL.md) 及其架构参考。

## 当前问题与建议拆分（待确认实施范围）

CombatDemoController 仍承担实体生成/清理、输入、相机、外观和 HUD 状态转换；新增场景维护需求后，已移除运行时固定展示树创建，CombatHud 改为展示快照与重开事件，不再直接引用控制器或 ECS 类型。继续加入技能前，剩余职责仍适合按下表逐步分离。

建议保留场景入口与序列化字段兼容性，逐步提取：

| 模块 | 职责与边界 |
| --- | --- |
| CombatSession | 管理 World 会话实体、生成与清理；提供移动/重开命令和小型展示快照 |
| CombatSimulation | ECS 追逐、索敌、弹体与伤害；不依赖相机、HUD、Inspector |
| MouseMoveInput | 鼠标采样、屏幕投影和 UI 遮挡；产生移动意图 |
| FollowCamera / PlayerView | 镜头与少量主角外观；消费展示状态 |
| CombatHud | 渲染展示数据，发出重开请求；不读取 EntityManager |
| CombatDemoController | 薄胶水入口，连接以上对象并统一释放 |
| Editor 工具 | 中文参数、范围标注、验证菜单；不进入运行时程序集 |

已有 Benchmark 独立保留。先按真实变化提取类，形成清晰依赖后再拆程序集；不为每个类型新建接口。专用机制留在所属模块，确认公共使用场景再抽取复用。

重构验收保持右键移动、移动中自动攻击、万敌追逐、追踪弹结算、死亡停止、重开及资源清理行为不变，运行现有 CombatValidation 与 Player probe。不得将改名/移文件当作解耦完成。

## 已接入的编辑器说明

选择 [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes)，UPM 包版本 2.1.6，Git 提交固定为 `b11d62b28c5fcbe242988f9f8d451deb925cf23b`，上游 MIT 许可证。源码下载在 Temp/NaughtyAttributes-source，项目依赖通过 Packages/manifest.json 声明，锁文件交由 Unity 生成。编辑器实现位于包的 Editor 程序集，运行时仅属性定义；ECS 组件和系统不使用插件属性。

打开 Game，选中带 CombatDemoController 的对象：Inspector 显示中文参数、单位及重开生效提示。Scene 开启 Gizmos 并选中该对象，显示黄色攻击配置范围、绿色初始安全区、青色场地边界；运行时追加生命、存活和击杀统计。范围展示读取 Inspector 配置，运行中修改战斗配置须重开才能与实际模拟一致。仅绘制少量标记，不逐敌人绘制。

Scene 绘制由项目 Editor 工具 CombatSceneVisualization 使用 Unity Handles 实现，不是 NaughtyAttributes 的功能。移除插件前需删除控制器中的 NaughtyAttributes 属性和 using、运行时 asmdef 中的 Core 引用，再移除 manifest 依赖并由 Unity 重新解析。不要同时安装覆盖同一 Inspector 的其他插件。

验证状态：见本轮交付记录；编译与构建不能替代 Inspector/Scene 的人工视觉检查。

## 开发者优先与当前落地

所有实现优先便于维护、使用、查看和修改。2026-09-23 已统一全部 7 个场景的节点、UIManager、面板 Prefab、事件通信和资源清单；新代码不得恢复跨面板拖拽依赖、全局常驻重复 Canvas 或散落资源路径。当前规范、修改入口和验证见 scene-ui-framework.md；项目 skill 与全局副本同步。
