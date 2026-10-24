# HUD 美术与界面生成

2026-10-25：现行战斗界面沿用原版金属金边美术，适配快速施法、六格装备和三关营地。当前布局入口是 `Assets/Game/Editor/ExpeditionSceneAuthoring.Style.cs`；控件绑定、保存和场景装配由同目录 `ExpeditionSceneAuthoring.cs` 负责。

在 Unity 执行 `Tools/Roguelike/远征/生成界面并装配战场`，会重新生成 `UI.Game.BottomHUD`、`UI.Expedition` 并装配 Game / EmberRift。手工修改布局前应先提交；日常改视觉不运行整套英雄、装备或怪物迁移。`ExpeditionSceneAuthoring.BuildInterface` 可完成生成、规则/资源验证和 Windows 构建。

视觉与玩法分开维护。删掉旧操作、天赋或固定商店时，只删对应控件；九宫格边框、肖像、装备/技能图标、血蓝条、等级徽章和悬停提示继续复用。不要用纯色临时布局覆盖正式 HUD。验收以真实 Player 截图为准。

## 现有美术来源

| 脚本 | 资源与用途 |
| --- | --- |
| `sprites.py` | `Assets/Game/UI/Sprites` 中的金属面板、九宫格边框、槽位、血条、徽章、梯形顶栏等 |
| `icons.py` | 技能、普攻、肖像、金币、商店等图标 |
| `items.py` | 旧版装备图标和旧数值资源生成；现行数值已迁移，不用于日常重建装备 |
| `enemies.py`、`ember.py` | 历史两地图怪物与头像来源，现行名册/模块以 GameData 为准 |
| `battlefields.py` | 地图预览、地面和熔岩纹理等 |
| `select.py` | 开局选择面板及预览，修改前核对现行选择流程 |
| `common.py`、`ui.py` | 历史绘图、GUID、UGUI YAML 与近似预览辅助 |

`hud.py` 已退出项目，历史天赋、固定商店、目标面板和地面装备界面保留在 Git 历史，不是当前生成入口。`preview/` 的旧图只是历史近似预览。

美术脚本依赖 Python 3、`pycairo`、`Pillow`，地图脚本还依赖 `numpy`。正常开发无需重跑。`guids.json` 及各类名称映射应保留；资源改名保持原 `.meta` GUID。重跑脚本可能覆盖输出资源，现行数值与布局以 Unity 资源和 C# 作者工具为准。
