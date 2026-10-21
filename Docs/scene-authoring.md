# Game 场景维护说明

## 2026-10-21 敌人名册、敌人预制体与 Dota 2 式 HUD（当前）

- `01_Systems > Game Combat` 的 `GameCombatController` 新增两个引用：“敌人名册”指向 `Assets/Game/Enemies/敌人名册.asset`，“敌方远程弹体材质”指向 `Assets/Game/Enemies/Materials/敌方弹体.mat`。名册为空时只刷旧的统一敌人。
- 敌人外形在 `Assets/Game/Enemies/Prefabs/` 的预制体里改（子物体网格会合并成一个 ECS 网格，第一个 MeshRenderer 的材质决定颜色）；预制体不会被放进场景，也不会实例化成 GameObject。数值、行为与攻击模块在预制体根节点的 `EnemyArchetype` 上改，出场规则在名册里改。
- HUD 布局在 `Assets/Resources/UI/Panels/` 的面板预制体里改：`UI.Game.BottomHUD`（底部技能栏）、`UI.Game.Title`（顶栏与首领血条）、`UI.Game.Controls`、`UI.Game.Progression`、`UI.Game.Talent`（天赋树）、`UI.Game.TargetInfo`（左键选中敌人后的左上角信息面板，按需加载，绑定键见 `CombatHud.TargetKeys`）。控件通过面板上的 `UIPanel` 绑定键接收数据，改名或删控件时同步调整绑定列表；按钮用 `UIAction` 发命令。场景里 BottomHUD/Title/Controls 实例覆盖了锚点和尺寸，改布局时注意实例覆盖。
- 美术资源在 `Assets/Game/UI/Sprites`（九宫格边框已在导入设置中配置）和 `Assets/Game/UI/Icons`。技能图标在 SkillDefinition 的“HUD 技能栏图标”，头像与被动图标在 HeroDefinition 和自动攻击模块上；敌人头像在敌人预制体 `EnemyArchetype` 的“头像”字段。
- 以上 YAML 为手写，**未在 Unity 中打开验证**；首次打开时如有丢失引用，按本节重新指定即可。详细说明见 [enemies-and-controls.md](enemies-and-controls.md)。

## 2026-09-24 当前技能架构（优先于下方历史章节）

用户要求组合地基优先，已移除Game固定QWER实现，当前契约见 [ability-foundation.md](ability-foundation.md)。`HeroDefinition → SkillDefinition → Stage[] → EffectDefinition[]`，Buff独立；Core无UI依赖，Game采用AbilityUnit唯一属性权威。GameCombatSession负责实体与Blob生命周期，Controller只装配输入/展示，CombatSimulationSystem处理战斗宿主行为，技能由AbilityExecution调度。现有行为差异、未实现内容与本轮验证统一见新文档。下文关于四份专用配置和旧验证的段落为历史记录。


2026-09-23 更新：Game 是持续开发的战斗场景，场景和 Prefab 应优先保存可编辑的 UI、主角外观与技能预览。下面保留 2026-09-22 的迁移与验证历史，现行结构以[场景/UI 框架](scene-ui-framework.md)和本页末尾新接入节为准。

## 修改入口

打开 `Assets/Scenes/Game.unity`。旧表记录迁移前的节点称谓，当前统一层级见后文：

| 节点 | 用途与修改方式 |
| --- | --- |
| Scene Views/Arena | 场地 Plane；修改 Transform、MeshRenderer 材质 |
| Scene Views/Player/Facing | 主角胶囊与朝向标记；修改网格、比例、材质及朝向标记局部位置 |
| Scene Views/Move destination | 移动目标标记；编辑状态默认隐藏，可临时启用调整样式 |
| Combat HUD/Title、Controls | 标题和操作说明，直接修改 Text、字体、颜色、RectTransform |
| Combat HUD/Bottom HUD | 角色状态、血条、QWER 占位和战斗统计；直接编辑布局与样式 |
| Combat HUD/Defeat | 结算面板；默认隐藏，编辑时临时启用查看，保存前恢复隐藏 |
| Demo EventSystem | 场景已有的 UI 输入系统，不在开局重复生成 |

当前 `CombatHud` 通过场景 UIManager 的事件与面板控件通信；控制器 Inspector 保存主角、移动标记、HUD、技能指示器、四份技能配置和 ECS 网格引用。移动或重命名子节点不影响序列化引用；替换节点或资产需要重新绑定。

运行时由状态驱动的属性包括主角位置/朝向、相机跟随与缩放、移动标记位置/显隐、技能指示器、血条填充、生命/计时/统计文本和结算显隐。技能槽和控件布局应保存在面板 Prefab 中，不在开局重建。场地视觉尺寸与控制器 `arenaHalfSize`（游戏边界）分别配置，修改场地大小时两处保持一致。

## 结构与生命周期

- 场景拥有 UI、地面、主角、标记、EventSystem；退出场景由 Unity 清理。单独销毁控制器只释放会话实体和回调，不删除这些可编辑对象。
- CombatHud 只接收 CombatHudState 展示快照，发出重开和技能槽意图；控制器负责连接命令并转换 ECS 数据。UI 不持有 World/Entity，也不直接调用控制器。
- 一万敌人和弹体继续使用 ECS 批量实例化；网格直接引用序列化资源，运行时无需临时创建基础 GameObject 获取 Mesh。
- HUD 使用项目内 NotoSansSC.ttf 和上游 OFL 许可，不依赖运行时创建系统字体。当前固定面板是场景中的 Prefab 实例，修改布局优先打开对应 Prefab。
- CombatHudSceneBuilder 和 CombatSceneAuthoring 仅位于 Editor 程序集，负责显式搭建/迁移。历史 `Prepare Game Demo Scene` 操作不能作为新技能指示器与配置接线完成的证据；缺失引用应报错，避免运行时偷偷生成替代树。

## 2026-09-22 基础版验证记录（历史）

- Unity 6000.3.18f1 隔离副本编译、战斗逻辑验证与 Windows Release 构建通过，日志 Logs/SceneAuthoringBuild.log。
- 重新打开已保存场景，验证引用和字体持久化；人为修改标题位置后重复迁移，修改保留且节点数不变。日志 Logs/SceneAuthoringValidation.log，SCENE_AUTHORING_VALIDATED。
- Player 回归通过：初始 10000 敌人、鼠标投影/UI 拦截、移动中攻击、伤害击杀、死亡停止；调用已保存按钮的 onClick 验证 HUD 事件重开路径。重开前后节点数不变，控制器销毁后 ECS 会话清理且场景 HUD 保留。
- 报告 DemoReports/SceneAuthoring/result.json：26 次发射、24 次命中/击杀，passed=true。查看 combat.png 和 defeat.png 确认中文字体、HUD 和结算画面正常。自动按钮事件检查不等同于人工鼠标点击验收；未做原生 Editor 交互式视觉检查。
- 未进行正式 FPS 性能认证；隐藏窗口中显示的 FPS 不作为性能成绩。退出时仍有此前的 ComputeBuffer 释放警告。
- Game.unity GUID 保持不变；迁移前代码与场景备份在 Backups/scene-authoring-20260922。回退需要同时恢复场景与匹配代码，不要只恢复其中一项。

## 2026-09-23 节点/UI 迁移

当前层级与面板入口以 scene-ui-framework.md 为准。Game HUD 已位于场景 04_UI 的根 Canvas，控制器位于 01_Systems；相机、世界对象与效果按功能分组。原固定布局保留，面板改为 Prefab，旧文档中的控制器子节点路径仅对应此前版本。

## 2026-09-23 上次寒冰技能与迷雾接入（历史）

Game 曾在 `02_World > Environment` 增加高地与三棵树以演示迷雾遮挡，Arena 曾使用 FogGround 材质。以下旧说明只用于追溯此前 Game 迷雾版本；本轮 Game 关闭视野后，这些组件不参与 Game 视野判定。独立 FogOfWarDemo 保留原功能。

此前 `01_Systems > Game Demo` Inspector 保存内联 `abilitySettings`、迷雾材质、昼夜与全可见开关。本轮以四份 ScriptableObject 配置与固定指示器实例替代这些 Game 入口。`UI.Game.BottomHUD` 的四个技能状态文字仍由 `CombatHud` 快照更新，布局在 Prefab 中编辑。

此前版本的 Unity 编译、Windows 构建和 Player 检查是历史证据；它们不验证本轮指示器、E 范围伤害或关闭 Game 视野。

## 当前 Game 技能场景接线

- `01_Systems > Game Combat`：`GameCombatController` 已引用 `Assets/Game/Skills` 下 Q/W/E/R 四份技能配置、`CombatHud` 和 `GameAbilityIndicator`；敌人数量使用场景序列化值 **3000**，`safeSpawnRadius` 为 **30**，不要因脚本默认 10000 或旧测试报告覆盖。
- `03_Presentation > Effects > GameAbilityIndicator`：已固定放置 `Assets/Game/Skills/Indicators` 下两个干净的圆环 Prefab 实例（距离与 E 落点范围）及一个 `LineRenderer`。W 扇形、R 直线轮廓和 E 两个圆从同一局技能快照读取数值；父节点保持单位缩放。指示器组件只修改显示，不结算伤害。
- `04_UI > Canvas - UIManager > 00_HUD`：在 `UI.Game.BottomHUD` 的技能槽上使用 `Button` + `UIAction` 发送 `combat.ability` 数值 0–3；面板不引用战斗控制器或 ECS。其他布局与资源按[场景/UI 框架](scene-ui-framework.md)维护。
- `02_World > Environment`：Game 地面已改用 `Assets/Game/Materials/GameGround.mat`，场景中旧视野组件已移除；旧高地/树木若保留展示，也不提供视野。独立 `FogOfWarDemo` 场景和资产不受影响。Game 旧迷雾逻辑仅封存于 `CombatFogSession.cs`，当前控制器不创建它。

本轮场景引用、Prefab 实例和地面材质已迁移；主 AI 在隔离副本运行 Unity 批处理迁移，通过日志 `D:\UnityProject\myGame_validation_20260923\migration2.log` 的 `GAME_COMBAT_MIGRATED` 与正常退出记录，生成文件已复制回主项目。最终版 CombatValidation、Windows Player 构建与 3000 敌人技能 Player 冒烟通过（同目录 `build4.log`、`player3.log`、`Smoke3/result.json`）；`Smoke3/e-aim.png` 可见 E 双圆。右键按住取消后的移动抑制已静态修复，但真实鼠标输入未测。主项目 Editor 未单独运行，交互式 Inspector/Scene 视觉及万敌完整战斗性能未验证。当前场景操作见[Game 战斗指南](game-combat.md)，技术边界见[接入说明](ashe-game-integration.md)。

