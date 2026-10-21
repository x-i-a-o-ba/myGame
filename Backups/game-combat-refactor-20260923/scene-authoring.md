# Game 场景维护说明

2026-09-22：用户要求优先在场景或 Prefab 中建立能预先配置的 UI 和少量物体，确保后续开发易理解、易调试、易修改。本轮范围已明确为当前场景迁移，不需要重复确认。

## 修改入口

打开 Assets/Scenes/Game.unity，在 Game Demo 下修改：

| 节点 | 用途与修改方式 |
| --- | --- |
| Scene Views/Arena | 场地 Plane；修改 Transform、MeshRenderer 材质 |
| Scene Views/Player/Facing | 主角胶囊与朝向标记；修改网格、比例、材质及朝向标记局部位置 |
| Scene Views/Move destination | 移动目标标记；编辑状态默认隐藏，可临时启用调整样式 |
| Combat HUD/Title、Controls | 标题和操作说明，直接修改 Text、字体、颜色、RectTransform |
| Combat HUD/Bottom HUD | 角色状态、血条、QWER 占位和战斗统计；直接编辑布局与样式 |
| Combat HUD/Defeat | 结算面板；默认隐藏，编辑时临时启用查看，保存前恢复隐藏 |
| Demo EventSystem | 场景已有的 UI 输入系统，不在开局重复生成 |

CombatHud Inspector 中保存显示控件、输入遮挡区域、结算面板和按钮引用；控制器 Inspector 保存主角、移动标记、HUD 和 ECS 网格引用。移动或重命名子节点不影响这些序列化引用；替换节点需要重新绑定引用。

运行时由状态驱动的属性包括主角位置/朝向、相机跟随与缩放、移动标记位置/显隐、血条填充、生命/计时/统计文本和结算显隐。固定标题、技能占位、按钮文案及控件布局不会在开局重建。场地视觉尺寸与控制器 arenaHalfSize（游戏边界）分别配置，修改场地大小时两处保持一致。

## 结构与生命周期

- 场景拥有 UI、地面、主角、标记、EventSystem；退出场景由 Unity 清理。单独销毁控制器只释放会话实体和回调，不删除这些可编辑对象。
- CombatHud 只接收 CombatHudState 展示快照，发出 RestartRequested；控制器负责连接命令并转换 ECS 数据。UI 不持有 World/Entity，也不直接调用控制器。
- 一万敌人和弹体继续使用 ECS 批量实例化；网格直接引用序列化资源，运行时无需临时创建基础 GameObject 获取 Mesh。
- HUD 使用项目内 NotoSansSC.ttf 和上游 OFL 许可，不依赖运行时创建系统字体。当前只有一个场景使用，直接保存场景对象；后续出现复用需求时提取 Prefab。
- CombatHudSceneBuilder 和 CombatSceneAuthoring 仅位于 Editor 程序集，负责初次迁移。Prepare Game Demo Scene 不覆盖已有 HUD 布局、已有资源引用或已迁移场景的镜头/灯光配置；缺失引用会报错，避免运行时偷偷生成替代树。

## 验证记录

- Unity 6000.3.18f1 隔离副本编译、战斗逻辑验证与 Windows Release 构建通过，日志 Logs/SceneAuthoringBuild.log。
- 重新打开已保存场景，验证引用和字体持久化；人为修改标题位置后重复迁移，修改保留且节点数不变。日志 Logs/SceneAuthoringValidation.log，SCENE_AUTHORING_VALIDATED。
- Player 回归通过：初始 10000 敌人、鼠标投影/UI 拦截、移动中攻击、伤害击杀、死亡停止；调用已保存按钮的 onClick 验证 HUD 事件重开路径。重开前后节点数不变，控制器销毁后 ECS 会话清理且场景 HUD 保留。
- 报告 DemoReports/SceneAuthoring/result.json：26 次发射、24 次命中/击杀，passed=true。查看 combat.png 和 defeat.png 确认中文字体、HUD 和结算画面正常。自动按钮事件检查不等同于人工鼠标点击验收；未做原生 Editor 交互式视觉检查。
- 未进行正式 FPS 性能认证；隐藏窗口中显示的 FPS 不作为性能成绩。退出时仍有此前的 ComputeBuffer 释放警告。
- Game.unity GUID 保持不变；迁移前代码与场景备份在 Backups/scene-authoring-20260922。回退需要同时恢复场景与匹配代码，不要只恢复其中一项。

## 2026-09-23 节点/UI 迁移

当前层级与面板入口以 scene-ui-framework.md 为准。Game HUD 已位于场景 04_UI 的根 Canvas，控制器位于 01_Systems；相机、世界对象与效果按功能分组。原固定布局保留，面板改为 Prefab，旧文档中的控制器子节点路径仅对应此前版本。

## 2026-09-23 寒冰技能与迷雾接入

Game 的 `02_World > Environment` 下新增可编辑的 `High Ground Region - Game` 和三棵 `Tree - Game`。高地的 `FogHeightRegion.size/level` 与可见平台 Transform 对齐；树的 `FogObstacle.radius/blocking/visual` 决定逻辑遮挡与探索记忆。Arena 与高地使用 FogGround shader 材质，迷雾贴图由 Game 会话提供。新增、删除或移动地形对象后重开本局以重建网格；若运行时工具直接修改，则调用 `CombatFogSession.RefreshTerrain()`。

`01_Systems > Game Demo` 的 Inspector 现在保存 `abilitySettings`、`fogSmoothingMaterial`、`fogTerrainRoot`、昼夜与 `allVisibleForLoadCheck`。Q/W/E/R 的默认数值是本项目设计值，重开后生效。常态关闭全可见，做万敌可见负载验收时才单独开启。`UI.Game.BottomHUD` 的四个技能状态文字有 `UIPanel` 绑定，显示值由 `CombatHud` 的快照更新；固定布局仍在 Prefab 中编辑。详细操作见 [接入说明](ashe-game-integration.md)。

本次接入已在独立验证副本完成 Unity 编译、Windows 构建和 Game 场景基础 Player 检查，输出日志与截图见[寒冰技能与迷雾接入](ashe-game-integration.md)。2026-09-22 的旧记录本身不覆盖新技能和迷雾；专项行为与正式性能仍按各自证据记录。
