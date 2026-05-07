# Pedal Juno106 — Session State

**Status:** v1.0 non-GUI scaffold **complete and packaged**. Not yet
compiled or tested in ReBuzz — that's the next step on the user's
Windows dev box.

This document is the canonical "where we are now" handoff. If a fresh
chat opens with this in scope, it should give enough context to resume
without re-deriving the design.

---

## Project identity

- **Build name (user-facing):** Pedal Juno106
- **AssemblyName (.dll filename, mandatory `.NET` suffix per Build §2):**
  `Pedal Juno106.NET` → produces `Pedal Juno106.NET.dll`
- **C# namespace / class identifier form (no space allowed by C#):**
  `PedalJuno106` (namespace) / `PedalJuno106Machine` (class)
- **Author:** Pedal
- **Browser entry, MachineDecl.Name, ShortName all:** "Pedal Juno106"

---

## What it is

Faithful Roland Juno-106 emulation as a **managed C# generator machine**
for ReBuzz. Track index ↔ voice index 1:1 (tracker-style direct mapping).
6-voice fixed polyphony.

Architectural contract — locked, do not revisit without good reason:

- 6 voices, fixed
- Track index = voice index
- Stereo output (chorus produces stereo); no mono-input concerns
  (it's a generator)
- One shared global LFO (Juno-faithful), triangle, with delay fade-in
- Per-voice envelope and filter state
- HPF and chorus on the post-mix bus, not per-voice
- No velocity, pitch bend, glide, or hold for v1.0 (v1.1 candidates,
  append-only per Build §3.3)
- Track parameter list is `Note` only
- Full retrigger on note-on (DCO phase, filter state, env all reset to 0)
- Note-off via Buzz byte 255 (0xFF), standard convention
- Filter: TPT 4-pole ladder with tanh-style soft-clip in feedback path,
  k = 0..4.5

---

## Parameter contract — DECLARATION ORDER IS PERMANENT (Build §3.3)

Reordering breaks every existing preset because ReBuzz indexes presets
by parameter position, not name. **Append-only** for any future version.

### 25 globals in declaration order

|  # | Name (display) | Range | Type | DefValue |
|---:|----------------|------:|------|---------:|
|  1 | LFO Rate        | 0–127 | Byte                       | 64  |
|  2 | LFO Delay       | 0–127 | Byte                       | 0   |
|  3 | DCO LFO         | 0–127 | Byte (DCO pitch mod depth) | 0   |
|  4 | DCO PWM         | 0–127 | Byte                       | 0   |
|  5 | DCO PWM Src     | 0–2   | Byte + ValueDescriptions ("Manual"/"LFO"/"Env") | 0 |
|  6 | DCO PW          | 0–127 | Byte                       | 64  |
|  7 | DCO Range       | 0–2   | Byte + ValueDescriptions ("16'"/"8'"/"4'") | 1 |
|  8 | DCO Pulse       | bool  | Switch                     | false |
|  9 | DCO Saw         | bool  | Switch                     | true |
| 10 | DCO Sub         | 0–127 | Byte                       | 0   |
| 11 | DCO Noise       | 0–127 | Byte                       | 0   |
| 12 | HPF             | 0–3   | Byte + ValueDescriptions ("Off"/"1"/"2"/"3") | 0 |
| 13 | VCF Freq        | 0–127 | Byte                       | 100 |
| 14 | VCF Reso        | 0–127 | Byte                       | 30  |
| 15 | VCF Env Amt     | 0–127 | Byte                       | 80  |
| 16 | VCF Env Pol     | 0–1   | Byte + ValueDescriptions ("+"/"-") | 0 |
| 17 | VCF LFO         | 0–127 | Byte                       | 0   |
| 18 | VCF Key Trk     | 0–127 | Byte                       | 64  |
| 19 | VCA Mode        | 0–1   | Byte + ValueDescriptions ("Env"/"Gate") | 0 |
| 20 | VCA Level       | 0–127 | Byte                       | 100 |
| 21 | Env A           | 0–127 | Byte (1 ms – 4 s)          | 5   |
| 22 | Env D           | 0–127 | Byte (5 ms – 8 s)          | 60  |
| 23 | Env S           | 0–127 | Byte                       | 80  |
| 24 | Env R           | 0–127 | Byte (5 ms – 8 s)          | 40  |
| 25 | Chorus          | 0–3   | Byte + ValueDescriptions ("Off"/"I"/"II"/"III") | 0 |

### Track param

- **Note** only (`IsStateless = true`, multi-track polling per Core §14)

The defaults give a playable saw lead with mild filter envelope as the
default patch — saw on, 8' range, mid cutoff with positive envelope sweep,
fast attack, medium release, no chorus.

---

## File structure (`pedaljuno106/`)

```
pedaljuno106/
├── PedalJuno106.cs        Machine class — 25 globals + Note setter,
│                          multi-track polling (Core §14), Work loop,
│                          ADSR cache, parameter hoisting
├── Voice.cs               Per-voice container (DCO + Envelope + Filter)
├── Dco.cs                 Saw + pulse + sub + noise, PolyBLEP anti-aliased
├── Filter.cs              TPT 4-pole ladder LPF (`LadderFilter` class)
├── Envelope.cs            ADSR — linear A, exp D/R, full retrigger
├── Lfo.cs                 Triangle LFO + delay fade-in
├── Hpf.cs                 4-position one-pole HPF (post-mix mono)
├── Chorus.cs              BBD-style stereo chorus, 3 modes
├── DspMath.cs             FastTan, FastPow2, FastDb⇄Lin, SoftClip,
│                          ParamToTime, ParamToHz, MidiToHz
├── PedalJuno106.csproj    net10.0-windows x64, build hygiene per Build §1.2,
│                          AfterBuild auto-deploy to ReBuzz\Gear\Generators
│                          (DLL + .prs.xml)
├── gen_presets.py         Preset bank generator (Build §3.4 convention)
├── Pedal Juno106_Presets.prs.xml   48 presets, 7 categories
├── README.md              User-facing project README
└── SESSION_STATE.md       This file
```

GUI files (a `PedalJuno106Gui.cs` and `PedalJuno106GuiFactory.cs`) join
later when GUI work begins.

---

## DSP design recap

**DCO** — single per voice, four sources mixed:
- Saw (PolyBLEP at wrap)
- Pulse with variable width 0.5..0.95 (PolyBLEP at both edges)
- Sub-oscillator: square one octave below, own phase accumulator
- Noise: LCG, allocation-free

**Filter** — TPT 4-pole ladder LPF. Soft-clip (`SoftClip(input − k·z4)`)
in the feedback path bounds self-oscillation cleanly at high resonance.
Coefficient G is recomputed at most every 16 samples (~0.36 ms at
44.1 kHz) — and only when fc has actually moved by ≥0.5 Hz. Skips
~94% of FastTan calls under per-sample modulation; sub-audio update
rate so no zipper artefacts.

**Cutoff clamp** — explicit clamp in the work loop bounds the modulated
cutoff to `[20 Hz, 0.45·sr]` before the filter call. Filter still has
its own internal clamp inside the decimated G-update block as a
defensive backstop, but the work-loop clamp is the primary one.

**HPF / Chorus** — both use a `Configure(...)` call once per Work()
block to cache mode/position-dependent coefficients (HPF: filter coef
from the 4-position table; Chorus: `_lfoInc`, `_depthSamp`, `_centerSamp`
from the mode's rate and depth). The per-sample `ProcessMono` /
`Process` paths have no switch-on-mode logic. `SetSampleRate`
invalidates the cache so SR changes pick up new coefficients.

**Envelope** — ADSR. Linear attack, exponential decay/release toward
target. Full retrigger (Level → 0 on note-on). Coefficients cached
at the machine class level (`UpdateAdsrIfNeeded`) — recomputed only
when ADSR parameters change.

**LFO** — single global, triangle waveform, delay fade-in re-armed on
every new note. Rate cached (avoids `MathF.Pow` per sample).

**HPF** — 4-position bank (0/100/200/400 Hz), one-pole `input − lp` form,
mono on the post-mix bus.

**Chorus** — BBD-style stereo. Single LFO modulates two delay taps in
anti-phase. One-pole pre/post LPF emulates BBD bandwidth (~5 kHz).
Three modes:
- I: 0.513 Hz, 1.92 ms — slow lush
- II: 0.863 Hz, 1.92 ms — denser
- III: 9.91 Hz, 0.18 ms — iconic Juno warble

**Modulation depth ranges (current tuning):**
- DCO LFO (vibrato): max ±1 semitone (≈100 cents) — Juno-faithful.
  Was originally ±1 octave; corrected before scaffolding finalised.
- VCF Env Amount: max ±6 octaves
- VCF LFO: max ±4 octaves

The latter two are wide-but-musical — likely the obvious tuning
targets when A/B'ing against reference recordings.

**Anti-click envelope (per voice)** — fade gain (0..1) multiplied into the
voice's output. Ramps to its target by `1/64` per sample (~1.3 ms at
48 kHz). Three roles:

1. *Retrigger of audible voice* — Voice.Trigger sees the voice is still
   sounding, stages a pending trigger, sets fade target to 0. Once
   AntiClick hits zero, TickAntiClick runs the actual DoTrigger (resets
   DCO/filter/env), sets target back to 1 to fade in. Total perceived
   gap ~2 ms — inaudible but completely click-free. PedalTracker §4.2
   pattern.
2. *Initial transient on note-on* — fresh trigger starts at AntiClick=0
   and ramps to 1. Masks any DC offset from filter init or DCO phase-0
   mismatch.
3. *Gate-mode VCA smoothing* — in Gate mode the work loop drives
   AntiClickTarget from env stage so the binary 0/1 transitions become
   smooth ~1.3 ms ramps. Gate-mode `vca = 1` constant; AntiClick is
   the gate envelope. Env mode leaves AntiClickTarget alone — env
   itself shapes the sound.

NoteOff cancels any pending retrigger but lets the in-flight fade-out
continue (the user pressed and released a key faster than the deferred
trigger could land — keep going to silence rather than start a fresh
note).

**Per-voice drift** (analog-like detuning) — each voice has a small static
cent offset (component-tolerance simulation, max ±0.8 cents, hand-tuned
to sum to zero across the 6 voices) plus a slow random walk on top
(±0.5 cents, ~2 second time constant). Tuned for "tightly tuned vintage"
character — chord stacks stay clean rather than sounding detuned, but
single notes and held chords still have subtle analog motion. Updated
once per Work() block via lowpass-filtered LCG noise; per-sample cost
is one extra add into the existing `pitchOct` chain — no new FastPow2
calls. Drift evolves continuously regardless of key state.

**Transport handling** — IBuzz.Playing is polled at the top of every
Work() call. On the falling edge (was playing, now not), all active
voices receive NoteOff (env enters Release so they eventually free)
plus AntiClickTarget=0 (forces audible silence in ~1.3 ms regardless
of the patch's release time). Pending pattern events queued in the
same tick as the Stop edge are discarded as stale. Without this poll,
sustained notes ring forever after the user presses Stop because the
pattern engine pauses without sending NoteOff. PedalTracker §3 pattern.

**Sample format** — Generators write directly to ±32768 (PedalComp §1).
All internal DSP is at ±1.0 normalised; the final scale-and-emit happens
at the bottom of `Work()`.

---

## Critical implementation details

### Multi-track Note polling (Core §14)

`machine.parametersChanged` is keyed by parameter, not (parameter, track).
When two pattern tracks fire the same note parameter on the same row,
the second write overwrites the first dictionary entry — losing one
voice's note delivery. Workaround: from inside `SetNote`, read the
parameter's `pvalues` (`ConcurrentDictionary<int,int>`) via reflection
and recover any sibling tracks whose setter was dropped.

In Pedal Juno106:
- `_ownNoteParam` and `_ownNotePValues` cached after first `SetNote` call
- `TryInitNotePolling` does lazy init via reflection; `_polledInit`
  resets if `ParameterGroups` not ready (lazy retry per Core §15)
- `PollOtherTracks` runs from inside `SetNote` after staging the
  fired track's pending note — captures the entire row's data
  regardless of which setter Tick() iteration runs first

### ADSR coefficient caching (per Voice via shared call)

The Envelope class doesn't cache parameter values internally.
`PedalJuno106Machine.UpdateAdsrIfNeeded()` does the dirty check at the
machine level and calls `UpdateCoefficients` on every voice when any
A/D/S/R parameter changes. Cheap when stable.

Called BEFORE `ProcessPendingNotes()` so freshly-triggered voices
see valid coefficients on their first sample.

### Range applied as multiplier, not baked into voice frequency

`Voice.Trigger(byte midiNote)` sets `Frequency = MidiToHz(midiNote)` —
no range offset. The Work loop multiplies by `rangeFactor`
(0.5 / 1 / 2 for 16' / 8' / 4') each sample. This means changing
range during a held note retunes dynamically, which is the desired
behaviour.

### Build hygiene (Build §1.2 mandatory properties)

Every managed machine `.csproj` MUST include:
```xml
<DebugType>none</DebugType>
<DebugSymbols>false</DebugSymbols>
<GenerateDependencyFile>false</GenerateDependencyFile>
<NoWarn>MSB3277</NoWarn>
```
Already in place. ReBuzz only loads the `.dll`; `.pdb` and `.deps.json`
just clutter the gear folder.

---

## Known v1.0 limitations / refinement targets

When you sit down with reference recordings to A/B against:

1. **Filter character** — TPT topology with soft-clip feedback is the
   v1.0 best-guess. Tuning the saturation curve and resonance compression
   in `Filter.cs` is the obvious place to refine "Juno-ness".
2. **Chorus parameters** — rates and depths from public reverse-engineering.
   Mode III's iconic warble in particular may need tweaking. Pre/post
   BBD lowpass at 5 kHz is a guess.
3. **Modulation depth ranges** — VCF Env ±6 oct and VCF LFO ±4 oct are
   wide. Lower if too aggressive at moderate knob settings.
4. **Default patch** — the DefValues give a usable saw lead, but they're
   judgement calls. May want to re-balance once GUI tuning starts.

Functionally missing (v1.1 append-only candidates):
- Velocity
- Pitch bend
- Glide / portamento
- Hold / sustain pedal

---

## Next concrete steps (in order)

1. **Compile on Windows** with `dotnet build -c Release` from the project
   folder (assumes .NET 10 SDK). DLL auto-deploys to
   `C:\Program Files\ReBuzz\Gear\Generators` via the AfterBuild target.
   Edit `<ReBuzzPath>` in `.csproj` if your install lives elsewhere.
2. **Verify the machine appears** in ReBuzz's machine browser as
   "Pedal Juno106" with all 25 parameters labelled and ValueDescriptions
   rendering correctly (16'/8'/4', Off/I/II/III, etc.).
3. **Trigger a note** and confirm sound. Default patch should give a
   saw lead with mild filter envelope.
4. **Then** start A/B'ing against reference recordings — that drives
   any further DSP tuning.
5. **GUI** — open question, deferred. Faithful skeuomorphic Juno panel
   vs pragmatic labelled-slider grid. Do not start until the audio is
   solid.

---

## Pointers to canonical reference notes

These live in the project workspace (read-only, not in this folder):

- `ReBuzz_ManagedMachine_Notes_Core.md` (Core §1–§26) — managed
  machine plumbing, Note delivery, ParameterDecl reference, audio
  thread safety
- `ReBuzz_ManagedMachine_Notes_Build.md` — csproj rules, `.NET`
  suffix, preset XML format, MSB3277 noise
- `ReBuzz_ManagedMachine_Notes_PedalComp.md` — sample scaling,
  fast log/exp, coefficient caching, volatile float metering,
  `ParameterDecl.MinValue ≥ 0` rule
- `ReBuzz_ManagedMachine_Notes_PedalTracker.md` — pattern-data
  delivery details, sub-tick timing
- `ReBuzz_ManagedMachine_Notes_PedalMuter.md` — control machine
  patterns

References in code use the form `Core §N`, `Build §N`, `PedalComp §N`.
