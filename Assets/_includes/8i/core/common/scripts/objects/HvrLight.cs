using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using HVR.Interface;
using HVR.Utils;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#else
using UnityEngine.VR;
#endif

namespace HVR
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("8i/HvrLight")]
    public class HvrLight : MonoBehaviour
    {
        /////////////////////////////////////////////////////////
        // Enums and constants
        public enum ShadowQuality
        {
            Unknown = -1,
            Low = 0,
            Medium,
            High,
            VeryHigh
        }

        // FIXME: Some Android devices using encoded ARGB for depth. RHalf or RFloat just doesn't work.
#if UNITY_IOS && !UNITY_EDITOR
        const RenderTextureFormat CubemapDepthRenderTextureFormat = RenderTextureFormat.RHalf;
#else
#if UNITY_2018_1_OR_NEWER
        const RenderTextureFormat CubemapDepthRenderTextureFormat = RenderTextureFormat.Depth;
#else
        const RenderTextureFormat CubemapDepthRenderTextureFormat = RenderTextureFormat.RFloat;
#endif
#endif

        struct ActorShadowMapKey
        {
            public HvrActor actor;
            public CubemapFace face;
            public int width;
            public int height;

            public ActorShadowMapKey(HvrActor actor, CubemapFace face, int faceWidth, int faceHeight)
            {
                this.actor = actor;
                this.face = face;
                this.width = faceWidth;
                this.height = faceHeight;
            }

            public override int GetHashCode()
            {
                return actor.GetHashCode() + width * 179 + height * 607 + (int)face * 929;
            }
        }

        IDictionary<ActorShadowMapKey, RenderTexture> pointLightActorShadowMapPool = new Dictionary<ActorShadowMapKey, RenderTexture>();

        RenderTexture RequestShadowMap(HvrActor actor, CubemapFace face, int faceWidth, int faceHeight)
        {
            ActorShadowMapKey key = new ActorShadowMapKey(actor, face, faceWidth, faceHeight);
            if (pointLightActorShadowMapPool.ContainsKey(key))
            {
                return pointLightActorShadowMapPool[key];
            }
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(faceWidth, faceHeight, 16, Helper.GetSupportedRenderTextureFormatForDepthBlit());
                rt.name = "Separated temp shadowmap(RFloat/RHalf) for transparency";
                rt.Create();
                pointLightActorShadowMapPool[key] = rt;
                return rt;
            }
        }

        void CleanupShadowMapPool()
        {
            // IMPORTANT: Before calling this function, make sure no one is referencing the temp render texture. Need especially care for 
            // CommandBuffer in Editor as they're executed somewhere else and don't have a callback to tell whether it's finished or not.
            foreach (KeyValuePair<ActorShadowMapKey, RenderTexture> pair in pointLightActorShadowMapPool)
            {
                RenderTexture rt = pair.Value;
                RenderTexture.ReleaseTemporary(rt);
            }

            pointLightActorShadowMapPool.Clear();

        }

        class ActorShadowMapPair
        {
            public HvrActor actor;
            public RenderTexture shadowmap;

            public ActorShadowMapPair(HvrActor actor)
            {
                this.actor = actor;
                this.shadowmap = null;
            }
        }

        // Cascaded shadowmap rect in screen space
        static readonly Vector4[] CASCADED4_SHADOWMAP_RECT_SS = new Vector4[4] {
            new Vector4(0.0f, 0.5f, 0.5f, 1.0f),
            new Vector4(0.5f, 0.5f, 1.0f, 1.0f),
            new Vector4(0.0f, 0.0f, 0.5f, 0.5f),
            new Vector4(0.5f, 0.0f, 1.0f, 0.5f),
        };

        static readonly Vector4[] CASCADED2_SHADOWMAP_RECT_SS = new Vector4[4] {
            new Vector4(0.0f, 0.0f, 0.5f, 1.0f),
            new Vector4(0.5f, 0.0f, 1.0f, 1.0f),
            Vector4.zero,
            Vector4.zero
        };

        static readonly Vector4[] CASCADED1_SHADOWMAP_RECT_SS = new Vector4[4] {
            new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            Vector4.zero,
            Vector4.zero,
            Vector4.zero
        };

        static readonly int[] BASE_RESOLUTION = new int[]
        {
            256, 512, 1024, 2048
        };

        static readonly int[] SINGLE_RESOLUTION = new int[]
        {
            128, 256, 512, 1024
        };

        static readonly int[][] SHADOWMAP_RESOLUTION_FOR_POINTLIGHT = new int[][]
        {
            new int[] {16, 32, 64, 128, 256},
            new int[] {32, 64, 128, 256, 512},
            new int[] {64, 128, 256, 512, 1024},
            new int[] {128, 256, 512, 1024, 1024}
        };

        static readonly int[] SHADOWMAP_RESOLUTION_FOR_DIRECTIONALLIGHT = new int[]
       {
            1024, 2048, 4096, 4096
       };

        const int MAX_VIEWPORT_NUM = 2;

        const int MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM = 4;

        readonly Matrix4x4 toUVSpace =
                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                    Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one);

        ////////////////////////////////////////////////////////////
        // depedent object
        Light m_DependentLight;

        HvrViewportSwapChain m_spotlightSwapChain;
        HvrViewportSwapChain[] m_directionalLightSwapChain = new HvrViewportSwapChain[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];

        Dictionary<CubemapFace, ViewportSavingData> cubemapViewportSaving = new Dictionary<CubemapFace, ViewportSavingData>();

        ////////////////////////////////////////////////////////////
        // rendering related resources
        RenderTexture m_DepthRT, m_DepthWithSceneRT;
#if UNITY_2017_1_OR_NEWER        
        RenderTexture m_ShadowmapRT; // only for directional
        RenderTexture m_screenspaceHvrShadowMaskTexture;
#endif

        CommandBuffer m_CommandBufferBakeSM;
        CommandBuffer m_CommandBufferCopyDepth;
#if UNITY_2017_1_OR_NEWER        
        CommandBuffer m_CommandBufferScreenspaceShadowMask;
#endif        
        Material m_CasterMaterial;
        Mesh m_CasterMesh;

        // The reason why we need a second material/shader to render point light shadow is, it seems Unity got confused when using CommandBuffer
        // and normal Graphics.Blit together, leaving the states in shader incorrect.
        Material m_LinearDepthMaterial;
        // Point light only
        class ViewportRecord
        {
            public RenderTexture depth;
            public RenderTexture linearDepth;
            public HvrViewportSwapChain swapchain;
            public Quaternion rotation;
            public bool hasTransparentActor;

#if UNITY_5_6_OR_NEWER
            public CommandBuffer bakeShadowMapCB;
            public CommandBuffer restoreShadowMapCB;
            public CommandBuffer restoreShadowMapCB2; // Need a second command buffer just to keep the current shadowmap target in point light rendering
#endif
        }

        struct ViewportSavingData
        {
            public ViewportSavingData(ViewportInterface viewport, Matrix4x4 inverseViewProjection)
            {
                this.viewport = viewport;
                this.inverseViewProjection = inverseViewProjection;
                this.actorList = null;
            }

            public ViewportInterface viewport;
            public Matrix4x4 inverseViewProjection;
            public ICollection<ActorShadowMapPair> actorList;
        }
#if UNITY_2017_1_OR_NEWER
        ViewportInterface[] directionalLightViewport = new ViewportInterface[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];
#endif        
        ViewportInterface spotLightViewport;
        Dictionary<CubemapFace, ViewportRecord> m_PointLightCubemapBookkeep = new Dictionary<CubemapFace, ViewportRecord>();

        // book keeping stuff
        private bool m_enabled = false;
        private bool m_CommandBufferAdded;
#if !UNITY_EDITOR
        private bool m_renderOnceToClear = false;
#endif        
        private ShadowQuality m_CachedShadowQuality = ShadowQuality.Unknown;
        private LightShadows m_CachedShadowType = LightShadows.None;
        private LightType m_CachedLightType = LightType.Area;
        private int m_CachedScreenWidth = -1;
        private int m_CachedScreenHeight = -1;
        private int m_CachedSceneViewWidth = -1;
        private int m_CachedSceneViewHeight = -1;
        private float m_CachedCameraFarPlane = 0.0f;
        private float m_CachedCameraFOV = 0.0f;
        private bool m_PointLightUsingNonlinearDepth = false;
        // will be used only if Unity uses non-linear depth map, it's 2017.3 onwards with D3D
        // will also be used in cubemap restoring command buffer, as after 2017.3 Unity issues warning over using temp render texture across command buffers
        private RenderTexture m_CubemapWorkingBuffer;

#if UNITY_2017_1_OR_NEWER
        // Directional light only
        Material m_ScreenspaceShadowMask;
        Mesh m_ScreenQuadForMask;

        BoundingSphere[] m_CachedBoundingSphere = new BoundingSphere[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];
        Matrix4x4[] m_CachedDirectionalLightViewProjectionMatrix = new Matrix4x4[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];

        ICollection<ActorShadowMapPair>[] directionalLightCachedActors = new List<ActorShadowMapPair>[4];
#endif
        // spot light only
        ICollection<ActorShadowMapPair> spotLightCachedActors;



#if UNITY_EDITOR
        WindowInfos unityWindowInfos;
#endif        

        [SerializeField, HideInInspector]
        private ShadowQuality m_ShadowQuality = ShadowQuality.High;

        [SerializeField, HideInInspector]
        private bool m_lightIsRealtime = true;

        [SerializeField, HideInInspector]
        private float m_directionalLightShadowDistance = 10.0f;

        // Public interfaces
        //////////////////////////////////////////////////////////////////////////////

        [ExposeProperty]
        public ShadowQuality ShadowResolution
        {
            get
            {
                _EnsureDependents();

                m_ShadowQuality = ShadowQuality.High;

                switch (m_DependentLight.shadowResolution)
                {
                    case LightShadowResolution.Low:
                        m_ShadowQuality = ShadowQuality.Low;
                        break;
                    case LightShadowResolution.Medium:
                        m_ShadowQuality = ShadowQuality.Medium;
                        break;
                    case LightShadowResolution.High:
                        m_ShadowQuality = ShadowQuality.High;
                        break;
                    case LightShadowResolution.VeryHigh:
                        m_ShadowQuality = ShadowQuality.VeryHigh;
                        break;
                    default:
                        switch (QualitySettings.shadowResolution)
                        {
                            case UnityEngine.ShadowResolution.Low:
                                m_ShadowQuality = ShadowQuality.Low;
                                break;
                            case UnityEngine.ShadowResolution.Medium:
                                m_ShadowQuality = ShadowQuality.Medium;
                                break;
                            case UnityEngine.ShadowResolution.High:
                                m_ShadowQuality = ShadowQuality.High;
                                break;
                            case UnityEngine.ShadowResolution.VeryHigh:
                                m_ShadowQuality = ShadowQuality.VeryHigh;
                                break;
                        }
                        break;

                }

                return m_ShadowQuality;
            }
        }

        public float ShadowDistanceLimit
        {
            get
            {
                return m_directionalLightShadowDistance;
            }

            set
            {
                m_directionalLightShadowDistance = value;
            }
        }

#if UNITY_2017_1_OR_NEWER
        public RenderTexture ScreenspaceHvrShadowMaskTexture
        {
            get
            {
                return m_screenspaceHvrShadowMaskTexture;
            }
        }
#endif

        internal Light GetDependentLight()
        {
            _EnsureDependents();
            return m_DependentLight;
        }

        public bool IsAttachedToRealtimeLight()
        {
            return m_lightIsRealtime;
        }

        public RenderTexture GetLightspaceDepthTexture()
        {
            return m_DepthWithSceneRT;
        }

        public RenderTexture GetLightspaceCubemappedLinearDepthTexture(CubemapFace face)
        {
            if (!m_enabled)
                return null;

            ViewportRecord record = m_PointLightCubemapBookkeep[face];
            return record.linearDepth;
        }

        public RenderTexture GetLightspaceCubemappedDepthTexture(CubemapFace face)
        {
            if (!m_enabled)
                return null;

            ViewportRecord record = m_PointLightCubemapBookkeep[face];
            return record.depth;
        }

        public Matrix4x4 GetWorldToShadowMatrix()
        {
            _EnsureDependents();

            // should be only working with spot light.
            LightType lightType = m_DependentLight.type;
            if (lightType == LightType.Spot)
            {
                // NOTE: we can't rely on sniffer because the unity_WorldToShadow[0-3] is the same across _all_ the spot lights
                Matrix4x4 view = m_DependentLight.transform.worldToLocalMatrix;
                Matrix4x4 invertZ = Matrix4x4.Scale(new Vector3(1, 1, -1));
                view = invertZ * view; // apply Unity convention

                Matrix4x4 proj = Matrix4x4.Perspective(m_DependentLight.spotAngle, 1.0f, m_DependentLight.shadowNearPlane, m_DependentLight.range);
                Matrix4x4 projGPU = GL.GetGPUProjectionMatrix(proj, false);

                return (toUVSpace * projGPU * view);
            }
            else
                return Matrix4x4.identity;
        }

#if UNITY_5_6_OR_NEWER
        // Useful when dealing with CommandBuffer.CopyTexture
        Matrix4x4 GetFlipMapMatrixForCubemapFace(CubemapFace face)
        {
            if (Helper.GetRenderingAPI().StartsWith("OpenGL"))
            {
#if (UNITY_EDITOR_WIN) && (UNITY_ANDROID) && (!UNITY_2017_3_OR_NEWER)
                // Android + (Win?) Editor needs special care, Android device is okay.
                return Matrix4x4.identity;
#else                
                // doing a (Y = 1 - Y);
                return Matrix4x4.TRS(new Vector3(0, 1, 0), Quaternion.identity, new Vector3(1.0f, -1.0f, 1.0f));
#endif                
            }
            else
            {
                return Matrix4x4.identity;
            }

        }
#endif

        public Matrix4x4 GetCubemappedWorldToShadowMatrix(CubemapFace face)
        {
            if (!m_enabled)
                return Matrix4x4.identity;

            ViewportRecord record = m_PointLightCubemapBookkeep[face];
            Matrix4x4 view = Matrix4x4.TRS(m_DependentLight.transform.position, record.rotation, Vector3.one);
            view = view.inverse; // from world to view

            // NOTE: From Unity script API Camera.worldToCameraMatrix,
            // "Note that camera space matches OpenGL convention: camera's forward is the negative Z axis. This is different from Unity's convention, where forward is the positive Z axis."
            Matrix4x4 invertZ = Matrix4x4.Scale(new Vector3(1, 1, -1));
            view = invertZ * view; // apply Unity convention

            Matrix4x4 proj = Matrix4x4.Perspective(90.0f, 1.0f, m_DependentLight.shadowNearPlane, m_DependentLight.range);
            Matrix4x4 projGPU = GL.GetGPUProjectionMatrix(proj, false);

            return toUVSpace * projGPU * view;
        }

        public static Vector4[] GetAllCascadedShadowmapRect()
        {
            int shadowSplit = QualitySettings.shadowCascades;
            switch (shadowSplit)
            {
                case 1:
                    return CASCADED1_SHADOWMAP_RECT_SS;

                case 2:
                    return CASCADED2_SHADOWMAP_RECT_SS;

                default:
                    return CASCADED4_SHADOWMAP_RECT_SS;
            }
        }

        public void IterateAllCubemapFaces(System.Func<CubemapFace, int> lambda)
        {
            if (!m_enabled)
                return;

            foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
            {
                lambda(pair.Key);
            }
        }

        // Unity message handler
        //////////////////////////////////////////////////////////////////////////////

        private void OnEnable()
        {
            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;
#if UNITY_EDITOR
            // In the case that this component is a prefab, don't allow an asset to be created
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;
#endif

            m_PointLightUsingNonlinearDepth = false;
#if UNITY_2017_3_OR_NEWER
            if (Helper.GetRenderingAPI().StartsWith("Direct"))
            {
                m_PointLightUsingNonlinearDepth = true;
            }
#endif

            _EnsureDependents();

            m_CommandBufferAdded = false;
#if !UNITY_EDITOR
            m_renderOnceToClear = false;
#endif            
            m_CachedShadowType = m_DependentLight.shadows;
            m_CachedShadowQuality = this.ShadowResolution;
            m_CachedLightType = m_DependentLight.type;
#if UNITY_EDITOR
            m_lightIsRealtime = EditorHelper.IsLightRealtime(m_DependentLight);
            unityWindowInfos = new WindowInfos();
#endif

            _CheckResources();

            HvrScene.Add(this);

            m_enabled = true;
        }

        public void Update()
        {
#if UNITY_EDITOR
            // In the case that this component is a prefab, don't allow an asset to be created
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;
#endif
            if (!m_enabled)
                return;

            // Call here in order to catch case where the user has changed the light settings
            if (_CheckResources())
            {
                _ClearAllCommandBuffers();
                return;
            }
        }

        private void OnDisable()
        {
            if (m_enabled)
            {
                m_enabled = false;

                HvrScene.Remove(this);

                _DestroyResources();
            }
        }

        private void OnDestroy()
        {
            HvrScene.Remove(this);

            _DestroyResources();
        }

        private void OnApplicationQuit()
        {
            HvrScene.Remove(this);

            _DestroyResources();
        }

        public void Render()
        {
            if (!m_enabled)
                return;

#if !UNITY_EDITOR                
            m_renderOnceToClear = false;
#endif
            _DoRenderFromLight();
        }

        public void RenderOnce()
        {
            if (!m_enabled)
                return;

#if !UNITY_EDITOR
            if (!m_renderOnceToClear) // save performance by clearing only once in standalone build
#endif
            {
                _DoRenderFromLight();
#if !UNITY_EDITOR
                m_renderOnceToClear = true;
#endif
            }
        }

        // Resource management
        //////////////////////////////////////////////////////////////////////////////

        bool _CheckResources()
        {
#if UNITY_EDITOR
            // There are issues with releasing unmanaged memory while running in batch mode for OSX and iOS
            // TODO: Remove this check
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return false;
#endif

#if UNITY_EDITOR   
            EditorWindow gameView = EditorHelper.GetMainGameView();
#endif

            bool recreatedResources = false;

            Camera cam = CameraHelper.GetMainCamera();

            // Check if any settings have changed that require this component's resources 
            // to be released and recreated
            if (m_CachedLightType != m_DependentLight.type ||
                m_CachedShadowQuality != this.ShadowResolution ||
                m_CachedShadowType != m_DependentLight.shadows ||
                cam == null ||
                m_CachedCameraFarPlane != cam.farClipPlane ||
                m_CachedCameraFOV != cam.fieldOfView ||
#if UNITY_EDITOR
                m_lightIsRealtime != EditorHelper.IsLightRealtime(m_DependentLight) ||
                m_CachedSceneViewWidth != (int)unityWindowInfos.scene.position.width ||
                m_CachedSceneViewHeight != (int)unityWindowInfos.scene.position.height ||
                (
                    gameView != null &&
                    (
                        m_CachedScreenWidth != gameView.position.width ||
                        m_CachedScreenHeight != gameView.position.height
                    )
                ) ||
#else
                m_CachedScreenWidth != Screen.width ||
                m_CachedScreenHeight != Screen.height ||
#endif
                m_spotlightSwapChain == null)
            {
                m_CachedLightType = m_DependentLight.type;
                m_CachedShadowQuality = this.ShadowResolution;
                m_CachedShadowType = m_DependentLight.shadows;
                m_CachedCameraFarPlane = cam != null ? cam.farClipPlane : 0;
                m_CachedCameraFOV = cam != null ? cam.fieldOfView : 0;

#if UNITY_EDITOR
                m_CachedSceneViewWidth = (int)unityWindowInfos.scene.position.width;
                m_CachedSceneViewHeight = (int)unityWindowInfos.scene.position.height;

                if (gameView != null)
                {
                    m_CachedScreenWidth = (int)gameView.position.width;
                    m_CachedScreenHeight = (int)gameView.position.height;
                }
                else
                {
                    m_CachedScreenWidth = -1;
                    m_CachedScreenHeight = -1;
                }
#else
                m_CachedScreenWidth = Screen.width;
                m_CachedScreenHeight = Screen.height;
#endif

#if UNITY_EDITOR
                m_lightIsRealtime = EditorHelper.IsLightRealtime(m_DependentLight);
#endif
                _DestroyResources();

                recreatedResources = true;
            }

            Shader casterShader = Resources.Load(Uniforms.ResourcePaths.shader_HVRShadowDepthCopy) as Shader;
            if (m_CasterMaterial == null)
            {
                m_CasterMaterial = new Material(casterShader);
                m_CasterMaterial.hideFlags = HideFlags.DontSave;
            }

            if (m_LinearDepthMaterial == null)
            {
                m_LinearDepthMaterial = new Material(Resources.Load(Uniforms.ResourcePaths.shader_HVRShadowDepthLinearizeCopy) as Shader);
                m_LinearDepthMaterial.hideFlags = HideFlags.DontSave;
            }

#if UNITY_2017_1_OR_NEWER
            if (m_ScreenspaceShadowMask == null)
            {
                // Can't use built-in "Hidden/Internal-ScreenSpaceShadows" directly because we need to change the blend mode to 'multiply'
                // However there's no way changing them in runtime/through APIs
                Shader screenSpaceShadowMaskShader = Resources.Load(Uniforms.ResourcePaths.shader_HVRScreenspaceShadowMask) as Shader;

                m_ScreenspaceShadowMask = new Material(screenSpaceShadowMaskShader);
                m_ScreenspaceShadowMask.hideFlags = HideFlags.DontSave;
            }
#endif            

            if (m_CasterMesh == null)
            {
                m_CasterMesh = CompositeBufferUtils.GenerateQuad();
                m_CasterMesh.hideFlags = HideFlags.HideAndDontSave;
            }

            if (m_spotlightSwapChain == null)
                m_spotlightSwapChain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);


            for (int i = 0; i < MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM; ++i)
            {
                if (m_directionalLightSwapChain[i] == null)
                    m_directionalLightSwapChain[i] = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
            }

            _CreateCommandBuffers();

            _InitialiseRenderTextures();

            return recreatedResources;
        }

        void _CreateCommandBuffers()
        {
            if (!m_CommandBufferAdded)
            {
                m_CommandBufferBakeSM = new CommandBuffer();
                m_CommandBufferBakeSM.name = "HVR Depth Blit CB";

                m_CommandBufferCopyDepth = new CommandBuffer();
                m_CommandBufferCopyDepth.name = "Copy Depth CB";

#if UNITY_2017_1_OR_NEWER
                m_CommandBufferScreenspaceShadowMask = new CommandBuffer();
                m_CommandBufferScreenspaceShadowMask.name = "HVR Screenspace Shadow Mask CB";
#endif                

                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMap, m_CommandBufferCopyDepth);

                if (m_DependentLight.type == LightType.Point)
                {
                    // Unity issue a clear command on each cubemap face, so have to do it after shadowmap
                    m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMap, m_CommandBufferBakeSM);
                }
                else
                {
                    // It would be nice to use AfterShadowMap event, but apparently Unity has a viewport bug which restrict 
                    // our rendering onto the last cascade shadowmap, so have to switch BeforeShadowMap event
                    m_DependentLight.AddCommandBuffer(LightEvent.BeforeShadowMap, m_CommandBufferBakeSM);
                }

#if UNITY_2017_1_OR_NEWER
                // Directional light need a screen space pass
                if (m_DependentLight.type == LightType.Directional)
                {
                    m_DependentLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, m_CommandBufferScreenspaceShadowMask);
                }
#endif
                m_CommandBufferAdded = true;
            }
        }

        void _InitialiseRenderTextures()
        {
            int baseMapSize = _GetBaseMapSize();
            int baseMapSizeForPoint = _GetPointMapSize();
#if UNITY_2017_1_OR_NEWER
            int baseMapSizeForDirectional = _GetDirectionalMapSize();
#endif
            if (!m_DepthRT)
            {
                m_DepthRT = new RenderTexture(baseMapSize, baseMapSize, 16, RenderTextureFormat.Depth)
                {
                    name = "HVR depth in spot light",
#if UNITY_5_5_OR_NEWER
                    autoGenerateMips = false,
#else
                    generateMips = false,
#endif
                    anisoLevel = 0,
                    filterMode = FilterMode.Point,
                    isPowerOfTwo = false,
                    useMipMap = false
                };
                m_DepthRT.Create();

                m_DepthWithSceneRT = new RenderTexture(baseMapSize, baseMapSize, 16, Helper.GetSupportedRenderTextureFormatForDepthBlit())
                {
                    name = "HVR combined with scene depth in spot light",
#if UNITY_5_5_OR_NEWER
                    autoGenerateMips = false,
#else
                    generateMips = false,
#endif
                    anisoLevel = 0,
                    filterMode = FilterMode.Point,
                    isPowerOfTwo = false,
                    useMipMap = false
                };
                m_DepthWithSceneRT.Create();
            }

#if UNITY_2017_1_OR_NEWER        
            if (!m_ShadowmapRT)
            {
                m_ShadowmapRT = new RenderTexture(baseMapSizeForDirectional, baseMapSizeForDirectional, 16, RenderTextureFormat.Shadowmap)
                {
                    name = "HVR shadowmap in directional light",
                    autoGenerateMips = false,
                    anisoLevel = 0,
                    filterMode = FilterMode.Point,
                    isPowerOfTwo = false,
                    useMipMap = false
                };
            }

#endif
            RenderTextureFormat depthFormat = RenderTextureFormat.Depth;
            RenderTextureFormat linearDepthFormat = Helper.GetSupportedRenderTextureFormatForDepthBlit();

            // We need to keep a temporary render texture across different command buffers.
            if (m_CubemapWorkingBuffer == null)
            {
                if (m_PointLightUsingNonlinearDepth)
                    m_CubemapWorkingBuffer = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, RenderTextureFormat.Shadowmap);
                else
                    m_CubemapWorkingBuffer = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, CubemapDepthRenderTextureFormat);
                m_CubemapWorkingBuffer.name = "Temporary blit texture for cubemaps";
                m_CubemapWorkingBuffer.Create();
            }

            // create cubemap related resources for point light
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.PositiveZ))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(0, 0, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth +Z";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit +Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightPositiveZ);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore +Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightPositiveZ);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 +Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightPositiveZ);
#endif
                m_PointLightCubemapBookkeep[CubemapFace.PositiveZ] = record;
            }
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.NegativeZ))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(0, 180, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth -Z";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit -Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightNegativeZ);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore -Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightNegativeZ);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 -Z";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightNegativeZ);

#endif
                m_PointLightCubemapBookkeep[CubemapFace.NegativeZ] = record;
            }
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.PositiveX))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(0, 90, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth +X";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit +X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightPositiveX);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore +X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightPositiveX);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 +X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightPositiveX);
#endif
                m_PointLightCubemapBookkeep[CubemapFace.PositiveX] = record;
            }
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.NegativeX))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(0, 270, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth -X";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit -X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightNegativeX);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore -X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightNegativeX);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 -X";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightNegativeX);
#endif
                m_PointLightCubemapBookkeep[CubemapFace.NegativeX] = record;
            }
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.PositiveY))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(-90, 0, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth +Y";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit +Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightPositiveY);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore +Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightPositiveY);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 +Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightPositiveY);
#endif
                m_PointLightCubemapBookkeep[CubemapFace.PositiveY] = record;
            }
            if (!m_PointLightCubemapBookkeep.ContainsKey(CubemapFace.NegativeY))
            {
                ViewportRecord record = new ViewportRecord();
                record.rotation = Quaternion.Euler(90, 0, 0);
                record.swapchain = new HvrViewportSwapChain(MAX_VIEWPORT_NUM);
                record.depth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, depthFormat);
                record.linearDepth = new RenderTexture(baseMapSizeForPoint, baseMapSizeForPoint, 16, linearDepthFormat);
                record.linearDepth.name = "Linear depth -Y";
                record.linearDepth.Create();
#if UNITY_5_6_OR_NEWER
                record.bakeShadowMapCB = new CommandBuffer();
                record.bakeShadowMapCB.name = "HVR Depth Blit -Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.bakeShadowMapCB, ShadowMapPass.PointlightNegativeY);

                record.restoreShadowMapCB = new CommandBuffer();
                record.restoreShadowMapCB.name = "HVR Depth Restore -Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB, ShadowMapPass.PointlightNegativeY);

                record.restoreShadowMapCB2 = new CommandBuffer();
                record.restoreShadowMapCB2.name = "HVR Depth Restore2 -Y";
                m_DependentLight.AddCommandBuffer(LightEvent.AfterShadowMapPass, record.restoreShadowMapCB2, ShadowMapPass.PointlightNegativeY);
#endif
                m_PointLightCubemapBookkeep[CubemapFace.NegativeY] = record;
            }

        }

        void _DestroyResources()
        {
            if (m_CommandBufferAdded)
            {
#if UNITY_2017_1_OR_NEWER                
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, m_CommandBufferScreenspaceShadowMask);
                m_CommandBufferScreenspaceShadowMask.Release();
                m_CommandBufferScreenspaceShadowMask = null;
#endif
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, m_CommandBufferCopyDepth);
                m_CommandBufferCopyDepth.Release();
                m_CommandBufferCopyDepth = null;

                m_DependentLight.RemoveCommandBuffer(LightEvent.BeforeShadowMap, m_CommandBufferBakeSM);
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, m_CommandBufferBakeSM);
                m_CommandBufferBakeSM.Release();
                m_CommandBufferBakeSM = null;

                m_CommandBufferAdded = false;
            }

            if (m_spotlightSwapChain != null)
            {
                m_spotlightSwapChain.DeleteViewports();
                m_spotlightSwapChain = null;
            }

            for (int i = 0; i < 4; ++i)
            {
                if (m_directionalLightSwapChain[i] != null)
                {
                    m_directionalLightSwapChain[i].DeleteViewports();
                    m_directionalLightSwapChain[i] = null;
                }
            }

#if UNITY_2017_1_OR_NEWER
            // set to null to facilitate recreation when next time is used
            m_ScreenQuadForMask = null;
#endif
            _ReleaseRenderTextures();
        }

        void _ReleaseRenderTextures()
        {
#if UNITY_2017_1_OR_NEWER                    
            if (m_ShadowmapRT != null)
            {
                m_ShadowmapRT.Release();
                m_ShadowmapRT = null;

            }

            if (m_screenspaceHvrShadowMaskTexture != null)
            {
                m_screenspaceHvrShadowMaskTexture.Release();
                m_screenspaceHvrShadowMaskTexture = null;
            }
#endif
            if (m_DepthRT != null)
            {
                m_DepthRT.Release();
                m_DepthRT = null;

                m_DepthWithSceneRT.Release();
                m_DepthWithSceneRT = null;
            }

            if (m_CubemapWorkingBuffer != null)
            {
                m_CubemapWorkingBuffer.Release();
                m_CubemapWorkingBuffer = null;
            }

            // remove render textures inside cubemap book keep
            foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
            {
                pair.Value.depth.Release();
                pair.Value.linearDepth.Release();
#if UNITY_5_6_OR_NEWER
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterShadowMapPass, pair.Value.bakeShadowMapCB);
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterShadowMapPass, pair.Value.restoreShadowMapCB);
                m_DependentLight.RemoveCommandBuffer(LightEvent.AfterShadowMapPass, pair.Value.restoreShadowMapCB2);
#endif
            }

            foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
            {
                pair.Value.swapchain.DeleteViewports();
            }

            m_PointLightCubemapBookkeep = new Dictionary<CubemapFace, ViewportRecord>();

            // Call here to ensure that the released render textures are fully released from memory
            Resources.UnloadUnusedAssets();
        }

        // Private implementation
        //////////////////////////////////////////////////////////////////////////////

        void _EnsureDependents()
        {
            if (m_DependentLight == null)
            {
                m_DependentLight = GetComponent<Light>();
            }
        }

        ICollection<ActorShadowMapPair> _CollectAllShadowCastingActors(Plane[] lightFrustumPlanes)
        {
            var actorList = new List<ActorShadowMapPair>();

            List<HvrActor> sceneActors = new List<HvrActor>();
            HvrScene.GetObjects(sceneActors);

            foreach (HvrActor actor in sceneActors)
            {
                if (!actor.enabled || !actor.gameObject.activeInHierarchy)
                    continue;

                if (actor.assetInterface == null || actor.assetInterface == null)
                    continue;

                // skip any actor that doesn't tick cast shadow option
                if (!actor.castShadows)
                    continue;

                // Skip if this actor is ignored by this light's culling mask
                if (!Helper.IsLayerVisibleInCullingMask(actor.gameObject.layer, m_DependentLight.cullingMask))
                    continue;

                Bounds localBounds = actor.assetInterface.GetBounds();
                Bounds worldBounds = new Bounds(localBounds.center, localBounds.size);
                worldBounds.center = actor.transform.localToWorldMatrix.MultiplyPoint(localBounds.center);

                if (GeometryUtility.TestPlanesAABB(lightFrustumPlanes, worldBounds))
                {
                    actorList.Add(new ActorShadowMapPair(actor));
                }
            }

            return actorList;
        }

        int _GetBaseMapSize()
        {
            return BASE_RESOLUTION[(int)ShadowResolution];
        }

        int _GetSingleMapSize()
        {
            return SINGLE_RESOLUTION[(int)ShadowResolution];
        }

        int _GetDirectionalMapSize()
        {
            return SHADOWMAP_RESOLUTION_FOR_DIRECTIONALLIGHT[(int)ShadowResolution];
        }

        int _GetPointMapSize()
        {
            int[] sizes = SHADOWMAP_RESOLUTION_FOR_POINTLIGHT[(int)ShadowResolution];

            int maxDimension;
#if UNITY_EDITOR
            EditorWindow gameView = EditorHelper.GetMainGameView();
            if (gameView != null)
                maxDimension = Mathf.Max((int)gameView.position.width, (int)gameView.position.height);
            else
                maxDimension = 640;
#else            
            maxDimension = Mathf.Max(Screen.width, Screen.height);
#endif            
            int size = 0;
            if (maxDimension <= 128)
            {
                size = sizes[0];
            }
            else if (maxDimension <= 256)
            {
                size = sizes[1];
            }
            else if (maxDimension <= 512)
            {
                size = sizes[2];
            }
            else if (maxDimension <= 1024)
            {
                size = sizes[3];
            }
            else
            {
                size = sizes[4];
            }

            return size;
        }

#if UNITY_EDITOR
        int[] _GetPointMapSizeForSceneView()
        {
            System.Collections.ArrayList sceneViews = SceneView.sceneViews;
            if (sceneViews.Count == 0)
            {
                return null;
            }

            int[] results = new int[sceneViews.Count];
            int counter = 0;
            foreach (SceneView sceneView in sceneViews)
            {

                int[] sizes = SHADOWMAP_RESOLUTION_FOR_POINTLIGHT[(int)ShadowResolution];

                int maxDimension = Mathf.Max((int)sceneView.position.width, (int)sceneView.position.height);
                int size = 0;
                if (maxDimension <= 128)
                {
                    size = sizes[0];
                }
                else if (maxDimension <= 256)
                {
                    size = sizes[1];
                }
                else if (maxDimension <= 512)
                {
                    size = sizes[2];
                }
                else if (maxDimension <= 1024)
                {
                    size = sizes[3];
                }
                else
                {
                    size = sizes[4];
                }

                results[counter++] = size;

            }


            return results;
        }
#endif

        static bool IsCopyTextureFromOrToSystemShadowmapSupported()
        {
#if UNITY_5_6_OR_NEWER
            int copyTextureSupport = (int)SystemInfo.copyTextureSupport;
            if ((copyTextureSupport & (int)CopyTextureSupport.Basic) != 0 &&
                (copyTextureSupport & (int)CopyTextureSupport.TextureToRT) != 0 &&
                (copyTextureSupport & (int)CopyTextureSupport.RTToTexture) != 0)
            {
                // Android tends to use encoded ARGB texture as depth map, so with CopyTexture across different types of texture it will 
                // mess up the depth, so don't use that at all.
                if ((copyTextureSupport & (int)CopyTextureSupport.DifferentTypes) != 0 &&
                    Application.platform == RuntimePlatform.Android)
                {
                    return false;
                }

#if UNITY_IOS
                bool isMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
                if (isMetal)
                {
                    return false;
                }
#endif                

                return true;
            }
#endif

            // NOTE: Copy texture is not universally available on desktops. 
            // e.g. Unity 5.6 Mac standalone with OGL 4.1 has no copy texture support. 
            // So expect point light not casting shadows on HVRs in these cases.
            return false;
        }

        void GetDirectionalLightSplitPercentAndShadowParams(Camera cam, out float[] splitPercent, out Rect[] cascadeShadowRect, out Matrix4x4[] toShadowUVSpace)
        {
            int mapSize = _GetDirectionalMapSize();

            int shadowSplit = QualitySettings.shadowCascades;
            if (cam.orthographic)
            {
                shadowSplit = 1; // no cascaded shadowmap in orthographic view
            }

            switch (shadowSplit)
            {
                case 4:
                    {
                        Vector3 split4 = QualitySettings.shadowCascade4Split;
                        splitPercent = new float[4]
                        {
                            split4.x, split4.y, split4.z, 1.0f
                        };
                    }
                    break;

                case 2:
                    {
                        splitPercent = new float[2]
                        {
                            QualitySettings.shadowCascade2Split, 1.0f
                        };
                    }
                    break;
                default:
                    {
                        splitPercent = new float[1]
                        {
                            1.0f
                        };
                    }
                    break;
            }

            // making four cascades
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore ||
#if !UNITY_5_5_OR_NEWER
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGL2 ||
#endif
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
            {
                switch (shadowSplit)
                {
                    case 4:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, 0, mapSize/2, mapSize/2),
                                new Rect(mapSize/2, 0, mapSize/2, mapSize/2),
                                new Rect(0, mapSize/2, mapSize/2, mapSize/2),
                                new Rect(mapSize/2, mapSize/2, mapSize/2, mapSize/2),

                            };

                            // OpenGL: clip space, xy E[-1, 1], z E[-1, 1], convert to UV: xy E[0, 1], zE[0, 1]
                            toShadowUVSpace = new Matrix4x4[4]
                            {
                                Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0.5f, 0, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),
                            };

                        }
                        break;

                    case 2:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, 0, mapSize, mapSize/2),
                                new Rect(0, mapSize/2, mapSize, mapSize/2)
                            };

                            toShadowUVSpace = new Matrix4x4[2]
                            {
                                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1.0f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(1.0f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one),
                            };
                        }
                        break;

                    default:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, 0, mapSize, mapSize)
                            };

                            toShadowUVSpace = new Matrix4x4[1]
                            {
                                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f)) *
                                    Matrix4x4.TRS(new Vector3(1, 1, 1), Quaternion.identity, Vector3.one)
                            };
                        }
                        break;
                }

            }
            else
            {
                switch (shadowSplit)
                {
                    case 4:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, mapSize/2, mapSize/2, mapSize/2),
                                new Rect(mapSize/2, mapSize/2, mapSize/2, mapSize/2),
                                new Rect(0, 0, mapSize/2, mapSize/2),
                                new Rect(mapSize/2, 0, mapSize/2, mapSize/2)
                            };

                            // D3D/Metal: clip space, xy E[-1, 1], z E[0, 1], convert to UV: xy E[0, 1], zE[0, 1]
                            toShadowUVSpace = new Matrix4x4[4]
                            {
                                Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(new Vector3(0.5f, 0, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one)
                            };
                        }
                        break;

                    case 2:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, mapSize/2, mapSize, mapSize/2),
                                new Rect(0, 0, mapSize, mapSize/2),
                            };

                            toShadowUVSpace = new Matrix4x4[2]
                            {
                                Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(1.0f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one),

                                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1.0f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                        Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one)
                            };
                        }
                        break;

                    default:
                        {
                            cascadeShadowRect = new Rect[]
                            {
                                new Rect(0, 0, mapSize, mapSize)
                            };

                            toShadowUVSpace = new Matrix4x4[1]
                            {
                                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 1.0f)) *
                                    Matrix4x4.TRS(new Vector3(1, 1, 0), Quaternion.identity, Vector3.one)
                            };
                        }
                        break;
                }
            }
        }

        public void WillRender()
        {
            if (!m_enabled)
                return;

            LightType lightType = m_DependentLight.type;

            if (lightType == LightType.Directional)
            {
                #region Directional
#if UNITY_2017_1_OR_NEWER
                int i;
                Camera cam = CameraHelper.GetMainCamera();

                if (cam != null)
                {

                    // Prepare and cache the view/proj matrices for RenderActor and pass them to WilLRender
                    float[] splitPercent;
                    Rect[] cascadeShadowRect;
                    Matrix4x4[] __;
                    GetDirectionalLightSplitPercentAndShadowParams(cam, out splitPercent, out cascadeShadowRect, out __);

                    int shadowSplit = splitPercent.Length;
                    float shadowLimit = Mathf.Min(ShadowDistanceLimit, QualitySettings.shadowDistance);
                    float Rmax = BoundsBuilder.GetBoundingSphereForCameraCascade(cam, 1.0f, shadowLimit).radius;
                    for (i = 0; i < shadowSplit; ++i)
                    {
                        // Match the stablization of directional light shadow in Unity.
                        // The idea has been discussed here: http://the-witness.net/news/2010/03/graphics-tech-shadow-maps-part-1/
                        // and here: http://bryanlawsmithblog.blogspot.co.nz/2014/12/rendering-post-stable-cascaded-shadow.html
                        // So fundamentally our goal is to have a perfect stationary shadow when camera moves(panning, rotating)
                        // in directional lights. As in directional lights, the shadow map doesn't move along with the light source
                        // since the position of directional lights have no meanings. The shadow map is generated by calculating a
                        // bounding sphere of the view frustum. So the shadow map moves when camera moves. This causes shadow
                        // "shimmering" problem because when the sampling shader samples the shadowmap, it will come across with
                        // subpixels that makes the edge of shadow swim. The idea is to "snap" the movement in a exact step of one
                        // single shadowmap texel. The calculation here involves compute the bounding sphere, taking the shadow map size,
                        // and use a reference point and transform it into clip space, then change its unit to texel and get the 
                        // round off offset. Using the offset in the orthogonal projection matrix, and you can get a perfect texel aligned
                        // shadow map and thus eliminate the shimmering effect.
                        Rect rect = cascadeShadowRect[i];
                        float split = splitPercent[i];



                        BoundingSphere worldSpaceFrustumSphere = BoundsBuilder.GetBoundingSphereForCameraCascade(cam, split, shadowLimit); // in world space
                        m_CachedBoundingSphere[i] = worldSpaceFrustumSphere;
                        // worldSpaceFrustumSphere.radius = Mathf.Ceil(worldSpaceFrustumSphere.radius * 16.0f) / 16.0f;

                        // make the stabalization compatible with shadow split
                        int textureMapSizeX = m_ShadowmapRT.width;
                        int textureMapSizeY = m_ShadowmapRT.height;
                        if (shadowSplit == 4)
                        {
                            textureMapSizeX /= 2;
                            textureMapSizeY /= 2;
                        }
                        else if (shadowSplit == 2)
                        {
                            textureMapSizeY /= 2;
                        }

                        //int textureMapSize = shadowSplit > 1 ? GetShadowmapDimension() / 2 : GetShadowmapDimension();
                        Quaternion orientation = m_DependentLight.transform.rotation;
                        Vector3 euler = orientation.eulerAngles;
                        orientation = Quaternion.Euler(euler.x, euler.y, 0); // make no funny rotation on z axis

                        Vector3 fromCenterToOrigin = new Vector3(0, 0, -worldSpaceFrustumSphere.radius);
                        Vector3 origin = worldSpaceFrustumSphere.position + m_DependentLight.transform.rotation * fromCenterToOrigin;



                        Matrix4x4 lightSpaceView = Matrix4x4.TRS(origin, orientation, Vector3.one).inverse;
                        Matrix4x4 invertZ = Matrix4x4.Scale(new Vector3(1, 1, -1));
                        lightSpaceView = invertZ * lightSpaceView;

                        // construct the view matrix with view space bias so that we don't have to include bias in shader
                        Matrix4x4 lightSpaceViewWithBias = Matrix4x4.TRS(new Vector3(0, 0, -m_DependentLight.shadowBias), Quaternion.identity, Vector3.one) *
                            Matrix4x4.TRS(origin, orientation, Vector3.one).inverse;
                        lightSpaceViewWithBias = invertZ * lightSpaceViewWithBias;

                        Matrix4x4 proj = Matrix4x4.Ortho(
                            -worldSpaceFrustumSphere.radius, +worldSpaceFrustumSphere.radius,
                            -worldSpaceFrustumSphere.radius, +worldSpaceFrustumSphere.radius,
                            0 - (Rmax - worldSpaceFrustumSphere.radius), 2 * worldSpaceFrustumSphere.radius // in order to prevent depth clipping when casting shadow, we need to extend near plane to the boundary of the maximum radius
                                                                                                            //0, 2 * worldSpaceFrustumSphere.radius
                            );

                        Matrix4x4 viewProject = proj * lightSpaceView;
                        Vector3 shadowMapOrigin = new Vector3(0, 0, 0);
                        shadowMapOrigin = viewProject.MultiplyPoint(shadowMapOrigin); // in clip space
                        shadowMapOrigin.x *= textureMapSizeX / 2.0f; // in clip space but texel unit
                        shadowMapOrigin.y *= textureMapSizeY / 2.0f;

                        Vector3 shadowMapOriginRounded = new Vector3(
                                Mathf.Round(shadowMapOrigin.x),
                                Mathf.Round(shadowMapOrigin.y),
                                Mathf.Round(shadowMapOrigin.z)
                            );

                        Vector3 roundOffset = shadowMapOrigin - shadowMapOriginRounded; // fraction of offset
                        roundOffset.x *= 2.0f / textureMapSizeX; // fraction in default unit
                        roundOffset.y *= 2.0f / textureMapSizeY;
                        roundOffset.x *= -1; // have to turn the direction on x, y
                        roundOffset.y *= -1;
                        roundOffset.z = 0;

                        // apply back to projection matrix
                        Vector4 column3 = proj.GetColumn(3);
                        column3 += new Vector4(roundOffset.x, roundOffset.y, 0, 0);
                        proj.SetColumn(3, column3);
                        // Or do the following if in a more meaningful way
                        //Matrix4x4 translate = Matrix4x4.TRS(roundOffset, Quaternion.identity, Vector3.one);
                        //proj = translate * proj;

                        Matrix4x4 lightSpaceProject = GL.GetGPUProjectionMatrix(proj, false);
                        Matrix4x4 lightSpaceProjectGPU = GL.GetGPUProjectionMatrix(proj, true);
                        m_CachedDirectionalLightViewProjectionMatrix[i] = lightSpaceProjectGPU * lightSpaceViewWithBias;

                        directionalLightViewport[i] = m_directionalLightSwapChain[i].NextViewport(
                                lightSpaceView, lightSpaceProject,
                                (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);


                        Plane[] lightFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(proj * lightSpaceView);

                        var actorList = _CollectAllShadowCastingActors(lightFrustumPlanes);

                        foreach (var actorShadowPair in actorList)
                        {
                            HvrScene.Player_WillRender(actorShadowPair.actor.actorInterface.handle, directionalLightViewport[i].handle);
                        }

                        directionalLightCachedActors[i] = actorList;

                    }

                    // fill in the zeros
                    for (; i < 4; ++i)
                    {
                        m_CachedBoundingSphere[i] = new BoundingSphere();
                        m_CachedDirectionalLightViewProjectionMatrix[i] = Matrix4x4.zero;
                        directionalLightCachedActors[i] = null;
                    }
                }
#endif

                #endregion
            }
            else
            if (lightType == LightType.Spot)
            {
                #region Spot

                int mapSize = _GetBaseMapSize();

                // DO IT IN THE OLD WAY
                Matrix4x4 view = m_DependentLight.transform.worldToLocalMatrix;
                // NOTE: From Unity script API Camera.worldToCameraMatrix,
                // "Note that camera space matches OpenGL convention: camera's forward is the negative Z axis. This is different from Unity's convention, where forward is the positive Z axis."
                Matrix4x4 invertZ = Matrix4x4.Scale(new Vector3(1, 1, -1));

                view = invertZ * view;

                // TODO, Determine if these values are required when creating the projection
                float near = m_DependentLight.shadowNearPlane;
                float far = m_DependentLight.range;

                Matrix4x4 offsetBias = Matrix4x4.TRS(new Vector3(0, 0, m_DependentLight.shadowBias / (m_DependentLight.range - m_DependentLight.shadowNearPlane)), Quaternion.identity, Vector3.one);
                Matrix4x4 proj = offsetBias * Matrix4x4.Perspective(m_DependentLight.spotAngle, 1.0f, near, far);

                Matrix4x4 lightSpaceProjectRT = GL.GetGPUProjectionMatrix(proj, true);

                spotLightViewport = m_spotlightSwapChain.NextViewport(view, lightSpaceProjectRT, 0, 0, mapSize, mapSize);

                Plane[] lightFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(proj * view);

                // Collect all the rendering actors but no render yet
                var actorList = _CollectAllShadowCastingActors(lightFrustumPlanes);

                foreach (var actorShadowPair in actorList)
                {
                    HvrScene.Player_WillRender(actorShadowPair.actor.actorInterface.handle, spotLightViewport.handle);
                }

                spotLightCachedActors = actorList;

                #endregion
            }
            else
            if (lightType == LightType.Point)
            {
                #region Point

                // cubemap size
                int cubeMapSize = _GetPointMapSize();
                float effectiveRange = m_DependentLight.range;
                Vector3 p = m_DependentLight.transform.position;

                foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
                {
                    CubemapFace cubemapFace = pair.Key;
                    ViewportRecord record = pair.Value;

                    Matrix4x4 view = Matrix4x4.TRS(p, record.rotation, Vector3.one);
                    view = view.inverse; // from world to view

                    // NOTE: From Unity script API Camera.worldToCameraMatrix,
                    // "Note that camera space matches OpenGL convention: camera's forward is the negative Z axis. This is different from Unity's convention, where forward is the positive Z axis."
                    Matrix4x4 invertZ = Matrix4x4.Scale(new Vector3(1, 1, -1));
                    view = invertZ * view; // apply Unity convention

                    Matrix4x4 proj = Matrix4x4.Perspective(90.0f, 1.0f, m_DependentLight.shadowNearPlane, effectiveRange);
                    Matrix4x4 projWithBias = Matrix4x4.TRS(new Vector3(0, 0, m_DependentLight.shadowBias / (effectiveRange - m_DependentLight.shadowNearPlane)), Quaternion.identity, Vector3.one) * proj;
                    Matrix4x4 projGPU = GL.GetGPUProjectionMatrix(projWithBias, false);

                    // we don't want the bias accounts in the inversion matrix as it will counter-affect in depth-rebuilding shader and bias will become zero
                    Matrix4x4 inverseViewProjection = (proj * view).inverse;

                    Plane[] lightFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(proj * view);

                    HvrViewportSwapChain swapchain = record.swapchain;
                    ViewportInterface cubeFaceViewport = swapchain.NextViewport(view, projGPU, 0, 0, cubeMapSize, cubeMapSize);

                    ViewportSavingData data = new ViewportSavingData(cubeFaceViewport, inverseViewProjection);

                    // Collect all the rendering actors but no render yet
                    var actorList = _CollectAllShadowCastingActors(lightFrustumPlanes);

                    record.hasTransparentActor = false;
                    foreach (var actorShadowPair in actorList)
                    {
                        if (actorShadowPair.actor.material != null &&
                            MaterialHelper.RenderQueueIsTransparent(actorShadowPair.actor.material))
                        {
                            record.hasTransparentActor = true;
                        }

                        HvrScene.Player_WillRender(actorShadowPair.actor.actorInterface.handle, cubeFaceViewport.handle);
                    }

                    data.actorList = actorList;
                    cubemapViewportSaving[cubemapFace] = data;
                }
                #endregion
            }
        }

        void _ClearAllCommandBuffers()
        {
#if UNITY_5_6_OR_NEWER
            foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
            {
                ViewportRecord record = pair.Value;
                record.bakeShadowMapCB.Clear();
                record.restoreShadowMapCB.Clear();
                record.restoreShadowMapCB2.Clear();
            }
#endif

            m_CommandBufferBakeSM.Clear();
            m_CommandBufferCopyDepth.Clear();
#if UNITY_2017_1_OR_NEWER            
            m_CommandBufferScreenspaceShadowMask.Clear();
#endif            
        }

        private static void RequestRenderTextureColorDepthPair(int width, int height, out RenderTexture colormap, out RenderTexture depthmap)
        {
            colormap = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            colormap.name = "Temp light space color buffer";
            colormap.Create();

            depthmap = RenderTexture.GetTemporary(width, height, 16, RenderTextureFormat.Depth);
            depthmap.name = "Temp light space depth buffer";
            depthmap.Create();
        }

        private static void ReleaseRenderTextureColorDepthPair(RenderTexture colormap, RenderTexture depthmap)
        {
            RenderTexture.ReleaseTemporary(colormap);
            RenderTexture.ReleaseTemporary(depthmap);
        }

        void _DoRenderFromLight()
        {
            if (m_DependentLight == null)
                return;

            if (!m_CommandBufferAdded)
                return;

            LightType lightType = m_DependentLight.type;

            MaterialPropertyBlock renderMPB = new MaterialPropertyBlock();

            m_CommandBufferBakeSM.Clear();


            RenderBuffer originalColorBuffer = Graphics.activeColorBuffer;
            RenderBuffer originalDepthBuffer = Graphics.activeDepthBuffer;

            int mapSize = _GetBaseMapSize();

            CleanupShadowMapPool();

            if (lightType == LightType.Point)
            {
                #region PointLight

                // cubemap size
                int cubeMapSize = _GetPointMapSize();
#if UNITY_EDITOR && UNITY_5_6_OR_NEWER
                bool sceneGameViewHasCompatibleCubemapSize = false;
                /* The code was kept here for reference. Due to the fact in light event we can't
                    reliably find the corresponding view/camera(scene camera, game view camera, 
                    camera preview, multiple views, etc)'s dimension, CopyTexture will be disabled
                    in the editor.

                bool sceneGameViewHasCompatibleCubemapSize = true;
                // all scene view cubemap size must match the size of game view's, otherwise 
                // don't use CopyTexture()
                int[] allSceneViewCubemapSizes = _GetPointMapSizeForSceneView();
                if (allSceneViewCubemapSizes != null )
                {
                    foreach(int sceneViewCubemapSize in allSceneViewCubemapSizes)
                    {
                        if (sceneViewCubemapSize != cubeMapSize)
                        {
                            sceneGameViewHasCompatibleCubemapSize = false;
                            break;
                        }
                    }
                }
                */
#endif

#if UNITY_5_6_OR_NEWER || (!UNITY_IPHONE && !UNITY_ANDROID)
                float effectiveRange = m_DependentLight.range;
                Vector3 p = m_DependentLight.transform.position;
#endif

                // Prepare for render
                if (m_lightIsRealtime)
                {
                    foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
                    {
                        CubemapFace cubemapFace = pair.Key;
                        ViewportRecord record = pair.Value;
                        // Check validity when building players
                        if (!cubemapViewportSaving.ContainsKey(cubemapFace))
                            continue;
                        ViewportInterface cubeFaceViewport = cubemapViewportSaving[cubemapFace].viewport;

#if UNITY_5_6_OR_NEWER || (!UNITY_IPHONE && !UNITY_ANDROID)
                        Matrix4x4 inverseViewProjection = cubemapViewportSaving[cubemapFace].inverseViewProjection;
#endif
                        var actorList = cubemapViewportSaving[cubemapFace].actorList;

#if UNITY_5_6_OR_NEWER
                        record.bakeShadowMapCB.Clear();
                        record.restoreShadowMapCB.Clear();
                        record.restoreShadowMapCB2.Clear();
#endif

                        if (actorList.Count > 0)
                        {
                            if (record.hasTransparentActor)
                            {
                                // Render each actor depth separatedly onto ActorShadowMapPair.shadowmap
                                foreach (ActorShadowMapPair actorShadowMap in actorList)
                                {
                                    RenderTexture colormap, depthmap;
                                    RequestRenderTextureColorDepthPair(cubeMapSize, cubeMapSize, out colormap, out depthmap);
                                    actorShadowMap.shadowmap = RequestShadowMap(actorShadowMap.actor, cubemapFace, cubeMapSize, cubeMapSize);

                                    Graphics.SetRenderTarget(depthmap.colorBuffer, depthmap.depthBuffer);
                                    GL.Clear(true, true, Color.white, 1.0f);


                                    if (depthmap.IsCreated())
                                        HvrScene.Player_RenderActor(actorShadowMap.actor.actorInterface.handle, cubeFaceViewport.handle, null, depthmap.depthBuffer.GetNativeRenderBufferPtr);

                                    Graphics.Blit(depthmap, actorShadowMap.shadowmap);
                                    ReleaseRenderTextureColorDepthPair(colormap, depthmap);
                                }

#if UNITY_5_6_OR_NEWER

                                // Use copy texture can eliminate point light shadowing artifacts
                                bool useCopyTexture = IsCopyTextureFromOrToSystemShadowmapSupported();
#if UNITY_EDITOR
                                useCopyTexture &= sceneGameViewHasCompatibleCubemapSize;
#endif

                                if (useCopyTexture)
                                {
                                    // In Unity 5.6, we have per shadowmap pass command buffer event, and the cubemap-friendly CommandBuffer.CopyTexture()(if platform supports) to backup/restore
                                    // each cubemap faces. The work involves create a temp target and copy both depth of current scene and HVRs into one, then restore it to the pipeline.
                                    int tempRT;
                                    tempRT = Shader.PropertyToID("_sceneDepth");
                                    if (m_PointLightUsingNonlinearDepth)
                                    {
                                        record.bakeShadowMapCB.GetTemporaryRT(tempRT, cubeMapSize, cubeMapSize, 16, FilterMode.Point, RenderTextureFormat.Depth);
                                        record.bakeShadowMapCB.SetShadowSamplingMode(BuiltinRenderTextureType.CurrentActive, ShadowSamplingMode.RawDepth);
                                    }
                                    else
                                    {
                                        record.bakeShadowMapCB.GetTemporaryRT(tempRT, cubeMapSize, cubeMapSize, 16, FilterMode.Point, CubemapDepthRenderTextureFormat);
                                    }

                                    record.bakeShadowMapCB.CopyTexture(BuiltinRenderTextureType.CurrentActive, (int)cubemapFace, tempRT, 0);
                                    // clears the working buffer
                                    record.bakeShadowMapCB.SetRenderTarget(m_CubemapWorkingBuffer, m_CubemapWorkingBuffer);
                                    record.bakeShadowMapCB.ClearRenderTarget(true, true, Color.green, 1.0f);

                                    // clears the linear depth buffer. and leave the linear depth buffer as current render buffer
                                    // important for cases (m_PointLightUsingNonlinearDepth == false)
                                    record.bakeShadowMapCB.SetRenderTarget(record.linearDepth, record.linearDepth);
                                    record.bakeShadowMapCB.ClearRenderTarget(true, true, Color.green, 1.0f);

                                    renderMPB.Clear();
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                                    renderMPB.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));
                                    Matrix4x4 flipMapMatrix = GetFlipMapMatrixForCubemapFace(cubemapFace);
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._FlipMapMatrix, flipMapMatrix);

                                    foreach (ActorShadowMapPair actorShadowMap in actorList)
                                    {
                                        if (actorShadowMap.actor.material != null &&
                                            MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                                        {
                                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, MaterialHelper.GetMaterialTransparencyValue(actorShadowMap.actor.material), 0));
                                        }
                                        else
                                        {
                                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));
                                        }
                                        renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, actorShadowMap.shadowmap);

                                        // record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 2, renderMPB);
                                        if (m_PointLightUsingNonlinearDepth)
                                        {
                                            record.bakeShadowMapCB.SetRenderTarget(record.linearDepth, record.linearDepth);
                                            record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 5, renderMPB);

                                            // now generate native shadowmap, do exactly the same as above, except it has a RenderTextureFormat.Shadowmap format
                                            record.bakeShadowMapCB.SetRenderTarget(m_CubemapWorkingBuffer, m_CubemapWorkingBuffer);
                                            record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 5, renderMPB);
                                        }
                                        else
                                        {
                                            record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 2, renderMPB);
                                        }

                                    }

                                    if (m_PointLightUsingNonlinearDepth)
                                    {
                                        // restore system shadowmap with native shadowmap format
                                        record.restoreShadowMapCB2.CopyTexture(m_CubemapWorkingBuffer, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                    }
                                    else
                                    {
                                        if (flipMapMatrix[1, 1] < 0.0f)
                                        {
                                            // blit and flip along Y
                                            record.restoreShadowMapCB.SetRenderTarget(m_CubemapWorkingBuffer, m_CubemapWorkingBuffer);
                                            renderMPB.Clear();
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, record.linearDepth);
                                            record.restoreShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 4, renderMPB);
                                            // restore system shadowmap with native shadowmap format
                                            record.restoreShadowMapCB2.CopyTexture(m_CubemapWorkingBuffer, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                        }
                                        else
                                        {
                                            // copy back to cube shadowmap, since CurrentActive is already being replaced via CommandBuffer.SetRenderTarget(), 
                                            // we need a second command buffer to "restore" it. If unity can provide a way to save this CurrentActive target, then we can do the whole thing in one command buffer
                                            record.restoreShadowMapCB2.CopyTexture(record.linearDepth, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                        }
                                    }

                                    record.bakeShadowMapCB.ReleaseTemporaryRT(tempRT);
                                }
                                else
                                {
                                    // No CopyTexture() support, so just render linear depth to cubemap

                                    renderMPB.Clear();
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                                    renderMPB.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));

                                    foreach (ActorShadowMapPair actorShadowMap in actorList)
                                    {
                                        if (actorShadowMap.actor.material != null &&
                                            MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                                        {
                                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, MaterialHelper.GetMaterialTransparencyValue(actorShadowMap.actor.material), 0));
                                        }
                                        else
                                        {
                                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));
                                        }
                                        renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, actorShadowMap.shadowmap);

                                        record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 3, renderMPB);
                                    }

                                    record.restoreShadowMapCB.SetRenderTarget(pair.Value.linearDepth, pair.Value.linearDepth);
                                    record.restoreShadowMapCB.ClearRenderTarget(true, true, Color.white, 1.0f);
                                }

#else

                                // Clear the final linear depth map, prepare for each ActorShadowMapPair.shadowmap to blit onto.
                                // Use blit with custom shader instead of GL.Clear()
                                Graphics.Blit(null, record.linearDepth, m_LinearDepthMaterial, 1);


                                // iOS & Android seems to use encoded ARGB as depth format, in Unity 5.4 - 5.5. 
                                // Using Grpahics.Blit from Depth format to RFloat simply destroys the depth info.
                                // So don't cast HVR shadow in such cases.
#if !UNITY_IOS && !UNITY_ANDROID
                                // blit from depth to linear depth(we don't have control over how depth is generated from HvrPlayerInterface so have to an additional blit)
                                // TODO: maybe we can modify the projection matrix passed to HvrPlayerInterface to linearize the depth generated.
                                m_LinearDepthMaterial.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);

                                m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._CurrLightShadowBias, new Vector4(m_DependentLight.shadowBias, 0, 0, 0));
                                m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));

                                foreach (ActorShadowMapPair actorShadowMap in actorList)
                                {
                                    if (actorShadowMap.actor.material != null &&
                                        MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                                    {
                                        m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, MaterialHelper.GetMaterialTransparencyValue(actorShadowMap.actor.material), 0));
                                    }
                                    else
                                    {

                                        m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));
                                    }
                                    m_LinearDepthMaterial.SetTexture(Uniforms.ShaderProperties._oDEP, actorShadowMap.shadowmap);

                                    if (!m_PointLightUsingNonlinearDepth)
                                    {
                                        Graphics.Blit(actorShadowMap.shadowmap, record.linearDepth, m_LinearDepthMaterial, 0);
                                    }
                                    else
                                    {
                                        // Since Unity 2017.3 using non-linear depth so we simply blit here
                                        Graphics.Blit(actorShadowMap.shadowmap, record.linearDepth);
                                    }
                                }

                                renderMPB.Clear();
                                renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, record.linearDepth);

                                m_CommandBufferBakeSM.SetRenderTarget(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, 0, cubemapFace);

                                // IMPORTANT: In Unity 5.4.4 the implementation of cubemap doesn't have real 'depth', so basically it's 6 RFloat textures consist of cubemap.
                                // The problem is, we don't have read access to the rendered cubemap, and thus we can't use Z buffer or even in-shader comparison to bake
                                // depth properly. Besides the depth map used is also incorrect.
                                // The following trick only works in one-pointlight case: call ClearRenderTarget with only depth flag on, thus eliminate incorrect depth buffer issue,
                                // then bake the linear depth using "Less" mode in Z test. This will results a depth map with incorrect occlusion but turns out okay when rendered with 
                                // only one point light.
                                m_CommandBufferBakeSM.ClearRenderTarget(true, false, Color.red, 1.0f);
                                m_CommandBufferBakeSM.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 1, renderMPB);
#endif                                

#endif

                            }
                            else
                            {

                                Graphics.SetRenderTarget(record.depth.colorBuffer, record.depth.depthBuffer);
                                GL.Clear(true, true, Color.white, 1.0f);

                                // Submit all the guaranteed rendered actors to all the viewport for rendering
                                foreach (ActorShadowMapPair actorShadow in actorList)
                                {
                                    if (record.depth.IsCreated())
                                        HvrScene.Player_RenderActor(actorShadow.actor.actorInterface.handle, cubeFaceViewport.handle, null, record.depth.depthBuffer.GetNativeRenderBufferPtr);
                                }

#if UNITY_5_6_OR_NEWER

                                // In Unity 5.6, we have per shadowmap pass command buffer event, and the cubemap-friendly CommandBuffer.CopyTexture() to backup/restore
                                // each cubemap faces. The work involves create a temp target and copy both depth of current scene and HVRs into one, then restore it to the pipeline.

                                bool useCopyTexture = IsCopyTextureFromOrToSystemShadowmapSupported();
#if UNITY_EDITOR
                                useCopyTexture &= sceneGameViewHasCompatibleCubemapSize;
#endif

                                if (useCopyTexture)
                                {
                                    int tempRT = 0;
                                    tempRT = Shader.PropertyToID("_sceneDepth");
                                    if (m_PointLightUsingNonlinearDepth)
                                    {
                                        record.bakeShadowMapCB.GetTemporaryRT(tempRT, cubeMapSize, cubeMapSize, 16, FilterMode.Point, RenderTextureFormat.Depth);
                                        record.bakeShadowMapCB.SetShadowSamplingMode(BuiltinRenderTextureType.CurrentActive, ShadowSamplingMode.RawDepth);
                                    }
                                    else
                                    {
                                        record.bakeShadowMapCB.GetTemporaryRT(tempRT, cubeMapSize, cubeMapSize, 16, FilterMode.Point, CubemapDepthRenderTextureFormat);
                                    }
                                    record.bakeShadowMapCB.CopyTexture(BuiltinRenderTextureType.CurrentActive, (int)cubemapFace, tempRT, 0);
                                    record.bakeShadowMapCB.SetRenderTarget(record.linearDepth, record.linearDepth);
                                    record.bakeShadowMapCB.ClearRenderTarget(true, true, Color.green, 1.0f);

                                    renderMPB.Clear();
                                    renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, record.depth);
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                                    renderMPB.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));
                                    renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));
                                    Matrix4x4 flipMapMatrix = GetFlipMapMatrixForCubemapFace(cubemapFace);
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._FlipMapMatrix, flipMapMatrix);

                                    if (m_PointLightUsingNonlinearDepth)
                                    {
                                        record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 5, renderMPB);

                                        // now generate native shadowmap, do exactly the same as above, except it has a RenderTextureFormat.Shadowmap format
                                        record.bakeShadowMapCB.SetRenderTarget(m_CubemapWorkingBuffer, m_CubemapWorkingBuffer);
                                        record.bakeShadowMapCB.ClearRenderTarget(true, true, Color.green, 1.0f);
                                        record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 5, renderMPB);

                                        // restore system shadowmap with native shadowmap format
                                        record.restoreShadowMapCB2.CopyTexture(m_CubemapWorkingBuffer, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                    }
                                    else
                                    {
                                        record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 2, renderMPB);

                                        if (flipMapMatrix[1, 1] < 0)
                                        {
                                            // blit and flip along Y
                                            record.restoreShadowMapCB.SetRenderTarget(m_CubemapWorkingBuffer, m_CubemapWorkingBuffer);
                                            renderMPB.Clear();
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, pair.Value.linearDepth);
                                            record.restoreShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 4, renderMPB);
                                            record.restoreShadowMapCB2.CopyTexture(m_CubemapWorkingBuffer, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                        }
                                        else
                                        {
                                            // copy back to cube shadowmap, since CurrentActive is already being replaced via CommandBuffer.SetRenderTarget(), 
                                            // we need a second command buffer to "restore" it. If unity can provide a way to save this CurrentActive target, then we can do the whole thing in one command buffer
                                            record.restoreShadowMapCB2.CopyTexture(record.linearDepth, 0, BuiltinRenderTextureType.CurrentActive, (int)cubemapFace);
                                        }
                                    }

                                    record.bakeShadowMapCB.ReleaseTemporaryRT(tempRT);
                                }
                                else
                                {

                                    renderMPB.Clear();
                                    renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, record.depth);
                                    renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                                    renderMPB.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));
                                    renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));

                                    record.bakeShadowMapCB.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 3, renderMPB);


                                    record.restoreShadowMapCB.SetRenderTarget(pair.Value.linearDepth, pair.Value.linearDepth);
                                    record.restoreShadowMapCB.ClearRenderTarget(true, true, Color.white, 1.0f);
                                }
#else

                                // as the later blit is "additive", we need to explictly clear the linear depth buffer here
                                // but rather than using GL.Clear(), we use more reliable blit with custom shader to explicitly
                                // specify the depth to be infinite.
                                Graphics.Blit(null, record.linearDepth, m_LinearDepthMaterial, 1);

                                // iOS & Android seems to use encoded ARGB as depth format, in Unity 5.4 - 5.5. 
                                // Using Grpahics.Blit simply destroy from Depth format to RFloat simply destroys the depth info.
                                // So don't cast HVR shadow in such cases.
#if !UNITY_IOS && !UNITY_ANDROID

                                // blit from depth to linear depth(we don't have control over how depth is generated from HvrPlayerInterface so have to an additional blit)
                                // TODO: maybe we can modify the projection matrix passed to HvrPlayerInterface to linearize the depth generated.
                                m_LinearDepthMaterial.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                                m_LinearDepthMaterial.SetTexture(Uniforms.ShaderProperties._oDEP, record.depth);

                                m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._CurrLightShadowBias, new Vector4(m_DependentLight.shadowBias, 0, 0, 0));
                                m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._CurrLightPositionRange, new Vector4(p.x, p.y, p.z, 1.0f / effectiveRange));
                                m_LinearDepthMaterial.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0.0f));

                                if (!m_PointLightUsingNonlinearDepth)
                                {
                                    Graphics.Blit(record.depth, record.linearDepth, m_LinearDepthMaterial, 0);
                                }
                                else
                                {
                                    // 2017.3 onwards with D3D uses non-linear depth, so just blit without linearization
                                    Graphics.Blit(record.depth, record.linearDepth);
                                }

                                renderMPB.Clear();
                                renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, record.linearDepth);
                                renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(cubeMapSize, cubeMapSize, 1.0f, 0));

                                m_CommandBufferBakeSM.SetRenderTarget(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, 0, cubemapFace);

                                // IMPORTANT: In Unity 5.4.4 the implementation of cubemap doesn't have real 'depth', so basically it's 6 RFloat textures consist of cubemap.
                                // The problem is, we don't have read access to the rendered cubemap, and thus we can't use Z buffer or even in-shader comparison to bake
                                // depth properly. Besides the depth map used is also incorrect.
                                // The following trick only works in one-pointlight case: call ClearRenderTarget with only depth flag on, thus eliminate incorrect depth buffer issue,
                                // then bake the linear depth using "Less" mode in Z test. This will results a depth map with incorrect occlusion but turns out okay when rendered with 
                                // only one point light.
                                m_CommandBufferBakeSM.ClearRenderTarget(true, false, Color.red, 1.0f);
                                m_CommandBufferBakeSM.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 1, renderMPB);
#endif

#endif
                            }
                        }
                        else
                        {
#if UNITY_5_6_OR_NEWER
                            // Also clear the linearDepth as HvrRender will need it for every cubemap faces.
                            record.restoreShadowMapCB.SetRenderTarget(record.linearDepth, record.linearDepth);
                            record.restoreShadowMapCB.ClearRenderTarget(true, true, Color.white, 1.0f);
#else

                            // Use a special blit to clear the linear "depth", which depends on whether Z is configured to be reversed or not,
                            // rather than GL.Clear()
                            Graphics.Blit(null, record.linearDepth, m_LinearDepthMaterial, 1);
#endif
                        }
                    }
                }
                else
                {
                    // no actor will be rendered at all, but cleaning the command buffer to remove artifacts in the editor scene
                    foreach (KeyValuePair<CubemapFace, ViewportRecord> pair in m_PointLightCubemapBookkeep)
                    {
                        ViewportRecord record = pair.Value;
#if UNITY_5_6_OR_NEWER                        
                        record.bakeShadowMapCB.Clear();
                        record.restoreShadowMapCB.Clear();
#endif
                        Graphics.Blit(null, record.linearDepth, m_LinearDepthMaterial, 1);
                    }
                }
                #endregion // Pointlight
            }
            else
            if (lightType == LightType.Directional)
            {
                #region Directional
#if UNITY_2017_1_OR_NEWER                
                if (QualitySettings.shadows != UnityEngine.ShadowQuality.Disable)
                {

                    int i;
                    Camera cam = CameraHelper.GetMainCamera();
                    if (cam != null)
                    {
                        float[] splitPercent;
                        Rect[] cascadeShadowRect;
                        Matrix4x4[] toShadowUVSpace;
                        GetDirectionalLightSplitPercentAndShadowParams(cam, out splitPercent, out cascadeShadowRect, out toShadowUVSpace);


                        Rect pixelRect;
#if UNITY_EDITOR
                        pixelRect = EditorHelper.GetMainGameViewRect();
#else
                        pixelRect = cam.pixelRect;
#endif


                        int shadowSplit = splitPercent.Length;
                        bool requiresTransparency = false;
                        for (i = 0; i < shadowSplit; ++i)
                        {
                            if (directionalLightCachedActors[i] == null)
                                continue;

                            foreach (ActorShadowMapPair actorShadowMap in directionalLightCachedActors[i])
                            {
                                if (actorShadowMap.actor.material != null &&
                                    MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                                {
                                    requiresTransparency = true;
                                    break;
                                }
                            }

                            if (requiresTransparency)
                                break;
                        }


                        // construct the quad mesh here as the camera must be valid for the mesh to generate ray data.
                        if (m_ScreenQuadForMask == null)
                        {
                            m_ScreenQuadForMask = CompositeBufferUtils.GenerateQuadForScreenspaceShadowMask(cam);
                            m_ScreenQuadForMask.hideFlags = HideFlags.HideAndDontSave;
                        }

                        if (requiresTransparency)
                        {
                            // Part 1. Render shadow map to separated target, and then accumulate to m_ShadowmapRT
                            for (i = 0; i < shadowSplit; ++i)
                            {
                                if (directionalLightCachedActors[i] == null)
                                    continue;

                                foreach (ActorShadowMapPair actorShadowMap in directionalLightCachedActors[i])
                                {
                                    RenderTexture colormap, depthmap;
                                    RequestRenderTextureColorDepthPair(m_ShadowmapRT.width, m_ShadowmapRT.height, out colormap, out depthmap);
                                    actorShadowMap.shadowmap = RequestShadowMap(actorShadowMap.actor, CubemapFace.Unknown, m_ShadowmapRT.width, m_ShadowmapRT.height);

                                    Graphics.SetRenderTarget(depthmap.colorBuffer, depthmap.depthBuffer);
                                    GL.Clear(true, true, Color.white, 1.0f);

                                    for (i = 0; i < shadowSplit; ++i)
                                    {
                                        Rect rect = cascadeShadowRect[i];

                                        // NOTE: looks like ViewportInterface.SetDimensions doesn't work. Use low level GL api to override it
                                        // It's still not working in Engine 1.1
                                        GL.Viewport(rect);

                                        HvrScene.Player_RenderActor(
                                            actorShadowMap.actor.actorInterface.handle, directionalLightViewport[i].handle, null, depthmap.depthBuffer.GetNativeRenderBufferPtr);
                                    }

                                    GL.Viewport(pixelRect);

                                    Graphics.Blit(depthmap, actorShadowMap.shadowmap);
                                    ReleaseRenderTextureColorDepthPair(colormap, depthmap);
                                }
                            }
                            // restore GL.Viewport
                            GL.Viewport(pixelRect);

#if UNITY_2017_2_OR_NEWER
                            m_CommandBufferBakeSM.SetRenderTarget(m_ShadowmapRT.colorBuffer, m_ShadowmapRT.depthBuffer);
#else
                            m_CommandBufferBakeSM.SetRenderTarget(m_ShadowmapRT, m_ShadowmapRT);
#endif

                            m_CommandBufferBakeSM.ClearRenderTarget(true, true, Color.black);

                            renderMPB.Clear();
                            // blit all actorShadowMap to m_ShadowmapRT with respect to their transparency, additively   
                            for (i = 0; i < shadowSplit; ++i)
                            {
                                if (directionalLightCachedActors[i] == null)
                                    continue;

                                foreach (ActorShadowMapPair actorShadowMap in directionalLightCachedActors[i])
                                {
                                    if (actorShadowMap.shadowmap == null)
                                        continue;

                                    if (actorShadowMap.actor.material != null &&
                                        MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                                    {
                                        renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(m_ShadowmapRT.width, m_ShadowmapRT.height, MaterialHelper.GetMaterialTransparencyValue(actorShadowMap.actor.material), 0));
                                    }
                                    else
                                    {
                                        renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(m_ShadowmapRT.width, m_ShadowmapRT.height, 1.0f, 0));
                                    }
                                    renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, actorShadowMap.shadowmap);
                                    m_CommandBufferBakeSM.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 0, renderMPB);
                                }
                            }

                        }
                        else
                        {
                            Graphics.SetRenderTarget(m_ShadowmapRT.colorBuffer, m_ShadowmapRT.depthBuffer);
                            GL.Clear(true, true, Color.black);

                            // Part 1. Render shadow map to target
                            // int shadowSplit = splitPercent.Length;
                            for (i = 0; i < shadowSplit; ++i)
                            {
                                Rect rect = cascadeShadowRect[i];

                                // NOTE: looks like ViewportInterface.SetDimensions doesn't work. Use low level GL api to override it
                                // It's still not working in Engine 1.1
                                GL.Viewport(rect);

                                if (directionalLightCachedActors[i] == null)
                                    continue;

                                foreach (ActorShadowMapPair actorShadowMap in directionalLightCachedActors[i])
                                {
                                    HvrScene.Player_RenderActor(
                                        actorShadowMap.actor.actorInterface.handle, directionalLightViewport[i].handle, null, m_ShadowmapRT.depthBuffer.GetNativeRenderBufferPtr);
                                }
                            }
                        }



                        // Part 2. Pass shadow map parameter to shader and sample shadow in the post screenspace shadow mask shader, 
                        // in order to mask the active render target(screen space mask)

                        // Only create ScreenspaceHvrShadowMaskTexture here when we know the dimension of the rendering camera
                        if (m_screenspaceHvrShadowMaskTexture != null &&
                            (m_screenspaceHvrShadowMaskTexture.width != pixelRect.width || m_screenspaceHvrShadowMaskTexture.height != pixelRect.height))
                        {
                            m_screenspaceHvrShadowMaskTexture.Release();
                            m_screenspaceHvrShadowMaskTexture = null;
                        }

                        if (m_screenspaceHvrShadowMaskTexture == null)
                        {
                            m_screenspaceHvrShadowMaskTexture = new RenderTexture((int)pixelRect.width, (int)pixelRect.height, 16, RenderTextureFormat.ARGB32)
                            {
                                name = "Copy of screen space HVR shadow mask",
                                autoGenerateMips = false,
                                anisoLevel = 0,
                                filterMode = FilterMode.Point,
                                isPowerOfTwo = false,
                                useMipMap = false
                            };
                            m_screenspaceHvrShadowMaskTexture.Create();
                        }

                        m_CommandBufferScreenspaceShadowMask.Clear();

                        renderMPB.Clear();
                        renderMPB.SetTexture(Uniforms.ShaderProperties._Builtin_ShadowMapTexture, m_ShadowmapRT);
                        renderMPB.SetVector(Uniforms.ShaderProperties._Builtin_ShadowMapTexture_TexelSize, new Vector4(1.0f / m_ShadowmapRT.width, 1.0f / m_ShadowmapRT.height, m_ShadowmapRT.width, m_ShadowmapRT.height));

                        Matrix4x4[] worldToShadowArray = new Matrix4x4[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];
                        Vector4[] splitSpherePos = new Vector4[MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM];
                        Vector4 splitSphereSqRadii = new Vector4();
                        // Important to this clip->uv space transform, as the projection matrix only put the projected fragment to [-1, 1] range,
                        // but sampling in shader requires [0, 1]
                        for (i = 0; i < shadowSplit; ++i)
                        {
                            worldToShadowArray[i] = toShadowUVSpace[i] * m_CachedDirectionalLightViewProjectionMatrix[i];

                            Vector3 c = m_CachedBoundingSphere[i].position;
                            splitSpherePos[i] = new Vector4(c.x, c.y, c.z, 1);
                            splitSphereSqRadii[i] = m_CachedBoundingSphere[i].radius * m_CachedBoundingSphere[i].radius;
                        }
                        // fill with inf
                        for (; i < MAX_DIRECTIONAL_SHADOWMAP_CASCADE_NUM; ++i)
                        {
                            worldToShadowArray[i] = Matrix4x4.zero;
                            splitSpherePos[i] = Vector4.negativeInfinity;
                            splitSphereSqRadii[i] = float.MaxValue;
                        }

                        renderMPB.SetMatrixArray(Uniforms.ShaderProperties._Builtin_unity_WorldToShadow, worldToShadowArray);
                        renderMPB.SetVectorArray(Uniforms.ShaderProperties._Builtin_unity_ShadowSplitSpheres, splitSpherePos);
                        renderMPB.SetVector(Uniforms.ShaderProperties._Builtin_unity_ShadowSplitSqRadii, splitSphereSqRadii);
                        renderMPB.SetVector(Uniforms.ShaderProperties._ShadowFadeCameraDistance, new Vector4(Mathf.Min(ShadowDistanceLimit, QualitySettings.shadowDistance), 3.0f, 0, 0));

                        // We don't support pre-SM3 GPUs so only 0 or 2 for pass index
                        int shadowPass = 2;
#if UNITY_2017_2_OR_NEWER
                        if (XRSettings.enabled &&
#else
                        if (VRSettings.enabled &&
#endif
                            cam.stereoEnabled)
                        {
                            // VR should use pass 3, this behaviour aligns with Unity official Hidden/Internal-ScreenSpaceShadows shader, 
                            // though pass index is replaced with subshader index
                            // Pass 2 seems to have optimisation not compatible with VR/multi pass rendering
                            shadowPass = 3;
                        }

                        if (
                            QualitySettings.shadows == UnityEngine.ShadowQuality.HardOnly ||
                            m_DependentLight.shadows == LightShadows.Hard
                            )
                        {
                            shadowPass = 0;
                        }

                        // save it for later modulating with the color image in composition stage(actor receiving shadow).
                        m_CommandBufferScreenspaceShadowMask.DrawMesh(m_ScreenQuadForMask, Matrix4x4.identity, m_ScreenspaceShadowMask, 0, shadowPass, renderMPB);
                        m_CommandBufferScreenspaceShadowMask.Blit(BuiltinRenderTextureType.CurrentActive, ScreenspaceHvrShadowMaskTexture);

                    }
                }
#endif                

                #endregion
            }
            else
            if (lightType == LightType.Spot)
            {
                #region SpotLight

                // Clear the depth to infinite far
                // m_DepthRT.DiscardContents();
                Graphics.SetRenderTarget(m_DepthRT.colorBuffer, m_DepthRT.depthBuffer);
                Graphics.Blit(null, m_DepthRT, m_LinearDepthMaterial, 1);

                // Render each actor separately
                var actorList = spotLightCachedActors;

                if (actorList != null && actorList.Count > 0 &&
                    m_lightIsRealtime)
                {
                    bool requiresTransparency = false;
                    foreach (ActorShadowMapPair actorShadowMap in actorList)
                    {
                        if (actorShadowMap.actor.material != null &&
                            MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                        {
                            requiresTransparency = true;
                            break;
                        }
                    }

                    if (requiresTransparency)
                    {
                        foreach (ActorShadowMapPair actorShadowMap in actorList)
                        {
                            RenderTexture colormap, depthmap;
                            RequestRenderTextureColorDepthPair(mapSize, mapSize, out colormap, out depthmap);
                            actorShadowMap.shadowmap = RequestShadowMap(actorShadowMap.actor, CubemapFace.Unknown, mapSize, mapSize);

                            Graphics.SetRenderTarget(depthmap.colorBuffer, depthmap.depthBuffer);
                            GL.Clear(true, true, Color.white, 1.0f);

                            if (depthmap.IsCreated())
                                HvrScene.Player_RenderActor(actorShadowMap.actor.actorInterface.handle, spotLightViewport.handle, null, depthmap.depthBuffer.GetNativeRenderBufferPtr);

                            Graphics.Blit(depthmap, actorShadowMap.shadowmap);
                            ReleaseRenderTextureColorDepthPair(colormap, depthmap);
                        }

                        renderMPB.Clear();
                        // blit all actorShadowMap to m_DepthRT with respect to their transparency, additively   
                        foreach (ActorShadowMapPair actorShadowMap in actorList)
                        {
                            if (actorShadowMap.actor.material != null &&
                                MaterialHelper.RenderQueueIsTransparent(actorShadowMap.actor.material))
                            {
                                renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(mapSize, mapSize, MaterialHelper.GetMaterialTransparencyValue(actorShadowMap.actor.material), 0));
                            }
                            else
                            {
                                renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(mapSize, mapSize, 1.0f, 0));
                            }
                            renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, actorShadowMap.shadowmap);
                            m_CommandBufferBakeSM.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 0, renderMPB);
                        }
                    }
                    else
                    {
                        foreach (ActorShadowMapPair actorShadowMap in actorList)
                        {
                            if (m_DepthRT.IsCreated())
                                HvrScene.Player_RenderActor(actorShadowMap.actor.actorInterface.handle, spotLightViewport.handle, null, m_DepthRT.depthBuffer.GetNativeRenderBufferPtr);
                        }

                        // just one blit
                        renderMPB.Clear();
                        renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, m_DepthRT);
                        renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, new Vector4(mapSize, mapSize, 1.0f, 0));

                        m_CommandBufferBakeSM.DrawMesh(m_CasterMesh, Matrix4x4.identity, m_CasterMaterial, 0, 0, renderMPB);
                    }
                }

                m_CommandBufferCopyDepth.Clear();

                RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
                m_CommandBufferCopyDepth.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
                m_CommandBufferCopyDepth.SetGlobalTexture(Uniforms.ShaderProperties._MainTex, shadowmap);
                m_CommandBufferCopyDepth.Blit(shadowmap, m_DepthWithSceneRT);

                #endregion // Spotlight
            }

            Graphics.SetRenderTarget(originalColorBuffer, originalDepthBuffer);
        }

    }


}
