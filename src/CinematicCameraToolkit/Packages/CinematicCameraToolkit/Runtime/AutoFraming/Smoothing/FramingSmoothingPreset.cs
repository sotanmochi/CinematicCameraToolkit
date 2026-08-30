using System;

namespace CinematicCameraToolkit.AutoFraming
{
    public enum FramingSmoothingPreset
    {
        /// <summary>
        /// Balanced default. Reasonable for general use.
        /// </summary>
        Standard,

        /// <summary>
        /// Cinematic. Heavy smoothing with slow follow — produces a "dolly on rails" feel
        /// where small subject movements are intentionally ignored.
        /// </summary>
        Cinematic,

        /// <summary>
        /// Documentary-style follow. Slightly slower and more forgiving of subject jitter,
        /// while still tracking large motion within ~half a second.
        /// </summary>
        Documentary,

        /// <summary>
        /// Maximally responsive. Higher cutoff and beta — minimal lag, only fine jitter is suppressed.
        /// Suited for sports / action capture where quick subject moves must be tracked tightly.
        /// </summary>
        Tight
    }

    public static class FramingSmoothingPresetCatalog
    {
        public static FramingSmoothingSettings Get(FramingSmoothingPreset preset)
        {
            switch (preset)
            {
                case FramingSmoothingPreset.Cinematic:
                    return CreateSettings(0.3f, 0.3f, 0.003f, 0.003f);
                case FramingSmoothingPreset.Documentary:
                    return CreateSettings(0.7f, 0.7f, 0.005f, 0.005f);
                case FramingSmoothingPreset.Tight:
                    return CreateSettings(2.0f, 2.0f, 0.02f, 0.02f);
                case FramingSmoothingPreset.Standard:
                default:
                    return CreateSettings(1.0f, 1.0f, 0.007f, 0.007f);
            }
        }

        private static FramingSmoothingSettings CreateSettings(
            float horizontalMinCutoff,
            float verticalMinCutoff,
            float horizontalBeta,
            float verticalBeta)
        {
            return new FramingSmoothingSettings
            {
                Enabled = true,
                HorizontalMinCutoff = horizontalMinCutoff,
                VerticalMinCutoff = verticalMinCutoff,
                HorizontalBeta = horizontalBeta,
                VerticalBeta = verticalBeta,
                // The reference point shares the tighter of the two axis cutoffs so the
                // camera's positional follow matches the preset's overall responsiveness.
                PositionMinCutoff = Math.Max(horizontalMinCutoff, verticalMinCutoff),
                PositionBeta = Math.Max(horizontalBeta, verticalBeta),
                DerivativeCutoff = 1f
            };
        }
    }
}