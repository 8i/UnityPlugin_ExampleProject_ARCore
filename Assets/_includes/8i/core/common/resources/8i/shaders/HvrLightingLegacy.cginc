#include "UnityCG.cginc"
#include "HvrCommonUniforms.cginc"

struct LegacyFragOutput
{
    half4 color : COLOR;
    float depth : DEPTH;
};

LegacyFragOutput fragLegacyBase(v2f i)
{
#if !defined(_ALPHABLEND_ON) && !defined(TRANSPARENCY_DITHER)
    _ColorFactor.a = 1;
#endif

    // Calculate the screen position of the pixel
    float2 screenPos = (i.screenPos.xy / i.screenPos.w);

    half4 color = tex2D(_HvrColorTex, screenPos);
    float depth = tex2D(_HvrDepthTex, screenPos).r;
    half4 wpos = DepthToWPOS(depth, screenPos);

    color.a = 1; // Set this to 1 for now, as we don't want the alpha within the color texture to be used
    color = color * _ColorFactor;

    color.rgb += _EmissiveTerm.rgb;

    UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, wpos)));
    UNITY_APPLY_FOG(i.fogCoord, color);

    LegacyFragOutput o;

    // Set the final colors here
    o.color = color;

#if TRANSPARENCY_DITHER
    o.color.a = BinaryDither4x4(_ColorFactor.a - 0.5, screenPos);
#endif

    o.depth = depth;

    return o;
}