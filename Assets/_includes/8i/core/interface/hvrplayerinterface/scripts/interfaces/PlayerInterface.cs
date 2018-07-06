using UnityEngine;
using UnityEngine.Rendering;
using System;

#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace HVR.Interface
{
    public class PlayerInterface
    {
        public int handle { get { return m_handle; } }
        private int m_handle;

        public GraphicsDeviceType graphicsDeviceType { get { return m_graphicsDeviceType; } }

        private GraphicsDeviceType m_graphicsDeviceType = GraphicsDeviceType.Null;

        public PlayerInterface()
        {
            m_graphicsDeviceType = SystemInfo.graphicsDeviceType;

            Create();

            UnityInterface.SceneObjectsAdd(handle, GetType().Name + handle, GetType().FullName);
        }

        public PlayerInterface(int playerHandle)
        {
            m_graphicsDeviceType = GraphicsDeviceType.Null;

            m_handle = playerHandle;
        }

        private void Create()
        {
#if UNITY_EDITOR
            // The GetUnityRenderDevice and graphicsDeviceType will be Null if the editor is in batch mode
            // TODO: Handle case where user may want the player to be built during batch mode?
            if (InternalEditorUtility.inBatchMode)
                return;
#endif
            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            // TODO, correct this by exposing the get native id
            int rendererType = HvrPlayerInterfaceAPI.RENDERER_TYPE_NONE;

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    rendererType = HvrPlayerInterfaceAPI.RENDERER_TYPE_DIRECT3D11;
                    break;
#if !UNITY_5_5_OR_NEWER
                case GraphicsDeviceType.OpenGL2:
#endif
                case GraphicsDeviceType.OpenGLCore:
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                    rendererType = HvrPlayerInterfaceAPI.RENDERER_TYPE_OPENGL;
                    break;
                case GraphicsDeviceType.Metal:
                    rendererType = HvrPlayerInterfaceAPI.RENDERER_TYPE_METAL;
                    break;
                default:
                    break;
            }

            try
            {
                UnityInterface.Lock();

                if (HvrPlayerInterface.Initialise())
                {
                    m_handle = HvrPlayerInterfaceAPI.Player_Create(rendererType, UnityInterfaceAPI.GetUnityRenderDevice());

#if VERBOSE_LOGGING
                    Debug.Log("Create " + GetType().Name + " Handle:" + handle);
#endif

                    m_graphicsDeviceType = SystemInfo.graphicsDeviceType;
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

            // TODO: Verify that the below actually works
            // The locking will not be correct, as the Player needs to be deleted on the render thread
            // and the locks need to be executed on the same thread

            try
            {
                UnityInterface.Lock_GL();

#if VERBOSE_LOGGING
                Debug.Log("Delete " + GetType().Name + " Handle:" + handle);
#endif

                int eventID_detach = UnityInterfaceAPI.QueueGLEventI(HvrPlayerInterfaceAPI.Player_Detach, handle);
                GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID_detach);

                int eventID_delete = UnityInterfaceAPI.QueueGLEventI(HvrPlayerInterfaceAPI.Player_Delete, handle);
                GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID_delete);

                int eventID_remove = UnityInterfaceAPI.QueueGLEventI(UnityInterfaceAPI.Scene_Objects_Remove, handle);
                GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID_remove);

                m_handle = Types.INVALID_HANDLE;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                UnityInterface.Unlock_GL();
            }
        }

        public bool IsValid()
        {
            if (handle == Types.INVALID_HANDLE)
                return false;

            return HvrPlayerInterfaceAPI.Player_IsValid(handle);
        }
    }
}
