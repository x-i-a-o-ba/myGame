using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 商店的固定位置（Dota 2 式）：英雄走进以本物体为中心、半径为 <see cref="radius"/> 的范围后才能打开商店购买与出售。
    /// 只提供场景位置与范围；控制器在重开时把它读入会话（<see cref="Roguelike.Abilities.ShopSite"/>），并在地面显示金色范围圈。
    /// 商店模型是本物体的子物体，只做展示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameShop : MonoBehaviour
    {
        public const float MinimumRadius=2,MaximumRadius=40;
        [Range(MinimumRadius,MaximumRadius), Tooltip("商店范围半径（米）：英雄与商店中心的平面距离不超过该值时才能打开商店、购买与出售。重开生效。")]
        public float radius=8;
        /// <summary>商店中心（世界坐标，只使用 x / z）。</summary>
        public Vector3 Position=>transform.position;
        /// <summary>夹到允许范围内的半径。</summary>
        public float Radius=>Mathf.Clamp(radius,MinimumRadius,MaximumRadius);
        private void OnDrawGizmos()
        {
            Gizmos.color=new Color(1f,0.82f,0.3f,0.9f);
            Vector3 center=new Vector3(Position.x,0.05f,Position.z),previous=center+new Vector3(Radius,0,0);
            for(int i=1;i<=64;i++)
            {
                float angle=i*Mathf.PI*2/64;
                Vector3 next=center+new Vector3(Mathf.Cos(angle)*Radius,0,Mathf.Sin(angle)*Radius);
                Gizmos.DrawLine(previous,next); previous=next;
            }
        }
    }
}
