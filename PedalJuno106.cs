using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using BuzzGUI.Interfaces;
using Buzz.MachineInterface;

namespace PedalJuno106
{
    /// <summary>
    /// Pedal Juno106 — faithful Roland Juno-106 emulation as a managed
    /// ReBuzz generator machine.
    ///
    /// Design notes live in /docs/SESSION_STATE.md and the project's
    /// ReBuzz_ManagedMachine_Notes_*.md files. Don't reorder parameter
    /// declarations: presets index by position (Build §3.3).
    /// </summary>
    [MachineDecl(
        Name      = "Pedal Juno106",
        ShortName = "Pedal Juno106",
        Author    = "Pedal",
        MaxTracks = 6)]
    public class PedalJuno106Machine : IBuzzMachine
    {
        // Single source of truth for voice count. The attribute above must
        // mirror this literally — attributes on a class cannot reference its
        // own consts.
        public const int VOICE_COUNT = 6;

        readonly IBuzzMachineHost host;

        // ── Components ─────────────────────────────────────────────────────────
        readonly Voice[] _voices;
        readonly Lfo     _lfo    = new Lfo();
        readonly Hpf     _hpf    = new Hpf();
        readonly Chorus  _chorus = new Chorus();

        // ── Pending note-event queue (drained at top of each Work) ─────────────
        readonly bool[] _hasNewNote;
        readonly byte[] _pendingNote;

        // ── Multi-track Note polling (Core §14) ────────────────────────────────
        IParameter _ownNoteParam;
        ConcurrentDictionary<int, int> _ownNotePValues;
        bool _polledInit;

        // ── Coefficient caches ─────────────────────────────────────────────────
        int _cachedSr      = -1;
        int _cachedAttack  = -1;
        int _cachedDecay   = -1;
        int _cachedSustain = -1;
        int _cachedRelease = -1;

        // ── Transport state ────────────────────────────────────────────────────
        // We poll IBuzz.Playing each Work() call and detect the falling edge.
        // When the user presses Stop, ReBuzz pauses the pattern engine (no more
        // setter calls) but keeps invoking Work() so downstream machines can
        // render their tails. Without this poll, sustained notes ring forever
        // because no NoteOff arrives. PedalTracker §3 pattern.
        bool _wasPlaying;

        // ── Per-voice drift (analog-like detuning) ─────────────────────────────
        // Static per-voice cent offsets — small constant detune unique to each
        // voice. Hand-tuned distribution that sums to zero so the chord average
        // stays in tune. Tuned for "tightly tuned vintage" — chord stacks
        // sound clean but with subtle analog character, not detuned.
        static readonly float[] _staticDetuneCents = {
            -0.8f, +0.2f, -0.4f, +0.6f, -0.1f, +0.5f
        };
        // Distinct LCG seeds so each voice's slow random walk evolves
        // independently. Any non-zero, non-equal values work.
        static readonly uint[] _voiceDriftSeeds = {
            0x12345678u, 0x9ABCDEF0u, 0x13579BDFu,
            0x2468ACE0u, 0x1A2B3C4Du, 0xFEDCBA98u
        };
        const float DriftTimeConstSec = 2.0f;   // ~2-second wander
        const float DriftWanderCents  = 0.5f;   // ±0.5 cent random-walk amplitude

        public PedalJuno106Machine(IBuzzMachineHost host)
        {
            this.host = host;
            _voices = new Voice[VOICE_COUNT];
            for (int i = 0; i < VOICE_COUNT; i++)
            {
                _voices[i] = new Voice();
                _voices[i].StaticDetuneCents = _staticDetuneCents[i];
                _voices[i].DriftSeed         = _voiceDriftSeeds[i];
                _voices[i].DriftOctaves      = _staticDetuneCents[i] * (1f / 1200f);
            }
            _hasNewNote  = new bool[VOICE_COUNT];
            _pendingNote = new byte[VOICE_COUNT];
        }

        // ──────────────────────────────────────────────────────────────────────
        // GLOBAL PARAMETERS — declaration order is the permanent preset contract.
        // Never reorder. Append-only for v1.1+.
        // ──────────────────────────────────────────────────────────────────────

        [ParameterDecl(Name = "LFO Rate",      MinValue = 0, MaxValue = 127, DefValue = 64)]
        public int LfoRate       { get; set; } = 64;

        [ParameterDecl(Name = "LFO Delay",     MinValue = 0, MaxValue = 127, DefValue = 0)]
        public int LfoDelay      { get; set; } = 0;

        [ParameterDecl(Name = "DCO LFO",       MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "DCO pitch modulation depth from LFO")]
        public int DcoLfoDepth   { get; set; } = 0;

        [ParameterDecl(Name = "DCO PWM",       MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Pulse-width modulation depth")]
        public int DcoPwmDepth   { get; set; } = 0;

        [ParameterDecl(Name = "DCO PWM Src",   MinValue = 0, MaxValue = 2, DefValue = 0,
            ValueDescriptions = new[] { "Manual", "LFO", "Env" })]
        public int DcoPwmSource  { get; set; } = 0;

        [ParameterDecl(Name = "DCO PW",        MinValue = 0, MaxValue = 127, DefValue = 64,
            Description = "Manual pulse width 50%–95%")]
        public int DcoPulseWidth { get; set; } = 64;

        [ParameterDecl(Name = "DCO Range",     MinValue = 0, MaxValue = 2, DefValue = 1,
            ValueDescriptions = new[] { "16'", "8'", "4'" })]
        public int DcoRange      { get; set; } = 1;

        [ParameterDecl(Name = "DCO Pulse",     DefValue = false,
            Description = "Pulse waveform on/off")]
        public bool DcoPulseOn   { get; set; } = false;

        [ParameterDecl(Name = "DCO Saw",       DefValue = true,
            Description = "Saw waveform on/off")]
        public bool DcoSawOn     { get; set; } = true;

        [ParameterDecl(Name = "DCO Sub",       MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Sub-oscillator level (square one octave below)")]
        public int DcoSubLevel   { get; set; } = 0;

        [ParameterDecl(Name = "DCO Noise",     MinValue = 0, MaxValue = 127, DefValue = 0)]
        public int DcoNoiseLevel { get; set; } = 0;

        [ParameterDecl(Name = "HPF",           MinValue = 0, MaxValue = 3, DefValue = 0,
            ValueDescriptions = new[] { "Off", "1", "2", "3" })]
        public int HpfPosition   { get; set; } = 0;

        [ParameterDecl(Name = "VCF Freq",      MinValue = 0, MaxValue = 127, DefValue = 100,
            Description = "Filter cutoff frequency")]
        public int VcfFrequency  { get; set; } = 100;

        [ParameterDecl(Name = "VCF Reso",      MinValue = 0, MaxValue = 127, DefValue = 30)]
        public int VcfResonance  { get; set; } = 30;

        [ParameterDecl(Name = "VCF Env Amt",   MinValue = 0, MaxValue = 127, DefValue = 80)]
        public int VcfEnvAmount  { get; set; } = 80;

        [ParameterDecl(Name = "VCF Env Pol",   MinValue = 0, MaxValue = 1, DefValue = 0,
            ValueDescriptions = new[] { "+", "-" })]
        public int VcfEnvPolarity { get; set; } = 0;

        [ParameterDecl(Name = "VCF LFO",       MinValue = 0, MaxValue = 127, DefValue = 0,
            Description = "Filter cutoff modulation depth from LFO")]
        public int VcfLfoDepth   { get; set; } = 0;

        [ParameterDecl(Name = "VCF Key Trk",   MinValue = 0, MaxValue = 127, DefValue = 64,
            Description = "Keyboard tracking (127 = full octave-per-octave)")]
        public int VcfKeyTrack   { get; set; } = 64;

        [ParameterDecl(Name = "VCA Mode",      MinValue = 0, MaxValue = 1, DefValue = 0,
            ValueDescriptions = new[] { "Env", "Gate" })]
        public int VcaMode       { get; set; } = 0;

        [ParameterDecl(Name = "VCA Level",     MinValue = 0, MaxValue = 127, DefValue = 100)]
        public int VcaLevel      { get; set; } = 100;

        [ParameterDecl(Name = "Env A",         MinValue = 0, MaxValue = 127, DefValue = 5,
            Description = "Attack time (1 ms – 4 s exponential)")]
        public int EnvAttack     { get; set; } = 5;

        [ParameterDecl(Name = "Env D",         MinValue = 0, MaxValue = 127, DefValue = 60,
            Description = "Decay time (5 ms – 8 s exponential)")]
        public int EnvDecay      { get; set; } = 60;

        [ParameterDecl(Name = "Env S",         MinValue = 0, MaxValue = 127, DefValue = 80)]
        public int EnvSustain    { get; set; } = 80;

        [ParameterDecl(Name = "Env R",         MinValue = 0, MaxValue = 127, DefValue = 40,
            Description = "Release time (5 ms – 8 s exponential)")]
        public int EnvRelease    { get; set; } = 40;

        [ParameterDecl(Name = "Chorus",        MinValue = 0, MaxValue = 3, DefValue = 0,
            ValueDescriptions = new[] { "Off", "I", "II", "III" })]
        public int ChorusMode    { get; set; } = 0;

        // ──────────────────────────────────────────────────────────────────────
        // TRACK PARAMETER — Note (one per voice).
        // ──────────────────────────────────────────────────────────────────────

        [ParameterDecl(Name = "Note", IsStateless = true,
            Description = "Note — 255 (off) releases the voice")]
        public void SetNote(Note value, int track)
        {
            if ((uint)track >= VOICE_COUNT) return;
            _pendingNote[track] = value.Value;
            _hasNewNote[track]  = true;

            // Recover any siblings that parametersChanged dropped (Core §14).
            PollOtherTracks(track);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Multi-track Note polling — recovers chords from the pvalues dictionary
        // before ReBuzz resets it post-Tick. See Core §14 / §15 for context.
        // ──────────────────────────────────────────────────────────────────────

        void TryInitNotePolling()
        {
            if (_polledInit) return;
            _polledInit = true;        // try only once even if it fails

            try
            {
                var groups = host?.Machine?.ParameterGroups;
                if (groups == null || groups.Count < 3) { _polledInit = false; return; }

                // Track group is index 2 in standard Buzz layout.
                _ownNoteParam = groups[2].Parameters
                    .FirstOrDefault(p => p?.Type == ParameterType.Note);
                if (_ownNoteParam == null) return;

                // Reflect on ParameterCore.pvalues (ConcurrentDictionary<int,int>).
                var fi = _ownNoteParam.GetType().GetField("pvalues",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _ownNotePValues = fi?.GetValue(_ownNoteParam)
                    as ConcurrentDictionary<int, int>;
            }
            catch
            {
                // Reflection failure is non-fatal; we just lose chord recovery.
                _ownNotePValues = null;
            }
        }

        void PollOtherTracks(int firedTrack)
        {
            if (_ownNotePValues == null) TryInitNotePolling();
            if (_ownNotePValues == null) return;

            int noVal = _ownNoteParam.NoValue;   // 0 for Note type
            for (int t = 0; t < VOICE_COUNT; t++)
            {
                if (t == firedTrack) continue;
                if (_hasNewNote[t]) continue;        // setter already fired this tick

                int pv;
                if (_ownNotePValues.TryGetValue(t, out pv) && pv != noVal && pv != 0)
                {
                    _pendingNote[t] = (byte)pv;
                    _hasNewNote[t]  = true;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Transport handling (PedalTracker §3) — detect Stop edge and silence
        // any sustained voices. Without this, voices in their sustain phase
        // ring forever after the user presses Stop because the pattern engine
        // pauses without sending NoteOff events.
        // ──────────────────────────────────────────────────────────────────────

        void CheckTransport()
        {
            bool nowPlaying;
            try
            {
                // host.Machine.Graph.Buzz is the IBuzz instance (Core §10).
                nowPlaying = host?.Machine?.Graph?.Buzz?.Playing ?? false;
            }
            catch
            {
                // Never break audio on a poll fail — assume no change.
                nowPlaying = _wasPlaying;
            }

            if (_wasPlaying && !nowPlaying)
                StopAllVoices();

            _wasPlaying = nowPlaying;
        }

        void StopAllVoices()
        {
            // Discard any pattern events the engine queued in the same tick
            // as the Stop edge — they're stale.
            for (int t = 0; t < VOICE_COUNT; t++)
            {
                _hasNewNote[t]  = false;
                _pendingNote[t] = 0;
            }

            // For every active voice: send NoteOff (env enters Release so the
            // voice eventually reaches Idle and frees) AND force the anti-click
            // fade target to 0 (audible silence in ~1.3 ms regardless of the
            // patch's release time — Stop should be immediate to the listener).
            for (int v = 0; v < VOICE_COUNT; v++)
            {
                var voice = _voices[v];
                if (!voice.Active) continue;
                voice.NoteOff();              // also clears HasPendingTrigger
                voice.AntiClickTarget = 0f;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Audio loop — ReBuzz calls this once per audio buffer (≤256 samples).
        // ──────────────────────────────────────────────────────────────────────

        public bool Work(Sample[] output, int numSamples, WorkModes mode)
        {
            // ── Sample rate check + cache update ───────────────────────────────
            int sr = host?.MasterInfo?.SamplesPerSec ?? 44100;
            if (sr != _cachedSr)
            {
                _cachedSr = sr;
                _lfo.SetSampleRate(sr);
                _hpf.SetSampleRate(sr);
                _chorus.SetSampleRate(sr);
                for (int v = 0; v < VOICE_COUNT; v++) _voices[v].SetSampleRate(sr);
                _cachedAttack = -1;   // force ADSR recompute
            }

            // ── Transport: detect Stop edge and silence held voices ────────────
            //    Done before ProcessPendingNotes so any pattern events queued
            //    in the same tick as a Stop edge are correctly discarded. ──────
            CheckTransport();

            // ── Drain any pending note events from this tick.
            //    NOTE: ADSR update must precede this so freshly-triggered
            //    voices have valid coefficients on their first sample. ─────────
            UpdateAdsrIfNeeded();
            ProcessPendingNotes();

            // ── Hoist parameter reads outside the inner loop ───────────────────
            float vcaLevel    = VcaLevel    * (1f / 127f);
            int   vcaModeL    = VcaMode;
            int   chorusModeL = ChorusMode;
            int   hpfPosL     = HpfPosition;
            int   pwmSrcL     = DcoPwmSource;

            float dcoLfoDepth = DcoLfoDepth * (1f / 127f);
            float pwmDepth    = DcoPwmDepth * (1f / 127f);
            float pwBase      = 0.5f + DcoPulseWidth * (0.45f / 127f);   // 0.5..0.95
            float subLvl      = DcoSubLevel   * (1f / 127f);
            float noiseLvl    = DcoNoiseLevel * (1f / 127f);
            bool  sawOn       = DcoSawOn;
            bool  pulseOn     = DcoPulseOn;
            float resonance01 = VcfResonance * (1f / 127f);

            // Range switch: 16' = 0.5×, 8' = 1×, 4' = 2× the natural pitch.
            float rangeFactor = DcoRange == 0 ? 0.5f
                              : DcoRange == 2 ? 2f
                                              : 1f;

            // Cutoff in octaves space (we add modulation in octaves, then FastPow2)
            float vcfFreqHz  = DspMath.ParamToHz(VcfFrequency, 30f, 16000f);
            float baseCutOct = MathF.Log2(vcfFreqHz);
            float keyTrk01   = VcfKeyTrack   * (1f / 127f);
            float envAmt01   = VcfEnvAmount  * (1f / 127f);
            float lfoCut01   = VcfLfoDepth   * (1f / 127f);
            float envSign    = (VcfEnvPolarity == 0) ? 1f : -1f;

            int   lfoRateL  = LfoRate;
            int   lfoDelayL = LfoDelay;
            float srf       = sr;

            // Cutoff clamp range — applied per-sample to the modulated cutoff
            // before passing to the filter. Without this, extreme env+LFO
            // modulation can push fc past Nyquist; FastTan saves us internally
            // but the filter then runs deep in non-linear territory with no
            // meaningful "cutoff" semantics.
            float cutoffMaxHz = srf * 0.45f;

            // Hoist post-mix selectors out of the per-sample loop. Configure
            // is cheap when the parameter hasn't changed (single int compare);
            // when it has, mode-dependent coefficients are computed once and
            // cached so the hot Process loop has no switch-on-mode logic.
            _hpf.Configure(hpfPosL);
            _chorus.Configure(chorusModeL);

            // Per-voice drift — slow analog-like wander, updated once per
            // Work() block. Coef derived from block size so the time constant
            // stays ~constant across whatever block size ReBuzz is using.
            float blockTimeSec = numSamples / srf;
            float driftCoef    = 1f - MathF.Exp(-blockTimeSec / DriftTimeConstSec);
            for (int v = 0; v < VOICE_COUNT; v++)
                _voices[v].UpdateDrift(driftCoef, DriftWanderCents);

            bool nonSilent = false;

            for (int i = 0; i < numSamples; i++)
            {
                _lfo.Process(lfoRateL, lfoDelayL);
                float lfoOut = _lfo.Output;

                float mix = 0f;

                for (int v = 0; v < VOICE_COUNT; v++)
                {
                    var voice = _voices[v];
                    if (!voice.Active) continue;

                    // Per-sample anti-click ramp. May fire a deferred
                    // trigger (in which case voice state has just been
                    // reset and AntiClick=0 starting its fade-in).
                    voice.TickAntiClick();

                    // Envelope tick
                    float env = voice.Env.Process();

                    // Modulated cutoff in octaves → Hz
                    float keyOct = (voice.MidiNote - 60f) * (1f / 12f) * keyTrk01;
                    float envOct = envAmt01 * env * envSign * 6f;     // ≤ 6 oct
                    float lfoOct = lfoCut01 * lfoOut * 4f;            // ≤ ±4 oct
                    float cutoffHz = DspMath.FastPow2(baseCutOct + keyOct + envOct + lfoOct);
                    // Explicit clamp before the filter — see cutoffMaxHz comment above.
                    if (cutoffHz < 20f)              cutoffHz = 20f;
                    else if (cutoffHz > cutoffMaxHz) cutoffHz = cutoffMaxHz;

                    // DCO frequency with pitch modulation (DCO LFO Depth) +
                    // per-voice drift (static detune + slow analog-like wander).
                    // Both folded into pitchOct so the existing FastPow2 carries
                    // them — one extra add per sample, no extra transcendentals.
                    // Juno vibrato range: max depth = ±1 semitone (≈ ±100 cents).
                    float pitchOct = dcoLfoDepth * lfoOut * (1f / 12f) + voice.DriftOctaves;
                    float freqHz   = voice.Frequency * rangeFactor *
                                     DspMath.FastPow2(pitchOct);

                    // Pulse width modulation source
                    float pwSource;
                    switch (pwmSrcL)
                    {
                        case 1:  pwSource = lfoOut; break;     // LFO
                        case 2:  pwSource = env;    break;     // Env
                        default: pwSource = 0f;     break;     // Manual
                    }
                    float pw = pwBase + pwmDepth * pwSource * 0.45f;
                    if (pw < 0.05f) pw = 0.05f;
                    if (pw > 0.95f) pw = 0.95f;

                    // DCO + filter
                    float dco = voice.Dco.Process(freqHz, pw, sawOn, pulseOn,
                                                  subLvl, noiseLvl, srf);
                    float filtered = voice.Filter.Process(dco, cutoffHz, resonance01);

                    // VCA shape:
                    //   Env mode  → vca = env, AntiClick stays at 1 (set by
                    //                Trigger; not touched here).
                    //   Gate mode → vca = 1, AntiClickTarget driven by env
                    //                stage so the binary on/off becomes a
                    //                smooth ~1.3 ms ramp. We don't override
                    //                AntiClickTarget while a deferred trigger
                    //                is in flight (its fade-out target of 0
                    //                must win).
                    float vca;
                    if (vcaModeL == 0)
                    {
                        vca = env;
                    }
                    else
                    {
                        vca = 1f;
                        if (!voice.HasPendingTrigger)
                        {
                            bool gateOn = !(voice.Env.Stage == EnvStage.Release ||
                                            voice.Env.Stage == EnvStage.Idle);
                            voice.AntiClickTarget = gateOn ? 1f : 0f;
                        }
                    }
                    mix += filtered * vca * voice.AntiClick;

                    // Free voice when envelope has fully decayed. AntiClick
                    // doesn't need to be in the check: in Env mode it stays
                    // at 1 (output is already 0 because env is 0); in Gate
                    // mode AntiClick has long since reached 0 by the time
                    // env hits Idle (fade is ~1.3 ms vs env release ≥5 ms).
                    if (voice.Env.Stage == EnvStage.Idle && voice.Env.Level == 0f)
                        voice.Active = false;
                }

                // Master gain
                mix *= vcaLevel;

                // HPF (mono, pre-chorus) → Chorus (mono in, stereo out).
                // Both were Configured once before this loop — the per-sample
                // calls just run their math against the cached coefficients.
                float monoHpf = _hpf.ProcessMono(mix);
                float outL = 0f, outR = 0f;
                _chorus.Process(monoHpf, ref outL, ref outR);

                if (outL != 0f || outR != 0f) nonSilent = true;

                // Buzz convention: generators write at ±32768 (PedalComp §1)
                output[i] = new Sample(outL * 32768f, outR * 32768f);
            }

            return nonSilent;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        void ProcessPendingNotes()
        {
            for (int t = 0; t < VOICE_COUNT; t++)
            {
                if (!_hasNewNote[t]) continue;
                _hasNewNote[t] = false;

                byte b = _pendingNote[t];
                if (b == 0) continue;        // NoValue — defensive

                if (b == 255)                // Note-off
                {
                    _voices[t].NoteOff();
                    continue;
                }

                // Buzz byte → MIDI: oct = b>>4, semi = (b & 0xF) - 1
                int oct  = (b >> 4);
                int semi = (b & 0xF) - 1;
                int midi = oct * 12 + semi;
                if (midi < 0 || midi > 127) continue;

                _voices[t].Trigger((byte)midi);
                _lfo.TriggerDelay();          // re-arm LFO fade-in on any new note
            }
        }

        void UpdateAdsrIfNeeded()
        {
            if (EnvAttack  == _cachedAttack  &&
                EnvDecay   == _cachedDecay   &&
                EnvSustain == _cachedSustain &&
                EnvRelease == _cachedRelease)
                return;

            _cachedAttack  = EnvAttack;
            _cachedDecay   = EnvDecay;
            _cachedSustain = EnvSustain;
            _cachedRelease = EnvRelease;

            for (int v = 0; v < VOICE_COUNT; v++)
                _voices[v].Env.UpdateCoefficients(EnvAttack, EnvDecay,
                                                   EnvSustain, EnvRelease);
        }
    }
}
