using UnityEngine;
using System.Collections;

namespace HVR
{
    [AddComponentMenu("8i/HvrActorAnimationSync")]
    public class HvrActorAnimationSync : MonoBehaviour
    {
        public HvrActor hvrActor;
        public Animation targetAnimation;
        public float offset = 0;

        void Start()
        {
            targetAnimation.Stop();
        }

        void Update()
        {
            if (hvrActor == null || hvrActor.assetInterface == null || targetAnimation == null)
                return;

            if (!targetAnimation.isActiveAndEnabled)
                return;

            if (targetAnimation.clip == null)
            {
                print("Please attach animation clip");
                return;
            }

            if (hvrActor.assetInterface.IsPlaying())
            {
                if (!targetAnimation.isPlaying)
                    targetAnimation.Play();
            }
            else
            {
                if (targetAnimation.isPlaying)
                    targetAnimation.Stop();
            }

            float targetTime = hvrActor.assetInterface.GetCurrentTime() - offset;
            float delta = targetTime - targetAnimation[targetAnimation.clip.name].time;

            if (targetTime >= 0 && targetTime < targetAnimation.clip.length)
            {
                if (Mathf.Abs(delta) > 0.2f)
                {
                    targetAnimation[targetAnimation.clip.name].time = targetTime;
                }
                else
                {
                    targetAnimation[targetAnimation.clip.name].speed = (delta + 1.0f) * Time.timeScale;
                }
            }
            else
            {
                targetAnimation.Stop();
            }

        }
    }
}
