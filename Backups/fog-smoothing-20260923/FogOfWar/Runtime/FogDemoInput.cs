using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>桌面输入适配器，只传递演示命令；不参与遮挡规则或显隐计算。</summary>
    public sealed class FogDemoInput
    {
        private readonly FogDemoSession session;
        public FogDemoInput(FogDemoSession session) { this.session=session; }
        public void Tick()
        {
            if(Input.GetKeyDown(KeyCode.Escape)) session.CancelPlacement();
            if(session.hud.Blocks(Input.mousePosition)) return;
            var camera=session.gameCamera;
            camera.orthographicSize=Mathf.Clamp(camera.orthographicSize-Input.mouseScrollDelta.y*3,12,85);
            var direction=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            camera.transform.position+=direction*Time.deltaTime*30;
            if(!session.TryGroundPoint(camera.ScreenPointToRay(Input.mousePosition),out var point)) return;
            if(Input.GetMouseButtonDown(1)) session.MoveObserver(point);
            if(Input.GetMouseButtonDown(0)) session.ClickGround(point);
        }
    }
}
