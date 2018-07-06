using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    public class Inspector_Utils
    {
        static GUIStyle m_alignLeftMiddleStyle;

        public static GUIStyle alignLeftMiddleStyle
        {
            get
            {
                if (m_alignLeftMiddleStyle == null)
                {
                    m_alignLeftMiddleStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 10
                    };
                }

                return m_alignLeftMiddleStyle;
            }
        }

        public static void DrawHeader()
        {
            Color editorColor = GUI.color;

            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);

            GUILayout.BeginHorizontal(GUI.skin.GetStyle("TextArea"));
            {
                GUI.color = editorColor;

                GUILayout.Label(EditorSharedResources.instance.headerLogo, alignLeftMiddleStyle, GUILayout.Height(20), GUILayout.Width(20));

                if (HVR.VersionInfo.VERSION == "-1")
                {
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField("DEVELOPER BUILD", alignLeftMiddleStyle, GUILayout.Height(20));
                    GUI.color = editorColor;
                }
                else
                {
                    GUILayout.Label("Version: " + HVR.VersionInfo.VERSION, alignLeftMiddleStyle, GUILayout.Height(20));
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("?"))
                {
                    AboutWindow.ShowWindow();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
