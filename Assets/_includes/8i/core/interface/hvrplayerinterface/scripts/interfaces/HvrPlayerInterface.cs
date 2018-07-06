// #define VERBOSE_LOGGING

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Runtime.InteropServices;

namespace HVR.Interface
{
    public static class HvrPlayerInterface
    {
#if UNITY_IOS || UNITY_ANDROID
        private static List<string> _renderMethodBlackList = new List<string>()
        {
            "InstancedCube"
        };
#else
        private static List<string> _renderMethodBlackList = new List<string>()
        { };
#endif

        private static string[] m_supportedRenderMethodTypes;

        private static float m_connection_check_lastTime = 0.0f;
        private const float CONNECTION_CHECK_WAIT_TIME = 3.0f;

        public static bool Initialise()
        {
            if (!HvrHelper.Support.IsApplicationStateSupported())
                return false;

            try
            {
                UnityInterface.Lock();

                if (!HvrPlayerInterfaceAPI.Interface_IsInitialised())
                {
                    m_connection_check_lastTime = 0.0f;

                    Types.InterfaceInitialiseInfo info = new Types.InterfaceInitialiseInfo();
                    info.structSize = (uint)Marshal.SizeOf(typeof(Types.InterfaceInitialiseInfo)); ;
                    info.appId = Application.productName;
                    info.appVersion = Application.version;
                    info.extensionPath = HvrHelper.GetExtensionsPath(Application.platform);
                    info.threadPoolSize = -1;

                    HvrPlayerInterfaceAPI.Interface_Initialise(ref info);

#if VERBOSE_LOGGING
                    HvrPlayerInterfaceAPI.Interface_SetLogLevel(HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_DEBUG);
#else
                    HvrPlayerInterfaceAPI.Interface_SetLogLevel(HvrPlayerInterfaceAPI.INTERFACE_LOG_TYPE_ERROR);
#endif

                    HvrPlayerInterfaceAPI.Interface_SetLogCallback(UnityInterfaceAPI.LogBuffer_Add);
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

            bool initialized = HvrPlayerInterfaceAPI.Interface_IsInitialised();

            return initialized;
        }

        public static bool IsInitialized()
        {
            if (!HvrHelper.Support.IsApplicationStateSupported())
                return false;

            bool initialized = HvrPlayerInterfaceAPI.Interface_IsInitialised();

            return initialized;
        }

        public static void Update()
        {
            if (IsInitialized())
            {
                HvrPlayerInterfaceAPI.Interface_Update();

                UnityInterface.Update();
            }
        }

        public static void CheckConnection()
        {
            if (m_connection_check_lastTime + CONNECTION_CHECK_WAIT_TIME < Helper.GetCurrentTime())
            {
                m_connection_check_lastTime = Helper.GetCurrentTime();
                HvrPlayerInterfaceAPI.Interface_Reconnect();
            }
        }

        public static string GetInfo(string key)
        {
            if (!Initialise())
                return string.Empty;

            StringBuilder stringBuilder = new StringBuilder(256);

            if (HvrPlayerInterfaceAPI.Interface_GetInfo(key, stringBuilder, stringBuilder.Capacity))
                return stringBuilder.ToString();

            return "";
        }

        public static string[] RenderMethod_GetSupportedTypes()
        {
            if (m_supportedRenderMethodTypes == null)
                RenderMethod_UpdateSupportedTypes();

            return m_supportedRenderMethodTypes;
        }

        public static void RenderMethod_UpdateSupportedTypes()
        {
            int count = HvrPlayerInterfaceAPI.Interface_GetRenderMethodTypeCount();

            List<string> methods = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string method = RenderMethod_GetTypeAtIndex(i);

                if (!_renderMethodBlackList.Contains(method))
                    methods.Add(method);
            }

            m_supportedRenderMethodTypes = new string[methods.Count];
            m_supportedRenderMethodTypes = methods.ToArray();
        }

        public static int RenderMethod_GetIndexForType(string method)
        {
            if (m_supportedRenderMethodTypes == null)
                RenderMethod_UpdateSupportedTypes();

            for (int i = 0; i < m_supportedRenderMethodTypes.Length; i++)
            {
                if (m_supportedRenderMethodTypes[i] == method)
                    return i;
            }

            return 0;
        }

        public static string RenderMethod_GetDefaultMethodType()
        {
            StringBuilder stringBuilder = new StringBuilder(256);

            if (HvrPlayerInterfaceAPI.Interface_GetRenderMethodDefault(stringBuilder, stringBuilder.Capacity))
            {
                return stringBuilder.ToString();
            }

            Debug.LogError("[HVR] Unable to get default rendermethod from interface");

            if (m_supportedRenderMethodTypes == null)
                RenderMethod_UpdateSupportedTypes();

            try
            {
                return m_supportedRenderMethodTypes[0];
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public static bool RenderMethod_IsTypeSupported(string method)
        {
            if (m_supportedRenderMethodTypes == null ||
                m_supportedRenderMethodTypes.Length == 0)
                RenderMethod_UpdateSupportedTypes();

            for (int i = 0; i < m_supportedRenderMethodTypes.Length; i++)
            {
                if (m_supportedRenderMethodTypes[i] == method)
                    return true;
            }

            return false;
        }

        public static string RenderMethod_GetTypeAtIndex(int idx)
        {
            StringBuilder stringBuilder = new StringBuilder(256);

            if (HvrPlayerInterfaceAPI.Interface_GetRenderMethodType(idx, stringBuilder, stringBuilder.Capacity))
                return stringBuilder.ToString();

            return "";
        }
    }
}
