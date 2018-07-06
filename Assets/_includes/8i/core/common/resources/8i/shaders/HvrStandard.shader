Shader "8i/HvrStandard"
{
    Properties
    {
        _ColorFactor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _EmissiveTerm("Emissive", Color) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _HvrColorTex("Texture", 2D) = "white" {}
        [HideInInspector] _HvrDepthTex("Texture", 2D) = "white" {}
        [HideInInspector] _Mode("__mode", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    SubShader
    {
        CGINCLUDE
    
        #pragma vertex vert
        
        #include "Hvr.cginc"
        
        #pragma multi_compile __ SCREENSPACE_QUAD

        // Spherical harmonics factors for RGB.
        float _SHFactorL2Red[9];
        float _SHFactorL2Green[9];
        float _SHFactorL2Blue[9];

        // I failed to find a proper way to allow offical macro being defined.
        // So force UNITY_SHOULD_SAMPLE_SH to be 1, rather than be 0 or undefined, for all Unity version supported,
        // to allow SH lighting
        #undef UNITY_SHOULD_SAMPLE_SH
        #define UNITY_SHOULD_SAMPLE_SH 1

        #if defined(SHADER_API_GLES3) || defined(SHADER_API_GLES)
        #undef UNITY_PBS_USE_BRDF3
        #define UNITY_PBS_USE_BRDF3 1
        #endif            

#if UNITY_VERSION < 550
        #undef DYNAMICLIGHTMAP_ON
        #undef LIGHTMAP_ON
#endif

        float3 MySHSampleProbeVolume(half4 worldNormal, half3 worldPos)
        {
            return DoShadeSH9(worldNormal, _SHFactorL2Red, _SHFactorL2Green, _SHFactorL2Blue);
        }

        // A hack to override SHEvalLinearL0L1_SampleProbeVolume which used some Unity internal
        // assigned uniform variables are not available to the CommandBuffer. 
        #define SHEvalLinearL0L1_SampleProbeVolume(x, y) MySHSampleProbeVolume(x, y)

        #include "UnityCG.cginc"
        #include "UnityStandardCore.cginc"
        #include "HvrCommonUniforms.cginc"

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float4 screenPos : TEXCOORD0;
            UNITY_FOG_COORDS(1)
        };

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

        fixed4 _ColorFactor; // _ColorFactor to avoid naming clashes in UnityStandardCore.cginc
        fixed4 _EmissiveTerm;
        float4 _LightWorldPos;
        float4 _LightWorldDir;

        sampler2D _HvrColorTex;
        sampler2D_float _HvrDepthTex;

        float3 _CenterWorldPos;
        float3 _CameraWorldPos;
        float4x4 _WorldToObject;
        float4x4 _WorldToLightSpace;

        float4 _LightColorAtSource;
        float4 _ShadowParameters;

        float3 HvrBoundsCenterApproxNormalWorld(half3 fragPos)
        {
            return normalize(fragPos - _CenterWorldPos);
        }

        FragmentCommonData HvrFragmentSetup(v2f i, float2 screenPos, out float depth)
        {
            FragmentCommonData frag;

            half4 color = tex2D(_HvrColorTex, screenPos);
            depth = tex2D(_HvrDepthTex, screenPos).r;
            half4 wpos = DepthToWPOS(depth, screenPos);

            color.rgb *= _ColorFactor.rgb;
            color.rgb += _EmissiveTerm.rgb;

            frag.oneMinusReflectivity = 0.85;   // TODO: find a proper value
#if UNITY_VERSION >= 550
            frag.smoothness = 0;
#else
            frag.oneMinusRoughness = 0;         // TODO: find a proper value
#endif            
            frag.diffColor = color.rgb;
            frag.alpha = color.a;
            frag.specColor = float3(0.0, 0.0, 0.0);

            // For some reason we need to flip the world normal to get the correct visuals.
            frag.normalWorld = -HvrBoundsCenterApproxNormalWorld(wpos.xyz); 
            frag.eyeVec = normalize(wpos.xyz - _CameraWorldPos);
            frag.posWorld = wpos.xyz;

            return frag;
        }

        #include "HvrLightingLegacy.cginc"

        ENDCG

        Pass
        {
            // Standard base lighting pass
            ZTest Less

            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha, One One
            ZWrite[_ZWrite]

            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile __ FIXED_AMBIENT_LIGHTING
            #pragma multi_compile_fog
            #pragma fragment frag

            struct Output
            {
                half4 color : COLOR;
                float depth : DEPTH;
            };

            Output frag(v2f i)
            {
                // HACK: Force force FragmentGI() to use SH lighting routine. 
                // iOS:
#if UNITY_LIGHT_PROBE_PROXY_VOLUME                 
                unity_ProbeVolumeParams.x = 1.0;
                // unity_ProbeVolumeParams.y = 1.0;
                // unity_ProbeVolumeParams.z = 1024;
#endif                

#if FIXED_AMBIENT_LIGHTING
                // HACK: Give a bit hardcoded ambient lighting on mobile platforms.
                _LightColor0.rgb = float3(0.05, 0.05, 0.05);
#else
                // HACK: Force the MainLight() to be dark as we don't want MainLight have any direct diffuse color
                _LightColor0.rgb = float3(0.0, 0.0, 0.0);
#endif
                // Calculate the screen position of the pixel
                float2 screenPos = (i.screenPos.xy / i.screenPos.w);
                float depth;
                FragmentCommonData s = HvrFragmentSetup(i, screenPos, depth);
                half occlusion = Occlusion(screenPos);
                half atten = 1.0; // No attenuation for ambient/SH lighting

#if UNITY_VERSION >= 550
                UnityLight light = MainLight();
#else
                UnityLight light = MainLight(s.normalWorld);
#endif                

                half4 ambientOrLightmapUV = half4(light.color, 1.0);
                UnityGI gi = FragmentGI(s, occlusion, ambientOrLightmapUV, atten, light);

                half4 c;
#if UNITY_VERSION >= 550                
                c.rgb = UNITY_BRDF_PBS(s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, 
                    s.normalWorld, s.eyeVec, gi.light, gi.indirect) * occlusion;
#else
                c.rgb = UNITY_BRDF_PBS(s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, 
                    s.normalWorld, s.eyeVec, gi.light, gi.indirect) * occlusion;
#endif                
                UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, s.posWorld)));
                UNITY_APPLY_FOG(i.fogCoord, c.rgb);

                Output o;
                o.color = half4(c.rgb, s.alpha);
                o.depth = depth;

                return o;
            }

            ENDCG
        }

        Pass
        {
            // Additive lighting for SPOT LIGHT in FORWARD renderer
            ZTest LEqual
            
            Cull Back
            Blend SrcAlpha One, One One
            ZWrite Off

            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile __ SUPPORT_SHADOWING
            #pragma multi_compile_fog
            #pragma fragment frag


            struct Output
            {
                half4 color : COLOR;
                float depth : DEPTH;
            };

            Output frag(v2f i)
            {
                Output o;

                // Calculate the screen position of the pixel
                float2 screenPos = (i.screenPos.xy / i.screenPos.w);
                float depth;
                FragmentCommonData s = HvrFragmentSetup(i, screenPos, depth);
                float3 fragToLight = _LightWorldPos.xyz - s.posWorld;
                // HACK: normalWorld is hard to get right, be full lit
                s.normalWorld = normalize(fragToLight);
                // HACK: remove the reflection appears in spot light by setting 
                // eyeVec to be same as normalWorld
                s.eyeVec = s.normalWorld;

                // convert wpos to shadow space by multiply with _FromWorldToShadow
                float4 shadowPos = mul(_FromWorldToShadow, float4(s.posWorld, 1.0));
                shadowPos /= shadowPos.w;

                const half4 unlitColor = half4(0, 0, 0, 0);
#if defined(UNITY_REVERSED_Z)
                if (depth <= 0)
#else
                if (depth >= 1)
#endif                
                {
                    o.color = unlitColor;
                    o.depth = 0;
                    return o;
                }

#if defined(SUPPORT_SHADOWING)
                float shadow_atten = 1.0;
                // SHADOWING:
                if (_ShadowParameters.x > 0)
                {
                    float depLS = tex2D(_LightDepthMap, shadowPos.xy);
#if !UNITY_UV_STARTS_AT_TOP
                    depLS = depLS * 2.0 - 1.0;
#endif

#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
                    shadowPos.z = 1.0 - shadowPos.z;
#endif

                    if (depLS < shadowPos.z)
                    {
                        if (_LightShadowData.r <= 0.0)
                        {
                            // only early exit when shadow strenght is 1
                            o.color = unlitColor;
                            o.depth = depth;
                            return o;
                        }
                        else
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                        
                    }
                }
#endif // SUPPORT_SHADOWING

                // LIGHTING:
                float3 fragSpotLightColor = AnalyticSpotLightColor(_LightColorAtSource, s.posWorld, 
                    _LightWorldPos.xyz, _LightWorldDir.xyz, _LightParameters.z, 1.33);
            
                // Use ((r-d) / r)^2 as attenuation, fancier formula produce more accurate attenuation but
                // have visible shadows near the end of the lighting range.
                float r = 1.0 / _LightParameters.w;
                float tmp = saturate((r - length(fragToLight)) * _LightParameters.w);
                float atten = saturate(tmp * tmp);
                //HACK: Assign _LightColor0 to reuse AdditiveLight() in Unity standard shader library
                _LightColor0.rgb = fragSpotLightColor * _LightParameters.x;

#if UNITY_VERSION >= 550
                UnityLight light = AdditiveLight (normalize(fragToLight), atten);
#else
                UnityLight light = AdditiveLight (s.normalWorld, normalize(fragToLight), atten);
#endif                
                UnityIndirect noIndirect = ZeroIndirect ();

                half4 c;
#if UNITY_VERSION >= 550
                c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, s.eyeVec, light, noIndirect);
#else
                c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.normalWorld, s.eyeVec, light, noIndirect);
#endif                
                // to support light cookie
                float cookieMask = tex2D(_LightCookie, shadowPos.xy).a;
                c *= cookieMask;

                UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, s.posWorld)));
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass

#if defined(SUPPORT_SHADOWING)
                c.rgb *= shadow_atten;
#endif
                o.color = half4(c.rgb, s.alpha);
                o.depth = depth;

                return o;
            }

            ENDCG
        }

        Pass
        {
            // Additive lighting for DIRECTIONAL LIGHT in FORWARD renderer
            ZTest LEqual
            
            Cull Back
            Blend SrcAlpha One, One One
            ZWrite Off

            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile __ SUPPORT_SHADOWING
            #pragma multi_compile_fog
            #pragma fragment frag

            struct Output
            {
                half4 color : COLOR;
                float depth : DEPTH;
            };

            UNITY_DECLARE_TEX2D(_ScreenspaceHvrMaskTexture);

            Output frag(v2f i)
            {
                Output o;

                // Calculate the screen position of the pixel
                float2 screenPos = (i.screenPos.xy / i.screenPos.w);

                float depth;
                FragmentCommonData s = HvrFragmentSetup(i, screenPos, depth);
                float3 fragToLight = -_LightWorldDir.xyz;
                // HACK: normalWorld is hard to get right, be full lit
                s.normalWorld = -fragToLight;
                // HACK: remove the reflection appears in directional light by setting 
                // eyeVec to be same as normalWorld
                s.eyeVec = s.normalWorld;

                const half4 unlitColor = half4(0, 0, 0, 0);
                // Support 3D masking
#if defined(UNITY_REVERSED_Z)
                if (depth <= 0)
#else
                if (depth >= 1)
#endif                
                {
                    o.color = unlitColor;
                    o.depth = 0;
                    return o;
                }

                // LIGHTING:
                float3 fragDirectionalLightColor = _LightColorAtSource;
                // TODO: remove the arbitrary constant attenuation 
                float atten = 1.0; // constant attenuation, since the full lit will be too much intensity for overall
                // HACK: Assign _LightColor0 to reuse AdditiveLight in Unity standard shader library
                _LightColor0.rgb = fragDirectionalLightColor * _LightParameters.x;

#if UNITY_VERSION >= 550
                UnityLight light = AdditiveLight (_LightWorldDir.xyz, atten);
#else
                UnityLight light = AdditiveLight (s.normalWorld, _LightWorldDir.xyz, atten);
#endif                
                UnityIndirect noIndirect = ZeroIndirect ();

#if UNITY_VERSION >= 550
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, s.eyeVec, light, noIndirect);
#else
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.normalWorld, s.eyeVec, light, noIndirect);
#endif

#if defined(SUPPORT_SHADOWING)
                float shadow_atten = UNITY_SAMPLE_TEX2D(_ScreenspaceHvrMaskTexture, screenPos).r;
                c.rgb *= shadow_atten;
#endif

                UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, s.posWorld)));
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass

                o.color = half4(c.rgb, s.alpha);
                o.depth = depth;

                return o;
            }

            ENDCG


        }

        Pass
        {
            // Additive lighting for POINT LIGHT in FORWARD renderer
            ZTest LEqual
            
            Cull Back
            Blend SrcAlpha One, One One
            ZWrite Off
 
            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile __ SUPPORT_SHADOWING
            #pragma multi_compile_fog
            #pragma fragment frag

            struct Output
            {
                half4 color : COLOR;
                float depth : DEPTH;
            };

            Output frag(v2f i)
            {
                Output o;

                // Calculate the screen position of the pixel
                float2 screenPos = (i.screenPos.xy / i.screenPos.w);
                float depth;
                FragmentCommonData s = HvrFragmentSetup(i, screenPos, depth);

                float3 fragToLight = _LightWorldPos.xyz - s.posWorld;
                // HACK: normalWorld is hard to get right, be full lit
                s.normalWorld = normalize(fragToLight);
                // HACK: remove the reflection appears in point light by setting 
                // eyeVec to be same as normalWorld
                s.eyeVec = s.normalWorld;

                // Need to be very careful/explicit with OpenGL ES on phones
                float3 lightCookieIndex = (mul(_WorldToLightSpace, 
                    float4(-fragToLight.x, -fragToLight.y, -fragToLight.z, 0))).xyz;

#if defined(SUPPORT_SHADOWING)
                // SHADOWING:
                float shadow_atten = 1.0;

                if (_ShadowParameters.x > 0)
                {
                    float4 wpos = float4(s.posWorld, 1.0);

                    float linearDepth = length(fragToLight) * _LightParameters.w; // <- 1/range
                    // convert wpos to shadow space by multiply with _FromWorldToShadow
                    // +Z
                    float4 shadowPos = mul(_FromWorldToShadowPZ, wpos);
                    shadowPos /= shadowPos.w;
                    float2 uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    float depLS = tex2D(_LightDepthMapPZ, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -Z
                    shadowPos = mul(_FromWorldToShadowNZ, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    depLS = tex2D(_LightDepthMapNZ, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // +X
                    shadowPos = mul(_FromWorldToShadowPX, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    depLS = tex2D(_LightDepthMapPX, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -X
                    shadowPos = mul(_FromWorldToShadowNX, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif                    
                    depLS = tex2D(_LightDepthMapNX, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // // +Y
                    shadowPos = mul(_FromWorldToShadowPY, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif                    
                    depLS = tex2D(_LightDepthMapPY, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -Y
                    shadowPos = mul(_FromWorldToShadowNY, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif                    
                    depLS = tex2D(_LightDepthMapNY, uv.xy);
#if defined(UNITY_REVERSED_Z) && UNITY_VERSION < 560
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS < linearDepth)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }
                }
#endif // SUPPORT_SHADOWING

                // LIGHTING:
                float3 fragPointLightColor = _LightColorAtSource;
                float cookieMask = texCUBE(_LightCookieCube, lightCookieIndex).a;

                // Use ((r-d) / r)^2 as attenuation, fancier formula produce more accurate attenuation but
                // have visible shadows near the end of the lighting range.
                float r = 1.0 / _LightParameters.w;
                float tmp = saturate((r - length(fragToLight)) * _LightParameters.w);
                float atten = saturate(tmp * tmp);

                // HACK: Assign _LightColor0 to reuse AdditiveLight in Unity standard shader library
                _LightColor0.rgb = fragPointLightColor * _LightParameters.x;

#if UNITY_VERSION >= 550
                UnityLight light = AdditiveLight (normalize(fragToLight), atten);
#else
                UnityLight light = AdditiveLight (s.normalWorld, normalize(fragToLight), atten);
#endif                
                UnityIndirect noIndirect = ZeroIndirect ();

#if UNITY_VERSION >= 550
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, s.eyeVec, light, noIndirect);
#else
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.normalWorld, s.eyeVec, light, noIndirect);
#endif

                UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, s.posWorld)));
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass

                c *= cookieMask;

#if SUPPORT_SHADOWING
                c.rgb *= shadow_atten;
#endif                
                o.color = half4(c.rgb, s.alpha);
                o.depth = depth;

                return o;
            }

            ENDCG
        }

        Pass
        {
            // Additive lighting for POINT LIGHT in FORWARD renderer for 2017.3 onward renderers supports non-linear depth
            ZTest LEqual
            
            Cull Back
            Blend SrcAlpha One, One One
            ZWrite Off
 
            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile __ SUPPORT_SHADOWING
            #pragma multi_compile_fog
            #pragma fragment frag

            struct Output
            {
                half4 color : COLOR;
                float depth : DEPTH;
            };

            Output frag(v2f i)
            {
                Output o;

                // Calculate the screen position of the pixel
                float2 screenPos = (i.screenPos.xy / i.screenPos.w);
                float depth;
                FragmentCommonData s = HvrFragmentSetup(i, screenPos, depth);

                float3 fragToLight = _LightWorldPos.xyz - s.posWorld;
                // HACK: normalWorld is hard to get right, be full lit
                s.normalWorld = normalize(fragToLight);
                // HACK: remove the reflection appears in point light by setting 
                // eyeVec to be same as normalWorld
                s.eyeVec = s.normalWorld;

                // Need to be very careful/explicit with OpenGL ES on phones
                float3 lightCookieIndex = (mul(_WorldToLightSpace, 
                    float4(-fragToLight.x, -fragToLight.y, -fragToLight.z, 0))).xyz;

#if defined(SUPPORT_SHADOWING)
                // SHADOWING:
                float shadow_atten = 1.0;

                if (_ShadowParameters.x > 0)
                {
                    float4 wpos = float4(s.posWorld, 1.0);

                    float linearDepth = length(fragToLight) * _LightParameters.w; // <- 1/range
                    // convert wpos to shadow space by multiply with _FromWorldToShadow
                    // +Z
                    float4 shadowPos = mul(_FromWorldToShadowPZ, wpos);
                    shadowPos /= shadowPos.w;
                    float2 uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    float depLS = tex2D(_LightDepthMapPZ, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif

                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -Z
                    shadowPos = mul(_FromWorldToShadowNZ, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    depLS = tex2D(_LightDepthMapNZ, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // +X
                    shadowPos = mul(_FromWorldToShadowPX, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif
                    depLS = tex2D(_LightDepthMapPX, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -X
                    shadowPos = mul(_FromWorldToShadowNX, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif                    
                    depLS = tex2D(_LightDepthMapNX, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // // +Y
                    shadowPos = mul(_FromWorldToShadowPY, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif                    
                    depLS = tex2D(_LightDepthMapPY, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif
                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }

                    // -Y
                    shadowPos = mul(_FromWorldToShadowNY, wpos);
                    shadowPos /= shadowPos.w;
                    uv = shadowPos.xy;
#if defined(UNITY_UV_STARTS_AT_TOP)
                    uv.y = 1 - uv.y;
#endif

                    depLS = tex2D(_LightDepthMapNY, uv.xy);
#if defined(UNITY_REVERSED_Z)
                    depLS = 1.0 - depLS;
#endif

                    if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 && 
                        shadowPos.z >= 0)
                    {
                        // compare depth
                        if (depLS > shadowPos.z)
                        {
                            shadow_atten = _LightShadowData.r;
                        }
                    }
                }
#endif // SUPPORT_SHADOWING

                // LIGHTING:
                float3 fragPointLightColor = _LightColorAtSource;
                float cookieMask = texCUBE(_LightCookieCube, lightCookieIndex).a;

                // Use ((r-d) / r)^2 as attenuation, fancier formula produce more accurate attenuation but
                // have visible shadows near the end of the lighting range.
                float r = 1.0 / _LightParameters.w;
                float tmp = saturate((r - length(fragToLight)) * _LightParameters.w);
                float atten = saturate(tmp * tmp);

                // HACK: Assign _LightColor0 to reuse AdditiveLight in Unity standard shader library
                _LightColor0.rgb = fragPointLightColor * _LightParameters.x;

#if UNITY_VERSION >= 550
                UnityLight light = AdditiveLight (normalize(fragToLight), atten);
#else
                UnityLight light = AdditiveLight (s.normalWorld, normalize(fragToLight), atten);
#endif                
                UnityIndirect noIndirect = ZeroIndirect ();

#if UNITY_VERSION >= 550
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, s.eyeVec, light, noIndirect);
#else
                half4 c = UNITY_BRDF_PBS (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, s.normalWorld, s.eyeVec, light, noIndirect);
#endif

                UNITY_TRANSFER_FOG(i, UnityObjectToClipPos(mul(unity_WorldToObject, s.posWorld)));
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass

                c *= cookieMask;

#if SUPPORT_SHADOWING
                c.rgb *= shadow_atten;
#endif                
                o.color = half4(c.rgb, s.alpha);
                o.depth = depth;

                return o;
            }

            ENDCG
        }

        Pass
        {
            // Legacy full-lit color pass
            ZTest Less
            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha, One One
            ZWrite[_ZWrite]

            CGPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma fragment fragLegacyBase

            ENDCG
        }
    }
    Fallback off

    CustomEditor "ShaderGUI_HvrStandard"
}
