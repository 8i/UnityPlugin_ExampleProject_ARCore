using HVR.Interface;
using UnityEngine;
using System.Collections;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace HVR
{
    [ExecuteInEditMode]
    [AddComponentMenu("8i/HvrActor")]
    public class HvrActor : MonoBehaviour
    {
        #region Public Properties

        public ActorInterface actorInterface { get { return m_actorInterface; } }

        public AssetInterface assetInterface { get { return m_assetInterface; } }

        public RenderMethodInterface renderMethodInterface { get { return m_renderMethodInterface; } }

        public string renderMethodType { get { return m_renderMethodType; } }

        public bool assetPlay;

        public bool assetLoop;

        public float assetSeekTime;

        /// <summary>       
        /// Used to store a GUID reference or a path which is used when creating a AssetInterface for
        /// this HvrActor
        /// How this value is used changes based on the DataMode.
        /// </summary>      
        public string data { get { return m_data; } }

        /// <summary>
        /// Modifies how the HvrActor data value is used when creating a AssetInterface
        /// <para>
        /// Reference
        /// <para>
        ///     [EDITOR]:
        ///         If this HvrActor's data value is a valid GUID which referes to an existing object 
        ///         within the project it will create an AssetInterface with the path to that object.
        ///         The object can be a file or a folder.
        ///         During the build process, the Scenes included in the EditorBuildSettings are
        ///         scanned to check if they contain a HvrActor with an valid data value which refers
        ///         exiting valid data.
        ///     [RUNTIME]:
        ///         If this HvrActor's data value refers to an existing file or folder within the
        ///         builds data folder, it will be used to create a new AssetInterface with a
        ///         path to that file or folder
        /// </para>
        /// </para>
        /// <para>
        /// Path
        /// <para>
        ///     [EDITOR + RUNTIME]:
        ///     Direct path to a file, folder or url
        ///     During the build process, any data at this path is not copied
        /// </para>
        /// </para>
        /// </summary>
        public enum eDataMode
        {
            reference,
            path
        }

        /// <summary>
        /// DataMode determines how the data value is used to locate data for this HvrActor's Asset
        /// </summary>
        public eDataMode dataMode { get { return m_dataMode; } }

        /// <summary>
        /// The material used for standard rendering
        /// </summary>
        public Material material
        {
            get
            {
                if (m_material == null)
                {
                    m_material = HvrHelper.CreateHvrStandardMaterial();
                }

                return m_material;
            }
            set
            {
                m_material = value;
            }
        }

        public Mesh renderMesh
        {
            get
            {
                return m_renderMesh;
            }
        }

        /// <summary>
        /// Should this actor be able to receive shadows?
        /// </summary>
        public bool receiveShadows
        {
            get
            {
                return m_receiveShadows && useLighting;
            }
            set
            {
                m_receiveShadows = value;
            }
        }

        /// <summary>
        /// Should this actor be able to cast shadows?
        /// </summary>
        public bool castShadows
        {
            get
            {
                return m_castShadows && useLighting;
            }
            set
            {
                m_castShadows = value;
            }
        }

        /// <summary>
        /// Should this actor receive standard Unity lighting?
        /// </summary>
        public bool useLighting
        {
            get
            {
                return m_useLighting;
            }
            set
            {
                m_useLighting = value;
            }
        }

        /// <summary>
        /// Whether actor should use Unity's occlusion culling system when determining if it should render
        /// </summary>
        public bool occlusionCullingEnabled = false;

        /// <summary>
        /// Occlusion culling creates a sphere based on actor bounds dimensions, this allows for a radius offset
        /// </summary>
        public float occlusionCullingMultipler = 1.0f;

        /// <summary>
        /// Scale factor to scale the transforms scale by
        /// </summary>
        public float actorScaleFactor = 0.01f;

        /// <summary>
        /// Should the renderer use the mesh based on the bounds of the actor, or a screenspace quad?
        /// This fixes an issue where ShaderSubroutines are used and the points are moved out of the asset's bounds
        /// There is an added rendering cost when using a quad cost due to the increased overdraw
        /// </summary>
        public bool useScreenSpaceQuad = false;

        #endregion

        #region Private Members

        [SerializeField]
        private string m_data;

        [SerializeField]
        private eDataMode m_dataMode = eDataMode.reference;

        private Bounds m_previousBounds;

        private ActorInterface m_actorInterface;

        private AssetInterface m_assetInterface;

        private RenderMethodInterface m_renderMethodInterface;

        [SerializeField]
        private string m_renderMethodType;

        private BoundsMeshBuilder m_boundsMeshBuilder = new BoundsMeshBuilder(false, false, true);

        private Mesh m_renderMesh;

        private bool m_forceUpdateSubroutines;

        private HvrActorShaderSubroutineStack m_subroutineStack = new HvrActorShaderSubroutineStack();

        [SerializeField]
        private Material m_material;

        [SerializeField]
        private bool m_useLighting = true;

        [SerializeField]
        private bool m_receiveShadows = true;

        [SerializeField]
        private bool m_castShadows = true;

#if UNITY_EDITOR
        [SerializeField]
        private int m_instanceID = 0;
#endif

        #endregion

        #region Monobehaviour Functions

        private void OnEnable()
        {
            Init();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Block this function from running if this object is a prefab
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;
#endif

            if (m_assetInterface != null)
            {
                m_assetInterface.Update(Helper.GetCurrentTime());

                if (m_assetInterface.assetSource == AssetInterface.AssetSource.VOD ||
                    m_assetInterface.assetSource == AssetInterface.AssetSource.RealTime)
                {
                    if (m_assetInterface.IsOffline())
                    {
                        HvrPlayerInterface.CheckConnection();
                    }
                }
            }

            bool subroutineStackUpdated = m_subroutineStack.Update(GetComponents<HvrActorShaderSubroutineBase>());

            if (m_forceUpdateSubroutines ||
                subroutineStackUpdated)
            {
                m_forceUpdateSubroutines = false;

                string[] shaders = m_subroutineStack.GetShaderArray();

                if (m_renderMethodInterface != null)
                {
                    m_renderMethodInterface.SetShaderSubroutinesArray(shaders);
                }
            }
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            // Block this function from running if this object is a prefab
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;
#endif

            UpdateTransform();

            if (!useScreenSpaceQuad)
            {
                if (m_assetInterface != null)
                {
                    Bounds b = m_assetInterface.GetBounds();

                    if (m_renderMesh == null ||
                        m_previousBounds.center != b.center ||
                        m_previousBounds.size != b.size)
                    {
                        m_previousBounds = b;

                        m_boundsMeshBuilder.UpdateFromBounds(b);
                        m_renderMesh = m_boundsMeshBuilder.mesh;
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnRenderObject()
        {
            // Get the current camera that is rendering
            Camera cam = Camera.current;

            // Ensures that a HvrRender component is added to the Unity Editor cameras
            // It seems that this is the only place where the PreviewCamera can be accessed
            if (EditorHelper.IsSceneViewCamera(cam) ||
                EditorHelper.IsPreviewCamera(cam))
            {
                if (!(HvrRender)cam.GetComponent(Uniforms.componentNames.hvrRender))
                {
                    cam.gameObject.AddComponent<HvrRender>();
                }
            }
        }

        // EDITOR ONLY MONOBEHAVIOR FUNCTION
        // This validates changes to this object's values
        // and allows us to handle Undo and Redo events
        private void OnValidate()
        {
            EditorValidate();
        }
#endif

        private void OnDisable()
        {
            Destruct();
        }

        private void OnDestroy()
        {
            Destruct();
        }

        private void OnApplicationQuit()
        {
            Destruct();
        }

        #endregion

        #region HvrActor Functions

        private void Init()
        {
#if UNITY_EDITOR
            // In the case that this component is a prefab,
            // don't allow interface objects to be created
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;

            // There are issues with releasing unmanaged memory while running in batch mode for OSX and iOS
            // TODO: Remove this check
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode)
                return;
#endif

            m_actorInterface = new ActorInterface();
            m_actorInterface.Create();

            if (m_renderMethodInterface == null)
            {
                if (string.IsNullOrEmpty(m_renderMethodType))
                    m_renderMethodType = HvrPlayerInterface.RenderMethod_GetDefaultMethodType();

                // Create this HvrActor's rendermethod
                // !! Always create the rendermethod before creating the asset as
                // !! there is a work around in SetRenderMethodInterface which requires
                // !! the asset to be recreated if the rendermethod changes
                CreateRenderMethod(m_renderMethodType);
            }

            if (m_assetInterface == null)
            {
                // Create the asset
                if (string.IsNullOrEmpty(data) == false)
                    CreateAsset(data, dataMode);
            }

            if (m_assetInterface != null)
            {
                m_assetInterface.Seek(assetSeekTime);

                m_assetInterface.SetLooping(assetLoop);

                // Only allow assets to be played or seek when the application is playing 
                if (Application.isPlaying)
                {
                    if (assetPlay)
                        m_assetInterface.Play();
                }
            }

#if UNITY_EDITOR
            // Handle case where HvrActor is duplicated within the Unity Editor
            // We need to ensure that the HvrActor has a unique material
            if (m_instanceID == 0)
            {
                m_instanceID = GetInstanceID();
            }
            else
            if (m_instanceID != GetInstanceID() && GetInstanceID() < 0)
            {
                m_instanceID = GetInstanceID();

                if (m_material != null)
                {
                    Material mat = new Material(m_material.shader);
                    mat.name = m_material.name;
                    mat.CopyPropertiesFromMaterial(m_material);
                    mat.shaderKeywords = m_material.shaderKeywords;
                    m_material = mat;
                }
            }
#endif

            m_renderMesh = new Mesh();

            m_subroutineStack = new HvrActorShaderSubroutineStack();

            HvrScene.Add(this);
        }

        private void Destruct()
        {
            if (m_actorInterface != null)
            {
                m_actorInterface.Delete();
                m_actorInterface = null;
            }

            if (m_assetInterface != null)
            {
                m_assetInterface.Delete();
                m_assetInterface = null;
            }

            if (m_renderMethodInterface != null)
            {
                m_renderMethodInterface.Delete();
                m_renderMethodInterface = null;
            }

            HvrScene.Remove(this);
        }

        public void CreateAsset(string data, eDataMode dataMode)
        {
            // Ensure any asset interfaces are deleted and not attached to this HvrActor's actor
            if (m_assetInterface != null)
            {
                m_assetInterface.Delete();
                m_assetInterface = null;
            }

            m_data = data;
            m_dataMode = dataMode;

            string path = GetDataPath(m_data, m_dataMode);

            if (!string.IsNullOrEmpty(path))
            {
                AssetInterface assetInterface = new AssetInterface();
                assetInterface.Create(path);
                SetAssetInterface(assetInterface);
            }
        }

        public void SetAssetInterface(AssetInterface asset)
        {
            // Assign the new asset
            if (asset == null)
            {
                m_assetInterface = null;

                m_actorInterface.SetAsset(HVR.Interface.Types.INVALID_HANDLE);
            }
            else
            {
                m_assetInterface = asset;

                m_actorInterface.SetAsset(m_assetInterface.handle);

                // Always update the transform after setting a new asset - Tom
                UpdateTransform();
            }

            // If the suborutinestack has any shaders, we need to set them again after the asset has changed
            if (m_subroutineStack.GetShaderArray().Length > 0)
                m_forceUpdateSubroutines = true;
        }

        public void CreateRenderMethod(string renderMethodType)
        {
            // Ensure any rendermethod interfaces are deleted and not attached to this HvrActor's actor
            if (m_renderMethodInterface != null)
            {
                m_renderMethodInterface.Delete();
                m_renderMethodInterface = null;
            }

            // It is required to recreate the asset if the rendermethod is changed
            // TODO Remove the need to do this
            if (m_assetInterface != null)
            {
                CreateAsset(data, dataMode);
            }

            RenderMethodInterface renderMethod = new RenderMethodInterface();
            renderMethod.Create(renderMethodType);
            SetRenderMethodInterface(renderMethod);
        }

        public void SetRenderMethodInterface(RenderMethodInterface renderMethod)
        {
            // Set the new rendermethod
            if (renderMethod == null)
            {
                m_renderMethodInterface = null;

                // Do not change the rendermethod type here as we may want to retain that property
                // for the next time we create a rendermethod

                m_actorInterface.SetRenderMethod(HVR.Interface.Types.INVALID_HANDLE);
            }
            else
            {
                m_renderMethodInterface = renderMethod;

                // Set the rendermethod here to ensure that the type matches the
                // rendermethod that was just assigned to this actor
                m_renderMethodType = renderMethod.type;

                m_actorInterface.SetRenderMethod(m_renderMethodInterface.handle);
            }

            // If the suborutinestack has any shaders, we need to set them again after the rendermethod has changed
            if (m_subroutineStack.GetShaderArray().Length > 0)
                m_forceUpdateSubroutines = true;
        }

        private static string GetDataPath(string data, eDataMode dataMode)
        {
            // Create a new asset
            // Always return a Empty string if the data is invalid
            string path = string.Empty;

            switch (dataMode)
            {
                case eDataMode.reference:
                    path = HvrHelper.GetDataPathFromGUID(data);
                    break;
                case eDataMode.path:
                    path = data;
                    break;
            }

            return path;
        }

        // Actor Interface
        //-------------------------------------------------------------------------

        private void UpdateTransform()
        {
            m_actorInterface.SetTransform(transform, actorScaleFactor);
        }

        public void SetSubroutineUniformInt(string uniformName, int value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformInt(uniformName, value);
        }

        public void SetSubroutineUniformFloat(string uniformName, float value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformFloat(uniformName, value);
        }

        public void SetSubroutineUniformVec2(string uniformName, Vector2 value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformVec2(uniformName, value);
        }

        public void SetSubroutineUniformVec3(string uniformName, Vector3 value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformVec3(uniformName, value);
        }

        public void SetSubroutineUniformVec4(string uniformName, Vector4 value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformVec4(uniformName, value);
        }

        // Sets the uniform using the top-left 3x3 submatrix of the provided 4x4 matrix.
        public void SetSubroutineUniformMat3x3(string uniformName, Matrix4x4 value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformMat3x3(uniformName, value);
        }

        public void SetSubroutineUniformMat4x4(string uniformName, Matrix4x4 value)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformMat4x4(uniformName, value);
        }

        public void SetSubroutineUniformTexture2D(string uniformName, Texture texture)
        {
            if (m_actorInterface != null)
                m_actorInterface.SetSubroutineUniformTexture2D(uniformName, texture);
        }

        // Checks
        //-------------------------------------------------------------------------

        #endregion

        #region Unity Editor Functions

#if UNITY_EDITOR

        public void EditorUpdate()
        {
            if (EditorApplication.isPlaying == false &&
                EditorApplication.isPaused == false)
            {
                Update();
                LateUpdate();
            }
        }

        private void EditorValidate()
        {
            // In the case that this component is a prefab,
            // don't allow interface objects to be created
            if (PrefabUtility.GetPrefabType(this) == PrefabType.Prefab)
                return;

            // Don't allow these checks to occur while the application is playing
            if (EditorApplication.isPlaying ||
                EditorApplication.isPaused)
                return;

            // If the actorInterface is null, then the code has likely just been
            // hot reloaded and we don't need to run the below checks
            if (m_actorInterface == null)
                return;

            // Check if the rendermethod type has changed
            if (m_renderMethodInterface != null &&
                m_renderMethodInterface.type != m_renderMethodType)
            {
                CreateRenderMethod(m_renderMethodType);
            }

            // Check if the data has changed and whether the asset needs to be recreated
            string path = GetDataPath(data, dataMode);

            if (string.IsNullOrEmpty(path))
            {
                if (m_assetInterface != null)
                {
                    m_assetInterface.Delete();
                    m_assetInterface = null;

                    SetAssetInterface(null);
                }
            }
            else
            {
                if (m_assetInterface == null)
                {
                    CreateAsset(data, dataMode);
                }
                else
                {
                    if (m_assetInterface.assetCreationInfo.assetPath != path)
                    {
                        CreateAsset(data, dataMode);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            // DrawBounds here in order to draw an invisible, cube around the actor
            // This allows the user to select the actor from the scene view
            DrawBounds(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawBounds(true);
            DrawDebugInfo();
        }

        private void DrawDebugInfo()
        {
            if (occlusionCullingEnabled)
            {
                Bounds b = new Bounds();

                if (m_assetInterface != null)
                {
                    b = m_assetInterface.GetBounds();
                }

                Vector3 center = transform.localToWorldMatrix.MultiplyPoint(b.center);

                Vector3 size = b.size;
                size.Scale(transform.lossyScale);
                b.size = size;

                Gizmos.color = new Color(0.0f, 0.8f, 0.0f, 0.8f);
                Gizmos.DrawWireSphere(center, Vector3.Distance(b.center, b.max) * occlusionCullingMultipler);
            }
        }

        private void DrawBounds(bool selected)
        {
            Matrix4x4 origMatrix = Gizmos.matrix;

            Bounds b = new Bounds();

            if (m_assetInterface != null)
            {
                b = m_assetInterface.GetBounds();
            }

            var col = new Color(0.0f, 0.7f, 1f, 1.0f);
            col.a = 0.0f;

            // Set Gizmos.matrix here in order for the transform to affect Gizmos.DrawCube below
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = col;
            Gizmos.DrawCube(b.center, b.size);

            if (selected)
            {
                col.a = 0.5f;
                Gizmos.color = col;
                Gizmos.DrawWireCube(b.center, b.size);
            }

            Gizmos.matrix = origMatrix;
        }

#endif

        #endregion
    }
}
