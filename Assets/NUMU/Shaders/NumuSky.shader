// Soft pastel gradient skybox with a warm glow towards the sun.
Shader "NUMU/Sky"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.93,0.84,0.86,1)
        _HorizonColor("Horizon Color", Color) = (1.0,0.92,0.84,1)
        _BottomColor("Bottom Color", Color) = (0.80,0.90,0.95,1)
        _GlowColor("Sun Glow", Color) = (1.0,0.88,0.70,1)
        _GlowDirection("Glow Direction", Vector) = (-0.5,0.25,0.8,0)
        _GlowPower("Glow Power", Float) = 6
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _TopColor;
            half4 _HorizonColor;
            half4 _BottomColor;
            half4 _GlowColor;
            float4 _GlowDirection;
            float _GlowPower;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 col = d.y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(d.y), 0.6))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-d.y), 0.5));
                half glow = pow(saturate(dot(d, normalize(_GlowDirection.xyz))), _GlowPower);
                col += _GlowColor.rgb * glow * 0.35h;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
