# 游戏配置与数字编号

现行配置来自 `Assets/GameData` 的 Unity 资源，Inspector 与技能工作台是编辑入口。

- `编号登记.json`：稳定数字 ID、GUID、路径和退休状态。技能 1000–1999、英雄 2000–2999、怪物 3000–3999、效果 4000–4999、Buff 5000–5999、装备 6000–6999、地图 7000–7999。编号不随名字、槽位和排序变化，不复用退休编号。
- `Logs/HeroSkillCatalog/source.json`：`HeroSkillDocumentation.Export` 从当前资源导出的最新英雄、技能、装配、阶段与怪物快照，当前含 2 英雄、24 技能（8 基础 +16 形态）和两图怪物。仅文档，不参与游戏加载。
- `英雄与技能配置.xlsx`：保留的旧版离线表格，尚未接入远征的形态/营地导出，不能作为当前数值来源；修改它不会写回 Unity。现行数值表已合并到 [英雄与技能](../heroes-and-skills.md)、[怪物与刷怪](../monsters-and-director.md)、[装备与遗物](../equipment-and-relics.md)。

装备中文改名保留原 GUID；6006–6008 退休，新增雷鸣锤 6018。所有技能执行均使用编译索引，不按 ID 或英雄名分支。编辑、编译和验证入口见 [工程与工具](../engineering-and-tools.md)。
