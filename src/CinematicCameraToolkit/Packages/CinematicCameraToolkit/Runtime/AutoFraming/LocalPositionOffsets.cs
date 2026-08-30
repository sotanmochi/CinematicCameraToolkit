using System;
using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct LocalPositionOffsets : IEquatable<LocalPositionOffsets>
    {
        public readonly float HorizontalShift;
        public readonly float VerticalShift;
        public readonly float DepthShift;

        public LocalPositionOffsets(float horizontalShift, float verticalShift, float depthShift)
        {
            HorizontalShift = horizontalShift;
            VerticalShift = verticalShift;
            DepthShift = depthShift;
        }

        public Vector3 ToWorldPosition(ReferenceFrame referenceFrame)
        {
            return referenceFrame.Origin
                   + referenceFrame.Right * HorizontalShift
                   + referenceFrame.Up * VerticalShift
                   - referenceFrame.Forward * DepthShift;
        }

        public bool Equals(LocalPositionOffsets other)
        {
            return HorizontalShift == other.HorizontalShift && VerticalShift == other.VerticalShift &&
                   DepthShift == other.DepthShift;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalPositionOffsets other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HorizontalShift, VerticalShift, DepthShift);
        }
    }
}