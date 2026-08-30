using System;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct FramingEdgeConstraints : IEquatable<FramingEdgeConstraints>
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        public FramingEdgeConstraints(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public bool Equals(FramingEdgeConstraints other)
        {
            return Left == other.Left && Right == other.Right && Bottom == other.Bottom && Top == other.Top;
        }

        public override bool Equals(object obj)
        {
            return obj is FramingEdgeConstraints other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Right, Bottom, Top);
        }
    }
}