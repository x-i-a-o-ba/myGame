# myGame 开发文档

> 自 2026-11-03 起，用户指定 [关卡之外网站](https://mygame.mighty-seal-7217.chatgpt.site/) 为后续开发的统一文档依据。请先读 [当前开发基准与文档总索引](https://mygame.mighty-seal-7217.chatgpt.site/?doc=cfba1d53-4cea-44b6-a1de-82001812a3d9)。本目录为迁移时保留的离线副本，部分正文仍含旧三关措辞，网站版本说明已标明适用范围；后续不要用本地旧稿覆盖网站的新设计。网站与 Unity 资源不会自动同步，完成开发后须更新对应文档与验证状态。

当前版本是两幕路线图远征肉鸽（类似《杀戮尖塔》）：Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8；实际依赖以工程文件为准。选择英雄和首图后依次进入两张战场，每张战场是一幕分支路线图，包含战斗、精英、商店、事件、营火与首领节点。

日常文档集中为以下八个入口。旧需求和历次验证已于 2026-11-02 移出仓库（需要时从 git 历史提交 `0a6ef8f` 恢复 `Docs/History`），不能用旧 Dota 操作、天赋、30 波或固定商店覆盖当前规则。

| 入口 | 内容 |
| --- | --- |
| 本页：项目总览 | 工程入口、目标、开发约定 |
| [玩法与数值](gameplay-and-numbers.md) | 两幕路线图、遭遇战规则、导演曲线、十五级经验与局内状态 |
| [英雄与技能](heroes-and-skills.md) | 两位英雄、八技能、十六形态与共享能力 |
| [怪物与刷怪](monsters-and-director.md) | 两图怪物、七类技能模块、词缀与首领 |
| [装备与遗物](equipment-and-relics.md) | 十六装备、八遗物、商店与战利品 |
| [操作与界面](controls-and-ui.md) | WASD、Q/E/R/F、暂停、HUD 和反馈 |
| [工程与工具](engineering-and-tools.md) | 职责、数据所有权、编辑器、构建与迁移 |
| [验证记录](validation.md) | 本轮实际检查、性能数字和未验证范围 |

项目从 `Assets/Scenes/Main.unity` 启动；选人选图在 `SelectScene`，战斗在 `Game` 与 `EmberRift`。技能工作台保留专用编辑场景。配置集中在 `Assets/GameData`，游戏代码位于 `Assets/Game`。

首批同屏硬上限为 4,000，万级是后续目标，不能用基准场景或数量统计代替完整战斗 Player 实测。标准局约十五分钟是目标时长，实际胜败取决于击杀首领和英雄存活。

统一在 `D:/UnityProject/myGame` 的 `main` 开发、验证、提交和拉取；提交标题与说明用中文。具体项目约定见根目录 `AGENTS.md`。技能与 Buff 的强制架构契约见 [技能地基](ability-foundation.md)。原始重设计定稿保留在 [设计来源](redesign-expedition.md)。
