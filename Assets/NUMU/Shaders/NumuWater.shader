// Turquoise cartoon ocean. Uses the camera depth texture to tint shallows around
// island bases and draw soft animated foam lines where water meets rock.
Shader "NUMU/Water"
{
    Properties
    {
        _DeepColor("Deep Color", Color) = (0.16,0.62,0.84,1)
        _MidColor("Mid Color", Color) = (0.26,0.74,0.90,1)
        _ShallowColor("Shallow Color", Color) = (0.62,0.92,0.94,1)
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _DepthScale("Depth Scale", Range(0.01,2)) = 0.28
        _ShallowAlpha("Shallow Alpha", Range(0,1)) = 0.55
        _FoamDistance("Foam Distance", Range(0.05,3)) = 0.7
        _FoamRings("Foam Rings", Range(0,1)) = 0.6
        _RippleScale("Ripple Scale", Float) = 0.11
        _RippleStrength("Ripple Strength", Range(0,1)) = 0.35
        _SparkleStrength("Sparkle Strength", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _MidColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half _DepthScale;
                half _ShallowAlpha;
                half _FoamDistance;
                half _FoamRings;
                float _RippleScale;
                half _RippleStrength;
                half _SparkleStrength;
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
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _NumuMotion;
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float rawDepth = SampleSceneDepth(suv);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfEye = i.screenPos.w;
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float waterDepth = max(0.0, sceneEye - surfEye) * saturate(abs(viewDir.y) + 0.05);

                float2 wp = i.positionWS.xz;
                float n1 = vnoise(wp * _RippleScale + float2(t * 0.05, t * 0.03));
                float n2 = vnoise(wp * _RippleScale * 2.3 - float2(t * 0.04, -t * 0.06));
                float ripple = (n1 * 0.6 + n2 * 0.4);

                half deep = 1.0h - exp(-waterDepth * _DepthScale);
                half3 col = lerp(_ShallowColor.rgb, _MidColor.rgb, saturate(deep * 1.6h));
                col = lerp(col, _DeepColor.rgb, saturate(deep * 1.4h - 0.45h));
                col += (ripple - 0.5h) * _RippleStrength * 0.18h;

                // Light caustic streaks across open water.
                float streak = vnoise(float2(wp.x * 0.35 + t * 0.15, wp.y * 1.4));
                streak = smoothstep(0.78, 0.92, streak) * (0.4 + 0.6 * n2);
                col += streak * _SparkleStrength * 0.35h;

                // Foam: a solid shoreline band plus slow rings drifting outwards.
                float foamNoise = vnoise(wp * 0.9 + t * 0.25);
                half shore = 1.0h - saturate(waterDepth / _FoamDistance);
                half foam = smoothstep(0.45h, 0.62h, shore + (foamNoise - 0.5h) * 0.35h);
                float ringPhase = waterDepth * 3.2 - t * 0.9 + foamNoise * 0.8;
                half rings = smoothstep(0.82h, 0.95h, sin(ringPhase * 3.14159)) * saturate(1.0h - waterDepth / (_FoamDistance * 3.5h)) * _FoamRings;
                foam = saturate(foam + rings);
                col = lerp(col, _FoamColor.rgb, foam * 0.92h);

                half alpha = lerp(_ShallowAlpha, 1.0h, saturate(deep * 1.8h));
                alpha = saturate(alpha + foam);

                col = NumuHaze(col, i.positionWS);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
