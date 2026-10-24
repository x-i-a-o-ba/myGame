using Unity.Mathematics;

namespace Roguelike.Abilities
{
    /// <summary>指示器与结算共享的平面几何。范围以米计；扇形角度为半角。</summary>
    public static class SkillTargeting
    {
        public static float3 ClampPoint(float3 origin, float3 point, float range)
        {
            float3 offset = point-origin; offset.y=0;
            return origin + math.normalizesafe(offset)*math.min(math.length(offset),range);
        }
        public static bool Contains(ref StageData stage, float3 origin, float3 direction, float3 point)
            =>Contains(stage.Shape,stage.Radius,stage.Range,stage.HalfAngleDegrees,stage.InnerRadius,origin,direction,point);
        public static bool Contains(AreaShape shape,float radius,float range,float halfAngle,float innerRadius,float3 origin,float3 direction,float3 point)
        {
            float2 delta=point.xz-origin.xz;
            switch(shape)
            {
                case AreaShape.Ring: return math.lengthsq(delta)<=radius*radius && math.lengthsq(delta)>=innerRadius*innerRadius;
                case AreaShape.Circle: return math.lengthsq(delta)<=radius*radius;
                case AreaShape.Cone:
                    return math.lengthsq(delta)<=range*range &&
                        (math.lengthsq(delta)<0.00001f || math.dot(math.normalizesafe(delta),direction.xz)>=math.cos(math.radians(halfAngle)));
                case AreaShape.Line:
                    float along=math.dot(delta,direction.xz);
                    return along>=0 && along<=range && math.lengthsq(delta-direction.xz*along)<=radius*radius;
                default: return false;
            }
        }
        public static float SegmentDistance(float3 start,float3 end,float3 point,out float along)
        {
            float2 segment=end.xz-start.xz;
            along=math.saturate(math.dot(point.xz-start.xz,segment)/math.max(0.000001f,math.lengthsq(segment)));
            return math.distance(point.xz,start.xz+segment*along);
        }
        public static bool FirstContact(float3 start,float3 end,float3 center,float radius,out float fraction)
        {
            float2 offset=start.xz-center.xz,delta=end.xz-start.xz;
            float c=math.dot(offset,offset)-radius*radius;
            fraction=0;
            if(c<=0) return true;
            float a=math.dot(delta,delta),b=math.dot(offset,delta);
            if(a<0.000001f || b>=0) return false;
            float discriminant=b*b-a*c;
            if(discriminant<0) return false;
            fraction=(-b-math.sqrt(discriminant))/a;
            return fraction>=0 && fraction<=1;
        }
    }
}

