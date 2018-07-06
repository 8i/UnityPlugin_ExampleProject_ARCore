Shader "Hidden/8i/ColorComposite" {
	SubShader { 

		CGINCLUDE
		#include "UnityCG.cginc"
		#include "Hvr.cginc"

		struct ia_out
		{
			float4 vertex : POSITION;
		};

		struct vs_out
		{
			float4 vertex	: SV_POSITION;
			float4 spos		: TEXCOORD0;
		};

		struct fs_out
		{
			float4 color : SV_Target;
			float depth : SV_Depth;
		};

		vs_out vert(ia_out v)
		{
			vs_out o;
			// just copy the vertex output since it's [-1,1] and in homo space
			o.vertex = v.vertex;
			o.spos = ComputeScreenPos(v.vertex);
			return o;
		}

		uniform sampler2D _HvrColorTex;
		uniform sampler2D_float _HvrDepthTex;
		uniform float4 _Tint;

		ENDCG

		Pass {
			
			Name "Composite opaque color with tint but no transparency, with depth writing."

			// Must be Less to preserve skybox
 			ZTest Less
 			Cull Off 
 			ZWrite On
 			Blend SrcAlpha OneMinusSrcAlpha, One One

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			fs_out frag(vs_out v)
			{
				fs_out o;
				float2 uv = v.spos.xy;
				o.color = tex2D(_HvrColorTex, uv);
				o.color.rgb *= _Tint.rgb;

				o.depth = tex2D(_HvrDepthTex, uv);
				return o;
			}

			ENDCG 
		}

		Pass {
			
			Name "Composite color regarding alpha blending and tint, with no depth writing."

 			ZTest Less
 			Cull Off 
 			ZWrite Off
 			Blend SrcAlpha OneMinusSrcAlpha, One One

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			fs_out frag(vs_out v)
			{
				fs_out o;
				float2 uv = v.spos.xy;
				
				o.color = tex2D(_HvrColorTex, uv) * _Tint;
				o.depth = tex2D(_HvrDepthTex, uv);

				return o;

			}

			ENDCG 

		}

		Pass {
			
			Name "Composite color regarding dither and tint, with no depth writing."

 			ZTest Less
 			Cull Off 
 			ZWrite Off
 			Blend SrcAlpha OneMinusSrcAlpha, One One

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			fs_out frag(vs_out v)
			{
				fs_out o;
				float2 uv = v.spos.xy;
				
				o.color = tex2D(_HvrColorTex, uv);
				o.color.rgb *= _Tint.rgb;

				float2 screenPos = (v.spos.xy / v.spos.w);
				float dither = BinaryDither4x4(_Tint.a - 0.5, screenPos);
				o.color.a *= dither;

				o.depth = tex2D(_HvrDepthTex, uv);	

				
				return o;

			}

			ENDCG 

		}
	}
	Fallback Off 
}
