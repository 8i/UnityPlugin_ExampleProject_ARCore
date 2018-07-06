using System;
using UnityEngine;
using System.Runtime.InteropServices;

namespace HVR.Interface
{
    public class AssetInterface
    {
        public int handle { get { return m_handle; } }

        public Types.AssetCreationInfo assetCreationInfo { get { return m_assetCreationInfo; } }

        public enum AssetSource
        {
            Local,
            RealTime,
            VOD
        }

        public AssetSource assetSource { get { return m_assetType; } }

        private int m_handle = Types.INVALID_HANDLE;

        private Types.AssetCreationInfo m_assetCreationInfo;

        private AssetSource m_assetType;

        private bool m_isLooping;

        public delegate void OnAssetInitialisedHandler(
            int error,
            IntPtr userData
        );

        public delegate bool OnAssetSelectRepresentationHandler(
            CommonTypes.HVRAdaptationSet adaptionSet,
            uint representationIndex,
            CommonTypes.HVRRepresentation[] representations,
            uint representationCount,
            IntPtr userData
        );

        public delegate void OnAssetRepresentationDataReceivedHandler(
            string mimeType,
            string codec,
            float startTime,
            IntPtr data,
            uint dataSize,
            IntPtr userData
        );

        public OnAssetInitialisedHandler onAssetInitialized;
        public OnAssetSelectRepresentationHandler onAssetSelectRepresentation;
        public OnAssetRepresentationDataReceivedHandler onAssetRepresentationDataReceived;

        public delegate void OnPlayEvent();
        public OnPlayEvent onPlay;
        public delegate void OnSeekEvent(float time);
        public OnSeekEvent onSeek;
        public delegate void OnPauseEvent();
        public OnPauseEvent onPause;
        public delegate void OnStopEvent();
        public OnStopEvent onStop;

        public void Create(string fileFolder)
        {
            m_handle = Types.INVALID_HANDLE;

            m_assetCreationInfo = new Types.AssetCreationInfo();
            m_assetCreationInfo.assetPath = fileFolder;
            //m_assetCreationInfo.cacheDir = Application.persistentDataPath;
            m_assetCreationInfo.bufferTime = 2.0f;

            Create(m_assetCreationInfo);
        }

        public void Create(Types.AssetCreationInfo info)
        {
            m_handle = Types.INVALID_HANDLE;

            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            try
            {
                UnityInterface.Lock();

                if (HvrPlayerInterface.Initialise())
                {
                    m_assetCreationInfo = info;

                    m_assetCreationInfo.structSize = (uint)Marshal.SizeOf(typeof(Types.AssetCreationInfo));

                    GCHandle assetHandle = GCHandle.Alloc(this);
                    IntPtr assetPtr = (IntPtr)assetHandle;
                    m_assetCreationInfo.userData = assetPtr;

                    // https://answers.unity.com/questions/1229036/callbacks-from-c-to-c-are-not-working-in-540f3.html?sort=votes
                    // For both IL2CPP and Mono on an AOT platform (like iOS) it is not possible to marshal an instance method to a function pointer
                    // that can be called from native code. Only static methods in C# code be called from native code like this.
                    m_assetCreationInfo.onInitialized = Callbacks.OnAssetInitialised;
                    m_assetCreationInfo.onRepresentationDataRecieved = Callbacks.OnAssetRepresentationDataReceived;
                    m_assetCreationInfo.onSelectRepresentation = Callbacks.OnAssetSelectRepresentation;

                    m_handle = HvrPlayerInterfaceAPI.Asset_CreateFromInfo(ref m_assetCreationInfo);

                    if (m_assetCreationInfo.assetPath.StartsWith("tcp"))
                        m_assetType = AssetSource.RealTime;
                    else
                    if (m_assetCreationInfo.assetPath.EndsWith("8imanifest"))
                        m_assetType = AssetSource.VOD;
                    else
                        m_assetType = AssetSource.Local;

#if VERBOSE_LOGGING
                    Debug.Log("Create " + GetType().Name + " Handle:" + handle);
#endif

                    UnityInterface.SceneObjectsAdd(handle, GetType().Name + handle, GetType().FullName);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                UnityInterface.Unlock();
            }
        }

        public void Delete()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            try
            {
                UnityInterface.Lock();

#if VERBOSE_LOGGING
                Debug.Log("Delete " + GetType().Name + " Handle:" + handle);
#endif

                HvrPlayerInterfaceAPI.Asset_Delete(handle);
                HvrPlayerInterfaceAPI.Interface_Update();

                UnityInterface.SceneObjectsRemove(handle);

                m_handle = Types.INVALID_HANDLE;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                UnityInterface.Unlock();
            }
        }

        public bool IsValid()
        {
            if (handle == Types.INVALID_HANDLE)
                return false;

            return HvrPlayerInterfaceAPI.Asset_IsValid(handle);
        }

        public void Update(float absoluteTime)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Update(handle, absoluteTime);
        }

        public void LogMeta()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            //// TODO: There was an API change
            //int count = HvrPlayerInterfaceAPI.Asset_GetMetaCount(handle);

            //for (int i = 0; i < count; ++i)
            //{
            //    StringBuilder key = new StringBuilder(256);
            //    StringBuilder val = new StringBuilder(256);
            //    string log = i + " - ";
            //    if (HvrPlayerInterfaceAPI.Asset_GetMetaEntry(handle, i, key, val))
            //    {
            //        log += key.ToString() + " = " + val.ToString();
            //    }
            //    Debug.Log(log + "\n");
            //}
        }

        public void Play()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Play(handle);

            if (onPlay != null)
                onPlay();
        }

        public void Pause()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Pause(handle);

            if (onPause != null)
                onPause();
        }

        public void Seek(float time)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Seek(handle, time);
        }

        public void Stop()
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Pause(handle);
            HvrPlayerInterfaceAPI.Asset_Seek(handle, 0);

            if (onStop != null)
                onStop();
        }

        public void Step(int frames)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Asset_Step(handle, frames);

            if (onStop != null)
                onStop();
        }

        public bool IsLooping()
        {
            if (handle == Types.INVALID_HANDLE)
                return false;

            return m_isLooping;
        }

        public void SetLooping(bool looping)
        {
            if (handle == Types.INVALID_HANDLE)
                return;

            m_isLooping = looping;

            HvrPlayerInterfaceAPI.Asset_SetLooping(handle, looping);
        }

        public Bounds GetBounds()
        {
            if (handle == Types.INVALID_HANDLE)
                return new Bounds();

            var b = HvrPlayerInterfaceAPI.Asset_GetBounds(handle);
            b.center.x *= -1.0f;

            var center = new Vector3(b.center.x, b.center.y, b.center.z);
            var size = new Vector3(b.halfDims.x, b.halfDims.y, b.halfDims.z);
            size *= 2.0f;

            center = center * Helper.CENTIMETRES_TO_METRES;
            size = size * Helper.CENTIMETRES_TO_METRES;

            return new Bounds(center, size);
        }

        public int GetState()
        {
            if (handle == Types.INVALID_HANDLE)
                return 0;

            return HvrPlayerInterfaceAPI.Asset_GetState(handle);
        }

        public float GetCurrentTime()
        {
            if (handle == Types.INVALID_HANDLE)
                return 0;

            return HvrPlayerInterfaceAPI.Asset_GetCurrentTime(handle);
        }

        public float GetActualTime()
        {
            if (handle == Types.INVALID_HANDLE)
                return 0;

            return HvrPlayerInterfaceAPI.Asset_GetActualTime(handle);
        }

        public float GetDuration()
        {
            if (handle == Types.INVALID_HANDLE)
                return 0;

            return HvrPlayerInterfaceAPI.Asset_GetDuration(handle);
        }

        public int GetVoxelCount()
        {
            if (handle == Types.INVALID_HANDLE)
                return 0;

            return HvrPlayerInterfaceAPI.Asset_GetVoxelCount(handle);
        }

        // DEPRECATED
        public bool IsReadyToPlay()
        {
            return !IsInitializing();
        }

        public bool IsInitializing()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_INITIALISING) != 0;
        }

        public bool IsPlaying()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_PLAYING) != 0;
        }

        public bool IsSeeking()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_SEEKING) != 0;
        }

        public bool IsCaching()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_CACHING) != 0;
        }

        public bool IsOffline()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_OFFLINE) != 0;
        }

        public bool IsFullyCached()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_FULLY_CACHED) != 0;
        }

        public bool IsInvalid()
        {
            return (GetState() & HvrPlayerInterfaceAPI.ASSET_STATE_INVALID) != 0;
        }

        public void OnAssetInitialised(
            int error,
            IntPtr userData
        )
        {
            if (onAssetInitialized != null)
                onAssetInitialized(error, userData);

            if (error != 0)
                Debug.LogError("Asset initialisation error: " + error);
        }

        public bool OnAssetSelectRepresentation(
            CommonTypes.HVRAdaptationSet adaptionSet,
            uint representationIndex,
            CommonTypes.HVRRepresentation[] representations,
            uint representationCount,
            IntPtr userData
        )
        {
            if (onAssetSelectRepresentation != null)
                onAssetSelectRepresentation(adaptionSet, representationIndex, representations, representationCount, userData);

            float maxFrameRate = 0;

            for (int i = 0; i < representations.Length; ++i)
                maxFrameRate = Mathf.Max(maxFrameRate, representations[i].maxFPS);

            // Only allow representations of the same framerate to be selected
            // At the moment this will default to the highest available framerate
            // TODO Make this configurable
            if (representations[representationIndex].maxFPS == maxFrameRate)
                return true;

            return false;
        }

        public void OnAssetRepresentationDataReceived(
            string mimeType,
            string codec,
            float startTime,
            IntPtr data,
            uint dataSize,
            IntPtr userData
        )
        {
            if (onAssetRepresentationDataReceived != null)
                onAssetRepresentationDataReceived(mimeType, codec, startTime, data, dataSize, userData);
        }
    }
}
