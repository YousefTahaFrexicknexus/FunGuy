Shader "FunGuy/Volumetric Sun Shafts"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 0.9, 0.7, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.85
        _Density ("Density", Range(0.001, 0.25)) = 0.07
        _EdgeFade ("Edge Fade", Range(0.02, 0.5)) = 0.22
        _ForwardScattering ("Forward Scattering", Range(0, 0.7)) = 0.35
        _MaxDistance ("Camera Range", Range(4, 100)) = 44
        [Enum(Low, 8, Medium, 16, High, 24)] _Steps ("Samples", Float) = 16
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" "DisableBatching" = "True" }

        Pass
        {
            Name "VolumetricSunShafts"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend One One
            ColorMask RGB
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Intensity;
                float _Density;
                float _EdgeFade;
                float _ForwardScattering;
                float _MaxDistance;
                float _Steps;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float2 IntersectUnitCube(float3 origin, float3 direction)
            {
                float3 inverse = lerp(-1.0, 1.0, step(0.0, direction))
                    / max(abs(direction), 0.00001);
                float3 first = (-0.5 - origin) * inverse;
                float3 second = (0.5 - origin) * inverse;
                float3 nearHit = min(first, second);
                float3 farHit = max(first, second);
                return float2(max(nearHit.x, max(nearHit.y, nearHit.z)),
                    min(farHit.x, min(farHit.y, farHit.z)));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    return 0;
                #else
                    float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                    float3 rayOrigin = GetCameraPositionWS();
                    float3 rayDirection = normalize(input.positionWS - rayOrigin);
                    if (!IsPerspectiveProjection())
                    {
                        rayOrigin = ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                        rayDirection = GetViewForwardDir();
                    }

                    // Keep the ray parameter in world units even when the box is nonuniformly scaled.
                    float3 originOS = TransformWorldToObject(rayOrigin);
                    float3 directionOS = TransformWorldToObjectDir(rayDirection, false);
                    float2 interval = IntersectUnitCube(originOS, directionOS);
                    float start = max(0.0, interval.x);
                    float end = min(_MaxDistance, interval.y);
                    float rawDepth = SampleSceneDepth(uv);
                    #if UNITY_REVERSED_Z
                        bool hasSurface = rawDepth > 0.00001;
                        float deviceDepth = rawDepth;
                    #else
                        bool hasSurface = rawDepth < 0.99999;
                        float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    #endif
                    if (hasSurface)
                    {
                        float3 surface = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                        end = min(end, dot(surface - rayOrigin, rayDirection));
                    }
                    if (end <= start || _Intensity <= 0.0)
                        return 0;

                    Light sun = GetMainLight();
                    half4 shadowParams = GetMainLightShadowParams();
                    // Use the full contrast of the canopy inside the volume, independent of surface shadow strength.
                    shadowParams.x = 1.0;
                    ShadowSamplingData shadowSampling = GetMainLightShadowSamplingData();
                    int steps = clamp((int)_Steps, 1, 24);
                    float stepLength = (end - start) / steps;
                    float jitter = 0.1 + 0.8 * frac(52.9829189 *
                        frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                    float transmittance = 1.0;
                    float scattering = 0.0;

                    [loop]
                    for (int i = 0; i < steps; ++i)
                    {
                        float distance = start + (i + jitter) * stepLength;
                        float3 positionOS = originOS + directionOS * distance;
                        float3 positionWS = rayOrigin + rayDirection * distance;
                        float3 edge = 0.5 - abs(positionOS);
                        float envelope = smoothstep(0.0, _EdgeFade, min(edge.x, min(edge.y, edge.z)));
                        float cameraFade = 1.0 - smoothstep(_MaxDistance * 0.7, _MaxDistance, distance);
                        float density = _Density * envelope * cameraFade;
                        float stepTransmittance = exp(-density * stepLength);
                        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                        float visibility = MainLightRealtimeShadow(shadowCoord, shadowParams, shadowSampling);
                        visibility *= 1.0 - GetMainLightShadowFade(positionWS);
                        scattering += transmittance * (1.0 - stepTransmittance) * visibility;
                        transmittance *= stepTransmittance;
                    }

                    float g = _ForwardScattering;
                    float cosTheta = dot(rayDirection, sun.direction);
                    float phase = (1.0 - g * g) / pow(max(0.1, 1.0 + g * g - 2.0 * g * cosTheta), 1.5);
                    return half4(_Tint.rgb * sun.color * (_Intensity * scattering * phase), 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
