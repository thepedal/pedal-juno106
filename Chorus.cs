using System;

namespace PedalJuno106
{
    /// <summary>
    /// Juno-106 stereo chorus emulation.
    ///
    /// One LFO (rate and depth depend on selected mode) modulates two delay
    /// lines in anti-phase. One-pole lowpass before and after each delay
    /// emulates BBD bandwidth (~5 kHz). Output is dry + wet at 0.5 each.
    ///
    /// Mode I:   ~0.51 Hz, 1.92 ms — slow, lush
    /// Mode II:  ~0.86 Hz, 1.92 ms — denser
    /// Mode III: ~9.91 Hz, 0.18 ms — iconic Juno warble (very fast, very tiny)
    ///
    /// Configure(mode) caches mode-dependent coefficients once per Work()
    /// block; Process(input) is the per-sample hot path with no mode
    /// switching.
    /// </summary>
    internal sealed class Chorus
    {
        const int   BufferLen      = 4096;     // ~93 ms at 44.1 kHz
        const float CenterDelayMs  = 5.0f;
        const float BbdCutoffHz    = 5000f;

        // Instance buffers (Core §22 — never static)
        readonly float[] _delayL = new float[BufferLen];
        readonly float[] _delayR = new float[BufferLen];

        int   _writeIdx;
        float _sr     = 44100f;
        float _invSr  = 1f / 44100f;
        float _bbdCoef;

        float _lfoPhase;
        float _preLpf;
        float _postLpfL, _postLpfR;

        // Mode-dependent values cached by Configure().
        // _lfoInc and _depthSamp depend on _sr too, so SetSampleRate must
        // invalidate _cachedMode to force a re-cache.
        int   _cachedMode = -1;
        bool  _isOn;
        float _lfoInc;          // rate · invSr     (LFO phase increment / sample)
        float _depthSamp;       // depthMs · sr · 0.001  (modulation depth in samples)
        float _centerSamp;      // CenterDelayMs · sr · 0.001  (centre delay in samples)

        public Chorus()
        {
            UpdateBbdCoef();
            _centerSamp = CenterDelayMs * _sr * 0.001f;
        }

        public void SetSampleRate(float sr)
        {
            _sr = sr;
            _invSr = 1f / sr;
            UpdateBbdCoef();
            _centerSamp = CenterDelayMs * _sr * 0.001f;
            _cachedMode = -1;       // _lfoInc / _depthSamp depend on sr — re-cache
        }

        void UpdateBbdCoef()
        {
            _bbdCoef = 1f - MathF.Exp(-2f * MathF.PI * BbdCutoffHz * _invSr);
        }

        public void Reset()
        {
            Array.Clear(_delayL, 0, BufferLen);
            Array.Clear(_delayR, 0, BufferLen);
            _writeIdx = 0;
            _lfoPhase = 0f;
            _preLpf = 0f;
            _postLpfL = _postLpfR = 0f;
        }

        /// <summary>
        /// Set chorus mode. Cheap when mode hasn't changed. Caches the
        /// mode-dependent rate→increment and depth→samples conversions so
        /// the per-sample Process loop has no mode switching.
        /// Call once per Work() block, before the per-sample Process loop.
        /// </summary>
        public void Configure(int mode)
        {
            if (mode == _cachedMode) return;
            _cachedMode = mode;

            if (mode <= 0)
            {
                _isOn = false;
                return;
            }
            _isOn = true;

            float rate, depthMs;
            switch (mode)
            {
                case 1:  rate = 0.513f; depthMs = 1.92f; break;
                case 2:  rate = 0.863f; depthMs = 1.92f; break;
                default: rate = 9.91f;  depthMs = 0.18f; break;   // mode 3
            }
            _lfoInc    = rate * _invSr;
            _depthSamp = depthMs * _sr * 0.001f;
        }

        /// <summary>
        /// Process one mono input sample, output stereo. Uses the mode last
        /// passed to Configure().
        /// </summary>
        public void Process(float input, ref float outL, ref float outR)
        {
            if (!_isOn)
            {
                outL = input;
                outR = input;
                return;
            }

            // Pre-LPF (BBD bandwidth)
            _preLpf += _bbdCoef * (input - _preLpf);

            // Write to both delay buffers
            _delayL[_writeIdx] = _preLpf;
            _delayR[_writeIdx] = _preLpf;

            // Advance LFO (cached _lfoInc — no rate/sr math per sample)
            _lfoPhase += _lfoInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
            float lfo = MathF.Sin(2f * MathF.PI * _lfoPhase);

            // Anti-phase modulation: L = centre + depth·lfo, R = centre − depth·lfo
            float dL = _centerSamp + _depthSamp * lfo;
            float dR = _centerSamp - _depthSamp * lfo;

            float wetL = ReadDelay(_delayL, dL);
            float wetR = ReadDelay(_delayR, dR);

            // Post-LPF
            _postLpfL += _bbdCoef * (wetL - _postLpfL);
            _postLpfR += _bbdCoef * (wetR - _postLpfR);

            // Mix dry + wet
            outL = (input + _postLpfL) * 0.5f;
            outR = (input + _postLpfR) * 0.5f;

            _writeIdx++;
            if (_writeIdx >= BufferLen) _writeIdx = 0;
        }

        float ReadDelay(float[] buf, float delaySamples)
        {
            if (delaySamples < 1f)              delaySamples = 1f;
            if (delaySamples > BufferLen - 2f)  delaySamples = BufferLen - 2f;

            float readIdx = _writeIdx - delaySamples;
            if (readIdx < 0f) readIdx += BufferLen;

            // Bit-mask both indices: BufferLen is a power of two so `& (BufferLen-1)`
            // is `% BufferLen` and is bulletproof against the edge case where
            // readIdx rounds to exactly BufferLen (4096.0f). That happens when
            // _writeIdx ≈ delaySamples and the negative-wrap addition lands at
            // a representable float boundary — `(int)4096.0f = 4096` would then
            // index past the array. Masking turns 4096 into 0, which is the
            // correct wraparound anyway.
            int   idx0 = (int)readIdx & (BufferLen - 1);
            float frac = readIdx - (int)readIdx;
            int   idx1 = (idx0 + 1) & (BufferLen - 1);

            return buf[idx0] + frac * (buf[idx1] - buf[idx0]);
        }
    }
}
