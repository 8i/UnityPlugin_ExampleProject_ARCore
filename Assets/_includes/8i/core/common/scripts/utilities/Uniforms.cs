using UnityEngine;

namespace HVR
{
    public class Uniforms
    {
        public class ComponentNames
        {
            // https://snowhydra.wordpress.com/2015/06/01/unity-performance-testing-getcomponent-fields-tags/
            public string hvrActor = "";
            public string hvrColorGrading = "";
            public string hvrRender = "";
            public string hvrLight = "";
            public string HvrActor3DMask = "";

            public ComponentNames()
            {
                hvrActor = typeof(HvrActor).ToString();
                hvrRender = typeof(HvrRender).ToString();
                hvrColorGrading = typeof(HvrActorColorGrade).ToString();
                hvrLight = typeof(HvrLight).ToString();
                HvrActor3DMask = typeof(HvrActor3DMask).ToString();
            }
        }

        public class ResourcePaths
        {
            internal static readonly string shader_HVRRender_CommandBufferDepthComposite = "8i/shaders/HVRRender_CommandBufferDepthComposite";
            internal static readonly string shader_HVRRender_SinglePassComposite = "8i/shaders/HVRRender_SinglePassComposite";
            internal static readonly string shader_Blit_Hvr3DMask = "8i/shaders/Blit-Hvr3DMask";
            internal static readonly string shader_HVRStandard = "8i/shaders/HVRStandard";
            internal static readonly string shader_HVRShadowCollect = "8i/shaders/HVRShadowCollect";
            internal static readonly string shader_HVRShadowDepthCopy = "8i/shaders/Blit-HvrShadowDepthCopy";
            internal static readonly string shader_HVRShadowDepthLinearizeCopy = "8i/shaders/Blit-HvrShadowLinearDepthOutput";
            internal static readonly string shader_HVRShadowMapParamSniffer = "8i/shaders/Blit-HvrShadowParamSniffer";
            internal static readonly string shader_HVRColorComposite = "8i/shaders/HVRColorComposite";
            internal static readonly string shader_HVRScreenspaceShadowMask = "8i/shaders/HVRScreenspaceShadowMask";
        }

        public class ShaderNames
        {
            public static readonly string shader_HvrStandard = "8i/HvrStandard";
        }

        public class Keys
        {
            public static readonly string SCENEPLAYER_NAME = "8I_HVRSCENE_PLAYER";
            public static readonly string SCENEPLAYER_GRAPHICS_DEVICE_TYPE = "8I_HVRSCENE_GRAPHICS_DEVICE_TYPE";
        }

        public class ShaderProperties
        {
            // https://github.com/kode80/kode80CloudsUnity3D/pull/9
            // Every time you do a material.SetXXX(string, value); it will internally compute a hash for the name and do a lookup.
            // Considering the amount of uniforms sent on every frame in this asset... It can quickly become expensive on slower CPUs.
            // So no need to do it on every frame, it should only be done once and reused :)

            internal static readonly int _ErrorBias = Shader.PropertyToID("_ErrorBias");
            internal static readonly int _HvrColorTex = Shader.PropertyToID("_HvrColorTex");
            internal static readonly int _HvrDepthTex = Shader.PropertyToID("_HvrDepthTex");
            internal static readonly int _HVRLightShadowData = Shader.PropertyToID("_HVRLightShadowData");
            internal static readonly int _HVRMapTexelSize = Shader.PropertyToID("_HVRMapTexelSize");
            internal static readonly int _LightDirectionAngle = Shader.PropertyToID("_LightDirectionAngle");
            internal static readonly int _LightPosRange = Shader.PropertyToID("_LightPosRange");
            internal static readonly int _LSViewProject = Shader.PropertyToID("_LSViewProject");
            internal static readonly int _mask_box_matrix = Shader.PropertyToID("_mask_box_matrix");
            internal static readonly int _mask_length = Shader.PropertyToID("_mask_length");
            internal static readonly int _mask_sphere_center = Shader.PropertyToID("_mask_sphere_center");
            internal static readonly int _mask_sphere_radius = Shader.PropertyToID("_mask_sphere_radius");
            internal static readonly int _mask_types = Shader.PropertyToID("_mask_types");
            internal static readonly int _mask_additive = Shader.PropertyToID("_mask_additive");
            internal static readonly int _matLSViewProject = Shader.PropertyToID("_matLSViewProject");
            internal static readonly int _matModel = Shader.PropertyToID("_matModel");
            internal static readonly int _matShadowSplitSphere = Shader.PropertyToID("_matShadowSplitSphere");
            internal static readonly int _matWorld2Shadow = Shader.PropertyToID("_matWorld2Shadow");
            internal static readonly int _oDEP = Shader.PropertyToID("_oDEP");
            internal static readonly int _oLSDEP = Shader.PropertyToID("_oLSDEP");
            internal static readonly int _oLSDEPCUBE = Shader.PropertyToID("_oLSDEPCUBE");
            internal static readonly int _Projection = Shader.PropertyToID("_Projection");
            internal static readonly int _ProjectionInverse = Shader.PropertyToID("_ProjectionInverse");
            internal static readonly int _ScreenSpaceShadowTex = Shader.PropertyToID("_ScreenSpaceShadowTex");
            internal static readonly int _texLSDepth = Shader.PropertyToID("_texLSDepth");
            internal static readonly int UNITY_MATRIX_VP = Shader.PropertyToID("UNITY_MATRIX_VP");
            internal static readonly int _vecShadowSplitSqRadii = Shader.PropertyToID("_vecShadowSplitSqRadii");
            internal static readonly int _ViewProjectInverse = Shader.PropertyToID("_ViewProjectInverse");
            internal static readonly int _yBias = Shader.PropertyToID("_yBias");
            internal static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            internal static readonly int _ViewportClip = Shader.PropertyToID("_ViewportClip");
            internal static readonly int _CurrLightShadowBias = Shader.PropertyToID("_CurrLightShadowBias");
            internal static readonly int _CurrLightPositionRange = Shader.PropertyToID("_CurrLightPositionRange");
            internal static readonly int _ShadowBiasData = Shader.PropertyToID("_ShadowBiasData");
            internal static readonly int _FromWorldToShadow = Shader.PropertyToID("_FromWorldToShadow");
            internal static readonly int _LightDepthMap = Shader.PropertyToID("_LightDepthMap");
            internal static readonly int _LightWorldDir = Shader.PropertyToID("_LightWorldDir");
            internal static readonly int _LightWorldPos = Shader.PropertyToID("_LightWorldPos");
            internal static readonly int _LightParameters = Shader.PropertyToID("_LightParameters");
            internal static readonly int _ShadowParameters = Shader.PropertyToID("_ShadowParameters");
            internal static readonly int _LightCookie = Shader.PropertyToID("_LightCookie");
            internal static readonly int _LightCookieCube = Shader.PropertyToID("_LightCookieCube");
            internal static readonly int _WorldToLightSpace = Shader.PropertyToID("_WorldToLightSpace");
            internal static readonly int _FromWorldToShadow0 = Shader.PropertyToID("_FromWorldToShadow0");
            internal static readonly int _FromWorldToShadow1 = Shader.PropertyToID("_FromWorldToShadow1");
            internal static readonly int _FromWorldToShadow2 = Shader.PropertyToID("_FromWorldToShadow2");
            internal static readonly int _FromWorldToShadow3 = Shader.PropertyToID("_FromWorldToShadow3");
            internal static readonly int _ViewportData0 = Shader.PropertyToID("_ViewportData0");
            internal static readonly int _ViewportData1 = Shader.PropertyToID("_ViewportData1");
            internal static readonly int _ViewportData2 = Shader.PropertyToID("_ViewportData2");
            internal static readonly int _ViewportData3 = Shader.PropertyToID("_ViewportData3");
            internal static readonly int _FromWorldToShadowPX = Shader.PropertyToID("_FromWorldToShadowPX");
            internal static readonly int _FromWorldToShadowNX = Shader.PropertyToID("_FromWorldToShadowNX");
            internal static readonly int _FromWorldToShadowPY = Shader.PropertyToID("_FromWorldToShadowPY");
            internal static readonly int _FromWorldToShadowNY = Shader.PropertyToID("_FromWorldToShadowNY");
            internal static readonly int _FromWorldToShadowPZ = Shader.PropertyToID("_FromWorldToShadowPZ");
            internal static readonly int _FromWorldToShadowNZ = Shader.PropertyToID("_FromWorldToShadowNZ");
            internal static readonly int _LightDepthMapPX = Shader.PropertyToID("_LightDepthMapPX");
            internal static readonly int _LightDepthMapNX = Shader.PropertyToID("_LightDepthMapNX");
            internal static readonly int _LightDepthMapPY = Shader.PropertyToID("_LightDepthMapPY");
            internal static readonly int _LightDepthMapNY = Shader.PropertyToID("_LightDepthMapNY");
            internal static readonly int _LightDepthMapPZ = Shader.PropertyToID("_LightDepthMapPZ");
            internal static readonly int _LightDepthMapNZ = Shader.PropertyToID("_LightDepthMapNZ");
            internal static readonly int _CenterWorldPos = Shader.PropertyToID("_CenterWorldPos");
            internal static readonly int _CameraWorldPos = Shader.PropertyToID("_CameraWorldPos");
            internal static readonly int _WorldToObject = Shader.PropertyToID("_WorldToObject");
            internal static readonly int _LightColorAtSource = Shader.PropertyToID("_LightColorAtSource");
            internal static readonly int _SHFactorL2Red = Shader.PropertyToID("_SHFactorL2Red");
            internal static readonly int _SHFactorL2Green = Shader.PropertyToID("_SHFactorL2Green");
            internal static readonly int _SHFactorL2Blue = Shader.PropertyToID("_SHFactorL2Blue");
            internal static readonly int _FlipMapMatrix = Shader.PropertyToID("_FlipMapMatrix");
            internal static readonly int _MainColor = Shader.PropertyToID("_MainColor");
            internal static readonly int _ColorFactor = Shader.PropertyToID("_ColorFactor");
            internal static readonly int _Tint = Shader.PropertyToID("_Tint");
            internal static readonly int _ScreenspaceMaskTexture = Shader.PropertyToID("_ScreenspaceMaskTexture");
            internal static readonly int _ScreenspaceHvrMaskTexture = Shader.PropertyToID("_ScreenspaceHvrMaskTexture");
            internal static readonly int _ShadowFadeCameraDistance = Shader.PropertyToID("_ShadowFadeCameraDistance");

            // Unity built-in shader variable slot goes here. These slots are useful when injecting 8i's data
            // in command buffers. Note the naming convetion doesn't necessarily follows 8i's.
            internal static readonly int _Builtin_ShadowMapTexture = Shader.PropertyToID("_ShadowMapTexture");
            internal static readonly int _Builtin_ShadowMapTexture_TexelSize = Shader.PropertyToID("_ShadowMapTexture_TexelSize");
            internal static readonly int _Builtin_unity_WorldToShadow = Shader.PropertyToID("unity_WorldToShadow");
            internal static readonly int _Builtin_unity_ShadowSplitSpheres = Shader.PropertyToID("unity_ShadowSplitSpheres");
            internal static readonly int _Builtin_unity_ShadowSplitSqRadii = Shader.PropertyToID("unity_ShadowSplitSqRadii");
        }

        public static readonly string buildDataPath = "hvrdata";
        public static ComponentNames componentNames
        {
            get
            {
                if (m_componentNames == null)
                    m_componentNames = new ComponentNames();
                
                return m_componentNames;
            }
        }

        private static ComponentNames m_componentNames;
    }
}
