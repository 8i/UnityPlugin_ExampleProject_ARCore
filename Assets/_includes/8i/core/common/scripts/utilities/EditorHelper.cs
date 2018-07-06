#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HVR
{
    public class EditorUpdateHelper
    {
        static private EditorUpdateHelper instance;
        static public EditorUpdateHelper Self()
        {
            if (instance == null)
                instance = new EditorUpdateHelper();

            return instance;
        }

        public int updateTick = -1;

        public EditorUpdateHelper()
        {
            EditorApplication.update -= UnityEditorUpdate;
            EditorApplication.update += UnityEditorUpdate;
        }

        ~EditorUpdateHelper()
        {
            EditorApplication.update -= UnityEditorUpdate;
        }

        public void UnityEditorUpdate()
        {
            // Edge case catching
            if (updateTick >= int.MaxValue)
                updateTick = 0;

            updateTick++;
        }
    }

    public class EditorHelper
    {
        private static Camera[] internalCameras;
        private static int lastUpdateTick = -1;

        public static bool IsSceneViewCamera(Camera cam, bool forceUpdate = false)
        {
            // Added so InternalEditorUtility.GetSceneViewCameras() is not being called
            // Every Editor tick. This function is quite expensive and can generate a
            // lot of garbage. So to ensure a smooth editor experience we defer the update
            if (internalCameras == null ||
                lastUpdateTick != EditorUpdateHelper.Self().updateTick ||
                forceUpdate)
            {
                lastUpdateTick = EditorUpdateHelper.Self().updateTick;

                internalCameras = InternalEditorUtility.GetSceneViewCameras();
            }

            for (int i = 0; i < internalCameras.Length; i++)
            {
                if (internalCameras[i] == cam)
                    return true;
            }

            return false;
        }

        public static bool IsPreviewCamera(Camera cam)
        {
            if (cam.hideFlags == HideFlags.HideAndDontSave &&
                cam.name == "Preview Camera")
            {
                return true;
            }

            return false;
        }

        public static EditorWindow GetMainGameView()
        {
            System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            System.Reflection.MethodInfo GetMainGameView = T.GetMethod("GetMainGameView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            System.Object obj = GetMainGameView.Invoke(null, null);

            return (EditorWindow)obj;
        }

        public static void RepaintAllGameViews()
        {
            System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            System.Reflection.MethodInfo RepaintAll = T.GetMethod("RepaintAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            RepaintAll.Invoke(null, null);
        }

        public static Rect GetMainGameViewRect()
        {
            System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            System.Reflection.MethodInfo GetMainGameView = T.GetMethod("GetMainGameView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            System.Object obj = GetMainGameView.Invoke(null, null);

            System.Reflection.PropertyInfo viewInWindow = T.GetProperty("viewInWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Object rect = viewInWindow.GetValue(obj, null);

            return (Rect)rect;
        }

        /// <summary>
        /// Used to get assets of a certain type and file extension from entire project
        /// </summary>
        /// <param name="type">The type to retrieve. eg typeof(GameObject).</param>
        /// <param name="fileExtension">The file extention the type uses eg ".prefab".</param>
        /// <returns>An Object array of assets.</returns>
        public static T[] GetProjectAssetsOfType<T>(string fileExtension) where T : UnityEngine.Object
        {
            List<T> tempObjects = new List<T>();
            DirectoryInfo directory = new DirectoryInfo(Application.dataPath);
            FileInfo[] goFileInfo = directory.GetFiles("*" + fileExtension, SearchOption.AllDirectories);

            int i = 0;
            int goFileInfoLength = goFileInfo.Length;
            FileInfo tempGoFileInfo; string tempFilePath;
            T tempGO;
            for (; i < goFileInfoLength; i++)
            {
                tempGoFileInfo = goFileInfo[i];
                if (tempGoFileInfo == null)
                    continue;

                tempFilePath = tempGoFileInfo.FullName;
                tempFilePath = tempFilePath.Replace(@"\", "/").Replace(Application.dataPath, "Assets");
                tempGO = AssetDatabase.LoadAssetAtPath(tempFilePath, typeof(T)) as T;
                if (tempGO == null)
                {
                    continue;
                }
                else if (!(tempGO is T))
                {
                    continue;
                }

                tempObjects.Add(tempGO);
            }

            return tempObjects.ToArray();
        }

        public static string GetFullPathToAsset(UnityEngine.Object asset)
        {
            string datapath = Application.dataPath;
            datapath = datapath.Substring(0, datapath.Length - 6);

            string assetPath = AssetDatabase.GetAssetPath(asset.GetInstanceID());

            return datapath + assetPath;
        }

        public static string GetAssetPathFromGUID(string guid)
        {
            string projectAssetPath = Application.dataPath;

            projectAssetPath = projectAssetPath.Substring(0, projectAssetPath.Length - "Assets".Length);
            string assetPathFromGUID = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetDatabase.IsValidFolder(assetPathFromGUID))
            {
                return Path.Combine(projectAssetPath, assetPathFromGUID);
            }
            else
            {
                // Should also check for file existence. AssetDatabase.IsValidFolder() fails on macOS when the 
                // provided string represents a file.
                if (File.Exists(assetPathFromGUID))
                {
                    return Path.Combine(projectAssetPath, assetPathFromGUID);
                }
            }

            return "";
        }

        public static string[] GetEnabledScenesInBuild()
        {
            return (from scene in EditorBuildSettings.scenes where scene.enabled select scene.path).ToArray();
        }

        public static string[] GetAllScenesInBuild()
        {
            return (from scene in EditorBuildSettings.scenes select scene.path).ToArray();
        }

        public static string GetScenePath(string scene)
        {
            foreach (string scenePath in GetAllScenes())
            {
                if (scenePath.EndsWith(scene + ".unity"))
                {
                    return scenePath;
                }
            }
            return null;
        }

        public static string[] GetAllScenes()
        {
            return (from scene in AssetDatabase.GetAllAssetPaths() where scene.EndsWith(".unity") select scene).ToArray();
        }

        public static void DrawBounds(Bounds bounds, Transform transform)
        {
            Vector3 v3Center = bounds.center;
            Vector3 v3Extents = bounds.extents;

            Vector3 v3FrontTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z);  // Front top left corner
            Vector3 v3FrontTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z);  // Front top right corner
            Vector3 v3FrontBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z);  // Front bottom left corner
            Vector3 v3FrontBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z);  // Front bottom right corner
            Vector3 v3BackTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z);  // Back top left corner
            Vector3 v3BackTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z);  // Back top right corner
            Vector3 v3BackBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z);  // Back bottom left corner
            Vector3 v3BackBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z);  // Back bottom right corner

            v3FrontTopLeft = transform.TransformPoint(v3FrontTopLeft);
            v3FrontTopRight = transform.TransformPoint(v3FrontTopRight);
            v3FrontBottomLeft = transform.TransformPoint(v3FrontBottomLeft);
            v3FrontBottomRight = transform.TransformPoint(v3FrontBottomRight);
            v3BackTopLeft = transform.TransformPoint(v3BackTopLeft);
            v3BackTopRight = transform.TransformPoint(v3BackTopRight);
            v3BackBottomLeft = transform.TransformPoint(v3BackBottomLeft);
            v3BackBottomRight = transform.TransformPoint(v3BackBottomRight);

            Gizmos.DrawLine(v3FrontTopLeft, v3FrontTopRight);
            Gizmos.DrawLine(v3FrontTopRight, v3FrontBottomRight);
            Gizmos.DrawLine(v3FrontBottomRight, v3FrontBottomLeft);
            Gizmos.DrawLine(v3FrontBottomLeft, v3FrontTopLeft);

            Gizmos.DrawLine(v3BackTopLeft, v3BackTopRight);
            Gizmos.DrawLine(v3BackTopRight, v3BackBottomRight);
            Gizmos.DrawLine(v3BackBottomRight, v3BackBottomLeft);
            Gizmos.DrawLine(v3BackBottomLeft, v3BackTopLeft);

            Gizmos.DrawLine(v3FrontTopLeft, v3BackTopLeft);
            Gizmos.DrawLine(v3FrontTopRight, v3BackTopRight);
            Gizmos.DrawLine(v3FrontBottomRight, v3BackBottomRight);
            Gizmos.DrawLine(v3FrontBottomLeft, v3BackBottomLeft);
        }

        public static string GetResourcePath(ScriptableObject type)
        {
            var ms = MonoScript.FromScriptableObject(type);
            var path = AssetDatabase.GetAssetPath(ms);
            path = Path.GetDirectoryName(path);
            return path;
        }

        public static long GetAssetLastWriteTimeTicks(UnityEngine.Object obj)
        {
            // Allow for automatic shader updates while in the editor 
            if (obj != null)
            {
                string projectAssetPath = Application.dataPath;
                projectAssetPath = projectAssetPath.Substring(0, projectAssetPath.Length - "Assets".Length);
                string assetPath = AssetDatabase.GetAssetPath(obj.GetInstanceID());

                return File.GetLastWriteTime(assetPath).Ticks;
            }

            return 0;
        }

        public static bool IsLightRealtime(Light light)
        {
#if UNITY_5_6_OR_NEWER
            if (light.lightmapBakeType == LightmapBakeType.Baked)
                return false;
#else
            //  -2 means that light is not baked, -1 is reserved for internal use.
            // When startup or duplicating the lights, bakedIndex can be -1 for realtime ones.
            // file:///C:/Program%20Files/Unity5.4/Editor/Data/Documentation/en/ScriptReference/Light-bakedIndex.html
            if (light.bakedIndex != -2 && light.bakedIndex != -1)
                return false;
#endif

            return true;
        }
    }

    public static class EditorCallbacks
    {
        public delegate void OnLoadSceneEvent();
        public static OnLoadSceneEvent onLoadScene;

        public delegate void OnSceneAssetOpenEvent();
        public static OnSceneAssetOpenEvent onSceneAssetOpen;

        static int previousSceneHashCode;

        // Check whether a new scene has been loaded and if we need to reset this component. 
        // If we do not, then rendertextures and other assets will persist on any HvrRender components...
        // attached to SceneView cameras

        [UnityEditor.Callbacks.OnOpenAsset] // Called on asset being double clicked and opened in UnityEditor project window
        static bool OnOpenAsset(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID).GetType() == typeof(UnityEditor.SceneAsset))
            {
                if (onSceneAssetOpen != null)
                    onSceneAssetOpen();

                // Delay the check until the next EditorApplication tick. This will allow the check to occur
                // if the user opened a scene, but hit cancel and didn't want anything to change.
                EditorApplication.delayCall += CheckIfSceneLoaded;

                previousSceneHashCode = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
            }

            return false; // we did not handle the open just listened to it
        }

        static void CheckIfSceneLoaded()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // The will be clean if the user loaded a scene, or reloaded the current active one. But
            // this will not handle the case where the user has reloaded the same scene but it had no changes

            // The HashCode of the scene will change if the user reloaded the current active scene.
            if (activeScene.isDirty == false ||
                previousSceneHashCode != activeScene.GetHashCode())
            {
                if (onLoadScene != null)
                    onLoadScene();
            }
        }
    }
}

#endif
