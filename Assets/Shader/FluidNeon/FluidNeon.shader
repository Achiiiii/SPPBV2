Shader "UI/FluidNeon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Tint", Color) = (1,1,1,1)

        [Header(Fluid Neon Settings)]
        _NeonIntensity ("Neon Intensity", Range(0, 5)) = 1.0
        _NeonColor ("Neon Color Tint", Color) = (0.5, 0.8, 1.0, 1)

        [Header(Fluid Animation)]
        _FluidScale ("Fluid Scale", Range(1, 10)) = 4.0
        _Speed ("Flow Speed", Range(0, 5)) = 1.0
        _Iterations ("Detail Iterations", Range(4, 8)) = 5  // Mobile: 降低預設值提升效能

        [Header(Color Settings)]
        _Saturation ("Saturation", Range(0.5, 3)) = 1.5
        _GlowColor ("Glow Color", Color) = (0.1, 0.2, 0.8, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.3

        [Header(Pulse Animation)]
        _PulseEnabled ("Enable Pulse", Float) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
        }

        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color, _NeonColor, _GlowColor;
            float _NeonIntensity;
            float _FluidScale, _Speed;
            float _Iterations;
            float _Saturation, _GlowIntensity;
            float _PulseEnabled, _PulseSpeed, _PulseIntensity;
            float4 _ClipRect;

            // Fast approximation of tanh (from GLSL reference)
            float4 fastTanh(float4 x) {
                float4 x2 = x * x;
                return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
            }

            v2f vert(appdata_full v) {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 spriteCol = tex2D(_MainTex, i.uv) * i.color;
                float spriteAlpha = spriteCol.a;

                // 如果在 sprite 外部，返回透明
                // 使用 alpha=0 代替 discard，避免破壞 mobile GPU 的 early-Z 優化
                if (spriteAlpha < 0.01) {
                    return fixed4(0, 0, 0, 0);
                }

                // 脈衝效果
                float pulse = 1.0;
                if (_PulseEnabled > 0.5) {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed * 3.14159) * _PulseIntensity;
                }

                // ========== 流體霓虹計算 (參考 GLSL) ==========

                // Centered, ratio corrected coordinates
                float2 p = i.uv * 2.0 - 1.0;

                // Z depth calculation
                float zBase = _FluidScale - _FluidScale * abs(0.7 - dot(p, p));
                float z = zBase;

                // Fluid coordinates
                float2 f = p * z;

                // Accumulator
                float4 O = float4(0.0, 0.0, 0.0, 0.0);

                float time = _Time.y * _Speed;

                // Loop iterations (based on reference GLSL)
                int iterations = (int)_Iterations;
                for(int j = 0; j < iterations; j++) {
                    float iterY = float(j) + 1.0;

                    // Set color waves and line brightness
                    float2 sinF = sin(f) + 1.0;

                    // Enhanced neon colors
                    float4 colorWave = float4(sinF.x, sinF.y, sinF.x, sinF.y) * abs(f.x - f.y) * 1.5;

                    O += colorWave;

                    // Add fluid waves
                    f += cos(f.yx * iterY + float2(iterY, iterY) + time) / iterY + 0.7;
                }

                // Tonemap, fade edges and color gradient
                float4 expTerm = exp(z - 4.0 - p.y * float4(-1.0, 1.0, 2.0, 0.0));
                float4 neonRaw = 7.0 * expTerm / max(O, 0.01);
                float4 neonCol = fastTanh(neonRaw);

                // Increase saturation for more vibrant neon effect
                float3 lum = float3(0.299, 0.587, 0.114);
                float luminance = dot(neonCol.rgb, lum);
                float3 saturated = lerp(float3(luminance, luminance, luminance), neonCol.rgb, _Saturation);

                // Add subtle glow effect
                saturated += _GlowColor.rgb * _GlowIntensity;

                // Apply neon color tint
                saturated *= _NeonColor.rgb;

                // ========== 疊加模式 (Additive) ==========
                // 原始圖片顏色 + 流體霓虹效果
                float3 neonEffect = saturated * _NeonIntensity * pulse;

                fixed4 finalCol;
                finalCol.rgb = spriteCol.rgb + neonEffect;
                finalCol.a = spriteAlpha;

                finalCol.a *= i.color.a;
                finalCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return finalCol;
            }
            ENDCG
        }
    }
}
