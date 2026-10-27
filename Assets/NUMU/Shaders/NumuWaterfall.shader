// Scrolling cartoon waterfall sheet. UV.x runs across the fall, UV.y from lip (0) to base (1).
Shader "NUMU/Waterfall"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.62,0.90,0.98,1)
        _BottomColor("Bottom Color", Color) = (0.86,0.97,1.0,1)
        _StreakColor("Streak Color", Color) = (1,1,1,1)
        _Speed("Speed", Float) = 1.2
        _StreakScale("Streak Scale", Float) = 7
        _Opacity("Opacity", Range(0,1)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _StreakColor;
                float _Speed;
                float _StreakScale;
                half _Opacity;
            CBUFFER_END

            float _NumuMotion;

            half4 _NumuHazeColor;
            float4 _NumuHazeParams; // x: start z, y: end z, z: max amount
            float4 _NumuHazeSide;   // x: start |x|, y: end |x|, z: max amount

            half3 NumuHaze(half3 col, float3 positionWS)
            {
                float hz = saturate((positionWS.z - _NumuHazeParams.x) / max(_NumuHazeParams.y - _NumuHazeParams.x, 0.001));
                hz = hz * hz * (3.0 - 2.0 * hz) * _NumuHazeParams.z;
                float hx = saturate((abs(positionWS.x) - _NumuHazeSide.x) / max(_NumuHazeSide.y - _NumuHazeSide.x, 0.001));
                hx = hx * hx * (3.0 - 2.0 * hx) * _NumuHazeSide.z;
                return lerp(col, _NumuHazeColor.rgb, max(hz, hx));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed * (0.15 + 0.85 * _NumuMotion);
                float s1 = vnoise(float2(i.uv.x * _StreakScale, i.uv.y * 2.0 - t));
                float s2 = vnoise(float2(i.uv.x * _StreakScale * 2.1 + 3.7, i.uv.y * 3.5 - t * 1.4));
                half streak = smoothstep(0.55h, 0.85h, s1 * 0.6 + s2 * 0.4);
                half3 col = lerp(_TopColor.rgb, _BottomColor.rgb, i.uv.y);
                col = lerp(col, _StreakColor.rgb, streak * 0.85h);
                // Whiter foam at the lip and the base.
                col = lerp(col, _StreakColor.rgb, smoothstep(0.82h, 1.0h, i.uv.y) * 0.8h + smoothstep(0.08h, 0.0h, i.uv.y) * 0.6h);
                half edge = smoothstep(0.0h, 0.14h, i.uv.x) * smoothstep(1.0h, 0.86h, i.uv.x);
                half alpha = _Opacity * edge * smoothstep(0.0h, 0.04h, i.uv.y);
                alpha = saturate(alpha * (0.75h + 0.25h * streak));
                col = NumuHaze(col, i.positionWS);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
