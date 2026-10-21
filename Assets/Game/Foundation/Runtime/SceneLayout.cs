using UnityEngine;
namespace Roguelike.Foundation
{
    /// <summary>显式迁移标记和场景说明，阻止工具重复重排开发者已经修改的节点。</summary>
    public sealed class SceneLayout : MonoBehaviour
    {
        public int version = 1;
        [TextArea(4,10)] public string developerNotes = "00_Bootstrap：装配与场景说明\n01_Systems：功能模块控制器\n02_World：环境与角色\n03_Presentation：相机、灯光、表现\n04_UI：单根 Canvas、面板层、EventSystem\n05_Debug：测试与调试";
    }
}
