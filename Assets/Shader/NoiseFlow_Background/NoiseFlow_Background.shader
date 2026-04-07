Shader "UI/NoiseFlow_Android_BG"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Tint", Color) = (1,1,1,1)

        [Header(Base Colors)]
        _Color1 ("Color 1", Color) = (0.2, 0.5, 0.9, 1)
        _Color2 ("Color 2", Color) = (0.9, 0.2, 0.5, 1)
        _Color3 ("Color 3", Color) = (0.3, 0.8, 0.4, 1)

        [Header(Dynamics)]
        _Speed ("Flow Speed", Range(0, 5)) = 0.5
        _RotationSpeed ("Rotation Speed", Range(-5, 5)) = 0.2
        _NoiseScale ("Noise Density", Range(1, 20)) = 4.0

        [Header(Grain Effect)]
        _GrainIntensity ("Grain Intensity", Range(0, 1)) = 0.15
        _GrainSize ("Grain Size", Range(100, 1000)) = 400
        _GrainSpeed ("Grain Animation Speed", Range(0, 10)) = 5.0

        // UI 遮罩與裁切屬性
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color, _Color1, _Color2, _Color3;
            float _Speed, _RotationSpeed, _NoiseScale;
            float _GrainIntensity, _GrainSize, _GrainSpeed;
            float4 _ClipRect;

            inline float random(float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // 噪點插值優化
            float noise(float2 st) {
                float2 i = floor(st);
                float2 f = frac(st);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // 座標旋轉
            float2 rotate2D(float2 p, float angle) {
                float s, c;
                sincos(angle, s, c); // 使用 sincos 提高效能
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            // Grain 顆粒效果
            float grain(float2 uv, float time) {
                float2 grainUV = uv * _GrainSize;
                // 使用時間讓顆粒動態變化
                float t = floor(time * _GrainSpeed);
                return random(grainUV + t);
            }

            v2f vert(appdata_full v) {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float2 uv = i.uv;
                float2 p = uv - 0.5;

                // 動態演算法
                float time = _Time.y * _Speed;
                float2 rotatedUV = rotate2D(p, _Time.y * _RotationSpeed);

                float n = noise(rotatedUV * _NoiseScale + time * 0.2);
                n += noise(rotatedUV * _NoiseScale * 2.1 - time * 0.3) * 0.5;

                // 顏色計算
                float3 colMix = lerp(_Color1.rgb, _Color2.rgb, n);
                colMix = lerp(colMix, _Color3.rgb, sin(n * 3.14159 + time) * 0.5 + 0.5);

                // Grain 顆粒效果疊加
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float grainValue = grain(screenUV, _Time.y);
                // 將 grain 轉換成 -1 到 1 的範圍，然後乘以強度
                float grainEffect = (grainValue - 0.5) * 2.0 * _GrainIntensity;
                colMix += grainEffect;

                // 最終顏色與 Alpha 處理
                fixed4 outColor = fixed4(colMix, i.color.a);

                // 支援 UI 遮罩裁切 (ScrollView)
                outColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return outColor;
            }
            ENDCG
        }
    }
}
