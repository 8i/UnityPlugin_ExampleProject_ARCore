using HVR.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HVR.Android.Editor
{
    public class AndroidEditorUtilities : MonoBehaviour
    {
        public const string editorPrefs_AndroidCustomBuild = "HVR_ANDROID_CUSTOMBUILD";
        public const string editorPrefs_AndroidBuildPath = "HVR_ANDROID_BUILDPATH";

        public static string GetBuildPath(bool force)
        {
            string path = "";

            if (string.IsNullOrEmpty(EditorPrefs.GetString(editorPrefs_AndroidBuildPath)) || force)
            {
                path = EditorUtility.SaveFilePanel("Choose Location of Built Game", "", PlayerSettings.productName, "apk");
                EditorPrefs.SetString(editorPrefs_AndroidBuildPath, path);
            }
            else
            {
                path = EditorPrefs.GetString(editorPrefs_AndroidBuildPath);
            }

            return path;
        }

        public static void PrepareBuild()
        {
            PostBuildDataCopier.instance.CopyHvrDataFromScenesInBuildSettings(Path.Combine(Application.streamingAssetsPath, Uniforms.buildDataPath));
            ProcessStreamingAssets();
        }

        static void ProcessStreamingAssets()
        {
            string streamingAssetsPath = Application.streamingAssetsPath;

            List<string> files = new List<string>();
            GetAllFiles(streamingAssetsPath + Path.DirectorySeparatorChar + Uniforms.buildDataPath, files);

            string manifest = "";
            long size = 0;
            foreach (string file in files)
            {
                manifest += '\n';

                string path = Path.Combine(streamingAssetsPath, Uniforms.buildDataPath);
                path = Path.Combine(path, file);

                FileInfo fileInfo = new FileInfo(path);

                size += fileInfo.Length;
                manifest += file;
            }
            manifest = size + manifest;

            string manifestPath = Path.Combine(streamingAssetsPath, Uniforms.buildDataPath);
            manifestPath = Path.Combine(manifestPath, "manifest.txt");

            File.WriteAllText(manifestPath, manifest);
        }

        static void GetAllFiles(string originalDirectory, List<string> files, string currentDirectory = null)
        {
            if (currentDirectory == null)
                currentDirectory = originalDirectory;

            string[] filesHere = Directory.GetFiles(currentDirectory);
            string[] dirsHere = Directory.GetDirectories(currentDirectory);

            foreach (string file in filesHere)
            {
                string cleanName = file.Replace(originalDirectory, "");
                cleanName = cleanName.Replace('\\', '/');
                if (cleanName.Length > 0)
                {
                    if (cleanName[0] == '/')
                    {
                        cleanName = cleanName.Substring(1);
                    }
                }
                if (cleanName.Length > 0)
                {
                    files.Add(cleanName);
                }
            }

            foreach (string dir in dirsHere)
            {
                GetAllFiles(originalDirectory, files, dir);
            }
        }

        public static void BuildPlayer(string outputPath, BuildOptions buildOptions)
        {
            EditorPrefs.SetBool(editorPrefs_AndroidCustomBuild, true);
            BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, outputPath, BuildTarget.Android, buildOptions);
        }

        /// <summary>
        /// Checks if the AndroidUnpackingScene is the first scene in the EditorBuidSettings scenes
        /// </summary>
        /// <returns></returns>
        static public bool IsAndroidUnpackingSceneFirstInSceneList()
        {
            if (EditorBuildSettings.scenes.Length > 0)
            {
                string unpackingScenePath = AssetDatabase.GetAssetPath(EditorSharedResources.instance.androidUnpackingScene);
                return EditorBuildSettings.scenes[0].path == unpackingScenePath;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the AndroidUnpackingScene is enabled in the EditorBuildSettings scenes
        /// </summary>
        /// <returns></returns>
        static public bool IsAndroidUnpackingSceneEnabled()
        {
            if (IsAndroidUnpackingSceneFirstInSceneList())
            {
                return EditorBuildSettings.scenes[0].enabled;
            }
            else
            {
                return false;
            }
        }

        public static void AddAndroidUnpackingScene()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            string unpackingScenePath = AssetDatabase.GetAssetPath(EditorSharedResources.instance.androidUnpackingScene);

            if (scenes.Count == 0)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(unpackingScenePath, true));
            }
            else
            {
                if (!IsAndroidUnpackingSceneFirstInSceneList())
                {
                    // Try to find the scene list if it's in the list...
                    EditorBuildSettingsScene unpackingScene = scenes.FirstOrDefault(x => x.path == unpackingScenePath);

                    if (unpackingScene == null)
                    {
                        // Okay, the scene is not in the list. Just add it.
                        scenes.Insert(0, new EditorBuildSettingsScene(unpackingScenePath, true));
                    }
                    else
                    {
                        // Found it! Okay, now remove it from it's current location...
                        int idx = scenes.IndexOf(unpackingScene);
                        scenes.RemoveAt(idx);

                        // And move it the first scene in the list!
                        scenes.Insert(0, unpackingScene);
                    }
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void EnableAndroidUnpackingScene()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            string unpackingScenePath = AssetDatabase.GetAssetPath(EditorSharedResources.instance.androidUnpackingScene);

            if (scenes.Count != 0)
            {
                // Try to find the scene list if it's in the list...
                EditorBuildSettingsScene unpackingScene = scenes.FirstOrDefault(x => x.path == unpackingScenePath);

                if (unpackingScene != null)
                {
                    int idx = scenes.ToList().IndexOf(unpackingScene);
                    scenes[idx].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                else
                {
                    Debug.LogError("AndroidUnpackingScene not found in EditorBuildSettings scenes!");
                }
            }
            else
            {
                Debug.LogError("No scenes are currently added to the EditorBuildSettings scenes!");
            }
        }
    }
}
