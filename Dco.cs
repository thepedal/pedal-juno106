using System;

namespace PedalJuno106
{
    /// <summary>
    /// Single DCO per voice:
    ///   - Saw (PolyBLEP anti-aliased)
    ///   - Pulse with variable width (PolyBLEP at both edges)
    ///   - Sub-oscillator: square one octave below (own phase, PolyBLEP)
    ///   - White noise (LCG, fast)
    ///
    /// Saw and pulse share the main phase accumulator. The sub uses an
    /// independent half-rate accumulator so PolyBLEP edge correction works
    /// cleanly on its transitions.
    /// </summary>
    internal sealed class Dco
    {
        float _phase;       // main DCO phase 0..1
        float _subPhase;    // sub-osc phase 0..1, increments at half rate

        // LCG noise state — instance field per voice (Core §22: never static)
        uint _noiseSeed;

        public Dco()
        {
            // Initial seed varies a little per instance; not strictly necessary
            // but avoids identical noise correlation across voices.
            _noiseSeed = 0x12345678u + (uint)GetHashCode();
        }

        public void Reset()
        {
            _phase = 0f;
            _subPhase = 0f;
        }

        // ── PolyBLEP correction at a phase discontinuity ──────────────────────
        // t is the phase value [0..1]; dt is the per-sample increment.
        // Subtract from a downward step, add to an upward step.
        static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            else if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        /// <summary>
        /// Render one sample.
        ///   freqHz: oscillator frequency
        ///   pw: pulse width 0..1 (typically 0.5..0.95)
        ///   sawOn / pulseOn: enable flags from panel switches
        ///   subLevel / noiseLevel: 0..1 mix amounts
        ///   sr: current sample rate
        /// </summary>
        public float Process(float freqHz, float pw,
                             bool sawOn, bool pulseOn,
                             float subLevel, float noiseLevel,
                             float sr)
        {
            float dt = freqHz / sr;
            if (dt <= 0f)    dt = 1e-9f;
            if (dt >= 0.45f) dt = 0.45f;       // Nyquist guard

            // Advance main phase
            _phase += dt;
            if (_phase >= 1f) _phase -= 1f;

            // Advance sub phase at half rate (one octave below)
            _subPhase += dt * 0.5f;
            if (_subPhase >= 1f) _subPhase -= 1f;

            // ── Saw ───────────────────────────────────────────────────────────
            float saw = 0f;
            if (sawOn)
            {
                saw = 2f * _phase - 1f;
                saw -= PolyBlep(_phase, dt);
            }

            // ── Pulse ─────────────────────────────────────────────────────────
            float pulse = 0f;
            if (pulseOn)
            {
                if (pw < 0.05f) pw = 0.05f;
                if (pw > 0.95f) pw = 0.95f;
                pulse = (_phase < pw) ? 1f : -1f;
                pulse += PolyBlep(_phase, dt);
                float t2 = _phase - pw;
                if (t2 < 0f) t2 += 1f;
                pulse -= PolyBlep(t2, dt);
            }

            // ── Sub: square at half rate ──────────────────────────────────────
            // Treat as a 50% pulse on _subPhase.
            float sub = (_subPhase < 0.5f) ? 1f : -1f;
            float subDt = dt * 0.5f;
            sub += PolyBlep(_subPhase, subDt);
            float ts = _subPhase - 0.5f;
            if (ts < 0f) ts += 1f;
            sub -= PolyBlep(ts, subDt);

            // ── Noise (LCG, no allocation) ────────────────────────────────────
            _noiseSeed = _noiseSeed * 1664525u + 1013904223u;
            // Map to -1..1
            float noise = ((int)_noiseSeed) * (1f / 2147483648f);

            return saw + pulse + sub * subLevel + noise * noiseLevel;
        }
    }
}
