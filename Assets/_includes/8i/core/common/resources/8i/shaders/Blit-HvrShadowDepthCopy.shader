Shader "Hidden/8i/HVRShadowDepthBlit"
{
	Properties
	{
		_oDEP("Offscreen Depth", 2D) = "" {}
	}
	SubShader
	{
		Name "HVRShadowDepthBlit"

		Pass
		{
			Name "Single depth map blit pass(index from [0, 4)) to shadowmap atlas. For Spot lights & directional lights"

			Cull Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"
			#include "Hvr.cginc"

			uniform sampler2D_float _oDEP;
			uniform float4 _ShadowParameters;

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

			fs_out frag(vs_out v)
			{
				float dep = tex2D(_oDEP, v.spos.xy);

				fs_out output;

				float intendedDither = _ShadowParameters.z;
				float dither = GenericBinaryDither4x4( intendedDither - 0.5, v.spos, _ShadowParameters.xy);
				if (dither <= 0)
				{
#if defined(UNITY_REVERSED_Z)
					dep = 0;
#else					
					dep = 1;
#endif					
				}

				output.depth = dep;
				output.color = float4(dep, dep, dep, 1);
				return output;
			}


			ENDCG
		}

		Pass
		{
			Name "Cubemap linear depth _additive_ blender"

			Cull Off
			ZTest Less
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"

			uniform sampler2D_float _oDEP;

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

			fs_out frag(vs_out v)
			{
				fs_out o;
#if !defined(UNITY_UV_STARTS_AT_TOP)
				v.spos.y = 1.0 - v.spos.y;
#endif
				float4 c = tex2D(_oDEP, v.spos);
				float linear_depth;
				linear_depth = c.r;
#if defined(UNITY_REVERSED_Z)
				o.color = float4(1 - linear_depth, 1 - linear_depth, 1 - linear_depth, 1 - linear_depth);
#else
				o.color = float4(linear_depth, linear_depth, linear_depth, linear_depth);
#endif				
				o.depth = linear_depth;

				return o;
			}

			ENDCG
		}

		Pass
		{
			Name "Cubemap linear depth blender for 5.6 or newer"

			Cull Off
			ZTest LEqual
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"
			#include "Hvr.cginc"

			uniform sampler2D_float _oDEP;
			uniform float4 _ShadowParameters;
			uniform sampler2D_float _sceneDepth;
			uniform float4 _CurrLightPositionRange;
			uniform float4x4 _FlipMapMatrix;

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

			fs_out frag(vs_out v)
			{
				fs_out o;
				float2 sceneUV = v.spos.xy;

				// depending on the behaviour of CopyTexture()
				sceneUV = mul(_FlipMapMatrix, float4(sceneUV, 0, 1)).xy;

				// convert dep from post clip space to linear space
				float dep = tex2D(_oDEP, v.spos).r;
				float4 d1;
				if (dep >= 1.0)
				{
					d1 = 1.0; // eliminate the "black ring"
				}
				else
				{
					// need to get linear depth/range from dep
					half4 WPOS = DepthToWPOS(dep, v.spos);
					float3 vec = WPOS.xyz - _CurrLightPositionRange.xyz;

					// output linear depth
					d1 = UnityEncodeCubeShadowDepth(length(vec) * _CurrLightPositionRange.w);
				}

				float4 d2 = tex2D(_sceneDepth, sceneUV);

				float linear_depth = d2;
				if (d1.r > d2.r)
				{
					linear_depth = d2.r;
				}
				else
				{
					linear_depth = d1.r;

					float intendedDither = _ShadowParameters.z;
					float dither = GenericBinaryDither4x4( intendedDither - 0.5, sceneUV, _ShadowParameters.xy);
					if (dither <= 0)
					{
						linear_depth = d2.r; // hollow depth will be fallback to _sceneDepth
					}
				}

				o.color = float4(linear_depth, linear_depth, linear_depth, linear_depth);
#if defined(UNITY_REVERSED_Z)				
				o.depth = 1 - linear_depth;
#else
				o.depth = linear_depth;
#endif
				return o;
			}

			ENDCG
		}

		Pass
		{
			Name "Cubemap linear depth blender for 5.6 or newer but with NO CopyTexture support"

			Cull Off
			ZTest LEqual
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"
			#include "Hvr.cginc"

			uniform sampler2D_float _oDEP;
			uniform float4 _ShadowParameters;
			uniform sampler2D_float _sceneDepth;
			uniform float4 _CurrLightPositionRange;

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

			fs_out frag(vs_out v)
			{
				fs_out o;
#if !UNITY_UV_STARTS_AT_TOP
				v.spos.y = 1.0 - v.spos.y;
#endif

				float2 uv = v.spos.xy;
#if !UNITY_UV_STARTS_AT_TOP
				uv.y = 1 - uv.y;
#endif				

				// convert dep from post clip space to linear space
				float dep = tex2D(_oDEP, v.spos).r;
				float4 d1;
				if (dep >= 1.0)
				{
					d1 = 1.0; // eliminate the "black ring"
				}
				else
				{
					// need to get linear depth/range from dep
					half4 WPOS = DepthToWPOS(dep, v.spos);
					float3 vec = WPOS.xyz - _CurrLightPositionRange.xyz;

					// output linear depth
					d1 = UnityEncodeCubeShadowDepth(length(vec) * _CurrLightPositionRange.w);
				}

				
#if defined(UNITY_REVERSED_Z)	
				float linear_depth = 1 - d1;
				float a = 0.0;
#else
				float linear_depth = d1;
				float a = 1.0;
#endif				

				float intendedDither = _ShadowParameters.z;
				float dither = GenericBinaryDither4x4( intendedDither - 0.5, uv, _ShadowParameters.xy);
				if (dither <= 0)
				{
					linear_depth = a;
					dep = a;
				}

				
				dep = (a * dep) + (1.0f - a)*(1.0 - dep);
				linear_depth = (a * linear_depth) + (1.0f - a) * (1.0 - linear_depth);

				o.color = float4(linear_depth, linear_depth, linear_depth, linear_depth);
				// use dep to maintain proper occlusion with the scene
#if defined(UNITY_REVERSED_Z)				
				o.depth = 1 - dep;
#else
				o.depth = dep;
#endif

				return o;
			}

			ENDCG
		}

		Pass
		{
			Name "Simply flip on Y axis"

			Cull Off
			ZTest Always
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"
			#include "Hvr.cginc"

			uniform sampler2D_float _oDEP;

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

			fs_out frag(vs_out v)
			{
				fs_out o;

				v.spos.y = 1.0 - v.spos.y;

				float dep = tex2D(_oDEP, v.spos).r;
				o.color = float4(dep, dep, dep, dep);
#if defined(UNITY_REVERSED_Z)				
				o.depth = 1 - dep;
#else
				o.depth = dep;
#endif
				return o;
			}

			ENDCG
		}

		Pass
		{
			Name "Cubemap non-linear depth blender for 2017.3 or newer with compatible renderer"

			Cull Off
			ZTest LEqual
			Blend One Zero
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"
			#include "Hvr.cginc"

			uniform sampler2D_float _oDEP;
			uniform float4 _ShadowParameters;
			uniform sampler2D_float _sceneDepth;
			uniform float4x4 _FlipMapMatrix;

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

			fs_out frag(vs_out v)
			{
				fs_out o;
				float2 sceneUV = v.spos.xy;

				// depending on the behaviour of CopyTexture()
				sceneUV = mul(_FlipMapMatrix, float4(sceneUV, 0, 1)).xy;

				// convert dep from post clip space to linear space
				float dep = tex2D(_oDEP, v.spos).r;
				float4 d1;
				if (dep >= 1.0)
				{
					d1 = 1.0; // eliminate the "black ring"
				}
				else
				{
					d1 = dep;
				}

				float4 d2 = tex2D(_sceneDepth, sceneUV);

				float nonlinear_depth = d2;
				if (d1.r < d2.r)
				{
					nonlinear_depth = d2.r;
				}
				else
				{
					nonlinear_depth = d1.r;

					float intendedDither = _ShadowParameters.z;
					float dither = GenericBinaryDither4x4( intendedDither - 0.5, sceneUV, _ShadowParameters.xy);
					if (dither <= 0)
					{
						nonlinear_depth = d2.r; // hollow depth will be fallback to _sceneDepth
					}
				}

				nonlinear_depth = 1 - nonlinear_depth;
				o.color = float4(nonlinear_depth, nonlinear_depth, nonlinear_depth, nonlinear_depth);
#if defined(UNITY_REVERSED_Z)				
				o.depth = 1 - nonlinear_depth;
#else
				o.depth = nonlinear_depth;
#endif
				return o;
			}

			ENDCG
		}
	}
}