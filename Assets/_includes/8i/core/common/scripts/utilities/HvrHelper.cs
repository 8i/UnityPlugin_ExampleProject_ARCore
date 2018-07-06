using HVR.Android;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace HVR
{
    public static class HvrHelper
    {
        public static class Support
        {
#if UNITY_EDITOR
            public static readonly UnityEditor.BuildTarget[] supportedBuildTargets =
            {
                BuildTarget.Android,
                BuildTarget.iOS,
#if UNITY_2017_3_OR_NEWER
                BuildTarget.StandaloneOSX,
#else
                BuildTarget.StandaloneOSXIntel64,
#endif
                BuildTarget.StandaloneLinux64,
                BuildTarget.StandaloneWindows64
            };
#endif

            public static readonly RuntimePlatform[] supportedRuntimePlatforms =
            {
                RuntimePlatform.Android,
                RuntimePlatform.IPhonePlayer,
#if UNITY_EDITOR_LINUX
                RuntimePlatform.LinuxEditor,
#endif
                RuntimePlatform.LinuxPlayer,
                RuntimePlatform.OSXEditor,
                RuntimePlatform.OSXPlayer,
                RuntimePlatform.WindowsEditor,
                RuntimePlatform.WindowsPlayer
            };

            public static readonly GraphicsDeviceType[] SupportedGraphicsDeviceTypes_Windows =
            {
                GraphicsDeviceType.Direct3D11,
                GraphicsDeviceType.OpenGLCore
            };

            public static readonly GraphicsDeviceType[] SupportedGraphicsDeviceTypes_macOS =
            {
                GraphicsDeviceType.OpenGLCore
            };

            public static readonly GraphicsDeviceType[] SupportedGraphicsDeviceTypes_linux =
            {
                GraphicsDeviceType.OpenGLCore,
            };

            public static readonly GraphicsDeviceType[] SupportedGraphicsDeviceTypes_android =
            {
                GraphicsDeviceType.OpenGLES3
            };

            public static readonly GraphicsDeviceType[] SupportedGraphicsDeviceTypes_ios =
            {
                GraphicsDeviceType.OpenGLES3,
                GraphicsDeviceType.Metal
            };

            public static readonly GraphicsDeviceType[] DRIVER_Direct3D_SUPPORTED_VERSIONS =
            {
                GraphicsDeviceType.Direct3D11,
            };

            public const int DRIVER_OpenGL_MIN_VERSION_MAJOR = 4;
            public const int DRIVER_OpenGL_MIN_VERSION_MINOR = 1;

            private enum PlatformSupport
            {
                Unknown,
                Supported,
                NotSupported
            }

#if UNITY_EDITOR
            private static BuildTarget m_support_currentBuildTarget = BuildTarget.NoTarget;
#endif
            private static RuntimePlatform m_support_currentPlatform = RuntimePlatform.Android; // Default to Android
            private static GraphicsDeviceType m_support_currentGraphicsDeviceType = GraphicsDeviceType.Null;
            private static PlatformSupport m_support_check = PlatformSupport.Unknown;

            public static bool IsApplicationStateSupported()
            {
#if UNITY_EDITOR
                // There are issues with releasing unmanaged memory while running in batch mode for OSX and iOS
                // TODO: Remove this check
                if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                    return false;

                // Handles case in the Unity Editor where the buildtarget has changed
                if (m_support_currentBuildTarget != EditorUserBuildSettings.activeBuildTarget)
                {
                    m_support_currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                    m_support_check = PlatformSupport.Unknown;
                }
#endif

                // Handles case in the Unity Editor where the platform has changed
                if (m_support_currentPlatform != Application.platform)
                {
                    m_support_currentPlatform = Application.platform;
                    m_support_check = PlatformSupport.Unknown;
                }

                // Handles case where somehow the graphics device type has changed
                if (m_support_currentGraphicsDeviceType != SystemInfo.graphicsDeviceType)
                {
                    m_support_currentGraphicsDeviceType = SystemInfo.graphicsDeviceType;
                    m_support_check = PlatformSupport.Unknown;
                }

                if (m_support_check == PlatformSupport.Unknown)
                {
                    bool supported = true;

#if UNITY_EDITOR
                    if (!IsSupportedBuildTarget(EditorUserBuildSettings.activeBuildTarget))
                    {
                        Debug.LogError("The current build target is not supported by the 8i Unity Plugin [" + EditorUserBuildSettings.activeBuildTarget + "]");
                        supported = false;
                    }
#endif

                    if (!IsSupportedRuntimePlatform(Application.platform))
                    {
                        Debug.LogError("This platform is not supported by the 8i Unity Plugin [" + Application.platform + "]");
                        supported = false;
                    }

                    if (!IsSupportedRuntimePlatformGraphicsDeviceType(Application.platform, SystemInfo.graphicsDeviceType))
                    {
                        Debug.LogError("GraphicsDeviceType is not supported by the 8i Unity Plugin [" + SystemInfo.graphicsDeviceType + "]");
                        supported = false;
                    }

                    if (!IsSupportedGraphicsDeviceVersion(SystemInfo.graphicsDeviceVersion))
                    {
                        Debug.LogError("GraphicsDeviceVersion is not supported by the 8i Unity Plugin [" + SystemInfo.graphicsDeviceVersion + "]");
                        supported = false;
                    }

                    if (supported)
                    {
                        m_support_check = PlatformSupport.Supported;
                    }
                    else
                    {
                        m_support_check = PlatformSupport.NotSupported;
                    }
                }

                if (m_support_check == PlatformSupport.NotSupported)
                    return false;

                return true;
            }

            public static bool IsSupportedRuntimePlatform(RuntimePlatform platform)
            {
                for (int i = 0; i < supportedRuntimePlatforms.Length; i++)
                {
                    if (supportedRuntimePlatforms[i] == platform)
                        return true;
                }

                return false;
            }

            public static bool IsSupportedGraphicsDeviceVersion(string deviceVersion)
            {
                GraphicsDeviceType gdt = SystemInfo.graphicsDeviceType;

                string[] version_split = deviceVersion.Split(' ');

                switch (gdt)
                {
#if !UNITY_5_5_OR_NEWER
                    case GraphicsDeviceType.OpenGL2:
#endif
                    case GraphicsDeviceType.OpenGLCore:

                        if (version_split.Count() > 1)
                        {
#if UNITY_EDITOR
                            // TODO Add a check to the recommmend project window around emulated graphics
                            if (deviceVersion.ToLower().Contains("emulate") ||
                                version_split[1].ToLower().Contains("emulate"))
                            {
                                Debug.LogWarning("Unity Editor Graphics Device Emulation is not fully supported, consider disabling it");
                                return true;
                            }
#endif

                            string[] driver_version_split = version_split[1].Split('.');

                            int major = 0;
                            int minor = 0;

                            int.TryParse(driver_version_split[0], out major);
                            int.TryParse(driver_version_split[1], out minor);

                            if (major >= DRIVER_OpenGL_MIN_VERSION_MAJOR &&
                                minor >= DRIVER_OpenGL_MIN_VERSION_MINOR)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }

                        // Assume that it's fine in the case we can't check the version number
                        // TODO: Don't assume anything- Tom
                        return true;

                    default:
                        return true;
                }
            }

            public static GraphicsDeviceType[] GetSupportedGraphicsDeviceTypes_FromRuntimePlatform(RuntimePlatform platform)
            {
                GraphicsDeviceType[] types = new GraphicsDeviceType[] { };

                switch (platform)
                {
                    case RuntimePlatform.Android:
                        types = SupportedGraphicsDeviceTypes_android;
                        break;

                    case RuntimePlatform.IPhonePlayer:
                        types = SupportedGraphicsDeviceTypes_ios;
                        break;

                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.WindowsPlayer:
                        types = SupportedGraphicsDeviceTypes_Windows;
                        break;

                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
                        types = SupportedGraphicsDeviceTypes_macOS;
                        break;
#if UNITY_EDITOR_LINUX
                    case RuntimePlatform.LinuxEditor:
#endif
                    case RuntimePlatform.LinuxPlayer:
                        types = SupportedGraphicsDeviceTypes_linux;
                        break;

                    default:
                        Debug.LogError("Unsupported runtime platform");
                        break;
                }

                return types;
            }

            public static bool IsSupportedRuntimePlatformGraphicsDeviceType(RuntimePlatform runtimePlatform, GraphicsDeviceType type)
            {
                GraphicsDeviceType[] types = GetSupportedGraphicsDeviceTypes_FromRuntimePlatform(runtimePlatform);
                return types.Contains(type);
            }

#if UNITY_EDITOR

            public static BuildTarget GetBuildTargetFromRuntimePlatform(RuntimePlatform platform)
            {
                switch (platform)
                {
                    case RuntimePlatform.Android:
                        return BuildTarget.Android;

                    case RuntimePlatform.IPhonePlayer:
                        return BuildTarget.iOS;

                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.WindowsPlayer:
                        return BuildTarget.StandaloneWindows64;

                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
#if UNITY_2017_3_OR_NEWER
                        return BuildTarget.StandaloneOSX;
#else
                        return BuildTarget.StandaloneOSXIntel64;
#endif
#if UNITY_EDITOR_LINUX
                    case RuntimePlatform.LinuxEditor:
#endif
                    case RuntimePlatform.LinuxPlayer:
                        return BuildTarget.StandaloneLinux64;

                    default:
                        Debug.LogError("Unsupported runtime platform");
                        return BuildTarget.NoTarget;
                }
            }

            public static bool IsSupportedBuildTarget(UnityEditor.BuildTarget target)
            {
                for (int i = 0; i < supportedBuildTargets.Length; i++)
                {
                    if (supportedBuildTargets[i] == target)
                        return true;
                }

                return false;
            }

            public static GraphicsDeviceType[] GetSupportedGraphicsDeviceTypeS_FromBuildTarget(BuildTarget buildTarget)
            {
                GraphicsDeviceType[] types = new GraphicsDeviceType[] { };

                switch (buildTarget)
                {
                    case BuildTarget.Android:
                        types = SupportedGraphicsDeviceTypes_android;
                        break;

                    case BuildTarget.iOS:
                        types = SupportedGraphicsDeviceTypes_ios;
                        break;

                    case BuildTarget.StandaloneWindows64:
                        types = SupportedGraphicsDeviceTypes_Windows;
                        break;
#if UNITY_2017_3_OR_NEWER
                    case BuildTarget.StandaloneOSX:
#else
                    case BuildTarget.StandaloneOSXIntel64:
#endif
                        types = SupportedGraphicsDeviceTypes_macOS;
                        break;

                    case BuildTarget.StandaloneLinux64:
                        types = SupportedGraphicsDeviceTypes_linux;
                        break;

                    default:
                        break;
                }

                return types;
            }

            public static bool IsBuildTargetGraphicsDeviceTypeSupported(BuildTarget buildtarget, GraphicsDeviceType type)
            {
                GraphicsDeviceType[] types = GetSupportedGraphicsDeviceTypeS_FromBuildTarget(buildtarget);
                return types.Contains(type);
            }
#endif
        }

        public static string GetBuildDataPath()
        {
            string path = string.Empty;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.IPhonePlayer:
                    path = Application.dataPath + Path.DirectorySeparatorChar + Uniforms.buildDataPath + Path.DirectorySeparatorChar;
                    break;
                case RuntimePlatform.Android:
                    path = AndroidFileUtils.GetInternalStorageDirectory() + Path.DirectorySeparatorChar + Uniforms.buildDataPath + Path.DirectorySeparatorChar;
                    break;
                default:
                    break;
            }

            return path;
        }

        public static string GetDataPathFromGUID(string guid)
        {
#if UNITY_EDITOR
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsEditor:
#if UNITY_EDITOR_LINUX
                case RuntimePlatform.LinuxEditor:
#endif
                    return EditorHelper.GetAssetPathFromGUID(guid);
            }
#endif

            string buildDataPath = GetBuildDataPath();

            if (!string.IsNullOrEmpty(buildDataPath))
            {
                DirectoryInfo dataDirInfo = new DirectoryInfo(buildDataPath);
                DirectoryInfo[] directoryInfos = dataDirInfo.GetDirectories();
                FileInfo[] fileInfos = dataDirInfo.GetFiles();

                for (int i = 0; i < directoryInfos.Length; i++)
                {
                    if (directoryInfos[i].Name == guid)
                        return Path.GetFullPath(Path.Combine(dataDirInfo.FullName, guid));
                }

                for (int i = 0; i < fileInfos.Length; i++)
                {
                    if (Path.GetFileNameWithoutExtension(fileInfos[i].FullName) == guid)
                        return Path.GetFullPath(Path.Combine(dataDirInfo.FullName, guid + fileInfos[i].Extension));
                }
            }

            // Always return a Empty string if the guid object does not exist
            return string.Empty;
        }

        public static string GetExtensionsPath(RuntimePlatform platform)
        {
            string extensionPath = string.Empty;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
#if UNITY_EDITOR
            string[] assets = AssetDatabase.FindAssets("t:" + typeof(ExtensionsReferenceAsset).Name, null);

            if (assets.Length != 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                AssetDatabase.LoadAssetAtPath(path, typeof(ExtensionsReferenceAsset));
                ExtensionsReferenceAsset extensionsReferenceAsset = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object)) as ExtensionsReferenceAsset;

                foreach (ExtensionsReferenceAsset.Reference reference in extensionsReferenceAsset.references)
                {
                    if (reference.platform == platform)
                    {
                        extensionPath = Path.GetFullPath(Path.Combine(new DirectoryInfo(Application.dataPath).Parent.FullName, AssetDatabase.GUIDToAssetPath(reference.guid)));
                        break;
                    }
                }
            }
#else
            extensionPath = Path.Combine(Application.dataPath, "Plugins");
#endif
            extensionPath = extensionPath.Replace('/', '\\');
#endif

            return extensionPath;
        }

        public static Material CreateHvrStandardMaterial()
        {
            // Create default material for the newly created HvrActor
            Shader shader = Shader.Find(Uniforms.ShaderNames.shader_HvrStandard);
            Material mat = new Material(shader);
            mat.name = "HvrStandard";
            return mat;
        }
    }
}