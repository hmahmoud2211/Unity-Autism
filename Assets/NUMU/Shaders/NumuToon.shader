// Stylized cartoon lighting for the NUMU world.
// Soft two-tone ramp, tinted shade colour, hemisphere ambient, rim light and an optional
// toon highlight. Vertex colours carry most of the albedo so props can share one material;
// vertex alpha is the wind weight for foliage and flags.
Shader "NUMU/Toon"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseMap("Base Map", 2D) = "white" {}
        _VertexColorStrength("Vertex Color Strength", Range(0,1)) = 1
        _ShadeColor("Shade Color", Color) = (0.74,0.70,0.88,1)
        _ShadeStep("Shade Step", Range(-1,1)) = 0.0
        _ShadeSoftness("Shade Softness", Range(0.01,1)) = 0.35
        _ShadowStrength("Received Shadow Strength", Range(0,1)) = 0.8
        _RimColor("Rim Color", Color) = (1,0.95,0.88,1)
        _RimStrength("Rim Strength", Range(0,2)) = 0.18
        _RimPower("Rim Power", Range(0.5,8)) = 3.5
        _HighlightColor("Highlight Color", Color) = (1,1,1,1)
        _HighlightStrength("Highlight Strength", Range(0,2)) = 0
        _HighlightSize("Highlight Size", Range(0.5,0.999)) = 0.96
        [HDR]_EmissionColor("Emission", Color) = (0,0,0,1)
        _AlphaClip("Alpha Clip", Range(0,1)) = 0
        _WindStrength("Wind Strength", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _VertexColorStrength;
            half4 _ShadeColor;
            half _ShadeStep;
            half _ShadeSoftness;
            half _ShadowStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half4 _HighlightColor;
            half _HighlightStrength;
            half _HighlightSize;
            half4 _EmissionColor;
            half _AlphaClip;
            half _WindStrength;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Globals driven by NumuLightingRig.
        half4 _NumuSkyAmbient;
        half4 _NumuGroundAmbient;
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

        float3 NumuWind(float3 positionWS, half weight)
        {
            float t = _Time.y;
            float phase = positionWS.x * 0.35 + positionWS.z * 0.27;
            float s = sin(t * 1.3 + phase) * 0.6 + sin(t * 2.1 + phase * 1.7) * 0.4;
            float3 offset = float3(s, 0.0, s * 0.6) * (_WindStrength * weight * _NumuMotion * 0.12);
            return positionWS + offset;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = NumuWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 vcol = lerp(half3(1.0h, 1.0h, 1.0h), i.color.rgb, _VertexColorStrength);
                half3 albedo = tex.rgb * _BaseColor.rgb * vcol;
                half alpha = tex.a * _BaseColor.a;
                clip(alpha - _AlphaClip);

                half3 n = normalize(i.normalWS);
                n = isFrontFace ? n : -n;
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);

                half ndl = dot(n, light.direction);
                half ramp = smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, ndl);
                half shadow = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
                half lit = ramp * shadow;

                half3 ambient = lerp(_NumuGroundAmbient.rgb, _NumuSkyAmbient.rgb, saturate(n.y * 0.5h + 0.5h));
                half ao = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                    AmbientOcclusionFactor aof = GetScreenSpaceAmbientOcclusion(suv);
                    ao = lerp(1.0h, aof.indirectAmbientOcclusion, 0.8h);
                #endif

                half3 shadeCol = albedo * _ShadeColor.rgb * ambient;
                half3 litCol = albedo * light.color * (0.82h + 0.18h * ambient);
                half3 col = lerp(shadeCol, litCol, lit) * ao;

                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                col += rim * _RimColor.rgb * (0.45h + 0.55h * lit);

                half3 h = normalize(light.direction + viewDir);
                half spec = smoothstep(_HighlightSize, _HighlightSize + 0.012h, dot(n, h)) * _HighlightStrength * lit;
                col += spec * _HighlightColor.rgb;

                col += _EmissionColor.rgb;
                col = NumuHaze(col, i.positionWS);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            V shadowVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = NumuWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, ld));
                o.positionCS = ApplyShadowClamping(cs);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 shadowFrag(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _AlphaClip);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            struct A
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            V depthVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = NumuWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half depthFrag(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _AlphaClip);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex dnVert
            #pragma fragment dnFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct A
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            V dnVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = NumuWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 dnFrag(V i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                clip(a - _AlphaClip);
                float3 normalWS = normalize(i.normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
