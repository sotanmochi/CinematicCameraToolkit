using UnityEngine;

namespace CinematicCameraToolkit.AutoFraming
{
    /// <summary>
    /// 1D adaptive low-pass filter (1€ Filter; Casiez, Roussel, &amp; Vogel, CHI 2012).
    ///
    /// Cutoff frequency adapts to the input's instantaneous speed: low cutoff at rest
    /// (heavy smoothing — micro-jitter is suppressed) and higher cutoff during fast
    /// motion (light smoothing — minimal lag). This trade-off cannot be made by a
    /// fixed-cutoff low-pass filter, which always picks one or the other.
    ///
    /// Reference implementation: <see href="http://cristal.univ-lille.fr/~casiez/1euro/"/>
    /// </summary>
    /// <remarks>
    /// Tuning guide:
    ///   - <see cref="MinCutoff"/>: lower → smoother at rest. Start at 1.0 Hz; drop to ~0.5 Hz
    ///     if idle jitter is still visible.
    ///   - <see cref="Beta"/>: higher → tighter tracking during fast motion. Start at 0.007;
    ///     raise to ~0.02 if fast moves feel laggy.
    ///   - <see cref="DerivativeCutoff"/>: low-pass cutoff applied to the speed estimate.
    ///     1.0 Hz is the canonical default and rarely needs changing.
    ///
    /// State retention: the filter persists the previous sample and speed estimate across
    /// calls. Call <see cref="Reset"/> when the underlying signal jumps discontinuously
    /// (target switch, teleport) so the speed estimate doesn't carry over the artifact.
    /// </remarks>
    public sealed class OneEuroFilter
    {
        public float MinCutoff;
        public float Beta;
        public float DerivativeCutoff;

        private float _xPrev;
        private float _dxPrev;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public float LastValue => _xPrev;

        public OneEuroFilter(float minCutoff = 1f, float beta = 0.007f, float derivativeCutoff = 1f)
        {
            MinCutoff = minCutoff;
            Beta = beta;
            DerivativeCutoff = derivativeCutoff;
        }

        public void Reset()
        {
            _initialized = false;
            _xPrev = 0f;
            _dxPrev = 0f;
        }

        /// <summary>
        /// Apply the filter to a new sample. The first call after construction or
        /// <see cref="Reset"/> initializes internal state and returns the input unchanged.
        /// </summary>
        /// <param name="value">New raw input sample.</param>
        /// <param name="dt">Time elapsed since the previous sample, in seconds. Must be &gt; 0.</param>
        /// <returns>The filtered output value.</returns>
        public float Filter(float value, float dt)
        {
            if (!(dt > 0f)) return _initialized ? _xPrev : value;

            if (!_initialized)
            {
                _xPrev = value;
                _dxPrev = 0f;
                _initialized = true;
                return value;
            }

            var dValue = (value - _xPrev) / dt;
            var aD = ComputeAlpha(DerivativeCutoff, dt);
            var edValue = aD * dValue + (1f - aD) * _dxPrev;

            var cutoff = MinCutoff + Beta * Mathf.Abs(edValue);
            var a = ComputeAlpha(cutoff, dt);
            var xFiltered = a * value + (1f - a) * _xPrev;

            _xPrev = xFiltered;
            _dxPrev = edValue;
            return xFiltered;
        }

        // dt-stable exponential form of the discrete-time low-pass coefficient,
        // equivalent to a zero-order-hold step of a continuous first-order LP at the
        // given cutoff. Robust under variable frame intervals — preferred over the
        // Euler form `1 / (1 + 1/(2π·cutoff·dt))` cited in the original paper.
        private static float ComputeAlpha(float cutoff, float dt)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);
        }
    }
}