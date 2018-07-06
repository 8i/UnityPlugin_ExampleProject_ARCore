using UnityEngine;
using UnityEditor;
using HVR.Interface;

namespace HVR.Editor.Analytics
{
    [InitializeOnLoad]
    public static class AnalyticsManager
    {
        public static bool initialized;

        public const string TRACKING_ID = "UA-60838253-17";

        public const string PREFS_HVR_ANALYTICS_OPT_OUT = "PREFS_HVR_ANALYTICS_OPT_OUT";
        public const string PREFS_HVR_ANALYTICS_SESSION_ID = "PREFS_HVR_ANALYTICS_SESSION_ID";

        private static GoogleAnalyticsMPV3 mpTracker;

        static AnalyticsManager()
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return;

            if (GetOptOut() == true)
                return;

            // Developer version, do not log
            if (HVR.VersionInfo.VERSION == "-1")
                return;

            // If the current process id is the same as last time, don't log any events
            // This suppresses analytics being sent after every code recompilation
            int currentID = System.Diagnostics.Process.GetCurrentProcess().Id;

            if (GetSessionID() == currentID)
                return;

            SetSessionID(currentID);

            LogEvent("AUTO", "INIT", "INIT", 0);
            LogEvent("INFO", "HVR_VERSION", HVR.VersionInfo.VERSION, 0);
            LogEvent("INFO", "HVR_GIT_INFO", HVR.VersionInfo.GIT_INFO, 0);

            if (HvrPlayerInterface.Initialise())
            {
                LogEvent("INFO", "HVR_PLAYERINTERFACE_GIT_VERSION", HvrPlayerInterface.GetInfo(HvrPlayerInterfaceAPI.Player_GetInfo_Keys.VERSION), 0);
                LogEvent("INFO", "HVR_PLAYERINTERFACE_GIT_INFO", HvrPlayerInterface.GetInfo(HvrPlayerInterfaceAPI.Player_GetInfo_Keys.GIT_INFO), 0);
            }
            
            LogEvent("INFO", "APPLICATION_UNITY_VERSION", Application.unityVersion, 0);
            LogEvent("INFO", "APPLICATION_PLATFORM", Application.platform.ToString(), 0);
            LogEvent("INFO", "SYSTEMINFO_OPERATINGSYSTEM", SystemInfo.operatingSystem, 0);
            LogEvent("INFO", "SYSTEMINFO_PROCESSORTYPE", SystemInfo.processorType, 0);
            LogEvent("INFO", "SYSTEMINFO_GRAPHICSDEVICENAME", SystemInfo.graphicsDeviceName.ToString(), 0);
            LogEvent("INFO", "SYSTEMINFO_GRAPHICSDEVICETYPE", SystemInfo.graphicsDeviceType.ToString(), 0);
        }

        private static void InitializeTracker()
        {
            if (!initialized)
            {
                initialized = true;

                mpTracker = new GoogleAnalyticsMPV3();
                mpTracker.SetTrackingCode(TRACKING_ID);
                mpTracker.SetAppName("8iUnityPlugin");
                mpTracker.SetAppVersion(HVR.VersionInfo.GIT_BRANCH);
                mpTracker.SetBundleIdentifier("");
                mpTracker.SetAnonymizeIP(true);
                mpTracker.InitializeTracker();
            }
        }

        public static bool GetOptOut()
        {
            if (EditorPrefs.HasKey(PREFS_HVR_ANALYTICS_OPT_OUT))
            {
                int state = EditorPrefs.GetInt(PREFS_HVR_ANALYTICS_OPT_OUT);
                return state == 1;
            }

            return false;
        }

        public static int GetSessionID()
        {
            if (EditorPrefs.HasKey(PREFS_HVR_ANALYTICS_SESSION_ID))
            {
                int time = EditorPrefs.GetInt(PREFS_HVR_ANALYTICS_SESSION_ID);
                return time;
            }

            return -1;
        }

        public static void SetSessionID(int id)
        {
            EditorPrefs.SetInt(PREFS_HVR_ANALYTICS_SESSION_ID, id);
        }

        public static void SetOptOut(bool state)
        {
            EditorPrefs.SetInt(PREFS_HVR_ANALYTICS_OPT_OUT, state ? 1 : 0);
        }

        public static void LogEvent(string eventCategory, string eventAction, string eventLabel, long value)
        {
            EventHitBuilder builder = new EventHitBuilder()
                .SetEventCategory(eventCategory)
                .SetEventAction(eventAction)
                .SetEventLabel(eventLabel)
                .SetEventValue(value);

            LogEvent(builder);
        }

        public static void LogEvent(EventHitBuilder builder)
        {
            InitializeTracker();

            if (builder.Validate() == null)
                return;

            mpTracker.LogEvent(builder);
        }
    }
}

