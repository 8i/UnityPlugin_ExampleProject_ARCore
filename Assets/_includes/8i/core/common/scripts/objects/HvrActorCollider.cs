using UnityEngine;

namespace HVR
{
    [AddComponentMenu("8i/HvrActorCollider")]
    [ExecuteInEditMode]
    public class HvrActorCollider : MonoBehaviour
    {
        public HvrActor sourceHvrActor;
        public Collider targetCollider;

        void Update()
        {
            if (sourceHvrActor == null || targetCollider == null)
                return;

            if (sourceHvrActor.transform != targetCollider.transform)
            {
                // Set the new transform position and rotation
                targetCollider.transform.position = sourceHvrActor.transform.position;
                targetCollider.transform.rotation = sourceHvrActor.transform.rotation;
            }

            if (sourceHvrActor.assetInterface != null)
            {
                Bounds actorBounds = sourceHvrActor.assetInterface.GetBounds();

                if (targetCollider.GetType() == typeof(BoxCollider))
                {
                    BoxCollider bc = targetCollider as BoxCollider;
                    bc.center = actorBounds.center;
                    bc.size = actorBounds.size;
                }

                if (targetCollider.GetType() == typeof(CapsuleCollider))
                {
                    CapsuleCollider bc = targetCollider as CapsuleCollider;
                    bc.center = actorBounds.center;
                    bc.height = actorBounds.size.y;
                    bc.radius = Mathf.Max(actorBounds.size.x, actorBounds.size.z) / 2f;
                }

                if (targetCollider.GetType() == typeof(SphereCollider))
                {
                    SphereCollider bc = targetCollider as SphereCollider;
                    bc.center = actorBounds.center;
                    bc.radius = Mathf.Max(actorBounds.size.x, actorBounds.size.y, actorBounds.size.z) / 2f;
                }
            }
        }
    }
}
