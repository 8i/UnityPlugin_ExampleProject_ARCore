using System;
using UnityEngine;

namespace HVR.Interface
{
    public class ActorInterface
    {
        public int handle { get { return m_handle; } }
        private int m_handle = Types.INVALID_HANDLE;

        public void Create()
        {
            m_handle = Types.INVALID_HANDLE;

            if (!HvrHelper.Support.IsApplicationStateSupported())
                return;

            try
            {
                UnityInterface.Lock();

                if (HvrPlayerInterface.Initialise())
                {
                    m_handle = HvrPlayerInterfaceAPI.Actor_Create();

#if VERBOSE_LOGGING
                    Debug.Log("Create " + GetType().Name + " Handle:" + handle);
#endif

                    UnityInterface.SceneObjectsAdd(m_handle, GetType().Name + m_handle, GetType().FullName);
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
            if (m_handle == Types.INVALID_HANDLE)
                return;

            try
            {
                UnityInterface.Lock();

                HvrPlayerInterfaceAPI.Actor_Delete(m_handle);

#if VERBOSE_LOGGING
                Debug.Log("Delete " + GetType().Name + " Handle:" + handle);
#endif

                UnityInterface.SceneObjectsRemove(m_handle);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                m_handle = Types.INVALID_HANDLE;

                UnityInterface.Unlock();
            }
        }

        public bool IsValid()
        {
            if (m_handle == Types.INVALID_HANDLE)
                return false;

            return HvrPlayerInterfaceAPI.Actor_IsValid(m_handle);
        }

        public void SetAsset(int _assethandle)
        {
            if (m_handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Actor_SetAsset(m_handle, _assethandle);
        }

        public void SetRenderMethod(int _renderMethodHandle)
        {
            if (m_handle == Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Actor_SetRenderMethod(m_handle, _renderMethodHandle);
        }

        public void SetTransform(Transform trans, float scaleFactor)
        {
            if (m_handle == Types.INVALID_HANDLE)
                return;

            // Set scale factor
            Matrix4x4 matrix = trans.localToWorldMatrix * Matrix4x4.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));

            // Account for Unity's -Z orientation, and fix the actors being mirrored
            matrix = matrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));

            var apiMat44 = HvrPlayerInterfaceHelper.GetMat44FromMatrix4x4(matrix);
            HvrPlayerInterfaceAPI.Actor_SetTransform(m_handle, apiMat44);
        }

        public void SetSubroutineUniformInt(string uniformName, int value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformInt(handle, uniformName, value);
        }

        public void SetSubroutineUniformFloat(string uniformName, float value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformFloat(handle, uniformName, value);
        }

        public void SetSubroutineUniformVec2(string uniformName, Vector2 value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            CommonTypes.Vec2 vec2 = new CommonTypes.Vec2 { x = value.x, y = value.y };

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformVec2(handle, uniformName, ref vec2);
        }

        public void SetSubroutineUniformVec3(string uniformName, Vector3 value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            CommonTypes.Vec3 vec3 = new CommonTypes.Vec3 {
                x = value.x,
                y = value.y,
                z = value.z
            };

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformVec3(handle, uniformName, ref vec3);
        }

        public void SetSubroutineUniformVec4(string uniformName, Vector4 value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            CommonTypes.Vec4 vec4 = new CommonTypes.Vec4
            {
                x = value.x,
                y = value.y,
                z = value.z,
                w = value.w
            };

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformVec4(handle, uniformName, ref vec4);
        }

        // Sets the uniform using the top-left 3x3 submatrix of the provided 4x4 matrix. 
        public void SetSubroutineUniformMat3x3(string uniformName, Matrix4x4 value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            CommonTypes.Mat33 mat33 = new CommonTypes.Mat33();
            mat33.m = new float[]
            {
                value[0, 0], value[1, 0], value[2, 0],
                value[0, 1], value[1, 1], value[2, 1],
                value[0, 2], value[1, 2], value[2, 2]
            };

            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformMat3x3(handle, uniformName, ref mat33);
        }

        public void SetSubroutineUniformMat4x4(string uniformName, Matrix4x4 value)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            CommonTypes.Mat44 mat44 = new CommonTypes.Mat44();
            mat44.m = new float[]
            {
                value[0, 0], value[1, 0], value[2, 0], value[3, 0],
                value[0, 1], value[1, 1], value[2, 1], value[3, 1],
                value[0, 2], value[1, 2], value[2, 2], value[3, 2],
                value[0, 3], value[1, 3], value[2, 3], value[3, 3]
            };
            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformMat4x4(handle, uniformName, ref mat44);
        }

        public void SetSubroutineUniformTexture2D(string uniformName, Texture texture)
        {
            if (m_handle == HVR.Interface.Types.INVALID_HANDLE)
                return;

            IntPtr nativePtr = texture.GetNativeTexturePtr();
            HvrPlayerInterfaceAPI.Actor_SetSubroutineUniformTexture2D(handle, uniformName, HvrScene.ScenePlayerHandle(), nativePtr);
        }
    }
}
