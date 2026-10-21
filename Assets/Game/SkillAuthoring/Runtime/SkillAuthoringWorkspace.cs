using Roguelike.Abilities;
using UnityEngine;

namespace Roguelike.SkillAuthoring
{
    /// <summary>持久保存编辑工作环境。技能数值归 SkillDefinition；本组件不运行战斗、不保存命中结果。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Roguelike/技能/技能配置工作台")]
    public sealed class SkillAuthoringWorkspace : MonoBehaviour
    {
        public SkillDefinition skill;
        public SkillCatalog catalog;
        public Transform caster;
        public Transform target;
        [Min(0)] public int stageIndex;
        public bool showAllStages;
        public bool showPreview = true;
        public bool showGrid = true;
        public bool editHandles = true;
        [Range(5,100)] public int gridHalfExtent = 20;
        [Range(0,1)] public float impactPreviewFraction = 0.6f;
        [Range(0,1)] public float flightPreviewProgress = 0.5f;
    }
}
