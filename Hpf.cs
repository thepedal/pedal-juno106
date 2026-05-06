using System;

namespace PedalJuno106
{
    /// <summary>
    /// Global 4-position highpass on the pre-chorus mono bus.
    ///   Position 0: bypass
    ///   Position 1: ~100 Hz
    ///   Position 2: ~200 Hz
    ///   Position 3: ~400 Hz
    ///
    /// One-pole HPF implemented as input − lowpass-tracker.
    /// Configure(position) is called once per Work() block; ProcessMono(input)
    /// is the per-sample hot path and just runs the filter math.
    /// </summary>
    internal sealed class Hpf
    {
        // Approximate Juno HPF positions
        static readonly float[] _cutoffs = { 0f, 100f, 200f, 400f };

        float _z;
        float _coef;
        float _sr = 44100f;
        int   _lastPosition = -1;
        bool  _isOn;

        public void SetSampleRate(float sr)
        {
            _sr = sr;
            _lastPosition = -1;     // force recompute on next Configure
        }

        public void Reset()
        {
            _z = 0f;
            _lastPosition = -1;
        }

        /// <summary>
        /// Set cutoff position. Cheap when position hasn't changed.
        /// Call once per Work() block, before the per-sample ProcessMono loop.
        /// </summary>
        public void Configure(int position)
        {
            if (position == _lastPosition) return;
            _lastPosition = position;

            if (position <= 0 || position >= _cutoffs.Length)
            {
                _isOn = false;
                _z = 0f;            // clear state when bypassed
                return;
            }
            _isOn = true;
            float fc = _cutoffs[position];
            // Lowpass-tracker α = 1 − exp(−2π·fc/sr). HPF output = input − lp.
            _coef = 1f - MathF.Exp(-2f * MathF.PI * fc / _sr);
        }

        /// <summary>
        /// Process one mono sample using the configured position.
        /// Returns input unchanged when bypassed.
        /// </summary>
        public float ProcessMono(float input)
        {
            if (!_isOn) return input;
            _z += _coef * (input - _z);
            return input - _z;
        }
    }
}
