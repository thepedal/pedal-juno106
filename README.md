# Pedal Juno106

Faithful Roland Juno-106 emulation as a managed ReBuzz generator machine.

**v1.0 — non-GUI scaffold.** All DSP and parameter wiring complete; uses
ReBuzz's default rack sliders. Skeuomorphic panel deferred to a later phase.

---

## Architecture

- **6-voice polyphony**, fixed. Pattern track index ↔ voice index (1:1).
- **Single global LFO** (Juno-faithful), triangle, with per-trigger fade-in.
- **Per-voice DCO + envelope + filter**.
- **DCO**: saw + pulse + sub (one octave below) + noise. PolyBLEP
  anti-aliased.
- **Filter**: TPT 4-pole ladder LPF with soft-clipped feedback. Resonance
  scales 0–127 → k 0–4.5; self-oscillates at max.
- **HPF**: 4-position one-pole, post-mix mono pre-chorus.
- **Chorus**: BBD-style stereo, modes I (~0.5 Hz lush) / II (~0.86 Hz
  denser) / III (~9.9 Hz iconic warble).
- **VCA**: Env or Gate mode.

Sample format: generators write to ±32768 (PedalComp §1). All internal DSP
is at ±1.0 normalised; the final scale-and-emit happens in `Work()`.

---

## Files

| File | Purpose |
|------|---------|
| `PedalJuno106.cs` | Machine class — parameters, setters, multi-track polling, Work loop |
| `Voice.cs`        | Per-voice container |
| `Dco.cs`          | Saw + pulse + sub + noise (PolyBLEP) |
| `Filter.cs`       | TPT 4-pole ladder LPF |
| `Hpf.cs`          | Global 4-position highpass |
| `Envelope.cs`     | ADSR — linear A, exponential D/R |
| `Lfo.cs`          | Triangle + fade-in delay |
| `Chorus.cs`       | BBD-style stereo chorus |
| `DspMath.cs`      | FastTan, FastPow2, FastDbToLin, SoftClip helpers |

---

## Building

Requires .NET 10 SDK on Windows and ReBuzz installed.

```
dotnet build -c Release
```

The project's `AfterBuild` target copies the DLL to
`C:\Program Files\ReBuzz\Gear\Generators` automatically. If your ReBuzz
install lives elsewhere, edit the `<ReBuzzPath>` property in
`PedalJuno106.csproj`.

If ReBuzz is running while you build, the copy step will fail silently
(`ContinueOnError=true`). Close ReBuzz and rebuild, or copy by hand.

---

## Deploying manually

Copy `bin/Release/net10.0-windows/Pedal Juno106.NET.dll` to
`C:\Program Files\ReBuzz\Gear\Generators`.

The machine appears in the browser as **Pedal Juno106** (the name comes
from `AssemblyName`, not `MachineDecl.Name` — see Notes_Build §2).

---

## Parameter list (declaration order — DO NOT REORDER)

Globals (1–25):

1. LFO Rate · 2. LFO Delay · 3. DCO LFO · 4. DCO PWM · 5. DCO PWM Src ·
6. DCO PW · 7. DCO Range · 8. DCO Pulse · 9. DCO Saw · 10. DCO Sub ·
11. DCO Noise · 12. HPF · 13. VCF Freq · 14. VCF Reso · 15. VCF Env Amt ·
16. VCF Env Pol · 17. VCF LFO · 18. VCF Key Trk · 19. VCA Mode ·
20. VCA Level · 21. Env A · 22. Env D · 23. Env S · 24. Env R · 25. Chorus

Track:

1. Note (with multi-track polling per Notes_Core §14)

Reordering breaks every existing preset (Notes_Build §3.3). v1.1+
parameters are appended only.

---

## Default patch

The DefValues give a playable saw lead with mild filter envelope:
saw on, 8' range, mid filter cutoff with positive envelope sweep,
fast attack, medium release, no chorus. Trigger any note and you
should hear sound.

---

## Known v1.0 limitations

- No velocity, pitch bend, glide, or hold (v1.1 candidates — append-only).
- No GUI (uses default ReBuzz parameter sliders).
- Filter character is the v1.0 best-guess — TPT topology with soft-clip
  feedback. Tuning the saturation curve and resonance compression is the
  obvious place to refine the "Juno-ness".
- Chorus mode parameters from public reverse-engineering; tuning the
  pre/post BBD lowpass cutoff and depth/rate may be worth A/B'ing
  against reference recordings.

---

## Files NOT shipped

- No `.pdb` (suppressed via `<DebugType>none</DebugType>`)
- No `.deps.json` (suppressed via `<GenerateDependencyFile>false</GenerateDependencyFile>`)

These would be ignored by ReBuzz at runtime and just clutter the gear
folder (Notes_Build §1).

---

## Preset bank

`Pedal Juno106_Presets.prs.xml` ships alongside the DLL — 48 presets
across 7 categories: Pads (8), Brass (5), Leads (8), Bass (8), Keys (6),
FX (6), Classic Juno (7). The csproj's deploy step copies it into
`ReBuzz\Gear\Generators` automatically when present, so they appear in
the machine's right-click menu after a rebuild.

To regenerate or add presets, edit `gen_presets.py` and run:

```
python3 gen_presets.py
```

Per Notes_Build §3.3, ReBuzz applies preset values by parameter **index**,
not name. **Don't reorder the parameters in `PedalJuno106.cs`** — every
existing preset would shift to the wrong parameters. New parameters in
v1.1+ must be appended at the end of the property list (and at the end
of `PARAM_INDEX` in the script).
