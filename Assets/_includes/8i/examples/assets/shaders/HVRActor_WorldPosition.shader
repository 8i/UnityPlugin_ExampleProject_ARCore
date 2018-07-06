Shader "8i/Example/HVRActor_WorldPosition"
{
    Properties
    {
        [HideInInspector] _HvrDepthTex("Texture", 2D) = "white" {}
        [HideInInspector] _Mode("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    #pragma multi_compile __ SCREENSPACE_QUAD

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float4 screenPos : TEXCOORD0;
    };

    struct Output
    {
        half4 color : COLOR;
        float depth : DEPTH;
    };

    sampler2D_float _HvrDepthTex;

    float4x4 _ViewProjectInverse;

    float4 DepthToWPOS(float depth, float2 uv)
    {
        // Returns World Position of a pixel from clip-space depth map..
        // H is the viewport position at this pixel in the range -1 to 1.

        // WPOS.rgb in world space, WPOS.z in clip space

#if defined(UNITY_REVERSED_Z)
        // Required for Unity 5.5's reversed depth buffer
        depth = 1.0f - depth;
#endif

        depth = depth * 2 - 1;

        float4 H = float4((uv.x) * 2 - 1, (uv.y) * 2 - 1, depth, 1.0);
        float4 D = mul(_ViewProjectInverse, H);
        D /= D.w;

        return D;
    }

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

    Output frag(v2f i)
    {
        Output output;

        // Calculate the screen position of the pixel
        float2 screenPos = (i.screenPos.xy / i.screenPos.w);

        float depth = tex2D(_HvrDepthTex, screenPos).r;
        half4 wpos = DepthToWPOS(depth, screenPos);

        // Set the final colors here
        output.color = wpos;
        output.depth = depth;

        return output;
    }
    ENDCG

    SubShader
    {
        Tags{ "RenderType" = "Opaque" "IgnoreProjector" = " True" }
        LOD 100

        Pass
        {
            ZTest Less
            Cull Back

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ TRANSPARENCY_DITHER
            #pragma multi_compile __ RECEIVE_SHADOWS
            #pragma multi_compile_fog
            ENDCG
        }
    }
    Fallback off
}
