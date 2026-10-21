using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>预览与实际施法共用的 XZ 圆形几何规则；范围单位为米。</summary>
    public static class AbilityGeometry
    {
        public static float3 Resolve(float3 origin,float3 point,float range,bool clamp,out bool valid)
        {
            point.y=origin.y; var delta=point-origin;
            bool outside=math.lengthsq(delta)>range*range+0.0001f;
            valid=clamp || !outside;
            return outside && clamp?origin+math.normalizesafe(delta)*range:point;
        }
        public static float Radius(ref SkillData skill,int level) => skill.Radius*(1+0.1f*(level-1));
        public static float Power(ref SkillData skill,int level) => 1+skill.LevelPower*(level-1);
    }
}
