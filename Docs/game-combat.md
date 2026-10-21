# Game 场景与组合技能

更新：2026-10-21（加入英雄成长与波次）。当前架构与完整合同见 [ability-foundation.md](ability-foundation.md)，成长规则见 [hero-progression.md](hero-progression.md)。

## 使用和配置

打开 `Assets/Scenes/Game.unity`。右键移动，自动攻击；Q/W/E/R 对应英雄技能槽0–3，自身目标立即提交，其余按配置瞄准，左键确认，右键/Esc取消，F5重开。开局1级、1个技能点、四个技能均未学习：Ctrl+Q/W/E/R 或成长面板按钮学习/升级技能，10/15/20/25 级按 1/2 选天赋。敌人按波次在玩家周围安全半径30米外刷出，同屏存活上限 `enemyCount`=3000；击杀第30波首领胜利。Game视野关闭。

选择 GameCombatController：`hero` 决定英雄基础属性、成长、天赋及技能装配，`skillCatalog` 是共享定义注册表，`progressionRules` 是经验与等级规则，`difficulty` 是关卡难度曲线（两者为空时使用代码默认值并警告）。配置目录 `Assets/Game/Skills/Foundation`，包含 Buffs、Effects、Skills、两份英雄装配及技能库。更换英雄资源后重开；`SelectHero` 提供运行时选人接入边界，完整选人界面尚未制作。第二份英雄仅换槽用于展示复用，不是新增完整英雄。

打开 SkillDefinition：先选择 aim/castRange，再配置 stages。每段独立配置范围、投递、延迟、重复次数/间隔、effects；直线弹另有 impactEffects。EffectDefinition资源可被多个技能复用。新普通技能只组装资源并登记到库，不新增 Q/W/E/R 配置类。Core编译器拒绝非法参数与未登记Buff。

编辑资源时 Inspector 使用中文字段、枚举和单位提示；正式技能隐藏旧独立示例字段。可在 Project 右键 `Create > Roguelike > 技能` 新建定义、Buff 与效果资源。稳定 ID 仍使用非本地化机器键。

BuffDefinition 使用修饰列表（移速/攻击力/攻速，Add/Percent/Multiply）、控制限制、时长/周期、叠层及来源合并。资源没有冷却/蓄层/剩余时间，状态分别位于拥有者AbilitySlot和ActiveBuff。重开会重新编译只读Blob，输入/指示器与模拟消费同一快照；运行中编辑资源需要重开。

## 现有数据示例

| 技能 | 组合 | 数值 |
|---|---|---|
| 冰霜专注 | 普攻命中条件→自身攻速Buff | 4次命中，+60%攻速5秒，CD7秒 |
| 冰霜扇击 | 方向→扇形→伤害+减速 | 22米/半角35度，30伤害，移速-35%持续2.5秒，CD5秒 |
| 冰霜冲击 | 目标点→弹体→圆形伤害 | 距离25米，速度42，半径5，18伤害；2充能，每15秒恢复1次 |
| 冰晶巨箭 | 直线首碰→单体效果+圆形效果 | 距离150/速度48/碰撞半径1.15；首碰20伤害+眩晕1.5秒；半径7内16伤害+移速-50%3秒；CD20秒 |

W已改为扇形查询组合例，不保留九射线各自首中。R目标若在首碰伤害后仍存活，会继续受爆炸伤害；无贯穿。Buff百分比按定义累加，不采用“最强减速”规则；W和R重叠可降低到基础移速15%，到期后重新计算贡献。

当前指示器展示首段几何：圆形落点、扇形或直线碰撞宽度；不承诺展示整个多段时间轴和未来弹道变化。场景节点仍可编辑，无运行时重建UI树。详细命中特效暂未扩充。

## 实现分工

- `GameCombatController`：组合根、输入桥、展示快照；技能身份没有QWER枚举。
- `GameCombatSession`：英雄装配、实体/Blob生命周期与重开。
- `CombatSimulationSystem`：并行Buff更新和敌人行动，普攻命中交共享效果入口；串行处理学习/天赋命令、结算经验升级、按 `CombatWaveDirector` 刷波与判定胜负。AbilityUnit唯一权威属性，CombatPlayer是展示/统计快照。
- `Abilities/Core`：SO编译、共享效果/Buff、通用施法/多段/投递/几何；不依赖UI、输入或场景。
- 旧独立技能演示使用同一基础效果代码及自己的历史调度，双向禁止与Game在同一World同时启动。

## 验证与限制

最新结果和证据见 [基础架构记录](ability-foundation.md)。当前Game只有四个可见输入槽，Core支持更长英雄配置但Game启动会明确拒绝超过四槽。局内升级、技能等级、天赋与波次已于2026-10-21加入（见 [hero-progression.md](hero-progression.md)，Unity 编译与试玩未验证）；被动事件总线、光环、完整时停、Excel导入器、完整选人界面、经验掉落留待明确内容需求；不把这些当已实现。万级完整战斗性能需另做Player实测。
