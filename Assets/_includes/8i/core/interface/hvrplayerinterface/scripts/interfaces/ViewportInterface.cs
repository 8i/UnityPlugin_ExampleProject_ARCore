using UnityEngine;
using System;
using UnityEngine.Rendering;

namespace HVR.Interface
{
    public class ViewportInterface
    {
        public int handle { get { return m_handle; } }
        private int m_handle;

        public ViewportInterface()
        {
            m_handle = Types.INVALID_HANDLE;

            Create();
        }

        private void Create()
        {
            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            try
            {
                UnityInterface.Lock();

                if (HvrPlayerInterface.Initialise())
                {
                    m_handle = HvrPlayerInterfaceAPI.Viewport_Create();

#if VERBOSE_LOGGING
                    Debug.Log("Create " + GetType().Name + " Handle:" + handle);
#endif

                    UnityInterface.SceneObjectsAdd(handle, GetType().Name + handle, GetType().FullName);

#if UNITY_5_5_OR_NEWER
                    // No C# macro for reversed depth configuration. The only reference is: https://docs.unity3d.com/Manual/SL-PlatformDifferences.html
                    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ||
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStation4 ||
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOne ||
                        SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
                    {
                        SetReverseDepthEnabled(true);
                    }
                    else
                    {
                        SetReverseDepthEnabled(false);
                    }
#else
                    SetReverseDepthEnabled(false);
#endif

                    SetColorSpace(QualitySettings.activeColorSpace);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                UnityInterface.Unlock();
            }
        }

        public void Delete()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            try
            {
                UnityInterface.Lock();

                HvrPlayerInterfaceAPI.Viewport_Delete(handle);

#if VERBOSE_LOGGING
                Debug.Log("Delete " + GetType().Name + " Handle:" + handle);
#endif

                UnityInterface.SceneObjectsRemove(handle);

                m_handle = Types.INVALID_HANDLE;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                UnityInterface.Unlock();
            }
        }

        public void UpdateViewport(Camera cam, bool forceRenderToTexture)
        {
            bool renderToTexture = RenderTexture.active != null;

            if (forceRenderToTexture)
                renderToTexture = forceRenderToTexture;

            Matrix4x4 view = cam.worldToCameraMatrix;
            Matrix4x4 proj = cam.projectionMatrix;

            // The renderIntoTexture value should be set to true if you intend to render into a RenderTexture
            // with this projection matrix. On some platforms it affects what the final matrix will look like.
            proj = GL.GetGPUProjectionMatrix(proj, renderToTexture);

            UpdateViewport(view, proj, cam.pixelRect);
        }

        public void UpdateViewport(Matrix4x4 view, Matrix4x4 proj, Rect rect)
        {
            SetViewMatrix(view);
            SetProjMatrix(proj);
            SetDimensions(rect.x, rect.y, rect.width, rect.height);
            SetColorSpace(QualitySettings.activeColorSpace);
        }

        public bool IsValid()
        {
            if (handle == Types.INVALID_HANDLE)
                return false;

            bool valid = HvrPlayerInterfaceAPI.Viewport_IsValid(handle);

            return valid;
        }

        public void SetViewMatrix(Matrix4x4 view)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            var apiMat44 = HvrPlayerInterfaceHelper.GetMat44FromMatrix4x4(view);
            HvrPlayerInterfaceAPI.Viewport_SetViewMatrix(handle, apiMat44);
        }

        public void SetProjMatrix(Matrix4x4 proj)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            var apiMat44 = HvrPlayerInterfaceHelper.GetMat44FromMatrix4x4(proj);
            HvrPlayerInterfaceAPI.Viewport_SetProjMatrix(handle, apiMat44);
        }

        public void SetDimensions(float x, float y, float width, float height)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Viewport_SetDimensions(handle, x, y, width, height);
        }

        public void SetColorSpace(ColorSpace colorSpace)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            int val = 0;

            switch (colorSpace)
            {
                case ColorSpace.Gamma:
                    val = HvrPlayerInterfaceAPI.COLOUR_SPACE_GAMMA;
                    break;
                case ColorSpace.Linear:
                    val = HvrPlayerInterfaceAPI.COLOUR_SPACE_LINEAR;
                    break;
            }

            HvrPlayerInterfaceAPI.Viewport_SetColourSpace(handle, val);
        }

        public void SetReverseDepthEnabled(bool reverseDepthEnabled)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Viewport_SetReverseDepthEnabled(handle, reverseDepthEnabled ? 1 : 0);
        }

        public void SetSamples(int samples)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            // TODO Reenable this when the feature comes online within Fafnir
            //HvrPlayerInterfaceAPI.Viewport_SetSamples(handle, samples);
        }
    }
}
