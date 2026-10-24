> 历史记录：当前规则请从 [开发文档总览](../README.md) 查阅。本页中的旧操作和旧验证结果不代表当前版本。

# 2026-09-22 验证记录

## 已通过

- Unity Package Manager 解析：URP 17.3.0、Entities 1.4.8、Entities Graphics 1.4.21；manifest 与 Unity 生成的 packages-lock 一致。
- 项目 C# 编辑器编译及 Windows x64 Release 构建通过；独立验证副本 `Temp/AgentValidation`，日志 `Logs/AgentValidation.log`，最终 Unity 退出码 0。
- `Validate Foundation` 的隔离 World 检查：10,000 实体并行运动位置有限且在预定半径内；合成生命负载归零后重置；销毁实体和配置后更新安全跳过。
- 场景脚本、材质 GUID 引用与文档本地链接静态检查通过。
- Windows Player 短时运行：10,000 请求实体 / 10,000 模拟实体；成功生成 5,609 帧 CSV 和 JSON 并退出。

## 短时运行环境与边界

Windows Player Release，Direct3D11，1920×1080；本机 Ryzen 7 7745HX、RTX 4060 Laptop GPU、约 32GB 内存。后台 batchmode，预热 2 秒、采样约 5 秒。运行路径：`Builds/Benchmark/RoguelikeBenchmark.exe`。

原始结果：`BenchmarkReports/Smoke/benchmark-20260922-123459-542.json` 与同名 CSV；Player 日志：`Logs/BenchmarkSmoke.log`。

这次仅验证启动、实体运行和导出链路。后台 batchmode、立方体网格、运动负载与短采样均不符合正式完整战斗性能验收条件；报告中的 FPS 不作为游戏帧率成绩或最低配置依据。

## 仍需验证

- 真实场景的画面、模型/动画和材质效果人工检查；本轮未做截图视觉验收。
- 在 Editor 中重复进入/退出 Play 和增量卸载压测场景的生命周期检查。
- 目标档位硬件下前台运行、完整预热与多轮采样；真实战斗系统接入后的性能验收。
- Release 下 GC ProfilerRecorder 不可用，报告已写 -1；需 Development Build + Profiler 补测。GPU 帧耗时、峰值内存和实际 GPU 可见实体数尚未测量。
- Player 退出时出现一次 `GarbageCollector disposing of ComputeBuffer` 警告，尚未定位来源；本项目新增代码未直接创建 ComputeBuffer，不能据此判定已无资源泄漏。后续在 Development Build 中定位退出/重进生命周期。
- Skill 文件已安装且与项目副本 SHA256 一致，frontmatter 和规则人工检查完成；官方 quick_validate.py 因本地 Python 缺 PyYAML 未运行成功，尚无独立行为回归测试。

## 恢复说明

初始渲染设置备份位于 `Backups/urp-migration-20260922`。该备份只覆盖初始 GraphicsSettings / QualitySettings，不是整个项目快照。若要回退全部 ECS 变更，还需要移除新增的 `Assets/Game` 与 Entities 包声明，再由 Unity 重新解析依赖；注意先保留后续用户修改。
