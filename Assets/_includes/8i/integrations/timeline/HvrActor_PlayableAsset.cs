// Timeline appears to be missing in Linux versions of Unity 2017.4
#if UNITY_2017_1_OR_NEWER && !(UNITY_2017_4 && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX))

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HVR.Timeline
{
    [System.Serializable]
    public class HvrActor_PlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        #region Serialized variables

        [SerializeField, NotKeyable]
        public string dataGUID;

        #endregion

        #region ITimelineClipAsset implementation

        public ClipCaps clipCaps { get { return ClipCaps.None; } }

        #endregion

        #region PlayableAsset overrides

        public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
        {
            ScriptPlayable<HvrActor_AssetBehavior> playable = ScriptPlayable<HvrActor_AssetBehavior>.Create(graph);

            HvrActor_AssetBehavior playableBehaviour = playable.GetBehaviour();

            playableBehaviour.dataGUID = dataGUID;

            return playable;
        }

        #endregion
    }
}

#endif