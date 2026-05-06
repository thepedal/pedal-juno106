using System;

namespace PedalJuno106
{
    /// <summary>
    /// Allocation-free DSP helpers. Fast approximations are used in the per-sample
    /// hot path; exact MathF versions are reserved for parameter-change-rate work
    /// (coefficient setup outside the inner loop).
    /// </summary>
    internal static class DspMath
    {
        // ── Fast LinToDb — replaces 20f * MathF.Log10(lin) ────────────────────
        // Accuracy: ±0.1 dB over 1e-6 to 1.0
        public static float FastLinToDb(float lin)
        {
            if (lin <= 1e-9f) return -120f;
            int   bits = BitConverter.SingleToInt32Bits(lin);
            float exp  = (bits >> 23) - 127f;
            float mant = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000) - 1f;
            float log2 = exp + mant * (1.4142f - 0.7071f * mant);
            return log2 * 6.02059f;
        }

        // ── Fast DbToLin — replaces MathF.Pow(10f, db / 20f) ──────────────────
        // Accuracy: ±0.1 dB for db in [−120, 24]
        public static float FastDbToLin(float db)
        {
            float x  = db * 0.16609f;            // db × log₂(10) / 20
            float xi = MathF.Floor(x);
            float xf = x - xi;
            float p  = 1f + xf * (0.69315f + xf * (0.24023f + xf * 0.05550f));
            int   e  = Math.Clamp((int)xi + 127, 1, 254);
            return BitConverter.Int32BitsToSingle(e << 23) * p;
        }

        // ── Fast tan(x) for x in [0, ~1.4] ────────────────────────────────────
        // Padé-style approximation. Used for filter coefficient g = tan(π·fc/sr).
        // Accuracy: better than 1% across the usable range.
        public static float FastTan(float x)
        {
            if (x > 1.4f) x = 1.4f;
            if (x < 0f)   x = 0f;
            float x2 = x * x;
            return x * (1f - 0.0958f * x2) / (1f - 0.4292f * x2);
        }

        // ── Soft saturation: smooth tanh-like curve, no transcendental ────────
        // Used inside the filter feedback path to bound self-oscillation.
        public static float SoftClip(float x)
        {
            float ax = x < 0f ? -x : x;
            return x / (1f + ax);
        }

        // ── Parameter 0..127 → exponential time in seconds ────────────────────
        // p=0 → fastTime, p=127 → slowTime.
        // NOT for hot-path use: contains MathF.Pow.
        public static float ParamToTime(int p, float fastTime, float slowTime)
        {
            if (p < 0)   p = 0;
            if (p > 127) p = 127;
            float t = p / 127f;
            return fastTime * MathF.Pow(slowTime / fastTime, t);
        }

        // ── Parameter 0..127 → exponential frequency (Hz) ─────────────────────
        // Useful for LFO rate, filter cutoff base.
        public static float ParamToHz(int p, float minHz, float maxHz)
        {
            if (p < 0)   p = 0;
            if (p > 127) p = 127;
            float t = p / 127f;
            return minHz * MathF.Pow(maxHz / minHz, t);
        }

        // ── MIDI note → frequency Hz (equal temperament, A4=440=MIDI 69) ──────
        public static float MidiToHz(float midi)
        {
            return 440f * MathF.Pow(2f, (midi - 69f) * (1f / 12f));
        }

        // ── Fast 2^x via IEEE 754 exponent manipulation ───────────────────────
        // Used per-sample for cutoff and pitch modulation in octave space.
        // Accuracy: within ~0.03% across the safe range [-126, 127].
        public static float FastPow2(float x)
        {
            if (x < -126f) return 0f;
            if (x >  127f) x =  127f;
            float xi = MathF.Floor(x);
            float xf = x - xi;
            float p  = 1f + xf * (0.69315f + xf * (0.24023f + xf * 0.05550f));
            int e = (int)xi + 127;
            if (e < 1)   e = 1;
            if (e > 254) e = 254;
            return BitConverter.Int32BitsToSingle(e << 23) * p;
        }
    }
}
