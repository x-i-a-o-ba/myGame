using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 编译后某一阶段的数值展示快照；不持有资源、Blob 或实体，不参与命中结算。
    /// 距离为 Unity 世界单位；扇形使用半角，直线区域使用半宽。
    /// </summary>
    public struct SkillAimPreview
    {
        public AimMode Aim;
        public TargetScope Scope;
        public AreaShape Shape;
        public DeliveryKind Delivery;
        public float CastRange,Range,Radius,HitRadius,HalfAngleDegrees;
        public float ArcHeight,FlightSeconds,LaunchHeight,ArrivalHeight;
        public Color IndicatorColor;

        /// <summary>复制已编译阶段；旧示例或无效阶段索引明确拒绝，返回值不依赖 Blob 生命周期。</summary>
        public static SkillAimPreview From(ref SkillData skill,int stageIndex=0)
        {
            if(stageIndex<0 || stageIndex>=skill.Stages.Length)
                throw new ArgumentOutOfRangeException(nameof(stageIndex),"指示器需要有效的组合技能阶段。");
            ref var stage=ref skill.Stages[stageIndex];
            return new SkillAimPreview
            {
                Aim=skill.Aim,Scope=stage.Scope,Shape=stage.Shape,Delivery=stage.Delivery,
                CastRange=skill.CastRange,Range=stage.Range,Radius=stage.Radius,
                HitRadius=stage.HitRadius,HalfAngleDegrees=stage.HalfAngleDegrees,
                ArcHeight=stage.ArcHeight,FlightSeconds=stage.FlightSeconds,LaunchHeight=stage.LaunchHeight,ArrivalHeight=stage.ArrivalHeight,
                IndicatorColor=new Color(skill.IndicatorColor.x,skill.IndicatorColor.y,skill.IndicatorColor.z,skill.IndicatorColor.w)
            };
        }
    }

    /// <summary>
    /// Game 与编辑器共用的指示器轮廓生成。只消费数值快照，无 Transform 缩放或场景查找。
    /// 调用方拥有并复用输出列表；轮廓在中心的 XZ 平面，末顶点与首顶点相同。
    /// 范围结算仍由 SkillTargeting 完成，显示线条厚度不改变命中范围。
    /// </summary>
    public static class SkillPreviewGeometry
    {
        /// <summary>
        /// 立即投递按瞄准方式取中心；落点弹体取落点；直线弹体的 point 应由调用者提供为假设首碰点。
        /// 直线首碰点未知时不代表必然在那里爆炸，界面必须标明该预览是假设。
        /// </summary>
        public static Vector3 AreaCenter(in SkillAimPreview preview,Vector3 origin,Vector3 point)
        {
            if(preview.Delivery==DeliveryKind.ParabolicProjectile) return point+Vector3.up*preview.ArrivalHeight;
            if(preview.Delivery!=DeliveryKind.Instant) return point;
            return preview.Aim==AimMode.Point || preview.Aim==AimMode.Unit?point:origin;
        }

        /// <summary>落点弹体沿起点到落点定向；零距离直接结算时保留施法方向，其余投递也消费施法方向。</summary>
        public static Vector3 DeliveryDirection(in SkillAimPreview preview,Vector3 origin,Vector3 point,Vector3 aimDirection)
        {
            Vector3 travel=point-origin; travel.y=0;
            if((preview.Delivery==DeliveryKind.PointProjectile || preview.Delivery==DeliveryKind.ParabolicProjectile) && travel.sqrMagnitude>=0.00000001f)
                return travel.normalized;
            return PlanarDirection(aimDirection);
        }

        /// <summary>区域为圆/扇/矩形；自身、单体和全体目标没有有限区域轮廓，返回空列表。</summary>
        public static void AreaOutline(in SkillAimPreview preview,Vector3 center,Vector3 direction,List<Vector3> output,int segments=64)
        {
            if(output==null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if(preview.Scope!=TargetScope.Area) return;
            segments=Mathf.Clamp(segments,8,512);
            Vector3 forward=PlanarDirection(direction);
            Vector3 side=new Vector3(forward.z,0,-forward.x);
            switch(preview.Shape)
            {
                case AreaShape.Circle:
                    Arc(center,forward,side,preview.Radius,0,360,segments,output);
                    output[output.Count-1]=output[0];
                    break;
                case AreaShape.Cone:
                    if(preview.HalfAngleDegrees>=180)
                    {
                        Arc(center,forward,side,preview.Range,0,360,segments,output);
                        output[output.Count-1]=output[0];
                    }
                    else
                    {
                        output.Add(center);
                        Arc(center,forward,side,preview.Range,-preview.HalfAngleDegrees,preview.HalfAngleDegrees,segments,output);
                        output.Add(center);
                    }
                    break;
                case AreaShape.Line:
                    Vector3 width=side*preview.Radius;
                    Vector3 end=center+forward*preview.Range;
                    output.Add(center-width); output.Add(end-width); output.Add(end+width);
                    output.Add(center+width); output.Add(center-width);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preview),"不支持的指示器区域形状。");
            }
        }

        /// <summary>直线弹体完整飞行段的扫掠圆轮廓，包含起止圆帽；其他投递返回空列表。</summary>
        public static void ProjectileOutline(in SkillAimPreview preview,Vector3 origin,Vector3 direction,List<Vector3> output,int segments=64)
        {
            if(output==null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if(preview.Delivery!=DeliveryKind.LinearProjectile) return;
            int halfSegments=Mathf.Clamp(segments,8,512)/2;
            Vector3 forward=PlanarDirection(direction);
            Vector3 side=new Vector3(forward.z,0,-forward.x);
            Vector3 end=origin+forward*preview.Range;
            Arc(end,forward,side,preview.HitRadius,-90,90,halfSegments,output);
            Arc(origin,forward,side,preview.HitRadius,90,270,halfSegments,output);
            output.Add(output[0]);
        }

        private static Vector3 PlanarDirection(Vector3 direction)
        {
            direction.y=0;
            return direction.sqrMagnitude>0.000001f?direction.normalized:Vector3.forward;
        }

        private static void Arc(Vector3 center,Vector3 forward,Vector3 side,float radius,float startDegrees,float endDegrees,int segments,List<Vector3> output)
        {
            for(int i=0;i<=segments;i++)
            {
                float radians=Mathf.Lerp(startDegrees,endDegrees,i/(float)segments)*Mathf.Deg2Rad;
                output.Add(center+radius*(forward*Mathf.Cos(radians)+side*Mathf.Sin(radians)));
            }
        }
    }
}
