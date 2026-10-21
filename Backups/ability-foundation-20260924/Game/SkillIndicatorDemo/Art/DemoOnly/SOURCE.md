# 仅供演示的素材

来源：[Kenney Particle Pack](https://kenney.nl/assets/particle-pack)，2026-09-22 下载。

- 下载文件：kenney_particle-pack.zip，包内 License.txt 标为 Particle Pack 1.1。
- 归档 SHA256：B631D4B07F7002549FDCF155F01141AD482F79F3440E4E301EED49CE5F1D8958。
- 选用 PNG (Transparent) 下 circle_02.png、circle_03.png、star_04.png，保留原图与 License.txt。
- CC0；这些纹理仅用于本项目技能指示器 Demo，不代表最终美术方向。配色、透明度和动画通过本项目材质及脚本控制。
- 清晰的施法边界使用项目生成并保存的圆环网格；装饰/施放反馈使用上述纹理，视觉柔光不参与范围计算。

研究参考：NiloCat UnityURPUnlitScreenSpaceDecalShader（MIT，提交 11b2e58c09bd869fd7dd4c7c098eea2256ae9c3e）和 SparkAflame Click-Decal-Shader 的上游预览。当前平面测试场景使用 URP 透明网格，无需为主游戏开启深度纹理或新增 Decal Renderer Feature；上述投影 Shader 未集成到项目。
