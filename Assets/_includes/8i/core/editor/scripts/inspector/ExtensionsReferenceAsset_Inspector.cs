using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace HVR.Editor
{
    [CustomEditor(typeof(ExtensionsReferenceAsset))]
    [CanEditMultipleObjects]
    public class ExtensionsReferenceAsset_Inspector : UnityEditor.Editor
    {
        ExtensionsReferenceAsset self { get { return (ExtensionsReferenceAsset)target; } }

        private ReorderableList reorderableList;

        private void OnEnable()
        {
            reorderableList = new ReorderableList(self.references, typeof(ExtensionsReferenceAsset.Reference), true, true, true, true);

            // Add listeners to draw events
            reorderableList.drawHeaderCallback += DrawHeader;
            reorderableList.drawElementCallback += DrawElement;

            reorderableList.elementHeightCallback = (index) =>
            {
                Repaint();
                return HvrEditorGUI.guiLineHeight * 2;
            };

            reorderableList.onAddCallback += AddItem;
            reorderableList.onRemoveCallback += RemoveItem;
        }

        private void OnDisable()
        {
            // Make sure we don't get memory leaks etc.
            reorderableList.drawHeaderCallback -= DrawHeader;
            reorderableList.drawElementCallback -= DrawElement;

            reorderableList.onAddCallback -= AddItem;
            reorderableList.onRemoveCallback -= RemoveItem;
        }

        public override void OnInspectorGUI()
        {
            reorderableList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Draws the header of the list
        /// </summary>
        /// <param name="rect"></param>
        private void DrawHeader(Rect rect)
        {
            GUI.Label(rect, "Extension Reference");
        }

        /// <summary>
        /// Draws one element of the list (ListItemExample)
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="index"></param>
        /// <param name="active"></param>
        /// <param name="focused"></param>
        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            ExtensionsReferenceAsset.Reference reference = self.references[index];

            EditorGUI.BeginChangeCheck();

            float yOffset = 0;
            reference.platform = (RuntimePlatform)EditorGUI.EnumPopup(new Rect(rect.x, rect.y + yOffset, rect.width, HvrEditorGUI.guiLineHeight), "Platform", reference.platform);
            yOffset += HvrEditorGUI.guiLineHeight;
            reference.guid = HvrEditorGUI.DrawObjectSlot(new Rect(rect.x, rect.y + yOffset, rect.width, HvrEditorGUI.guiLineHeight), "Data:", reference.guid);
            yOffset += HvrEditorGUI.guiLineHeight;

            if (EditorGUI.EndChangeCheck())
            {
                self.references[index] = reference;
                EditorUtility.SetDirty(target);
            }
        }

        private void AddItem(ReorderableList list)
        {
            self.references.Add(new ExtensionsReferenceAsset.Reference());

            EditorUtility.SetDirty(target);
        }

        private void RemoveItem(ReorderableList list)
        {
            self.references.RemoveAt(list.index);

            EditorUtility.SetDirty(target);
        }


    }
}
