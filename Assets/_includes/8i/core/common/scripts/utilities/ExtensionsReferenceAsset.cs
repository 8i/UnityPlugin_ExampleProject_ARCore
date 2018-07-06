using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace HVR
{
    public class ExtensionsReferenceAsset : ScriptableObject
    {
        [Serializable]
        public struct Reference
        {
            public RuntimePlatform platform;
            public string guid;
        }

        public List<Reference> references = new List<Reference>();
    }
}
