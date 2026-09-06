using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace CinematicCameraToolkit
{
    public sealed class AutoFramingSolver : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker CaptureSubjectMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.AutoFramingSolver.CaptureSubject");

        private static readonly ProfilerMarker CollectWorldPointsMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.AutoFramingSolver.CollectWorldPoints");

        private static readonly ProfilerMarker WorldToReferenceFrameMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.AutoFramingSolver.WorldToReferenceFrame");

        private static readonly ProfilerMarker ComputeEdgeConstraintsMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.AutoFramingSolver.ComputeEdgeConstraints");

        private static readonly ProfilerMarker SolveCameraPositionMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.AutoFramingSolver.SolveCameraPosition");
#endif

        private readonly FramingSubjectSnapshot _subjectSnapshot = new();
        private readonly IMeshPointCollector _meshPointCollector;
        private readonly bool _ownsPointCollector;

        private NativeArray<Vector3> _worldPointsBuffer;

        public AutoFramingSolver(IMeshPointCollector pointCollector = null)
        {
            _ownsPointCollector = pointCollector == null;
            _meshPointCollector = pointCollector ?? new ComputeShaderMeshPointCollector();
        }

        public void Dispose()
        {
            _subjectSnapshot.Dispose();
            if (_worldPointsBuffer.IsCreated) _worldPointsBuffer.Dispose();
            if (_ownsPointCollector) _meshPointCollector.Dispose();
        }

        public Vector3 ComputeCameraPosition(
            IReadOnlyList<Renderer> subjectRenderers,
            Vector3 referencePoint,
            Camera camera,
            RenderTargetMargin margin,
            FramingAlignment alignment)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            var cameraTransform = camera.transform;
            CaptureSubject(_subjectSnapshot, subjectRenderers, new ReferenceFrame(referencePoint,
                cameraTransform.right, cameraTransform.up, cameraTransform.forward));

            var renderTargetProperties = RenderTargetProperties.From(camera);
            var framingEdgeConstraints =
                ComputeFramingEdgeConstraints(_subjectSnapshot, renderTargetProperties, margin);

            return ComputeCameraPosition(
                _subjectSnapshot, framingEdgeConstraints, renderTargetProperties, margin, alignment);
        }

        public void CaptureSubject(
            FramingSubjectSnapshot output,
            IReadOnlyList<Renderer> subjectRenderers,
            in ReferenceFrame referenceFrame)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = CaptureSubjectMarker.Auto();
#endif
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (subjectRenderers == null) throw new ArgumentNullException(nameof(subjectRenderers));
            if (subjectRenderers.Count == 0)
                throw new ArgumentException("subjectRenderers must not be empty.", nameof(subjectRenderers));

            int written;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (CollectWorldPointsMarker.Auto())
#endif
            {
                var totalVertexCount = _meshPointCollector.GetTotalVertexCount(subjectRenderers);
                if (totalVertexCount == 0)
                    throw new InvalidOperationException(
                        "No supported renderers found in the input list (no vertices to frame).");

                EnsureWorldPointsBufferCapacity(totalVertexCount);
                written = _meshPointCollector.WriteWorldSpacePoints(subjectRenderers, _worldPointsBuffer);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (WorldToReferenceFrameMarker.Auto())
#endif
            {
                var localPoints = output.RentBuffer(written);
                for (var i = 0; i < written; i++)
                {
                    var d = _worldPointsBuffer[i] - referenceFrame.Origin;
                    localPoints[i] = new Vector3(
                        Vector3.Dot(d, referenceFrame.Right),
                        Vector3.Dot(d, referenceFrame.Up),
                        Vector3.Dot(d, referenceFrame.Forward));
                }

                output.Commit(written, referenceFrame);
            }
        }

        public FramingEdgeConstraints ComputeFramingEdgeConstraints(
            FramingSubjectSnapshot framingSubjectSnapshot,
            in RenderTargetProperties renderTargetProperties,
            RenderTargetMargin margin)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = ComputeEdgeConstraintsMarker.Auto();
#endif
            var ndcBounds = NdcBounds.From(
                renderTargetProperties.PixelWidth, renderTargetProperties.PixelHeight, margin);

            return ComputeFramingEdgeConstraints(framingSubjectSnapshot, ndcBounds,
                renderTargetProperties.KHorizontal, renderTargetProperties.KVertical);
        }

        public Vector3 ComputeCameraPosition(
            FramingSubjectSnapshot framingSubjectSnapshot,
            FramingEdgeConstraints framingEdgeConstraints,
            in RenderTargetProperties renderTargetProperties,
            RenderTargetMargin margin,
            FramingAlignment alignment)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = SolveCameraPositionMarker.Auto();
#endif
            if (framingSubjectSnapshot == null) throw new ArgumentNullException(nameof(framingSubjectSnapshot));
            if (framingSubjectSnapshot.PointCount == 0)
                throw new InvalidOperationException(
                    "FramingSubjectSnapshot has no points. Capture a non-empty subject first.");

            var ndcBounds = NdcBounds.From(
                renderTargetProperties.PixelWidth, renderTargetProperties.PixelHeight, margin);
            var localPositionOffsets = ComputeLocalPositionOffsets(framingEdgeConstraints, ndcBounds,
                renderTargetProperties.KHorizontal, renderTargetProperties.KVertical, alignment);

            return localPositionOffsets.ToWorldPosition(framingSubjectSnapshot.ReferenceFrame);
        }

        private void EnsureWorldPointsBufferCapacity(int requiredCapacity)
        {
            if (_worldPointsBuffer.IsCreated && _worldPointsBuffer.Length >= requiredCapacity) return;
            if (_worldPointsBuffer.IsCreated) _worldPointsBuffer.Dispose();
            _worldPointsBuffer = new NativeArray<Vector3>(requiredCapacity,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private static FramingEdgeConstraints ComputeFramingEdgeConstraints(FramingSubjectSnapshot snapshot,
            NdcBounds ndcBounds, float kHorizontal, float kVertical)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var points = snapshot.Points;
            if (points.Length == 0)
                throw new InvalidOperationException(
                    "FramingSubjectSnapshot has no points. Capture a non-empty subject first.");

            var nl = ndcBounds.Left;
            var nr = ndcBounds.Right;
            var nb = ndcBounds.Bottom;
            var nt = ndcBounds.Top;
            var kh = kHorizontal;
            var kv = kVertical;

            var minLeftConstraint = float.PositiveInfinity;
            var maxRightConstraint = float.NegativeInfinity;
            var minBottomConstraint = float.PositiveInfinity;
            var maxTopConstraint = float.NegativeInfinity;

            for (var i = 0; i < points.Length; i++)
            {
                var p = points[i];
                var r = p.x;
                var u = p.y;
                var f = p.z;

                var leftEdgeConstraint = r - nl * kh * f;
                var rightEdgeConstraint = r - nr * kh * f;
                var bottomEdgeConstraint = u - nb * kv * f;
                var topEdgeConstraint = u - nt * kv * f;

                if (leftEdgeConstraint < minLeftConstraint) minLeftConstraint = leftEdgeConstraint;
                if (rightEdgeConstraint > maxRightConstraint) maxRightConstraint = rightEdgeConstraint;
                if (bottomEdgeConstraint < minBottomConstraint) minBottomConstraint = bottomEdgeConstraint;
                if (topEdgeConstraint > maxTopConstraint) maxTopConstraint = topEdgeConstraint;
            }

            return new FramingEdgeConstraints(
                minLeftConstraint, maxRightConstraint, minBottomConstraint, maxTopConstraint);
        }

        private static LocalPositionOffsets ComputeLocalPositionOffsets(FramingEdgeConstraints framingEdgeConstraints,
            NdcBounds ndcBounds, float kHorizontal, float kVertical, FramingAlignment alignment)
        {
            var dHorizontal = ComputeFramingDistance(framingEdgeConstraints.Left, framingEdgeConstraints.Right,
                ndcBounds.Left, ndcBounds.Right, kHorizontal, alignment.Horizontal);

            var dVertical = ComputeFramingDistance(framingEdgeConstraints.Bottom, framingEdgeConstraints.Top,
                ndcBounds.Bottom, ndcBounds.Top, kVertical, alignment.Vertical);

            var framingDistance = Math.Max(dHorizontal, dVertical);

            var horizontalShift = ComputeShiftValue(framingDistance, alignment.Horizontal,
                framingEdgeConstraints.Left, framingEdgeConstraints.Right,
                ndcBounds.Left, ndcBounds.Right, kHorizontal);

            var verticalShift = ComputeShiftValue(framingDistance, alignment.Vertical,
                framingEdgeConstraints.Bottom, framingEdgeConstraints.Top,
                ndcBounds.Bottom, ndcBounds.Top, kVertical);

            return new LocalPositionOffsets(horizontalShift, verticalShift, framingDistance);
        }

        private static float ComputeFramingDistance(
            float edgeConstraintMin, float edgeConstraintMax, float ndcMin, float ndcMax, float projectionScale,
            FramingAxisAlignment alignment)
        {
            if (alignment == FramingAxisAlignment.CenterOnReferencePoint)
            {
                var ndcCenter = 0.5f * (ndcMin + ndcMax);
                return Math.Max(
                    edgeConstraintMin / ((ndcMin - ndcCenter) * projectionScale),
                    edgeConstraintMax / ((ndcMax - ndcCenter) * projectionScale));
            }

            return (edgeConstraintMax - edgeConstraintMin) / ((ndcMax - ndcMin) * projectionScale);
        }

        private static float ComputeShiftValue(float framingDistance, FramingAxisAlignment alignment,
            float edgeConstraintMin, float edgeConstraintMax, float ndcMin, float ndcMax, float projectionScale)
        {
            switch (alignment)
            {
                case FramingAxisAlignment.CenterBetweenEdgeAnchors:
                    var minEdgeShift = edgeConstraintMin - ndcMin * projectionScale * framingDistance;
                    var maxEdgeShift = edgeConstraintMax - ndcMax * projectionScale * framingDistance;
                    return 0.5f * (minEdgeShift + maxEdgeShift);

                case FramingAxisAlignment.AnchorToMinEdge:
                    return edgeConstraintMin - ndcMin * projectionScale * framingDistance;

                case FramingAxisAlignment.AnchorToMaxEdge:
                    return edgeConstraintMax - ndcMax * projectionScale * framingDistance;

                case FramingAxisAlignment.CenterOnReferencePoint:
                    var ndcCenter = 0.5f * (ndcMin + ndcMax);
                    return -ndcCenter * projectionScale * framingDistance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(alignment), alignment,
                        $"Undefined {nameof(FramingAxisAlignment)} value.");
            }
        }
    }
}
