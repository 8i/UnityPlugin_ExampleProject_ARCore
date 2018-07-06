using HVR.Interface;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace HVR
{
    public static class HvrScene
    {
        private static List<WeakReference> references = new List<WeakReference>();

        public delegate void OnActorAdded(HvrActor actor);
        public static OnActorAdded onActorAdded;

        public delegate void OnActorRemoved(HvrActor actor);
        public static OnActorRemoved onActorRemoved;

        public static int lastPreparedFrame = -1;

        public static int ScenePlayerHandle()
        {
            int handle = HVR.Interface.Types.INVALID_HANDLE;

            UnityInterfaceAPI.Lock();

            // Check if a ScenePlayer already exists, use it instead of creating a new one
            if (UnityInterfaceAPI.Map_Contains(Uniforms.Keys.SCENEPLAYER_NAME) &&
                UnityInterfaceAPI.Map_GetValue(Uniforms.Keys.SCENEPLAYER_NAME) != HVR.Interface.Types.INVALID_HANDLE)
            {
                handle = UnityInterfaceAPI.Map_GetValue(Uniforms.Keys.SCENEPLAYER_NAME);

                if (UnityInterfaceAPI.Map_Contains(Uniforms.Keys.SCENEPLAYER_GRAPHICS_DEVICE_TYPE))
                {
                    // Check if the graphics context has changed and recreate the player if it has
                    GraphicsDeviceType deviceType = (GraphicsDeviceType)UnityInterfaceAPI.Map_GetValue(Uniforms.Keys.SCENEPLAYER_GRAPHICS_DEVICE_TYPE);

                    if (deviceType != SystemInfo.graphicsDeviceType)
                    {
                        DeleteScenePlayer();
                        handle = HVR.Interface.Types.INVALID_HANDLE;
                    }
                }
            }

            // Create a new player if the handle is null, or the player is invalid
            if (handle == HVR.Interface.Types.INVALID_HANDLE ||
                !HvrPlayerInterfaceAPI.Player_IsValid(handle))
            {
                if (HvrHelper.Support.IsApplicationStateSupported())
                {
                    PlayerInterface player = new PlayerInterface();

                    // In order to get around Unity clearing memory between state changes such
                    // as reloading code or changing the graphics API within the editor, we store
                    // a reference in the memory of a native plugin which we control the state of
                    UnityInterfaceAPI.Map_Add(Uniforms.Keys.SCENEPLAYER_NAME, player.handle);
                    UnityInterfaceAPI.Map_Add(Uniforms.Keys.SCENEPLAYER_GRAPHICS_DEVICE_TYPE, (int)player.graphicsDeviceType);
                }
            }

            UnityInterfaceAPI.Unlock();

            return handle;
        }

        public static bool ScenePlayerExists()
        {
            // Check if a ScenePlayer already exists, use it instead of creating a new one
            if (UnityInterfaceAPI.Map_Contains(Uniforms.Keys.SCENEPLAYER_NAME))
                return true;

            return false;
        }

        public static void DeleteScenePlayer()
        {
            if (ScenePlayerExists())
            {
                int scenePlayerHandle = UnityInterfaceAPI.Map_GetValue(Uniforms.Keys.SCENEPLAYER_NAME);

                UnityInterfaceAPI.Map_Remove(Uniforms.Keys.SCENEPLAYER_NAME);
                UnityInterfaceAPI.Map_Remove(Uniforms.Keys.SCENEPLAYER_GRAPHICS_DEVICE_TYPE);

                PlayerInterface player = new PlayerInterface(scenePlayerHandle);
                player.Delete();
            }
        }

        public static void GetObjects<T>(List<T> list)
        {
            foreach (WeakReference weak in references)
            {
                if (weak.IsAlive)
                {
                    if (weak.Target.GetType() == typeof(T))
                        list.Add((T)weak.Target);
                }
            }
        }

        public static void Add(object o)
        {
            if (Contains(o))
                return;

            CleanReferences();

            references.Add(new WeakReference(o));

            Type type = o.GetType();

            if (type == typeof(HvrActor))
            {
                if (onActorAdded != null)
                    onActorAdded((HvrActor)o);
            }

            if (type == typeof(HvrRender))
            {
                HvrRender render = (HvrRender)o;

                List<HvrActor> actors = new List<HvrActor>();
                GetObjects(actors);

                foreach (HvrActor actor in actors)
                {
                    render.AddHvrActor(actor);
                }
            }
        }

        public static void Remove(object o)
        {
            if (!Contains(o))
                return;

            CleanReferences();

            WeakReference foundRef = references.First(x => x.Target == o);

            if (foundRef != null)
            {
                references.Remove(foundRef);

                Type type = o.GetType();

                if (type == typeof(HvrActor))
                {
                    if (onActorRemoved != null)
                        onActorRemoved((HvrActor)o);
                }
            }
        }

        private static void CleanReferences()
        {
            // Handles cases where an object has been added to the references, but is now invalid
            references.RemoveAll(x => x.Target == null);

            // The case where the target is now "null" and not just null can occur when loading
            // the example scene which was built using Unity 2017 in an older version of unity
            // Since the old version of unity can't interpret the scene, some of the objects become
            // invalid and are "null" as a string. Weird, but that's how it is.
            references.RemoveAll(x => x.Target.ToString() == "null");
        }

        public static bool Contains(object o)
        {
            foreach (WeakReference weak in references)
            {
                if (weak.IsAlive)
                {
                    if (weak.Target == o)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void WillRender()
        {
            foreach (WeakReference weak in references)
            {
                if (weak.IsAlive)
                {
                    Type type = weak.Target.GetType();

                    if (type == typeof(HvrRender))
                    {
                        HvrRender render = (HvrRender)weak.Target;
                        render.WillRender();
                    }
                    else
                    if (type == typeof(HvrLight))
                    {
                        HvrLight light = (HvrLight)weak.Target;
                        light.WillRender();
                    }
                }
            }
        }

        public static void Player_WillRender(int actor, int viewport)
        {
            if (!HvrPlayerInterface.IsInitialized())
                return;

            int player = ScenePlayerHandle();
            int eventID = UnityInterfaceAPI.QueueGLEventIII(HvrPlayerInterfaceAPI.Player_WillRender, player, actor, viewport);
            GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID);
            GL.InvalidateState();
        }

        public delegate IntPtr GetRenderTextureNativePtr();

        static IntPtr ReturnZeroPointer()
        {
            return IntPtr.Zero;
        }

        public static void Player_PrepareRender(GetRenderTextureNativePtr getColorTarget, GetRenderTextureNativePtr getDepthTarget)
        {
            if (!HvrPlayerInterface.IsInitialized())
                return;

            // Only allow PrepareRender to be called if the frame has changed
            // This handles an issue with Metal where a internal buffer index needs to be bumped each PrepareRender
            //
            // A potential issue for this is that lodding step within PrepareRender will not take into account any viewport changes
            // that occur within this frame, after PrepareRender is called.
            if (Time.frameCount != lastPreparedFrame)
            {
                lastPreparedFrame = Time.frameCount;

                HvrPlayerInterface.Update();

                WillRender();

                int player = ScenePlayerHandle();
                if (getColorTarget == null)
                {
                    getColorTarget = ReturnZeroPointer;
                }
                if (getDepthTarget == null)
                {
                    getDepthTarget = ReturnZeroPointer;
                }

                int eventID = UnityInterfaceAPI.QueueGLEventPrepFrameContextI(HvrPlayerInterfaceAPI.Player_PrepareRender, player, 
                    getColorTarget(), getDepthTarget());
                GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID);
                GL.InvalidateState();
            }
        }

        public static void Player_RenderActor(int actor, int viewport, GetRenderTextureNativePtr getColorTarget, GetRenderTextureNativePtr getDepthTarget)
        {
            if (!HvrPlayerInterface.IsInitialized())
                return;

            bool sRGBWrite = GL.sRGBWrite;

            int player = ScenePlayerHandle();
            if (getColorTarget == null)
            {
                getColorTarget = ReturnZeroPointer;
            }
            if (getDepthTarget == null)
            {
                getDepthTarget = ReturnZeroPointer;
            }

            int eventID = UnityInterfaceAPI.QueueGLEventPrepFrameContextIII(HvrPlayerInterfaceAPI.Player_Render, player, actor, viewport, 
                getColorTarget(), getDepthTarget());
            GL.IssuePluginEvent(UnityInterfaceAPI.UnityRenderEventFunc(), eventID);
            GL.InvalidateState();

            // TODO: Reset this as a deferred step. 
            GL.sRGBWrite = sRGBWrite;
        }
    }
}
