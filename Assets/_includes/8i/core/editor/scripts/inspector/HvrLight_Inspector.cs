using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    [CustomEditor(typeof(HvrLight))]
    public class HvrLight_Inspector : UnityEditor.Editor
    {
        HvrLight m_Instance;
        PropertyField[] m_fields;

        Color editorColor;

        public void OnEnable()
        {
            m_Instance = target as HvrLight;
            m_fields = ExposeProperties.GetProperties(m_Instance);
            editorColor = GUI.color;
        }

        public override void OnInspectorGUI()
        {
            if (m_Instance == null)
                return;

            Undo.RecordObject(target, "HVR Light");

            GUI.color = editorColor;

            Inspector_Utils.DrawHeader();
            GUILayout.Space(4);

            this.DrawDefaultInspector();
            ExposeProperties.Expose(m_fields);

            Light dependentLight = m_Instance.GetComponent<Light>();

            if (dependentLight != null && dependentLight.type == LightType.Directional)
            {
#if UNITY_2017_1_OR_NEWER  
                EditorGUI.BeginChangeCheck();

                m_Instance.ShadowDistanceLimit = EditorGUILayout.Slider("Shadow Distance Limit", m_Instance.ShadowDistanceLimit, 0.1f, QualitySettings.shadowDistance);

                if (EditorGUI.EndChangeCheck())
                {
                    HvrEditorGUI.ForceRedrawHvrWorldAndRepaintView();
                }
                
#else                
                EditorGUILayout.HelpBox("Casting shadows from directional lights is not supported in versions before Unity 2017.1.", MessageType.Warning);
#endif                
            }

            if (GUI.changed)
            {
                SceneView.RepaintAll();

                EditorUtility.SetDirty(target);

                Repaint();
            }
        }
    }
}