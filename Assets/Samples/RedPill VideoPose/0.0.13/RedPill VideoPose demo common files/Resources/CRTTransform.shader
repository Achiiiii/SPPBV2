Shader "Custom/CRTTransform" {
	Properties{
		_Transform("Transform", vector) = (1, 0, 0, 1)
	}

	SubShader{
		Pass {
			CGPROGRAM
			#pragma vertex CustomRenderTextureVertexShader
			#pragma fragment frag
			#include "UnityCustomRenderTexture.cginc"

			sampler2D _MainTex;
			float4 _Transform;
			float4 frag(v2f_customrendertexture input) : COLOR{
				float2x2 transform = float2x2(_Transform[0], _Transform[1], _Transform[2], _Transform[3]);
				float4 base = tex2D(_MainTex, mul(transform, (float2)(input.globalTexcoord)-0.5)+0.5);
				return base;
			}
			ENDCG
		}
	}
}