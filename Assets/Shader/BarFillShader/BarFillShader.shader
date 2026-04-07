Shader "SPPB/BarFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Fill Settings)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.5
        _FillDirection ("Fill Direction (0=Left, 1=Right)", Range(0, 1)) = 0

        [Header(Colors)]
        [HDR] _ColorStart ("Start Color", Color) = (0.2, 0.6, 1.0, 1.0)
        [HDR] _ColorEnd ("End Color", Color) = (0.8, 0.2, 1.0, 1.0)
        _Saturation ("Saturation", Range(1, 2)) = 1.3

        [Header(FBM Cloud Animation)]
        _FBMScale ("FBM Scale", Range(0.5, 10)) = 2
        _FBMRatio ("FBM Ratio (X/Y)", Range(0.1, 10)) = 1
        _FBMSpeed ("FBM Animation Speed", Range(0, 1)) = 0.15
        _FBMColorMix ("FBM Color Mix", Range(0, 1)) = 0.5
        _FBMBrightness ("FBM Brightness Variation", Range(0, 0.5)) = 0.2
        [HDR] _FBMTintColor ("FBM Tint Color", Color) = (1.0, 0.8, 1.2, 1.0)
        _FBMTintStrength ("FBM Tint Strength", Range(0, 1)) = 0.3

        [Header(Blade Glow Effect)]
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _BladeHeight ("Blade Height", Range(0.5, 2.0)) = 1.2
        _BladeWidth ("Blade Width", Range(0.001, 0.1)) = 0.02
        _BladeSoftness ("Blade Softness", Range(0.01, 0.5)) = 0.15
        _GlowPulseSpeed ("Glow Pulse Speed", Range(0, 5)) = 2

        [Header(Edge)]
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.02

        // Unity UI required properties
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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;

            float _FillAmount;
            float _FillDirection;

            float4 _ColorStart;
            float4 _ColorEnd;
            float _Saturation;

            float _FBMSpeed;
            float _FBMScale;
            float _FBMRatio;
            float _FBMColorMix;
            float _FBMBrightness;
            float4 _FBMTintColor;
            float _FBMTintStrength;

            float4 _GlowColor;
            float _GlowIntensity;
            float _BladeHeight;
            float _BladeWidth;
            float _BladeSoftness;
            float _GlowPulseSpeed;

            float _EdgeSoftness;

            // 隨機函數
            inline float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // 噪聲函數
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
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 取得原始圖片的 alpha（用於保持形狀）
                float4 texColor = tex2D(_MainTex, i.uv);
                float originalAlpha = texColor.a;

                // 根據填充方向計算進度
                float progress;
                if (_FillDirection > 0.5)
                {
                    // 從右向左填充
                    progress = 1.0 - i.uv.x;
                }
                else
                {
                    // 從左向右填充（預設）
                    progress = i.uv.x;
                }

                // 填充遮罩（頭尾都帶柔邊）
                float softEdge = _EdgeSoftness;

                // 尾端柔邊：從 _FillAmount 向內漸變
                float tailFade = smoothstep(_FillAmount, _FillAmount - softEdge, progress);

                // 頭端柔邊：從 0 向內漸變
                float headFade = smoothstep(0, softEdge, progress);

                // 組合填充遮罩
                float fillMask = tailFade * headFade;

                // 時間
                float time = _Time.y * _FBMSpeed;

                // 基礎漸層（沿進度方向）
                float gradientT = progress / max(_FillAmount, 0.001);
                gradientT = saturate(gradientT);

                // 大色塊 FBM 效果 - 緩慢飄動
                // 只用一層大範圍的雲，製造大色塊
                // 使用 Ratio 調整 X/Y 比例避免拉伸
                float2 cloudUV = i.uv * _FBMScale * float2(_FBMRatio, 1.0);
                cloudUV += float2(time * 0.2, time * 0.15); // 緩慢斜向飄動
                float cloud = fbm(cloudUV, time * 0.5);

                // 對雲值做平滑處理，讓色塊邊界更柔和
                cloud = smoothstep(0.3, 0.7, cloud);

                // 使用雲來混合顏色（在漸層基礎上增加色彩變化）
                float colorShift = cloud * _FBMColorMix;
                float adjustedGradient = saturate(gradientT + (colorShift - 0.5) * 0.6);

                // 雙色漸層（受雲影響）
                float3 baseColor = lerp(_ColorStart.rgb, _ColorEnd.rgb, adjustedGradient);

                // 增強飽和度
                baseColor = enhanceSaturation(baseColor, _Saturation);

                // 明暗變化（大色塊的明暗起伏）
                float brightness = (cloud - 0.5) * _FBMBrightness * 2.0;
                float3 colMix = baseColor * (1.0 + brightness);

                // 套用 FBM Tint 顏色（根據雲的強度混合 tint color）
                float3 tintedColor = lerp(colMix, colMix * _FBMTintColor.rgb, cloud * _FBMTintStrength);

                // 漸層顏色
                float4 gradientColor = float4(tintedColor, 1.0);

                // 刀鋒發光效果（垂直方向，超出 bar fill 範圍）
                float glowMask = 0;

                if (_FillAmount > 0.01)
                {
                    // 計算尾端 X 位置
                    float tailX;
                    if (_FillDirection > 0.5)
                    {
                        tailX = 1.0 - _FillAmount;
                    }
                    else
                    {
                        tailX = _FillAmount;
                    }

                    // 計算當前點與尾端的水平距離（用於刀鋒寬度）
                    float distToTail = abs(i.uv.x - tailX);

                    // 計算垂直位置（0.5 為中心）
                    // 使用 _BladeHeight 來控制刀鋒延伸的高度（可以超過 1.0 = 50% 高度）
                    // _BladeHeight = 1.0 表示剛好到達邊緣，> 1.0 表示超出
                    float verticalPos = abs(i.uv.y - 0.5) * 2.0;  // 0 在中心，1 在邊緣

                    // 刀鋒的垂直範圍（可以延伸到 bar 外面）
                    // verticalPos / _BladeHeight 將範圍正規化
                    float normalizedVertical = verticalPos / _BladeHeight;

                    // 刀鋒寬度遮罩（水平方向，越靠近尾端越亮）
                    float bladeWidthMask = smoothstep(_BladeWidth, 0, distToTail);

                    // 刀鋒頭尾柔邊（垂直方向上下兩端漸變）
                    // 從中心 (normalizedVertical=0) 到邊緣 (normalizedVertical=1) 漸變
                    float bladeSoft = _BladeSoftness;

                    // 上下兩端的柔邊效果
                    float bladeVerticalFade = smoothstep(1.0, 1.0 - bladeSoft, normalizedVertical);

                    // 中心最亮，向兩端漸暗
                    float verticalGradient = 1.0 - normalizedVertical * 0.3;

                    // 組合刀鋒遮罩
                    glowMask = bladeWidthMask * bladeVerticalFade * verticalGradient;

                    // 中心線更亮的效果
                    float centerLine = smoothstep(0.15, 0, normalizedVertical);
                    glowMask = max(glowMask, centerLine * bladeWidthMask * 0.6);

                    // 發光脈動
                    float glowPulse = 0.8 + 0.2 * sin(_Time.y * _GlowPulseSpeed);
                    glowMask *= glowPulse;
                }

                // 組合最終顏色
                float4 finalColor = float4(0, 0, 0, 0);

                // 填充部分（只在原圖 alpha > 0 的區域顯示）
                if (originalAlpha > 0.01 && fillMask > 0.01)
                {
                    finalColor.rgb = gradientColor.rgb;
                    finalColor.a = fillMask * originalAlpha;
                }

                // 刀鋒發光（可以超出原圖範圍，但仍受原圖 alpha 影響以保持形狀）
                // 刀鋒可以在填充區域外發光
                float glowAmount = glowMask * _GlowIntensity;
                if (glowAmount > 0.01)
                {
                    float3 glowAdditive = _GlowColor.rgb * glowAmount;
                    finalColor.rgb += glowAdditive;
                    // 刀鋒發光的 alpha（受原圖 alpha 限制以保持形狀）
                    float glowAlpha = saturate(glowAmount) * originalAlpha;
                    finalColor.a = max(finalColor.a, glowAlpha);
                }

                // 如果完全透明，直接返回
                if (finalColor.a < 0.01)
                {
                    return float4(0, 0, 0, 0);
                }

                // 應用頂點顏色
                finalColor *= i.color;

                // UI Clipping
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
