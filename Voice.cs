using System;

namespace PedalJuno106
{
    /// <summary>
    /// One voice. Owns DCO + envelope + filter state.
    /// Track index ↔ voice index (1:1 in v1.0).
    ///
    /// Hot fields stay direct on this class — no nested objects in the
    /// inner-loop sample call beyond the explicit Dco/Filter/Envelope
    /// references.
    /// </summary>
    internal sealed class Voice
    {
        public readonly Dco          Dco    = new Dco();
        public readonly Envelope     Env    = new Envelope();
        public readonly LadderFilter Filter = new LadderFilter();

        public bool   Active;
        public byte   CurrentNote = 255;   // 0..127 valid; 255 = none/off
        public float  Frequency;           // Hz from MidiNote
        public float  MidiNote;            // for keyboard tracking

        public void SetSampleRate(float sr)
        {
            Env.SetSampleRate(sr);
            Filter.SetSampleRate(sr);
        }

        public void Reset()
        {
            Dco.Reset();
            Filter.Reset();
            Env.Stage = EnvStage.Idle;
            Env.Level = 0f;
            Active = false;
            CurrentNote = 255;
        }

        /// <summary>Note-on: reset DCO phase + filter, retrigger envelope (Juno-faithful).</summary>
        public void Trigger(byte midiNote)
        {
            CurrentNote = midiNote;
            MidiNote    = midiNote;
            Frequency   = DspMath.MidiToHz(midiNote);
            Dco.Reset();
            Filter.Reset();
            Env.Trigger();
            Active = true;
        }

        public void NoteOff()
        {
            Env.NoteOff();
        }
    }
}
