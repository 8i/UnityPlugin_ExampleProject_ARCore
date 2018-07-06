// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "8i/HvrUnlit"
{
	Properties
	{
		[HideInInspector] _Mode("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _ZWrite("__zw", Float) = 1.0
	}

	SubShader
	{
		CGINCLUDE
	
		#pragma vertex vert
		#pragma fragment frag
		#pragma multi_compile __ SCREENSPACE_QUAD

		#include "UnityCG.cginc"
		#include "Hvr.cginc"

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float4 screenPos : TEXCOORD0;
		};

		v2f vert(appdata_full v)
		{
			v2f o;

#if defined(SCREENSPACE_QUAD)
			o.vertex = float4(v.vertex.xy, 0.0, 0.5);
#else
			o.vertex = UnityObjectToClipPos(v.vertex);
#endif  

			o.screenPos = ComputeScreenPos(o.vertex);
			return o;
		}

		sampler2D _HvrColorTex;
		sampler2D_float _HvrDepthTex;

		ENDCG

		Pass
		{
			ZTest Less
			Cull Off

			Blend[_SrcBlend][_DstBlend]
			ZWrite[_ZWrite]

			CGPROGRAM

			#pragma enable_d3d11_debug_symbols
			#pragma target 3.0
			#pragma multi_compile __ TRANSPARENCY_DITHER
			#pragma multi_compile_fog

			struct Output
			{
				half4 color : COLOR;
				float depth : DEPTH;
			};

			Output frag(v2f i)
			{
				// Calculate the screen position of the pixel
				float2 screenPos = (i.screenPos.xy / i.screenPos.w);

				half4 color = tex2D(_HvrColorTex, screenPos);
				float depth = tex2D(_HvrDepthTex, screenPos).r;

				Output o;

				// Set the final colors here
				o.color = color;
				o.depth = depth;

				return o;
			}

			ENDCG
		}
	}
	Fallback off
}
