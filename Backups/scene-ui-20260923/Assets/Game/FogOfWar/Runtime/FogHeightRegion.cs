using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>地形高度分区；XZ 包围盒与离散高度用于遮挡，编辑器可直接修改。</summary>
    public sealed class FogHeightRegion : MonoBehaviour
    {
        public Vector2 size = new Vector2(18, 20);
        [Range(0,5)] public int level = 1;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x,1,size.y));
        }
    }
}
