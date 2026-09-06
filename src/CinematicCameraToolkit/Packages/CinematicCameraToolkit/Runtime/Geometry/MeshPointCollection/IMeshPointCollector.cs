using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace CinematicCameraToolkit
{
    public interface IMeshPointCollector : IDisposable
    {
        int GetTotalVertexCount(IReadOnlyList<Renderer> renderers);
        int WriteWorldSpacePoints(IReadOnlyList<Renderer> renderers, NativeArray<Vector3> output);
    }
}
