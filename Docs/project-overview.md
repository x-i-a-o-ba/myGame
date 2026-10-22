# 项目整理（2026-10-21）

本文是给新加入的开发者或 AI 的一页式总览：项目现在是什么、代码在哪、哪些已完成、哪些是旧代码、接下来做什么。详细规则仍以 [README](README.md) 阅读顺序中的各专题文档为准。

## 项目是什么

Unity 6.3（6000.3.18f1）+ URP 17.3.0 + Entities 1.4.8 / Entities Graphics 1.4.21 的 3D 俯视角肉鸽，目标是 PC 上同屏一万只可战斗敌人。主场景 `Assets/Scenes/Game.unity`，当前英雄“寒冰射手”，Q/W/E/R 为冰霜专注 / 冰霜扇击 / 冰霜冲击 / 冰晶巨箭。

## 目录与模块

| 目录 | 程序集 | 职责 |
| --- | --- | --- |
| `Assets/Game/Abilities/Core` | Roguelike.Abilities.Core | 技能、Buff、效果的 ScriptableObject 定义与编译；通用施法、多段、投递、几何；英雄成长（`Progression/`）、属性（`Stats/`）与装备（`Equipment/`）。不依赖 UI、输入或场景 |
| `Assets/Game/Abilities/Editor` | Roguelike.Abilities.Editor | 中文 Inspector、技能验证、演示资源工具 |
| `Assets/Game/Runtime` | Roguelike.Runtime | Game 战斗宿主：会话、ECS 系统、波次、HUD、输入、指示器；压测与迷雾演示 |
| `Assets/Game/Editor` | Roguelike.Editor | 场景构建与迁移、战斗/成长验证、压测工具、难度曲线 Inspector |
| `Assets/Game/Foundation` | Roguelike.Foundation | 场景导航、UIManager/UIPanel/UIAction、资源目录 |
| `Assets/Game/SkillAuthoring` | — | 技能配置工作台（独立窗口和场景） |
| `Assets/Game/FogOfWar`、`SkillIndicatorDemo` | — | 独立迷雾与指示器演示，Game 中视野已关闭 |
| `Assets/Game/Skills/Foundation` | 资源 | 正式技能、Buff、效果、两份英雄、技能库 |
| `Assets/Game/Progression` | 资源 | 经验与等级规则、关卡难度曲线 |
| `Assets/Game/Enemies` | 资源 | 8 个敌人种类预制体、敌人自动攻击模块、材质与敌人名册（代码在 `Runtime/Combat/Enemies`） |
| `Assets/Game/Equipment` | 资源 | 装备库、18 件装备定义与图标（2026-10-22，程序生成图标） |
| `Assets/Game/UI` | 资源 | Dota 2 风格 HUD 的精灵与图标（程序生成 PNG） |
| `Assets/Resources/UI/Panels` | 资源 | 按需加载的 UI 面板，ID 登记在 `Resources/ProjectResourceCatalog.json` |
| `Assets/Scripts/ProjectBase`、`Assets/_Scripts` | 无 asmdef | 2021 版遗留的 MonoBehaviour 管理工具（单例、事件、输入、对象池、A*、UI 等，共 24 个脚本），当前 Game 代码不引用 |
| `Docs`、`Tools`、`Backups`、`BenchmarkReports` | — | 文档、辅助脚本、迁移备份和压测报告 |

核心约定（见 [AGENTS.md](../AGENTS.md)）：技能必须由模块组合，不按 QWER 或英雄写分支；定义资源只存配置，运行状态归拥有者；`AbilityUnit` 是生命和属性的唯一权威；Inspector 用中文；每次改动同步更新需求、架构和验证文档。

## 当前完成度

| 功能 | 状态 |
| --- | --- |
| URP、ECS 框架、万级压测场景 | 已完成，历史构建与 Player 验证见 [requirements.md](requirements.md) |
| Game 移动、自动攻击、死亡重开 | 已完成；2026-10-21 改为 Dota 2 式指令（右键移动/攻击、A 攻击移动、S 停止、H 待命、转身速度），自动攻击改为英雄与敌人共用的被动模块，Unity 编译、121 项 Editor 规则、Windows 构建及 42 项 Player 检查通过；人工手感未验收，见 [enemies-and-controls.md](enemies-and-controls.md) |
| 敌人种类与预制体 | 2026-10-21 新增 8 种（近战、快速、远程、冲锋、自爆、坦克、首领、远程精英），名册按波次与权重出场，已通过名册校验与 Player 混刷检查，全部特殊行为实战未验收 |
| Dota 2 风格 HUD | 2026-10-21 重做底部技能栏、顶栏首领血条与天赋树，Player 实际 HUD 截图已检查，更多分辨率与人工交互未验收 |
| 左键选中敌人信息面板 | 2026-10-21 新增左上角目标面板（头像、档位、生命、属性与修改百分比、攻击状态、行为特性、Buff、说明）与 8 张敌人头像；Unity 编译、137 项 Editor 检查、Windows 构建及 49 项 Player 检查通过，目标面板截图已检查；原始键鼠交互未验收 |
| 技能悬停提示、移动中攻击 | 2026-10-22 新增 Dota 2 式技能 / 被动悬停提示（逐级数值、冷却法力、状态）与“移动中攻击”（右键移动途中边走边打）；Unity 编译、171 项 Editor 检查、Windows 构建及 62 项 Player 检查通过，提示框截图已检查；人工手感未验收 |
| 属性与装备、金币、商店 | 2026-10-22 新增 15 种直接相加的属性（无主属性换算）、18 件可扩展装备、击杀金币、B 键商店与 6 格装备栏、装备/属性悬停提示；Unity 编译、运行、画面与数值平衡未验证，见 [equipment-and-stats.md](equipment-and-stats.md) |
| 组合技能 / Buff 架构、V2 配方、中文编辑器、技能工作台 | 已完成，见 [ability-foundation.md](ability-foundation.md)、[V2 实施记录](skill-composition-v2-implementation.md) |
| 寒冰射手四技能与指示器 | 已完成，见 [ashe-game-integration.md](ashe-game-integration.md) |
| Dota 2 式升级、技能点、技能等级、天赋、波次难度、胜负 | 2026-10-21 Unity 编译、89 项 Editor 规则、Windows 构建及 31 项 Player 集成检查通过；完整实玩和数值平衡未验收，见 [hero-progression.md](hero-progression.md) |
| 万敌完整战斗性能 | 未在 Player 中实测 |
| 选人界面、经验掉落、敌人技能（复用技能组合）、局外成长、存档、音效、正式美术与动画 | 未开始 |

## 遗留代码建议（未执行）

`Assets/Scripts/ProjectBase` 与 `Assets/_Scripts` 没有程序集定义，会编进默认的 Assembly-CSharp，新代码不依赖它们。功能已分别被 `Roguelike.Foundation`（场景、UI、资源）和 ECS 系统替代。建议在确认 Main、SelectScene 两个旧场景不再需要后，单独一次提交将它们移到 `Backups/` 或删除，并检查场景内是否还挂着这些脚本。本轮没有删除或移动任何旧代码。

## 建议的后续顺序

1. 在 Unity 中打开项目，跑 **Tools/Roguelike/验证/战斗与成长（不构建）**，修正编译或验证问题；检查新 HUD 画面、左键选中敌人信息面板、技能悬停提示、移动中攻击手感、商店与装备栏、装备与属性提示、敌人预制体外形和 Dota 2 式操作手感，试玩一局调节经验、难度曲线和各敌人倍率。
2. Player 中把同屏上限调到一万，按 [performance.md](performance.md) 测帧时间；必要时为索敌和范围技能加空间划分。
3. 经验球掉落与拾取范围；精英/首领技能（复用技能组合架构）；敌人之间的避让与分离。
4. 选人界面与第二个完整英雄，验证成长系统对其他英雄的复用。
5. 局外成长、存档、音效与正式美术资源。
