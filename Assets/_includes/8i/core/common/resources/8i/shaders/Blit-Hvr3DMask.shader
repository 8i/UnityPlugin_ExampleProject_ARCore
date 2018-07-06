Shader "Hidden/8i/Hvr3DMask"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }

        Pass
        {
            ZWrite On
            ZTest Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D_float _MainTex;

            float4x4 _ViewProjectInverse;

            uniform int _mask_length = 0;
            uniform float _mask_types[20];
            uniform float _mask_additive[20];

            uniform float3 _mask_sphere_center[20];
            uniform float _mask_sphere_radius[20];

            uniform float4x4 _mask_box_matrix[20];

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

            float distance(float3 a, float3 b)
            {
                float x = (a[0] - b[0]) * (a[0] - b[0]);
                float y = (a[1] - b[1]) * (a[1] - b[1]);
                float z = (a[2] - b[2]) * (a[2] - b[2]);
                return sqrt(x + y + z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float frag(v2f i) : SV_TARGET
            {
                float depth = tex2D(_MainTex, i.uv).r;
                half4 wpos = DepthToWPOS(depth, i.uv);
                float hiddenDepth = 1;

#if defined(UNITY_REVERSED_Z)
                // Required for Unity 5.5's reversed depth buffer
                hiddenDepth = 0;
#endif

                float val = 1;

                for (int i = 0; i < _mask_length; i++)
                {
                    float type = _mask_types[i];
                    float additive = _mask_additive[i];
                    
                    float sphere = 0;
                    float box = 0;

                    float dist = distance(wpos, _mask_sphere_center[i]);
                    sphere = step(dist, _mask_sphere_radius[i]);

                    half4 wpos_t = mul(_mask_box_matrix[i], wpos);

                    float x1 = step(-0.5, wpos_t.x);
                    float x2 = step(wpos_t.x, 0.5);

                    float y1 = step(-0.5, wpos_t.y);
                    float y2 = step(wpos_t.y, 0.5);

                    float z1 = step(-0.5, wpos_t.z);
                    float z2 = step(wpos_t.z, 0.5);

                    box = x1 * x2 * y1 * y2 * z1 * z2;

                    if (lerp(sphere, box, type) > 0)
                        val = additive;
                }

                if (val == 0)
                    depth = hiddenDepth;

                return depth;
            }
            ENDCG
        }
    }
}
