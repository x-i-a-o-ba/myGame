# HUD 美术与敌人资源生成脚本

2026-10-21 用于生成 Dota 2 风格 HUD 和首批敌人资源的 Python 脚本。生成结果已提交到 `Assets/`，正常开发**不需要运行**；只有想批量重做这些资源时才用。

| 脚本 | 输出 |
| --- | --- |
| `sprites.py` | `Assets/Game/UI/Sprites/*.png` 与导入设置（九宫格边框） |
| `icons.py` | `Assets/Game/UI/Icons/*.png`（技能、被动、英雄头像、敌人攻击图标，8 张 192×192 敌人头像，2026-10-22 新增“金币”“商店”图标） |
| `items.py` | 2026-10-22 新增：`Assets/Game/Equipment/` 下 18 张 128×128 装备图标（边框为品质色）、18 个装备定义资源、装备库与 `.meta`，并写出 `items.json`（编号登记条目）和预览 `preview/装备图标.png`；装备数值表在脚本里的 `ITEM_TABLE` |
| `hud.py` | `Assets/Resources/UI/Panels/` 下 BottomHUD、Title、Controls、Progression、Talent、TargetInfo（选中敌人信息）、AbilityTooltip（悬停提示）、Shop（商店，2026-10-22）面板预制体，并在 `preview/` 渲染近似预览图（`hud_preview*.png`、`tooltip_preview*.png`、`shop_preview.png`、`stats_preview.png`） |
| `enemies.py` | 自动攻击模块、敌人材质、8 个敌人预制体（含头像引用）、敌人名册与相关 `.meta` |
| `common.py`、`ui.py` | 绘图、GUID 分配、UGUI 预制体 YAML 生成与预览渲染 |

- 依赖：Python 3、`pycairo`、`Pillow`；预览需要 Noto Sans SC 字体（`ui.py` 中的路径）。在本目录运行，例如 `python3 sprites.py && python3 icons.py && python3 hud.py && python3 enemies.py`。
- `guids.json` 记录生成资源的 GUID，保证重跑后引用不变；不要删除。`sprites.json`、`icons.json`、`enemies.json` 是脚本之间共享的名称→GUID 表。
- **会覆盖**输出文件：在 Unity 里手工改过的面板布局、预制体或名册会被替换。手工修改后应以 Unity 中的资源为准，不再重跑对应脚本。
- 预览图只是 PIL 近似渲染，不代表 Unity 实际画面。
- 2026-10-22 装备与商店：`hud.py` 用 `extra(上下文)` 给新增节点分配独立、稳定的 fileID，已有节点的 ID 不变；BottomHUD 加宽到 1564，新增属性第二列、装备区与商店按钮。商店与装备栏绑定键必须与 `CombatHud.ShopKeys`、`EquipmentKeys` 一致（编辑器校验会检查）。`items.py` 会覆盖装备资源，在 Unity 里手工调整过装备数值后不要再重跑。
- 2026-10-22 装备拖动与固定商店（PR #6）：`ui.py` 新增 `drag(节点, 开始命令, 结束命令, 数值, require=需要显示的节点, ghost=拖动图标, dimmed=源图标透明度)` 与 `drop(节点, 命令, 数值)`，分别写出 `UIDragAction` / `UIDropAction` 组件（脚本 GUID 在 `DRG` / `DRP`）。`hud.py` 把装备区改为 Dota 2 式 3 × 2（每格 60×60，金币移到下方），每格挂拖动源、换位放置目标与金色 `Drop Target` 高亮（键 `equip.s{i}.drop`）；商店按钮新增暗色遮罩 `shop.far` 与距离标签 `shop.distance`；商店面板根节点挂 `combat.shop.drop` 放置目标。新增节点仍用独立的 `extra` 序列分配 fileID，已有节点 ID 不变。
- 2026-10-21 选中目标面板新增精灵：`target_frame`、`target_header`、`portrait_frame`、`badge`、`chip`、`divider` 与 6 个 40×40 属性图标 `stat_damage/interval/range/speed/xp/distance`。面板绑定键必须与 `CombatHud.TargetKeys` 一致（编辑器校验会检查）。
