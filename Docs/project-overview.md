# 项目整理（2026-10-21）

本文是给新加入的开发者或 AI 的一页式总览：项目现在是什么、代码在哪、哪些已完成、哪些是旧代码、接下来做什么。详细规则仍以 [README](README.md) 阅读顺序中的各专题文档为准。

## 项目是什么

Unity 6.3（6000.3.18f1）+ URP 17.3.0 + Entities 1.4.8 / Entities Graphics 1.4.21 的 3D 俯视角肉鸽，目标是 PC 上同屏一万只可战斗敌人。主场景 `Assets/Scenes/Game.unity`，当前英雄“寒冰射手”，Q/W/E/R 为冰霜专注 / 冰霜扇击 / 冰霜冲击 / 冰晶巨箭。

## 目录与模块

| 目录 | 程序集 | 职责 |
| --- | --- | --- |
| `Assets/Game/Abilities/Core` | Roguelike.Abilities.Core | 技能、Buff、效果的 ScriptableObject 定义与编译；通用施法、多段、投递、几何；英雄成长（`Progression/`）。不依赖 UI、输入或场景 |
| `Assets/Game/Abilities/Editor` | Roguelike.Abilities.Editor | 中文 Inspector、技能验证、演示资源工具 |
| `Assets/Game/Runtime` | Roguelike.Runtime | Game 战斗宿主：会话、ECS 系统、波次、HUD、输入、指示器；压测与迷雾演示 |
| `Assets/Game/Editor` | Roguelike.Editor | 场景构建与迁移、战斗/成长验证、压测工具、难度曲线 Inspector |
| `Assets/Game/Foundation` | Roguelike.Foundation | 场景导航、UIManager/UIPanel/UIAction、资源目录 |
| `Assets/Game/SkillAuthoring` | — | 技能配置工作台（独立窗口和场景） |
| `Assets/Game/FogOfWar`、`SkillIndicatorDemo` | — | 独立迷雾与指示器演示，Game 中视野已关闭 |
| `Assets/Game/Skills/Foundation` | 资源 | 正式技能、Buff、效果、两份英雄、技能库 |
| `Assets/Game/Progression` | 资源 | 经验与等级规则、关卡难度曲线 |
| `Assets/Resources/UI/Panels` | 资源 | 按需加载的 UI 面板，ID 登记在 `Resources/ProjectResourceCatalog.json` |
| `Assets/Scripts/ProjectBase`、`Assets/_Scripts` | 无 asmdef | 2021 版遗留的 MonoBehaviour 管理工具（单例、事件、输入、对象池、A*、UI 等，共 24 个脚本），当前 Game 代码不引用 |
| `Docs`、`Tools`、`Backups`、`BenchmarkReports` | — | 文档、辅助脚本、迁移备份和压测报告 |

核心约定（见 [AGENTS.md](../AGENTS.md)）：技能必须由模块组合，不按 QWER 或英雄写分支；定义资源只存配置，运行状态归拥有者；`AbilityUnit` 是生命和属性的唯一权威；Inspector 用中文；每次改动同步更新需求、架构和验证文档。

## 当前完成度

| 功能 | 状态 |
| --- | --- |
| URP、ECS 框架、万级压测场景 | 已完成，历史构建与 Player 验证见 [requirements.md](requirements.md) |
| Game 移动、自动攻击、死亡重开 | 已完成 |
| 组合技能 / Buff 架构、V2 配方、中文编辑器、技能工作台 | 已完成，见 [ability-foundation.md](ability-foundation.md)、[V2 实施记录](skill-composition-v2-implementation.md) |
| 寒冰射手四技能与指示器 | 已完成，见 [ashe-game-integration.md](ashe-game-integration.md) |
| Dota 2 式升级、技能点、技能等级、天赋、波次难度、胜负 | 2026-10-21 新增代码与配置，**未在 Unity 中编译和试玩**，见 [hero-progression.md](hero-progression.md) |
| 万敌完整战斗性能 | 未在 Player 中实测 |
| 选人界面、经验掉落、敌人种类/技能、局外成长、存档、音效、正式美术 | 未开始 |

## 遗留代码建议（未执行）

`Assets/Scripts/ProjectBase` 与 `Assets/_Scripts` 没有程序集定义，会编进默认的 Assembly-CSharp，新代码不依赖它们。功能已分别被 `Roguelike.Foundation`（场景、UI、资源）和 ECS 系统替代。建议在确认 Main、SelectScene 两个旧场景不再需要后，单独一次提交将它们移到 `Backups/` 或删除，并检查场景内是否还挂着这些脚本。本轮没有删除或移动任何旧代码。

## 建议的后续顺序

1. 在 Unity 中打开项目，跑 **Tools/Roguelike/验证/战斗与成长（不构建）**，修正编译或验证问题，试玩一局调节经验和难度曲线。
2. Player 中把同屏上限调到一万，按 [performance.md](performance.md) 测帧时间；必要时为索敌和范围技能加空间划分。
3. 经验球掉落与拾取范围；敌人种类（远程、冲锋）与精英/首领技能，复用技能组合架构。
4. 选人界面与第二个完整英雄，验证成长系统对其他英雄的复用。
5. 局外成长、存档、音效与正式美术资源。
