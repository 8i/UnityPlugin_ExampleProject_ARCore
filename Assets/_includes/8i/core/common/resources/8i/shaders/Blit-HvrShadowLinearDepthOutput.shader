Shader "Hidden/8i/ShadowLinearDepthOutput"
{
	Properties
	{
		_oDEP("Offscreen Depth", 2D) = "" {}
	}
	SubShader
	{
		Name "HVRShadowLinearDepthOutput"

		CGINCLUDE
		#include "UnityCG.cginc"
		#include "Hvr.cginc"

		struct ia_out
		{
			float4 vertex : POSITION;
			float2 uv     : TEXCOORD0;
		};

		struct vs_out
		{
			float4 vertex	: SV_POSITION;
			float4 spos		: TEXCOORD0;
			float2 uv 		: TEXCOORD1;
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
			o.vertex.xy = (o.vertex.xy - 0.5) * 2.0;
			o.uv = v.uv;
			return o;
		}

		ENDCG

		Pass
		{
			Name "Overwrite buffer with linear depth"

			Cull Off
			ZTest LEqual
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			uniform sampler2D_float _oDEP;
			float4 _CurrLightPositionRange;
			float4 _CurrLightShadowBias;
			float4 _ShadowParameters;

			fs_out frag(vs_out v)
			{
				fs_out o;

#if UNITY_UV_STARTS_AT_TOP
				v.uv.y = 1.0 - v.uv.y;
#endif
				float2 uv = v.uv;
				float dep;
				
				dep = tex2D(_oDEP, uv);

				float4 linear_depth;
				if (dep >= 1.0)
				{
					linear_depth = 1.0; // eliminate the "black ring"
				}
				else
				{
					// need to get linear depth/range from dep
					half4 WPOS = DepthToWPOS(dep, uv);
					float3 vec = WPOS.xyz - _CurrLightPositionRange.xyz;

					// output linear depth
					linear_depth = UnityEncodeCubeShadowDepth((length(vec) + _CurrLightShadowBias.x) * _CurrLightPositionRange.w);
				}


				float intendedDither = _ShadowParameters.z;
				float dither = GenericBinaryDither4x4( intendedDither - 0.5, uv, _ShadowParameters.xy);
				if (dither <= 0)
				{
					linear_depth = 1.0;
				}

#if defined(UNITY_REVERSED_Z) // in Unity 5.6 we have a different buffer copying path so keep the original depth
				linear_depth = 1.0 - linear_depth;
#endif
				o.color = linear_depth;
				o.depth = linear_depth;

				return o;
			}

			ENDCG
		}

		Pass
		{
			Name "Clear cubemap depth to infinite far. Replacement of GL.Clear regarding different config over depth"

			Cull Off
			ZTest Always
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			fs_out frag(vs_out v)
			{
				fs_out o;

#if defined(UNITY_REVERSED_Z)
				float4 infinite_far = 0;
				o.color = infinite_far;
				o.depth = infinite_far;
#else
				float4 infinite_far = 1;
				o.color = infinite_far;
				o.depth = infinite_far;
#endif

				return o;
			}

			ENDCG
		}
	}
}