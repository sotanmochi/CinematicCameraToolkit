using System;
using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct RenderTargetProperties : IEquatable<RenderTargetProperties>
    {
        /// <summary>
        /// The width of the render target in pixels.
        /// </summary>
        public readonly int PixelWidth;

        /// <summary>
        /// The height of the render target in pixels.
        /// </summary>
        public readonly int PixelHeight;

        /// <summary>
        /// Half-width of the view frustum at unit forward depth: <c>tan(horizontalFov / 2)</c>.
        /// </summary>
        /// <remarks>
        /// A point with camera-local coordinates <c>(r, u, f)</c> projects to horizontal
        /// NDC (Normalized Device Coordinates) as <c>nx = r / (f * KHorizontal)</c>.
        /// </remarks>
        public readonly float KHorizontal;

        /// <summary>
        /// Half-height of the view frustum at unit forward depth: <c>tan(verticalFov / 2)</c>.
        /// </summary>
        /// <remarks>
        /// A point with camera-local coordinates <c>(r, u, f)</c> projects to vertical
        /// NDC (Normalized Device Coordinates) as <c>ny = u / (f * KVertical)</c>.
        /// </remarks>
        public readonly float KVertical;

        public RenderTargetProperties(int pixelWidth, int pixelHeight, float kHorizontal, float kVertical)
        {
            if (pixelWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth,
                    "Pixel width must be positive.");

            if (pixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight,
                    "Pixel height must be positive.");

            if (!(kHorizontal > 0 && kHorizontal <= float.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(kHorizontal), kHorizontal,
                    "KHorizontal must be a finite positive number.");

            if (!(kVertical > 0 && kVertical <= float.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(kVertical), kVertical,
                    "KVertical must be a finite positive number.");

            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            KHorizontal = kHorizontal;
            KVertical = kVertical;
        }

        public static RenderTargetProperties From(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            var pixelWidth = camera.pixelWidth;
            var pixelHeight = camera.pixelHeight;

            return From(pixelWidth, pixelHeight, camera.fieldOfView, pixelWidth / (float)pixelHeight);
        }

        public static RenderTargetProperties From(
            int pixelWidth, int pixelHeight, float verticalFieldOfViewDegrees, float aspect)
        {
            var kVertical = Mathf.Tan(verticalFieldOfViewDegrees * Mathf.Deg2Rad / 2f);
            return new RenderTargetProperties(pixelWidth, pixelHeight, kVertical * aspect, kVertical);
        }

        public bool Equals(RenderTargetProperties other)
        {
            return PixelWidth == other.PixelWidth && PixelHeight == other.PixelHeight &&
                   KHorizontal == other.KHorizontal && KVertical == other.KVertical;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderTargetProperties other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PixelWidth, PixelHeight, KHorizontal, KVertical);
        }
    }
}