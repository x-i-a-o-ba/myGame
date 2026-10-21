# 圆形技能指示器 Demo

状态：2026-09-22，布尔配置与交互已实现，Windows 构建和自动 Player 回归通过。

## 已确认

- 新建独立测试场景，不在 Game 万敌战斗中加入技能伤害。
- 第一版仅圆形：施法距离圈 + 鼠标落点范围。
- Q/W/E 进入瞄准，鼠标调整，左键确认，右键或 Esc 取消；确认播放简单视觉反馈。
- 网上素材仅用于 Demo，不代表最终效果；UI 与少量展示对象保存到场景或 Prefab。

## 超距设置与操作

用户确认采用布尔配置，默认允许边界施放。选中场景根节点 Skill Indicator Demo，在 IndicatorDemoController 的「超出距离行为」下调整 Clamp Out Of Range：

- true（默认）：鼠标超距时把落点中心限制在最大距离边界，左键直接施放。
- false：保留鼠标落点，超距显示红色并禁止确认；回到距离内恢复可施放。

开关可在运行时修改，也可通过公开属性 ClampOutOfRange 供未来设置界面调用；本轮未新增设置 UI 或持久化存档。

Q/W/E 或底部按钮选择三个圆形配置，鼠标在地面瞄准，左键确认，右键/Esc 取消。施放后退出瞄准并播放扩散淡出；再次按技能键重新瞄准。UI 区域/屏幕外不能确认旧落点。施法者暂时固定，测试地面为水平平面，没有移动、地形投影和伤害。

Q：距离 10、半径 2.5；W：距离 8、半径 4；E：距离 14、半径 1.5。配置单位为米/世界单位，修改 Settings 中对应资产即可。

## 选材与实现方向

- [Kenney Particle Pack](https://kenney.nl/assets/particle-pack)：已下载 CC0 透明圆环与光斑，保留来源、许可和归档哈希。用于装饰与扩散反馈。
- [NiloCat URP 投影 Shader](https://github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader)：上游有不同混合模式的投影预览，当前仅调研。它要求深度纹理，正交相机需要对应选项。
- [Click Decal Shader](https://github.com/SparkAflame/Click-Decal-Shader)：可参考其点击落点的脉冲圈展示，未安装该插件。
- 当前平面 Demo 使用透明网格和明确的几何边界，避免为独立演示改变 Game 的全局 URP 配置。复杂地形投影作为后续单独需求。

展示采用冷色细边线、低透明度填充、鼠标落点装饰圆环，确认后扩散淡出。清晰边界按实际世界半径绘制，外侧柔光不算作技能范围。

`Assets/Art/技能指示器ui` 是按英雄联盟和 DOTA2 指示类型整理的原创对照图和白色底图，入口为该目录的 `索引.md`。它还没有接到 Demo 或 Game 场景，也不包含这两个游戏的官方界面。

## 当前可查看内容

打开 Assets/Scenes/SkillIndicatorDemo.unity，或 Tools/Roguelike/Skill Indicators/Open Demo。场景中包含平面网格、静止主角占位、可编辑 HUD、距离圈与落点圈；Cast Feedback 节点默认隐藏。Assets/Game/SkillIndicatorDemo/Prefabs 保存 CastRange、TargetArea、CastFeedback 三种 Prefab；Settings 保存 Q/W/E 圆形演示配置。当前未改变 Game 场景或全局 URP 设置，也未改变主游戏构建列表。

Unity 6000.3.18f1 在隔离副本编译和 Windows 构建成功，日志 Logs/IndicatorInteractionBuild.log。运行结果 DemoReports/SkillIndicator/result.json 为 passed=true，日志 Logs/IndicatorInteractionPlayer.log 标记 INDICATOR_SMOKE_PASSED。

通过的自动检查：默认布尔值、屏幕到地面投影、范围内落点、精确边界限制与确认、关闭开关时拒绝超距、回到范围内恢复、取消、W/E 参数切换、UI 防止误用旧落点、反馈结束、重复选择不增加节点。查看 aim.png / rejected.png 确认正常/标红状态与中文 UI 显示。测试调用与键鼠共用的方法，未做人工按键鼠标逐项验收。退出仍有项目此前存在的 ComputeBuffer 释放警告。

可直接运行 Builds/SkillIndicatorDemo/SkillIndicatorDemo.exe；或打开场景点击 Play。菜单 Tools/Roguelike/Skill Indicators/Build Windows Demo 可重新构建。Prepare Interaction 只为旧静态场景补缺失组件，不覆盖已配置的布尔值和布局。本轮不包含万实体性能验收。

备份：Backups/indicator-interaction-20260922，原场景 GUID 保留。

## 维护边界

CircleSkillPreset 保存距离、半径、颜色、反馈时长；CircleIndicatorView 只显示圆形；IndicatorFeedback 播放视觉反馈；IndicatorDemoHud 发出按钮意图。场景与 Prefab 由 Editor 工具首次生成，已有资源不会自动覆盖。IndicatorDemoInput 适配输入与屏幕投影；CircleTargeting 独立计算距离规则；IndicatorDemoController 桥接瞄准状态、显示与反馈，不依赖 Game/ECS 模拟。

## 2026-09-23 场景/UI 维护更新

节点、UI 面板与资源入口按 scene-ui-framework.md 统一；三个 UI 面板为 Prefab，按钮通过 UIAction 发意图，IndicatorDemoHud 负责与业务连接。原有瞄准、截距和取消行为保持不变。
