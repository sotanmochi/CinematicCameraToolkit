using System;
using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct ReferenceFrame : IEquatable<ReferenceFrame>
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly Vector3 Forward;

        public ReferenceFrame(Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
        {
            Origin = origin;
            Right = right;
            Up = up;
            Forward = forward;
        }

        public bool Equals(ReferenceFrame other)
        {
            return Origin == other.Origin && Right == other.Right && Up == other.Up && Forward == other.Forward;
        }

        public override bool Equals(object obj)
        {
            return obj is ReferenceFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Origin, Right, Up, Forward);
        }
    }
}