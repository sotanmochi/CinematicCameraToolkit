using System;
using Unity.Collections;
using UnityEngine;

namespace CinematicCameraToolkit
{
    public sealed class FramingSubjectSnapshot : IDisposable
    {
        private NativeArray<Vector3> _pointsBuffer;
        private NativeArray<Vector3> _points;
        private ReferenceFrame _referenceFrame;

        public int PointCount => _points.IsCreated ? _points.Length : 0;
        public NativeArray<Vector3> Points => _points;
        public ReferenceFrame ReferenceFrame => _referenceFrame;

        public void Dispose()
        {
            if (_pointsBuffer.IsCreated) _pointsBuffer.Dispose();
            _points = default;
        }

        internal NativeArray<Vector3> RentBuffer(int count)
        {
            EnsurePointsBufferCapacity(count);
            return _pointsBuffer.GetSubArray(0, count);
        }

        internal void Commit(int count, in ReferenceFrame frame)
        {
            _points = _pointsBuffer.GetSubArray(0, count);
            _referenceFrame = frame;
        }

        private void EnsurePointsBufferCapacity(int requiredCapacity)
        {
            if (_pointsBuffer.IsCreated && _pointsBuffer.Length >= requiredCapacity) return;
            if (_pointsBuffer.IsCreated) _pointsBuffer.Dispose();
            _pointsBuffer = new NativeArray<Vector3>(requiredCapacity,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _points = default; // Invalidate the slice; Commit recreates it after the new data is written.
        }
    }
}