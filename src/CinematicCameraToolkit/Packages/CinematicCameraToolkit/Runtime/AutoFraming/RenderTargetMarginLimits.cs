using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    /// <summary>
    /// Keeps a percentage-based <see cref="RenderTargetMargin"/> usable: opposing edges must
    /// leave a non-empty framing rectangle between them. Apply these where values are set
    /// (component setters, <c>OnValidate</c>) so that no invalid margin is ever stored.
    /// </summary>
    public static class RenderTargetMarginLimits
    {
        public const float DefaultMinFramePercentage = 0.1f;

        private const float AbsoluteMinFramePercentage = 0.01f;

        /// <summary>
        /// Clamps one edge against a fixed opposing edge on the same axis.
        /// The edge being set is the one that yields; the opposing edge is left as is.
        /// </summary>
        public static float ClampPercentage(float value, float opposite,
            float minFramePercentage = DefaultMinFramePercentage)
        {
            var minFrame = Mathf.Clamp(minFramePercentage, AbsoluteMinFramePercentage, 100f);
            return Mathf.Clamp(value, 0f, Mathf.Max(0f, 100f - opposite - minFrame));
        }

        /// <summary>
        /// Clamps both edges of one axis. <paramref name="first"/> has priority; the second yields.
        /// </summary>
        public static (float first, float second) ClampPair(float first, float second,
            float minFramePercentage = DefaultMinFramePercentage)
        {
            first = ClampPercentage(first, 0f, minFramePercentage);
            second = ClampPercentage(second, first, minFramePercentage);
            return (first, second);
        }
    }
}
