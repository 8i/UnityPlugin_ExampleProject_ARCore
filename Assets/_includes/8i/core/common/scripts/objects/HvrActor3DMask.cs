using UnityEngine;

namespace HVR
{
    [ExecuteInEditMode]
    [AddComponentMenu("8i/HvrActor3DMask")]
    public class HvrActor3DMask : MonoBehaviour
    {
        public HvrActor3DMaskObject[] objects = new HvrActor3DMaskObject[0];

        Shader maskShader;
        Material maskMaterial;

        float[] masks_type = new float[0];
        float[] masks_additive = new float[0];
        Vector4[] mask_sphere_center = new Vector4[0];
        float[] mask_sphere_radius = new float[0];
        Matrix4x4[] mask_box_matrix = new Matrix4x4[0];

        void Start()
        {
            maskShader = Resources.Load(Uniforms.ResourcePaths.shader_Blit_Hvr3DMask) as Shader;

            if (maskShader != null)
            {
                maskMaterial = new Material(maskShader);
            }
            else
            {
                Debug.LogWarning("3DMaskShader could not be found, disabling this component", this);
                this.enabled = false;
                return;
            }

            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            int maskCount = objects.Length;
            if (maskCount == 0)
                return;

            if (masks_type != null && masks_type.Length != maskCount)
            {
                // Recreating the material here avoids a warning around changing the size of the array
                // within the material
                if (maskShader != null)
                    maskMaterial = new Material(maskShader);
            }

            if (masks_type == null || masks_type.Length != maskCount)
                masks_type = new float[maskCount];

            if (masks_additive == null || masks_additive.Length != maskCount)
                masks_additive = new float[maskCount];

            if (mask_sphere_center == null || mask_sphere_center.Length != maskCount)
                mask_sphere_center = new Vector4[maskCount];

            if (mask_sphere_radius == null || mask_sphere_radius.Length != maskCount)
                mask_sphere_radius = new float[maskCount];

            if (mask_box_matrix == null || mask_box_matrix.Length != maskCount)
                mask_box_matrix = new Matrix4x4[maskCount];

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                    continue;

                HvrActor3DMaskObject mask = objects[i];
                GameObject go = mask.gameObject;

                masks_additive[i] = mask.additive ? 1 : 0;

                Vector3 sphere_center = Vector3.zero;
                float sphere_radius = 0;
                Matrix4x4 box_matrix = Matrix4x4.identity;

                switch (mask.type)
                {
                    case HvrActor3DMaskObject.eType.sphere:
                        masks_type[i] = 0;
                        sphere_center = go.transform.position;
                        sphere_radius = Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.y, go.transform.lossyScale.z);
                        break;

                    case HvrActor3DMaskObject.eType.box:
                        masks_type[i] = 1;
                        box_matrix = go.transform.worldToLocalMatrix;
                        break;
                }

                mask_sphere_center[i] = sphere_center;
                mask_sphere_radius[i] = sphere_radius;
                mask_box_matrix[i] = box_matrix;
            }

            maskMaterial.SetFloat(Uniforms.ShaderProperties._mask_length, maskCount);
            maskMaterial.SetFloatArray(Uniforms.ShaderProperties._mask_types, masks_type);
            maskMaterial.SetFloatArray(Uniforms.ShaderProperties._mask_additive, masks_additive);
            maskMaterial.SetVectorArray(Uniforms.ShaderProperties._mask_sphere_center, mask_sphere_center);
            maskMaterial.SetFloatArray(Uniforms.ShaderProperties._mask_sphere_radius, mask_sphere_radius);
            maskMaterial.SetMatrixArray(Uniforms.ShaderProperties._mask_box_matrix, mask_box_matrix);
        }

        public void Mask(Camera cam, RenderTexture depth)
        {
            if (maskMaterial == null)
                return;

            Matrix4x4 inverseViewProjection = (cam.projectionMatrix * cam.worldToCameraMatrix).inverse;

            maskMaterial.SetMatrix(Uniforms.ShaderProperties._ViewProjectInverse, inverseViewProjection);
            maskMaterial.SetTexture(Uniforms.ShaderProperties._HvrDepthTex, depth);

            UpdateMaterial();

            RenderTexture tempRT_3DMask = RenderTexture.GetTemporary(depth.width, depth.height, 16, Helper.GetSupportedRenderTextureFormatForDepthBlit());
    
            Graphics.Blit(depth, tempRT_3DMask, maskMaterial, 0);
            Graphics.Blit(tempRT_3DMask, depth);
            RenderTexture.ReleaseTemporary(tempRT_3DMask);
        }
    }
}
