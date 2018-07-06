Shader "Hidden/8i/HvrShadowMapParamSniffer"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	SubShader
	{
		

		Pass
		{
			Cull Off
			ZTest Always

			CGPROGRAM
			// #pragma vertex vert_img
			#pragma vertex myvert
			#pragma fragment frag_view_sniffer

 			#include "UnityCG.cginc"

 			struct vertex_in {
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct vertex_out { 
				float4 pos : SV_POSITION; 
				float2 uv : TEXCOORD0; 
			};

			vertex_out myvert(vertex_in i)
			{
				vertex_out o;
				o.pos = i.pos;
				o.uv = i.uv;
				return o;
			}
 
			uniform sampler2D _MainTex;

			float4 frag_view_sniffer(vertex_out i) : COLOR
			{
				float2 fIndex = i.uv * float2(4, 16);//float2(3.49, 13.96) + 0.5;
				int2 iIndex = int2(floor(fIndex.x), floor(fIndex.y));

				float c;

				if (iIndex.y < 4)
				{
					c = UNITY_MATRIX_V[iIndex.x][iIndex.y];
				}
				else if (iIndex.y < 8)
				{
					c = UNITY_MATRIX_P[iIndex.x][iIndex.y - 4];	
				}
				else if (iIndex.y < 12)
				{
					c = unity_ShadowSplitSpheres[iIndex.y - 8][iIndex.x];
				}
				else if (iIndex.y < 13)
				{
					c = unity_OrthoParams[iIndex.x];
				}
				else
				{
					c = unity_ShadowSplitSqRadii[iIndex.x];
				}
				return float4(c,c,c,c);
			}
			ENDCG
		}

		Pass
		{
			Cull Off
			ZTest Always

			CGPROGRAM
			// #pragma vertex vert_img
			#pragma vertex myvert
			#pragma fragment frag_param_sniffer

 			#include "UnityCG.cginc"

 			struct vertex_in {
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct vertex_out { 
				float4 pos : SV_POSITION; 
				float2 uv : TEXCOORD0; 
			};

			vertex_out myvert(vertex_in i)
			{
				vertex_out o;
				o.pos = i.pos;
				o.uv = i.uv;
				return o;
			}
 
			uniform sampler2D _MainTex;

			float4 frag_param_sniffer(vertex_out i) : COLOR
			{
#if UNITY_UV_STARTS_AT_TOP				
				float2 fIndex = i.uv * float2(4, 4);
#else
				float2 fIndex = float2(i.uv.x, 1.0 - i.uv.y) * float2(4, 4);			
#endif
				int2 iIndex = int2(floor(fIndex.x), floor(fIndex.y));

				float4 result;
				for(int i = 0; i < 4; ++i)
				{
					result[i] = unity_WorldToShadow[i][iIndex.x][iIndex.y];
				}

				return result;
			}
			ENDCG
		}

		
	}
}
