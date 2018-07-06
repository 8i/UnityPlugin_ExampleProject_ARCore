using System;
using System.Runtime.InteropServices;

namespace HVR.Interface
{
    public static class Types
    {
        public const int INVALID_HANDLE = 0;

        // NOTE: InterfaceInitialiseInfo is declared as struct so defaultly passed as value
        // Need to be extremely careful when declaring the function has InterfaceInitialiseInfo as parameters
        // because they usually need 'ref' keyword to pass by reference
        [StructLayout(LayoutKind.Sequential)]
        public struct InterfaceInitialiseInfo
        {
            [MarshalAs(UnmanagedType.U4)] public uint structSize; 
            [MarshalAs(UnmanagedType.LPStr)] public string appId;
            [MarshalAs(UnmanagedType.LPStr)] public string appVersion;
            [MarshalAs(UnmanagedType.LPStr)] public string apiKey;
            [MarshalAs(UnmanagedType.LPStr)] public string extensionPath;
            [MarshalAs(UnmanagedType.I4)] public int threadPoolSize;
            public IntPtr logCallback;
            [MarshalAs(UnmanagedType.I4)] public int logLevel;
        }

        public delegate void OnAssetInitialised(
            [MarshalAs(UnmanagedType.SysInt)] int error,
            IntPtr userData
        );

        public delegate bool OnAssetSelectRepresentation(
            IntPtr adaptionSet,
            [MarshalAs(UnmanagedType.U4)] uint representationIndex,
            IntPtr representations,
            [MarshalAs(UnmanagedType.U4)] uint representationCount,
            IntPtr userData
        );

        public delegate void OnAssetRepresentationDataReceived(
            [In] [MarshalAs(UnmanagedType.LPStr)] string mimeType,
            [In] [MarshalAs(UnmanagedType.LPStr)] string codec,
            float startTime,
            IntPtr data,
            [MarshalAs(UnmanagedType.U4)] uint dataSize,
            IntPtr userData
        );

        // NOTE: AssetCreationInfo is declared as struct so defaultly passed as value
        // Need to be extremely careful when declaring the function has AssetCreationInfo as parameters
        // because they usually need 'ref' keyword to pass by reference
        [StructLayout(LayoutKind.Sequential)]
        public struct AssetCreationInfo
        {
            [MarshalAs(UnmanagedType.U4)]  public uint structSize;
            [MarshalAs(UnmanagedType.LPStr)] public string assetPath;
            [MarshalAs(UnmanagedType.LPStr)] public string cacheDir;
            public IntPtr userData;
            [MarshalAs(UnmanagedType.FunctionPtr)] public OnAssetInitialised onInitialized;
            [MarshalAs(UnmanagedType.FunctionPtr)] public OnAssetSelectRepresentation onSelectRepresentation;
            [MarshalAs(UnmanagedType.FunctionPtr)] public OnAssetRepresentationDataReceived onRepresentationDataRecieved;
            [MarshalAs(UnmanagedType.R4)] public float bufferTime;
        }

        public delegate void LogCallback(int messageType, IntPtr str);
    }
}