using UnityEngine;

namespace HVR.Interface
{
    static class HvrPlayerInterfaceHelper
    {
        public static CommonTypes.Mat44 GetMat44FromMatrix4x4(Matrix4x4 m)
        {
            CommonTypes.Mat44 mat44 = new CommonTypes.Mat44();
            for (int a = 0; a < 16; ++a)
            {
                mat44.m[a] = m[a];
            }
            return mat44;
        }

        public static CommonTypes.Vec2 GetVec2FromVector2(Vector2 m)
        {
            CommonTypes.Vec2 vec2 = new CommonTypes.Vec2();
            vec2.x = m.x;
            vec2.y = m.y;
            return vec2;
        }

        public static CommonTypes.Vec3 GetVec2FromVector3(Vector3 m)
        {
            CommonTypes.Vec3 vec3 = new CommonTypes.Vec3();
            vec3.x = m.x;
            vec3.y = m.y;
            vec3.z = m.z;
            return vec3;
        }

        public static CommonTypes.Vec4 GetVec2FromVector4(Vector4 m)
        {
            CommonTypes.Vec4 vec4 = new CommonTypes.Vec4();
            vec4.x = m.x;
            vec4.y = m.y;
            vec4.z = m.z;
            vec4.w = m.w;
            return vec4;
        }
    }
}
