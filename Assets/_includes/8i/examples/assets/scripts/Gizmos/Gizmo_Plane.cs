using UnityEngine;
using System.Collections;

public class Gizmo_Plane : MonoBehaviour
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
        Matrix4x4 origMatrix = Gizmos.matrix;
        Color origCol = GUI.color;

        // Set Gizmos.matrix here in order for the transform to affect Gizmos.DrawCube below
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);

        Gizmos.color = new Color(origCol.r, origCol.g, origCol.b, selected ? 0.5f : 0.2f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 0, 1));

        Gizmos.matrix = origMatrix;
        Gizmos.color = origCol;
    }
#endif
}
