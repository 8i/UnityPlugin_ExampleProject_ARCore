using UnityEngine;
using System;

namespace HVR.Interface
{
    public class RenderMethodInterface
    {
        public int handle { get { return m_handle; } }
        private int m_handle = Types.INVALID_HANDLE;

        public string type { get { return m_type; } }
        private string m_type = string.Empty;

        public void Create(string renderMethodType)
        {
            m_handle = Types.INVALID_HANDLE;

            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            try
            {
                UnityInterface.Lock();

                if (HvrPlayerInterface.Initialise())
                {
                    if (string.IsNullOrEmpty(renderMethodType))
                    {
                        renderMethodType = HvrPlayerInterface.RenderMethod_GetDefaultMethodType();
                    }
                    else
                    {
                        if (!HvrPlayerInterface.RenderMethod_IsTypeSupported(renderMethodType))
                        {
                            Debug.LogWarning("Rendermethod '" + renderMethodType + "' is not supported on this platform. Defaulting to to '" + HvrPlayerInterface.RenderMethod_GetDefaultMethodType() + "'");
                            renderMethodType = HvrPlayerInterface.RenderMethod_GetDefaultMethodType();
                        }
                    }

                    m_type = renderMethodType;
                    m_handle = HvrPlayerInterfaceAPI.RenderMethod_Create(renderMethodType);

#if VERBOSE_LOGGING
                    Debug.Log("Create " + GetType().Name + " Handle:" + handle);
#endif

                    UnityInterface.SceneObjectsAdd(handle, GetType().Name + handle, GetType().FullName);
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

                HvrPlayerInterfaceAPI.RenderMethod_Delete(handle);

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

        public bool IsValid()
        {
            if (handle == Types.INVALID_HANDLE)
                return false;

            return HvrPlayerInterfaceAPI.RenderMethod_IsValid(handle);
        }

        public void SetShaderSubroutines(string code)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.RenderMethod_SetShaderSubroutines(handle, code);
        }

        public void SetShaderSubroutinesArray(string[] code)
        {
            if (handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.RenderMethod_SetShaderSubroutinesArray(handle, code, code.Length);
        }
    }
}
