Shader "SPPB/RingScore"
{
    Properties
    {
        [Header(Ring Settings)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.5
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.15
        _RingRadius ("Ring Radius", Range(0.1, 0.5)) = 0.4

        [Header(Colors)]
        [HDR] _ColorStart ("Start Color", Color) = (0.2, 0.6, 1.0, 1.0)
        [HDR] _ColorEnd ("End Color", Color) = (0.8, 0.2, 1.0, 1.0)
        _Saturation ("Saturation", Range(1, 2)) = 1.3

        [Header(FBM Animation)]
        _FBMSpeed ("FBM Speed", Range(0, 3)) = 0.5
        _FBMScale ("FBM Scale", Range(1, 10)) = 4
        _FBMIntensity ("FBM Intensity", Range(0, 0.3)) = 0.15

        [Header(Blade Glow Effect)]
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _BladeLength ("Blade Length", Range(0.05, 0.3)) = 0.15
        _BladeWidth ("Blade Width", Range(0.001, 0.02)) = 0.005
        _GlowPulseSpeed ("Glow Pulse Speed", Range(0, 5)) = 2

        [Header(Edge)]
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.05)) = 0.01
        _StartAngle ("Start Angle", Range(-180, 180)) = -90

        // UI Stencil 屬性（用於 UI 遮罩）
        [Header(UI Stencil)]
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
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // UI Stencil 配置
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            // Properties
            float _FillAmount;
            float _RingWidth;
            float _RingRadius;

            float4 _ColorStart;
            float4 _ColorEnd;
            float _Saturation;

            float _FBMSpeed;
            float _FBMScale;
            float _FBMIntensity;

            float4 _GlowColor;
            float _GlowIntensity;
            float _BladeLength;
            float _BladeWidth;
            float _GlowPulseSpeed;

            float _EdgeSoftness;
            float _StartAngle;

            // NoiseFlow 風格的隨機函數
            inline float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // NoiseFlow 風格的噪聲函數
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

            // 2D 旋轉函數
            float2 rotate2D(float2 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            // 2 層 FBM
            float fbm(float2 p, float time)
            {
                float2 rotatedP = rotate2D(p, time * 0.1);
                float n = noise(rotatedP * _FBMScale + time * 0.2);
                n += noise(rotatedP * _FBMScale * 2.0 - time * 0.15) * 0.5;
                return n / 1.5;
            }

            // 增強飽和度
            float3 enhanceSaturation(float3 color, float saturation)
            {
                float grey = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(float3(grey, grey, grey), color, saturation);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 將 UV 轉換到 -0.5 ~ 0.5 範圍
                float2 uv = i.uv - 0.5;

                // 計算極座標
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                // 將角度轉換為 0-1 範圍（從起始角度開始，順時針方向）
                float startRad = _StartAngle * 3.14159265 / 180.0;
                // 順時針：用 startRad - angle 而非 angle - startRad
                float normalizedAngle = startRad - angle;
                if (normalizedAngle < 0) normalizedAngle += 6.28318530;
                float progress = normalizedAngle / 6.28318530;

                // 環形遮罩（硬邊）
                float innerRadius = _RingRadius - _RingWidth * 0.5;
                float outerRadius = _RingRadius + _RingWidth * 0.5;

                float ringMask = step(innerRadius, dist) * step(dist, outerRadius);

                // 進度遮罩（頭尾柔邊向內漸變，不超出邊界）
                float softEdge = _EdgeSoftness;

                // 尾端柔邊：從 _FillAmount 向內漸變（不超出 _FillAmount）
                float tailFade = smoothstep(_FillAmount, _FillAmount - softEdge, progress);

                // 頭端柔邊：從 0 向內漸變
                float headFade;
                if (_FillAmount >= 1.0 - softEdge)
                {
                    // 接近滿圓時，頭尾平滑接合
                    headFade = 1.0;
                }
                else
                {
                    headFade = smoothstep(0, softEdge, progress);
                }

                // 基礎填充遮罩（硬邊界）
                float baseFill = step(progress, _FillAmount);

                // 組合：硬邊界內 + 頭尾柔邊
                float fillMask = baseFill * tailFade * headFade;

                // 時間
                float time = _Time.y * _FBMSpeed;

                // 基礎漸層（沿進度方向）
                float gradientT = progress / max(_FillAmount, 0.001);
                gradientT = saturate(gradientT);

                // 雙色漸層
                float3 baseColor = lerp(_ColorStart.rgb, _ColorEnd.rgb, gradientT);

                // 增強飽和度
                baseColor = enhanceSaturation(baseColor, _Saturation);

                // FBM 動態效果：用噪聲產生明暗變化
                float2 fbmUV = uv * 2.0;
                float n = fbm(fbmUV, time);
                float brightness = (n - 0.5) * _FBMIntensity * 2.0;
                float3 colMix = baseColor * (1.0 + brightness);

                // 漸層顏色
                float4 gradientColor = float4(colMix, 1.0);

                // 刀鋒發光效果
                float glowMask = 0;

                if (_FillAmount > 0.01)
                {
                    // 計算尾端的角度位置
                    float tailAngleRad = startRad - _FillAmount * 6.28318530;

                    // 計算尾端中心點位置
                    float2 tailCenter = float2(cos(tailAngleRad), sin(tailAngleRad)) * _RingRadius;

                    // 計算刀鋒方向（從圓心向外的徑向方向）
                    float2 bladeDir = normalize(tailCenter);

                    // 計算當前點相對於尾端中心的位置
                    float2 toPoint = uv - tailCenter;

                    // 投影到刀鋒方向（沿徑向的距離）
                    float alongBlade = dot(toPoint, bladeDir);

                    // 垂直於刀鋒的距離（切線方向）
                    float perpBlade = abs(dot(toPoint, float2(-bladeDir.y, bladeDir.x)));

                    // 刀鋒長度範圍（從環內延伸到環外）
                    float halfLength = _BladeLength * 0.5;
                    float bladeAlongMask = smoothstep(halfLength, halfLength * 0.3, abs(alongBlade));

                    // 刀鋒寬度（中心最亮，兩側衰減）
                    float bladePerpMask = smoothstep(_BladeWidth, 0, perpBlade);

                    // 組合刀鋒遮罩
                    glowMask = bladeAlongMask * bladePerpMask;

                    // 中心更亮的效果
                    float centerBoost = smoothstep(_BladeWidth * 2.0, 0, perpBlade);
                    glowMask = max(glowMask, centerBoost * bladeAlongMask * 0.5);

                    // 發光脈動
                    float glowPulse = 0.8 + 0.2 * sin(_Time.y * _GlowPulseSpeed);
                    glowMask *= glowPulse;
                }

                // 組合最終顏色
                float4 finalColor = float4(0, 0, 0, 0);

                // 填充部分
                float combinedMask = ringMask * fillMask;
                finalColor.rgb = gradientColor.rgb;
                finalColor.a = combinedMask;

                // 刀鋒發光（Additive 混合，讓 HDR 顏色更明顯）
                float glowAmount = glowMask * _GlowIntensity;
                // 發光顏色直接加上去（不受 alpha 影響）
                float3 glowAdditive = _GlowColor.rgb * glowAmount;
                finalColor.rgb += glowAdditive;
                // 發光區域也需要 alpha
                finalColor.a = max(finalColor.a, saturate(glowAmount));

                // 應用頂點顏色
                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
