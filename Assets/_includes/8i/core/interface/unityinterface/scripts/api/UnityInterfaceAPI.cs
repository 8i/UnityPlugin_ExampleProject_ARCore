using AOT;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace HVR.Interface
{
    public static class UnityInterfaceAPI
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string DLLName = "__Internal";
#else
        public const string DLLName = "UnityInterface";
#endif

        static UnityInterfaceAPI()
        {
#if UNITY_EDITOR
            // There are issues with releasing unmanaged memory while running in batch mode for OSX and iOS
            // TODO: Remove this check
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return;
#endif

            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            RegisterUnityPlugin();

            SetFunc_InterfaceShutdown(HvrPlayerInterfaceAPI.Interface_Shutdown);
        }

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RegisterUnityPlugin();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetUnityRenderDevice();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr UnityRenderEventFunc();

        public delegate void GLEvent();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int QueueGLEvent(GLEvent func);

        public delegate void GLEventI(int a0);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int QueueGLEventI(GLEventI func, int a0);

        public delegate void GLEventWithFrameContextI(int a0, IntPtr fc);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int QueueGLEventPrepFrameContextI(GLEventWithFrameContextI func, int a0, IntPtr nativeColorRenderBuffer, IntPtr nativeDepthRenderBuffer);

        public delegate void GLEventIII(int a0, int a1, int a2);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int QueueGLEventIII(GLEventIII func, int a0, int a1, int a2);

        public delegate void GLEventWithFrameContextIII(int a0, int a1, int a2, IntPtr fc);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int QueueGLEventPrepFrameContextIII(GLEventWithFrameContextIII func, int a0, int a1, int a2, IntPtr nativeColorRenderBuffer, IntPtr nativeDepthRenderBuffer);

        // --------------------------------------------------------------------------------------------------------------------------------------------------
        // Locks
        // --------------------------------------------------------------------------------------------------------------------------------------------------

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEvent))]
        public static extern void Lock();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEvent))]
        public static extern void Unlock();

        // --------------------------------------------------------------------------------------------------------------------------------------------------
        // Scene Management
        // --------------------------------------------------------------------------------------------------------------------------------------------------

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Scene_Objects_Add(int handle, string name, string type);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventI))]
        public static extern void Scene_Objects_Remove(int handle);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Scene_Objects_GetHandleAtIndex(int index);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Scene_Objects_GetHandleFromName(string name);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Scene_Objects_GetNameFromHandle(int handle, StringBuilder value, int valueSize);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Scene_Objects_GetTypeFromHandle(int handle, StringBuilder value, int valueSize);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Scene_Objects_GetCount();

        // --------------------------------------------------------------------------------------------------------------------------------------------------
        // Interface Communication
        // --------------------------------------------------------------------------------------------------------------------------------------------------

        public static void SetFunc_InterfaceShutdown(GLEvent callback)
        {
            IntPtr callback_ptr = Marshal.GetFunctionPointerForDelegate(callback);
            SetFunc_InterfaceShutdown(callback_ptr);
        }

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetFunc_InterfaceShutdown(IntPtr callback);

        // --------------------------------------------------------------------------------------------------------------------------------------------------
        // Key Value Map
        // --------------------------------------------------------------------------------------------------------------------------------------------------

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Map_Add(string key, int value);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Map_Remove(string key);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Map_Contains(string key);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Map_GetValue(string key);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Map_SetValue(string key, int value);

        // --------------------------------------------------------------------------------------------------------------------------------------------------
        // Log Buffer 
        // --------------------------------------------------------------------------------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct LogMessage
        {
            public int type;
            public IntPtr message;
        }

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(Types.LogCallback))]
        public static extern void LogBuffer_Add(int type, IntPtr message);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr LogBuffer_Get();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void LogBuffer_Pop();
    }
}
