using System;
using CinematicCameraToolkit.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CinematicCameraToolkit.Timeline
{
    /// <summary>
    /// Drives the framing parameters of one <see cref="CinemachineAutoFraming"/> over time.
    /// Bind it to the extension, then place <see cref="AutoFramingClip"/>s on it;
    /// overlapping clips cross-fade the margins.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(AutoFramingClip))]
    [TrackBindingType(typeof(CinemachineAutoFraming))]
    [TrackColor(0.16f, 0.55f, 0.83f)]
    public class AutoFramingTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<AutoFramingMixer>.Create(graph, inputCount);
        }

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            // Lets the editor restore the component after a preview instead of leaving the
            // scrubbed values baked in.
            if (director != null && director.GetGenericBinding(this) is CinemachineAutoFraming framing)
                driver.AddFromComponent(framing.gameObject, framing);

            base.GatherProperties(director, driver);
        }
    }
}
