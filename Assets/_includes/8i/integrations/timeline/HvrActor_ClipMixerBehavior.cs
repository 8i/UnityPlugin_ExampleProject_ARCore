// Timeline appears to be missing in Linux versions of Unity 2017.4
#if UNITY_2017_1_OR_NEWER && !(UNITY_2017_4 && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX))

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HVR.Timeline
{
    [System.Serializable]
    public class HvrActor_ClipMixerBehavior : PlayableBehaviour
    {
        private IEnumerable<TimelineClip> m_Clips;
        private PlayableDirector m_Director;

        private HvrActor m_trackBindingActor;
        private HVR.Interface.AssetInterface m_originalAssetInterface;
        private bool m_firstFrameHappened = false;

        private string m_originalActorRenderMethodType;

        internal PlayableDirector director
        {
            get { return m_Director; }
            set { m_Director = value; }
        }

        internal IEnumerable<TimelineClip> clips
        {
            get { return m_Clips; }
            set { m_Clips = value; }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (m_Clips == null)
                return;

            HvrActor actor = playerData as HvrActor;

            if (actor == null)
                return;

            // Allow for the previous asset that was assigned to the HvrActor to 
            // be set back when this graph ends
            if (actor != null &&
                !m_firstFrameHappened)
            {
                m_firstFrameHappened = true;
                m_trackBindingActor = actor;
                m_originalAssetInterface = actor.assetInterface;
                m_originalActorRenderMethodType = actor.renderMethodType;
            }

            Interface.AssetInterface current_asset = null;
            float greatestWeight = 0;

            int inputPort = 0;

            foreach (TimelineClip clip in m_Clips)
            {
                float inputWeight = playable.GetInputWeight(inputPort);
                ScriptPlayable<HvrActor_AssetBehavior> scriptPlayable = (ScriptPlayable<HvrActor_AssetBehavior>)playable.GetInput(inputPort);
                HvrActor_AssetBehavior hvrAssetBehavior = scriptPlayable.GetBehaviour();

                if (hvrAssetBehavior != null)
                {
                    double preloadTime = Math.Max(0.0, hvrAssetBehavior.preloadTime);

                    if (m_Director.time >= clip.start + clip.duration ||
                        m_Director.time <= clip.start - preloadTime)
                    {
                        hvrAssetBehavior.StopAsset();
                    }
                    else if (m_Director.time > clip.start - preloadTime)
                    {
                        hvrAssetBehavior.PrepareAsset();
                    }

                    // If this behavior is within the range of the clip, then take it's asset and
                    // overwrite the activeAsset in order to ensure that only one asset is assigned per 
                    // evaluation to the target HvrActor
                    // this assumes that the order of clips remain in a consistent order between frames
                    if (m_Director.time >= clip.start &&
                        m_Director.time <= clip.end)
                    {
                        if (inputWeight >= greatestWeight)
                        {
                            greatestWeight = inputWeight;
                            current_asset = hvrAssetBehavior.asset;
                        }
                    }
                }

                ++inputPort;
            }

            if (!string.IsNullOrEmpty(m_originalActorRenderMethodType) &&
                m_originalActorRenderMethodType != actor.renderMethodType)
            {
                // TODO Remove this warning once this bug is fixed - Tom
                Debug.LogError("HvrActor RenderMethod changed while a Timeline Clip was playing. This behavior is unsupported");
                return;
            }

            if (actor.actorInterface != null &&
                actor.assetInterface != current_asset)
                actor.SetAssetInterface(current_asset);
        }

        public override void OnGraphStop(Playable playable)
        {
            m_firstFrameHappened = false;

            if (m_trackBindingActor != null &&
                m_originalAssetInterface != null)
            {
                m_trackBindingActor.SetAssetInterface(m_originalAssetInterface);
            }
        }
    }
}

#endif