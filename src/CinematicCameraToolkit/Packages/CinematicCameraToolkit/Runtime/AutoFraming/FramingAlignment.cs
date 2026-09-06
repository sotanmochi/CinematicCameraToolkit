using System;

namespace CinematicCameraToolkit
{
    public readonly struct FramingAlignment : IEquatable<FramingAlignment>
    {
        public readonly FramingAxisAlignment Horizontal;
        public readonly FramingAxisAlignment Vertical;

        public FramingAlignment(FramingAxisAlignment horizontal, FramingAxisAlignment vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        public bool Equals(FramingAlignment other)
        {
            return Horizontal == other.Horizontal && Vertical == other.Vertical;
        }

        public override bool Equals(object obj)
        {
            return obj is FramingAlignment alignment && Equals(alignment);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Horizontal, Vertical);
        }
    }

    public enum FramingAxisAlignment
    {
        Balanced = 0,
        AnchorToMinEdge = 1,
        AnchorToMaxEdge = 2,
    }
}