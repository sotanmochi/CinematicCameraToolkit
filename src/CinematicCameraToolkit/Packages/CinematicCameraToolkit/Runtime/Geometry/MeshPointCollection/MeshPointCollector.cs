using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace CinematicCameraToolkit
{
    public sealed class MeshPointCollector : IMeshPointCollector
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker WriteWorldSpacePointsMarker =
            new(ProfilerCategory.Scripts, "CCT.Geometry.MeshPointCollector.WriteWorldSpacePoints");

        private static readonly ProfilerMarker BakeSkinnedMeshMarker =
            new(ProfilerCategory.Scripts, "CCT.Geometry.MeshPointCollector.BakeSkinnedMesh");

        private static readonly ProfilerMarker ReadVerticesMarker =
            new(ProfilerCategory.Scripts, "CCT.Geometry.MeshPointCollector.ReadVertices");

        private static readonly ProfilerMarker TransformVerticesMarker =
            new(ProfilerCategory.Scripts, "CCT.Geometry.MeshPointCollector.TransformVertices");

        private static readonly ProfilerMarker ResizeBufferMarker =
            new(ProfilerCategory.Scripts, "CCT.Geometry.MeshPointCollector.ResizeBuffer");
#endif

        private NativeArray<Vector3> _localVertexBuffer;
        private Mesh _skinnedMesh;

        public void Dispose()
        {
            if (_localVertexBuffer.IsCreated) _localVertexBuffer.Dispose();
            if (_skinnedMesh != null)
            {
                UnityObjectDestroyer.DestroyRuntimeOrEditor(_skinnedMesh);
                _skinnedMesh = null;
            }
        }

        /// <inheritdoc />
        public int GetTotalVertexCount(IReadOnlyList<Renderer> renderers)
        {
            if (renderers == null) return 0;
            var total = 0;
            for (var i = 0; i < renderers.Count; i++) total += GetSourceVertexCount(renderers[i]);
            return total;
        }

        /// <inheritdoc />
        public int WriteWorldSpacePoints(IReadOnlyList<Renderer> renderers, NativeArray<Vector3> output)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = WriteWorldSpacePointsMarker.Auto();
#endif
            if (renderers == null) return 0;

            var writeIndex = 0;
            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                var mesh = GetCurrentMesh(renderer);
                if (mesh == null) continue;

                var vertexCount = mesh.vertexCount;
                if (vertexCount == 0) continue;

                EnsureLocalBufferCapacity(vertexCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (ReadVerticesMarker.Auto())
#endif
                using (var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var buffer = _localVertexBuffer.GetSubArray(0, vertexCount);
                    meshDataArray[0].GetVertices(buffer);
                }

                var localToWorld = renderer.transform.localToWorldMatrix;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (TransformVerticesMarker.Auto())
#endif
                {
                    for (var j = 0; j < vertexCount; j++)
                    {
                        output[writeIndex] = localToWorld.MultiplyPoint3x4(_localVertexBuffer[j]);
                        writeIndex++;
                    }
                }
            }

            return writeIndex;
        }

        private static int GetSourceVertexCount(Renderer renderer)
        {
            if (renderer is MeshRenderer meshRenderer)
            {
                var sharedMesh = meshRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                return sharedMesh != null ? sharedMesh.vertexCount : 0;
            }

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                var sharedMesh = skinnedMeshRenderer.sharedMesh;
                return sharedMesh != null ? sharedMesh.vertexCount : 0;
            }

            return 0;
        }

        private Mesh GetCurrentMesh(Renderer renderer)
        {
            if (renderer is MeshRenderer meshRenderer)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                return meshFilter != null ? meshFilter.sharedMesh : null;
            }

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (BakeSkinnedMeshMarker.Auto())
#endif
                {
                    if (_skinnedMesh == null)
                    {
                        _skinnedMesh = new Mesh { name = "MeshPointCollector.SkinnedMesh" };
                        _skinnedMesh.hideFlags = HideFlags.HideAndDontSave;
                        _skinnedMesh.MarkDynamic();
                    }

                    skinnedMeshRenderer.BakeMesh(_skinnedMesh, true);
                    return _skinnedMesh;
                }
            }

            return null;
        }

        private void EnsureLocalBufferCapacity(int requiredCapacity)
        {
            if (_localVertexBuffer.IsCreated && _localVertexBuffer.Length >= requiredCapacity) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = ResizeBufferMarker.Auto();
#endif
            if (_localVertexBuffer.IsCreated) _localVertexBuffer.Dispose();
            _localVertexBuffer = new NativeArray<Vector3>(requiredCapacity,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
    }
}
