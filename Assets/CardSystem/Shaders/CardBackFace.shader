Shader "CardSystem/CardBackFace"
{
    Properties
    {
        _MainTex ("Back Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Burn Width", Range(0, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Burn Color", Color) = (1, 0.4, 0, 1)
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uvNoise     : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // mirror U so back face main texture reads correctly
                OUT.uv      = float2(1.0 - IN.uv.x, IN.uv.y);
                // keep noise UV unmirrored → dissolve pattern appears L-R flipped vs front
                OUT.uvNoise = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uvNoise).r;
                float edge = _DissolveAmount + _EdgeWidth;
                if (noise < edge && noise >= _DissolveAmount)
                {
                    float t = 1.0 - (noise - _DissolveAmount) / max(_EdgeWidth, 0.0001);
                    color.rgb += _EdgeColor.rgb * t;
                }
                clip(noise - _DissolveAmount);

                return color;
            }
            ENDHLSL
        }
    }
}
