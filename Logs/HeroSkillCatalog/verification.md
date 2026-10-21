# 数字配置 ID 验证记录（2026-09-28）

## 已执行
- Unity 6000.3.18f1 隔离项目编译通过：Logs/numeric-ids-compile.log。
- NumericConfigIdsValidation.MigrateValidateAndExport：23 项断言通过，迁移 8 个技能与 2 个英雄。覆盖映射、幂等、缺失/重复/越界、编号段耗尽及运行库按数字查询。
- NumericConfigIdsValidation.RunCreation：19 项断言通过，Logs/numeric-ids-creation.log。真实创建、复制、保存重导入、删除同路径重建、强制同 GUID 不同身份、历史编号不复用。首次失败发现 Unity 复用 GUID，已修复真实分配器并重跑通过。
- 图形 Editor 实际打开技能设计器，中文数字 ID 字段和列表编号可见：Logs/numeric-ids-visual.log，证据 outputs/hero-skills-20260928/技能设计器.png。未运行战斗。
- 主项目 10 个定义逐文件与备份比较：仅追加 configId，原技能数值和引用均未改动；主场景未修改。
- artifact-tool 构建检查通过：2 英雄、8 技能、1 怪物、8 装配、5 阶段；数值主键与外键为 Excel 数字。覆盖空/文本/小数/越界/缺失/重复和无效装配。
- Microsoft Excel 原生重算通过：1000/2000/3000 查询、3999 未找到、9999 越界提示，无公式错误；6 张表导出 PDF 并检查可读性。证据 outputs/hero-skills-20260928 下。

## 范围与限制
- 当前普通怪物编号3000随实体生成写入 CombatEnemy.ConfigId。只有现有统一怪物配置，未实现多怪物资源系统。
- Excel 仍为已保存配置的设计文档，不自动导入。内部字符串键仅兼容用途，GUID 保持。
- 临时检查资源、试验编号和图形检查场景均留在隔离项目，没有复制进主项目。
- 本轮未执行 Game/Player 战斗、性能测量，不据此声明万级战斗达标。
- 备份：Backups/numeric-ids-20260928。