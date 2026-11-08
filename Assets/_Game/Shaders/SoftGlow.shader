Shader "TowerTrials/SoftGlow"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Shape ("Shape: glow, ring, ribbon", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Shape;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                half mask = pow(saturate(1 - radius), 2);
                if (_Shape > 0.5 && _Shape < 1.5) mask = saturate(1 - abs(radius - 0.72) * 18);
                if (_Shape > 1.5) mask = saturate(1 - abs(input.uv.y * 2 - 1)) * sin(saturate(input.uv.x) * PI);
                half4 color = _Tint * input.color;
                color.a *= mask;
                return color;
            }
            ENDHLSL
        }
    }
}
