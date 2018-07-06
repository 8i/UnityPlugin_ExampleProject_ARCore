using HVR.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;
using HVR.Editor.Analytics;

namespace HVR.Editor
{
    [Serializable]
    public class AboutWindow : UnityEditor.EditorWindow
    {
        public static AboutWindow instance;

        private const string TITLE = "8i | About";
        private const string MENU_ITEM = "8i/About";

        List<String[]> pluginInfo = new List<String[]>();
        List<String[]> hvrEngineInfo = new List<String[]>();

        private string about_info;

        static AboutWindow window;

        AboutWindow()
        {
            instance = this;
        }

        /// <summary>
        /// Show the 8i About Window
        /// </summary>
        [MenuItem(MENU_ITEM, false, 100)]
        public static void ShowWindow()
        {
            window = GetWindow<AboutWindow>(true);
            window.minSize = new Vector2(320, 480);
            window.maxSize = new Vector2(320, 480);
            window.titleContent = new GUIContent(TITLE);
        }

        public void OnEnable()
        {
            pluginInfo.Add(new string[] { "Version: ", VersionInfo.VERSION });
            pluginInfo.Add(new string[] { "Build Date: ", VersionInfo.BUILD_DATE });
            pluginInfo.Add(new string[] { "Hash: ", VersionInfo.GIT_HASH });

            if (HvrPlayerInterface.Initialise())
            {
                hvrEngineInfo.Add(new string[] { "Version: ", HvrPlayerInterface.GetInfo("VERSION") });
                hvrEngineInfo.Add(new string[] { "Build Date: ", HvrPlayerInterface.GetInfo("BUILD_DATE") });
                hvrEngineInfo.Add(new string[] { "Hash: ", HvrPlayerInterface.GetInfo("GIT_HASH") });
            }

            about_info = "8i Unity Plugin:";

            for (int i = 0; i < pluginInfo.Count; i++)
            {
                about_info += "\n";
                about_info += pluginInfo[i][0] + pluginInfo[i][1];
            }
            about_info += "\n";

            about_info += "\n";
            about_info += "HVR Engine:";

            for (int i = 0; i < hvrEngineInfo.Count; i++)
            {
                about_info += "\n";
                about_info += hvrEngineInfo[i][0] + hvrEngineInfo[i][1];
            }

            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        void OnGUI()
        {
            if (EditorSharedResources.instance.aboutWindowLogo)
            {
                var rect = GUILayoutUtility.GetRect(position.width, 80, GUI.skin.box);
                GUI.DrawTexture(rect, EditorSharedResources.instance.aboutWindowLogo, ScaleMode.ScaleAndCrop);
            }

            HVR.Interface.HvrPlayerInterface.Update();

            DrawAboutSection();
        }

        private void DrawAboutSection()
        {
            Rect links = EditorGUILayout.GetControlRect();
            GUI.Box(new Rect(links.x - 4, links.y, links.width + 8, links.height), "Links", EditorStyles.toolbarButton);

            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                // Website
                if (GUILayout.Button("8i.com", GUILayout.Height(32), GUILayout.Width(100)))
                    Application.OpenURL("http://8i.com");

                // Website
                if (GUILayout.Button("Documentation", GUILayout.Height(32), GUILayout.Width(100)))
                    Application.OpenURL("https://8i.com/unity/docs/index.html");

                // Website
                if (GUILayout.Button("Support", GUILayout.Height(32), GUILayout.Width(100)))
                    Application.OpenURL("https://8isupport.zendesk.com");

                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            Rect baseRect = EditorGUILayout.GetControlRect();
            GUI.Box(new Rect(baseRect.x - 4, baseRect.y, baseRect.width + 8, baseRect.height), "Version Info", EditorStyles.toolbarButton);

            if (GUI.Button(new Rect(baseRect.xMax - 120, baseRect.y, 120, baseRect.height), "Copy To Clipboard", EditorStyles.miniButton))
            {
                EditorGUIUtility.systemCopyBuffer = about_info;
            }

            GUILayout.BeginVertical("box");
            {
                EditorGUILayout.TextArea(about_info, EditorStyles.largeLabel);
            }
            GUILayout.EndVertical();

            Rect memoryRect = EditorGUILayout.GetControlRect();
            GUI.Box(new Rect(memoryRect.x - 4, memoryRect.y, memoryRect.width + 8, memoryRect.height), "Memory Statistics", EditorStyles.toolbarButton);

            if (HvrPlayerInterface.Initialise())
            {
                GUILayout.BeginHorizontal();
                {
                    CommonTypes.MemoryStats memoryStats = Interface.HvrPlayerInterfaceAPI.Interface_GetMemoryStats();

                    GUILayout.BeginVertical("box");
                    {
                        string stats = string.Empty;

                        stats += "allocBytes\n";
                        stats += "allocBlocks\n";
                        stats += "freeBytes\n";
                        stats += "freeBlocks\n";
                        stats += "usedBytes\n";
                        stats += "usedBlocks";

                        EditorGUILayout.TextArea(stats, EditorStyles.largeLabel);
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical("box");
                    {
                        string stats = string.Empty;

                        stats += memoryStats.allocBytes + "\n";
                        stats += memoryStats.allocBlocks + "\n";
                        stats += memoryStats.freeBytes + "\n";
                        stats += memoryStats.freeBlocks + "\n";
                        stats += memoryStats.usedBytes + "\n";
                        stats += memoryStats.usedBlocks;

                        EditorGUILayout.TextArea(stats, EditorStyles.largeLabel);
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }

            Rect settingsRect = EditorGUILayout.GetControlRect();
            GUI.Box(new Rect(settingsRect.x - 4, settingsRect.y, settingsRect.width + 8, settingsRect.height), "Settings", EditorStyles.toolbarButton);

            bool analyticsOptOut = HVR.Editor.Analytics.AnalyticsManager.GetOptOut();
            analyticsOptOut = EditorGUILayout.Toggle("Opt-out of Analytics", analyticsOptOut);
            HVR.Editor.Analytics.AnalyticsManager.SetOptOut(analyticsOptOut);
        }
    }
}
