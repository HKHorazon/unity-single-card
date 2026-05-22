Shader "CardSystem/CardBackFace"
{
    Properties
    {
        _MainTex ("Back Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Burn Width", Range(0, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Burn Color", Color) = (1, 0.4, 0, 1)
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}

        _CardAspect   ("Card Aspect (W/H)",   Float) = 0.667
        _CornerRadius ("Corner Radius",       Range(0, 0.5)) = 0.08
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
                float2 uv          : TEXCOORD0;  // mirrored U for back-face texture sampling
                float2 uvRaw       : TEXCOORD1;  // unmirrored, for SDF and noise
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _CardAspect;
                float  _CornerRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv    = float2(1.0 - IN.uv.x, IN.uv.y);
                OUT.uvRaw = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uvRaw = IN.uvRaw;

                // rounded-rect SDF on full mesh UV
                float aspect = max(_CardAspect, 0.0001);
                float2 p     = uvRaw - 0.5;
                float2 p_w   = float2(p.x * aspect, p.y);
                float2 innerHalf_w = float2(0.5 * aspect, 0.5);
                float r      = min(_CornerRadius, min(innerHalf_w.x, innerHalf_w.y));
                float2 dd    = abs(p_w) - innerHalf_w + r;
                float dist   = length(max(dd, 0)) + min(max(dd.x, dd.y), 0) - r;
                if (dist > 0) clip(-1);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvRaw).r;
                float burn = _DissolveAmount + _EdgeWidth;
                if (noise < burn && noise >= _DissolveAmount)
                {
                    float t = 1.0 - (noise - _DissolveAmount) / max(_EdgeWidth, 0.0001);
                    color.rgb += _EdgeColor.rgb * t;
                }
                clip(noise - _DissolveAmount);
                color.a = 1.0;

                return color;
            }
            ENDHLSL
        }
    }
}
