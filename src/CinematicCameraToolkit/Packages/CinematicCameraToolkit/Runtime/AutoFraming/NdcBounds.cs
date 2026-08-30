using System;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct NdcBounds : IEquatable<NdcBounds>
    {
        /// <summary>
        /// Minimum span required between opposite edges.
        /// </summary>
        public const float MinSpan = 1e-4f;

        /// <summary>
        /// Left edge of the target framing rectangle in NDC ([-1, 1]).
        /// </summary>
        public readonly float Left;

        /// <summary>
        /// Right edge of the target framing rectangle in NDC ([-1, 1]).
        /// </summary>
        public readonly float Right;

        /// <summary>
        /// Bottom edge of the target framing rectangle in NDC ([-1, 1]).
        /// </summary>
        public readonly float Bottom;

        /// <summary>
        /// Top edge of the target framing rectangle in NDC ([-1, 1]).
        /// </summary>
        public readonly float Top;

        public NdcBounds(float left, float right, float bottom, float top)
        {
            if (right - left < MinSpan)
                throw new ArgumentException(
                    $"right - left must be at least {MinSpan} but was {right - left} (left: {left}, right: {right}).");

            if (top - bottom < MinSpan)
                throw new ArgumentException(
                    $"top - bottom must be at least {MinSpan} but was {top - bottom} (bottom: {bottom}, top: {top}).");

            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public static NdcBounds From(int renderWidth, int renderHeight, RenderTargetMargin margin)
        {
            var (left, right, bottom, top) = margin.ToNdcBounds(renderWidth, renderHeight);
            return new NdcBounds(left, right, bottom, top);
        }

        public bool Equals(NdcBounds other)
        {
            return Left == other.Left && Right == other.Right && Bottom == other.Bottom && Top == other.Top;
        }

        public override bool Equals(object obj)
        {
            return obj is NdcBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Right, Bottom, Top);
        }
    }
}