using System;
using UnityEngine;

namespace Roguelike.FogOfWar
{
    /// <summary>只拥有显示用 GPU 纹理：4 倍放大、可分离高斯滤波、快照过渡。不回写逻辑位图。</summary>
    public sealed class FogVisualMask : IDisposable
    {
        private readonly Material filter;
        private RenderTexture previous, current, scratch;
        private bool initialized;
        private float started, duration;
        public RenderTexture Current => current;
        public RenderTexture Previous => previous;
        public float Blend => duration <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - started) / duration));

        public FogVisualMask(Material template, int gridSize)
        {
            if (!template) throw new ArgumentException("FogDemoSession 缺少视野显示滤波材质，请绑定 Fog Smoothing.mat。");
            filter = new Material(template) { name = "Fog smoothing (session owned)" };
            previous = CreateTexture(gridSize * 4, "Fog previous");
            current = CreateTexture(gridSize * 4, "Fog current");
            scratch = CreateTexture(gridSize * 4, "Fog scratch");
        }
        private static RenderTexture CreateTexture(int size, string name)
        {
            var texture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1
            };
            texture.Create();
            return texture;
        }
        /// <summary>仅在快照变化时调用；featherCells 单位为逻辑格。换阵营/重置直接替换历史，避免跨阵营残留。</summary>
        public void Push(Texture raw, float featherCells, float transitionSeconds, bool reset)
        {
            var active = RenderTexture.active;
            try
            {
                if (initialized && !reset)
                {
                    // 保存此刻的混合结果，连续移动不会从上一张目标图重新起跳。
                    filter.SetTexture("_Previous", previous);
                    filter.SetFloat("_Blend", Blend);
                    Graphics.Blit(current, scratch, filter, 2);
                    var swap = previous; previous = scratch; scratch = swap;
                }
                filter.SetVector("_FilterStep", new Vector4(featherCells / raw.width, featherCells / raw.height, 0, 0));
                Graphics.Blit(raw, scratch, filter, 0);
                Graphics.Blit(scratch, current, filter, 1);
                if (!initialized || reset) Graphics.Blit(current, previous);
                initialized = true;
                duration = Mathf.Max(0, transitionSeconds);
                started = reset ? Time.time - duration : Time.time;
                Shader.SetGlobalTexture("_DemoFogMap", current);
                Shader.SetGlobalTexture("_DemoFogPrevious", previous);
                Shader.SetGlobalVector("_DemoFogTransition", new Vector4(started, duration, 0, 0));
            }
            finally { RenderTexture.active = active; }
        }
        public void Dispose()
        {
            Shader.SetGlobalTexture("_DemoFogMap", Texture2D.whiteTexture);
            Shader.SetGlobalTexture("_DemoFogPrevious", Texture2D.whiteTexture);
            Release(previous); Release(current); Release(scratch);
            DestroyOwned(filter);
        }
        private static void Release(RenderTexture texture)
        {
            texture.Release();
            DestroyOwned(texture);
        }
        private static void DestroyOwned(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            // EditMode validation has no frame-end destruction pass; release only session-owned objects now.
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(asset);
                return;
            }
#endif
            UnityEngine.Object.Destroy(asset);
        }
    }
}
