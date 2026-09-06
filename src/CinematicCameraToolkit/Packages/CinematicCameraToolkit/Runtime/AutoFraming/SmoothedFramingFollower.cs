using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace CinematicCameraToolkit.AutoFraming
{
    [AddComponentMenu("Cinematic Camera Toolkit/Smoothed Framing Follower")]
    public sealed class SmoothedFramingFollower : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker TickMarker =
            new(ProfilerCategory.Scripts, "CCT.AutoFraming.SmoothedFramingFollower.Tick");
#endif

        public enum UpdateTiming
        {
            Update,
            LateUpdate,
            FixedUpdate
        }

        [SerializeField] private UpdateTiming _updateTiming = UpdateTiming.LateUpdate;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _referencePoint;
        [SerializeField] private List<Renderer> _targets = new();
        [SerializeField] [Range(0f, 100f)] private float _marginLeft = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginRight = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginBottom = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginTop = 10f;
        [SerializeField] private FramingAxisAlignment _horizontalAlignment = FramingAxisAlignment.Balanced;
        [SerializeField] private FramingAxisAlignment _verticalAlignment = FramingAxisAlignment.Balanced;

        [Header("Smoothing")]
        [SerializeField]
        private FramingSmoothingSettings _smoothingSettings = FramingSmoothingSettings.CreateDefault();

        [Header("Preset")]
        [SerializeField] private FramingSmoothingPreset _preset = FramingSmoothingPreset.Standard;

        [SerializeField] [HideInInspector]
        private FramingSmoothingPreset _appliedPreset = FramingSmoothingPreset.Standard;

        private SmoothedFramingSolver _solver;
        private SmoothedFramingResult _lastResult;
        private bool _hasLastResult;

        public UpdateTiming Timing
        {
            get => _updateTiming;
            set => _updateTiming = value;
        }

        public Camera Camera
        {
            get => _camera;
            set => _camera = value;
        }

        public Transform ReferencePoint
        {
            get => _referencePoint;
            set => _referencePoint = value;
        }

        public RenderTargetMargin CurrentMargin =>
            RenderTargetMargin.Percentage(_marginLeft, _marginRight, _marginBottom, _marginTop);

        public void SetMargins(float left, float right, float bottom, float top)
        {
            (_marginLeft, _marginRight) = RenderTargetMarginLimits.ClampPair(left, right);
            (_marginBottom, _marginTop) = RenderTargetMarginLimits.ClampPair(bottom, top);
        }

        public float MarginLeft
        {
            get => _marginLeft;
            set => _marginLeft = RenderTargetMarginLimits.ClampPercentage(value, _marginRight);
        }

        public float MarginRight
        {
            get => _marginRight;
            set => _marginRight = RenderTargetMarginLimits.ClampPercentage(value, _marginLeft);
        }

        public float MarginBottom
        {
            get => _marginBottom;
            set => _marginBottom = RenderTargetMarginLimits.ClampPercentage(value, _marginTop);
        }

        public float MarginTop
        {
            get => _marginTop;
            set => _marginTop = RenderTargetMarginLimits.ClampPercentage(value, _marginBottom);
        }

        public FramingAxisAlignment HorizontalAlignment
        {
            get => _horizontalAlignment;
            set => _horizontalAlignment = value;
        }

        public FramingAxisAlignment VerticalAlignment
        {
            get => _verticalAlignment;
            set => _verticalAlignment = value;
        }

        public FramingSmoothingSettings Settings
        {
            get => _smoothingSettings;
            set => _smoothingSettings = value;
        }

        public FramingSmoothingPreset Preset
        {
            get => _preset;
            set => ApplyPreset(value);
        }

        public bool SmoothingEnabled
        {
            get => _smoothingSettings.Enabled;
            set => _smoothingSettings.Enabled = value;
        }

        public IReadOnlyList<Renderer> Targets => _targets;

        public void SetTargets(IReadOnlyList<Renderer> targets)
        {
            if (!ReferenceEquals(targets, _targets))
            {
                _targets.Clear();
                if (targets != null)
                    for (var i = 0; i < targets.Count; i++)
                        _targets.Add(targets[i]);
            }

            ResetSmoothing();
        }

        public void ResetSmoothing()
        {
            _solver?.Reset();
            _hasLastResult = false;
        }

        public bool TryGetLastResult(out SmoothedFramingResult result)
        {
            result = _lastResult;
            return _hasLastResult;
        }

        public void ApplyPreset(FramingSmoothingPreset preset)
        {
            _smoothingSettings = FramingSmoothingPresetCatalog.Get(preset);
            _preset = preset;
            _appliedPreset = preset;
        }

        private void Awake()
        {
            EnsureSmoothingSettingsInitialized();
            _solver = new SmoothedFramingSolver { Settings = _smoothingSettings };
        }

        private void ClampMargins()
        {
            (_marginLeft, _marginRight) = RenderTargetMarginLimits.ClampPair(_marginLeft, _marginRight);
            (_marginBottom, _marginTop) = RenderTargetMarginLimits.ClampPair(_marginBottom, _marginTop);
        }

        private void OnValidate()
        {
            // The Inspector writes the fields directly, so the setters never run.
            ClampMargins();

            EnsureSmoothingSettingsInitialized();
            // Only apply preset values when the dropdown actually changed.
            if (_preset != _appliedPreset) ApplyPreset(_preset);
        }

        private void OnDestroy()
        {
            _solver?.Dispose();
            _solver = null;
        }

        private void Update()
        {
            if (_updateTiming == UpdateTiming.Update) Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_updateTiming == UpdateTiming.LateUpdate) Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (_updateTiming == UpdateTiming.FixedUpdate) Tick(Time.fixedDeltaTime);
        }

        private void Tick(float dt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var profilerScope = TickMarker.Auto();
#endif
            if (!TryPrepareTick(out var margin)) return;

            _solver.Settings = _smoothingSettings;
            var alignment = new FramingAlignment(_horizontalAlignment, _verticalAlignment);

            _lastResult = _solver.Evaluate(dt, _targets, _referencePoint.position, _camera, margin, alignment);
            _hasLastResult = true;
            _camera.transform.position = _lastResult.SmoothedCameraPosition;
        }

        private bool TryPrepareTick(out RenderTargetMargin margin)
        {
            margin = default;
            if (_solver == null) return false;
            if (_camera == null || _referencePoint == null) return false;
            if (_targets == null || _targets.Count == 0) return false;
            if (_camera.pixelWidth <= 0 || _camera.pixelHeight <= 0) return false;

            margin = CurrentMargin;
            return true;
        }

        private void EnsureSmoothingSettingsInitialized()
        {
            var defaultSettings = FramingSmoothingSettings.CreateDefault();
            if (_smoothingSettings.Equals(default))
            {
                _smoothingSettings = defaultSettings;
                return;
            }

            if (_smoothingSettings.DerivativeCutoff <= 0f)
                _smoothingSettings.DerivativeCutoff = defaultSettings.DerivativeCutoff;

            if (_smoothingSettings.PositionMinCutoff <= 0f)
            {
                _smoothingSettings.PositionMinCutoff = defaultSettings.PositionMinCutoff;
                _smoothingSettings.PositionBeta = defaultSettings.PositionBeta;
            }
        }
    }
}