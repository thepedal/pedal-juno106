using System;

namespace PedalJuno106
{
    internal enum EnvStage
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release
    }

    /// <summary>
    /// ADSR envelope — linear attack, exponential decay/release toward target.
    /// Used per-voice. Coefficients are recomputed each note-on (and any time
    /// ADSR parameters change while the voice is active).
    ///
    /// Full retrigger style (Juno-faithful): note-on resets Level to 0.
    /// Brief click on Attack=0 retrigger of a sustaining note is accepted —
    /// users will set A>0 in practice.
    /// </summary>
    internal sealed class Envelope
    {
        public EnvStage Stage = EnvStage.Idle;
        public float Level = 0f;

        float _sr = 44100f;
        float _attackRate;       // linear ramp per sample
        float _decayCoef;        // exponential coefficient toward sustainLevel
        float _releaseCoef;      // exponential coefficient toward 0
        float _sustainLevel;

        public void SetSampleRate(float sr)
        {
            _sr = sr;
        }

        /// <summary>
        /// Compute the rate coefficients from current ADSR param values.
        /// Attack: 1 ms – 4 s. Decay/Release: 5 ms – 8 s. Sustain: 0..127 → 0..1.
        /// </summary>
        public void UpdateCoefficients(int attack, int decay, int sustain, int release)
        {
            float aTime = DspMath.ParamToTime(attack, 0.001f, 4f);
            _attackRate = 1f / (aTime * _sr);

            float dTime = DspMath.ParamToTime(decay, 0.005f, 8f);
            _decayCoef = MathF.Exp(-1f / (dTime * _sr));

            _sustainLevel = sustain * (1f / 127f);

            float rTime = DspMath.ParamToTime(release, 0.005f, 8f);
            _releaseCoef = MathF.Exp(-1f / (rTime * _sr));
        }

        /// <summary>Note-on: full retrigger from 0.</summary>
        public void Trigger()
        {
            Stage = EnvStage.Attack;
            Level = 0f;
        }

        /// <summary>Note-off: enter Release stage from current level.</summary>
        public void NoteOff()
        {
            if (Stage != EnvStage.Idle) Stage = EnvStage.Release;
        }

        /// <summary>Process one sample, returns the new envelope level [0..1].</summary>
        public float Process()
        {
            switch (Stage)
            {
                case EnvStage.Attack:
                    Level += _attackRate;
                    if (Level >= 1f) { Level = 1f; Stage = EnvStage.Decay; }
                    break;

                case EnvStage.Decay:
                    Level = _sustainLevel + (Level - _sustainLevel) * _decayCoef;
                    if (Level <= _sustainLevel + 0.0005f)
                    {
                        Level = _sustainLevel;
                        Stage = EnvStage.Sustain;
                    }
                    break;

                case EnvStage.Sustain:
                    // Track sustain in case the parameter changes while held
                    Level = _sustainLevel;
                    break;

                case EnvStage.Release:
                    Level *= _releaseCoef;
                    if (Level < 0.0005f)
                    {
                        Level = 0f;
                        Stage = EnvStage.Idle;
                    }
                    break;
            }
            return Level;
        }
    }
}
