using HVR.Interface;
using UnityEngine;
using UnityEngine.Rendering;

namespace HVR
{
    public class HvrViewportSwapChain
    {
        int m_maximumViewports = 0;
        ViewportInterface[] m_viewports = null;
        int m_viewportIndex = 0;

        public HvrViewportSwapChain(int maximumViewports)
        {
            m_maximumViewports = maximumViewports;
            m_viewports = new ViewportInterface[m_maximumViewports];
            for (int i = 0; i < m_maximumViewports; ++i)
            {
                m_viewports[i] = new ViewportInterface();
            }
            m_viewportIndex = 0;
        }

        public void DeleteViewports()
        {
            for (int i = 0; i < m_viewports.Length; i++)
            {
                m_viewports[i].Delete();
                m_viewports[i] = null;
            }
            m_viewports = null;
        }

        private ViewportInterface _GetViewportAndFlip()
        {
            if (m_viewports == null)
                return null;

            ViewportInterface viewport = m_viewports[m_viewportIndex];

            m_viewportIndex = (m_viewportIndex + 1) % m_maximumViewports; // flip the index

            viewport.SetSamples(QualitySettings.antiAliasing);

            return viewport;
        }

        public ViewportInterface NextViewport(Camera cam, bool forceRenderToTexture)
        {
            bool renderToTexture = RenderTexture.active != null;

            if (forceRenderToTexture)
                renderToTexture = forceRenderToTexture;

            Matrix4x4 view = cam.worldToCameraMatrix;

            // The renderIntoTexture value should be set to true if you intend to render into a RenderTexture
            // with this projection matrix. On some platforms it affects how the final matrix will look like.
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, renderToTexture);

            // left and top should always be 0 as the viewport we want to render in shouldn't need to be offset in x,y - Tom
            return NextViewport(view, proj, 0, 0, cam.pixelWidth, cam.pixelHeight);
        }

        public ViewportInterface NextViewport(Matrix4x4 view, Matrix4x4 proj, int viewportLeft, int viewportTop, int viewportWidth, int viewportHeight)
        {
            if (m_viewports == null)
                return null;

            ViewportInterface viewport = _GetViewportAndFlip();

            viewport.SetViewMatrix(view);

#if UNITY_5_5_OR_NEWER
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
            {
            	viewport.SetReverseDepthEnabled(true);
            }
#endif

            viewport.SetProjMatrix(proj);
            viewport.SetDimensions(viewportLeft, viewportTop, viewportWidth, viewportHeight);

            return viewport;
        }
    }
}
