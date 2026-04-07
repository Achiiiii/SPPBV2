Shader "SPPB/HintEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Tint", Color) = (1,1,1,1)

        [Header(Halftone Settings)]
        _DotColor ("Dot Color", Color) = (1,1,1,1)
        _DotDensity ("Dot Density", Range(5, 100)) = 30.0
        _DotSize ("Dot Size", Range(0.1, 3.0)) = 0.8
        _DotSharpness ("Dot Sharpness", Range(0.01, 0.5)) = 0.1
        _DotOpacity ("Dot Opacity", Range(0, 1)) = 0.3

        [Header(Wave Animation)]
        _WaveSpeed ("Wave Speed", Range(0.1, 5.0)) = 1.0
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.5)) = 0.15
        _WaveFrequency ("Wave Frequency", Range(1, 10)) = 3.0

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
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            fixed4 _Color;

            // Halftone parameters
            fixed4 _DotColor;
            float _DotDensity;
            float _DotSize;
            float _DotSharpness;
            float _DotOpacity;

            // Wave parameters
            float _WaveSpeed;
            float _WaveAmplitude;
            float _WaveFrequency;

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
                // Sample original texture
                fixed4 texColor = tex2D(_MainTex, i.uv);
                float spriteAlpha = texColor.a;

                // 透明像素返回透明色
                // 使用 alpha=0 代替 discard，避免破壞 mobile GPU 的 early-Z 優化
                if (spriteAlpha < 0.01)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // Apply color tint
                fixed4 originalColor = texColor * i.color * _Color;

                // Calculate grid cell
                float2 gridUV = i.uv * _DotDensity;
                float2 cellIndex = floor(gridUV);
                float2 cellUV = frac(gridUV);

                // Cell center is at (0.5, 0.5)
                float2 cellCenter = float2(0.5, 0.5);

                // Sample texture at cell center for luminance
                float2 sampleUV = (cellIndex + cellCenter) / _DotDensity;
                fixed4 sampleColor = tex2D(_MainTex, sampleUV);

                // Calculate luminance (perceived brightness)
                float luminance = dot(sampleColor.rgb, float3(0.299, 0.587, 0.114));

                // Wave animation effect
                float time = _Time.y * _WaveSpeed;
                float waveOffset = sin(cellIndex.x * 0.5 + cellIndex.y * 0.5 + time * _WaveFrequency) * _WaveAmplitude;

                // Dot size based on luminance (brighter = smaller dot for classic halftone)
                // Invert: darker areas get bigger dots
                float baseDotRadius = (1.0 - luminance) * _DotSize * 0.5;

                // Apply wave animation to dot size
                float dotRadius = baseDotRadius + waveOffset * 0.1;
                dotRadius = max(dotRadius, 0.01); // Minimum size

                // Distance from cell center
                float dist = length(cellUV - cellCenter);

                // Smooth circle mask (1 = inside dot, 0 = outside dot)
                float dotMask = 1.0 - smoothstep(dotRadius - _DotSharpness, dotRadius + _DotSharpness, dist);

                // Custom color dot overlay with transparency
                // dotMask = 1 inside dot, blend with _DotColor
                fixed4 finalColor;
                float blendAmount = dotMask * _DotOpacity;
                finalColor.rgb = lerp(originalColor.rgb, _DotColor.rgb, blendAmount);
                finalColor.a = spriteAlpha;

                // Apply vertex color alpha
                finalColor.a *= i.color.a;

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
