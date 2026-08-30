using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace CinematicCameraToolkit.AutoFraming
{
    /// <summary>
    /// Reads skinned vertex positions from the GPU.
    /// Static meshes and unsupported GPU paths are handled by <see cref="MeshPointCollector"/>.
    /// </summary>
    /// <remarks>
    /// Returns the latest completed GPU sample while the next asynchronous readback is in flight.
    /// The first sample and renderer-layout changes use the CPU fallback.
    /// </remarks>
    public sealed class ComputeShaderMeshPointCollector : IMeshPointCollector
    {
        private const string ComputeShaderResourcePath = "ComputeShaderMeshPointCollector";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker WriteWorldSpacePointsMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.WriteWorldSpacePoints");

        private static readonly ProfilerMarker CompleteReadbackMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.CompleteReadback");

        private static readonly ProfilerMarker GetGpuLayoutMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.GetGpuLayout");

        private static readonly ProfilerMarker ScheduleReadbackMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.ScheduleReadback");

        private static readonly ProfilerMarker DispatchMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.Dispatch");

        private static readonly ProfilerMarker RequestReadbackMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.RequestReadback");

        private static readonly ProfilerMarker CopyLatestPointsMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.CopyLatestPoints");

        private static readonly ProfilerMarker CpuFallbackMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.CpuFallback");

        private static readonly ProfilerMarker ResizeBufferMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.ComputeShaderMeshPointCollector.ResizeBuffer");
#endif

        public const string KernelName = "TransformVertexPositions";
        private const int VertexStride = sizeof(float) * 3;

        private static readonly int GpuVertexBufferId = Shader.PropertyToID("GpuVertexBuffer");
        private static readonly int OutputVertexBufferId = Shader.PropertyToID("OutputVertexBuffer");
        private static readonly int GpuVertexBufferStrideId = Shader.PropertyToID("GpuVertexBufferStride");
        private static readonly int VertexCountId = Shader.PropertyToID("VertexCount");
        private static readonly int OutputVertexOffsetId = Shader.PropertyToID("OutputVertexOffset");
        private static readonly int LocalToWorldId = Shader.PropertyToID("LocalToWorld");

        private readonly MeshPointCollector _cpuFallback = new();
        private readonly ComputeShader _computeShader;
        private int _kernelId;
        private uint _threadGroupSizeX;
        private readonly Renderer[] _singleRenderer = new Renderer[1];

        private ComputeBuffer _outputBuffer;
        private int _outputBufferCapacity;
        private NativeArray<Vector3> _readbackBuffer;
        private NativeArray<Vector3> _latestBuffer;
        private AsyncGPUReadbackRequest _readbackRequest;
        private bool _readbackPending;
        private int _requestedVertexCount;
        private ulong _requestedLayoutKey;
        private bool _hasLatest;
        private int _latestVertexCount;
        private ulong _latestLayoutKey;

        public bool UsesComputeShader { get; private set; }

        /// <summary>
        /// Creates a collector using the compute shader bundled with the package.
        /// Falls back to the CPU path if the asset is missing from the build.
        /// </summary>
        public ComputeShaderMeshPointCollector()
        {
            _computeShader = Resources.Load<ComputeShader>(ComputeShaderResourcePath);
            if (_computeShader == null)
            {
                Debug.LogWarning($"Compute shader '{ComputeShaderResourcePath}' was not found. " +
                                 "Skinned meshes fall back to CPU vertex collection.");
                return;
            }

            Initialize();
        }

        /// <summary>
        /// Creates a collector using, but not owning, <paramref name="computeShader"/>.
        /// </summary>
        public ComputeShaderMeshPointCollector(ComputeShader computeShader)
        {
            if (computeShader == null) throw new ArgumentNullException(nameof(computeShader));

            _computeShader = computeShader;
            Initialize();
        }

        private void Initialize()
        {
            UsesComputeShader = SystemInfo.supportsComputeShaders && SystemInfo.supportsAsyncGPUReadback;
            if (!UsesComputeShader) return;

            _kernelId = _computeShader.FindKernel(KernelName);
            _computeShader.GetKernelThreadGroupSizes(_kernelId, out _threadGroupSizeX, out _, out _);
        }

        public void Dispose()
        {
            if (_readbackPending && !_readbackRequest.done) _readbackRequest.WaitForCompletion();
            _readbackPending = false;

            _outputBuffer?.Release();
            _outputBuffer = null;
            _outputBufferCapacity = 0;

            if (_readbackBuffer.IsCreated) _readbackBuffer.Dispose();
            if (_latestBuffer.IsCreated) _latestBuffer.Dispose();
            _cpuFallback.Dispose();
        }

        /// <inheritdoc />
        public int GetTotalVertexCount(IReadOnlyList<Renderer> renderers)
        {
            return _cpuFallback.GetTotalVertexCount(renderers);
        }

        /// <inheritdoc />
        public int WriteWorldSpacePoints(IReadOnlyList<Renderer> renderers, NativeArray<Vector3> output)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = WriteWorldSpacePointsMarker.Auto();
#endif
            if (renderers == null) return 0;
            if (!UsesComputeShader) return _cpuFallback.WriteWorldSpacePoints(renderers, output);

            CompleteReadback();

            int gpuVertexCount;
            ulong layoutKey;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (GetGpuLayoutMarker.Auto())
#endif
            {
                GetGpuLayout(renderers, out gpuVertexCount, out layoutKey);
            }

            var gpuSamplingActive = _readbackPending;
            if (!gpuSamplingActive && gpuVertexCount > 0)
                gpuSamplingActive = TryScheduleReadback(renderers, gpuVertexCount, layoutKey);

            var useLatest = gpuSamplingActive && _hasLatest &&
                            _latestVertexCount == gpuVertexCount &&
                            _latestLayoutKey == layoutKey;
            var writeIndex = 0;
            var gpuReadIndex = 0;

            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                if (TryGetGpuRenderer(renderer, out _, out var vertexCount))
                {
                    if (useLatest)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        using var copyLatestPointsScope = CopyLatestPointsMarker.Auto();
#endif
                        NativeArray<Vector3>.Copy(_latestBuffer, gpuReadIndex, output, writeIndex, vertexCount);
                        writeIndex += vertexCount;
                    }
                    else
                    {
                        writeIndex += WriteWithCpuFallback(renderer, output, writeIndex);
                    }

                    gpuReadIndex += vertexCount;
                }
                else
                {
                    writeIndex += WriteWithCpuFallback(renderer, output, writeIndex);
                }
            }

            return writeIndex;
        }

        private void CompleteReadback()
        {
            if (!_readbackPending || !_readbackRequest.done) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = CompleteReadbackMarker.Auto();
#endif
            _readbackPending = false;
            if (_readbackRequest.hasError)
            {
                _hasLatest = false;
                return;
            }

            EnsureLatestBufferCapacity(_requestedVertexCount);
            NativeArray<Vector3>.Copy(_readbackBuffer, 0, _latestBuffer, 0, _requestedVertexCount);
            _latestVertexCount = _requestedVertexCount;
            _latestLayoutKey = _requestedLayoutKey;
            _hasLatest = true;
        }

        private bool TryScheduleReadback(IReadOnlyList<Renderer> renderers, int gpuVertexCount, ulong layoutKey)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = ScheduleReadbackMarker.Auto();
#endif
            EnsureReadbackBufferCapacity(gpuVertexCount);

            var outputOffset = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (DispatchMarker.Auto())
#endif
            {
                for (var i = 0; i < renderers.Count; i++)
                {
                    if (!TryGetGpuRenderer(renderers[i], out var renderer, out var vertexCount)) continue;

                    if ((renderer.vertexBufferTarget & GraphicsBuffer.Target.Raw) == 0)
                        renderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

                    var gpuVertexBuffer = renderer.GetVertexBuffer();
                    if (gpuVertexBuffer == null) return false;

                    try
                    {
                        _computeShader.SetBuffer(_kernelId, GpuVertexBufferId, gpuVertexBuffer);
                        _computeShader.SetBuffer(_kernelId, OutputVertexBufferId, _outputBuffer);
                        _computeShader.SetInt(GpuVertexBufferStrideId, gpuVertexBuffer.stride);
                        _computeShader.SetInt(VertexCountId, vertexCount);
                        _computeShader.SetInt(OutputVertexOffsetId, outputOffset);
                        _computeShader.SetMatrix(LocalToWorldId, renderer.rootBone.localToWorldMatrix);
                        _computeShader.Dispatch(_kernelId,
                            Mathf.CeilToInt(vertexCount / (float)_threadGroupSizeX), 1, 1);
                    }
                    finally
                    {
                        gpuVertexBuffer.Release();
                    }

                    outputOffset += vertexCount;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (RequestReadbackMarker.Auto())
#endif
            {
                _readbackRequest = AsyncGPUReadback.RequestIntoNativeArray(
                    ref _readbackBuffer, _outputBuffer, gpuVertexCount * VertexStride, 0);
            }

            _readbackPending = true;
            _requestedVertexCount = gpuVertexCount;
            _requestedLayoutKey = layoutKey;
            return true;
        }

        private int WriteWithCpuFallback(Renderer renderer, NativeArray<Vector3> output, int offset)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = CpuFallbackMarker.Auto();
#endif
            _singleRenderer[0] = renderer;
            return _cpuFallback.WriteWorldSpacePoints(
                _singleRenderer, output.GetSubArray(offset, output.Length - offset));
        }

        private void EnsureReadbackBufferCapacity(int requiredCapacity)
        {
            if (_outputBuffer != null && _outputBufferCapacity >= requiredCapacity) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = ResizeBufferMarker.Auto();
#endif

            _outputBuffer?.Release();
            _outputBuffer = new ComputeBuffer(requiredCapacity, VertexStride);
            _outputBufferCapacity = requiredCapacity;

            if (_readbackBuffer.IsCreated) _readbackBuffer.Dispose();
            _readbackBuffer = new NativeArray<Vector3>(requiredCapacity,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void EnsureLatestBufferCapacity(int requiredCapacity)
        {
            if (_latestBuffer.IsCreated && _latestBuffer.Length >= requiredCapacity) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = ResizeBufferMarker.Auto();
#endif

            if (_latestBuffer.IsCreated) _latestBuffer.Dispose();
            _latestBuffer = new NativeArray<Vector3>(requiredCapacity,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private static void GetGpuLayout(IReadOnlyList<Renderer> renderers, out int vertexCount, out ulong layoutKey)
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;

            vertexCount = 0;
            layoutKey = offsetBasis;

            for (var i = 0; i < renderers.Count; i++)
            {
                if (!TryGetGpuRenderer(renderers[i], out var renderer, out var rendererVertexCount)) continue;

                vertexCount += rendererVertexCount;
                layoutKey = (layoutKey ^ (uint)renderer.GetInstanceID()) * prime;
                layoutKey = (layoutKey ^ (uint)renderer.sharedMesh.GetInstanceID()) * prime;
                layoutKey = (layoutKey ^ (uint)renderer.rootBone.GetInstanceID()) * prime;
                layoutKey = (layoutKey ^ (uint)rendererVertexCount) * prime;
            }
        }

        private static bool TryGetGpuRenderer(
            Renderer renderer, out SkinnedMeshRenderer skinnedMeshRenderer, out int vertexCount)
        {
            skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
            var mesh = skinnedMeshRenderer != null ? skinnedMeshRenderer.sharedMesh : null;
            vertexCount = mesh != null ? mesh.vertexCount : 0;
            return vertexCount > 0 && skinnedMeshRenderer.rootBone != null;
        }
    }
}