using UnityEngine;

namespace CinematicCameraToolkit
{
    /// <summary>
    /// Smooths the inputs of the auto-framing camera solution: the reference point position
    /// and the four framing edge constraints, each with its own <see cref="OneEuroFilter"/>.
    ///
    /// Horizontal (left/right) and vertical (bottom/top) axes can have independent One-Euro
    /// parameters, allowing e.g. faster horizontal tracking with looser vertical follow.
    /// </summary>
    public sealed class FramingSmoother
    {
        private readonly OneEuroFilter _leftFilter = new();
        private readonly OneEuroFilter _rightFilter = new();
        private readonly OneEuroFilter _bottomFilter = new();
        private readonly OneEuroFilter _topFilter = new();
        private readonly OneEuroFilter _positionXFilter = new();
        private readonly OneEuroFilter _positionYFilter = new();
        private readonly OneEuroFilter _positionZFilter = new();

        private FramingSmoothingSettings _settings = FramingSmoothingSettings.CreateDefault();

        public FramingSmoothingSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public bool Enabled
        {
            get => _settings.Enabled;
            set => _settings.Enabled = value;
        }

        public void Reset()
        {
            _leftFilter.Reset();
            _rightFilter.Reset();
            _bottomFilter.Reset();
            _topFilter.Reset();
            _positionXFilter.Reset();
            _positionYFilter.Reset();
            _positionZFilter.Reset();
        }

        /// <summary>
        /// Apply One-Euro filtering to the reference point position. The camera follows the
        /// reference point 1:1, so any jitter on it (hand-held subject, mocap root) passes
        /// straight to the camera unless filtered here.
        /// </summary>
        /// <param name="rawReferencePoint">Reference point position of the current frame.</param>
        /// <param name="deltaTime">Time elapsed since the previous sample, in seconds. Must be &gt; 0.</param>
        public Vector3 SmoothReferencePoint(Vector3 rawReferencePoint, float deltaTime)
        {
            if (!_settings.Enabled)
            {
                Reset();
                return rawReferencePoint;
            }

            ApplySettingsToFilters();
            return new Vector3(
                _positionXFilter.Filter(rawReferencePoint.x, deltaTime),
                _positionYFilter.Filter(rawReferencePoint.y, deltaTime),
                _positionZFilter.Filter(rawReferencePoint.z, deltaTime));
        }

        /// <summary>
        /// Apply One-Euro filtering to raw framing edge constraints.
        /// </summary>
        /// <param name="rawFramingEdgeConstraints">Framing edge constraints computed from the current frame.</param>
        /// <param name="deltaTime">Time elapsed since the previous sample, in seconds. Must be &gt; 0.</param>
        public FramingEdgeConstraints SmoothFramingEdgeConstraints(FramingEdgeConstraints rawFramingEdgeConstraints,
            float deltaTime)
        {
            if (!_settings.Enabled)
            {
                Reset();
                return rawFramingEdgeConstraints;
            }

            ApplySettingsToFilters();
            return new FramingEdgeConstraints(
                _leftFilter.Filter(rawFramingEdgeConstraints.Left, deltaTime),
                _rightFilter.Filter(rawFramingEdgeConstraints.Right, deltaTime),
                _bottomFilter.Filter(rawFramingEdgeConstraints.Bottom, deltaTime),
                _topFilter.Filter(rawFramingEdgeConstraints.Top, deltaTime));
        }

        private void ApplySettingsToFilters()
        {
            _leftFilter.MinCutoff = _settings.HorizontalMinCutoff;
            _leftFilter.Beta = _settings.HorizontalBeta;
            _leftFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _rightFilter.MinCutoff = _settings.HorizontalMinCutoff;
            _rightFilter.Beta = _settings.HorizontalBeta;
            _rightFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _bottomFilter.MinCutoff = _settings.VerticalMinCutoff;
            _bottomFilter.Beta = _settings.VerticalBeta;
            _bottomFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _topFilter.MinCutoff = _settings.VerticalMinCutoff;
            _topFilter.Beta = _settings.VerticalBeta;
            _topFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _positionXFilter.MinCutoff = _settings.PositionMinCutoff;
            _positionXFilter.Beta = _settings.PositionBeta;
            _positionXFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _positionYFilter.MinCutoff = _settings.PositionMinCutoff;
            _positionYFilter.Beta = _settings.PositionBeta;
            _positionYFilter.DerivativeCutoff = _settings.DerivativeCutoff;
            _positionZFilter.MinCutoff = _settings.PositionMinCutoff;
            _positionZFilter.Beta = _settings.PositionBeta;
            _positionZFilter.DerivativeCutoff = _settings.DerivativeCutoff;
        }
    }
}