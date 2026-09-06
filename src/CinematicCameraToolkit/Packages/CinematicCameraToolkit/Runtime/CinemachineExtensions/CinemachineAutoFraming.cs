using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace CinematicCameraToolkit.Cinemachine
{
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Cinematic Camera Toolkit/Cinemachine Auto Framing")]
    [ExecuteAlways]
    [SaveDuringPlay]
    [DisallowMultipleComponent]
    public sealed class CinemachineAutoFraming : CinemachineExtension
    {
        [SerializeField] private List<Renderer> _targets = new();

        [Header("Margins")]
        [SerializeField] [Range(0f, 100f)] private float _marginLeft = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginRight = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginBottom = 10f;
        [SerializeField] [Range(0f, 100f)] private float _marginTop = 10f;

        [Header("Alignment")]
        [SerializeField] private FramingAxisAlignment _horizontalAlignment = FramingAxisAlignment.CenterBetweenEdgeAnchors;
        [SerializeField] private FramingAxisAlignment _verticalAlignment = FramingAxisAlignment.CenterBetweenEdgeAnchors;

        [Header("Smoothing")]
        [SerializeField] private FramingSmoothingPreset _preset = FramingSmoothingPreset.Standard;

        [SerializeField] private FramingSmoothingSettings _smoothingSettings = FramingSmoothingSettings.CreateDefault();

        [SerializeField] [HideInInspector]
        private FramingSmoothingPreset _appliedPreset = FramingSmoothingPreset.Standard;

        private SmoothedFramingSolver _solver;
        private SmoothedFramingResult _lastResult;
        private bool _hasLastResult;

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

        private void ClampMargins()
        {
            (_marginLeft, _marginRight) = RenderTargetMarginLimits.ClampPair(_marginLeft, _marginRight);
            (_marginBottom, _marginTop) = RenderTargetMarginLimits.ClampPair(_marginBottom, _marginTop);
        }

        private void OnValidate()
        {
            // The Inspector writes the fields directly, so the setters never run.
            ClampMargins();

            // Only apply preset values when the dropdown actually changed.
            if (_preset != _appliedPreset) ApplyPreset(_preset);
        }

        protected override void OnDestroy()
        {
            _solver?.Dispose();
            _solver = null;
            base.OnDestroy();
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != ResolveStage(vcam)) return;

            if (_targets.Count == 0) return;
            if (state.Lens.Orthographic) return;
            if (!state.HasLookAt()) return;

            _solver ??= new SmoothedFramingSolver();
            _solver.Settings = _smoothingSettings;

            if (deltaTime < 0f || !vcam.PreviousStateIsValid) _solver.Reset();

            var position = state.GetCorrectedPosition();
            var result = _solver.Evaluate(
                deltaTime, _targets, state.ReferenceLookAt, state.GetFinalOrientation(),
                RenderTargetProperties.From(1920, 1080, state.Lens.FieldOfView, state.Lens.Aspect),
                RenderTargetMargin.Percentage(_marginLeft, _marginRight, _marginBottom, _marginTop),
                new FramingAlignment(_horizontalAlignment, _verticalAlignment));

            _lastResult = result;
            _hasLastResult = true;

            state.PositionCorrection += result.SmoothedCameraPosition - position;
        }

        private static CinemachineCore.Stage ResolveStage(CinemachineVirtualCameraBase vcam)
        {
            var body = vcam.GetCinemachineComponent(CinemachineCore.Stage.Body);
            return body != null && body.BodyAppliesAfterAim
                ? CinemachineCore.Stage.Body
                : CinemachineCore.Stage.Aim;
        }
    }
}
