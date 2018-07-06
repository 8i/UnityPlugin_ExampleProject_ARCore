using UnityEngine;
using UnityEngine.Rendering;

namespace HVR
{
    public class HvrBuffers
    {
        public int bufferWidth { get { return m_bufferWidth; } }
        public int bufferHeight { get { return m_bufferHeight; } }

        public RenderTexture color;
        public RenderTexture colorCopy;
        public RenderTexture depth;

        private int m_bufferWidth;
        private int m_bufferHeight;

        public int createdFrameIndex;

        public HvrBuffers(int pixelWidth, int pixelHeight)
        {
            m_bufferWidth = pixelWidth;
            m_bufferHeight = pixelHeight;

            // RenderTextureReadWrite.Linear so that the colors within the textures are never modified as they go through the pipeline
            // http://docs.unity3d.com/ScriptReference/RenderTextureReadWrite.html
            // When using Gamma color space, no conversions are done of any kind, and this setting is not used.
            // When Linear color space is used, then by default non-HDR render textures are considered to contain sRGB data (i.e. "regular colors"), and fragment shaders are considered to output linear color values.
            // So by default the fragment shader color value is converted into sRGB when rendering into a texture; and when sampling the texture in the shader the sRGB colors are converted into linear values.

            // Metal strictly needs depth if shader writes to depth
            bool isMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            int depthBit = 0;
            if (isMetal)
                depthBit = 16;

            color = new RenderTexture(pixelWidth, pixelHeight, depthBit, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "HvrBuffers_color",
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
                anisoLevel = 0,
                filterMode = FilterMode.Point,
                isPowerOfTwo = false,
                useMipMap = false
            };
            color.Create();

            colorCopy = new RenderTexture(pixelWidth, pixelHeight, depthBit, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "HvrBuffers_colorCopy",
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
                anisoLevel = 0,
                filterMode = FilterMode.Point,
                isPowerOfTwo = false,
                useMipMap = false
            };
            colorCopy.Create();

            // Create Depth Buffer

            // http://docs.unity3d.com/ScriptReference/RenderTextureReadWrite.html
            // "Note that some render texture formats are always considered to contain "linear" data and
            // no sRGB conversions are ever performed on them, no matter what is the read-write setting.
            // This is true for all "HDR" (floating point) formats, and other formats like Depth or Shadowmap."
            depth = new RenderTexture(pixelWidth, pixelHeight, 32, Helper.GetSupportedRenderTextureFormatForDepthBlit())
            {
                name = "HvrBuffers_depth",
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
                anisoLevel = 0,
                filterMode = FilterMode.Point,
                isPowerOfTwo = false,
                useMipMap = false
            };
            depth.Create();

            createdFrameIndex = Time.frameCount;
        }

        public void Dispose()
        {
            createdFrameIndex = 0;

            if (color)
            {
                color.Release();
                color = null;
            }

            if (colorCopy)
            {
                colorCopy.Release();
                colorCopy = null;
            }

            if (depth)
            {
                depth.Release();
                depth = null;
            }
            // This is required in order to fully release the render textures.
            // Without this function being called here, the render textures will persist in memory
            Resources.UnloadUnusedAssets();
        }
    }
}
