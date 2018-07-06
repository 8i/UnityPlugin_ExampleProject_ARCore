using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HVR.Interface;
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
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("8i/HvrRender")]

    public class HvrRender : MonoBehaviour
    {
        #region Public Properties

        public class RenderBundle : IComparable<RenderBundle>
        {
            public HvrActor actor { get { return m_actor; } }
            public Dictionary<CameraMonoOrStereoEye, HvrBuffers> stereoBuffers { get { return m_stereoBuffers; } }
            public CullingHelper cullingHelper { get { return m_cullingHelper; } }
            public bool isVisible { get { return m_isVisible; } }

            private HvrActor m_actor;
            public CullingHelper m_cullingHelper;
            private Dictionary<CameraMonoOrStereoEye, HvrBuffers> m_stereoBuffers;
            private float m_distanceToCamera = 0;
            // The random value here is used when sorting the draw order of the renderbundles.
            // It is used to correct zfighting when actors are rendered on top of each other
            private float m_zBias;
            private bool m_isVisible = true;

            public RenderBundle(HvrActor actor)
            {
                m_actor = actor;

                // TODO change this so the scale number isn't a factor.
                // This may cause issues when HvrActors are less than 0.001 in scale - Tom
                m_zBias = UnityEngine.Random.Range(-0.001f, 0.001f);

                m_stereoBuffers = new Dictionary<CameraMonoOrStereoEye, HvrBuffers>();

                m_cullingHelper = new CullingHelper();
            }

            public HvrBuffers GetBuffers(Camera cam)
            {
                CameraMonoOrStereoEye eye = GetMonoOrStereoEye(cam);

                if (!m_stereoBuffers.ContainsKey(eye))
                    m_stereoBuffers[eye] = new HvrBuffers(cam.pixelWidth, cam.pixelHeight);

                HvrBuffers buffers = m_stereoBuffers[eye];

                // Ensure buffers match current camera resolution
                if (buffers.bufferWidth != cam.pixelWidth ||
                    buffers.bufferHeight != cam.pixelHeight)
                {
                    buffers.Dispose();
                    buffers = new HvrBuffers(cam.pixelWidth, cam.pixelHeight);
                    m_stereoBuffers[eye] = buffers;
                }

                return buffers;
            }

            public void Dispose()
            {
                if (m_stereoBuffers != null)
                {
                    foreach (HvrBuffers buffers in m_stereoBuffers.Values)
                    {
                        if (buffers != null)
                            buffers.Dispose();
                    }
                    m_stereoBuffers = null;
                }

                if (m_cullingHelper != null)
                {
                    m_cullingHelper.Dispose();
                    m_cullingHelper = null;
                }
            }

            public int CompareTo(RenderBundle other)
            {
                return (other.m_distanceToCamera).CompareTo(m_distanceToCamera);
            }

            public void UpdateRenderDistance(Transform trans)
            {
                m_distanceToCamera = Vector3.Distance(m_actor.transform.position, trans.position);
                m_distanceToCamera += m_zBias;
            }

            public void UpdateVisibility(Camera cam)
            {
                if (m_actor.occlusionCullingEnabled)
                {
                    if (m_actor.assetInterface != null)
                    {
                        // Update the culling helper for this actor
                        cullingHelper.Update(
                            m_actor.transform,
                            m_actor.assetInterface.GetBounds(),
                            m_actor.occlusionCullingMultipler,
                            cam
                        );
                    }

                    m_isVisible = cullingHelper.IsVisible();
                }
                else
                {
                    m_isVisible = true;
                }
            }
        }

        private class CommandBufferSet
        {
            public CommandBuffer opaque;
            public CommandBuffer opaqueComposite;
            public CommandBuffer transparent;
            public CommandBuffer transparentComposite;
            public CommandBuffer depth;

            public CommandBufferSet()
            {
                opaque = new CommandBuffer()
                { name = "8i.HvrRender.Opaque" };

                opaqueComposite = new CommandBuffer()
                { name = "8i.HvrRender.Opaque.Composite" };

                transparent = new CommandBuffer()
                { name = "8i.HvrRender.Transparent" };

                transparentComposite = new CommandBuffer()
                { name = "8i.HvrRender.Transparent.Composite" };

                depth = new CommandBuffer()
                { name = "8i.HvrRender.Depth" };
            }

            public void Clear()
            {
                opaque.Clear();
                opaqueComposite.Clear();
                transparent.Clear();
                transparentComposite.Clear();
                depth.Clear();
            }

            public void Release()
            {
                opaque.Release();
                opaqueComposite.Release();
                transparent.Release();
                transparentComposite.Release();
                depth.Release();
            }
        }

        public List<RenderBundle> renderBundles { get { return m_renderBundles; } }

        public enum eMode
        {
            standard,
            direct
        };

        /// <summary>
        /// Render mode within Unity
        /// </summary>
        public eMode mode;

        #endregion

        #region Private Members

        private Camera cam
        {
            get
            {
                if (m_cam == null)
                    m_cam = GetComponent<Camera>();

                return m_cam;
            }
        }

        private Camera m_cam;

        const CameraEvent evt_depthComposite = CameraEvent.AfterDepthTexture;
        const CameraEvent evt_forward_opaqueClearFlagOthers = CameraEvent.AfterForwardOpaque;
        const CameraEvent evt_forward_opaqueClearFlagSkybox = CameraEvent.AfterSkybox;
        const CameraEvent evt_deffered_opaque = CameraEvent.BeforeImageEffectsOpaque;
        const CameraEvent evt_transparent = CameraEvent.AfterForwardAlpha;

        private CommandBufferSet cmdbuf_set;

        private Material mat_cmdBuf_depthComposite;
        private Material mat_cmdBuf_colorComposite;

        private Texture2D m_dummyShadowMaskTexture;
        private Cubemap m_dummyCubemapCookie;
        private Mesh m_mesh_quad;

        private int m_currentMSAALevel = 0;
        private RenderingPath m_currentRenderPath;
        private CameraClearFlags m_cameraClearFlags;

        private List<RenderBundle> m_renderBundles = new List<RenderBundle>();


        public enum CameraMonoOrStereoEye
        {
            Left = 0,
            Right = 1,
            Mono = 2
        }

        private Dictionary<CameraMonoOrStereoEye, ViewportInterface> m_stereoViewports = new Dictionary<CameraMonoOrStereoEye, ViewportInterface>();

        #endregion

        #region Monobehaviour Functions

        private void OnEnable()
        {
#if UNITY_EDITOR
            // In the case that this component is a prefab, don't allow an asset to be created
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;
#endif

            m_currentRenderPath = cam.actualRenderingPath;
            m_cameraClearFlags = cam.clearFlags;
            m_currentMSAALevel = QualitySettings.antiAliasing;

            HvrScene.onActorAdded -= AddHvrActor;
            HvrScene.onActorAdded += AddHvrActor;

            HvrScene.onActorRemoved -= RemoveActor;
            HvrScene.onActorRemoved += RemoveActor;

            CheckResources();

            HvrScene.Add(this);
        }

        private void OnDisable()
        {
            ReleaseResources();

            HvrScene.Remove(this);
        }

        private void OnDestroy()
        {
            HvrScene.onActorAdded -= AddHvrActor;
            HvrScene.onActorRemoved -= RemoveActor;

            ReleaseResources();

            HvrScene.Remove(this);
        }

        private void OnApplicationQuit()
        {
            HvrScene.onActorAdded -= AddHvrActor;
            HvrScene.onActorRemoved -= RemoveActor;

            ReleaseResources();

            HvrScene.Remove(this);
        }

        private void OnPreCull()
        {
            for (int i = 0; i < m_renderBundles.Count; i++)
            {
                RenderBundle bundle = m_renderBundles[i];
                bundle.UpdateVisibility(cam);
                bundle.UpdateRenderDistance(cam.transform);
            }

            // Sort the bundle list by the distance from the actor to the camera
            // This solves transparent objects not sorting correctly when they are rendered
            // With this, actors are now submitted back to front - Tom
            // TODO - Change how this works so the renderBundles list is not ever jumbled each frame.
            m_renderBundles.Sort();
        }

        private void OnPreRender()
        {
            CheckResources();

            // Always clear the commandBuffers before submitting additional meshes to draw
            // otherwise the number of submitted meshes will build up every frame and kill the framerate - Tom
            // TODO - Look into whether those submitted meshes can be reused?
            cmdbuf_set.Clear();

            if (mode == eMode.standard)
            {
                UpdateViewports();
                HvrScene.Player_PrepareRender(GetHvrRenderColorBuffer, GetHvrRenderDepthBuffer);
                RenderStandardActors();
            }
        }

        private void OnPostRender()
        {
            if (mode == eMode.direct)
            {
                UpdateViewports();
                HvrScene.Player_PrepareRender(GetHvrRenderColorBuffer, GetHvrRenderDepthBuffer);
                RenderDirectActors();
            }
        }

        public void ForceRedraw()
        {
            CheckResources();

            cmdbuf_set.Clear();

            UpdateViewports();
            HvrScene.Player_PrepareRender(GetHvrRenderColorBuffer, GetHvrRenderDepthBuffer);
            if (mode == eMode.standard)
            {
                RenderStandardActors();
            }
            else
            {
                RenderDirectActors();
            }
        }

        private void LateUpdate()
        {
#if UNITY_2017_2_OR_NEWER
            if (XRDevice.isPresent && 
                XRDevice.userPresence != UserPresenceState.Present)
            {
                // Oculus will cause Unity stop the PreRender->Render->PostRender call
                // if user isn't present. However that will remove the call of Interface_Update()
                // together, which will cause memory allocation keeps growing.
                HvrPlayerInterface.Update();
            }
#endif            

        }

        #endregion

        #region HvrRender Functions

        // Handling Actors
        // ------------------------------

        public void AddHvrActor(HvrActor actor)
        {
            if (!ContainsActor(actor))
            {
                RenderBundle bundle = new RenderBundle(actor);
                m_renderBundles.Add(bundle);
            }
        }

        public void RemoveActor(HvrActor actor)
        {
            if (ContainsActor(actor))
            {
                RenderBundle bundle = renderBundles.Find(x => x.actor == actor);
                bundle.Dispose();
                m_renderBundles.Remove(bundle);
            }
        }

        public bool ContainsActor(HvrActor actor)
        {
            for (int i = 0; i < m_renderBundles.Count; i++)
            {
                if (m_renderBundles[i].actor == actor)
                    return true;
            }

            return false;
        }

        // Resources
        // ------------------------------

        private bool CheckResources()
        {
#if UNITY_EDITOR
            // There are issues with releasing unmanaged memory while running in batch mode for OSX and iOS
            // TODO: Remove this check
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return false;
#endif
            // In the case the rendering path has changed, then clear the attached command buffers
            // This is required as some of the command buffers require a particular CameraEvent based on the rendering path - Tom

            bool releaseResources = false;

            if (m_currentRenderPath != cam.actualRenderingPath)
            {
                m_currentRenderPath = cam.actualRenderingPath;
                releaseResources = true;
            }

            if (m_cameraClearFlags != cam.clearFlags)
            {
                m_cameraClearFlags = cam.clearFlags;
                releaseResources = true;
            }

            if (releaseResources)
            {
                ReleaseResources();

                List<HvrActor> actors = new List<HvrActor>();
                HvrScene.GetObjects(actors);

                foreach (HvrActor actor in actors)
                {
                    AddHvrActor(actor);
                }
            }

            if (m_mesh_quad == null)
            {
                m_mesh_quad = CompositeBufferUtils.GenerateQuad();
            }

            if (m_dummyShadowMaskTexture == null)
            {
                m_dummyShadowMaskTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false, false);

                Color[] col = new Color[4];
                for (int i = 0; i < col.Length; i++)
                    col[i] = Color.white;

                m_dummyShadowMaskTexture.SetPixels(col);
                m_dummyShadowMaskTexture.Apply();
            }

            if (m_dummyCubemapCookie == null)
            {
                Color[] faceColor = new Color[] { Color.white, Color.white, Color.white, Color.white };
                m_dummyCubemapCookie = new Cubemap(2, TextureFormat.ARGB32, false);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.NegativeX);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.PositiveX);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.PositiveY);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.NegativeY);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.NegativeZ);
                m_dummyCubemapCookie.SetPixels(faceColor, CubemapFace.PositiveZ);
                m_dummyCubemapCookie.Apply();
            }


            if (mat_cmdBuf_colorComposite == null)
            {
                Shader shader_HVRColorComposite = Resources.Load(Uniforms.ResourcePaths.shader_HVRColorComposite) as Shader;
                mat_cmdBuf_colorComposite = new Material(shader_HVRColorComposite);
            }

            if (mat_cmdBuf_depthComposite == null)
            {
                Shader shader_depthComposite = Resources.Load(Uniforms.ResourcePaths.shader_HVRRender_CommandBufferDepthComposite) as Shader;
                mat_cmdBuf_depthComposite = new Material(shader_depthComposite);
            }

            if (cmdbuf_set == null)
            {
                cmdbuf_set = new CommandBufferSet();

                cam.AddCommandBuffer(evt_transparent, cmdbuf_set.transparent);
                cam.AddCommandBuffer(evt_transparent, cmdbuf_set.transparentComposite);

                if (m_currentMSAALevel <= 1)
                    cam.AddCommandBuffer(evt_depthComposite, cmdbuf_set.depth);

                if (cam.actualRenderingPath == RenderingPath.DeferredShading)
                {
                    // It appears that the deferred renderer under OpenGL4.5 cannot use AfterForwardOpaque as it's event
                    // In this case, use BeforeImageEffectsOpaque as it occurs just after AfterForwardOpaque - Tom
                    cam.AddCommandBuffer(evt_deffered_opaque, cmdbuf_set.opaque);
                    cam.AddCommandBuffer(evt_deffered_opaque, cmdbuf_set.opaqueComposite);
                }
                else
                {
#if UNITY_EDITOR
                    if (EditorHelper.IsSceneViewCamera(cam))
                    {
                        cam.AddCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaque);
                        cam.AddCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaqueComposite);
                    }
                    else
                    {
#endif
                        if (cam.clearFlags == CameraClearFlags.Skybox)
                        {
                            cam.AddCommandBuffer(evt_forward_opaqueClearFlagSkybox, cmdbuf_set.opaque);
                            cam.AddCommandBuffer(evt_forward_opaqueClearFlagSkybox, cmdbuf_set.opaqueComposite);
                        }
                        else
                        {
                            cam.AddCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaque);
                            cam.AddCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaqueComposite);
                        }
#if UNITY_EDITOR
                    }
#endif
                }
            }

            if (m_currentMSAALevel != QualitySettings.antiAliasing)
            {
                m_currentMSAALevel = QualitySettings.antiAliasing;

                cam.RemoveCommandBuffer(evt_depthComposite, cmdbuf_set.depth);

                if (m_currentMSAALevel <= 1)
                    cam.AddCommandBuffer(evt_depthComposite, cmdbuf_set.depth);
            }

            return true;
        }

        private void ReleaseResources()
        {
            if (m_renderBundles.Count > 0)
            {
                for (int i = 0; i < m_renderBundles.Count; i++)
                    m_renderBundles[i].Dispose();

                m_renderBundles.Clear();
            }

            if (m_stereoViewports.Count > 0)
            {
                foreach (KeyValuePair<CameraMonoOrStereoEye, ViewportInterface> pair in m_stereoViewports)
                    pair.Value.Delete();

                m_stereoViewports.Clear();
            }

            if (cmdbuf_set != null)
            {
                cam.RemoveCommandBuffer(evt_depthComposite, cmdbuf_set.depth);

                cam.RemoveCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaque);
                cam.RemoveCommandBuffer(evt_forward_opaqueClearFlagSkybox, cmdbuf_set.opaque);
                cam.RemoveCommandBuffer(evt_deffered_opaque, cmdbuf_set.opaque);

                cam.RemoveCommandBuffer(evt_transparent, cmdbuf_set.transparent);

                cam.RemoveCommandBuffer(evt_forward_opaqueClearFlagOthers, cmdbuf_set.opaqueComposite);
                cam.RemoveCommandBuffer(evt_forward_opaqueClearFlagSkybox, cmdbuf_set.opaqueComposite);
                cam.RemoveCommandBuffer(evt_deffered_opaque, cmdbuf_set.opaqueComposite);

                cam.RemoveCommandBuffer(evt_transparent, cmdbuf_set.transparentComposite);

                cmdbuf_set.Clear();
                cmdbuf_set.Release();
                cmdbuf_set = null;
            }


        }

        // Rendering
        // ------------------------------

        private static CameraMonoOrStereoEye GetMonoOrStereoEye(Camera cam)
        {
            CameraMonoOrStereoEye eye = CameraMonoOrStereoEye.Mono;

            // Is VR enabled?
            // Is this camera rendering from two virtual eye-points to a stereoscopic output?
#if UNITY_2017_2_OR_NEWER
            if (XRSettings.enabled &&
#else
            if (VRSettings.enabled &&
#endif
                cam.stereoEnabled)
            {
                switch (cam.stereoTargetEye)
                {
                    case StereoTargetEyeMask.Left:
                        eye = CameraMonoOrStereoEye.Left;
                        break;

                    case StereoTargetEyeMask.Right:
                        eye = CameraMonoOrStereoEye.Right;
                        break;

                    case StereoTargetEyeMask.Both:
#if UNITY_5_5_OR_NEWER
                        switch (cam.stereoActiveEye)
                        {
                            case Camera.MonoOrStereoscopicEye.Left:
                                eye = CameraMonoOrStereoEye.Left;
                                break;
                            case Camera.MonoOrStereoscopicEye.Right:
                                eye = CameraMonoOrStereoEye.Right;
                                break;
                            case Camera.MonoOrStereoscopicEye.Mono:
                                eye = CameraMonoOrStereoEye.Mono;
                                break;
                            default:
                                break;
                        }
#endif
                        break;

                    case StereoTargetEyeMask.None:
                        eye = CameraMonoOrStereoEye.Mono;
                        break;

                    default:
                        break;
                }
            }

            return eye;
        }

        private ViewportInterface GetViewport()
        {
            ViewportInterface viewport = null;

            CameraMonoOrStereoEye eye = GetMonoOrStereoEye(cam);

            if (!m_stereoViewports.ContainsKey(eye))
            {
                m_stereoViewports[eye] = new ViewportInterface();
            }

            viewport = m_stereoViewports[eye];

            return viewport;
        }

        private void UpdateViewports()
        {
            // Make sure proper viewport is created before updating it.
            GetViewport();

            foreach (KeyValuePair<CameraMonoOrStereoEye, ViewportInterface> pair in m_stereoViewports)
            {
                Matrix4x4 view = cam.worldToCameraMatrix;
                Matrix4x4 proj = cam.projectionMatrix;

                switch (pair.Key)
                {
                    case CameraMonoOrStereoEye.Left:
                        view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                        proj = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                        break;

                    case CameraMonoOrStereoEye.Right:
                        view = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                        proj = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                        break;

                    default:
                        view = cam.worldToCameraMatrix;
                        proj = cam.projectionMatrix;
                        break;
                }

                ViewportInterface viewport = pair.Value;

                bool renderToTexture = RenderTexture.active != null;

                // The renderIntoTexture value should be set to true if you intend to render into a RenderTexture
                // with this projection matrix. On some platforms it affects what the final matrix will look like.
                if (mode == eMode.standard)
                    renderToTexture = true;

                proj = GL.GetGPUProjectionMatrix(proj, renderToTexture);

                viewport.UpdateViewport(view, proj, cam.pixelRect);

                // HACKHACK: In Direct render always set sample as 1 to fallback onto the "vanilla" direct draw MSAA, otherwise the edge will
                // not be anti-aliased
                if (mode == eMode.direct)
                    viewport.SetSamples(1);
            }
        }

        public void WillRender()
        {
            if (m_renderBundles != null)
            {
                ViewportInterface viewport = GetViewport();

                for (int i = 0; i < m_renderBundles.Count; i++)
                {
                    RenderBundle bundle = m_renderBundles[i];

                    if (bundle.isVisible &&
                        bundle.actor.assetInterface != null)
                    {
                        HvrScene.Player_WillRender(bundle.actor.actorInterface.handle, viewport.handle);
                    }
                }
            }
        }

        private bool RenderActorIntoBuffers(HvrActor actor, HvrBuffers buffers, ViewportInterface viewport)
        {
            bool isMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            bool rendered = false;
            // Create a temporary render texture to render depth into.
            RenderTexture tempRT_Depth = RenderTexture.GetTemporary(buffers.depth.width, buffers.depth.height, 16, RenderTextureFormat.Depth);
            tempRT_Depth.Create();
            tempRT_Depth.DiscardContents();

            // Added for Unity 5.6 support on Android, and it's likely good practice in any case - Tom
            buffers.color.DiscardContents();
            buffers.depth.DiscardContents();

            // Store the previous render buffers so we can restore them later.
            RenderBuffer originalColorBuffer = Graphics.activeColorBuffer;
            RenderBuffer originalDepthBuffer = Graphics.activeDepthBuffer;

            // For some reason, Metal can't reliably fetch native texture pointer created on the current frame using CommandBuffer. Here we skipped the buffer created
            // on the fly and wait til next frame.
            if (buffers.createdFrameIndex == Time.frameCount && isMetal)
            {
                // Store the previous render buffers so we can restore them later.

                // Set the render targets so the HvrEngine can render into them
                Graphics.SetRenderTarget(buffers.color.colorBuffer, buffers.depth.depthBuffer);

                // Clear the render targets buffers
                GL.Clear(true, true, new Color(1.0f, 0.0f, 0.0f, 0.0f), 1.0f);

                Graphics.SetRenderTarget(originalColorBuffer, originalDepthBuffer);

                return false;
            }


            // -------------------------
            // Bind buffers and render the actor
            // -------------------------
            // Set the render targets so the HvrEngine can render into them
            Graphics.SetRenderTarget(buffers.color.colorBuffer, tempRT_Depth.depthBuffer);

            // Clear the render targets buffers
            GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f);

            viewport.SetSamples(QualitySettings.antiAliasing);

            // Render
            if (viewport != null)
            {
                if (buffers.color.IsCreated() && tempRT_Depth.IsCreated())
                {
                    HvrScene.Player_RenderActor(actor.actorInterface.handle, viewport.handle, buffers.color.colorBuffer.GetNativeRenderBufferPtr, tempRT_Depth.depthBuffer.GetNativeRenderBufferPtr);
                    rendered = true;
                }
            }

            // Blit the temporary depth into the persistent renderTextures
            Graphics.Blit(tempRT_Depth, buffers.depth);

            // Make sure to release the temporary depth buffer
            RenderTexture.ReleaseTemporary(tempRT_Depth);

            // Restore the previous render targets
            Graphics.SetRenderTarget(originalColorBuffer, originalDepthBuffer);

            // -------------------------
            // MODIFIERS
            // -------------------------

            // Color Grading
            if (actor.gameObject.GetComponent(Uniforms.componentNames.hvrColorGrading))
            {
                HvrActorColorGrade colorGrading = (HvrActorColorGrade)actor.gameObject.GetComponent(Uniforms.componentNames.hvrColorGrading);

                if (colorGrading != null && colorGrading.isActiveAndEnabled)
                {
                    colorGrading.Grade(buffers.color);
                }
            }

            // 3D Mask
            if (actor.GetComponent(Uniforms.componentNames.HvrActor3DMask))
            {
                HvrActor3DMask mask = (HvrActor3DMask)actor.GetComponent(Uniforms.componentNames.HvrActor3DMask);

                if (mask != null && mask.isActiveAndEnabled)
                {
                    if (mask.objects != null && mask.objects.Length != 0)
                    {
                        mask.Mask(cam, buffers.depth);
                    }
                }
            }
            return rendered;
        }

        private void RenderStandardActors()
        {
            // TODO: MSAA flag needed?
            // bool actorRenderedWithMultiSample = QualitySettings.antiAliasing > 1;

            // Pick out all the opaque and transparent render bundles from renderBundles, put them into separate lists
            // and render per actor base lighting. This prepass also filter out those disabled actors or
            // actors without materials.
            List<RenderBundle> opaqueRenderBundles = new List<RenderBundle>();
            List<RenderBundle> transparentRenderBundles = new List<RenderBundle>();

            for (int i = 0; i < renderBundles.Count; i++)
            {
                RenderBundle bundle = renderBundles[i];

                HvrActor actor = bundle.actor;

                // If the user has not specified a material, do not render
                // TODO Make this render a default material? - Tom
                if (actor.material == null)
                    continue;

                bool isVisible = actor.occlusionCullingEnabled ? bundle.cullingHelper.IsVisible() : true;

                if (actor.isActiveAndEnabled && 
                    actor.assetInterface != null &&
                    isVisible)
                {
                    if (MaterialHelper.RenderQueueIsTransparent(actor.material))
                    {
                        transparentRenderBundles.Add(bundle);
                    }
                    else
                    {
                        opaqueRenderBundles.Add(bundle);
                    }
                }
            }

            // NOTE: Only transparent objects need to be sorted by viewing distance
            // Sort the bundle list by the distance from the actor to the camera
            // This solves transparent objects not sorting correctly when they are rendered
            // With this, actors are now submitted back to front - Tom
            // TODO - Change how this works so the renderBundles list is not ever jumbled each frame.
            for (int i = 0; i < transparentRenderBundles.Count; i++)
            {
                RenderBundle bundle = transparentRenderBundles[i];
                bundle.UpdateRenderDistance(cam.transform);
            }
            transparentRenderBundles.Sort();

            // 1. Render all opaque bundles
            _DoRenderStandardActors(opaqueRenderBundles, cmdbuf_set.opaque, cmdbuf_set.opaqueComposite, (Material material) =>
            {
                return 0;
            }, false);

            // 2. Render all transparent bundles
            _DoRenderStandardActors(transparentRenderBundles, cmdbuf_set.transparent, cmdbuf_set.transparentComposite, (Material material) =>
            {
                if (material.IsKeywordEnabled("TRANSPARENCY_DITHER"))
                {
                    return 2;
                }
                else
                {
                    return 1;
                }
            }, true);

            // 3. render opaque objects into camera target depth to enable post processing
            for (int i = 0; i < opaqueRenderBundles.Count; ++i)
            {
                RenderBundle bundle = opaqueRenderBundles[i];
                HvrBuffers buffers = bundle.GetBuffers(cam);

                if (bundle.actor.assetInterface != null)
                {
                    MaterialPropertyBlock renderMPB = new MaterialPropertyBlock();
                    renderMPB.SetTexture(Uniforms.ShaderProperties._oDEP, buffers.depth);

                    cmdbuf_set.depth.DrawMesh(m_mesh_quad, Matrix4x4.identity, mat_cmdBuf_depthComposite, 0, 0, renderMPB);
                }
            }
        }

        private delegate int DoRenderStandardActorMaterialPassSelector(Material material);

        // Abstracted from RenderStandardActors() to reuse some code...
        private void _DoRenderStandardActors(List<RenderBundle> renderBundleList, CommandBuffer cmdBuf, CommandBuffer cmdBufComposite, DoRenderStandardActorMaterialPassSelector passSelector, bool isTransparent)
        {
            bool isMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            MaterialPropertyBlock renderMPB = new MaterialPropertyBlock();

            List<HvrLight> hvrLights = new List<HvrLight>();
            HvrScene.GetObjects(hvrLights);

            Matrix4x4 inverseViewProjection = (cam.projectionMatrix * cam.worldToCameraMatrix).inverse;
            ViewportInterface viewport = GetViewport();

            bool rendered = true;

            for (int i = 0; i < renderBundleList.Count; i++)
            {
                RenderBundle bundle = renderBundleList[i];
                HvrBuffers buffers = bundle.GetBuffers(cam);

                HvrActor actor = bundle.actor;

                if (actor.assetInterface == null)
                {
                    rendered = false;
                    continue;
                }

                Mesh renderMesh = actor.renderMesh;
                Matrix4x4 mat4x4 = actor.transform.localToWorldMatrix;

                if (actor.useScreenSpaceQuad)
                {
                    renderMesh = m_mesh_quad;
                }

                Matrix4x4 worldToObjectSpace = actor.transform.worldToLocalMatrix;
                if (!RenderActorIntoBuffers(bundle.actor, buffers, viewport))
                {
                    rendered = false;
                    continue;
                }

                if (!isMetal)
                {
                    // Reuse HvrBuffers.colorCopy as it's of no use now
                    cmdBuf.SetRenderTarget(buffers.colorCopy);
                    cmdBuf.ClearRenderTarget(true, true, Color.black);


                    renderMPB.Clear();
                    renderMPB.SetTexture(Uniforms.ShaderProperties._HvrColorTex, buffers.color);
                    renderMPB.SetTexture(Uniforms.ShaderProperties._HvrDepthTex, buffers.depth);
                    renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);

                    Material renderMaterial = actor.material;
                    int baseLightingPass;

                    if (actor.useScreenSpaceQuad)
                    {
                        renderMaterial.EnableKeyword("SCREENSPACE_QUAD");
                    }
                    else
                    {
                        renderMaterial.DisableKeyword("SCREENSPACE_QUAD");
                    }

#if UNITY_ANDROID || UNITY_IOS
                    renderMaterial.EnableKeyword("FIXED_AMBIENT_LIGHTING");
#else
                    renderMaterial.DisableKeyword("FIXED_AMBIENT_LIGHTING");
#endif

                    if (actor.useLighting)
                    {
                        baseLightingPass = 0;

                        renderMPB.SetVector(Uniforms.ShaderProperties._CenterWorldPos, actor.transform.localToWorldMatrix.MultiplyPoint(actor.assetInterface.GetBounds().center));
                        renderMPB.SetVector(Uniforms.ShaderProperties._CameraWorldPos, this.transform.position);
                        renderMPB.SetMatrix(Uniforms.ShaderProperties._WorldToObject, worldToObjectSpace);

                        SphericalHarmonicsL2 aSample;
                        LightProbes.GetInterpolatedProbe(actor.transform.localToWorldMatrix.MultiplyPoint(actor.assetInterface.GetBounds().center), null, out aSample);

                        // Vector3 lightProbeColor = LightProbeUtil.SampleLightProbe(actor.GetBounds().center + actor.transform.position, null, Quaternion.identity.eulerAngles);
                        // renderMPB.SetVector("_LightProbeSample", lightProbeColor);
                        renderMPB.SetFloatArray(Uniforms.ShaderProperties._SHFactorL2Red, new float[]{
                                aSample[0, 0],
                                aSample[0, 1],
                                aSample[0, 2],
                                aSample[0, 3],
                                aSample[0, 4],
                                aSample[0, 5],
                                aSample[0, 6],
                                aSample[0, 7],
                                aSample[0, 8]
                            });

                        renderMPB.SetFloatArray(Uniforms.ShaderProperties._SHFactorL2Green, new float[]{
                                aSample[1, 0],
                                aSample[1, 1],
                                aSample[1, 2],
                                aSample[1, 3],
                                aSample[1, 4],
                                aSample[1, 5],
                                aSample[1, 6],
                                aSample[1, 7],
                                aSample[1, 8]
                            });

                        renderMPB.SetFloatArray(Uniforms.ShaderProperties._SHFactorL2Blue, new float[]{
                                aSample[2, 0],
                                aSample[2, 1],
                                aSample[2, 2],
                                aSample[2, 3],
                                aSample[2, 4],
                                aSample[2, 5],
                                aSample[2, 6],
                                aSample[2, 7],
                                aSample[2, 8]
                            });
                    }
                    else
                    {
                        baseLightingPass = 5;
                    }

                    // Handle case where HvrActor is using a custom material
                    if (actor.material.passCount - 1 < baseLightingPass)
                        baseLightingPass = 0;

                    cmdBuf.DrawMesh(renderMesh, mat4x4, renderMaterial, 0, baseLightingPass, renderMPB);

                    if (actor.useLighting)
                    {
                        actor.material.EnableKeyword("SUPPORT_SHADOWING");

                        // Standard lighting and shadowing pass
                        foreach (HvrLight shadowcaster in hvrLights)
                        {
                            Light dependentLight = shadowcaster.GetDependentLight();

                            if (!dependentLight.enabled ||
                                !shadowcaster.enabled)
                            {
                                continue;
                            }

                            // Do not influence rendering if this light is not a realtime light
                            if (!shadowcaster.IsAttachedToRealtimeLight())
                                continue;

                            if (!Helper.IsLayerVisibleInCullingMask(actor.gameObject.layer, dependentLight.cullingMask))
                                continue;

                            shadowcaster.Render();

                            // No need to render to buffer here since all buffers should be cached by the previous base lighting pass
                            renderMPB.Clear();
                            renderMPB.SetTexture(Uniforms.ShaderProperties._HvrColorTex, buffers.color);
                            renderMPB.SetTexture(Uniforms.ShaderProperties._HvrDepthTex, buffers.depth);
                            renderMPB.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
                            renderMPB.SetVector(Uniforms.ShaderProperties._CenterWorldPos, actor.transform.localToWorldMatrix.MultiplyPoint(actor.assetInterface.GetBounds().center));
                            renderMPB.SetVector(Uniforms.ShaderProperties._CameraWorldPos, this.transform.position);

                            Vector3 lightPosWorld = dependentLight.transform.position;
                            renderMPB.SetVector(Uniforms.ShaderProperties._LightWorldPos, new Vector4(lightPosWorld.x, lightPosWorld.y, lightPosWorld.z, 1.0f));

                            Vector3 lightDir = dependentLight.transform.forward;
                            renderMPB.SetVector(Uniforms.ShaderProperties._LightWorldDir, new Vector4(lightDir.x, lightDir.y, lightDir.z, 1.0f));
                            renderMPB.SetVector(Uniforms.ShaderProperties._LightColorAtSource, (Vector4)dependentLight.color);

                            renderMPB.SetVector(Uniforms.ShaderProperties._LightParameters, new Vector4(dependentLight.intensity, dependentLight.shadowStrength, Mathf.Deg2Rad * dependentLight.spotAngle, 1.0f / dependentLight.range));

                            Vector4 shadowParameter = new Vector4();

                            if (actor.receiveShadows &&
                                dependentLight.shadows != LightShadows.None &&
#if UNITY_5_5_OR_NEWER
                                QualitySettings.shadows != ShadowQuality.Disable
#else
                                // Can't find global shadow quality control in 5.4 although it appears in Quality settings inspector
                                QualitySettings.pixelLightCount > 0
#endif
                                )
                            {
                                shadowParameter.x = 1.0f;
                            }

                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, shadowParameter);
                            mat4x4 = actor.transform.localToWorldMatrix;
                            int pass;
                            switch (dependentLight.type)
                            {
                                case LightType.Directional:
                                    {
                                        pass = 2;
#if UNITY_2017_1_OR_NEWER
                                        if (!isTransparent && actor.receiveShadows && shadowcaster.ScreenspaceHvrShadowMaskTexture != null && shadowcaster.ScreenspaceHvrShadowMaskTexture.IsCreated())
                                        {
#if UNITY_EDITOR
                                            Rect pixelRect = EditorHelper.GetMainGameViewRect();
                                            if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono)
                                            {
                                                // Editor without VR
                                                if (pixelRect.width == cam.pixelRect.width &&
                                                    pixelRect.height == cam.pixelRect.height)
                                                {
                                                    renderMPB.SetTexture(Uniforms.ShaderProperties._ScreenspaceHvrMaskTexture, shadowcaster.ScreenspaceHvrShadowMaskTexture);
                                                }
                                                else
                                                {
                                                    
                                                    renderMPB.SetTexture(Uniforms.ShaderProperties._ScreenspaceHvrMaskTexture, m_dummyShadowMaskTexture);
                                                }
                                            }
                                            else
                                            {
                                                // Editor with VR, ignores the view dimension checking so that the screen space shadow renders
                                                renderMPB.SetTexture(Uniforms.ShaderProperties._ScreenspaceHvrMaskTexture, shadowcaster.ScreenspaceHvrShadowMaskTexture);
                                            }
#else
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._ScreenspaceHvrMaskTexture, shadowcaster.ScreenspaceHvrShadowMaskTexture);
#endif
                                            
                                        }
                                        else
#endif
                                        {
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._ScreenspaceHvrMaskTexture, m_dummyShadowMaskTexture);
                                        }


                                        break;
                                    }

                                case LightType.Point:
                                    {

                                        pass = 3;
#if UNITY_2017_3_OR_NEWER
                                        if (Helper.GetRenderingAPI().StartsWith("Direct"))
                                        {
                                            pass = 4;
                                        }
#endif

                                        if (dependentLight.cookie != null)
                                        {
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._LightCookieCube, dependentLight.cookie);
                                        }
                                        else
                                        {
                                            if (m_dummyCubemapCookie != null)
                                                renderMPB.SetTexture(Uniforms.ShaderProperties._LightCookieCube, m_dummyCubemapCookie);
                                        }

                                        renderMPB.SetMatrix(Uniforms.ShaderProperties._WorldToLightSpace, dependentLight.transform.worldToLocalMatrix);

                                        shadowcaster.IterateAllCubemapFaces((cubemapFace) =>
                                        {
                                            int shaderFromWorldToShadowName;
                                            int shaderLightDepthMap;
                                            if (cubemapFace == CubemapFace.PositiveX)
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowPX;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapPX;
                                            }
                                            else if (cubemapFace == CubemapFace.NegativeX)
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowNX;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapNX;
                                            }
                                            else if (cubemapFace == CubemapFace.PositiveY)
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowPY;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapPY;
                                            }
                                            else if (cubemapFace == CubemapFace.NegativeY)
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowNY;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapNY;
                                            }
                                            else if (cubemapFace == CubemapFace.PositiveZ)
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowPZ;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapPZ;
                                            }
                                            else
                                            {
                                                shaderFromWorldToShadowName = Uniforms.ShaderProperties._FromWorldToShadowNZ;
                                                shaderLightDepthMap = Uniforms.ShaderProperties._LightDepthMapNZ;
                                            }

                                            renderMPB.SetMatrix(shaderFromWorldToShadowName, shadowcaster.GetCubemappedWorldToShadowMatrix(cubemapFace));
                                            renderMPB.SetTexture(shaderLightDepthMap, shadowcaster.GetLightspaceCubemappedLinearDepthTexture(cubemapFace));

                                            return 0;
                                        });

                                        break;
                                    }

                                default:
                                    {
                                        pass = 1;

#if !UNITY_5_5_OR_NEWER
                                        // In Unity 5.4 shadow support on mobile devices is not well supported, mainly because
                                        // lacking of a shadow config in QualitySettings
                                        if (Application.platform == RuntimePlatform.IPhonePlayer ||
                                            Application.platform == RuntimePlatform.Android)
                                        {
                                            shadowParameter.x = 0.0f;
                                            renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, shadowParameter);
                                        }
#else
                                        // After Unity 5.5, the orthographic projected camera will render undesired depth in HvrLight.m_DepthWithSceneRT.
                                        // Disable the shadow testing for now.
                                        if (Application.platform == RuntimePlatform.IPhonePlayer ||
                                            Application.platform == RuntimePlatform.Android)
                                        {
                                            if (cam.orthographic)
                                            {
                                                shadowParameter.x = 0.0f;
                                                renderMPB.SetVector(Uniforms.ShaderProperties._ShadowParameters, shadowParameter);
                                            }
                                        }
#endif

                                        if (dependentLight.cookie != null)
                                        {
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._LightCookie, dependentLight.cookie);
                                        }
                                        else
                                        {
                                            renderMPB.SetTexture(Uniforms.ShaderProperties._LightCookie, Texture2D.whiteTexture);
                                        }

                                        RenderTexture depLS = shadowcaster.GetLightspaceDepthTexture();
                                        renderMPB.SetTexture(Uniforms.ShaderProperties._LightDepthMap, depLS);
                                        renderMPB.SetMatrix(Uniforms.ShaderProperties._FromWorldToShadow, shadowcaster.GetWorldToShadowMatrix());

                                        break;
                                    }
                            }

                            // Handle case where HvrActor is using a custom material
                            if (actor.material.passCount - 1 >= pass)
                                cmdBuf.DrawMesh(renderMesh, mat4x4, actor.material, 0, pass, renderMPB);
                        }
                    }
                }
            }

            if (rendered)
            {
                for (int i = 0; i < renderBundleList.Count; i++)
                {
                    RenderBundle bundle = renderBundleList[i];
                    HvrBuffers buffers = bundle.GetBuffers(cam);

                    HvrActor actor = bundle.actor;

                    if (actor.assetInterface == null)
                        continue;

                    // Blit the final results to camera target
                    renderMPB.Clear();
                    renderMPB.SetVector(Uniforms.ShaderProperties._Tint, MaterialHelper.GetMaterialColorFactor(actor.material, Color.white));
                    // For some reason Metal cannot reliably use buffers.colorCopy and causing IOAF error which cannot be recovered from, so has to disable lighting
                    if (isMetal)
                        renderMPB.SetTexture(Uniforms.ShaderProperties._HvrColorTex, buffers.color);   
                    else
                        renderMPB.SetTexture(Uniforms.ShaderProperties._HvrColorTex, buffers.colorCopy);   
                    renderMPB.SetTexture(Uniforms.ShaderProperties._HvrDepthTex, buffers.depth); // we can still use bundle.buffers.depth
                    

                    // Corresponding composition process
                    int finalCompositionPass = passSelector != null ? passSelector(actor.material) : 0;
                    cmdBufComposite.DrawMesh(m_mesh_quad, Matrix4x4.identity, mat_cmdBuf_colorComposite, 0, finalCompositionPass, renderMPB);
                }

                if (renderBundleList.Count == 0)
                {
                    // Force re-render all HvrLights to refresh depth buffers
                    // run shadow casters (HvrLights) to clear their buffers
                    foreach (HvrLight shadowcaster in hvrLights)
                    {
                        shadowcaster.RenderOnce();
                    }
                }
            }
            else
            {
                // run shadow casters (HvrLights) to clear their buffers
                foreach (HvrLight shadowcaster in hvrLights)
                {
                    shadowcaster.RenderOnce();
                }
            }
        }

        private IntPtr GetHvrRenderColorBuffer()
        {
            RenderTexture rt = cam.targetTexture;

            if (rt == null)
                return IntPtr.Zero;

            RenderBuffer colorBuffer = rt.colorBuffer;

            return colorBuffer.GetNativeRenderBufferPtr();
        }

        private IntPtr GetHvrRenderDepthBuffer()
        {
            RenderTexture rt = cam.targetTexture;

            if (rt == null)
                return IntPtr.Zero;

            RenderBuffer depthBuffer = rt.depthBuffer;

            return depthBuffer.GetNativeRenderBufferPtr();
        }

        private void RenderDirectActors()
        {
            // In some cases, Direct Mode will cause IUnityGraphicsMetal.CurrentCommandEncoder and CurrentRenderPassDescriptor both 
            // to be nil and there's no way to create command encoder from that.
            bool isMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            if (isMetal)
                return;

            if (m_renderBundles != null)
            {
                ViewportInterface viewport = GetViewport();

                for (int i = 0; i < m_renderBundles.Count; i++)
                {
                    RenderBundle bundle = m_renderBundles[i];
                    HvrActor actor = bundle.actor;

                    if (!actor.isActiveAndEnabled)
                        return;

                    if (Helper.IsLayerVisibleInCullingMask(actor.gameObject.layer, cam.cullingMask))
                    {
                        bool isVisible = actor.occlusionCullingEnabled ? bundle.cullingHelper.IsVisible() : true;

                        if (isVisible)
                        {
                            HvrScene.Player_RenderActor(actor.actorInterface.handle, viewport.handle, GetHvrRenderColorBuffer, GetHvrRenderDepthBuffer);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
