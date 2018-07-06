using UnityEngine;
using System.Collections;

public class Gizmo_Sphere : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Draw(false);
    }

    private void OnDrawGizmosSelected()
    {
        Draw(true);
    }

    void Draw(bool selected)
    {
        Color origCol = GUI.color;
        Gizmos.color = new Color(origCol.r, origCol.g, origCol.b, selected ? 0.5f : 0.2f);
        Gizmos.DrawWireSphere(transform.position, transform.lossyScale.x);
        Gizmos.color = origCol;
    }
#endif
}
