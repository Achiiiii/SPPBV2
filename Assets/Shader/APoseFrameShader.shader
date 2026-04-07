Shader "SPPB/APoseFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Edge Mask)]
        _EdgeMask ("Edge Mask (R=edge intensity)", 2D) = "white" {}

        [Header(Fill Settings)]
        _FillTex ("Fill Texture", 2D) = "black" {}
        _FillColor ("Fill Color", Color) = (0.0, 0.3, 0.6, 0.5)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.3
        _FillPulseSpeed ("Fill Pulse Speed", Range(0.1, 3.0)) = 1.0
        _FillPulseMin ("Fill Pulse Min", Range(0, 1)) = 0.2
        _FillPulseMax ("Fill Pulse Max", Range(0, 1)) = 0.5

        [Header(Glow Settings)]
        _GlowColor1 ("Glow Color 1 (Head)", Color) = (0.2, 0.8, 1.0, 1.0)
        _GlowColor2 ("Glow Color 2 (Tail)", Color) = (0.0, 0.4, 1.0, 1.0)
        _GlowWidth ("Glow Width", Range(0.5, 5.0)) = 2.0
        _GlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 2.5
        _GlowBloom ("Glow Bloom", Range(0, 5.0)) = 0.5
        _GlowCoreIntensity ("Glow Core Intensity", Range(0, 3.0)) = 1.0

        [Header(Base Edge)]
        _BaseEdgeColor ("Base Edge Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BaseEdgeAlpha ("Base Edge Alpha", Range(0, 1)) = 1.0

        [Header(Animation)]
        _Speed ("Move Speed", Range(0.1, 3.0)) = 0.8
        _TrailLength ("Trail Length", Range(0.05, 0.5)) = 0.2

        [Header(Base)]
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.5

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

            sampler2D _EdgeMask;

            sampler2D _FillTex;
            float4 _FillColor;
            float _FillAlpha;
            float _FillPulseSpeed;
            float _FillPulseMin;
            float _FillPulseMax;

            float4 _GlowColor1;
            float4 _GlowColor2;
            float _GlowWidth;
            float _GlowIntensity;
            float _GlowBloom;
            float _GlowCoreIntensity;
            float4 _BaseEdgeColor;
            float _BaseEdgeAlpha;
            float _Speed;
            float _TrailLength;
            float _BaseAlpha;

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
                // 取得原始圖片
                float4 texColor = tex2D(_MainTex, i.uv);

                // 取得邊緣遮罩（只用 R 通道）
                float4 edgeMaskTex = tex2D(_EdgeMask, i.uv);
                float edgeIntensity = edgeMaskTex.r;

                // 取得填充圖片
                float4 fillTexColor = tex2D(_FillTex, i.uv);
                float fillMask = fillTexColor.a; // 用 alpha 作為填充區域遮罩

                // 如果不是邊緣、不是填充區域、且原圖透明，返回透明
                // 使用 alpha=0 代替 discard，避免破壞 mobile GPU 的 early-Z 優化
                if (texColor.a < 0.01 && edgeIntensity < 0.01 && fillMask < 0.01)
                {
                    return float4(0, 0, 0, 0);
                }

                // 用角度計算位置（從中心點算，順時針繞一圈 0-1）
                float2 center = float2(0.5, 0.5);
                float2 dir = i.uv - center;
                // atan2 返回 -PI 到 PI，轉換成 0-1
                // 從上方開始，順時針
                float angle = atan2(dir.x, dir.y); // 注意：x,y 順序讓 0 度在上方
                float edgePosition = (angle + 3.14159265) / (2.0 * 3.14159265); // 轉成 0-1

                // 計算發光位置（隨時間移動）
                float time = _Time.y * _Speed;
                float glowCenter = frac(time); // 0-1 循環

                // 計算與發光中心的距離（考慮循環）
                float dist1 = abs(edgePosition - glowCenter);
                float dist2 = abs(edgePosition - glowCenter + 1.0);
                float dist3 = abs(edgePosition - glowCenter - 1.0);
                float distToGlow = min(min(dist1, dist2), dist3);

                // 發光強度（在 trail 範圍內漸變）
                float trailMask = 1.0 - saturate(distToGlow / _TrailLength);
                trailMask = pow(trailMask, 1.2); // 讓邊緣更柔和

                // 邊緣中心漸淡效果（中心亮，兩側暗）
                // edgeIntensity 越接近 1 表示越靠近邊緣中心線
                float centerFalloff = pow(edgeIntensity, 0.5); // 讓中心更亮，邊緣更快衰減

                // 組合發光（邊緣強度 * 拖尾遮罩 * 中心漸淡）
                float finalGlow = centerFalloff * trailMask * _GlowWidth;
                finalGlow = saturate(finalGlow);

                // 雙色漸層（根據拖尾位置混合，頭部用 Color1，尾部用 Color2）
                float4 glowColor = lerp(_GlowColor2, _GlowColor1, trailMask);

                // 基礎顏色（原始圖片）
                float4 baseColor = texColor;
                baseColor.a *= _BaseAlpha;

                // 填充顏色（帶呼吸動畫）
                // 使用 sin 函數產生 0-1 之間的脈動值
                float fillPulse = (sin(_Time.y * _FillPulseSpeed * 3.14159) + 1.0) * 0.5; // 0-1 範圍
                float animatedFillAlpha = lerp(_FillPulseMin, _FillPulseMax, fillPulse);

                float4 fillColor = _FillColor;
                fillColor.a = fillMask * animatedFillAlpha;

                // 邊緣基底顏色（沒有 glow 的地方顯示指定顏色）
                float4 edgeBaseColor = _BaseEdgeColor;
                edgeBaseColor.a = edgeIntensity * _BaseEdgeAlpha;

                // 最終顏色 - 從填充開始
                float4 finalColor = baseColor;

                // 先混入填充顏色（作為底層）
                finalColor.rgb = lerp(finalColor.rgb, fillColor.rgb, fillColor.a);
                finalColor.a = max(finalColor.a, fillColor.a);

                // 再混入邊緣基底色（覆蓋在填充上，只在沒有 glow 的地方顯示）
                float noGlowMask = 1.0 - finalGlow; // glow 越強，邊緣基底色越弱
                float edgeBlend = edgeIntensity * _BaseEdgeAlpha * noGlowMask;
                finalColor.rgb = lerp(finalColor.rgb, edgeBaseColor.rgb, saturate(edgeBlend));
                finalColor.a = max(finalColor.a, edgeBaseColor.a * noGlowMask);

                // 再疊加發光效果（glow 會覆蓋邊緣基底色）
                float glowAmount = finalGlow * _GlowIntensity;
                finalColor.rgb = lerp(finalColor.rgb, glowColor.rgb * _GlowIntensity, finalGlow);

                // 添加 bloom 效果（讓發光更亮，超過 1.0）
                finalColor.rgb += glowColor.rgb * finalGlow * _GlowBloom;

                // 添加 core 效果（glow 中心更亮，接近白色）
                float coreMask = pow(trailMask, 2.0) * edgeIntensity; // 中心最亮
                float3 coreColor = lerp(glowColor.rgb, float3(1, 1, 1), 0.5); // 混入白色
                finalColor.rgb += coreColor * coreMask * _GlowCoreIntensity;

                finalColor.a = max(finalColor.a, finalGlow * glowColor.a);

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
