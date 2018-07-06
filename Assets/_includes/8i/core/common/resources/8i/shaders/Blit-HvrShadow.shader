Shader "Hidden/8i/Blit-HvrShadow"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		[HideInInspector] _ScreenSpaceShadowTex("Texture", 2D) = "white" {}
	}

	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag

            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA

 
			#include "UnityCG.cginc"
 
			uniform sampler2D _MainTex;
			uniform sampler2D_float _ScreenSpaceShadowTex;

			float4 frag(v2f_img i) : COLOR
			{
				float4 c = tex2D(_MainTex, i.uv);

				float s = tex2D(_ScreenSpaceShadowTex, i.uv).r;

#if !UNITY_COLORSPACE_GAMMA
                s = GammaToLinearSpace(s);
#endif

				c.rgb *= s;
				return c;
			}
			ENDCG
		}
	}
}
