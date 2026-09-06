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
        /// <summary>
        /// Centers the camera shift between the two edge-anchored positions.
        /// </summary>
        CenterBetweenEdgeAnchors = 0,

        /// <summary>
        /// Anchors the subject's minimum edge to the framing bounds' minimum edge.
        /// </summary>
        AnchorToMinEdge = 1,

        /// <summary>
        /// Anchors the subject's maximum edge to the framing bounds' maximum edge.
        /// </summary>
        AnchorToMaxEdge = 2,

        /// <summary>
        /// Keeps the reference point at the center of the framing bounds on this axis.
        /// </summary>
        CenterOnReferencePoint = 3,
    }
}
