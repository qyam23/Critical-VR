Shader "CriticalVR/PassthroughTrip"
{
    Properties
    {
        [HideInInspector] _MainTex("MainTex", 2D) = "white" {}
        [HideInInspector] _PassthroughMode("Passthrough Mode", Float) = 0
        [HideInInspector] _DesktopStrength("Desktop Strength", Float) = 1
        [HideInInspector] _DesktopDepth("Desktop Distortion and Pattern", Float) = 1
        [HideInInspector] _CameraFeedReady("Camera Feed Ready", Float) = 0
        [HideInInspector] _CameraLeft("Left Camera", 2D) = "black" {}
        [HideInInspector] _CameraRight("Right Camera", 2D) = "black" {}
        [HideInInspector] _CameraMaximumOpacity("Camera Maximum Opacity", Float) = 0.45
        [HideInInspector] _CameraReferenceDistance("Camera Reference Distance", Float) = 2
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
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
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
            #pragma target 3.5
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CameraLeft);
            TEXTURE2D(_CameraRight);
            float4x4 _CameraWorldToLocalLeft;
            float4x4 _CameraWorldToLocalRight;
            float4 _CameraCalibrationLeft;
            float4 _CameraCalibrationRight;
            CBUFFER_START(UnityPerMaterial)
                float _PassthroughMode;
                float _DesktopStrength;
                float _DesktopDepth;
                float _CameraFeedReady;
                float _CameraReferenceDistance;
                float _CameraMaximumOpacity;
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

            // Each eye uses its corresponding sensor, capture pose, and SDK crop calibration.
            // A reference-distance reprojection is an approximation, not depth reconstruction.
            float2 CameraUV(float2 uv, out float coverage)
            {
                uint eye = 0;
                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    eye = unity_StereoEyeIndex;
                #endif
                float3 farWorld = ComputeWorldSpacePosition(uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 ray = normalize(farWorld - _WorldSpaceCameraPos);
                float3 worldPoint = _WorldSpaceCameraPos + ray * _CameraReferenceDistance;
                float3 cameraPoint = float3(0, 0, 1);
                float4 calibration = float4(1, 1, 0, 0);
                if (eye == 0)
                {
                    cameraPoint = mul(_CameraWorldToLocalLeft, float4(worldPoint, 1)).xyz;
                    calibration = _CameraCalibrationLeft;
                }
                else
                {
                    cameraPoint = mul(_CameraWorldToLocalRight, float4(worldPoint, 1)).xyz;
                    calibration = _CameraCalibrationRight;
                }
                float2 cameraUV = cameraPoint.xy / max(cameraPoint.z, 0.0001) * calibration.xy + calibration.zw;
                float border = min(min(cameraUV.x, cameraUV.y), min(1-cameraUV.x, 1-cameraUV.y));
                coverage = smoothstep(0, 0.08, border) * step(0.01, cameraPoint.z);
                return cameraUV;
            }

            half4 ReadSource(float2 uv)
            {
                half4 color = half4(0, 0, 0, 0);
                if (_PassthroughMode < 0.5)
                    color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                else
                {
                    float coverage = 0;
                    float2 cameraUV = CameraUV(uv, coverage);
                    color = SAMPLE_TEXTURE2D(_CameraLeft, sampler_LinearClamp, cameraUV);
                    #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                        if (unity_StereoEyeIndex == 1)
                            color = SAMPLE_TEXTURE2D(_CameraRight, sampler_LinearClamp, cameraUV);
                    #endif
                }
                return color;
            }

            float CriticalVRLuminance(float3 color)
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
                float lum00 = CriticalVRLuminance(ReadSource(uv + texel * float2(-1, -1)).rgb);
                float lum10 = CriticalVRLuminance(ReadSource(uv + texel * float2( 0, -1)).rgb);
                float lum20 = CriticalVRLuminance(ReadSource(uv + texel * float2( 1, -1)).rgb);
                float lum01 = CriticalVRLuminance(ReadSource(uv + texel * float2(-1,  0)).rgb);
                float lum21 = CriticalVRLuminance(ReadSource(uv + texel * float2( 1,  0)).rgb);
                float lum02 = CriticalVRLuminance(ReadSource(uv + texel * float2(-1,  1)).rgb);
                float lum12 = CriticalVRLuminance(ReadSource(uv + texel * float2( 0,  1)).rgb);
                float lum22 = CriticalVRLuminance(ReadSource(uv + texel * float2( 1,  1)).rgb);

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
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_PassthroughMode > 0.5 && _IntroFade > 0.0)
                {
                    // Opaque black at launch, then reveal the compositor underlay through alpha.
                    half fade = saturate(_IntroFade);
                    return sceneColor * (1.0h - fade) + half4(0, 0, 0, fade);
                }
                if (_PassthroughMode > 0.5 && (_CameraFeedReady < 0.5 || _IntensityScore < 0.001 || _DesktopStrength <= 0.0))
                    return sceneColor;
                float depth = _PassthroughMode > 0.5 ? clamp(_DesktopDepth, 0.0, 1.3) : 1.0;
                float peripheralMask = PeripheralMask(uv);
                float breathingWave = sin(_BreathingPhase + peripheralMask * 2.6) * 0.5 + 0.5;

                float2 noiseUv = uv * lerp(7.0, 18.0, _ModeIndex) + float2(_BreathingPhase * 0.04, _BreathingPhase * 0.025);
                float noiseA = Noise(noiseUv);
                float noiseB = Noise(noiseUv * 1.7 + 19.37);
                float wobble = ((noiseA - 0.5) + (noiseB - 0.5)) * peripheralMask * min(_DistortionAmount * depth, 0.05);

                float2 radialDir = normalize(((uv * 2.0) - 1.0) + 1e-5);
                float2 distortionOffset = radialDir * wobble * float2(1.0, 0.82);

                float edge = EdgeStrength(uv + distortionOffset);
                float3 baseColor = ReadSource(uv + distortionOffset).rgb;
                float luminance = CriticalVRLuminance(baseColor);

                float highlightMask = saturate((luminance - 0.55) * 2.4) * peripheralMask;
                float2 chromaOffset = radialDir * (_ChromaticAmount * (0.35 + highlightMask));
                float red = ReadSource(uv + distortionOffset + chromaOffset).r;
                float green = ReadSource(uv + distortionOffset).g;
                float blue = ReadSource(uv + distortionOffset - chromaOffset).b;
                float3 chromaColor = float3(red, green, blue);

                float pattern = Noise(noiseUv * 2.4 + edge * 5.0 + luminance * 3.0);
                float patternMask = saturate(edge * 0.65 + highlightMask * 0.55 + peripheralMask * 0.35);
                float patternLayer = saturate((pattern - 0.35) * 1.6) * saturate(_PatternAmount * depth) * patternMask;

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

                if (_PassthroughMode > 0.5)
                {
                    float coverage;
                    CameraUV(uv + distortionOffset, coverage);
                    float opacity = min(_CameraMaximumOpacity * clamp(_DesktopStrength, 0.0, 1.3), 0.6) * saturate(_IntensityScore) * coverage;
                    // Premultiplied overlay above native underlay, below virtual opaque content.
                    return half4(sceneColor.rgb + graded * opacity * (1-sceneColor.a),
                        sceneColor.a + opacity * (1-sceneColor.a));
                }

                float introAlpha = saturate(1.0 - _IntroFade);
                float finalAlpha = saturate(introAlpha * max(_IntensityScore, _ColorAtmosphere * 0.65 + _PatternAmount * 0.25 + _OverdriveAmount * 0.4));
                float3 finalColor = lerp(baseColor, graded, saturate(0.25 + finalAlpha * 0.9));
                finalColor = lerp(finalColor, float3(0.0, 0.0, 0.0), _IntroFade);

                // Preserve camera alpha so a compositor passthrough underlay remains visible.
                // This texture contains Unity objects, NOT the device passthrough camera feed.
                float sourceAlpha = ReadSource(uv).a;
                return half4(finalColor, max(sourceAlpha, _IntroFade));
            }
            ENDHLSL
        }
    }
}
