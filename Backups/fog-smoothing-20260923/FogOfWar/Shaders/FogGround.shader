Shader "Roguelike/FogDemo/WorldVisibility"
{
    Properties { _BaseColor("Base Color",Color)=(0.3,0.45,0.4,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DemoFogMap); SAMPLER(sampler_DemoFogMap);
            float4 _DemoFogBounds; float _DemoFogOmniscient;
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 vertex:POSITION; };
            struct V { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; };
            V vert(A a) { V o; o.world=TransformObjectToWorld(a.vertex.xyz); o.vertex=TransformWorldToHClip(o.world); return o; }
            half4 frag(V i):SV_Target
            {
                float2 uv=(i.world.xz-_DemoFogBounds.xy)/_DemoFogBounds.zw;
                half2 fog=SAMPLE_TEXTURE2D(_DemoFogMap,sampler_DemoFogMap,uv).rg;
                half brightness=max(_DemoFogOmniscient,lerp(0.025,0.26,fog.g));
                brightness=max(brightness,fog.r);
                return half4(_BaseColor.rgb*brightness,1);
            }
            ENDHLSL
        }
    }
}
