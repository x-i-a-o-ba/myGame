using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>纯距离规则，与输入、渲染和 ECS 无关；范围比较使用地面 XZ 平面。</summary>
    public static class CircleTargeting
    {
        public static Vector3 Resolve(Vector3 origin, Vector3 requested, float range, bool clamp, out bool valid)
        {
            requested.y = origin.y;
            var offset = requested - origin;
            range = Mathf.Max(0, range);
            bool outside = offset.sqrMagnitude > range * range + 0.0001f;
            valid = clamp || !outside;
            return outside && clamp ? origin + Vector3.ClampMagnitude(offset, range) : requested;
        }
    }
}
