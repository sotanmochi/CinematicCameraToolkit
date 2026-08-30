using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    public readonly struct SmoothedFramingResult
    {
        public readonly FramingEdgeConstraints RawFramingEdgeConstraints;
        public readonly FramingEdgeConstraints SmoothedFramingEdgeConstraints;
        public readonly Vector3 RawCameraPosition;
        public readonly Vector3 SmoothedCameraPosition;

        public SmoothedFramingResult(
            FramingEdgeConstraints rawFramingEdgeConstraints,
            FramingEdgeConstraints smoothedFramingEdgeConstraints,
            Vector3 rawCameraPosition,
            Vector3 smoothedCameraPosition)
        {
            RawFramingEdgeConstraints = rawFramingEdgeConstraints;
            SmoothedFramingEdgeConstraints = smoothedFramingEdgeConstraints;
            RawCameraPosition = rawCameraPosition;
            SmoothedCameraPosition = smoothedCameraPosition;
        }
    }
}