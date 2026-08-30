using System;

namespace CinematicCameraToolkit.AutoFraming
{
    public enum RenderTargetMarginUnit
    {
        Percentage,
        Pixels
    }

    public readonly struct RenderTargetMargin : IEquatable<RenderTargetMargin>
    {
        public readonly RenderTargetMarginUnit Unit;
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        public RenderTargetMargin(float left, float right, float bottom, float top, RenderTargetMarginUnit unit)
        {
            ValidateMargin(left, nameof(left));
            ValidateMargin(right, nameof(right));
            ValidateMargin(bottom, nameof(bottom));
            ValidateMargin(top, nameof(top));

            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            Unit = unit;
        }

        public static RenderTargetMargin Percentage(float left, float right, float bottom, float top)
        {
            return new RenderTargetMargin(left, right, bottom, top, RenderTargetMarginUnit.Percentage);
        }

        public static RenderTargetMargin Pixels(float left, float right, float bottom, float top)
        {
            return new RenderTargetMargin(left, right, bottom, top, RenderTargetMarginUnit.Pixels);
        }

        /// <summary>
        /// Converts the margin to NDC (Normalized Device Coordinates) bounds.
        /// </summary>
        /// <param name="renderPixelWidth">Width of the render target in pixels. Must be positive.</param>
        /// <param name="renderPixelHeight">Height of the render target in pixels. Must be positive.</param>
        /// <returns>The framing rectangle edges in NDC ([-1, 1]).</returns>
        public (float nLeft, float nRight, float nBottom, float nTop) ToNdcBounds(int renderPixelWidth,
            int renderPixelHeight)
        {
            if (renderPixelWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(renderPixelWidth), renderPixelWidth,
                    "Render target width must be positive.");

            if (renderPixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(renderPixelHeight), renderPixelHeight,
                    "Render target height must be positive.");

            float ml, mr, mb, mt;
            switch (Unit)
            {
                case RenderTargetMarginUnit.Percentage:
                    ml = Left * 0.01f * renderPixelWidth;
                    mr = Right * 0.01f * renderPixelWidth;
                    mb = Bottom * 0.01f * renderPixelHeight;
                    mt = Top * 0.01f * renderPixelHeight;
                    break;
                case RenderTargetMarginUnit.Pixels:
                    ml = Left;
                    mr = Right;
                    mb = Bottom;
                    mt = Top;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported {nameof(RenderTargetMarginUnit)}: {Unit}");
            }

            if (ml + mr >= renderPixelWidth)
                throw new InvalidOperationException(
                    $"Horizontal margins ({ml} + {mr} px) must be smaller than the render target width ({renderPixelWidth} px).");
            if (mb + mt >= renderPixelHeight)
                throw new InvalidOperationException(
                    $"Vertical margins ({mb} + {mt} px) must be smaller than the render target height ({renderPixelHeight} px).");

            return (
                nLeft: 2f * ml / renderPixelWidth - 1f,
                nRight: 1f - 2f * mr / renderPixelWidth,
                nBottom: 2f * mb / renderPixelHeight - 1f,
                nTop: 1f - 2f * mt / renderPixelHeight
            );
        }

        public bool Equals(RenderTargetMargin other)
        {
            return Left == other.Left && Right == other.Right && Bottom == other.Bottom && Top == other.Top &&
                   Unit == other.Unit;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderTargetMargin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Right, Bottom, Top, Unit);
        }

        private static void ValidateMargin(float value, string paramName)
        {
            if (!(value >= 0f && value <= float.MaxValue))
                throw new ArgumentOutOfRangeException(paramName, value,
                    "Margin value must be a finite non-negative number.");
        }
    }
}