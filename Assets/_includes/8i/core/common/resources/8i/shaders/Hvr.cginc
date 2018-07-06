#include "UnityCG.cginc"

float4x4 _ViewProjectInverse;

float GenericBinaryDither4x4(float value, float2 sceneUVs, float2 renderTargetDimension)
{
    float4x4 mtx = float4x4(
        float4(1, 9, 3, 11) / 17.0,
        float4(13, 5, 15, 7) / 17.0,
        float4(4, 12, 2, 10) / 17.0,
        float4(16, 8, 14, 6) / 17.0
        );

    float2 px = floor(renderTargetDimension * sceneUVs);
    int xSmp = fmod(px.x, 4);
    int ySmp = fmod(px.y, 4);
    float4 xVec = 1 - saturate(abs(float4(0, 1, 2, 3) - xSmp));
    float4 yVec = 1 - saturate(abs(float4(0, 1, 2, 3) - ySmp));
    float4 pxMult = float4(dot(mtx[0], yVec), dot(mtx[1], yVec), dot(mtx[2], yVec), dot(mtx[3], yVec));
    return round(value + dot(pxMult, xVec));
}

float BinaryDither4x4(float value, float2 sceneUVs)
{
    return GenericBinaryDither4x4(value, sceneUVs, _ScreenParams.xy);
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

half3 WorldPositionToNormal(float4 worldPosition)
{
    float3 dx = ddx(worldPosition.xyz);
    float3 dy = ddy(worldPosition.xyz);

    // The built-in variable _ProjectionParams.x contains a +1 or –1 value which 
    // indicates whether projection has been flipped upside down or not.
    // https://docs.unity3d.com/Manual/SL-PlatformDifferences.html

    if (_ProjectionParams.x < 0)
    {
        return normalize(cross(dy, dx));
    }
    else
    {
        return normalize(cross(dx, dy));
    }
}

// Calculate lighting color in the space of a spot light.
float3 AnalyticSpotLightColor(float3 sourceColor, float3 fragPos, 
        float3 lightPos, float3 lightDir, float coneAngle, float exponent)
{
    const float PI = 3.1415926;

    float3 v = normalize(fragPos - lightPos);
    float3 l = normalize(lightDir);
    float vdotl = dot(v, l);
    float tmp = acos(vdotl) / (coneAngle * 0.5);
    tmp = clamp(tmp, 0, PI);
    float base = saturate(cos(tmp));

    return sourceColor * pow(base, exponent);
}


// Calculate the color of a lit fragment based on the SH coefficients and the normal
float3 DoShadeSH9(float3 normal, float cs1[9], float cs2[9], float cs3[9])
{
    const float c0 = 0.2820948f;
    const float c1 = 0.325735f;
    const float c2 = 0.2731371f;
    const float c3 = 0.15769578f;
    const float c4 = 0.1365685f;

    float x = normal.x;
    float y = normal.y;
    float z = normal.z;

    float x1 = x * y;
    float y1 = y * z;
    float z1 = z * z;
    float w1 = z * x;

    float c = x * x - y * y;

    float r = c1 * (cs1[2] * z - cs1[3] * x - cs1[1] * y) + c0 * cs1[0] + c2 * (cs1[4] * x1 - cs1[5] * y1 - cs1[7] * w1) + c3 * cs1[6] * z1 + c * c4 * cs1[8];
    float g = c1 * (cs2[2] * z - cs2[3] * x - cs2[1] * y) + c0 * cs2[0] + c2 * (cs2[4] * x1 - cs2[5] * y1 - cs2[7] * w1) + c3 * cs2[6] * z1 + c * c4 * cs2[8];
    float b = c1 * (cs3[2] * z - cs3[3] * x - cs3[1] * y) + c0 * cs3[0] + c2 * (cs3[4] * x1 - cs3[5] * y1 - cs3[7] * w1) + c3 * cs3[6] * z1 + c * c4 * cs3[8];

    return float3(r, g, b);
}
