using UnityEngine;

namespace HVR
{
    // Attach this component to a gameobject that has a box collider set to 'trigger'
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("8i/HvrActorTrigger")]
    public class HvrActorTrigger : MonoBehaviour
    {
        public HvrActor actor;

        void OnTriggerEnter(Collider other)
        {
            if (actor != null &&
                actor.assetInterface != null)
            {
                actor.assetInterface.Play();
            }
        }
    }
}
