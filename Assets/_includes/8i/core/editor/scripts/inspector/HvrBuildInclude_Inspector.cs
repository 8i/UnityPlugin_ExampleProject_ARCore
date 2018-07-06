using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HVR.Editor
{
    [CustomEditor(typeof(HvrDataReference))]
    [CanEditMultipleObjects]
    public class HvrDataReference_Inspector : UnityEditor.Editor
    {
        HvrDataReference self { get { return (HvrDataReference)target; } }

        private ReorderableList reorderableList;

        private float guiLineHeight = 0;

        private void OnEnable()
        {
            guiLineHeight = EditorGUIUtility.singleLineHeight * 1.25f;

            reorderableList = new ReorderableList(self.data, typeof(HvrDataReference.Data), true, true, true, true);

            // Add listeners to draw events
            reorderableList.drawHeaderCallback += DrawHeader;
            reorderableList.drawElementCallback += DrawElement;

            reorderableList.elementHeightCallback = (index) =>
            {
                Repaint();
                return guiLineHeight * 1;
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
            //base.OnInspectorGUI();

            // Actually draw the list in the inspector
            reorderableList.DoLayoutList();

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
            GUI.Label(rect, "Data");
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
            HvrDataReference.Data item = self.data[index];

            EditorGUI.BeginChangeCheck();

            float yOffset = 0;
            item.guid = HvrEditorGUI.DrawObjectSlot(new Rect(rect.x, rect.y + yOffset, rect.width, guiLineHeight), "Data:", item.guid);
            yOffset += guiLineHeight;

            if (EditorGUI.EndChangeCheck())
            {
                self.data[index] = item;
                EditorUtility.SetDirty(target);
            }
        }

        private void AddItem(ReorderableList list)
        {
            self.data.Add(new HvrDataReference.Data());

            EditorUtility.SetDirty(target);
        }

        private void RemoveItem(ReorderableList list)
        {
            self.data.RemoveAt(list.index);

            EditorUtility.SetDirty(target);
        }
    }
}
