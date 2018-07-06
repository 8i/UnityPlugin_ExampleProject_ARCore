#ifndef _HVR_COMMON_UNIFORMS_
#define _HVR_COMMON_UNIFORMS_

sampler2D_float _LightDepthMap;
sampler2D_float _LightDepthMapPZ;
sampler2D_float _LightDepthMapNZ;
sampler2D_float _LightDepthMapPX;
sampler2D_float _LightDepthMapNX;
sampler2D_float _LightDepthMapPY;
sampler2D_float _LightDepthMapNY;
sampler2D _LightCookie;
samplerCUBE _LightCookieCube;
float4 _LightParameters;
float4 _LightWorldPosition;
float4 _LightWorldDirection;
float4 _ViewportData0;
float4 _ViewportData1;
float4 _ViewportData2;
float4 _ViewportData3;
float4x4 _FromWorldToShadow;
float4x4 _FromWorldToShadow0; // Cascade 0
float4x4 _FromWorldToShadow1; // Cascade 1
float4x4 _FromWorldToShadow2; // Cascade 2
float4x4 _FromWorldToShadow3; // Cascade 3
float4x4 _FromWorldToShadowPZ; // +z
float4x4 _FromWorldToShadowNZ; // -z
float4x4 _FromWorldToShadowPX; // +x
float4x4 _FromWorldToShadowNX; // -x
float4x4 _FromWorldToShadowPY; // +y
float4x4 _FromWorldToShadowNY; // -y

#endif 