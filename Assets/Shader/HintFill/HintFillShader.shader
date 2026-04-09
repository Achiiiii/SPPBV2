Shader "SPPB/HintFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Tint", Color) = (1,1,1,1)

        [Header(Fill Settings)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _FillOpacity ("Fill Opacity", Range(0, 1)) = 0.85
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.015

        [Header(Gradient Colors)]
        [HDR] _ColorStart ("Start Color (Bottom)", Color) = (0.2, 0.6, 1.0, 1.0)
        [HDR] _ColorEnd ("End Color (Top)", Color) = (0.3, 1.0, 0.5, 1.0)

        [Header(Fluid Neon Animation)]
        _FluidSpeed ("Fluid Speed", Range(0, 5)) = 1.5
        _FluidScale ("Fluid Scale", Range(1, 10)) = 4.0
        _FluidIntensity ("Fluid Intensity", Range(0, 2)) = 0.4
        _Iterations ("Detail Iterations", Range(4, 8)) = 5
        _Saturation ("Saturation", Range(0.5, 3)) = 1.5
        _NeonColor ("Neon Color Tint", Color) = (0.5, 0.8, 1.0, 1)
        [HDR] _GlowColor ("Glow Color Additive", Color) = (0.1, 0.2, 0.8, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.3

        [Header(Pulse Animation)]
        _PulseEnabled ("Enable Pulse", Float) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3

        [Header(FBM Cloud Animation)]
        _FBMScale ("FBM Scale", Range(0.5, 10)) = 2
        _FBMRatio ("FBM Ratio XY", Range(0.1, 10)) = 1
        _FBMSpeed ("FBM Animation Speed", Range(0, 1)) = 0.15
        _FBMColorMix ("FBM Color Mix", Range(0, 1)) = 0.5
        _FBMBrightness ("FBM Brightness Variation", Range(0, 0.5)) = 0.2
        [HDR] _FBMTintColor ("FBM Tint Color", Color) = (1.0, 0.8, 1.2, 1.0)
        _FBMTintStrength ("FBM Tint Strength", Range(0, 1)) = 0.3

        [Header(Blade Glow at Fill Edge)]
        [HDR] _BladeGlowColor ("Blade Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BladeGlowIntensity ("Blade Glow Intensity", Range(0, 5)) = 2.0
        _BladeWidth ("Blade Width (Horizontal extent)", Range(0.5, 2.0)) = 1.2
        _BladeHeight ("Blade Height (Vertical thickness)", Range(0.001, 0.1)) = 0.02
        _BladeSoftness ("Blade Softness", Range(0.01, 0.5)) = 0.15
        _BladeGlowPulseSpeed ("Blade Pulse Speed", Range(0, 5)) = 2

        // Unity UI
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            // Fill
            float  _FillAmount;
            float  _FillOpacity;
            float  _EdgeSoftness;

            // Gradient
            float4 _ColorStart;
            float4 _ColorEnd;

            // Fluid Neon
            float  _FluidSpeed;
            float  _FluidScale;
            float  _FluidIntensity;
            float  _Iterations;
            float  _Saturation;
            fixed4 _NeonColor;
            float4 _GlowColor;
            float  _GlowIntensity;

            // Pulse
            float  _PulseEnabled;
            float  _PulseSpeed;
            float  _PulseIntensity;

            // FBM
            float  _FBMScale;
            float  _FBMRatio;
            float  _FBMSpeed;
            float  _FBMColorMix;
            float  _FBMBrightness;
            float4 _FBMTintColor;
            float  _FBMTintStrength;

            // Blade Glow
            float4 _BladeGlowColor;
            float  _BladeGlowIntensity;
            float  _BladeWidth;
            float  _BladeHeight;
            float  _BladeSoftness;
            float  _BladeGlowPulseSpeed;

            // ========== Helper Functions ==========

            // Fast tanh approximation (from FluidNeon)
            float4 fastTanh(float4 x)
            {
                float4 x2 = x * x;
                return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
            }

            // Random hash (from BarFill)
            inline float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // 2D value noise (from BarFill)
            float noise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            // 2D rotation (from BarFill)
            float2 rotate2D(float2 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            // 2-layer FBM (from BarFill)
            float fbm(float2 p, float time)
            {
                float2 rotatedP = rotate2D(p, time * 0.1);
                float n = noise(rotatedP * _FBMScale + time * 0.2);
                n += noise(rotatedP * _FBMScale * 2.0 - time * 0.15) * 0.5;
                return n / 1.5;
            }

            // Saturation enhancement (from BarFill)
            float3 enhanceSaturation(float3 color, float sat)
            {
                float grey = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(float3(grey, grey, grey), color, sat);
            }

            // ========== Vertex ==========

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // ========== Fragment ==========

            fixed4 frag(v2f i) : SV_Target
            {
                // Step 1: Sprite sampling
                fixed4 spriteCol = tex2D(_MainTex, i.uv) * i.color;
                float spriteAlpha = spriteCol.a;

                if (spriteAlpha < 0.01)
                    return fixed4(0, 0, 0, 0);

                float2 uv = i.uv;

                // Step 2: Fill mask (bottom-to-top)
                float fillEdgeY = 1.0 - _FillAmount;
                float fillMask = smoothstep(fillEdgeY + _EdgeSoftness, fillEdgeY - _EdgeSoftness, uv.y);

                // Step 3: Gradient base color
                float gradientT = saturate((1.0 - uv.y) / max(_FillAmount, 0.001));
                gradientT = 1.0 - gradientT;

                // Step 4: FBM cloud animation
                float fbmTime = _Time.y * _FBMSpeed;
                float2 cloudUV = uv * _FBMScale * float2(_FBMRatio, 1.0);
                cloudUV += float2(fbmTime * 0.2, fbmTime * 0.15);
                float cloud = fbm(cloudUV, fbmTime * 0.5);
                cloud = smoothstep(0.3, 0.7, cloud);

                float colorShift = cloud * _FBMColorMix;
                float adjustedGradient = saturate(gradientT + (colorShift - 0.5) * 0.6);
                float3 baseColor = lerp(_ColorStart.rgb, _ColorEnd.rgb, adjustedGradient);

                // Brightness variation
                float brightness = (cloud - 0.5) * _FBMBrightness * 2.0;
                baseColor *= (1.0 + brightness);

                // FBM tint
                baseColor = lerp(baseColor, baseColor * _FBMTintColor.rgb, cloud * _FBMTintStrength);

                // Enhance saturation on base color
                baseColor = enhanceSaturation(baseColor, _Saturation);

                // Step 5: Fluid neon calculation (from FluidNeon, full version)
                float2 p = uv * 2.0 - 1.0;
                float zBase = _FluidScale - _FluidScale * abs(0.7 - dot(p, p));
                float z = zBase;
                float2 f = p * z;
                float4 O = float4(0, 0, 0, 0);
                float neonTime = _Time.y * _FluidSpeed;
                int iterations = (int)_Iterations;

                for (int j = 0; j < iterations; j++)
                {
                    float iterY = float(j) + 1.0;
                    float2 sinF = sin(f) + 1.0;
                    float4 colorWave = float4(sinF.x, sinF.y, sinF.x, sinF.y) * abs(f.x - f.y) * 1.5;
                    O += colorWave;
                    f += cos(f.yx * iterY + float2(iterY, iterY) + neonTime) / iterY + 0.7;
                }

                float4 expTerm = exp(z - 4.0 - p.y * float4(-1, 1, 2, 0));
                float4 neonRaw = 7.0 * expTerm / max(O, 0.01);
                float4 neonCol = fastTanh(neonRaw);

                // Step 6: Neon post-processing
                float luminance = dot(neonCol.rgb, float3(0.299, 0.587, 0.114));
                float3 saturatedNeon = lerp(float3(luminance, luminance, luminance), neonCol.rgb, _Saturation);
                saturatedNeon += _GlowColor.rgb * _GlowIntensity;
                saturatedNeon *= _NeonColor.rgb;

                // Step 7: Pulse animation
                float pulse = 1.0;
                if (_PulseEnabled > 0.5)
                {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed * 3.14159) * _PulseIntensity;
                }

                // Step 8: Blade glow at fill frontier (adapted from BarFill, horizontal)
                float bladeGlowMask = 0;
                if (_FillAmount > 0.01)
                {
                    // Distance from the fill frontier
                    float distToEdge = abs(uv.y - fillEdgeY);

                    // Horizontal position: 0 at center, 1 at edges
                    float horizontalPos = abs(uv.x - 0.5) * 2.0;
                    float normalizedHorizontal = horizontalPos / _BladeWidth;

                    // Blade thickness mask (vertical proximity to fill edge)
                    float bladeThicknessMask = smoothstep(_BladeHeight, 0, distToEdge);

                    // Horizontal soft falloff at left/right ends
                    float bladeHorizontalFade = smoothstep(1.0, 1.0 - _BladeSoftness, normalizedHorizontal);

                    // Center brighter
                    float horizontalGradient = 1.0 - normalizedHorizontal * 0.3;

                    // Combine
                    bladeGlowMask = bladeThicknessMask * bladeHorizontalFade * horizontalGradient;

                    // Brighter center line
                    float centerLine = smoothstep(0.15, 0, normalizedHorizontal);
                    bladeGlowMask = max(bladeGlowMask, centerLine * bladeThicknessMask * 0.6);

                    // Blade pulse
                    float bladePulse = 0.8 + 0.2 * sin(_Time.y * _BladeGlowPulseSpeed);
                    bladeGlowMask *= bladePulse;
                }

                // Step 9: Final composition
                float3 neonEffect = saturatedNeon * _FluidIntensity * pulse;
                float3 fillColor = baseColor + neonEffect;

                // 使用 lerp 混合：填充區域用 fillColor 取代原色，而非加法疊加
                float blendFactor = fillMask * _FillOpacity;
                float3 blended = lerp(spriteCol.rgb, fillColor, blendFactor);

                // Blade glow (additive, straddles the fill edge)
                float bladeGlowAmount = bladeGlowMask * _BladeGlowIntensity;
                float3 bladeLayer = _BladeGlowColor.rgb * bladeGlowAmount;

                // Composite
                float3 finalRGB = blended + bladeLayer;
                float  finalA = spriteAlpha * i.color.a;
                finalA = max(finalA, saturate(bladeGlowAmount) * spriteAlpha);

                // UI clipping
                #ifdef UNITY_UI_CLIP_RECT
                finalA *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return fixed4(finalRGB, finalA);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
