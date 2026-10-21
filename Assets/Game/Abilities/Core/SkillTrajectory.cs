using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>执行与预览共享的定时三维二次弧线；无物理重力、沿途碰撞或伤害结算。</summary>
    public static class SkillTrajectory
    {
        /// <summary>start/end 为包含高度偏移的世界位置；t 限定在 0..1，附加高度在 t=0.5 达到 arcHeight。</summary>
        public static float3 Evaluate(float3 start,float3 end,float arcHeight,float normalizedTime)
        {
            float t=math.saturate(normalizedTime);
            return math.lerp(start,end,t)+new float3(0,arcHeight*(4*t*(1-t)),0);
        }

        /// <summary>输入未加偏移的施法起点与目标点；输出包含起终点。调用方复用列表，非弧线投递返回空列表。</summary>
        public static void Sample(in SkillAimPreview preview,Vector3 origin,Vector3 target,List<Vector3> output,int segments=32)
        {
            if(output==null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if(preview.Delivery!=DeliveryKind.ParabolicProjectile) return;
            segments=Mathf.Clamp(segments,2,512);
            float3 start=(float3)origin+new float3(0,preview.LaunchHeight,0);
            float3 end=(float3)target+new float3(0,preview.ArrivalHeight,0);
            for(int i=0;i<=segments;i++) output.Add((Vector3)Evaluate(start,end,preview.ArcHeight,i/(float)segments));
        }
    }
}
