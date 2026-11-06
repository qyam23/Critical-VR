Shader "CriticalVR/PassthroughTrip"
{
    Properties
    {
        [HideInInspector] _MainTex("MainTex", 2D) = "white" {}
        _IntensityScore("Intensity Score", Range(0, 1)) = 0
        _MotionSignal("Motion Signal", Range(0, 1)) = 0
        _EdgeGlow("Edge Glow", Range(0, 1)) = 0
        _DistortionAmount("Distortion Amount", Range(0, 0.05)) = 0
        _PatternAmount("Pattern Amount", Range(0, 1)) = 0
        _ChromaticAmount("Chromatic Amount", Range(0, 0.05)) = 0
        _ColorAtmosphere("Color Atmosphere", Range(0, 1)) = 0
        _PeripheralBlur("Peripheral Blur", Range(0, 1)) = 0
        _BreathingAmount("Breathing Amount", Range(0, 1)) = 0
        _BreathingPhase("Breathing Phase", Float) = 0
        _AfterImageAmount("After Image Amount", Range(0, 1)) = 0
        _OverdriveAmount("Overdrive Amount", Range(0, 1)) = 0
        _PeripheralWeight("Peripheral Weight", Range(0, 1)) = 0.85
        _CenterStability("Center Stability", Range(0, 1)) = 0.75
        _IntroFade("Intro Fade", Range(0, 1)) = 1
        _PeakHold("Peak Hold", Range(0, 1)) = 0
        _StateIndex("State Index", Float) = 0
        _ModeIndex("Mode Index", Float) = 0
        _EdgeColor("Edge Color", Color) = (0.4, 0.9, 1, 1)
        _AtmosphereColor("Atmosphere Color", Color) = (0.35, 0.2, 0.45, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" }
        ZWrite Off
        Cull Off
        Blend Off
        ZTest Always

        Pass
        {
            Name "CriticalVRFullscreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _IntensityScore;
                float _MotionSignal;
                float _EdgeGlow;
                float _DistortionAmount;
                float _PatternAmount;
                float _ChromaticAmount;
                float _ColorAtmosphere;
                float _PeripheralBlur;
                float _BreathingAmount;
                float _BreathingPhase;
                float _AfterImageAmount;
                float _OverdriveAmount;
                float _PeripheralWeight;
                float _CenterStability;
                float _IntroFade;
                float _PeakHold;
                float _StateIndex;
                float _ModeIndex;
                float4 _EdgeColor;
                float4 _AtmosphereColor;
            CBUFFER_END

            float Luminance(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 78.233);
                return frac(p.x * p.y);
            }

            float Noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float EdgeStrength(float2 uv)
            {
                float2 texel = _BlitTexture_TexelSize.xy;
                float lum00 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1, -1)).rgb);
                float lum10 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 0, -1)).rgb);
                float lum20 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1, -1)).rgb);
                float lum01 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1,  0)).rgb);
                float lum21 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1,  0)).rgb);
                float lum02 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1,  1)).rgb);
                float lum12 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 0,  1)).rgb);
                float lum22 = Luminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1,  1)).rgb);

                float sobelX = lum20 + (2.0 * lum21) + lum22 - lum00 - (2.0 * lum01) - lum02;
                float sobelY = lum02 + (2.0 * lum12) + lum22 - lum00 - (2.0 * lum10) - lum20;
                return saturate(sqrt((sobelX * sobelX) + (sobelY * sobelY)) * 1.45);
            }

            float PeripheralMask(float2 uv)
            {
                float2 centered = (uv * 2.0) - 1.0;
                float radius = length(centered);
                float peripheral = smoothstep(_CenterStability * 0.35, 1.05, radius);
                return saturate(peripheral * _PeripheralWeight);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float peripheralMask = PeripheralMask(uv);
                float breathingWave = sin(_BreathingPhase + peripheralMask * 2.6) * 0.5 + 0.5;

                float2 noiseUv = uv * lerp(7.0, 18.0, _ModeIndex) + float2(_BreathingPhase * 0.04, _BreathingPhase * 0.025);
                float noiseA = Noise(noiseUv);
                float noiseB = Noise(noiseUv * 1.7 + 19.37);
                float wobble = ((noiseA - 0.5) + (noiseB - 0.5)) * peripheralMask * _DistortionAmount;

                float2 radialDir = normalize(((uv * 2.0) - 1.0) + 1e-5);
                float2 distortionOffset = radialDir * wobble * float2(1.0, 0.82);

                float edge = EdgeStrength(uv + distortionOffset);
                float3 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + distortionOffset).rgb;
                float luminance = Luminance(baseColor);

                float highlightMask = saturate((luminance - 0.55) * 2.4) * peripheralMask;
                float2 chromaOffset = radialDir * (_ChromaticAmount * (0.35 + highlightMask));
                float red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + distortionOffset + chromaOffset).r;
                float green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + distortionOffset).g;
                float blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + distortionOffset - chromaOffset).b;
                float3 chromaColor = float3(red, green, blue);

                float pattern = Noise(noiseUv * 2.4 + edge * 5.0 + luminance * 3.0);
                float patternMask = saturate(edge * 0.65 + highlightMask * 0.55 + peripheralMask * 0.35);
                float patternLayer = saturate((pattern - 0.35) * 1.6) * _PatternAmount * patternMask;

                float3 edgeTint = _EdgeColor.rgb * edge * _EdgeGlow * (0.35 + peripheralMask * 0.8);
                float3 atmosphere = _AtmosphereColor.rgb * _ColorAtmosphere * (0.15 + peripheralMask * 0.85);
                float3 overdrive = lerp(float3(0.9, 0.1, 0.6), float3(0.1, 1.0, 0.95), noiseB) * _OverdriveAmount * (0.18 + peripheralMask * 0.75);
                float persistence = _AfterImageAmount * (0.25 + breathingWave * 0.75) * peripheralMask;

                float vignette = saturate(peripheralMask * (_PeripheralBlur * 0.85 + _IntensityScore * 0.15));
                float3 graded = lerp(chromaColor, chromaColor + edgeTint + atmosphere, saturate(_EdgeGlow + _ColorAtmosphere * 0.55));
                graded += patternLayer * lerp(float3(0.12, 0.3, 0.55), float3(0.95, 0.2, 0.75), _ModeIndex);
                graded += persistence * lerp(float3(0.18, 0.08, 0.3), float3(0.55, 0.18, 0.08), _ModeIndex);
                graded += overdrive;
                graded = lerp(graded, graded * (1.0 + vignette * 0.35), peripheralMask);
                graded = lerp(graded, graded + (breathingWave - 0.5) * _BreathingAmount * 0.4, peripheralMask);

                float introAlpha = saturate(1.0 - _IntroFade);
                float finalAlpha = saturate(introAlpha * max(_IntensityScore, _ColorAtmosphere * 0.65 + _PatternAmount * 0.25 + _OverdriveAmount * 0.4));
                float3 finalColor = lerp(baseColor, graded, saturate(0.25 + finalAlpha * 0.9));
                finalColor = lerp(finalColor, float3(0.0, 0.0, 0.0), _IntroFade);

                // Preserve camera alpha so a compositor passthrough underlay remains visible.
                // This texture contains Unity objects, NOT the device passthrough camera feed.
                float sourceAlpha = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).a;
                return half4(finalColor, max(sourceAlpha, _IntroFade));
            }
            ENDHLSL
        }
    }
}
