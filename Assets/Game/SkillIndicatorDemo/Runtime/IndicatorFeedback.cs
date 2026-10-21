using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>复用场景中的单个反馈对象播放扩散圆环，不生成粒子树或接入伤害系统。</summary>
    public sealed class IndicatorFeedback : MonoBehaviour
    {
        [SerializeField] private CircleIndicatorView ring;
        private Vector3 position;
        private float radius, duration, elapsed;
        private Color color;
        public bool IsPlaying { get; private set; }

        public void Play(Vector3 center, CircleSkillPreset preset)
        {
            position = center; radius = preset.areaRadius;
            duration = Mathf.Max(0.1f, preset.feedbackDuration);
            color = preset.color; elapsed = 0; IsPlaying = true;
            Render();
        }
        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Stop(); return; }
            Render();
        }
        private void Render()
        {
            float t = Mathf.Clamp01(elapsed / duration);
            ring.Show(position, radius * Mathf.Lerp(0.3f, 1.15f, t), color, 1 - t);
        }
        public void Stop()
        {
            IsPlaying = false;
            if (ring) ring.Hide();
        }
        private void OnDisable() => Stop();
    }
}
