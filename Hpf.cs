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
    /// Coefficient cached and recomputed only on position change.
    /// </summary>
    internal sealed class Hpf
    {
        // Approximate Juno HPF positions
        static readonly float[] _cutoffs = { 0f, 100f, 200f, 400f };

        float _z;
        float _coef;
        float _sr = 44100f;
        int   _lastPosition = -1;

        public void SetSampleRate(float sr)
        {
            _sr = sr;
            _lastPosition = -1;   // force recompute
        }

        public void Reset()
        {
            _z = 0f;
            _lastPosition = -1;
        }

        void UpdateCoefficient(int position)
        {
            if (position == _lastPosition) return;
            _lastPosition = position;
            if (position <= 0)
            {
                _coef = 0f;
                _z = 0f;
                return;
            }
            float fc = _cutoffs[position];
            // Lowpass-tracking coefficient: alpha = 1 - exp(-2π·fc/sr)
            _coef = 1f - MathF.Exp(-2f * MathF.PI * fc / _sr);
        }

        /// <summary>
        /// Process one mono sample.
        /// position: 0..3 (panel parameter). 0 = bypass.
        /// </summary>
        public float ProcessMono(int position, float input)
        {
            if (position != _lastPosition) UpdateCoefficient(position);
            if (position == 0) return input;
            _z += _coef * (input - _z);
            return input - _z;
        }
    }
}
