//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController_Simple : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR background).
        /// </summary>
        public Camera FirstPersonCamera;

        public GameObject RootObject;

        /// <summary>
        /// A prefab for tracking and visualizing detected planes.
        /// </summary>
        public GameObject DetectedPlanePrefab;

        /// <summary>
        /// A model to place when a raycast from a user touch hits a plane.
        /// </summary>
        public GameObject ArPrefab;

        public GameObject UI_InfoBar;
        public GameObject UI_Searching;
        public GameObject UI_TapToPlace;

        /// <summary>
        /// The rotation in degrees need to apply to model when the prefab is placed
        /// </summary>
        private const float k_ModelRotation = 180.0f;

        /// <summary>
        /// A list to hold all planes ARCore is tracking in the current frame. This object is used across
        /// the application to avoid per-frame allocations.
        /// </summary>
        private List<DetectedPlane> m_AllPlanes = new List<DetectedPlane>();

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error, otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;

        private GameObject m_arObject;

        enum TouchState
        {
            idle,
            waitingForUp
        }

        private TouchState touchState = TouchState.idle;

        private float startTime;

        private float groundPlaneAlpha;
        public float groundPlaneAlphaFade;
        public AnimationCurve groundPlaneAnimCurve;

        public Material materialGroundPlane;

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        public void Update()
        {
            _UpdateApplicationLifecycle();

            // Hide snackbar when currently tracking at least one plane.
            Session.GetTrackables<DetectedPlane>(m_AllPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < m_AllPlanes.Count; i++)
            {
                if (m_AllPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;
                    break;
                }
            }

            UI_Searching.SetActive(showSearchingUI);

            bool showTapUI = false;

            if (!showSearchingUI &&
                m_arObject == null)
            {
                showTapUI = true;
            }

            UI_TapToPlace.SetActive(showTapUI);

            bool showInfoBar = true;

            if (!showSearchingUI &&
                !showTapUI)
            {
                showInfoBar = false;
            }

            UI_InfoBar.SetActive(showInfoBar);

            // If the screen has been touched, then show the ground
            if (Input.touchCount > 0)
            {
                groundPlaneAlpha = 1f;
            }
            else
            {
                if (m_arObject != null)
                {
                    // If there hasn't been a screen interaction, start fading the ground plane
                    groundPlaneAlpha -= Time.deltaTime / groundPlaneAlphaFade;
                }
                else
                {
                    groundPlaneAlpha = 1f;
                }
            }

            groundPlaneAlpha = Mathf.Clamp01(groundPlaneAlpha);
            float smoothAlpha = groundPlaneAnimCurve.Evaluate(groundPlaneAlpha);
            Shader.SetGlobalFloat("_ARCORE_PLANE_FADEAMOUNT", smoothAlpha);

            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);

                if (t.phase == TouchPhase.Began)
                {
                    touchState = TouchState.waitingForUp;
                    startTime = Time.time;
                }

                if (touchState == TouchState.waitingForUp)
                {
                    if (Time.time - startTime > 0.3f)
                    {
                        touchState = TouchState.idle;
                    }
                    else
                    {
                        // Finger is raised off the screen
                        if (Input.GetTouch(0).phase == TouchPhase.Ended)
                        {
                            touchState = TouchState.idle;
                            DropCheck();
                        }
                    }
                }
            }
            else
            {
                touchState = TouchState.idle;
            }
        }

        private void DropCheck()
        {
            Touch touch = Input.GetTouch(0);

            // Raycast against the location the player touched to search for planes.
            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }
                else
                {
                    var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                    RootObject.transform.parent = anchor.transform;
                    RootObject.transform.position = anchor.transform.position;
                    RootObject.transform.rotation = anchor.transform.rotation;

                    RootObject.transform.Rotate(0, k_ModelRotation, 0, Space.Self);

                    if (m_arObject != null)
                    {
                        Destroy(m_arObject);
                        m_arObject = null;
                    }

                    // Instantiate prefab at the hit pose.
                    m_arObject = Instantiate(ArPrefab);

                    m_arObject.transform.parent = RootObject.transform;
                    m_arObject.transform.localPosition = Vector3.zero;
                    m_arObject.transform.localEulerAngles = Vector3.zero;
                    m_arObject.transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
