using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HVR.Editor
{
    public class PluginReferenceController : ScriptableObject
    {
        [Serializable]
        public struct PluginReference
        {
            public string guid;
            public bool supportEditor;
            public bool platformNone;
            public bool platformWindows;
            public bool platformMac;
            public bool platformLinux;
            public bool platformAndroid;
            public bool platformiOS;
            public string versionFilter;
        }

        public List<PluginReference> references;
    }
}
