Shader "Hidden/Funguy/LandingPrediction"
{
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 position : POSITION; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float4 color : COLOR; };
            Output vert(Input input)
            {
                Output output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                return output;
            }
            half4 frag(Output input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
