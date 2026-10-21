Shader "Hidden/Roguelike/FogSmoothing"
{
    Properties { _MainTex("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _Previous;
        float4 _FilterStep;
        float _Blend;
        // 归一化五抽头高斯核，两次一维滤波避免场景每像素重复多抽头采样。
        half4 Filter(float2 uv, float2 stepSize)
        {
            half4 value = tex2D(_MainTex, uv) * 0.38774;
            value += (tex2D(_MainTex, uv + stepSize) + tex2D(_MainTex, uv - stepSize)) * 0.24477;
            value += (tex2D(_MainTex, uv + stepSize * 2) + tex2D(_MainTex, uv - stepSize * 2)) * 0.06136;
            return value;
        }
        half4 Horizontal(v2f_img i) : SV_Target { return Filter(i.uv, float2(_FilterStep.x, 0)); }
        half4 Vertical(v2f_img i) : SV_Target { return Filter(i.uv, float2(0, _FilterStep.y)); }
        half4 History(v2f_img i) : SV_Target { return lerp(tex2D(_Previous, i.uv), tex2D(_MainTex, i.uv), _Blend); }
        ENDHLSL
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Vertical
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment History
            ENDHLSL
        }
    }
}
