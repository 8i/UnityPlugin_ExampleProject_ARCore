using HVR.Editor;
using UnityEditor;
using UnityEditorInternal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HVR.Editor
{
    [CustomEditor(typeof(PluginReferenceController))]
    [CanEditMultipleObjects]
    public class PluginReferenceController_Inspector : UnityEditor.Editor
    {
        PluginReferenceController self { get { return (PluginReferenceController)target; } }

        private ReorderableList reorderableList;

        private float lineHeight = 0;

        private void OnEnable()
        {
            lineHeight = EditorGUIUtility.singleLineHeight * 1.25f;

            if (self.references == null)
                self.references = new List<PluginReferenceController.PluginReference>();
                    
            reorderableList = new ReorderableList(self.references, typeof(PluginReferenceController.PluginReference), true, true, true, true);

            // Add listeners to draw events
            reorderableList.drawHeaderCallback += DrawHeader;
            reorderableList.drawElementCallback += DrawElement;

            reorderableList.elementHeightCallback = (index) =>
            {
                Repaint();
                return lineHeight * 10;
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
            // Actually draw the list in the inspector
            reorderableList.DoLayoutList();

            //this.DrawDefaultInspector();

            EditorUtility.SetDirty(target);
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
            PluginReferenceController.PluginReference item = self.references[index];

            EditorGUI.BeginChangeCheck();

            float o = 0;

            float lx = rect.x;
            float rx = rect.x + rect.width / 2.0f;
            float wHalf = rect.width / 2;

            item.guid = HvrEditorGUI.DrawObjectSlot(new Rect(rect.x, rect.y + o, rect.width, lineHeight), "Object:", item.guid);
            o += lineHeight;

            EditorGUI.LabelField(new Rect(lx, rect.y + o, rect.width / 2, lineHeight), "Platform");
            EditorGUI.LabelField(new Rect(rx, rect.y + o, rect.width / 2, lineHeight), "Other");
            o += lineHeight;

            item.platformNone = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "Support None", item.platformNone);
            o += lineHeight;

            if (!item.platformNone)
            {
                item.platformWindows = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "Windows", item.platformWindows);
                item.supportEditor = EditorGUI.ToggleLeft(new Rect(rx, rect.y + o, wHalf, lineHeight), "Use In Editor", item.supportEditor);
                o += lineHeight;

                item.platformMac = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "Mac", item.platformMac);
                o += lineHeight;

                item.platformLinux = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "Linux", item.platformLinux);
                o += lineHeight;

                item.platformAndroid = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "Android", item.platformAndroid);
                o += lineHeight;

                item.platformiOS = EditorGUI.ToggleLeft(new Rect(lx, rect.y + o, wHalf, lineHeight), "iOS", item.platformiOS);
                o += lineHeight;

                item.versionFilter = EditorGUI.TextField(new Rect(lx, rect.y + o, rect.width, lineHeight), "Version Filter", item.versionFilter);
                o += lineHeight;
            }

            if (EditorGUI.EndChangeCheck())
            {
                self.references[index] = item;
                EditorUtility.SetDirty(target);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                PluginCompatibility.EnsureCompatiblity();
            }
        }

        private void AddItem(ReorderableList list)
        {
            self.references.Add(new PluginReferenceController.PluginReference());

            EditorUtility.SetDirty(target);
        }

        private void RemoveItem(ReorderableList list)
        {
            self.references.RemoveAt(list.index);

            EditorUtility.SetDirty(target);
        }
    }
}
