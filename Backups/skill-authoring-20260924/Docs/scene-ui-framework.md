# 场景节点与 UI 框架

日期：2026-09-23。用户要求覆盖**当前所有场景**，统一节点职责、Canvas/UIManager、面板 Prefab、事件通信和集中资源路径。首要标准是方便开发者维护、使用、查看和修改。功能行为保持原演示范围。

## 场景入口

7 个场景：Main、SelectScene、Game、SkillIndicatorDemo、FogOfWarDemo、SkillArchitectureDemo、Benchmark10k。

```text
<场景名>_Scene
├── 00_Bootstrap                 场景说明、迁移版本
├── 01_Systems                   各功能控制器与装配组件
├── 02_World
│   ├── Environment              地形、地图和障碍
│   └── Actors                   主角外观、观察者、视野来源
├── 03_Presentation
│   ├── Cameras
│   ├── Lighting
│   └── Effects                  指示器、迷雾显示与少量反馈节点
├── 04_UI
│   ├── Canvas - UIManager
│   │   ├── 00_HUD               常驻面板 Prefab 实例
│   │   ├── 10_Panels            功能面板挂载层
│   │   ├── 20_Popups            结算、按需面板
│   │   └── 30_Overlay           顶层提示扩展位置
│   └── EventSystem
└── 05_Debug                     后续调试视图的预留位置
```

没有把万级 ECS 对象变成 GameObject。自动验证探针仍与对应控制器放在一起，只在命令行启用；低数量场景对象通过原序列化引用连接。迷雾新增 `contentRoot` 明确来源/树木/高地的扫描作用域，层级重排不再要求它们是控制器子节点。Benchmark 的相机和单位 Mesh 也改为编辑态配置，不再运行时临时创建相机/模型。

## UI 职责与事件流

```text
本面板 Button → UIAction → UIPanel → UiEvents.Command
                                           ↓
                                   功能 HUD 适配器
                                           ↓
                                  现有业务控制器/模拟
                                           ↓
                                    功能展示快照
                                           ↓
功能 HUD 适配器 → UIManager → UiEvents.Changed → 本面板控件
```

| 类型/目录 | 负责 | 不负责 |
| --- | --- | --- |
| Foundation/UI/UIManager | 场景面板登记、显示/隐藏、按需加载、加载合并、鼠标遮挡、事件作用域 | 伤害、视野、技能计算 |
| Foundation/UI/UIPanel | 自己的 Text/RawImage/填充图绑定，订阅数据、恢复最近状态、发出命令 | 获取其他面板或直接操作 ECS |
| Foundation/UI/UIAction | 按钮的命令 ID 和参数；在本面板内查找发送入口 | 拖拽控制器、其他面板或其他 Canvas |
| Foundation/UI/UiEvents | 场景内命令与展示事件，最近值回放 | 全局静态事件总线、万实体逐对象消息 |
| CombatHud / IndicatorDemoHud / FogDemoHud / SkillDemoHud | 模块边界的事件翻译和展示数据发布 | 持有跨面板控件、拼装布局 |
| SceneNavigation | 主菜单导航意图到场景切换/退出 | 主菜单按钮布局 |

原 HUD 的旧控件字段只用于旧场景的显式迁移，已隐藏并清空；运行时不使用。旧全局 `UIManager` 只保留兼容适配，转到场景管理器，不再生成 DontDestroyOnLoad Canvas/EventSystem。新代码明确使用 `Roguelike.Foundation.UI.UIManager`，禁止用旧 `GetPanel` 绕过面板间事件约定。

事件命令例：`ability.command` 携带 `SkillDemoCommand` 的数值；`fog.command` 携带 `FogCommand`；`indicator.skill` 携带 Q/W/E 索引；`combat.restart`、`main.start`、`main.quit`。展示键例：`ability.summary`、`fog.minimap`、`combat.healthFill`。可用项目全文搜索追踪发布者和接收者。

当前每场景一个根 Canvas。将来性能分析证明高频区域需要独立重建时，可在同一 UI 根下增加子 Canvas，仍由同一管理器负责；不以统一管理为由把所有高频更新强制放在一个绘制批次。

## 面板修改与扩展

目录：`Assets/Resources/UI/Panels`。共有 15 个原场景面板 Prefab，另有一个按需的 `UI.DeveloperHelp`。静态 HUD 保持 Prefab 实例显示在场景中，未运行也能查看、调整位置和引用。

1. 修改布局：打开对应 Prefab，或在场景修改实例并按需 Apply Overrides。迁移工具不会自动覆盖已有布局。
2. 新增小面板：制作 RectTransform 根、挂 UIPanel，设置唯一 `panelId`；控件仅绑定本 Prefab 内对象。
3. 按钮挂 UIAction，填写命令 ID/参数；外部业务适配器订阅场景 `UiEvents.Command`，不让面板拖业务对象。
4. 显示数据由适配器调用 UIManager.Text/Texture/Fill 发布，面板在本地配置对应键。复杂专用控件可继承 UIPanel，并在重写生命周期时调用 base，以保留订阅/解绑和回放。
5. 常驻面板保存为场景内 Prefab 实例；按需面板通过资源清单注册后 `Show(id)`。当前动态层为 20_Popups，可在 Inspector 调整 `popupLayer`。

F1 打开开发说明面板，第一次异步加载，关闭后保留实例供当前场景重用。UIManager 用字典登记面板、集合合并加载请求；加载中关闭会记录最终可见性，完成后不会意外弹出；场景销毁会停止协程、清空缓存/事件并随场景释放实例。展示纹理的所有权留在提供者，UI 不主动释放迷雾纹理。

## 资源路径

唯一运行时加载入口：`Assets/Game/Foundation/Runtime/Resources/ResourcePaths.cs`。

文本清单：`Assets/Resources/ProjectResourceCatalog.json`。记录 `id/path/kind`；`kind=resource` 使用相对 Resources 路径且不带扩展名，`kind=scene` 记录场景名。场景实际能否切换仍由 Player 构建列表决定。

```json
{"id":"UI.DeveloperHelp","path":"UI/Panels/UI.DeveloperHelp","kind":"resource"}
```

解析后使用字典查询路径；只读清单不会预加载所有面板或音效。ResourcesMgr/ResMgr 的旧加载入口已转到同一个清单，不再自己维护路径字典或忙等 WWW。当前音频的旧路径 ID 也已登记；新增业务代码应使用语义 ID，路径只改清单。

`Tools > Roguelike > Scene Framework > Refresh Resource Catalog` 补充项目 Assets 下的 Resources 资源和面板 ID，保留已有自定义键/路径；不扫描 Unity Packages。资源改名/搬迁后需手动检查原键路径，工具不会猜测应该重定向到哪个资源。清单不是 Addressables，也没有假装实现引用计数释放或远程资源下载。

静态世界资源、材质、Mesh、配置仍通过场景/Prefab Inspector 显式引用，确保编辑态能看见；没有为了统一路径强行把整个项目搬进 Resources。Editor 搭建工具使用 AssetDatabase 的资产路径，与 Player 的按需加载清单分开。

## 迁移、备份与验证

菜单：`Tools > Roguelike > Scene Framework > Migrate All Scenes` / `Validate All Scenes`。迁移显式执行，已带 SceneLayout 标记的场景保留手改结构；后续升级只处理明确缺失的绑定。原场景/脚本/规范备份在 `Backups/scene-ui-20260923`；Editor 首次迁移也备份到 `Backups/scene-framework-editor`。

Main 原脚本存在 Start 自动跳场景和错误按钮订阅，已替换为按钮意图 + 导航适配器；SelectScene 原本的占位内容保留，没有新增角色选择玩法。Game、技能指示器、视野、技能架构均保持原功能范围。

验证已完成：

| 检查 | 结果/证据 |
| --- | --- |
| 7 场景结构、唯一 Canvas/UIManager/EventSystem、Prefab 内部引用、Missing Script | 通过；重复迁移按版本跳过已有结构，`Logs/SceneFrameworkBuild.log` |
| Windows Release 全场景构建 | 通过；`Builds/SceneFramework/SceneFramework.exe` |
| 跨场景 UI 检查 | 48 项通过，8 次场景检查覆盖 7 个场景及返回主菜单；重复加载合并、加载中关闭、缓存重开、按钮关闭、隐藏状态回放、作用域销毁、主菜单不会自动跳转且开始按钮能跳转；`Logs/SceneFrameworkPlayer.log`、`Builds/SceneFramework/scene-ui-result.json` |
| Game 玩法回归 | 20 项通过；`Logs/SceneFrameworkGame.log` |
| 技能指示器回归 | 18 项通过；`Logs/SceneFrameworkIndicator.log` |
| 迷雾回归 | 25 项通过；发现并修正来源/地形查找依赖旧层级的问题后复验通过；`Logs/SceneFrameworkFog.log` |
| 技能架构回归 | 19 项通过；`Logs/SceneFrameworkAbility.log` |
| 实际画面 | 已查看四个 Demo 的离屏截图，布局、文字、结算面板、指示器和小地图保持可用；`DemoReports/SceneFramework` |
| skill | 官方 quick_validate.py 通过，项目/全局副本同步；验证依赖仅安装到 Temp/SkillValidationDependencies |

复验全场景：运行 Player 加 `--scene-ui-smoke`。指定场景回归：`--start-scene Game --demo-smoke`、`--start-scene SkillIndicatorDemo --indicator-smoke`、`--start-scene FogOfWarDemo --fog-smoke`、`--start-scene SkillArchitectureDemo --ability-smoke`；可加 `--demo-output <绝对目录>`。

Player 退出时仍有此前已知的 ComputeBuffer GC 释放警告，本轮未解决。该改造不改变技能/战斗/视野算法，没有把旧性能数字当作 UI 重构后的性能证明。

## 设计来源

- [Unity：使用事件通道解耦](https://unity.com/how-to/scriptableobjects-event-channels-game-code)：参考发布/订阅和生命周期原则。本项目采用场景作用域 C# 事件，避免跨场景资产事件遗留和每个消息新建资源。
- [Unity：UI 性能建议](https://unity.com/how-to/unity-ui-optimization-tips)：参考 Canvas 重建隔离。统一根 Canvas 与管理职责，按测量决定是否需要子 Canvas。

没有新增第三方 UI 框架依赖。规范已同步到项目与全局 unity-requirements-first skill。
