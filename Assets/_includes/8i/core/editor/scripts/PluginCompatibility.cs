using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace HVR.Editor
{
    [InitializeOnLoad]
    public class PluginCompatibility
    {
        struct PlatformSupport
        {
            public string platformName;
            public bool editorSupport;
            public string editorCPU;
            public string editorOS;
            public bool platformSupport;
            public string platformCPU;
        }

        static readonly PlatformSupport[] support_windows =
        {
            new PlatformSupport{
                platformName = "Win64",
                editorSupport = true,
                editorCPU = "x86_64",
                editorOS = "Windows",
                platformSupport = true,
                platformCPU = "x86_64"
            }
        };

        static readonly PlatformSupport[] support_mac =
        {
            new PlatformSupport{
                platformName = "OSXIntel64",
                editorSupport = true,
                editorCPU = "x86_64",
                editorOS = "OSX",
                platformSupport = true,
                platformCPU = "x86_64"
            },
        };

        static readonly PlatformSupport[] support_linux =
        {
            new PlatformSupport{
                platformName = "Linux64",
                editorSupport = true,
                editorCPU = "x86_64",
                editorOS = "Linux",
                platformSupport = true,
                platformCPU = "x86_64"
            },
        };

        static readonly PlatformSupport[] support_ios =
        {
            new PlatformSupport{
                platformName = "iOS",
                editorSupport = false,
                platformSupport = true,
            },
        };

        static readonly PlatformSupport[] support_android =
        {
            new PlatformSupport{
                platformName = "Android",
                editorSupport = false,
                platformSupport = true,
                platformCPU = "ARMv7"
            },
        };

        static PluginCompatibility()
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
            {
                EnsureCompatiblity();
            }
            else
            {
                // While running in the Unity Editor delayCall is required as
                // ScriptableObjects will have null values for all properties
                EditorApplication.delayCall += EnsureCompatiblity;
            }
        }

        public static void EnsureCompatiblity()
        {
            for (int i = 0; i < EditorSharedResources.instance.pluginCompatibility.Length; i++)
            {
                PluginReferenceController controller = EditorSharedResources.instance.pluginCompatibility[i];

                if (controller != null)
                {
                    foreach (PluginReferenceController.PluginReference reference in controller.references)
                    {
                        bool filterThisVersion = false;

                        if (!string.IsNullOrEmpty(reference.versionFilter))
                        {
                            string[] split = reference.versionFilter.Split(';');

                            foreach (string s in split)
                            {
                                if (Application.unityVersion.StartsWith(s))
                                    filterThisVersion = true;
                            }
                        }

                        List<PlatformSupport> sup = new List<PlatformSupport>();

                        if (reference.platformNone ||
                            filterThisVersion)
                        {
                            sup.Clear();
                        }
                        else
                        {
                            if (reference.platformWindows)
                                sup.AddRange(support_windows.ToList());

                            if (reference.platformMac)
                                sup.AddRange(support_mac.ToList());

                            if (reference.platformLinux)
                                sup.AddRange(support_linux.ToList());

                            if (reference.platformiOS)
                                sup.AddRange(support_ios.ToList());

                            if (reference.platformAndroid)
                                sup.AddRange(support_android.ToList());
                        }

                        SetCompatibility(reference.guid, sup.ToArray());
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SetCompatibility(string guid, PlatformSupport[] platformSupport)
        {
            string pluginPath = AssetDatabase.GUIDToAssetPath(guid);

            PluginImporter plugin = AssetImporter.GetAtPath(pluginPath) as PluginImporter;

            if (plugin == null)
                return;

            bool updateRequired = false;

            if (platformSupport.Length == 0)
            {
                // Just set every platform to disabled before enabling the correct ones 
                foreach (BuildTarget bt in Enum.GetValues(typeof(BuildTarget)))
                {
                    // If the build target is obsolete 
                    if (bt < 0)
                        continue;

                    // Use a string here to handle issue where a platform dependency may not be installed 
                    // within the current Unity Editor install 
                    if (plugin.GetCompatibleWithPlatform(bt.ToString()))
                        updateRequired = true;
                }
            }
            else
            {
                foreach (PlatformSupport s in platformSupport)
                {
                    if (!plugin.GetCompatibleWithPlatform(s.platformName))
                        updateRequired = true;

                    if (plugin.GetCompatibleWithEditor() != s.editorSupport)
                        updateRequired = true;

                    if (s.editorSupport)
                    {
                        if (plugin.GetEditorData("OS") != s.editorOS ||
                            plugin.GetEditorData("CPU") != s.editorCPU)
                            updateRequired = true;
                    }
                }
            }

            if (updateRequired)
            {
#if UNITY_5_5_OR_NEWER
                plugin.ClearSettings();
#endif
                plugin.SetCompatibleWithEditor(false);
                plugin.SetCompatibleWithAnyPlatform(false);

                // Just set every platform to disabled before enabling the correct ones
                foreach (BuildTarget bt in Enum.GetValues(typeof(BuildTarget)))
                {
                    // If the build target is obsolete
                    if (bt < 0)
                        continue;

                    // Use a string here to handle issue where a platform dependency may not be installed
                    // within the current Unity Editor install
                    plugin.SetCompatibleWithPlatform(bt.ToString(), false);
                }

                foreach (PlatformSupport s in platformSupport)
                {
                    plugin.SetCompatibleWithPlatform(s.platformName, s.platformSupport);

                    plugin.SetPlatformData("Any", "Exclude Editor", (s.editorSupport ? 0 : 1).ToString());
                    plugin.SetPlatformData("Any", "Exclude " + s.platformName, (s.platformSupport ? 0 : 1).ToString());

                    if (!string.IsNullOrEmpty(s.platformCPU))
                        plugin.SetPlatformData(s.platformName, "CPU", s.platformCPU);

                    plugin.SetCompatibleWithEditor(s.editorSupport);

                    if (s.editorSupport)
                    {
                        plugin.SetEditorData("OS", s.editorOS);
                        plugin.SetEditorData("CPU", s.editorCPU);
                    }
                }

                plugin.SaveAndReimport();
            }
        }
    }
}
