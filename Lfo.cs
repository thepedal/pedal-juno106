using System;

namespace PedalJuno106
{
    /// <summary>
    /// Single global LFO (Juno-faithful — one LFO modulates everything).
    /// Triangle waveform with a delay-fade-in envelope that re-arms whenever
    /// any voice triggers a new note.
    ///
    /// Output property is the LFO value scaled by the fade-in level, range [-1, 1].
    /// Process() is called once per sample from the main work loop.
    /// </summary>
    internal sealed class Lfo
    {
        float _phase;        // 0..1
        float _delayLevel;   // 0..1, fade-in envelope level
        float _sr = 44100f;
        float _invSr = 1f / 44100f;

        // Cache for rate → increment conversion (avoid MathF.Pow per sample)
        int   _cachedRate = -1;
        float _cachedInc;

        public float Output { get; private set; }   // -1..1 after delay scaling

        public void SetSampleRate(float sr)
        {
            _sr = sr;
            _invSr = 1f / sr;
            _cachedRate = -1;   // force recompute
        }

        public void Reset()
        {
            _phase = 0f;
            _delayLevel = 1f;   // start fully on; TriggerDelay arms the fade
            Output = 0f;
        }

        /// <summary>Re-arm the fade-in envelope. Call on note-on of any voice.</summary>
        public void TriggerDelay()
        {
            _delayLevel = 0f;
        }

        /// <summary>
        /// Process one sample. rate / delay are the current parameter values (0..127).
        /// Rate maps exponentially to ~0.1–30 Hz. Delay maps linearly to 0–~5 s
        /// fade-in time.
        /// </summary>
        public void Process(int rate, int delay)
        {
            // Cache rate → increment (recompute only when rate parameter changes)
            if (rate != _cachedRate)
            {
                _cachedRate = rate;
                float hz = DspMath.ParamToHz(rate, 0.1f, 30f);
                _cachedInc = hz * _invSr;
            }

            _phase += _cachedInc;
            if (_phase >= 1f) _phase -= 1f;

            // Triangle: 0..0.5 → -1..1, 0.5..1 → 1..-1
            float tri = (_phase < 0.5f) ? (4f * _phase - 1f) : (3f - 4f * _phase);

            // Delay fade-in: linear ramp over delayTime seconds
            // delay=0 is instant (no fade), delay=127 is ~5 s
            float delayTimeSec = delay * (5f / 127f);
            if (delayTimeSec <= 0.001f)
            {
                _delayLevel = 1f;
            }
            else if (_delayLevel < 1f)
            {
                _delayLevel += _invSr / delayTimeSec;
                if (_delayLevel > 1f) _delayLevel = 1f;
            }

            Output = tri * _delayLevel;
        }
    }
}
