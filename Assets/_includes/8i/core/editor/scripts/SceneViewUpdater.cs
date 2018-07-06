using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace HVR.Editor
{
    [InitializeOnLoad]
    public class SceneViewUpdater
    {
        static GraphicsDeviceType previousGraphicsDeviceType;

        static bool wasPlaying = false;

        static SceneViewUpdater()
        {
            previousGraphicsDeviceType = SystemInfo.graphicsDeviceType;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;

#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged -= CheckPlayModeState;
            EditorApplication.playModeStateChanged += CheckPlayModeState;
#else
            EditorApplication.playmodeStateChanged -= CheckPlayModeState;
            EditorApplication.playmodeStateChanged += CheckPlayModeState;
#endif

            // Always destroy scene cameras between scene loads while in the editor
            // This ensures that there are no references to HvrActors being stored between scenes
            EditorCallbacks.onLoadScene -= DestroySceneCameras;
            EditorCallbacks.onLoadScene += DestroySceneCameras;
        }

        static int sLastVoxelCount = 0;

        private static void Update()
        {
            // In the case that the graphics api changes while the editor is running
            // destroy any hvrrender components that are attached to Editor cameras
            if (previousGraphicsDeviceType != SystemInfo.graphicsDeviceType)
            {
                previousGraphicsDeviceType = SystemInfo.graphicsDeviceType;

                DestroySceneCameras();
            }

            if (!EditorApplication.isPlaying)
            {
                Camera[] sceneCameras = InternalEditorUtility.GetSceneViewCameras();

                foreach (Camera camera in sceneCameras)
                {
                    if (camera.gameObject.GetComponent(Uniforms.componentNames.hvrRender))
                    {
                        HvrRender render = (HvrRender)camera.gameObject.GetComponent(Uniforms.componentNames.hvrRender);

                        if (!HvrScene.Contains(render))
                            HvrScene.Add(render);
                    }
                }

                List<HvrActor> actors = new List<HvrActor>();
                HvrScene.GetObjects(actors);

                // If the asset's CurrentTime is different to the ActualTime, then a frame
                // has probably begun decoding or has finished decoding.
                // In order for this the data to be pushed to the renderer, we must force a render to occur
                bool assetTimeChanged = false;

                // Because of the async nature of the decoding and rendering we must wait until the voxel count
                // has changed before rerendering the views
                int currentVoxelCount = 0;

                foreach (HvrActor actor in actors)
                {
                    if (actor != null &&
                        actor.assetInterface != null)
                    {
                        if (actor.assetInterface.GetCurrentTime() != actor.assetInterface.GetActualTime())
                            assetTimeChanged = true;

                        currentVoxelCount += actor.assetInterface.GetVoxelCount();
                    }
                }

                if (assetTimeChanged ||
                    sLastVoxelCount != currentVoxelCount)
                {
                    sLastVoxelCount = currentVoxelCount;

                    // Ensures that the HvrActor's mesh is updated
                    List<HvrActor> hvrActors = new List<HvrActor>();
                    HvrScene.GetObjects(hvrActors);

                    foreach (HvrActor actor in hvrActors)
                        actor.EditorUpdate();

                    // Force HvrScene to allow the PrepareRender stage to occur
                    HvrScene.lastPreparedFrame = -1;

                    // Force all HvrRender cameras to render
                    // Do this last after updating the scene
                    List<HvrRender> hvrRenders = new List<HvrRender>();
                    HvrScene.GetObjects(hvrRenders);

                    foreach (HvrRender render in hvrRenders)
                        render.GetComponent<Camera>().Render();
                }
            }
        }

#if UNITY_2017_2_OR_NEWER
        public static void CheckPlayModeState(PlayModeStateChange stateChange)
        {
            CheckPlayModeState();
        }
#endif

        private static void CheckPlayModeState()
        {
            // Do not allow the HvrRender component from being destroyed while paused
            if (!EditorApplication.isPaused)
            {
                bool enteredEditMode = false;
                bool exitingEditMode = false;
                bool enteredPlayMode = false;
                bool exitingPlayMode = false;

                // Leaving 'Edit' Mode
                if (EditorApplication.isPlayingOrWillChangePlaymode &&
                    !EditorApplication.isPlaying)
                {
                    //Debug.Log("exitingEditMode");
                    exitingEditMode = true;
                }

                // Entering 'Play' Mode
                if (EditorApplication.isPlayingOrWillChangePlaymode &&
                    EditorApplication.isPlaying)
                {
                    //Debug.Log("enteredPlayMode");
                    enteredPlayMode = true;
                }

                // Exiting 'Play' Mode
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    wasPlaying == true &&
                    EditorApplication.isPlaying)
                {
                    //Debug.Log("exitingPlayMode");
                    exitingPlayMode = true;
                }

                // Entered 'Edit' Mode
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    wasPlaying != EditorApplication.isPlaying)
                {
                    //Debug.Log("enteredEditMode");
                    enteredEditMode = true;
                }

                // If any of the above states are true, then we should destroy any scene cameras that exist
                if (enteredEditMode || exitingEditMode ||
                    enteredPlayMode || exitingPlayMode)
                {
                    DestroySceneCameras();
                }
            }

            wasPlaying = EditorApplication.isPlaying;
        }

        private static void DestroySceneCameras()
        {
            List<HvrRender> hvrRenders = new List<HvrRender>();
            HvrScene.GetObjects(hvrRenders);

            foreach (HvrRender render in hvrRenders)
            {
                if (EditorHelper.IsSceneViewCamera(render.GetComponent<Camera>()) ||
                    EditorHelper.IsPreviewCamera(render.GetComponent<Camera>()))
                {
                    UnityEngine.Object.DestroyImmediate(render);
                }
            }
        }
    }
}
