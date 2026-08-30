using System;
using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    /// <summary>
    /// Serialized smoothing configuration shared by presets, components, and runtime filters.
    /// </summary>
    [Serializable]
    public struct FramingSmoothingSettings : IEquatable<FramingSmoothingSettings>
    {
        [Tooltip("Master switch. When off, smoothing is bypassed.")]
        public bool Enabled;

        [Min(0f)]
        public float HorizontalMinCutoff;

        [Min(0f)]
        public float VerticalMinCutoff;

        [Min(0f)]
        public float HorizontalBeta;

        [Min(0f)]
        public float VerticalBeta;

        [Min(0f), Tooltip("One-Euro minimum cutoff (Hz) applied to the reference point position. The camera follows the reference point 1:1, so jitter on it must be filtered here.")]
        public float PositionMinCutoff;

        [Min(0f)]
        public float PositionBeta;

        [HideInInspector]
        public float DerivativeCutoff;

        public static FramingSmoothingSettings CreateDefault()
        {
            return new FramingSmoothingSettings
            {
                Enabled = true,
                HorizontalMinCutoff = 1f,
                VerticalMinCutoff = 1f,
                HorizontalBeta = 0.007f,
                VerticalBeta = 0.007f,
                PositionMinCutoff = 1f,
                PositionBeta = 0.007f,
                DerivativeCutoff = 1f,
            };
        }

        public bool Equals(FramingSmoothingSettings other) =>
            Enabled == other.Enabled &&
            HorizontalMinCutoff == other.HorizontalMinCutoff &&
            VerticalMinCutoff == other.VerticalMinCutoff &&
            HorizontalBeta == other.HorizontalBeta &&
            VerticalBeta == other.VerticalBeta &&
            PositionMinCutoff == other.PositionMinCutoff &&
            PositionBeta == other.PositionBeta &&
            DerivativeCutoff == other.DerivativeCutoff;

        public override bool Equals(object obj) => obj is FramingSmoothingSettings other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Enabled,
            HorizontalMinCutoff,
            VerticalMinCutoff,
            HorizontalBeta,
            VerticalBeta,
            HashCode.Combine(PositionMinCutoff, PositionBeta, DerivativeCutoff));
    }
}
