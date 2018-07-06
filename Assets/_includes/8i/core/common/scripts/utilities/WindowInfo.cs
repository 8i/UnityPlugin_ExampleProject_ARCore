using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;

namespace HVR
{
    // Useful when you want to access Unity official editor windows
    // http://answers.unity3d.com/questions/267097/editor-programming-experts-regarding-layouts.html
    class WindowInfo
    {
        string defaultTitle;
        string menuPath;
        Type type;

        public WindowInfo(string typeName, string defaultTitle = null, string menuPath = null, System.Reflection.Assembly assembly = null)
        {
            this.defaultTitle = defaultTitle;
            this.menuPath = menuPath;
            if (assembly == null)
            {
                assembly = typeof(UnityEditor.EditorWindow).Assembly;
            }

            type = assembly.GetType(typeName);

            if (type == null)
            {
                Debug.LogWarning("Unable to find type \"" + typeName + "\" in assembly \"" + assembly.GetName().Name + "\".\nYou might want to update the data in WindowInfos.");
            }
        }

        public EditorWindow[] FindAll()
        {
            if (type == null)
                return new EditorWindow[0];

            return (EditorWindow[])(Resources.FindObjectsOfTypeAll(type));
        }
        public EditorWindow FindFirst()
        {
            foreach (EditorWindow window in FindAll())
            {
                return window;
            }
            return null;
        }
        public EditorWindow FindFirstOrCreate()
        {
            EditorWindow window = FindFirst();
            if (window != null)
                return window;

            if (type == null)
                return null;

            if (menuPath != null && menuPath.Length != 0)
            {
                EditorApplication.ExecuteMenuItem(menuPath);
            }

            window = EditorWindow.GetWindow(type, false, defaultTitle);
            return window;
        }

        // shortcut for setting/getting the position and size of the first window of this type.
        // when setting the position, if the window doesn't exist it will also be created.
        public Rect position
        {
            get
            {
                EditorWindow window = FindFirst();
                if (window == null)
                {
                    return new Rect(0, 0, 0, 0);
                }
                return window.position;
            }
            set
            {
                EditorWindow window = FindFirstOrCreate();
                if (window != null)
                {
                    window.position = value;
                }
            }
        }
        // shortcut for deciding if any windows of this type are open,
        // or for opening/closing windows
        public bool isOpen
        {
            get
            {
                return FindAll().Length != 0;
            }
            set
            {
                if (value)
                {
                    FindFirstOrCreate();
                }
                else
                {
                    foreach (EditorWindow window in FindAll())
                    {
                        window.Close();
                    }
                }
            }
        }
    }

    class WindowInfos
    {
        // note: some of this data might need to change a little between different versions of Unity
        public WindowInfo scene = new WindowInfo("UnityEditor.SceneView", "Scene", "Window/Scene");
        public WindowInfo game = new WindowInfo("UnityEditor.GameView", "Game", "Window/Game");
    }
}

#endif
