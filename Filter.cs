using System;

namespace PedalJuno106
{
    /// <summary>
    /// TPT (Topology Preserving Transform) 4-pole ladder lowpass.
    /// Four cascaded one-pole TPT integrators with a single feedback path.
    /// Soft saturation in the feedback bounds self-oscillation cleanly at
    /// high resonance — the character we want for Juno-style behaviour.
    ///
    /// Cutoff coefficient G is recomputed at most every GUpdateInterval
    /// samples (and only when fc has actually moved by ≥0.5 Hz). At 16
    /// samples that's ~0.36 ms at 44.1 kHz / ~0.33 ms at 48 kHz — well
    /// below the timescale of any audible cutoff change, but skips ~94%
    /// of FastTan calls under per-sample env/LFO modulation.
    /// </summary>
    internal sealed class LadderFilter
    {
        const int GUpdateInterval = 16;

        float _z1, _z2, _z3, _z4;

        float _sr    = 44100f;
        float _invSr = 1f / 44100f;

        float _lastFc = -1f;
        float _G      = 0.5f;
        int   _gCounter;

        public void SetSampleRate(float sr)
        {
            _sr = sr;
            _invSr = 1f / sr;
            _lastFc   = -1f;
            _gCounter = 0;          // force recompute on next process
        }

        public void Reset()
        {
            _z1 = _z2 = _z3 = _z4 = 0f;
            _lastFc   = -1f;
            _gCounter = 0;
        }

        /// <summary>
        /// Process one sample.
        ///   cutoffHz: target cutoff in Hz (clamped to [20, 0.45·sr] internally
        ///             too, but caller is expected to keep it in range)
        ///   resonance01: 0..1 — internally scaled to k=0..4.5
        ///                (k≈4 is the unity-gain self-oscillation threshold;
        ///                 we go a touch higher so max-resonance self-oscillates
        ///                 audibly through the soft-clip)
        /// </summary>
        public float Process(float input, float cutoffHz, float resonance01)
        {
            // G recompute is decimated to every GUpdateInterval samples.
            // The cutoff clamp + FastTan only run on the recompute cycle.
            if (--_gCounter <= 0)
            {
                _gCounter = GUpdateInterval;

                // Defensive clamp; caller normally clamps upstream too.
                if (cutoffHz < 20f) cutoffHz = 20f;
                float maxFc = _sr * 0.45f;
                if (cutoffHz > maxFc) cutoffHz = maxFc;

                float diff = cutoffHz - _lastFc;
                if (diff < 0f) diff = -diff;
                if (diff > 0.5f)
                {
                    _lastFc = cutoffHz;
                    float wd = DspMath.FastTan(MathF.PI * cutoffHz * _invSr);
                    _G = wd / (1f + wd);
                }
            }

            float k = resonance01 * 4.5f;

            // Feedback with soft saturation — bounds self-oscillation amplitude
            float fb = k * _z4;
            float u  = DspMath.SoftClip(input - fb);

            // Four cascaded one-pole TPT lowpass stages (use cached _G)
            float v;
            v = (u  - _z1) * _G; float y1 = v + _z1; _z1 = y1 + v;
            v = (y1 - _z2) * _G; float y2 = v + _z2; _z2 = y2 + v;
            v = (y2 - _z3) * _G; float y3 = v + _z3; _z3 = y3 + v;
            v = (y3 - _z4) * _G; float y4 = v + _z4; _z4 = y4 + v;

            return y4;
        }
    }
}
