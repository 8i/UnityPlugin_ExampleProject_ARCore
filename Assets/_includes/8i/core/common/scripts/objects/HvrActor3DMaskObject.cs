using UnityEngine;
using System.Collections;

namespace HVR
{
    [AddComponentMenu("8i/HvrActor3DMaskObject")]
    public class HvrActor3DMaskObject : MonoBehaviour
    {
        public enum eType
        {
            sphere,
            box
        };

        public eType type;

        public bool additive;

        private Color add = new Color(0.2f, 0.2f, 1.0f, 0.6f);
        private Color sub = new Color(1.0f, 0.2f, 0.2f, 0.6f);

        void OnDrawGizmos()
        {
            Color origColro = Gizmos.color;

            Gizmos.color = additive ? add : sub;

            if (type == eType.sphere)
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
            }

            if (type == eType.box)
            {
                Matrix4x4 origMatrix = Gizmos.matrix;

                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

                Gizmos.matrix = origMatrix;
            }

            Gizmos.color = origColro;
        }
    }
}