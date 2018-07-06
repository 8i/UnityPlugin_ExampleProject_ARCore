// Timeline appears to be missing in Linux versions of Unity 2017.4
#if UNITY_2017_1_OR_NEWER && !(UNITY_2017_4 && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX))

using System;
using UnityEngine;
using UnityEngine.Playables;

namespace HVR.Timeline
{
    [Serializable]
    public class HvrActor_AssetBehavior : PlayableBehaviour
    {
        public string dataGUID;
        public HVR.Interface.AssetInterface asset;

        public double preloadTime = 1.0;

        private const float m_maxTimeDelta = 0.1f;

        public void PrepareAsset()
        {
            if (!string.IsNullOrEmpty(dataGUID) &&
                asset == null)
            {
                string path = HvrHelper.GetDataPathFromGUID(dataGUID);

                asset = new Interface.AssetInterface();
                asset.Create(path);
            }
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (asset == null)
                return;

            asset.Seek((float)(playable.GetTime()));

            if (playable.GetGraph().IsPlaying())
                PlayAsset();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (asset == null)
                return;

            if (Application.isPlaying)
                PauseAsset();
            else
                StopAsset();
        }

        public override void OnGraphStop(Playable playable)
        {
            if (asset == null)
                return;

            StopAsset();
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (asset == null)
                return;

            // Ensure whether the current state should require the
            // asset to be playing.
            if (!asset.IsPlaying())
            {
                if (Application.isPlaying &&
                    playable.GetGraph().IsPlaying())
                {
                    PlayAsset();
                }
            }

            float playableTime = (float)playable.GetTime();
            float delta = playableTime - asset.GetCurrentTime();

            if (Math.Abs(delta) > m_maxTimeDelta ||
                !playable.GetGraph().IsPlaying())
            {
                if (playableTime > 0 &&
                    playableTime < asset.GetDuration())
                    asset.Seek(playableTime);
            }
        }

        public void PlayAsset()
        {
            if (asset == null)
                return;

            if (!asset.IsPlaying())
                asset.Play();

            if (!Application.isPlaying)
                PauseAsset();
        }

        public void PauseAsset()
        {
            if (asset == null)
                return;

            if (asset.IsPlaying())
                asset.Pause();
        }

        public void StopAsset()
        {
            if (asset == null)
                return;

            if (asset.IsPlaying())
                asset.Stop();

            asset.Delete();
            asset = null;
        }
    }
}

#endif