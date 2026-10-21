using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>可破坏树木的逻辑遮挡与场景显示。不要用 SetActive 代替砍树，迷雾内需保留记忆外观。</summary>
    public sealed class FogObstacle : MonoBehaviour
    {
        public bool blocking = true;
        [Min(0.1f)] public float radius = 1.1f;
        public Renderer visual;
        [System.NonSerialized] public bool rememberedBlue = true, rememberedRed = true;
    }
}
