using UnityEngine;
using System;
using System.Text;
using System.Runtime.InteropServices;

namespace HVR.Interface
{
    public static class CommonTypes 
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Vec2
        {
            public float x;
            public float y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Vec3
        {
            public float x;
            public float y;
            public float z;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Vec4
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class Mat33
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
            public float[] m;

            public Mat33()
            {
                m = new float[9];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public class Mat44
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public float[] m;

            public Mat44()
            {
                m = new float[16];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Bounds
        {
            public Vec3 center;
            public Vec3 halfDims;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryStats
        {
            // Total allocations since initialisation
            public ulong allocBytes;
            public ulong allocBlocks;

            // Total frees since initialisation
            public ulong freeBytes;
            public ulong freeBlocks;

            // Currently used
            public long usedBytes;
            public long usedBlocks;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NetworkStats
        {
            public long receivedBits;
            public long sentBits;
            public long bitsPerSecond;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HVRAdaptationSet
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string mimeType;
            [MarshalAs(UnmanagedType.LPStr)]
            public string codec;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HVRRepresentation
        {
            public float maxFPS;
            [MarshalAs(UnmanagedType.U4)]
            public uint bandwidth;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FrameContext
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint structSize;
            [MarshalAs(UnmanagedType.U4)]
            public uint pixelFormat;
            public IntPtr commandQueue;
            public IntPtr commandEncoder;
        }
    }
}