# myGame 开发文档

当前版本是三关远征肉鸽：Unity 6000.3.18f1、URP 17.3.0、Entities 1.4.8；实际依赖以工程文件为准。选择英雄和首图后依次进入两张战场，再挑战首图深渊版；两次营地承接成长、装备和遗物。

日常文档集中为以下八个入口。旧需求和历次验证已归档到 `History/`，不能用旧 Dota 操作、天赋、30 波或固定商店覆盖当前规则。

| 入口 | 内容 |
| --- | --- |
| 本页：项目总览 | 工程入口、目标、开发约定 |
| [玩法与数值](gameplay-and-numbers.md) | 三关时间、导演曲线、十五级经验与局内状态 |
| [英雄与技能](heroes-and-skills.md) | 两位英雄、八技能、十六形态与共享能力 |
| [怪物与刷怪](monsters-and-director.md) | 两图怪物、七类技能模块、词缀、首领与深渊 |
| [装备与遗物](equipment-and-relics.md) | 十六装备、八遗物、营地交易和路线 |
| [操作与界面](controls-and-ui.md) | WASD、Q/E/R/F、暂停、HUD 和反馈 |
| [工程与工具](engineering-and-tools.md) | 职责、数据所有权、编辑器、构建与迁移 |
| [验证记录](validation.md) | 本轮实际检查、性能数字和未验证范围 |

项目从 `Assets/Scenes/Main.unity` 启动；选人选图在 `SelectScene`，战斗在 `Game` 与 `EmberRift`。技能工作台保留专用编辑场景。配置集中在 `Assets/GameData`，游戏代码位于 `Assets/Game`。

首批同屏硬上限为 4,000，万级是后续目标，不能用基准场景或数量统计代替完整战斗 Player 实测。标准局约十五分钟是目标时长，实际胜败取决于击杀首领和英雄存活。

统一在 `D:/UnityProject/myGame` 的 `main` 开发、验证、提交和拉取；提交标题与说明用中文。具体项目约定见根目录 `AGENTS.md`。技能与 Buff 的强制架构契约见 [技能地基](ability-foundation.md)。原始重设计定稿保留在 [设计来源](redesign-expedition.md)，历史证据见 [归档](History/)。
