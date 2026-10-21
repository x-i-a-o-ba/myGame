using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>桌面输入适配器；本 Demo 的落点使用固定地面平面，不包含地形投影或移动。</summary>
    public sealed class IndicatorDemoInput : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Transform ground;
        [SerializeField] private IndicatorDemoController controller;
        [SerializeField] private IndicatorDemoHud hud;

        public bool UpdatePointer(Vector2 screen)
        {
            if (hud.BlocksPointer(screen) || screen.x < 0 || screen.y < 0 || screen.x > Screen.width || screen.y > Screen.height)
            { controller.SetPointer(default, false); return false; }
            var plane = new Plane(Vector3.up, ground.position);
            var ray = gameCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) { controller.SetPointer(default, false); return false; }
            controller.SetPointer(ray.GetPoint(distance), true); return true;
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { controller.CancelAim(); return; }
            if (Input.GetKeyDown(KeyCode.Q)) controller.SelectSkill(0);
            if (Input.GetKeyDown(KeyCode.W)) controller.SelectSkill(1);
            if (Input.GetKeyDown(KeyCode.E)) controller.SelectSkill(2);
            if (!controller.IsAiming) return;
            bool usable = UpdatePointer(Input.mousePosition);
            if (usable && Input.GetMouseButtonDown(0)) controller.TryConfirm();
        }
    }
}
