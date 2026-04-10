Shader "DLNK Shaders/ASE/Nature/WaterSimple"
{
	Properties
	{
		_UVScale("UVScale", Float) = 1
		_ColorA("Color A", Color) = (0.2971698,0.6247243,1,0)
		_ColorB("Color B", Color) = (0.09838911,0.1034623,0.3113208,0)
		_NormalA("Normal A", 2D) = "bump" {}
		_NormalB("Normal B", 2D) = "bump" {}
		_NormalScale("NormalScale", Float) = 1
		_SpecXYSnsZW("Spec(XY)Sns(ZW)", Vector) = (0.1,0,0.5,0.2)
		_VelocityXYFoamZ("Velocity(XY)Foam(Z)", Vector) = (0.03,-0.05,0.04,0)
		_Depth("Depth", Float) = 0.9
		_Falloff("Falloff", Float) = -3
		_Distorsion("Distorsion", Float) = 0.1
		_ColorFoam("ColorFoam", Color) = (0.9386792,0.9671129,1,0)
		_FoamMask("FoamMask", 2D) = "white" {}
		_FoamTiling("FoamTiling", Float) = 1
		_FoamDepth("FoamDepth", Float) = 0.9
		_FoamFalloff("FoamFalloff", Float) = -3
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
			float _UVScale;
			float4 _ColorA;
			float4 _ColorB;
			float _NormalScale;
			float4 _SpecXYSnsZW;
			float4 _VelocityXYFoamZ;
			float _Depth;
			float _Falloff;
			float _Distorsion;
			float4 _ColorFoam;
			float _FoamTiling;
			float _FoamDepth;
			float _FoamFalloff;
			CBUFFER_END

			TEXTURE2D(_NormalA);
			SAMPLER(sampler_NormalA);
			TEXTURE2D(_NormalB);
			SAMPLER(sampler_NormalB);
			TEXTURE2D(_FoamMask);
			SAMPLER(sampler_FoamMask);

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				float fogCoord : TEXCOORD1;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionHCS = vertexInput.positionCS;
				output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
				output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				float2 worldUv = input.worldPos.xz * max(_UVScale, 0.0001);
				float2 flowA = _VelocityXYFoamZ.xx * _Time.y;
				float2 flowB = _VelocityXYFoamZ.yy * _Time.y;
				float2 foamFlow = _VelocityXYFoamZ.zz * _Time.y;

				float2 waveA = SAMPLE_TEXTURE2D(_NormalA, sampler_NormalA, worldUv + flowA).rg * 2.0 - 1.0;
				float2 waveB = SAMPLE_TEXTURE2D(_NormalB, sampler_NormalB, worldUv + flowB).rg * 2.0 - 1.0;
				float2 wave = (waveA + waveB) * (0.5 * max(_NormalScale, 0.0));

				float depthBias = saturate(_Depth / (1.0 + abs(_Falloff)));
				float waveMix = saturate(0.5 + (wave.x + wave.y) * (0.25 + _Distorsion * 0.25) + depthBias * 0.15);

				float2 foamUv = worldUv * max(_FoamTiling, 0.0001) + foamFlow + wave * (0.05 + _Distorsion * 0.05);
				float foam = SAMPLE_TEXTURE2D(_FoamMask, sampler_FoamMask, foamUv).r;
				foam = saturate(foam * (0.55 + _FoamDepth * 0.35) + abs(_FoamFalloff) * 0.05);

				half3 baseColor = lerp(_ColorA.rgb, _ColorB.rgb, waveMix);
				half3 color = lerp(baseColor, _ColorFoam.rgb, foam);
				color += waveMix * _SpecXYSnsZW.x * 0.1;
				color = MixFog(color, input.fogCoord);

				half alpha = saturate(0.55 + foam * 0.25 + abs(wave.x + wave.y) * 0.1);
				return half4(color, alpha);
			}
			ENDHLSL
		}
	}

	FallBack Off
}
