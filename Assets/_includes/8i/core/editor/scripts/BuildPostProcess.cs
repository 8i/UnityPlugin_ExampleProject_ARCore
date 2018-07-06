using HVR.Android.Editor;
using HVR.iOS.Editor.Xcode;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HVR.Editor
{
    class BuildPostProcess
    {
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string buildPath)
        {
            if (target == BuildTarget.Android)
            {
                // If this was a custom Android build, then delete the streaming assets folder to keep the project clean
                if (EditorPrefs.HasKey(AndroidEditorUtilities.editorPrefs_AndroidCustomBuild) &&
                    EditorPrefs.GetBool(AndroidEditorUtilities.editorPrefs_AndroidCustomBuild) == true)
                {
                    string streamingAssets = Application.streamingAssetsPath;
                    string streamingAssets_HVR = streamingAssets + Path.DirectorySeparatorChar + Uniforms.buildDataPath;

                    // Clean up the project by deleting the hvrdata folder
                    if (Directory.Exists(streamingAssets_HVR))
                        Directory.Delete(streamingAssets_HVR, true);

                    if (File.Exists(streamingAssets_HVR + ".meta"))
                        File.Delete(streamingAssets_HVR + ".meta");

                    // If the Assets/StreamingAssets folder is empty, just delete it too
                    // Just a nice thing to do to keep the project clean
                    if (Directory.Exists(streamingAssets))
                    {
                        if (Directory.GetFiles(streamingAssets).Length == 0 &&
                            Directory.GetDirectories(streamingAssets).Length == 0)
                        {
                            Directory.Delete(streamingAssets, true);

                            if (File.Exists(streamingAssets + ".meta"))
                                File.Delete(streamingAssets + ".meta");
                        }
                    }

                    // Make sure to reset the editorprefs so this doesn't occur next time the user makes a build
                    EditorPrefs.SetBool(AndroidEditorUtilities.editorPrefs_AndroidCustomBuild, false);
                }
            }
            else
            {
                FileInfo buildPathFileInfo = new FileInfo(buildPath);

                string exeName = buildPathFileInfo.Name;

                // Catches issues with Linux builds
                if (!string.IsNullOrEmpty(buildPathFileInfo.Extension))
                    exeName = exeName.Replace(buildPathFileInfo.Extension, "");

                string buildDir = buildPathFileInfo.Directory.FullName;
                string buildDataDir = Path.Combine(buildDir, exeName + "_Data");
                string buildDataPluginsDir = Path.Combine(buildDataDir, "Plugins");
                string buildDataOutputDir = Path.Combine(buildDataDir, Uniforms.buildDataPath);

                BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

                if (buildTarget == BuildTarget.iOS)
                {
                    // Anything in the Data directory is automatically bundled with the app
                    string hvrDataPath = Path.Combine("Data", Uniforms.buildDataPath);
                    buildDataOutputDir = Path.Combine(buildPathFileInfo.FullName, hvrDataPath);

                    // --------------------------------------------------
                    // Ensure that the 8i Framework and Libraries are added

                    string frameworkName = "HVRPlayerInterface.framework";

                    DirectoryInfo unity_project_assets_dir = new DirectoryInfo(Application.dataPath);
                    DirectoryInfo unity_project_root_dir = unity_project_assets_dir.Parent;

                    string build_pbxproj_file_path = buildPath + "/" + PBXProject.GetUnityTargetName() + ".xcodeproj/project.pbxproj";

                    PBXProject pbxProj = new PBXProject();
                    var file = File.ReadAllText(build_pbxproj_file_path);
                    pbxProj.ReadFromString(file);

                    string pbxTargetGuid = pbxProj.TargetGuidByName(PBXProject.GetUnityTargetName());
                    string pbxTestTargetGuid = pbxProj.TargetGuidByName(PBXProject.GetUnityTestTargetName());

                    if (EditorSharedResources.instance.plugin_HvrPlayerInterfaceFramework_macOS != null)
                    {
                        string pluginAssetPath = AssetDatabase.GetAssetPath(EditorSharedResources.instance.plugin_HvrPlayerInterfaceFramework_macOS);

                        string path_framework_fullPath = Path.Combine(unity_project_root_dir.FullName, pluginAssetPath);
                        string path_framework_relative_to_assets_dir = path_framework_fullPath.Remove(0, unity_project_assets_dir.FullName.Length);
                        if (path_framework_relative_to_assets_dir[0] == '/')
                        {
                            path_framework_relative_to_assets_dir = path_framework_relative_to_assets_dir.Substring(1, path_framework_relative_to_assets_dir.Length - 1);
                        }
                        string path_framework_relative_to_build_root = Path.Combine("Frameworks", path_framework_relative_to_assets_dir);
                        string path_framework_full = Path.Combine(buildPath, path_framework_relative_to_build_root);

                        pbxProj.RemoveFrameworkFromProject(pbxTargetGuid, path_framework_relative_to_build_root, PBXSourceTree.Source);

                        string guid = pbxProj.FindFileGuidByProjectPath(path_framework_relative_to_build_root);

                        if (guid != null)
                            pbxProj.RemoveFile(guid);

                        if (Directory.Exists(path_framework_full))
                            Directory.Delete(path_framework_full, true);

                        string framework_unity_project_full_path = Path.Combine(unity_project_root_dir.FullName, pluginAssetPath);
                        string framework_build_project_relative_path = "Frameworks" + Path.DirectorySeparatorChar + frameworkName;
                        string framework_build_project_full_path = buildPath + Path.DirectorySeparatorChar + framework_build_project_relative_path;

                        if (Directory.Exists(framework_build_project_full_path))
                            Directory.Delete(framework_build_project_full_path, true);

                        Helper.DirectoryCopy(framework_unity_project_full_path, framework_build_project_full_path, true);

                        // Ensure the project includes the following locations when looking for the framework
                        foreach (var e in pbxProj.buildConfigs.GetObjects())
                        {
                            e.AddProperty("FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks");
                            e.AddProperty("LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
                            e.SetProperty("ONLY_ACTIVE_ARCH", "YES");
                        }

                        pbxProj.AddFrameworkToProject(pbxTargetGuid, frameworkName, false, "Frameworks" + Path.DirectorySeparatorChar, PBXSourceTree.Source);
                        pbxProj.AddDynamicFrameworkToProject(pbxTargetGuid, framework_build_project_relative_path);
                    }
                    else
                    {
                        Debug.LogError("Unable to find HVRPlayerInterface.framework. It may needed to be added manually to the XCode Project.");
                    }

                    // iOS 8 required for framework linking, for both target and test target
                    pbxProj.SetBuildProperty(pbxTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "8.0");
                    pbxProj.SetBuildProperty(pbxTestTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "8.0");
#if UNITY_2018_1_OR_NEWER
                    pbxProj.SetBuildProperty(pbxTargetGuid, "CODE_SIGN_IDENTITY", "iPhone Developer");
                    pbxProj.SetBuildProperty(pbxTargetGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Developer");
                    pbxProj.SetBuildProperty(pbxTestTargetGuid, "CODE_SIGN_IDENTITY", "iPhone Developer");
                    pbxProj.SetBuildProperty(pbxTestTargetGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Developer");
#endif                    

                    File.WriteAllText(build_pbxproj_file_path, pbxProj.WriteToString());

                    // To set code sign attributes to framework   
                    string contents = File.ReadAllText(build_pbxproj_file_path);
                    contents = Regex.Replace(contents,
                        "(?<=Embed Frameworks)(?:.*)(\\/\\* HVRPlayerInterface\\.framework \\*\\/)(?=; };)",
                        m => m.Value.Replace("/* HVRPlayerInterface.framework */",
                            "/* HVRPlayerInterface.framework */; settings = {ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, ); }"));

                    File.WriteAllText(build_pbxproj_file_path, contents);
                }
#if UNITY_2017_3_OR_NEWER
                else if (buildTarget == BuildTarget.StandaloneOSX)
#else
                else if (buildTarget == BuildTarget.StandaloneOSXIntel ||
                        buildTarget == BuildTarget.StandaloneOSXIntel64)
#endif
                {
                    string appPath = buildPathFileInfo.FullName;
                    string appBundleContentsDir = Path.Combine(appPath, "Contents");
                    buildDataOutputDir = Path.Combine(appBundleContentsDir, Uniforms.buildDataPath);
                }

                // Exports all .hvr data from the project into the built player's data folder.
                PostBuildDataCopier.instance.CopyHvrDataFromScenesInBuildSettings(buildDataOutputDir);

#if UNITY_2017_3_OR_NEWER
                if (buildTarget == BuildTarget.StandaloneOSX)
                {
                    // HACK: move the .bundles to proper location as there's no way to manually or programmatically to do it in 2017.3
                    // Only manually modify into .meta file is helpful but it will be override by Unity versions earlier than 2017.2 so it's not an option.
                    string appContentPath = Path.Combine(buildPathFileInfo.FullName, "Contents/");
                    string appBundleUniversalPluginsDir = Path.Combine(appContentPath, "Plugins/");
                    string appBundleX64PluginsDir = Path.Combine(appContentPath, "Plugins/x86_64/");

                    if (Directory.Exists(appBundleX64PluginsDir))
                    {
                        string[] subdirectories = Directory.GetDirectories(appBundleX64PluginsDir);
                        foreach (string subdirectory in subdirectories)
                        {
                            string binaryName = subdirectory.Substring(subdirectory.LastIndexOf('/') + 1);
                            FileUtil.MoveFileOrDirectory(subdirectory, Path.Combine(appBundleUniversalPluginsDir, binaryName));
                        }
                    }
                }
#endif

                if (target == BuildTarget.StandaloneWindows ||
                    target == BuildTarget.StandaloneWindows64)
                {
                    // Copy required windows .dlls from the plugins directory, next to the exe
                    // d3dcompiler_47.dll   - Windows 7 Support
                    // vcruntime140.dll     - Support for computers that do not have the Visual Studio 2015 redistributable installed
                    // msvcp140.dll         - Support for computers that do not have the Visual Studio 2015 redistributable installed
                    string[] dlls = new string[3]
                    {
                        "d3dcompiler_47.dll",
                        "vcruntime140.dll",
                        "msvcp140.dll"
                    };

                    for (int i = 0; i < dlls.Length; i++)
                    {
                        string dllName = dlls[i];
                        string source_path = Path.Combine(buildDataPluginsDir, dllName);
                        string target_path = Path.Combine(buildDir, dllName);

                        if (File.Exists(source_path))
                        {
                            File.Copy(source_path, target_path, true);
                        }
                        else
                        {
                            Debug.LogError("Dll was not found at " + source_path + " \nCheck in your build settings that the target Architecture is set to x86_64");
                        }
                    }
                }
            }
        }

#if UNITY_5_6_OR_NEWER
        // Replaces the first C++ macro with the given name in the source file. Only changes
        // single-line macro declarations, if multi-line macro declaration is detected, the
        // function returns without changing it. Macro name must be a valid C++ identifier.
        internal static bool ReplaceCppMacro(string[] lines, string name, string newValue)
        {
            bool replaced = false;
            Regex matchRegex = new Regex(@"^.*#\s*define\s+" + name);
            Regex replaceRegex = new Regex(@"^.*#\s*define\s+" + name + @"(:?|\s|\s.*[^\\])$");
            for (int i = 0; i < lines.Count(); i++)
            {
                if (matchRegex.Match(lines[i]).Success)
                {
                    lines[i] = replaceRegex.Replace(lines[i], "#define " + name + " " + newValue);
                    replaced = true;
                }
            }
            return replaced;
        }

        internal static void AddOrReplaceCppMacro(ref string[] lines, string name, string newValue)
        {
            if (ReplaceCppMacro(lines, name, newValue) == false)
            {
                System.Array.Resize(ref lines, lines.Length + 1);
                lines[lines.Length - 1] = "#define " + name + " " + newValue;
            }
        }

        static void UpdateDefinesInFile(string file, Dictionary<string, bool> valuesToUpdate)
        {
            string[] src = File.ReadAllLines(file);
            var copy = (string[])src.Clone();

            foreach (var kvp in valuesToUpdate)
                AddOrReplaceCppMacro(ref copy, kvp.Key, kvp.Value ? "1" : "0");

            if (!copy.SequenceEqual(src))
                File.WriteAllLines(file, copy);
        }
#endif
    }

    public class PostBuildDataCopier
    {
        static PostBuildDataCopier m_instance;
        public static PostBuildDataCopier instance
        {
            get
            {
                if (m_instance == null)
                    m_instance = new PostBuildDataCopier();

                return m_instance;
            }
        }

        FileCopier fileCopier;

        public PostBuildDataCopier()
        {
            fileCopier = null;
        }

        public void CopyHvrDataFromSceneList(string[] scenes, string directory)
        {
            List<string> guids = GetGuidsForDataToExport(scenes);
            ExportAssetData(guids, directory);
        }

        public void CopyHvrDataFromScenesInBuildSettings(string directory)
        {
            string[] scenes = EditorHelper.GetEnabledScenesInBuild();
            List<string> guids = GetGuidsForDataToExport(scenes);
            ExportAssetData(guids, directory);
        }

        public void ExportAssetData(List<string> guids, string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);

            Directory.CreateDirectory(directory);

            HashSet<string[]> copyMappings = new HashSet<string[]>();

            for (int i = 0; i < guids.Count; i++)
            {
                string dataGuid = guids[i];
                string dataPath = EditorHelper.GetAssetPathFromGUID(dataGuid);

                // Skip if data does not exist
                if (string.IsNullOrEmpty(dataPath))
                    continue;

                if (Application.platform != RuntimePlatform.OSXEditor
#if UNITY_EDITOR_LINUX
                    && Application.platform != RuntimePlatform.LinuxEditor
#endif
                    )
                {
                    // Just in case of different file systems returning different results
                    dataPath = dataPath.Replace("/", "\\");
                    dataPath = dataPath.Replace(@"\", "\\");
                }

                FileAttributes fileAttr = File.GetAttributes(dataPath);

                if ((fileAttr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    DirectoryInfo dataFolder = new DirectoryInfo(dataPath);
                    FileInfo[] files = dataFolder.GetFiles();

                    for (int j = 0; j < files.Length; j++)
                    {
                        // Skip any meta files as they aren't wanted
                        if (files[j].FullName.EndsWith(".meta"))
                            continue;

                        string path = Path.Combine(directory, dataGuid);
                        path = Path.Combine(path, files[j].Name);

                        copyMappings.Add(new string[] { files[j].FullName, path });
                    }
                }
                else
                {
                    FileInfo sourceFile = new FileInfo(dataPath);
                    FileInfo destinationFile = new FileInfo(Path.Combine(directory, dataGuid) + sourceFile.Extension);
                    copyMappings.Add(new string[] { sourceFile.FullName, destinationFile.FullName });
                }
            }

            fileCopier = new FileCopier();
            fileCopier.Start(copyMappings.ToArray(), true);
        }

        private List<string> GetGuidsForDataToExport(string[] scenes)
        {
            List<string> guids = new List<string>();

            for (int i = 0; i < scenes.Length; i++)
            {
                Scene scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);

                GameObject[] sceneRootObjects = scene.GetRootGameObjects();

                List<GameObject> everyObjectInScene = new List<GameObject>();

                foreach (GameObject go in sceneRootObjects)
                {
                    everyObjectInScene.Add(go);
                    everyObjectInScene.AddRange(Helper.GetGameObjectChildrenRecursive(go));
                }

                for (int o = 0; o < everyObjectInScene.Count; o++)
                {
                    HvrActor[] allHvrActors = everyObjectInScene[o].GetComponents<HvrActor>();

                    for (int p = 0; p < allHvrActors.Length; p++)
                    {
                        HvrActor actor = allHvrActors[p];

                        if (actor != null &&
                            actor.dataMode == HvrActor.eDataMode.reference &&
                            actor.data != null)
                        {
                            // Early exit if the actor does not have any data assigned
                            if (actor.data == string.Empty)
                                continue;

                            // Do not copy the same data twice
                            if (guids.Contains(actor.data))
                                continue;

                            guids.Add(actor.data);
                        }
                    }

                    HvrDataBuildInclude[] includes = everyObjectInScene[o].GetComponents<HvrDataBuildInclude>();

                    for (int p = 0; p < includes.Length; p++)
                    {
                        HvrDataBuildInclude include = includes[p];

                        if (include != null &&
                            include.dataReference != null)
                        {
                            for (int d = 0; d < include.dataReference.data.Count; d++)
                            {
                                string guid = include.dataReference.data[d].guid;

                                // Early exit if the actor does not have any data assigned
                                if (guid == "")
                                    continue;

                                // Do not copy the same data twice
                                if (guids.Contains(guid))
                                    continue;

                                guids.Add(guid);
                            }
                        }
                    }
                }
            }
            return guids;
        }
    }
}
