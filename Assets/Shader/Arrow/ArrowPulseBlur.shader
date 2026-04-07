Shader "SPPB/ArrowPulseBlur"
{
    Properties
    {
        [Header(Main)]
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Arrow Index)]
        _Index ("Arrow Index (0-2)", Range(0, 2)) = 0

        [Header(Pulse Settings)]
        _PulseSpeed ("Pulse Speed", Range(0.5, 5)) = 2
        _PulsePhaseOffset ("Phase Offset Per Index", Range(0, 2)) = 0.5
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.2
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 1.0
        _AlphaFalloff ("Alpha Falloff Per Index", Range(0, 0.5)) = 0.25

        [Header(Blur Settings)]
        _BlurAmount ("Blur Amount", Range(0, 10)) = 2
        _BlurFalloff ("Blur Falloff Per Index", Range(0, 3)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _Color;

            float _Index;
            float _PulseSpeed;
            float _PulsePhaseOffset;
            float _MinAlpha;
            float _MaxAlpha;
            float _AlphaFalloff;

            float _BlurAmount;
            float _BlurFalloff;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // 高斯模糊取樣
            fixed4 GaussianBlur(sampler2D tex, float2 uv, float2 texelSize, float blurSize)
            {
                fixed4 color = fixed4(0, 0, 0, 0);

                // 9 點取樣高斯模糊
                float weights[9] = {
                    0.0625, 0.125, 0.0625,
                    0.125,  0.25,  0.125,
                    0.0625, 0.125, 0.0625
                };

                float2 offsets[9] = {
                    float2(-1, -1), float2(0, -1), float2(1, -1),
                    float2(-1,  0), float2(0,  0), float2(1,  0),
                    float2(-1,  1), float2(0,  1), float2(1,  1)
                };

                for (int i = 0; i < 9; i++)
                {
                    float2 offset = offsets[i] * texelSize * blurSize;
                    color += tex2D(tex, uv + offset) * weights[i];
                }

                return color;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 計算此箭頭的模糊程度（越後面越模糊）
                float blurSize = _BlurAmount + _Index * _BlurFalloff;

                // 取樣（帶模糊）
                fixed4 col;
                if (blurSize > 0.1)
                {
                    col = GaussianBlur(_MainTex, i.uv, _MainTex_TexelSize.xy, blurSize);
                }
                else
                {
                    col = tex2D(_MainTex, i.uv);
                }

                // 套用顏色
                col *= _Color * i.color;

                // 計算閃爍透明度
                // 每個箭頭有不同相位，產生波浪效果
                float phase = _Time.y * _PulseSpeed + _Index * _PulsePhaseOffset * 3.14159;
                float pulse = sin(phase) * 0.5 + 0.5; // 0-1

                // 基礎透明度（越後面越透明）
                float baseAlpha = _MaxAlpha - _Index * _AlphaFalloff;
                baseAlpha = max(baseAlpha, _MinAlpha);

                // 計算最終透明度（在 minAlpha 和 baseAlpha 之間閃爍）
                float minPulseAlpha = baseAlpha * 0.5;
                float finalAlpha = lerp(minPulseAlpha, baseAlpha, pulse);

                col.a *= finalAlpha;

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
