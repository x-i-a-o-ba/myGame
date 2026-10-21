using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>仅用于演示的圆形技能配置；施法距离与落点半径采用同一世界单位。</summary>
    [CreateAssetMenu(menuName = "Roguelike/Demo/Circle Skill Preset")]
    public sealed class CircleSkillPreset : ScriptableObject
    {
        [Tooltip("Demo 名称，不代表最终技能设计。")] public string displayName;
        [Min(0.1f), Tooltip("施法者到落点中心的最大距离。")] public float castRange = 10;
        [Min(0.1f), Tooltip("落点范围半径；边缘可以超出施法距离圈。")] public float areaRadius = 2.5f;
        public Color color = new Color(0.18f, 0.85f, 1, 1);
        [Min(0.1f), Tooltip("确认后的视觉反馈时长（秒），没有伤害逻辑。")] public float feedbackDuration = 0.8f;
    }
}
