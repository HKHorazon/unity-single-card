Shader "CardSystem/CardParallaxDissolve"
{
    Properties
    {
        _FrameTex ("Frame Texture", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BGTex ("Background Texture", 2D) = "white" {}
        _TextTex ("Text Snapshot", 2D) = "black" {}
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}
        _GlazeTex ("Glaze/Sweep Texture", 2D) = "black" {}

        _CardAspect ("Card Aspect (W/H)", Float) = 0.75

        _MainDepth ("Main Parallax Depth", Float) = 0.05
        _BGDepth ("BG Parallax Depth", Float) = -0.03
        _TextDepth ("Text Parallax Depth", Float) = 0.02

        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Burn Width", Range(0, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Burn Color", Color) = (1, 0.4, 0, 1)

        _SweepProgress ("Sweep Progress", Range(0,1)) = 0
        _GlazeIntensity ("Glaze Intensity", Range(0,2)) = 1

        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.08
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PARALLAX_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                #if defined(PARALLAX_ON)
                float3 viewDirTS   : TEXCOORD1;
                #endif
            };

            TEXTURE2D(_FrameTex); SAMPLER(sampler_FrameTex);
            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_BGTex);    SAMPLER(sampler_BGTex);
            TEXTURE2D(_TextTex);  SAMPLER(sampler_TextTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_GlazeTex); SAMPLER(sampler_GlazeTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrameTex_ST;
                float4 _MainTex_ST;
                float4 _BGTex_ST;
                float4 _TextTex_ST;
                float4 _NoiseTex_ST;
                float4 _GlazeTex_ST;
                float  _CardAspect;
                float  _MainDepth;
                float  _BGDepth;
                float  _TextDepth;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _SweepProgress;
                float  _GlazeIntensity;
                float  _CornerRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                #if defined(PARALLAX_ON)
                float3 normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * IN.tangentOS.w;

                float3 posWS     = TransformObjectToWorld(IN.positionOS.xyz);
                float3 viewDirWS = normalize(GetCameraPositionWS() - posWS);

                OUT.viewDirTS = float3(
                    dot(viewDirWS, tangentWS),
                    dot(viewDirWS, bitangentWS),
                    dot(viewDirWS, normalWS)
                );
                #endif

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // rounded-rect SDF on full mesh UV (no outline ring)
                float aspect = max(_CardAspect, 0.0001);
                float2 p     = uv - 0.5;
                float2 p_w   = float2(p.x * aspect, p.y);
                float2 innerHalf_w = float2(0.5 * aspect, 0.5);
                float r      = min(_CornerRadius, min(innerHalf_w.x, innerHalf_w.y));
                float2 dd    = abs(p_w) - innerHalf_w + r;
                float dist   = length(max(dd, 0)) + min(max(dd.x, dd.y), 0) - r;
                if (dist > 0) clip(-1);

                float2 cardUv = uv;

                float2 parallaxOffset = float2(0, 0);
                #if defined(PARALLAX_ON)
                float3 viewDirTS = normalize(IN.viewDirTS);
                float vz = max(abs(viewDirTS.z), 0.001) * sign(viewDirTS.z + 0.0001);
                parallaxOffset = viewDirTS.xy / vz;
                #endif

                half4 color = half4(0, 0, 0, 0);

                // card content compositing using cardUv
                {
                    float2 uv_corrected = float2(cardUv.x, (cardUv.y - 0.5) / _CardAspect + 0.5);

                    float2 uv_frame    = cardUv;
                    float2 uv_main_st  = TRANSFORM_TEX(uv_corrected, _MainTex);
                    float2 uv_main     = uv_main_st + parallaxOffset * _MainDepth;
                    float2 uv_bg       = TRANSFORM_TEX(uv_corrected, _BGTex) + parallaxOffset * _BGDepth;
                    float2 uv_text     = cardUv + parallaxOffset * _TextDepth;

                    half4 bgCol    = SAMPLE_TEXTURE2D(_BGTex,    sampler_BGTex,    uv_bg);
                    half4 mainCol  = SAMPLE_TEXTURE2D(_MainTex,  sampler_MainTex,  uv_main);
                    bool mainInBounds = uv_main_st.x >= 0 && uv_main_st.x <= 1 &&
                                        uv_main_st.y >= 0 && uv_main_st.y <= 1;
                    mainCol.a *= mainInBounds ? 1.0 : 0.0;
                    half4 frameCol = SAMPLE_TEXTURE2D(_FrameTex, sampler_FrameTex, uv_frame);
                    half4 textCol  = SAMPLE_TEXTURE2D(_TextTex,  sampler_TextTex,  uv_text);
                    bool textInBounds = uv_text.x >= 0 && uv_text.x <= 1 &&
                                        uv_text.y >= 0 && uv_text.y <= 1;
                    textCol.a *= textInBounds ? 1.0 : 0.0;

                    color = bgCol;
                    color.rgb = lerp(color.rgb, mainCol.rgb,  mainCol.a);
                    color.rgb = lerp(color.rgb, frameCol.rgb, frameCol.a);
                    color.rgb = lerp(color.rgb, textCol.rgb,  textCol.a);

                    float2 uv_glaze = cardUv * _GlazeTex_ST.xy + _GlazeTex_ST.zw;
                    #if defined(PARALLAX_ON)
                    uv_glaze += viewDirTS.xy * 0.5 + _SweepProgress;
                    #else
                    uv_glaze += _SweepProgress;
                    #endif
                    half4 glazeCol = SAMPLE_TEXTURE2D(_GlazeTex, sampler_GlazeTex, uv_glaze);
                    color.rgb += glazeCol.rgb * glazeCol.a * _GlazeIntensity;

                    half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, cardUv).r;
                    float burn = _DissolveAmount + _EdgeWidth;
                    if (noise < burn && noise >= _DissolveAmount)
                    {
                        float t = 1.0 - (noise - _DissolveAmount) / max(_EdgeWidth, 0.0001);
                        color.rgb += _EdgeColor.rgb * t;
                    }
                    clip(noise - _DissolveAmount);
                    color.a = 1.0;
                }

                return color;
            }
            ENDHLSL
        }
    }
}
