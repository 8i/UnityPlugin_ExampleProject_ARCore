Shader "8i/Example/HVRActor_Hologram_3D"
{
    Properties
    {
        _HologramColor("Hologram Color", Color) = (0.0, 0.2, 0.6, 1.0)
        _HologramGlow("Hologram Glow", Float) = 20.0
        _BlurEffect("Blur Effect", Float) = 0.75
        _RimPower("Rim Power", Range(0.0, 2.0)) = 0.5
        _ScanLines("Scan lines", Int) = 100
        _fade_upper("Fade Upper", Float) = 0.75
        _fade_lower("Fade Lower", Float) = 0.0
        [HideInInspector] _HvrColorTex("Texture", 2D) = "white" {}
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
        UNITY_FOG_COORDS(1)
    };

    struct Output
    {
        half4 color : COLOR;
        float depth : DEPTH;
    };

    fixed4 _HologramColor;
    uniform float _HologramGlow;
    uniform float _BlurEffect;
    uniform float _RimPower;
    uniform int _ScanLines;

    sampler2D _HvrColorTex;
    sampler2D_float _HvrDepthTex;

    sampler2D_float _ScreenSpaceShadowTex;

    float4x4 _ViewProjectInverse;

    uniform float3 _phaseGlitch;

    uniform float4 _colorGlitchColor;
    uniform float2 _colorGlitchOffset;

    uniform float _fade_upper;
    uniform float _fade_lower;

    float BinaryDither4x4(float value, float2 sceneUVs)
    {
        float4x4 mtx = float4x4(
            float4(1, 9, 3, 11) / 17.0,
            float4(13, 5, 15, 7) / 17.0,
            float4(4, 12, 2, 10) / 17.0,
            float4(16, 8, 14, 6) / 17.0
            );
        float2 px = floor(_ScreenParams.xy * sceneUVs);
        int xSmp = fmod(px.x, 4);
        int ySmp = fmod(px.y, 4);
        float4 xVec = 1 - saturate(abs(float4(0, 1, 2, 3) - xSmp));
        float4 yVec = 1 - saturate(abs(float4(0, 1, 2, 3) - ySmp));
        float4 pxMult = float4(dot(mtx[0], yVec), dot(mtx[1], yVec), dot(mtx[2], yVec), dot(mtx[3], yVec));
        return round(value + dot(pxMult, xVec));
    }

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

    half4 WorldPositionToNormal(float4 worldPosition)
    {
        float3 dx = ddx(worldPosition.xyz);
        float3 dy = ddy(worldPosition.xyz);

        // The built-in variable _ProjectionParams.x contains a +1 or –1 value which 
        // indicates whether projection has been flipped upside down or not.
        // https://docs.unity3d.com/Manual/SL-PlatformDifferences.html

        if (_ProjectionParams.x < 0)
        {
            return half4(normalize(cross(dy, dx)), 1);
        }
        else
        {
            return half4(normalize(cross(dx, dy)), 1);
        }
    }

    v2f vert(appdata_full v)
    {
        v2f o;
#if defined(SCREENSPACEQUAD)
        o.vertex = float4(v.vertex.xy, 0.0, 0.5);
#else
        o.vertex = UnityObjectToClipPos(v.vertex);
#endif  

        o.screenPos = ComputeScreenPos(o.vertex);
        return o;
    }

    Output frag_hologram(v2f i)
    {
        Output output;

        // Calculate the screen position of the pixel
        float2 screenPos = (i.screenPos.xy / i.screenPos.w);

        float depth = tex2D(_HvrDepthTex, screenPos);
        float4 wpos = DepthToWPOS(depth, screenPos);

        float glitch_v1 = min(_phaseGlitch.x, _phaseGlitch.y);
        float glitch_v2 = max(_phaseGlitch.x, _phaseGlitch.y);
        float glitch_amount = _phaseGlitch.z;

        if (wpos.y < glitch_v2 && wpos.y > glitch_v1)
        {
            wpos = wpos + glitch_amount * 10;

            float4 screenPos = ComputeScreenPos(mul(UNITY_MATRIX_VP, wpos));
            screenPos.xy /= screenPos.w;

            screenPos.xy = screenPos;
        }

        half4 color = tex2D(_HvrColorTex, screenPos.xy);
        float alpha = color.a;
        depth = tex2D(_HvrDepthTex, screenPos.xy);
        wpos = DepthToWPOS(depth, screenPos);
        half4 normal = WorldPositionToNormal(wpos);

        // Rim Color
        float3 viewDir = normalize(ObjSpaceViewDir(wpos));
        float dotProduct = 1 - dot(normal, viewDir);
        float rimWidth = 0.7;

        float rimColor = smoothstep(1 - rimWidth, 1.0, dotProduct);
        rimColor *= _RimPower;
        color.rgb += rimColor;

        // Color Fade
        float color_fade = lerp(0, 1, (wpos.y - _fade_lower) / (_fade_upper - _fade_lower));
        color_fade = clamp(color_fade, 0, 1);
        color.rgb *= color_fade;

        // Scanlines
        half scanline = abs(sin(wpos.x * _ScanLines)); // 3D Scanlines

        float3 luma = float3(0.2126, 0.7152, 0.0722);
        float mono = dot(color.rgb, luma) * 0.33;

        half4 col_offset = tex2D(_HvrColorTex, screenPos.xy + _colorGlitchOffset);

#if UNITY_COLORSPACE_GAMMA
        color.rgb = mono * _HologramColor * _HologramGlow + dot(col_offset.rgb, luma) * _colorGlitchColor;
#else
        color.rgb = mono * _HologramColor * pow(_HologramGlow, 2) + dot(col_offset.rgb, luma) * _colorGlitchColor;
#endif

        // Blurred image addition
        int blurSamples = 6;
        float4 blurSum = float4(0, 0, 0, 0);
        int diff = (6 - 1) / 2;
        float sizeFactor = 0.0125;

        for (int x = -diff; x <= diff; x++)
        {
            for (int y = -diff; y <= diff; y++)
            {
                float2 offset = float2(x, y) * sizeFactor;
                float4 color = tex2D(_HvrColorTex, screenPos.xy + offset) * color_fade;
                color.a = 0;
                blurSum += color;
            }
        }
        blurSum *= _BlurEffect;

        color = (color + (blurSum / (blurSamples * blurSamples)));
        color.a = clamp(scanline, 0.5, 1.0) * alpha;

        UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, wpos)));
        UNITY_APPLY_FOG(i.fogCoord, color);

#if TRANSPARENCY_DITHER
        color.a *= BinaryDither4x4(color.a - 0.5, screenPos);
#endif

        // Set the final colors here
        output.color = color;
        output.depth = depth;

        return output;
    }

    ENDCG

    SubShader
    {
        Tags{ "RenderType" = "Transparent" "IgnoreProjector" = " True" }
        LOD 100

        Pass
        {
            ZTest Less
            Cull Back

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_hologram
            #pragma target 3.0
            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA
            #pragma multi_compile __ TRANSPARENCY_DITHER
            #pragma multi_compile __ RECEIVE_SHADOWS
            #pragma multi_compile_fog
            ENDCG
        }

        Pass
        {
            ZTest Less

            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha, One One
            ZWrite[_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_hologram
            #pragma target 3.0
            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA
            #pragma multi_compile __ TRANSPARENCY_DITHER
            #pragma multi_compile __ RECEIVE_SHADOWS
            #pragma multi_compile_fog
            ENDCG
        }
    }
    Fallback off

    CustomEditor "ShaderGUI_HvrStandard"
}
