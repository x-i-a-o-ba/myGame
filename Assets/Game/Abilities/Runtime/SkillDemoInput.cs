using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>桌面输入适配，确认前必须完成当帧投影和 UI 拦截，不使用旧落点。</summary>
    public sealed class SkillDemoInput
    {
        private readonly SkillDemoController demo;
        public SkillDemoInput(SkillDemoController demo) { this.demo=demo; }
        public void Tick()
        {
            if(Input.GetKeyDown(KeyCode.Escape)) { demo.Cancel(); return; }
            if(Input.GetKeyDown(KeyCode.Q)) demo.SelectSkill(0);
            if(Input.GetKeyDown(KeyCode.W)) demo.SelectSkill(1);
            if(Input.GetKeyDown(KeyCode.E)) demo.SelectSkill(2);
            if(Input.GetMouseButtonDown(1) && demo.Aiming>=0) { demo.Cancel(); return; }
            bool blocked=demo.hud.Blocks(Input.mousePosition);
            var ray=demo.gameCamera.ScreenPointToRay(Input.mousePosition);
            bool hit=new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance);
            var point=ray.GetPoint(distance);
            if(demo.Aiming>=0)
            {
                demo.SetAim(point,hit && !blocked);
                if(!blocked && Input.GetMouseButtonDown(0)) demo.Confirm();
            }
            else if(hit && !blocked)
            {
                if(Input.GetMouseButton(1)) demo.Move(point);
                if(Input.GetMouseButtonDown(0)) demo.Pick(point);
            }
            if(!blocked)
            {
                demo.gameCamera.orthographicSize=Mathf.Clamp(demo.gameCamera.orthographicSize-Input.mouseScrollDelta.y*2,12,75);
                demo.gameCamera.transform.position+=new Vector3((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),0,(Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0))*Time.deltaTime*25;
            }
        }
    }
}
