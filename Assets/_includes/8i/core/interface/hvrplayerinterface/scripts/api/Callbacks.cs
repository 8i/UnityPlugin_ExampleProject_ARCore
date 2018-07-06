using AOT;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HVR.Interface
{
    public static class Callbacks
    {
        [MonoPInvokeCallback(typeof(Types.OnAssetInitialised))]
        public static void OnAssetInitialised(
                int error,
                IntPtr userData
            )
        {
            if (userData != IntPtr.Zero)
            {
                AssetInterface asset = Helper.GCHandleToObject<AssetInterface>((GCHandle)userData);

                if (asset != null)
                    asset.OnAssetInitialised(error, userData);
            }
        }

        [MonoPInvokeCallback(typeof(Types.OnAssetSelectRepresentation))]
        public static bool OnAssetSelectRepresentation(
                IntPtr adaptionSet,
                uint representationIndex,
                IntPtr representations,
                uint representationCount,
                IntPtr userData
            )
        {
            CommonTypes.HVRAdaptationSet _adaptionSet = Helper.PtrToStruct<CommonTypes.HVRAdaptationSet>(adaptionSet);

            CommonTypes.HVRRepresentation[] _representations = new CommonTypes.HVRRepresentation[(int)representationCount];

            int representationStructSize = Marshal.SizeOf(typeof(CommonTypes.HVRRepresentation));

            for (int i = 0; i < _representations.Length; ++i)
            {
                IntPtr dataPtr = new IntPtr(representations.ToInt64() + representationStructSize * i);
                _representations[i] = Helper.PtrToStruct<CommonTypes.HVRRepresentation>(dataPtr);
            }

            if (userData != IntPtr.Zero)
            {
                AssetInterface asset = Helper.GCHandleToObject<AssetInterface>((GCHandle)userData);

                if (asset != null)
                    return asset.OnAssetSelectRepresentation(_adaptionSet, representationIndex, _representations, representationCount, userData);
            }

            // at least must return true for one of the representationIndex within [0, representationCount)
            if (representationIndex == representationCount - 1)
                return true;

            return false;
        }

        [MonoPInvokeCallback(typeof(Types.OnAssetRepresentationDataReceived))]
        public static void OnAssetRepresentationDataReceived(
                string mimeType,
                string codec,
                float startTime,
                IntPtr data,
                uint dataSize,
                IntPtr userData
            )
        {
            if (userData != IntPtr.Zero)
            {
                AssetInterface asset = Helper.GCHandleToObject<AssetInterface>((GCHandle)userData);

                if (asset != null)
                    asset.OnAssetRepresentationDataReceived(mimeType, codec, startTime, data, dataSize, userData);
            }
        }
    }
}