# HUD 美术与敌人资源生成脚本

2026-10-21 用于生成 Dota 2 风格 HUD 和首批敌人资源的 Python 脚本。生成结果已提交到 `Assets/`，正常开发**不需要运行**；只有想批量重做这些资源时才用。

| 脚本 | 输出 |
| --- | --- |
| `sprites.py` | `Assets/Game/UI/Sprites/*.png` 与导入设置（九宫格边框） |
| `icons.py` | `Assets/Game/UI/Icons/*.png`（技能、被动、头像、敌人攻击图标） |
| `hud.py` | `Assets/Resources/UI/Panels/` 下 BottomHUD、Title、Controls、Progression、Talent 五个面板预制体，并在 `preview/` 渲染近似预览图 |
| `enemies.py` | 自动攻击模块、敌人材质、8 个敌人预制体、敌人名册与相关 `.meta` |
| `common.py`、`ui.py` | 绘图、GUID 分配、UGUI 预制体 YAML 生成与预览渲染 |

- 依赖：Python 3、`pycairo`、`Pillow`；预览需要 Noto Sans SC 字体（`ui.py` 中的路径）。在本目录运行，例如 `python3 sprites.py && python3 icons.py && python3 hud.py && python3 enemies.py`。
- `guids.json` 记录生成资源的 GUID，保证重跑后引用不变；不要删除。`sprites.json`、`icons.json`、`enemies.json` 是脚本之间共享的名称→GUID 表。
- **会覆盖**输出文件：在 Unity 里手工改过的面板布局、预制体或名册会被替换。手工修改后应以 Unity 中的资源为准，不再重跑对应脚本。
- 预览图只是 PIL 近似渲染，不代表 Unity 实际画面。
