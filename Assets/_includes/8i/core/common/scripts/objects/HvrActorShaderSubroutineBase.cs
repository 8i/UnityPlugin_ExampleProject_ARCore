using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HVR
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(HvrActor))]
    public abstract class HvrActorShaderSubroutineBase : MonoBehaviour
    {
        public const string UNIQUE_ID_IDENTIFIER = "<ID>";

        [Header("Shader")]
        public TextAsset textAsset;

        public string id
        {
            get
            {
                if (string.IsNullOrEmpty(m_id))
                {
                    m_id = Helper.IntToLetters(UniqueIdRegistry.Register(this));
                }

                return m_id;
            }
        }

        public HvrActor hvrActor
        {
            get
            {
                if (m_hvrActor == null)
                    m_hvrActor = gameObject.GetComponent<HvrActor>();

                return m_hvrActor;
            }
        }

        private string m_id = string.Empty;
        private HvrActor m_hvrActor;
    }
}
