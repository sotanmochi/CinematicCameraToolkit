using System;
using UnityEngine;

namespace CinematicCameraToolkit.Timeline
{
    /// <summary>
    /// One clip's worth of framing intent. Margins and the smoothing values are blended between
    /// overlapping clips; alignment and the smoothing switch are discrete, so the clip with the
    /// greater weight wins instead.
    /// </summary>
    [Serializable]
    public struct AutoFramingClipData
    {
        [Range(0f, 100f)] public float MarginLeft;
        [Range(0f, 100f)] public float MarginRight;
        [Range(0f, 100f)] public float MarginBottom;
        [Range(0f, 100f)] public float MarginTop;

        public FramingAxisAlignment HorizontalAlignment;
        public FramingAxisAlignment VerticalAlignment;

        public FramingSmoothingSettings Smoothing;

        public static AutoFramingClipData CreateDefault()
        {
            return new AutoFramingClipData
            {
                MarginLeft = 10f,
                MarginRight = 10f,
                MarginBottom = 10f,
                MarginTop = 10f,
                HorizontalAlignment = FramingAxisAlignment.Balanced,
                VerticalAlignment = FramingAxisAlignment.Balanced,
                Smoothing = FramingSmoothingSettings.CreateDefault(),
            };
        }

        public static AutoFramingClipData FromSmoothingPreset(FramingSmoothingPreset preset)
        {
            var data = CreateDefault();
            data.ApplySmoothingPreset(preset);
            return data;
        }

        /// <summary>
        /// Overwrites the smoothing values with a preset's. The preset itself is not remembered:
        /// a clip drives values, so that they stay editable and blendable afterwards.
        /// </summary>
        public void ApplySmoothingPreset(FramingSmoothingPreset preset)
        {
            Smoothing = FramingSmoothingPresetCatalog.Get(preset);
        }

        public void ClampMargins()
        {
            (MarginLeft, MarginRight) = RenderTargetMarginLimits.ClampPair(MarginLeft, MarginRight);
            (MarginBottom, MarginTop) = RenderTargetMarginLimits.ClampPair(MarginBottom, MarginTop);
        }
    }
}
