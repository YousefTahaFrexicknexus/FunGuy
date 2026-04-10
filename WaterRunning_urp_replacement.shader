Shader "DLNK Shaders/ASE/Nature/WaterRunning"
{
	Properties
	{
		_TessPhongStrength("Phong Tess Strength", Range(0, 1)) = 0.897
		_ColorA("Color A", Color) = (1,1,1,0)
		_ColorB("Color B", Color) = (0,0,0,0)
		_ColorMix("Color Mix", Float) = 1
		_MainTex("Albedo", 2D) = "white" {}
		_MainTex1("Albedo B", 2D) = "white" {}
		_Metalness("Metalness", Float) = 0.5
		_Smoothness("Smoothness", Float) = 0.5
		_BumpMap("Normal A", 2D) = "bump" {}
		_BumpMap1("Normal B", 2D) = "bump" {}
		_NormalScale("NormalScale", Float) = 1
		[Header(Refraction)]
		_ChromaticAberration("Chromatic Aberration", Range(0, 0.3)) = 0.1
		_RefractionLevel("Refraction Level", Float) = 1
		_Transparency("Transparency", Float) = 1
		_Tessellation("Tessellation", Range(0, 20)) = 1
		_Displacement("Displacement", Float) = 1
		_Offset("Offset", Float) = 1
		_Speed("Speed", Float) = 1
		_SpeedAB("Speed A (XY) B (ZW)", Vector) = (0,1,0,0.5)
		[HideInInspector] _texcoord("", 2D) = "white" {}
		[HideInInspector] __dirty("", Int) = 1
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}

		Pass
		{
			Name "Forward"
			Tags { "LightMode" = "UniversalForward" }

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Back

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorA;
			float4 _ColorB;
			float _ColorMix;
			float _Metalness;
			float _Smoothness;
			float _NormalScale;
			float _ChromaticAberration;
			float _RefractionLevel;
			float _Transparency;
			float _Displacement;
			float _Offset;
			float _Speed;
			float4 _SpeedAB;
			CBUFFER_END

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_MainTex1);
			SAMPLER(sampler_MainTex1);
			TEXTURE2D(_BumpMap);
			SAMPLER(sampler_BumpMap);
			TEXTURE2D(_BumpMap1);
			SAMPLER(sampler_BumpMap1);

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float fogCoord : TEXCOORD1;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionHCS = vertexInput.positionCS;
				output.uv = input.uv;
				output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				float timeOffset = _Time.y * _Speed;
				float2 flowA = _SpeedAB.xy * timeOffset;
				float2 flowB = _SpeedAB.zw * timeOffset;

				float2 normalUvA = input.uv + flowA;
				float2 normalUvB = input.uv + flowB;
				float2 waveA = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, normalUvA).rg * 2.0 - 1.0;
				float2 waveB = SAMPLE_TEXTURE2D(_BumpMap1, sampler_BumpMap1, normalUvB).rg * 2.0 - 1.0;
				float2 wave = (waveA + waveB) * (0.5 * max(_NormalScale, 0.0));

				float distortion = (_Displacement + _Offset) * 0.02;
				float2 uvA = input.uv + flowA + wave * distortion;
				float2 uvB = input.uv + flowB - wave * distortion;

				half4 texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA);
				half4 texB = SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, uvB);
				half4 albedo = lerp(texA, texB, 0.5);

				float gradient = saturate(input.uv.x * _ColorMix + wave.x * 0.2);
				half3 tint = lerp(_ColorA.rgb, _ColorB.rgb, gradient);

				half3 color = albedo.rgb * tint;
				color += abs(wave.x + wave.y) * (_RefractionLevel * 0.08 + _ChromaticAberration * 0.15);
				color += (_Metalness + _Smoothness) * 0.03;
				color = MixFog(color, input.fogCoord);

				half alpha = saturate(max(albedo.a * _Transparency, saturate(_Transparency) * 0.35));
				return half4(color, alpha);
			}
			ENDHLSL
		}
	}

	FallBack Off
}
