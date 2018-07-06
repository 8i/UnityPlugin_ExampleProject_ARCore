using System;
using System.Collections.Generic;
using UnityEngine;

namespace HVR
{
    public class HvrDataReference : ScriptableObject
    {
        [Serializable]
        public struct Data
        {
            public string guid;
        }

        public List<Data> data = new List<Data>();
    }
}
