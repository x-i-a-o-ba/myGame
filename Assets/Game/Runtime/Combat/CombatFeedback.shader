Shader "Roguelike/Combat/Feedback"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input {float4 positionOS:POSITION;half4 color:COLOR;};
            struct Output {float4 positionCS:SV_POSITION;half4 color:COLOR;};
            Output Vert(Input v) {Output o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;return o;}
            half4 Frag(Output i):SV_Target {return i.color;}
            ENDHLSL
        }
    }
}
