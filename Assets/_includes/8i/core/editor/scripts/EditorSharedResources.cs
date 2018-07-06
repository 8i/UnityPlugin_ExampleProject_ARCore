using UnityEngine;
using UnityEditor;

namespace HVR.Editor
{
    [InitializeOnLoad]
    public class EditorSharedResources : ScriptableObject
    {
        public static EditorSharedResources instance
        {
            get
            {
                if (m_instance == null)
                {
                    EditorSharedResources esr = Resources.Load("8i/editor/EditorSharedResources") as EditorSharedResources;
                    m_instance = esr;
                }

                return m_instance;
            }
        }

        static EditorSharedResources m_instance;

        public Texture headerLogo;
        public Texture2D aboutWindowLogo;

        public Texture2D hvrActorPlay;
        public Texture2D hvrActorPause;
        public Texture2D hvrActorStop;
        public Texture2D hvrActorStepBack;
        public Texture2D hvrActorStepForward;
        public Texture2D hvrActorStepLoopOn;
        public Texture2D hvrActorStepLoopOff;

        public Texture2D hvrColorGradeNormal;
        public Texture2D hvrColorGradeHover;
        public Texture2D hvrColorGradeActive;

        public Shader hvrRenderEditorPreviewShader;

        public Object androidUnpackingScene;

        public Object plugin_HvrPlayerInterfaceFramework_macOS;

        public PluginReferenceController[] pluginCompatibility;
    }
}
