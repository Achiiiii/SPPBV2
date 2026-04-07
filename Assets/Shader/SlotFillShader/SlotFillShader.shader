Shader "SPPB/SlotFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Fill Settings)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 1

        [Header(FBM Cloud Animation)]
        _FBMSpeed ("FBM Animation Speed", Range(0, 1)) = 0.15
        _FBMScale ("FBM Scale", Range(0.5, 10)) = 2
        _FBMRatio ("FBM Ratio (X/Y)", Range(0.1, 10)) = 1
        _FBMBrightness ("FBM Brightness Variation", Range(0, 0.5)) = 0.2
        [HDR] _FBMTintColor ("FBM Tint Color", Color) = (1.0, 0.8, 1.2, 1.0)
        _FBMTintStrength ("FBM Tint Strength", Range(0, 1)) = 0.3

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
            float _FBMSpeed;
            float _FBMScale;
            float _FBMRatio;
            float _FBMBrightness;
            float4 _FBMTintColor;
            float _FBMTintStrength;
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

            // FBM 噪聲
            float fbm(float2 p, float time)
            {
                float2 rotatedP = rotate2D(p, time * 0.1);
                float n = noise(rotatedP * _FBMScale + time * 0.2);
                n += noise(rotatedP * _FBMScale * 2.0 - time * 0.15) * 0.5;
                return n / 1.5;
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
                // 取得原始圖片顏色
                float4 texColor = tex2D(_MainTex, i.uv);

                // 從下往上填滿：uv.y = 0 是底部，uv.y = 1 是頂部
                // 當 _FillAmount = 0 時，完全不顯示
                // 當 _FillAmount = 1 時，完全顯示
                float fillY = _FillAmount;

                // 計算填充遮罩（從底部往上）
                // i.uv.y < fillY 的部分顯示
                float fillMask = smoothstep(fillY, fillY - _EdgeSoftness, i.uv.y);

                // 時間
                float time = _Time.y * _FBMSpeed;

                // 大色塊 FBM 效果 - 緩慢飄動
                // 只用一層大範圍的雲，製造大色塊
                // 使用 Ratio 調整 X/Y 比例避免拉伸
                float2 cloudUV = i.uv * _FBMScale * float2(_FBMRatio, 1.0);
                cloudUV += float2(time * 0.2, time * 0.15); // 緩慢斜向飄動
                float cloud = fbm(cloudUV, time * 0.5);

                // 對雲值做平滑處理，讓色塊邊界更柔和
                cloud = smoothstep(0.3, 0.7, cloud);

                // 明暗變化（大色塊的明暗起伏）
                float brightness = (cloud - 0.5) * _FBMBrightness * 2.0;

                // 組合最終顏色
                float4 finalColor = texColor;
                finalColor.rgb *= (1.0 + brightness);

                // 套用 FBM Tint 顏色（根據雲的強度混合 tint color）
                finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * _FBMTintColor.rgb, cloud * _FBMTintStrength);
                finalColor.a *= fillMask;

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
