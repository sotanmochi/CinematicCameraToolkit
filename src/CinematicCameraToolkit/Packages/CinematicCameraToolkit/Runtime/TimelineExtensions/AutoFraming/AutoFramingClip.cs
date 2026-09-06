using UnityEngine;
using UnityEngine.Playables;

namespace CinematicCameraToolkit.Timeline
{
    /// <summary>
    /// Timeline clip that drives a <see cref="CinemachineAutoFraming"/>'s framing parameters.
    /// Place it on a <see cref="AutoFramingTrack"/> bound to that extension.
    /// </summary>
    public sealed class AutoFramingClip : PlayableAsset
    {
        [SerializeField] private AutoFramingClipData _data = AutoFramingClipData.CreateDefault();

        public AutoFramingClipData Data
        {
            get => _data;
            set
            {
                _data = value;
                _data.ClampMargins();
            }
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<AutoFramingBehaviour>.Create(graph);
            playable.GetBehaviour().Data = _data;
            return playable;
        }

        private void OnValidate()
        {
            _data.ClampMargins();
        }
    }
}
