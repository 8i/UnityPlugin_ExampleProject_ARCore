using AOT;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace HVR.Interface
{
    public static class HvrPlayerInterfaceAPI
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string DLLName = "__Internal";
#else
        public const string DLLName = "HVRPlayerInterface";
#endif

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_Initialise([In] ref Types.InterfaceInitialiseInfo info);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEvent))]
        public static extern void Interface_Shutdown();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_IsInitialised();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Interface_Update();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Interface_Reconnect();

        public const int INTERFACE_LOG_TYPE_ALL = 0;
        public const int INTERFACE_LOG_TYPE_DEBUG = 0;
        public const int INTERFACE_LOG_TYPE_INFO = 1;
        public const int INTERFACE_LOG_TYPE_WARNING = 2;
        public const int INTERFACE_LOG_TYPE_ERROR = 3;
        public const int INTERFACE_LOG_TYPE_NONE = 4;

        // System values that can be queried 
        public struct Player_GetInfo_Keys
        {
            public const string VERSION_MAJOR = "VERSION_MAJOR";
            public const string VERSION_MINOR = "VERSION_MINOR";
            public const string VERSION_REVISION = "VERSION_REVISION";

            public const string VERSION_CHANGES = "VERSION_CHANGES";
            public const string VERSION_EDIT = "VERSION_EDIT";
            public const string VERSION = "VERSION";

            public const string BUILD_HOST = "BUILD_HOST";
            public const string BUILD_DATE = "BUILD_DATE";
            public const string BUILD_INFO = "BUILD_INFO";

            public const string GIT_BRANCH = "GIT_BRANCH";
            public const string GIT_HASH = "GIT_HASH";
            public const string GIT_MODIFIED = "GIT_MODIFIED";
            public const string GIT_INFO = "GIT_INFO";
        }

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_GetInfo(string key, StringBuilder value, int valueSize);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_PopLogEntry(int logLevel, StringBuilder value, int valueSize);

        public static void Interface_SetLogCallback(Types.LogCallback callback)
        {
            // Marshalling complex types like delegates and callbacks needs special treaments
            // Unity interop doesn't simply do that for us.
            // https://msdn.microsoft.com/en-us/library/367eeye0.aspx
            // https://msdn.microsoft.com/en-us/library/system.runtime.interopservices.marshal.getfunctionpointerfordelegate.aspx
            IntPtr callback_ptr = Marshal.GetFunctionPointerForDelegate(callback);
            Interface_SetLogCallback(callback_ptr);
        }
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Interface_SetLogCallback(IntPtr callback);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Interface_SetLogLevel(int logLevel);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CommonTypes.MemoryStats Interface_GetMemoryStats();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CommonTypes.NetworkStats Interface_GetNetworkStats();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Interface_GetRenderMethodTypeCount();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_GetRenderMethodType(int idx, StringBuilder value, int valueSize);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Interface_GetRenderMethodDefault(StringBuilder value, int valueSize);

        //-----------------------------------------------------------------------------
        // Player Functions

        public const int RENDERER_TYPE_NONE = -1;
        public const int RENDERER_TYPE_OPENGL = 0;
        public const int RENDERER_TYPE_DIRECT3D11 = 1;
        public const int RENDERER_TYPE_GNMX = 2;
        public const int RENDERER_TYPE_METAL = 3;

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 Player_Create(int rendererType, IntPtr device);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventI))]
        public static extern void Player_Delete(Int32 player);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Player_IsValid(Int32 player);

        // Delete all Render data
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventI))]
        public static extern void Player_Detach(Int32 player);

        // Specifies an actor / viewport pair will be rendered this frame.
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventIII))]
        public static extern void Player_WillRender(Int32 player, Int32 actor, Int32 viewport);

        // Calculates LOD information from all WillRender calls, Prepares for Render calls and Decodes frames. 
        // All WillRender calls must be completed first.
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventI))]
        public static extern void Player_PrepareRender(Int32 player, IntPtr frameContext);

        // Renders an actor / viewport pair. Must be called after WillRender & PrepareRender.
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        [MonoPInvokeCallback(typeof(UnityInterfaceAPI.GLEventWithFrameContextIII))]
        public static extern void Player_Render(Int32 player, Int32 actor, Int32 viewport, IntPtr frameContext);

        //-----------------------------------------------------------------------------
        // Actor Functions

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 Actor_Create();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_Delete(Int32 actor);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Actor_IsValid(Int32 actor);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetAsset(Int32 actor, Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetRenderMethod(Int32 actor, Int32 renderMethod);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetTransform(Int32 actor, CommonTypes.Mat44 transform);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformInt(Int32 actor, string uniformName, int intValue);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformFloat(Int32 actor, string uniformName, float floatValue);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]

        public static extern void Actor_SetSubroutineUniformVec2(Int32 actor, string uniformName, [In] ref CommonTypes.Vec2 value);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformVec3(Int32 actor, string uniformName, [In] ref CommonTypes.Vec3 value);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformVec4(Int32 actor, string uniformName, [In] ref CommonTypes.Vec4 value);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformMat3x3(Int32 actor, string uniformName, [In] ref CommonTypes.Mat33 value);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformMat4x4(Int32 actor, string uniformName, [In] ref CommonTypes.Mat44 value);

        // The textureNativeHandle argument should be either the GLuint texture ID for OpenGL, or the ID3D11Resource* for D3D11.
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Actor_SetSubroutineUniformTexture2D(Int32 actor, string uniformName, Int32 player, IntPtr textureNativeHandle);

        //-----------------------------------------------------------------------------
        // Asset Functions

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 Asset_Create(string fileFolder);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 Asset_CreateFromInfo([In] ref Types.AssetCreationInfo info);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Delete(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Asset_IsValid(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Update(Int32 asset, float absoluteTime);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Play(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Pause(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Seek(Int32 asset, float time);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_Step(Int32 asset, int frames);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Asset_SetLooping(Int32 asset, bool looping);

        public const int ASSET_STATE_INITIALISING = 1 << 0;
        public const int ASSET_STATE_PLAYING = 1 << 1;
        public const int ASSET_STATE_SEEKING = 1 << 2;
        public const int ASSET_STATE_CACHING = 1 << 3;
        public const int ASSET_STATE_OFFLINE = 1 << 4;
        public const int ASSET_STATE_FULLY_CACHED = 1 << 5;
        public const int ASSET_STATE_INVALID = 1 << 6;
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Asset_GetState(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CommonTypes.Bounds Asset_GetBounds(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float Asset_GetCurrentTime(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float Asset_GetActualTime(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float Asset_GetDecodeTime(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float Asset_GetDuration(Int32 asset);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Asset_GetFrameMeta(Int32 asset, string json, int jsonSize);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Asset_GetTrackMeta(Int32 asset, string json, int jsonSize);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Asset_GetVoxelCount(Int32 asset);

        //-----------------------------------------------------------------------------
        // RenderMethod Functions

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 RenderMethod_Create(string type);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderMethod_Delete(Int32 renderMethod);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool RenderMethod_IsValid(Int32 renderMethod);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderMethod_SetShaderSubroutines(Int32 renderMethod, string code);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderMethod_SetShaderSubroutinesArray(Int32 renderMethod, string[] code, int count);
        //-----------------------------------------------------------------------------
        // Viewport Functions

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 Viewport_Create();
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_Delete(Int32 viewport);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Viewport_IsValid(Int32 viewport);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_SetViewMatrix(Int32 viewport, CommonTypes.Mat44 viewMat44);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_SetProjMatrix(Int32 viewport, CommonTypes.Mat44 projMat44);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_SetDimensions(Int32 viewport, float x, float y, float width, float height);

        public const int COLOUR_SPACE_GAMMA = 0;
        public const int COLOUR_SPACE_LINEAR = 1;
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_SetColourSpace(Int32 viewport, int colourspace);
        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Viewport_SetReverseDepthEnabled(Int32 viewport, Int32 reverseDepthEnabled);
    }
}
