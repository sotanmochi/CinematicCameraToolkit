using CinematicCameraToolkit.AutoFraming;
using CinematicCameraToolkit.Cinemachine;
using UnityEngine.Playables;

namespace CinematicCameraToolkit.Timeline
{
    /// <summary>
    /// Blends the active clips and writes the result to the bound extension. Weighted values are
    /// re-normalised so a clip's values hold exactly even where its weight fades at the ends.
    /// Outside every clip the component's own values are restored.
    /// </summary>
    public sealed class AutoFramingMixer : PlayableBehaviour
    {
        private CinemachineAutoFraming _bound;
        private AutoFramingClipData _defaults;
        private bool _driving;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (playerData is not CinemachineAutoFraming framing) return;

            if (!ReferenceEquals(framing, _bound))
            {
                _bound = framing;
                _defaults = Capture(framing);
                _driving = false;
            }

            var inputCount = playable.GetInputCount();
            var totalWeight = 0f;
            float left = 0f, right = 0f, bottom = 0f, top = 0f;
            var smoothing = default(FramingSmoothingSettings);
            var dominantWeight = 0f;
            var blended = default(AutoFramingClipData);

            for (var i = 0; i < inputCount; i++)
            {
                var weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;

                var input = (ScriptPlayable<AutoFramingBehaviour>)playable.GetInput(i);
                var data = input.GetBehaviour().Data;

                totalWeight += weight;
                left += data.MarginLeft * weight;
                right += data.MarginRight * weight;
                bottom += data.MarginBottom * weight;
                top += data.MarginTop * weight;
                AccumulateSmoothing(ref smoothing, data.Smoothing, weight);

                // Discrete values cannot be blended, so the heaviest clip supplies them.
                if (weight > dominantWeight)
                {
                    dominantWeight = weight;
                    blended = data;
                }
            }

            if (totalWeight <= 0f)
            {
                Restore();
                return;
            }

            var inverse = 1f / totalWeight;
            blended.MarginLeft = left * inverse;
            blended.MarginRight = right * inverse;
            blended.MarginBottom = bottom * inverse;
            blended.MarginTop = top * inverse;

            var enabled = blended.Smoothing.Enabled;
            blended.Smoothing = Scale(smoothing, inverse);
            blended.Smoothing.Enabled = enabled;

            Apply(framing, blended);
            _driving = true;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            Restore();
            _bound = null;
        }

        private void Restore()
        {
            if (!_driving || _bound == null) return;

            Apply(_bound, _defaults);
            _driving = false;
        }

        private static void Apply(CinemachineAutoFraming framing, in AutoFramingClipData data)
        {
            // Both edges of an axis at once: the single-edge setters clamp against the opposing
            // edge's current value, which is stale mid-blend.
            framing.SetMargins(data.MarginLeft, data.MarginRight, data.MarginBottom, data.MarginTop);

            framing.HorizontalAlignment = data.HorizontalAlignment;
            framing.VerticalAlignment = data.VerticalAlignment;
            framing.Settings = data.Smoothing;
        }

        private static void AccumulateSmoothing(
            ref FramingSmoothingSettings total, in FramingSmoothingSettings value, float weight)
        {
            total.HorizontalMinCutoff += value.HorizontalMinCutoff * weight;
            total.VerticalMinCutoff += value.VerticalMinCutoff * weight;
            total.HorizontalBeta += value.HorizontalBeta * weight;
            total.VerticalBeta += value.VerticalBeta * weight;
            total.PositionMinCutoff += value.PositionMinCutoff * weight;
            total.PositionBeta += value.PositionBeta * weight;
            total.DerivativeCutoff += value.DerivativeCutoff * weight;
        }

        private static FramingSmoothingSettings Scale(FramingSmoothingSettings value, float scale)
        {
            value.HorizontalMinCutoff *= scale;
            value.VerticalMinCutoff *= scale;
            value.HorizontalBeta *= scale;
            value.VerticalBeta *= scale;
            value.PositionMinCutoff *= scale;
            value.PositionBeta *= scale;
            value.DerivativeCutoff *= scale;
            return value;
        }

        private static AutoFramingClipData Capture(CinemachineAutoFraming framing)
        {
            return new AutoFramingClipData
            {
                MarginLeft = framing.MarginLeft,
                MarginRight = framing.MarginRight,
                MarginBottom = framing.MarginBottom,
                MarginTop = framing.MarginTop,
                HorizontalAlignment = framing.HorizontalAlignment,
                VerticalAlignment = framing.VerticalAlignment,
                Smoothing = framing.Settings,
            };
        }
    }
}
