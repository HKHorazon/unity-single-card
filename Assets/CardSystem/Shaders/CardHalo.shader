Shader "CardSystem/CardHalo"
{
    Properties
    {
        [HDR] _HaloColor ("Halo Color", Color) = (1, 0.85, 0.3, 1)
        _HaloIntensity ("Halo Intensity", Range(0, 10)) = 1.5
        _HaloSpread    ("Halo Spread (Gaussian sigma)", Range(0.01, 1.0)) = 0.25
        _HaloPower     ("Halo Power",  Range(0.5, 4.0)) = 1.5

        // Inner rounded rect (where card occludes halo). UV-space, [0..1].
        _HaloInset        ("Inset (UV of card inside halo)", Range(0, 0.45)) = 0.18
        _HaloCornerRadius ("Corner Radius",                  Range(0, 0.5)) = 0.08
        _CardAspect       ("Card Aspect (W/H)",              Float) = 0.667
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            Blend One One   // additive — natural bloom feel; switch to SrcAlpha OneMinusSrcAlpha for premult

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _HaloColor;
                float  _HaloIntensity;
                float  _HaloSpread;
                float  _HaloPower;
                float  _HaloInset;
                float  _HaloCornerRadius;
                float  _CardAspect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // SDF distance to inner rounded rect representing the card occlusion
                float aspect = max(_CardAspect, 0.0001);
                float inset  = _HaloInset;
                float2 p     = uv - 0.5;
                float2 p_w   = float2(p.x * aspect, p.y);
                float2 innerHalf_w = float2((0.5 - inset) * aspect, 0.5 - inset);
                float r      = min(_HaloCornerRadius, min(innerHalf_w.x, innerHalf_w.y));
                float2 dd    = abs(p_w) - innerHalf_w + r;
                float dist   = length(max(dd, 0)) + min(max(dd.x, dd.y), 0) - r;

                // halo only outside the card — inside is occluded
                if (dist <= 0) { clip(-1); }

                float sigma = max(_HaloSpread, 0.001);
                float g = exp(-pow(dist / sigma, _HaloPower));
                float mag = g * _HaloIntensity;
                if (mag < 0.001) { clip(-1); }

                half4 c;
                c.rgb = _HaloColor.rgb * mag;
                c.a   = saturate(mag);
                return c;
            }
            ENDHLSL
        }
    }
}
