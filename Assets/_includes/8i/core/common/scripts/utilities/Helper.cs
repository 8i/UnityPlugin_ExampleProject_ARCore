using AOT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using HVR.Android;
using HVR.Interface;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace HVR
{
    public class Helper
    {
        public const float CENTIMETRES_TO_METRES = 1.0f / 100.0f;

        public static List<GameObject> GetGameObjectChildrenRecursive(GameObject parent)
        {
            List<GameObject> children = new List<GameObject>();

            foreach (Transform child in parent.transform)
            {
                children.Add(child.gameObject);
                children.AddRange(GetGameObjectChildrenRecursive(child.gameObject));
            }

            return children;
        }

        public static string GetParameterArgument(string parameterName)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.ToLower().StartsWith(parameterName.ToLower()))
                {
                    return arg.Substring(parameterName.Length);
                }
            }
            return null;
        }

        public static float GetCurrentTime()
        {
            float time = Time.unscaledTime;

#if UNITY_EDITOR
            // Check here or else the EditorApplication.timeSinceStartup will be used
            // even if the Editor is in 'Play' mode but also 'Paused'
            if (!Application.isPlaying)
                time = (float)EditorApplication.timeSinceStartup;
#endif

            return time;
        }

        public static int GetCurrentFrameCount()
        {
            int frame = 0;

            if (Application.isEditor && !Application.isPlaying)
            {
#if UNITY_EDITOR
                frame = EditorUpdateHelper.Self().updateTick;
#endif
            }
            else
            {
                frame = Time.frameCount;
            }

            return frame;
        }

        public static string GetArchitecture()
        {
#if UNITY_64
            return "X64";
#endif
#if UNITY_32
            return "X86";
#endif
#if !UNITY_64 && !UNITY_32
            return SystemInfo.operatingSystem.ToString();
#endif
        }

        public static double TimeSinceEpoch(DateTime dt)
        {
            DateTime epoch = new DateTime(1970, 1, 1);
            TimeSpan span = dt - epoch;
            return span.TotalMilliseconds;
        }

        public static string GetRenderingAPI()
        {
            return SystemInfo.graphicsDeviceVersion;
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        private static string[] alphabetArray = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

        public static string IntToLetters(int value)
        {
            string stringValue = value.ToString();
            string[] result = new string[stringValue.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = alphabetArray[int.Parse(stringValue[i].ToString())];
            }

            return string.Concat(result);
        }

        public static void HvrWorldForceRedraw()
        {
            HvrLight[] hvrLightList = UnityEngine.Object.FindObjectsOfType<HvrLight>();
            foreach(HvrLight hvrLight in hvrLightList)
            {
                Light light = hvrLight.GetDependentLight();
                if (light != null)
                {
                    hvrLight.Update();
                    hvrLight.WillRender();
                }       
            }


            HvrRender[] hvrRenderList = UnityEngine.Object.FindObjectsOfType<HvrRender>();
            foreach(HvrRender hvrRender in hvrRenderList)
            {
                hvrRender.ForceRedraw();
            }
        }

        public static RenderTextureFormat GetSupportedRenderTextureFormatForDepthBlit()
        {
#if UNITY_2017_3_OR_NEWER
            if (GetRenderingAPI().StartsWith("Direct"))
            {
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
                {
                    // Unity iOS doesn't support RFloat but only RHalf format. See SystemInfo.SupportsRenderTextureFormat()
                    // But it can use RenderTextureFormat.Depth format for blitting. However for the sake of blitting
                    return RenderTextureFormat.RHalf;
                }
            }
            else
            {
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
                {
                    return RenderTextureFormat.RFloat;
                }
                else
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
                {
                    // Unity iOS doesn't support RFloat but only RHalf format. See SystemInfo.SupportsRenderTextureFormat()
                    // But it can use RenderTextureFormat.Depth format for blitting. However for the sake of blitting
                    return RenderTextureFormat.RHalf;
                }
            }

#else

            // Some render texture formats are not supported on all platforms
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {
                return RenderTextureFormat.RFloat;
            }
            else
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
            {
                // Unity iOS doesn't support RFloat but only RHalf format. See SystemInfo.SupportsRenderTextureFormat()
                // But it can use RenderTextureFormat.Depth format for blitting. However for the sake of blitting
                return RenderTextureFormat.RHalf;
            }
#endif            

            // If none of the above formats are supported, fallback to Default.
            // This format will likely not work however, but RenderTextureFormat is not nullable
            return RenderTextureFormat.Default;
        }

        public static bool IsLayerVisibleInCullingMask(int layer, int cullingMask)
        {
            return (((1 << layer) & cullingMask) != 0);
        }

        public static T PtrToStruct<T>(IntPtr ptr)
        {
            try
            {
                if (ptr != IntPtr.Zero)
                {
                    T obj = (T)Marshal.PtrToStructure(ptr, typeof(T));
                    return obj;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            return default(T);
        }

        public static T GCHandleToObject<T>(GCHandle handle)
        {
            try
            {
                if (handle.Target.GetType() == typeof(T))
                {
                    T obj = (T)handle.Target;
                    return obj;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            return default(T);
        }
    }

    public class JSONHelper
    {
        public static string DictionaryToJson(Dictionary<string, string> dictionary)
        {
            List<string> entries = new List<string>();

            for (int i = 0; i < dictionary.Count; i++)
            {
                string key = dictionary.ElementAt(i).Key;
                string value = dictionary.ElementAt(i).Value;

                entries.Add(string.Format("\"{0}\": \"{1}\"", key, "," + value));
            }

            return "{" + string.Join(",", entries.ToArray()) + "}";
        }
    }

    public class MaterialHelper
    {
        public static bool RenderQueueIsTransparent(Material mat)
        {
            if (mat != null)
            {
                // Internally each queue is represented by integer index;
                // Background is 1000
                // Geometry is 2000
                // AlphaTest is 2450
                // Transparent is 3000
                // Overlay is 4000
                return mat.renderQueue >= 2450;
            }

            return false;
        }

        public static float GetMaterialTransparencyValue(Material mat)
        {
            if (mat != null)
            {
                if (mat != null && mat.HasProperty(Uniforms.ShaderProperties._MainColor))
                    return mat.GetColor(Uniforms.ShaderProperties._MainColor).a;

                if (mat != null && mat.HasProperty(Uniforms.ShaderProperties._ColorFactor))
                    return mat.GetColor(Uniforms.ShaderProperties._ColorFactor).a;
            }

            return 1.0f;
        }

        public static Color GetMaterialColorFactor(Material mat, Color defaultTint)
        {
            if (mat != null && mat.HasProperty(Uniforms.ShaderProperties._ColorFactor))
            {
                return mat.GetColor(Uniforms.ShaderProperties._ColorFactor);
            }

            return defaultTint;
        }

        public static Material CheckShaderAndCreateMaterial(Shader s, Material material)
        {
            if (!s)
            {
                Debug.Log("No shader specified");
                return null;
            }

            if (s.isSupported && material && material.shader == s)
                return material;

            if (!s.isSupported)
            {
                Debug.Log("The shader " + s.ToString() + " is not supported on this platform!");
                return null;
            }
            else
            {
                material = new Material(s);
                if (material)
                    return material;
                else return null;
            }
        }

        public static Material CreateMaterial(Shader s, Material m2Create)
        {
            if (!s)
            {
                Debug.Log("Missing shader");
                return null;
            }

            if (m2Create && (m2Create.shader == s) && (s.isSupported))
                return m2Create;

            if (!s.isSupported)
            {
                return null;
            }
            else
            {
                m2Create = new Material(s);
                if (m2Create)
                    return m2Create;
                else return null;
            }
        }
    }

    public class RenderTexturePixelHash
    {
        private Texture2D m_tex2D;
        private HashSet<int> m_hash;

        public RenderTexturePixelHash(int width, int height)
        {
            m_tex2D = new Texture2D(width, height, TextureFormat.ARGB32, false, true);
            m_hash = new HashSet<int>();
        }

        public HashSet<int> GetHash()
        {
            return m_hash;
        }

        public void UpdateHash(RenderTexture rt)
        {
            UpdateHash(rt, new Rect(0, 0, rt.width, rt.height));
        }

        public void UpdateHash(RenderTexture rt, Rect sampleRect)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = rt;
            m_tex2D.ReadPixels(sampleRect, 0, 0);
            RenderTexture.active = currentRT;

            m_hash.Clear();

            byte[] buffer = m_tex2D.GetRawTextureData();

            byte[] previousSection = new byte[4];

            for (var i = 0; i < buffer.Length; i += 4)
            {
                // This allows it to skip this section of the buffer if the data is the
                // same as the last section. Done to help improve the performance of adding
                // to the hashset
                if (buffer[i + 0] == previousSection[0] &&
                    buffer[i + 1] == previousSection[1] &&
                    buffer[i + 2] == previousSection[2] &&
                    buffer[i + 3] == previousSection[3])
                    continue;

                previousSection[0] = buffer[i + 0];
                previousSection[1] = buffer[i + 1];
                previousSection[2] = buffer[i + 2];
                previousSection[3] = buffer[i + 3];

                int bytesHash = BitConverter.ToInt32(buffer, i);
                m_hash.Add(bytesHash);
            }
        }
    }

    public static class BuildInfo
    {
        public static Dictionary<string, string> info
        {
            get
            {
                if (m_info.Count == 0)
                {
                    Update();
                }

                return m_info;
            }
        }

        private static Dictionary<string, string> m_info = new Dictionary<string, string>();

        static void Update()
        {
            UpdateHvrInfo();
            UpdateSettingsInfo();
            UpdateSystemInfo();
        }

        static void UpdateHvrInfo()
        {
            m_info["HVR_VersionInfo_VERSION"] = VersionInfo.VERSION;
            m_info["HVR_VersionInfo_GIT_HASH"] = VersionInfo.GIT_HASH;
            m_info["HVR_VersionInfo_GIT_BRANCH"] = VersionInfo.GIT_BRANCH;
            m_info["HVR_VersionInfo_BUILD_DATE"] = VersionInfo.BUILD_DATE;

            if (HvrPlayerInterface.Initialise())
            {
                m_info["HvrPlayerInterface_VERSION"] = HvrPlayerInterface.GetInfo(HvrPlayerInterfaceAPI.Player_GetInfo_Keys.VERSION);
                m_info["HvrPlayerInterface_BUILD_DATE"] = HvrPlayerInterface.GetInfo(HvrPlayerInterfaceAPI.Player_GetInfo_Keys.BUILD_DATE);
                m_info["HvrPlayerInterface_GIT_HASH"] = HvrPlayerInterface.GetInfo(HvrPlayerInterfaceAPI.Player_GetInfo_Keys.GIT_HASH);
            }
        }

        static void UpdateSettingsInfo()
        {
            m_info["UnityEngine_Screen_width"] = Screen.width.ToString();
            m_info["UnityEngine_Screen_height"] = Screen.height.ToString();
            m_info["UnityEngine_Screen_currentResolution_width"] = Screen.currentResolution.width.ToString();
            m_info["UnityEngine_Screen_currentResolution_height"] = Screen.currentResolution.height.ToString();
            m_info["UnityEngine_Screen_currentResolution_refreshRate"] = Screen.currentResolution.refreshRate.ToString();
            m_info["UnityEngine_Screen_fullScreen"] = Screen.fullScreen.ToString();
            m_info["UnityEngine_QualitySettings_GetQualityLevel"] = QualitySettings.GetQualityLevel().ToString();
            m_info["UnityEngine_QualitySettings_activeColorSpace"] = QualitySettings.activeColorSpace.ToString();
            m_info["UnityEngine_QualitySettings_antiAliasing"] = QualitySettings.antiAliasing.ToString();
            m_info["UnityEngine_QualitySettings_vSyncCount"] = QualitySettings.vSyncCount.ToString();
        }

        static void UpdateSystemInfo()
        {
            m_info["UnityEngine_SystemInfo_deviceModel"] = UnityEngine.SystemInfo.deviceModel;
            m_info["UnityEngine_SystemInfo_deviceName"] = UnityEngine.SystemInfo.deviceName;
            m_info["UnityEngine_SystemInfo_deviceType"] = UnityEngine.SystemInfo.deviceType.ToString();
            m_info["UnityEngine_SystemInfo_deviceUniqueIdentifier"] = UnityEngine.SystemInfo.deviceUniqueIdentifier;
            m_info["UnityEngine_SystemInfo_graphicsDeviceID"] = UnityEngine.SystemInfo.graphicsDeviceID.ToString();
            m_info["UnityEngine_SystemInfo_graphicsDeviceName"] = UnityEngine.SystemInfo.graphicsDeviceName;
            m_info["UnityEngine_SystemInfo_graphicsDeviceType"] = UnityEngine.SystemInfo.graphicsDeviceType.ToString();
            m_info["UnityEngine_SystemInfo_graphicsDeviceVendor"] = UnityEngine.SystemInfo.graphicsDeviceVendor;
            m_info["UnityEngine_SystemInfo_graphicsDeviceVendorID"] = UnityEngine.SystemInfo.graphicsDeviceVendorID.ToString();
            m_info["UnityEngine_SystemInfo_graphicsDeviceVersion"] = UnityEngine.SystemInfo.graphicsDeviceVersion;
            m_info["UnityEngine_SystemInfo_graphicsMemorySize"] = UnityEngine.SystemInfo.graphicsMemorySize.ToString();
            m_info["UnityEngine_SystemInfo_graphicsMultiThreaded"] = UnityEngine.SystemInfo.graphicsMultiThreaded.ToString();
            m_info["UnityEngine_SystemInfo_graphicsShaderLevel"] = UnityEngine.SystemInfo.graphicsShaderLevel.ToString();
            m_info["UnityEngine_SystemInfo_maxTextureSize"] = UnityEngine.SystemInfo.maxTextureSize.ToString();
            m_info["UnityEngine_SystemInfo_npotSupport"] = UnityEngine.SystemInfo.npotSupport.ToString();
            m_info["UnityEngine_SystemInfo_operatingSystem"] = UnityEngine.SystemInfo.operatingSystem;
            m_info["UnityEngine_SystemInfo_processorCount"] = UnityEngine.SystemInfo.processorCount.ToString();
            m_info["UnityEngine_SystemInfo_processorFrequency"] = UnityEngine.SystemInfo.processorFrequency.ToString();
            m_info["UnityEngine_SystemInfo_processorType"] = UnityEngine.SystemInfo.processorType.ToString();
            m_info["UnityEngine_SystemInfo_supportedRenderTargetCount"] = UnityEngine.SystemInfo.supportedRenderTargetCount.ToString();
            m_info["UnityEngine_SystemInfo_supports3DTextures"] = UnityEngine.SystemInfo.supports3DTextures.ToString();
            m_info["UnityEngine_SystemInfo_supportsAccelerometer"] = UnityEngine.SystemInfo.supportsAccelerometer.ToString();
            m_info["UnityEngine_SystemInfo_supportsComputeShaders"] = UnityEngine.SystemInfo.supportsComputeShaders.ToString();
            m_info["UnityEngine_SystemInfo_supportsGyroscope"] = UnityEngine.SystemInfo.supportsGyroscope.ToString();
            m_info["UnityEngine_SystemInfo_supportsImageEffects"] = UnityEngine.SystemInfo.supportsImageEffects.ToString();
            m_info["UnityEngine_SystemInfo_supportsInstancing"] = UnityEngine.SystemInfo.supportsInstancing.ToString();
            m_info["UnityEngine_SystemInfo_supportsLocationService"] = UnityEngine.SystemInfo.supportsLocationService.ToString();
            m_info["UnityEngine_SystemInfo_supportsRawShadowDepthSampling"] = UnityEngine.SystemInfo.supportsRawShadowDepthSampling.ToString();
            m_info["UnityEngine_SystemInfo_supportsRenderToCubemap"] = UnityEngine.SystemInfo.supportsRenderToCubemap.ToString();
            m_info["UnityEngine_SystemInfo_supportsShadows"] = UnityEngine.SystemInfo.supportsShadows.ToString();
            m_info["UnityEngine_SystemInfo_supportsSparseTextures"] = UnityEngine.SystemInfo.supportsSparseTextures.ToString();
#if !UNITY_5_5_OR_NEWER
            m_info["UnityEngine_SystemInfo_supportsStencil"] = UnityEngine.SystemInfo.supportsStencil.ToString();
#endif
            m_info["UnityEngine_SystemInfo_supportsVibration"] = UnityEngine.SystemInfo.supportsVibration.ToString();
            m_info["UnityEngine_SystemInfo_systemMemorySize"] = UnityEngine.SystemInfo.systemMemorySize.ToString();
        }
    }

    public class BoundsMeshBuilder
    {
        public Mesh mesh;

        private bool requireUVS = false;
        private bool requireNormals = false;
        private bool reverseTriangles = false;
        private readonly Vector3[] vertices = new Vector3[8];
        private readonly int[] triangles = new int[36];
        private readonly Vector2[] uvs = new Vector2[8];
        private readonly Vector3[] normals = new Vector3[8];

        public BoundsMeshBuilder(bool shouldGenerateUVS, bool shouldGenerateNormals, bool shouldReverseTriangles)
        {
            requireUVS = shouldGenerateUVS;
            requireNormals = shouldGenerateNormals;
            reverseTriangles = shouldReverseTriangles;
        }

        public void UpdateFromBounds(Bounds b)
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Bounds Mesh";
                mesh.MarkDynamic();
            }

            /*    4_ _ _5
                 /|    /|
               0/_|_ 1/ |      
               | 6|_ _|_|7
               | /    | / 
               |/_ _ _|/ 
               2      3    */

            Vector3 c = b.center;
            Vector3 e = b.extents;

            Vector3 ftl = new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);  // Front Top Left
            Vector3 ftr = new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);  // Front Top Right
            Vector3 fbl = new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);  // Front Bottom Left
            Vector3 fbr = new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);  // Front Bottom Right
            Vector3 btl = new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);  // Back Top Left
            Vector3 btr = new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);  // Back Top Right
            Vector3 bbl = new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);  // Back Bottom Left
            Vector3 bbr = new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);  // Back Bottom Right

            vertices[0] = ftl; vertices[1] = ftr; vertices[2] = fbl; vertices[3] = fbr;    //  4   5   6   7
            vertices[4] = btl; vertices[5] = btr; vertices[6] = bbl; vertices[7] = bbr;    //  0   1   2   3

            triangles[0] = 0; triangles[1] = 1; triangles[2] = 2; triangles[3] = 2; triangles[4] = 1; triangles[5] = 3; // Front
            triangles[6] = 5; triangles[7] = 4; triangles[8] = 7; triangles[9] = 7; triangles[10] = 4; triangles[11] = 6; // Back
            triangles[12] = 4; triangles[13] = 0; triangles[14] = 6; triangles[15] = 6; triangles[16] = 0; triangles[17] = 2; // Left
            triangles[18] = 1; triangles[19] = 5; triangles[20] = 3; triangles[21] = 3; triangles[22] = 5; triangles[23] = 7; // Right
            triangles[24] = 4; triangles[25] = 5; triangles[26] = 0; triangles[27] = 0; triangles[28] = 5; triangles[29] = 1; // Top
            triangles[30] = 2; triangles[31] = 3; triangles[32] = 6; triangles[33] = 3; triangles[34] = 7; triangles[35] = 6; // Bottom

            if (reverseTriangles)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int temp = triangles[i + 0];
                    triangles[i + 0] = triangles[i + 1];
                    triangles[i + 1] = temp;
                }
            }

            if (requireUVS)
            {
                Vector2 _00 = new Vector2(0f, 0f);
                Vector2 _10 = new Vector2(1f, 0f);
                Vector2 _01 = new Vector2(0f, 1f);
                Vector2 _11 = new Vector2(1f, 1f);
                uvs[0] = _01; uvs[1] = _11; uvs[2] = _00; uvs[3] = _10; // Front
                uvs[4] = _01; uvs[5] = _11; uvs[6] = _00; uvs[7] = _10; // Back
            }

            if (requireNormals)
            {
                normals[0] = new Vector3(0, 0, -1); normals[1] = new Vector3(0, 0, -1); normals[2] = new Vector3(0, 0, -1); normals[3] = new Vector3(0, 0, -1); // Front
                normals[4] = new Vector3(0, 0, 1); normals[5] = new Vector3(0, 0, 1); normals[6] = new Vector3(0, 0, 1); normals[7] = new Vector3(0, 0, 1); // Back
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            if (requireUVS) mesh.uv = uvs;
            if (requireNormals) mesh.normals = normals;

            mesh.RecalculateBounds();

            // Call UploadMeshData to immediately send the modified data to the graphics API, to avoid a possible problem later. 
            // Passing true in a markNoLogerReadable argument makes Mesh data not be readable from the script anymore, and frees up system memory copy of the data.
            mesh.UploadMeshData(false);
        }
    }

    public static class UniqueIdRegistry
    {
        private static Dictionary<WeakReference, int> registry = new Dictionary<WeakReference, int>();

        public static int Register(object target)
        {
            Clean();

            int value = GenerateValue();

            while (registry.Any(x => x.Value == value))
                value = GenerateValue();

            registry.Add(new WeakReference(target), value);

            return value;
        }

        public static void Deregister(object target)
        {
            if (registry.Any(x => x.Key.Target == target))
            {
                KeyValuePair<WeakReference, int> wr = registry.First(x => x.Key.Target == target);
                registry.Remove(wr.Key);
            }
            else
            {
                Debug.Log("Object was not registered");
            }
        }

        private static int GenerateValue()
        {
            return UnityEngine.Random.Range(1, int.MaxValue);
        }

        private static void Clean()
        {
            registry = registry.Where(x => x.Key.IsAlive)
                               .ToDictionary(x => x.Key, x => x.Value);
        }
    }

    public static class FrustumBuilder
    {
        public static void GenerateVertex(float near, float far, float fovDeg, float aspect, out Vector3[] nearPlaneVertices, out Vector3[] farPlaneVertices)
        {
            float halfFOV = fovDeg * Mathf.Deg2Rad * 0.5f;
            float Hnear = Mathf.Tan(halfFOV) * near * 2;
            float Wnear = Hnear * aspect;
            float Hfar = Mathf.Tan(halfFOV) * far * 2;
            float Wfar = Hfar * aspect;

            nearPlaneVertices = new Vector3[4];
            farPlaneVertices = new Vector3[4];

            nearPlaneVertices[0] = new Vector3(-Wnear * 0.5f, Hnear * 0.5f, near);
            nearPlaneVertices[1] = new Vector3(Wnear * 0.5f, Hnear * 0.5f, near);
            nearPlaneVertices[2] = new Vector3(-Wnear * 0.5f, -Hnear * 0.5f, near);
            nearPlaneVertices[3] = new Vector3(Wnear * 0.5f, -Hnear * 0.5f, near);

            farPlaneVertices[0] = new Vector3(-Wfar * 0.5f, Hfar * 0.5f, far);
            farPlaneVertices[1] = new Vector3(Wfar * 0.5f, Hfar * 0.5f, far);
            farPlaneVertices[2] = new Vector3(-Wfar * 0.5f, -Hfar * 0.5f, far);
            farPlaneVertices[3] = new Vector3(Wfar * 0.5f, -Hfar * 0.5f, far);
        }

        public static Mesh Create(Vector3[] nearPlane, Vector3[] farPlane)
        {
            Vector3[] vertices = new Vector3[8];

            System.Array.Copy(nearPlane, vertices, nearPlane.Length);
            System.Array.Copy(farPlane, 0, vertices, 4, farPlane.Length);

            int[] indices = new int[]
            {
                0, 1, 2, 2, 1, 3, // near plane
                5, 4, 7, 7, 4, 6, // far plane

                4, 5, 0, 0, 5, 1, // ceiling
                2, 3, 6, 6, 3, 7, // floor

                6, 4, 2, 2, 4, 0, // left wall
                5, 7, 1, 1, 7, 3// right wall
            };

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = indices;
            return mesh;
        }
    }

    public static class CompositeBufferUtils
    {
        public static Mesh GenerateQuad()
        {
            Vector3[] vertices = new Vector3[4] {
                new Vector3( 1.0f, 1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f),
                new Vector3(-1.0f,-1.0f, 0.0f),
                new Vector3( 1.0f,-1.0f, 0.0f),
            };

            int[] indices;
            if (Helper.GetRenderingAPI().StartsWith("OpenGL"))
            {
                indices = new int[6] { 2, 1, 0, 0, 3, 2 };
            }
            else
            {
                indices = new int[6] { 0, 1, 2, 2, 3, 0 };
            }

            Mesh r = new Mesh();
            r.vertices = vertices;
            r.triangles = indices;
            return r;
        }

        static void GetPerspectiveCameraSpaceClipPlanePositions(Camera cam, float planeDepth, ref Vector3[] positions)
        {
            float np = planeDepth;
            float fov_V = cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = cam.aspect;

            float d_V = Mathf.Tan(fov_V * 0.5f) * np;
            float d_H = d_V * aspect;
            
            positions[0] = new Vector3(d_H, d_V, np);
            positions[1] = new Vector3(-d_H, d_V, np);
            positions[2] = new Vector3(-d_H, -d_V, np);
            positions[3] = new Vector3(d_H, -d_V, np);
        }

        public static Mesh GenerateQuadForScreenspaceShadowMask(Camera cam)
        {
            Vector3[] vertices = new Vector3[4] {
                new Vector3( 1.0f, 1.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f),
                new Vector3(0.0f,0.0f, 0.0f),
                new Vector3( 1.0f,0.0f, 0.0f),
            };

            Vector2[] uvs = new Vector2[4] {
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f)
            };

            // TODO: add stereo instancing support by adding two set of rays, see HvrScreenspaceShadowMask.shader->struct appdata
            Vector3[] rays = new Vector3[4];
            GetPerspectiveCameraSpaceClipPlanePositions(cam, cam.farClipPlane, ref rays);

            // DO NOT NORMALIZE AS THE RAY WILL BE MULTIPLIED BY DEPTH FROM [0, 1] TO 
            // RECONSTRUCT WORLD POSITION
            int[] indices;
            if (Helper.GetRenderingAPI().StartsWith("OpenGL"))
            {
                indices = new int[6] { 2, 1, 0, 0, 3, 2 };
            }
            else
            {
                indices = new int[6] { 0, 1, 2, 2, 3, 0 };
            }

            Mesh r = new Mesh();
            r.SetVertices(new List<Vector3>(vertices));
            r.SetUVs(0, new List<Vector2>(uvs));
            r.SetUVs(1, new List<Vector3>(rays));
            // need to set indices at last to include texcoords
            r.SetTriangles(indices, 0);

            return r;
        }

        public static Mesh GenerateDetailedQuad()
        {
            const int div_x = 325;
            const int div_y = 200;

            var cell = new Vector2(2.0f / div_x, 2.0f / div_y);
            var vertices = new Vector3[65000];
            var indices = new int[(div_x - 1) * (div_y - 1) * 6];
            for (int iy = 0; iy < div_y; ++iy)
            {
                for (int ix = 0; ix < div_x; ++ix)
                {
                    int i = div_x * iy + ix;
                    vertices[i] = new Vector3(cell.x * ix - 1.0f, cell.y * iy - 1.0f, 0.0f);
                }
            }
            for (int iy = 0; iy < div_y - 1; ++iy)
            {
                for (int ix = 0; ix < div_x - 1; ++ix)
                {
                    int i = ((div_x - 1) * iy + ix) * 6;
                    indices[i + 0] = (div_x * (iy + 1)) + (ix + 1);
                    indices[i + 1] = (div_x * (iy + 0)) + (ix + 1);
                    indices[i + 2] = (div_x * (iy + 0)) + (ix + 0);

                    indices[i + 3] = (div_x * (iy + 0)) + (ix + 0);
                    indices[i + 4] = (div_x * (iy + 1)) + (ix + 0);
                    indices[i + 5] = (div_x * (iy + 1)) + (ix + 1);
                }
            }

            Mesh r = new Mesh();
            r.vertices = vertices;
            r.triangles = indices;
            return r;
        }
    }

    public struct KeyValue
    {
        public string key;
        public float val;
    }

    public class Statistic
    {
        public string name;
        float m_current = 0.0f;
        float m_avg = 0.0f;
        float m_min = float.MaxValue;
        float m_max = 0.0f;

        const int MAX_SAMPLES = 60;

        int idx = 0;
        float[] buffer = new float[MAX_SAMPLES];
        int bufferSize = 0;

        public void Accumulate(float metric)
        {
            m_current = metric;

            buffer[idx] = metric;

            if (idx == MAX_SAMPLES - 1)
                idx = 0;
            else
                idx++;

            if (bufferSize != MAX_SAMPLES)
                bufferSize++;

            float total = 0;
            m_min = float.MaxValue;
            m_max = float.MinValue;
            for (int i = 0; i < bufferSize; i++)
            {
                float s = buffer[i];
                total += s;
                m_min = Math.Min(s, m_min);
                m_max = Math.Max(s, m_max);
            }
            m_avg = total / bufferSize;
        }

        public void Reset()
        {
            m_current = 0.0f;
            m_avg = 0.0f;
            m_min = float.MaxValue;
            m_max = 0.0f;

            idx = 0;
            buffer = new float[MAX_SAMPLES];
            bufferSize = 0;
        }

        public float Current()
        {
            return m_current;
        }

        public float Avg()
        {
            return m_avg;
        }

        public float Min()
        {
            return m_min;
        }

        public float Max()
        {
            return m_max;
        }
    }

    public class CullingHelper
    {
        // Helper function for checking if a boundingSpheres is visible to a camera

        BoundingSphere[] boundingSpheres;

        CullingGroup cullingGroup;

        private bool isVisible = true;

        public CullingHelper()
        {
            boundingSpheres = new BoundingSphere[1];

            cullingGroup = new CullingGroup();
            cullingGroup.SetBoundingSpheres(boundingSpheres);
            cullingGroup.SetBoundingSphereCount(1);
        }

        public void Dispose()
        {
            if (cullingGroup != null)
            {
                cullingGroup.Dispose();
                cullingGroup = null;
            }
        }

        public bool IsVisible()
        {
            return isVisible;
        }

        public void Update(Transform transform, Bounds bounds, float radiusMultiplier, Camera cam)
        {
#if UNITY_EDITOR
            // If the editor is compilng, block any updating
            // This is neccessary as the 
            if (EditorApplication.isCompiling)
                return;
#endif

            if (cullingGroup == null || boundingSpheres == null)
                return;

            cullingGroup.targetCamera = cam;

            Bounds b = bounds;

            // True if the bounds are not valid
            if (b.size == Vector3.zero)
                b.size = new Vector3(100f, 100f, 100f);

            // Update Visibility Helper
            Vector3 center = transform.localToWorldMatrix.MultiplyPoint(b.center);

            Vector3 size = b.size;
            size.Scale(transform.lossyScale);

            b.center = center;
            b.size = size;

            float radius = Vector3.Distance(b.center, b.max) * radiusMultiplier;

            boundingSpheres[0].position = center;
            boundingSpheres[0].radius = radius;

            isVisible = cullingGroup.IsVisible(0);
        }

        public Vector3 GetPosition()
        {
            return boundingSpheres[0].position;
        }

        public float GetRadius()
        {
            return boundingSpheres[0].radius;
        }
    }

    public class AABB
    {
        Vector3 m_Min;
        Vector3 m_Max;
        bool m_VertexSynced;
        Vector3[] m_CachedVert;
        int[] m_idx = new int[]
        {
            0, 1, 2, 2, 1, 3, // front 
            4, 5, 6, 6, 5, 7, // back
            5, 0, 7, 7, 0, 2, // left
            1, 4, 3, 3, 4, 6, // right
            5, 4, 0, 0, 4, 1, // top
            2, 3, 7, 7, 3, 6  // bottom
        };

        public Vector3 min
        {
            get
            {
                return m_Min;
            }

            set
            {
                m_Min = value;
                m_VertexSynced = false;
            }
        }
        public Vector3 max
        {
            get
            {
                return m_Max;
            }

            set
            {
                m_Max = value;
                m_VertexSynced = false;
            }
        }

        public AABB(Vector3 min, Vector3 max)
        {
            m_Min = min;
            m_Max = max;
        }

        public static AABB Create(Vector3[] vertices)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Vector3 v in vertices)
            {
                if (v.x < min.x)
                {
                    min.x = v.x;
                }
                if (v.y < min.y)
                {
                    min.y = v.y;
                }

                if (v.z < min.z)
                {
                    min.z = v.z;
                }

                if (v.x > max.x)
                {
                    max.x = v.x;
                }
                if (v.y > max.y)
                {
                    max.y = v.y;
                }

                if (v.z > max.z)
                {
                    max.z = v.z;
                }
            }

            return new AABB(min, max);
        }

        public int[] Indices
        {
            get
            {
                return m_idx;
            }
        }

        public void CalcVertex(ref Vector3[] vertices)
        {
            if (m_CachedVert == null || !m_VertexSynced)
            {
                m_CachedVert = new Vector3[8];

                // front
                m_CachedVert[0] = new Vector3(m_Min.x, m_Max.y, m_Max.z);
                m_CachedVert[1] = new Vector3(m_Max.x, m_Max.y, m_Max.z);
                m_CachedVert[2] = new Vector3(m_Min.x, m_Min.y, m_Max.z);
                m_CachedVert[3] = new Vector3(m_Max.x, m_Min.y, m_Max.z);

                // back
                m_CachedVert[4] = new Vector3(m_Max.x, m_Max.y, m_Min.z);
                m_CachedVert[5] = new Vector3(m_Min.x, m_Max.y, m_Min.z);
                m_CachedVert[6] = new Vector3(m_Max.x, m_Min.y, m_Min.z);
                m_CachedVert[7] = new Vector3(m_Min.x, m_Min.y, m_Min.z);

                m_VertexSynced = true;
            }

            // copy out
            for (int i = 0; i < m_CachedVert.Length; ++i)
            {
                vertices[i] = m_CachedVert[i];
            }
        }

        public Bounds ToBounds()
        {
            Bounds bounds = new Bounds();
            bounds.SetMinMax(m_Min, m_Max);
            return bounds;
        }

    }

    public static class BoundsBuilder
    {
        public static Bounds GetBoundsForCamera(Camera cam)
        {
            Vector3[] nearPlaneVertices;
            Vector3[] farPlaneVertices;
            Vector3[] totalVertices = new Vector3[8];

            FrustumBuilder.GenerateVertex(cam.nearClipPlane, cam.farClipPlane,
                            cam.fieldOfView, cam.aspect, out nearPlaneVertices, out farPlaneVertices);

            System.Array.Copy(nearPlaneVertices, totalVertices, nearPlaneVertices.Length);
            System.Array.Copy(farPlaneVertices, 0, totalVertices, 4, farPlaneVertices.Length);

            // transform frustum vertices into world space
            for (int i = 0; i < totalVertices.Length; ++i)
            {
                totalVertices[i] = cam.transform.localToWorldMatrix.MultiplyPoint(totalVertices[i]);
            }

            AABB aabb = AABB.Create(totalVertices);
            return aabb.ToBounds();
        }

        public static BoundingSphere GetBoundingSphereForCamera(Camera cam)
        {
            return GetBoundingSphereForCameraCascade(cam, 1.0f, QualitySettings.shadowDistance);
        }

        public static BoundingSphere GetBoundingSphereForCameraCascade(Camera cam, float percent, float shadowDistanceLimit)
        {
            // in camera space
            BoundingSphere sphere = new BoundingSphere();

            float far = Mathf.Min(cam.farClipPlane, shadowDistanceLimit) * percent;
            float near = cam.nearClipPlane;

            if (!cam.orthographic)
            {
                float theta_v = cam.fieldOfView * Mathf.Deg2Rad; // vertical theta
                float diagonal = Mathf.Sqrt(cam.pixelWidth * cam.pixelWidth + cam.pixelHeight * cam.pixelHeight);
                float theta = 2.0f * Mathf.Atan((diagonal / (float)cam.pixelHeight) * Mathf.Tan(theta_v * 0.5f)); // calculate the diagonal theta, which is used in the bounding sphere calculation

                float R = far / (2.0f * Mathf.Cos(theta * 0.5f) * Mathf.Cos(theta * 0.5f));
                sphere.radius = R;
                Vector3 center = new Vector3(0, 0, R - near);
                sphere.position = cam.transform.localToWorldMatrix.MultiplyPoint(center); // world center position
            }
            else
            {
                float dV = cam.orthographicSize;
                float dH = dV * cam.aspect;
                Vector3 farCorner = new Vector3(dH, dV, far);
                Vector3 center = new Vector3(0, 0, (near + far) * 0.5f);

                sphere.radius = (farCorner - center).magnitude;
                sphere.position = cam.transform.localToWorldMatrix.MultiplyPoint(center);
            }

            return sphere;
        }
    }

    public static class CameraHelper
    {
        public static Camera GetMainCamera()
        {
            Camera cam = Camera.main; // or Camera.current?
            // basically check if "SteamVR.active" is true without requiring the SteamVR imported
            Type typeSteamVR = Type.GetType("SteamVR");
            if (typeSteamVR != null)
            {
                System.Reflection.PropertyInfo property = typeSteamVR.GetProperty("active");
                if (property != null)
                {
                    bool isActive = (bool)property.GetValue(null, null);
                    if (isActive)
                    {
                        Type typeSteamVRCamera = Type.GetType("SteamVR_Camera");
                        if (typeSteamVRCamera != null)
                        {
                            // SteamVR conceals the real Camera so find it out, note GetComponentInChild() won't do the work
                            for (int i = 0; i < cam.transform.childCount; ++i)
                            {
                                Transform childTransform = cam.transform.GetChild(i);
                                if (childTransform.GetComponent(typeSteamVRCamera) && childTransform.GetComponent<Camera>())
                                {
                                    cam = childTransform.GetComponent<Camera>();
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return cam;
        }

        public static bool IsLayerVisibleInCullingMask(int layer, int cullingMask)
        {
            return (((1 << layer) & cullingMask) != 0);
        }
    }
}
