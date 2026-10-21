using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>视野预制体的配置入口；没有 Update。挂在指定单位下，由会话统一收集和计算。</summary>
    public sealed class VisionSource : MonoBehaviour
    {
        [Range(0,1), Tooltip("0=蓝方，1=红方；同阵营共享视野。")] public int team = 1;
        [Tooltip("关闭后停止贡献，不影响所属敌人的模拟。")] public bool providesVision = true;
        [Min(0), Tooltip("白天普通视野半径，世界单位。")] public float dayRadius = 16;
        [Min(0), Tooltip("夜间普通视野半径，世界单位。")] public float nightRadius = 9;
        [Min(0), Tooltip("真视仅侦测隐身，仍需要普通视野才能看见目标。")] public float trueSightRadius;
        [Tooltip("飞行视野忽略树木和高低地遮挡。")] public bool flying;
        [Tooltip("隐形来源；敌方需要普通视野和真视才能看到标记。")] public bool invisible;
        [Min(0), Tooltip("有效时长（秒）；0=永久。由会话计时，重置时重新开始。")] public float lifetime;
        [HideInInspector] public float expiresAt;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = team == 0 ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, dayRadius);
        }
    }
}
