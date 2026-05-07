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
    ///
    /// Anti-click envelope: a per-sample fade gain (0..1) multiplied into the
    /// voice's final output. Suppresses the three click sources in this
    /// design (PedalTracker §4.2 deferred-trigger pattern):
    ///   1. Retrigger of an audible voice — stage as a pending trigger,
    ///      fade to 0 first, then do the reset, then fade back in.
    ///   2. Initial transient on note-on — every fresh trigger starts at 0
    ///      and ramps to 1 over ~1.3 ms. Masks any DC offset from filter init
    ///      and any DCO phase-0 mismatch with what came before.
    ///   3. Gate-mode VCA transitions — in Gate mode the work loop drives
    ///      AntiClickTarget from env stage, so the binary 0/1 becomes a
    ///      smooth ~1.3 ms ramp. (Env mode leaves it at 1; the envelope
    ///      itself shapes the sound.)
    /// </summary>
    internal sealed class Voice
    {
        // ── Anti-click constants ──────────────────────────────────────────────
        // 1/64 per sample ≈ 1.33 ms at 48 kHz, ≈ 1.45 ms at 44.1 kHz.
        // Short enough to be inaudible but long enough to fully suppress
        // the step discontinuities at note boundaries.
        public const float FadeStep = 1f / 64f;
        public const float NearZero = 1e-4f;

        public readonly Dco          Dco    = new Dco();
        public readonly Envelope     Env    = new Envelope();
        public readonly LadderFilter Filter = new LadderFilter();

        public bool   Active;
        public byte   CurrentNote = 255;   // 0..127 valid; 255 = none/off
        public float  Frequency;           // Hz from MidiNote
        public float  MidiNote;            // for keyboard tracking

        // ── Anti-click state ──────────────────────────────────────────────────
        public float AntiClick;            // current value [0..1]
        public float AntiClickTarget;      // 0 = fading out, 1 = fading in
        public bool  HasPendingTrigger;    // a Trigger() was deferred during a fade-out
        public byte  PendingNote;          // the note to actually trigger when fade hits 0

        // ── Per-voice drift (analog-like detuning) ────────────────────────────
        // StaticDetuneCents is a small constant per-voice offset (component
        // tolerance) — set once at construction, never changes.
        // DriftCents is a slow random walk on top, lowpass-filtered noise
        // updated once per Work() block. Time constant ~2 s so the wander is
        // genuinely slow (analog-like, not LFO-like).
        // DriftOctaves is the combined offset converted once per block, added
        // straight into the existing pitchOct add chain in the per-sample
        // loop — costs one extra add per sample, no extra FastPow2 calls.
        public float StaticDetuneCents;    // immutable per voice
        public float DriftCents;           // slow random-walk state
        public float DriftOctaves;         // (StaticDetuneCents + DriftCents) / 1200, cached per block
        public uint  DriftSeed;            // per-voice LCG seed

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
            AntiClick = 0f;
            AntiClickTarget = 0f;
            HasPendingTrigger = false;
            // Note: StaticDetuneCents, DriftCents, DriftSeed deliberately
            // not reset — drift is an analog-like effect that persists across
            // notes (a real Juno DCO doesn't re-tune itself on each key).
        }

        /// <summary>
        /// Advance the slow drift random walk by one block-rate step, then
        /// recompute the cached DriftOctaves used by the per-sample loop.
        ///   coef         : lowpass coefficient for the random walk
        ///                  (≈ blockTimeSec / driftTimeConstantSec)
        ///   wanderCents  : range of the random kick (peak-to-peak ≈ 2× this)
        /// Always called for every voice every block, including silent ones —
        /// drift evolves continuously in real hardware, regardless of key state.
        /// </summary>
        public void UpdateDrift(float coef, float wanderCents)
        {
            // LCG step, signed value in approximately [-1, 1)
            DriftSeed = DriftSeed * 1664525u + 1013904223u;
            float rnd = (DriftSeed * (1f / 2147483648f)) - 1f;

            // One-pole lowpass on white noise — produces slowly-wandering output
            // bounded roughly by ±wanderCents.
            DriftCents += coef * (rnd * wanderCents - DriftCents);

            // Cache the combined detune as octaves for the per-sample loop.
            DriftOctaves = (StaticDetuneCents + DriftCents) * (1f / 1200f);
        }

        /// <summary>
        /// Note-on. If the voice is currently audible, defer the actual
        /// retrigger: fade to 0 first, then DoTrigger() runs from
        /// TickAntiClick when the fade completes. Otherwise (silent voice)
        /// trigger immediately and fade in from 0.
        /// </summary>
        public void Trigger(byte midiNote)
        {
            if (Active && AntiClick > NearZero)
            {
                // Stage as pending — voice keeps playing and fades out.
                // The actual reset happens later in TickAntiClick.
                HasPendingTrigger = true;
                PendingNote       = midiNote;
                AntiClickTarget   = 0f;
                return;
            }

            // Voice is silent — fire immediately, fade in from 0.
            DoTrigger(midiNote);
            AntiClickTarget = 1f;
        }

        public void NoteOff()
        {
            // Cancel any pending retrigger — the user pressed a key, then
            // released it before the deferred trigger had a chance to fire.
            // Keep the deferred fade-out going (target stays at 0); just
            // don't bring up a new note when it lands.
            HasPendingTrigger = false;
            Env.NoteOff();
        }

        /// <summary>
        /// Per-sample anti-click ramp. Called once per sample from the work
        /// loop. Cheap: a couple of compares and a possible deferred-trigger
        /// dispatch when the fade-out has finished.
        /// </summary>
        public void TickAntiClick()
        {
            // Ramp toward AntiClickTarget by FadeStep
            if (AntiClick < AntiClickTarget)
            {
                AntiClick += FadeStep;
                if (AntiClick > AntiClickTarget) AntiClick = AntiClickTarget;
            }
            else if (AntiClick > AntiClickTarget)
            {
                AntiClick -= FadeStep;
                if (AntiClick < AntiClickTarget) AntiClick = AntiClickTarget;
            }

            // Fire the deferred trigger when fade-out has reached zero.
            // The work loop will see the new state from the next sample.
            if (HasPendingTrigger && AntiClick <= NearZero && AntiClickTarget == 0f)
            {
                HasPendingTrigger = false;
                DoTrigger(PendingNote);
                AntiClickTarget = 1f;        // fade back in
            }
        }

        /// <summary>
        /// The actual reset that used to live in Trigger(). Now private —
        /// only Trigger() and TickAntiClick() (deferred dispatch) call it,
        /// and they take care of the AntiClick state around it.
        /// </summary>
        void DoTrigger(byte midiNote)
        {
            CurrentNote = midiNote;
            MidiNote    = midiNote;
            Frequency   = DspMath.MidiToHz(midiNote);
            Dco.Reset();
            Filter.Reset();
            Env.Trigger();
            Active    = true;
            AntiClick = 0f;                   // start the fade-in from zero
        }
    }
}
