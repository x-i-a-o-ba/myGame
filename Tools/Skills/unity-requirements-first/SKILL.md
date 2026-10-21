---
name: unity-requirements-first
description: 在 Unity 项目开发功能、调整 URP/ECS 架构或处理性能需求时，参考实际项目版本、现有实现与资源，自主完成方案、实现和验证。
---

# Unity 项目开发参考

## 自主推进

根据用户目标和已有代码，自主选择方案、工具、技能与协作方式，补齐玩法、数值、表现和实现细节并完成开发。按实际需要读取 AGENTS.md、Docs/README.md 及相关模块文档；文档用于提供上下文，不作为开始写代码的审批步骤。

普通实现取舍直接处理，重要决定随实现记录。只有缺少无法可靠推断的必要信息或授权时才询问用户。文档与现状冲突时，以用户最新方向和实际项目事实为依据更新。

## Unity 技术参考

- Editor 和包版本以 ProjectSettings/ProjectVersion.txt、Packages/manifest.json 为准；兼容性问题查对应版本官方文档或已安装包源码。
- 技能/Buff 改动参考 Docs/ability-foundation.md 的共享模块、数据所有权和执行边界。
- 模块设计、ECS、中文 Inspector、场景与 Prefab 的现有约定见 [架构与注释参考](references/architecture-and-comments.md)，按改动涉及的内容查阅。
- 保留 Unity GUID 和序列化引用，利用 Git 或备份保留可恢复状态，并保留用户未提交改动。
- ECS 高频路径按实际负载选用数据组件、Burst、Jobs、空间索引与批量渲染。维护性和性能取舍由当前任务事实决定。

## 验证与记录

按改动范围选择静态检查、Unity 编译、Editor/Player 功能检查或性能测量，发现问题后修复并复验。共享 Unity 项目的占用和写入由实际工具约束协调，独立工作可并行推进。

同步与本次改动相关的需求、架构和使用说明，简洁汇报实际改动、关键取舍、验证结果和剩余限制。用户要求、AI 决策、代码事实与未验证项分别表述；性能目标与对应负载的 Player 实测结果分开记录。
