# 万级同屏肉鸽开发文档

## 2026-10-23 冰箭散射与磐锋卫士

寒冰第二技能改为默认七支独立冰箭，首碰造成伤害并消失；新增近战战士“磐锋卫士”，通过蓄势重劈、守势回复、固定刃阵与延迟落剑作战，游戏右上角可选择并重开。配置、数值和共享模块边界见 [功能说明](volley-and-vanguard.md)，实测见 [验证记录](validation-volley-vanguard-2026-10-23.md)。

## 2026-10-22 30 波平衡、回蓝与单项属性

怪物基准生命/攻击/移速集中到 GameData 难度资源，重配 30 波数量与成长、精英和首领倍率。寒冰生命与法力随级成长，回蓝新增基础及每级配置；蓝条显示回复，属性逐项悬停，头像显示总览，技能蓝耗统一放到图标底部。修复最终波后继续刷怪的问题。当前数值见 [30 波平衡与属性明细](wave-balance-and-stat-details.md)，实际验证见 [本轮记录](validation-wave-balance-2026-10-22.md)。

## 2026-10-22 寒冰射手与 GameData

寒冰射手四技能加入逐级蓝耗，重新配置伤害、冷却与攻速收益；当时的基础法力 240、每级 +12、基础回复 3 点/秒已由上方 30 波平衡版本更新。78 份游戏配置迁入 `Assets/GameData`，按功能分类，保留数字 ID 与 GUID；正式配置、编辑示例和历史示例分开。职责、数值表、编辑入口与迁移说明见 [寒冰射手与数据规范](ashe-balance-and-gamedata.md)，实际验证见 [验证记录](validation-ashe-gamedata-2026-10-22.md)。

## 2026-10-22 装备丢弃、拾取与固定商店

参考 Dota 2：把装备拖到地面丢弃（英雄走到落点 3.3 米内放下）、拖到其他格换位、拖到打开的商店面板出售；右键地上的装备显示提示框并走过去拾取（5 米内），左键只查看。6 格装备栏改为 Dota 2 式 3 × 2。商店固定在 Game 场景出生点北侧 14 米处（半径 8 米，有商店模型和地面金色范围圈），只有走进范围才能打开商店买卖，范围外商店按钮变暗并显示距离，离开范围自动关店。说明见 [equipment-and-stats.md](equipment-and-stats.md) 第 2.6、2.7 节。合并前 Unity 6000.3.18f1 编译、303 项 Editor 检查、Windows 构建与 105 项 Player 检查通过，无失败、无跳过项；商店和地上装备截图已检查。人工操作手感、多分辨率与完整战斗性能尚未验收，见 [验证记录](validation-item-drop-shop-2026-10-22.md)。

## 2026-10-22 属性与装备系统

英雄属性改为 15 种、所有来源直接相加（无力量/敏捷/智力换算）：基础 + 成长 ×（等级 - 1）+ 天赋 + 装备。新增装备系统：击杀得金币，B 打开右下角商店 `UI.Game.Shop`，左键购买、商店打开时右键装备栏出售；底部 HUD 加宽并新增属性两列、金币与 6 格装备栏，悬停装备或属性区显示详细提示。首批 18 件装备（5 种品质），新增装备只需新建 `EquipmentDefinition`、登记编号并加入装备库。说明见 [equipment-and-stats.md](equipment-and-stats.md)。合并前 Unity 6000.3.18f1 编译、251 项 Editor 检查、Windows 构建及 74 项 Player 检查通过，无失败、无跳过项，商店实际截图已检查。人工操作手感、数值平衡与万敌完整战斗性能未验收，见 [验证记录](validation-stats-equipment-2026-10-22.md)。

## 2026-10-22 技能悬停详细提示与移动中攻击

鼠标悬停 Q/W/E/R 或被动图标时，图标上方显示 Dota 2 式提示 `UI.Game.AbilityTooltip`：名称、热键、类型、等级、施法属性、说明、逐级数值（当前级白、可升下一级绿、其他灰）、冷却与法力、附注与状态。英雄新增“移动中攻击”（寒冰射手开启）：右键移动途中自动攻击攻击距离内的敌人，边走边打。见 [enemies-and-controls.md](enemies-and-controls.md) 1 与 4.2。合并前 Unity 6000.3.18f1 编译、171 项 Editor 检查、Windows 构建及 62 项 Player 检查通过，无跳过项，提示框实际截图已检查。人工手感与多分辨率未验收，见 [验证记录](validation-ability-tooltip-2026-10-22.md)。

## 2026-10-21 左键选中敌人信息面板

左键点敌人后脚下显示金色选中圈，左上角打开 `UI.Game.TargetInfo`：头像（8 种敌人新增头像）、档位、波次与行为、生命、攻击力/攻击间隔/攻击距离/移速/经验/距离（被 Buff 修改时显示彩色百分比）、自动攻击状态、行为特性、状态效果与说明；左键空地、Esc 或 × 关闭。见 [enemies-and-controls.md](enemies-and-controls.md) 4.1。合并前 Unity 6000.3.18f1 编译、137 项 Editor 检查、Windows 构建和 49 项 Player 检查通过，目标面板实际截图已检查。原始键鼠与多分辨率交互尚未验收，见 [验证记录](validation-enemy-info-panel-2026-10-21.md)。

## 2026-10-21 敌人种类、Dota 2 式操作与 HUD

新增 8 种敌人预制体与敌人名册、英雄与敌人共用的自动攻击被动模块、Dota 2 式指令（右键移动/攻击、A 攻击移动、S 停止、H 待命）和 Dota 2 风格 HUD，见 [enemies-and-controls.md](enemies-and-controls.md)。合并前 Unity 编译、121 项 Editor 规则、Windows 构建与 42 项 Player 检查通过，并检查了实际 HUD 截图。人工操作手感、全部敌人实战与万敌性能未验收，见 [验证记录](validation-enemies-dota-hud-2026-10-21.md)。

## 2026-10-21 英雄成长分支合并验证

[英雄成长](hero-progression.md)已完成 Unity 编译、89 项 Editor 规则、Windows 构建与 31 项 Player 集成检查。完整一局、数值平衡与万敌性能仍待验收，见 [验证记录](validation-hero-progression-2026-10-21.md)。

## 2026-09-28 自由组合技能 V2

用户已批准按 [V2 设计方案](skill-composition-v2-proposal.md)实施。当前新增数字模块目录、纯数据配方、共享编译器、有限事件步骤、版本化执行库及中文步骤编辑器。5 个正式技能迁移为配方输入，旧阶段保留用于恢复，Game 继续使用共享结算核心。最新职责、迁移、限制与验证见 [V2 实施记录](skill-composition-v2-implementation.md)，该记录优先于下方旧版说明。

首版开放伤害/治疗数值覆盖；Buff 规格固定。玩家 JSON 装配 API 已接入，玩家界面、解锁/平衡、有限连锁、完整时停及 Excel 配方导入尚未实现。配方使用数字 ID；中文设计器通过“何时触发 → 怎么到达 → 影响谁 → 做哪些效果”组合。

2026-09-28 这轮只做了静态审查、隔离 Unity 编译、纯数据/编辑事务和实际 Editor 窗口检查，未进行游戏或 Player 测试，也未证明万敌战斗性能达标。这是历史验证状态，不限制后续任务自行运行适用测试。


更新日期：2026-09-24。当前技能架构入口为 [组合技能与 Buff 基础架构](ability-foundation.md)。旧 Demo 验证记录只说明当时版本。

## 已确认方向

- 从 Unity 2021 迁移到 Unity 6 系列。
- 使用 URP 渲染管线和 ECS 相关架构。
- 开发同屏一万以上的肉鸽游戏。
- 使用 3D 俯视角和 3D 模型（用户后续确认）。
- 首发 PC；一万指可战斗敌人，不含子弹与掉落物；上一轮已交付基础框架和独立性能验证场景。
- Game 是当前战斗场景：鼠标移动、自动索敌射击，Q/W/E/R 主动技能；技能操作/UI 参考英雄联盟和 DOTA2。
- 用户最新要求：以可选英雄、组合技能及通用 Buff 为底层；允许暂缓具体功能，优先架构。配置采用 ScriptableObject，技能由目标/形状/投递/多段/效果组合，不能按 QWER 固定类。E 为目标点范围伤害；Game 视野继续关闭。
- 编辑器配置的字段、枚举、提示、菜单及 Project 配置资源文件名能用中文清楚表达时优先中文；程序稳定 ID、序列化字段名和资源 GUID 保持不变。技能与 Buff 的当前中文 Inspector 和资源命名见 [组合技能架构](ability-foundation.md)。
- 后续开发由用户给大方向，AI 自主补齐普通玩法、数值和实现细节，完成实现、适用验证与汇报；根据新需求同步维护开发文档。仅真正无法推断或不可逆的事项再问用户。

## 当前项目事实

| 项目 | 检查结果 |
| --- | --- |
| Editor | ProjectSettings/ProjectVersion.txt 为 6000.3.18f1，即 Unity 6.3 |
| URP | 17.3.0；Forward+ 与全局 GraphicsSettings 已绑定，各 Quality 继承全局设置；基础构建与 Player 初始化已验证 |
| ECS | Entities 1.4.8 + Entities Graphics 1.4.21；基础框架、独立压测及 Game 战斗系统已加入 |
| 现有代码 | Assets/Scripts/ProjectBase 与 Assets/_Scripts 两套 MonoBehaviour/管理工具 |
| 场景 | Game.unity 绑定 HeroDefinition、SkillCatalog、经验与等级规则、关卡难度曲线和按配置工作的指示器；enemyCount=3000（同屏存活上限）、safeSpawnRadius=30。Main、SelectScene及独立历史场景保留；完整选人UI尚未接入 |
| 版本管理 | 2026-10-21 建立 Git 仓库，远程为公开仓库 x-i-a-o-ba/myGame；修改前检查工作区并保留可恢复状态，备份范围见根目录 README.md |

当前 Game 使用 `Roguelike.Abilities.Core`：旧固定技能类及 QWER 状态已移除，共享核心已实际接入。最新验证以 [基础架构记录](ability-foundation.md) 为准。`build4.log/player3.log/Smoke3` 属于上一版固定技能实现，不能证明本轮重构通过。万敌完整战斗性能仍未验收。

## 阅读顺序

1. [需求与验收](requirements.md)：需求范围、实现状态、变更记录。
2. [架构与迁移](architecture.md)：渲染、ECS 边界、迁移步骤。
3. [性能验收](performance.md)：同屏数量定义、测量方法和报告格式。
4. [压测场景使用说明](benchmark-guide.md)：菜单、参数、输出和测量边界。
5. [本轮验证记录](validation-2026-09-22.md)：编译、构建、短时运行证据及未验证项。
6. [Game 场景战斗](game-combat.md)：当前操作、场景默认值与验证状态。
7. [Game 旧版验证记录](validation-game-demo-2026-09-22.md)：2026-09-22 战斗逻辑、Player 集成与画面；不覆盖当前改造。旧指南路径 `game-demo.md` 保留历史兼容入口。
8. [架构规范与编辑器说明](architecture-guidelines.md)：胶水层、注释、复用边界及现行 Game 职责。
9. [编辑器工具验证记录](validation-architecture-editor-2026-09-22.md)：skill 校验、包解析、构建与 Player 回归。
10. [Game 场景维护说明](scene-authoring.md)：场景节点、布局修改入口、动态属性、迁移与验证。
11. [技能指示器独立演示](skill-indicator-demo.md)：演示场景和历史验证；Game 接入状态以第 16 项为准。
12. [迷雾与视野 Demo](fog-of-war-demo.md)：独立双阵营场景、指定敌人视野预制体、万级显隐目标与验证。
13. [视野系统调研](fog-of-war-research.md)：Dota/RTS 参考资料、规则边界与性能设计。
14. [AI 主控与多代理游戏开发调研及操作手册（历史）](ai-multi-agent-game-dev-workflow-draft.md)：旧方案研究，不再作为执行规则。
15. [项目开发约定](../AGENTS.md)：AI 按目标自主选择方案、工具、技能与协作方式。旧 [AI 团队手册](ai-team/README.md)仅作历史参考。
16. [Game 寒冰技能与指示器](ashe-game-integration.md)：四份技能配置、W/E/R 瞄准、E 范围伤害、Game 视野封存及本轮验证状态。
17. [技能配置工作台与分类设计器](skill-authoring-workbench.md)：独立窗口、六种模板、效果/阶段组合、Unity 单位范围句柄、三维抛物线投递与共享预览、保存和技能库登记。
18. [项目整理](project-overview.md)：一页式目录、模块、完成度、遗留代码建议与后续顺序。
19. [英雄成长、技能等级与波次难度](hero-progression.md)：Dota 2 式经验/技能点/天赋、技能等级、30 波难度曲线、胜负、操作与验证状态。
20. [敌人种类、Dota 2 式操作与 HUD](enemies-and-controls.md)：自动攻击被动模块、右键/A/S/H 指令、8 种敌人预制体与名册、新版 HUD 面板、左键选中敌人信息面板、技能悬停提示、移动中攻击与验证状态。
21. [属性与装备](equipment-and-stats.md)：15 种直接相加的属性、合成规则与上限、装备与装备库、首批 18 件、新增装备步骤、金币、商店操作与验证状态。

## 文档维护规则

随实现更新用户方向、关键决定和需求状态。普通细节由 AI 根据项目事实自主确定；文档按任务需要维护，不设置写代码前的审批步骤。

实现完成后执行与改动相称的验证并返工，同步更新架构、验证证据和需求状态；未运行的检查明确写「未验证」及具体原因，不默认交给用户测试。遇到与旧需求冲突的新要求，记录差异、主 AI 裁决及依据，不静默覆盖或冒充用户确认。性能目标不得写成实测成绩。

## 技能架构基础演示

[使用与配置](skill-architecture-demo.md)：独立 SkillArchitectureDemo，三个主动技能、两种被动、共享 Buff 管线和万目标验证。设计依据见 [架构调研与批准方案](skill-architecture-proposal.md)。

## 统一场景与 UI 框架

[场景节点与 UI 使用规范](scene-ui-framework.md)：覆盖全部 7 个场景，单根 Canvas/UIManager、面板 Prefab、场景事件、集中资源文本清单与按需加载。当前节点和面板修改入口以此文档为准。

## 2026-09-24 组合架构验证

最终Unity编译、41项新规则、23项旧核心回归、Windows构建及3000敌人Player检查通过；主AI核对URP离屏指示器/HUD截图。证据已保存到 Logs/AbilityFoundation，详见 ability-foundation.md。未进行万敌完整战斗性能测量。




## 英雄与技能配置文档

[英雄与技能 Excel 目录](游戏配置/README.md)：唯一 ID 查询、英雄属性、技能参数、槽位装配和阶段明细。编辑器入口为 Tools/Roguelike/技能设计器。



2026-09-28：配置主 ID 改为数字：技能 1000–1999、英雄 2000–2999、怪物 3000–3999。见 [配置目录](游戏配置/README.md)；原字符串键仅保留内部兼容。

## 2026-09-28 自由组合技能设计提案

用户希望先讨论架构：多个效果/选项按数字 ID 自由拼装，未来允许玩家组合。新提案见 [自由组合技能 V2](skill-composition-v2-proposal.md)，含第一方资料调研、现有代码证据、模块目录/纯数据配方/类型化事件/不可变版本计划、玩家授权、3D空间合同、组合预算和分阶段迁移。

本轮范围为调研与设计，仅更新文档。未修改代码、资源、场景、编号登记或 Excel。新效果/Buff编号段、流程上限、时停时钟规则均为 AI 建议，未登记、未实现、未运行验证。当前实现状态仍以原架构记录为准。
