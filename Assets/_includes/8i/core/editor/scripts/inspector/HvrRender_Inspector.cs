using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(HvrRender))]
    public class HvrRender_Inspector : UnityEditor.Editor
    {
        private HvrEditorGUI hvrEditorGUI;

        private void OnEnable()
        {
            hvrEditorGUI = new HvrEditorGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Inspector_Utils.DrawHeader();

            GUILayout.Space(4);

            DrawCustomInspector();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        private void DrawCustomInspector()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndVertical();

                hvrEditorGUI.HvrRenderMode(target, serializedObject);
            }
            EditorGUILayout.EndVertical();
        }
    }
}
