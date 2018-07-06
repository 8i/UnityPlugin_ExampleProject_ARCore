// Timeline appears to be missing in Linux versions of Unity 2017.4
#if UNITY_2017_1_OR_NEWER && !(UNITY_2017_4 && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX))

using UnityEditor;

namespace HVR.Timeline
{
    [CustomEditor(typeof(HvrActor_PlayableAsset))]
    class HvrActor_PlayableAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty _dataGUID = serializedObject.FindProperty("dataGUID");

            string pathToDataObject = AssetDatabase.GUIDToAssetPath(_dataGUID.stringValue);

            UnityEngine.Object dataObject = AssetDatabase.LoadAssetAtPath(pathToDataObject, typeof(UnityEngine.Object));
            dataObject = EditorGUILayout.ObjectField("Data", dataObject, typeof(UnityEngine.Object), false);

            string guid = "";

            if (dataObject != null)
            {
                string path = AssetDatabase.GetAssetPath(dataObject);
                guid = AssetDatabase.AssetPathToGUID(path);
            }

            if (_dataGUID.stringValue != guid)
            {
                _dataGUID.stringValue = guid;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif