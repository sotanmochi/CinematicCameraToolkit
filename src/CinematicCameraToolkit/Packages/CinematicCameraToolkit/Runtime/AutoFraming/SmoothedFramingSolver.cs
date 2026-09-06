using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace CinematicCameraToolkit
{
    public sealed class SmoothedFramingSolver : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker EvaluateMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.SmoothedFramingSolver.Evaluate");
        private static readonly ProfilerMarker SmoothReferencePointMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.SmoothedFramingSolver.SmoothReferencePoint");
        private static readonly ProfilerMarker SmoothConstraintsMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.SmoothedFramingSolver.SmoothConstraints");
#endif

        private readonly AutoFramingSolver _solver;
        private readonly FramingSmoother _smoother;
        private readonly FramingSubjectSnapshot _snapshot = new();
        private readonly bool _ownsSolver;

        public FramingSmoothingSettings Settings
        {
            get => _smoother.Settings;
            set => _smoother.Settings = value;
        }

        public SmoothedFramingSolver(AutoFramingSolver solver = null, FramingSmoother smoother = null)
        {
            _smoother = smoother ?? new FramingSmoother();

            if (solver == null)
            {
                _solver = new AutoFramingSolver();
                _ownsSolver = true;
            }
            else
            {
                _solver = solver;
                _ownsSolver = false;
            }
        }

        public void Dispose()
        {
            _snapshot.Dispose();
            if (_ownsSolver) _solver.Dispose();
        }

        public void Reset()
        {
            _smoother.Reset();
        }

        public SmoothedFramingResult Evaluate(
            float deltaTime,
            IReadOnlyList<Renderer> subjectRenderers,
            Vector3 referencePoint,
            Camera camera,
            RenderTargetMargin margin,
            FramingAlignment alignment)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            return Evaluate(deltaTime, subjectRenderers, referencePoint, camera.transform.rotation,
                RenderTargetProperties.From(camera), margin, alignment);
        }

        public SmoothedFramingResult Evaluate(
            float deltaTime,
            IReadOnlyList<Renderer> subjectRenderers,
            Vector3 referencePoint,
            Quaternion cameraRotation,
            in RenderTargetProperties renderTargetProperties,
            RenderTargetMargin margin,
            FramingAlignment alignment)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = EvaluateMarker.Auto();
#endif
            // The reference point is smoothed first: the camera follows it 1:1,
            // so jitter on it would pass straight to the camera. The constraints are computed
            // relative to the smoothed point (the closed-form solution is reference-invariant,
            // so the raw recomposition below still yields the exact unsmoothed camera position).
            Vector3 smoothedReferencePoint;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (SmoothReferencePointMarker.Auto())
#endif
            {
                smoothedReferencePoint = _smoother.SmoothReferencePoint(referencePoint, deltaTime);
            }

            var referenceFrame = new ReferenceFrame(
                smoothedReferencePoint,
                cameraRotation * Vector3.right,
                cameraRotation * Vector3.up,
                cameraRotation * Vector3.forward);

            _solver.CaptureSubject(_snapshot, subjectRenderers, referenceFrame);

            var rawConstraints =
                _solver.ComputeFramingEdgeConstraints(_snapshot, renderTargetProperties, margin);
            FramingEdgeConstraints smoothedConstraints;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (SmoothConstraintsMarker.Auto())
#endif
            {
                smoothedConstraints = _smoother.SmoothFramingEdgeConstraints(rawConstraints, deltaTime);
            }

            var rawPosition = _solver.ComputeCameraPosition(
                _snapshot, rawConstraints, renderTargetProperties, margin, alignment);
            var smoothedPosition = _solver.ComputeCameraPosition(
                _snapshot, smoothedConstraints, renderTargetProperties, margin, alignment);

            return new SmoothedFramingResult(rawConstraints, smoothedConstraints, rawPosition, smoothedPosition);
        }
    }
}
