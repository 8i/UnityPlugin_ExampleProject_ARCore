// #define VERBOSE_LOGGING

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace HVR.Interface
{
    public static class UnityInterface
    {
        public static void Update()
        {
            Lock();

            while (UnityInterfaceAPI.LogBuffer_Get() != IntPtr.Zero)
            {
                try
                {
                    IntPtr ptr = UnityInterfaceAPI.LogBuffer_Get();

                    UnityInterfaceAPI.LogMessage logMessage = Helper.PtrToStruct<UnityInterfaceAPI.LogMessage>(ptr);
                    string message = Marshal.PtrToStringAnsi(logMessage.message);

                    UnityInterfaceAPI.LogBuffer_Pop();

                    switch (logMessage.type)
                    {
                        case HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_DEBUG:
                            Debug.Log(message);
                            break;
                        case HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_INFO:
#if VERBOSE_LOGGING
                            Debug.Log(message);
#endif
                            break;
                        case HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_WARNING:
                            Debug.LogWarning(message);
                            break;
                        case HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_ERROR:
                            Debug.LogError(message);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }

            Unlock();
        }

        public static void Lock()
        {
            UnityInterfaceAPI.Lock();
        }

        public static void Unlock()
        {
            UnityInterfaceAPI.Unlock();
        }

        public static void Lock_GL()
        {
            int eventID = UnityInterfaceAPI.QueueGLEvent(UnityInterfaceAPI.Lock);
            GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID);
            GL.InvalidateState();
        }

        public static void Unlock_GL()
        {
            int eventID = UnityInterfaceAPI.QueueGLEvent(UnityInterfaceAPI.Unlock);
            GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID);
            GL.InvalidateState();
        }

        public static void SceneObjectsAdd(int handle, string name, string type)
        {
            Lock();

            if (!SceneObjectsContains(handle))
            {
                UnityInterfaceAPI.Scene_Objects_Add(handle, name, type);
            }

            Unlock();
        }

        public static void SceneObjectsRemove(int handle)
        {
            Lock();

            UnityInterfaceAPI.Scene_Objects_Remove(handle);

            // Make sure that if the scene object is deleted if it is the last sceneobject.
            // This may not always be the case as the sceneplayer is only created when an 
            // actor is rendered.
            if (SceneObjectsCount() == 1 &&
                HvrScene.ScenePlayerExists())
            {
                HvrScene.DeleteScenePlayer();
            }

            Unlock();
        }

        public static bool SceneObjectsContains(int handle)
        {
            int count = SceneObjectsCount();

            bool contains = false;

            for (int i = 0; i < count; i++)
            {
                if (UnityInterfaceAPI.Scene_Objects_GetHandleAtIndex(i) == handle)
                {
                    contains = true;
                }
            }

            return contains;
        }

        public static bool SceneObjectsContains(string name)
        {
            bool contains = false;

            if (SceneObjectsGetHandleFromName(name) != 0)
                contains = true;

            return contains;
        }

        public static string SceneObjectsGetNameFromHandle(int handle)
        {
            StringBuilder stringBuilder = new StringBuilder(256);

            string name = string.Empty;

            if (UnityInterfaceAPI.Scene_Objects_GetNameFromHandle(handle, stringBuilder, stringBuilder.Capacity))
            {
                name = stringBuilder.ToString();
            }

            return name;
        }

        public static Type SceneObjectsGetTypeFromHandle(int handle)
        {
            StringBuilder stringBuilder = new StringBuilder(256);

            Type type = null;

            if (UnityInterfaceAPI.Scene_Objects_GetTypeFromHandle(handle, stringBuilder, stringBuilder.Capacity))
            {
                try
                {
                    type = Type.GetType(stringBuilder.ToString(), true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }

            return type;
        }

        public static int SceneObjectsGetHandleFromName(string name)
        {
            int handle = UnityInterfaceAPI.Scene_Objects_GetHandleFromName(name);

            return handle;
        }

        public static int SceneObjectsCount()
        {
            int count = UnityInterfaceAPI.Scene_Objects_GetCount();

            return count;
        }
    }
}
