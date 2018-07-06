// Timeline appears to be missing in Linux versions of Unity 2017.4
#if UNITY_2017_1_OR_NEWER && !(UNITY_2017_4 && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX))

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HVR.Timeline
{
    [TrackColor(0.8f, 0.3f, 0.4f)]
    [TrackClipType(typeof(HvrActor_PlayableAsset))]
    [TrackBindingType(typeof(HvrActor))]
    public class HvrActor_AssetTrack : TrackAsset
    {
        #region TrackAsset overrides

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            PlayableDirector playableDirector = go.GetComponent<PlayableDirector>();

            ScriptPlayable<HvrActor_ClipMixerBehavior> playable = ScriptPlayable<HvrActor_ClipMixerBehavior>.Create(graph, inputCount);

            HvrActor_ClipMixerBehavior hvrAssetClipMixerBehavior = playable.GetBehaviour();

            if (hvrAssetClipMixerBehavior != null)
            {
                hvrAssetClipMixerBehavior.director = playableDirector;
                hvrAssetClipMixerBehavior.clips = GetClips();
            }

            return playable;
        }

        #endregion
    }
}

#endif