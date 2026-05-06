#!/usr/bin/env python3
"""
Generate Pedal Juno106_Presets.prs.xml — 48 presets covering pads, brass,
leads, bass, keys, FX, and classic Juno-style sounds.

Per Build §3.4: keep this script alongside the source but DON'T deploy it.
The deployed bundle is just the .prs.xml + .dll.

Per Build §3.3: parameter index is the declaration-order position on the
machine class. Reordering ParameterDecls in the source breaks every preset
in this file. Append-only for v1.1+.

Each preset only lists overrides from DEFAULTS — keeps the script readable
and lets new globals added in a future version inherit their DefValue
without touching every preset.
"""

# ─────────────────────────────────────────────────────────────────────────
# Parameter declaration order — must match PedalJuno106.cs exactly.
# ─────────────────────────────────────────────────────────────────────────
PARAM_INDEX = {
    "LFO Rate":      0,
    "LFO Delay":     1,
    "DCO LFO":       2,
    "DCO PWM":       3,
    "DCO PWM Src":   4,
    "DCO PW":        5,
    "DCO Range":     6,
    "DCO Pulse":     7,
    "DCO Saw":       8,
    "DCO Sub":       9,
    "DCO Noise":    10,
    "HPF":          11,
    "VCF Freq":     12,
    "VCF Reso":     13,
    "VCF Env Amt":  14,
    "VCF Env Pol":  15,
    "VCF LFO":      16,
    "VCF Key Trk":  17,
    "VCA Mode":     18,
    "VCA Level":    19,
    "Env A":        20,
    "Env D":        21,
    "Env S":        22,
    "Env R":        23,
    "Chorus":       24,
}

DEFAULTS = {
    "LFO Rate":     64,
    "LFO Delay":     0,
    "DCO LFO":       0,
    "DCO PWM":       0,
    "DCO PWM Src":   0,   # 0=Manual, 1=LFO, 2=Env
    "DCO PW":       64,
    "DCO Range":     1,   # 0=16', 1=8', 2=4'
    "DCO Pulse":     0,   # bool: 0/1
    "DCO Saw":       1,   # bool: 0/1
    "DCO Sub":       0,
    "DCO Noise":     0,
    "HPF":           0,   # 0=Off, 1/2/3=positions
    "VCF Freq":    100,
    "VCF Reso":     30,
    "VCF Env Amt":  80,
    "VCF Env Pol":   0,   # 0=+, 1=-
    "VCF LFO":       0,
    "VCF Key Trk":  64,
    "VCA Mode":      0,   # 0=Env, 1=Gate
    "VCA Level":   100,
    "Env A":         5,
    "Env D":        60,
    "Env S":        80,
    "Env R":        40,
    "Chorus":        0,   # 0=Off, 1=I, 2=II, 3=III
}

# ─────────────────────────────────────────────────────────────────────────
# 48 PRESETS — each dict lists overrides only.
# ─────────────────────────────────────────────────────────────────────────
PRESETS = {
    # ─── PADS (8) ────────────────────────────────────────────────────────
    "Pad — Lush": {
        "VCF Freq": 72, "VCF Reso": 12, "VCF Env Amt": 25,
        "Env A": 75, "Env D": 80, "Env S": 110, "Env R": 90,
        "Chorus": 2, "LFO Rate": 48,
    },
    "Pad — Hollow": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 90,
        "DCO PWM": 35, "DCO PWM Src": 1,
        "VCF Freq": 70, "VCF Reso": 18, "VCF Env Amt": 15,
        "Env A": 80, "Env D": 80, "Env S": 110, "Env R": 85,
        "Chorus": 1, "LFO Rate": 40,
    },
    "Pad — Vintage Strings": {
        "DCO Sub": 70, "DCO LFO": 8,
        "VCF Freq": 80, "VCF Reso": 12, "VCF Env Amt": 15,
        "Env A": 65, "Env D": 80, "Env S": 115, "Env R": 90,
        "Chorus": 2, "LFO Rate": 55, "LFO Delay": 35,
    },
    "Pad — Air": {
        "DCO Noise": 25, "HPF": 1,
        "VCF Freq": 88, "VCF Reso": 8, "VCF Env Amt": 15,
        "Env A": 95, "Env D": 90, "Env S": 110, "Env R": 110,
        "Chorus": 3,
    },
    "Pad — Sweep": {
        "VCF Freq": 70, "VCF Reso": 22, "VCF Env Amt": 25, "VCF LFO": 75,
        "Env A": 90, "Env D": 80, "Env S": 115, "Env R": 100,
        "Chorus": 2, "LFO Rate": 22,
    },
    "Pad — Choir": {
        "DCO LFO": 18, "LFO Delay": 45,
        "VCF Freq": 75, "VCF Reso": 18, "VCF Env Amt": 12,
        "Env A": 80, "Env D": 80, "Env S": 115, "Env R": 90,
        "Chorus": 2, "LFO Rate": 55,
    },
    "Pad — Wide Stack": {
        "DCO Sub": 60,
        "VCF Freq": 70, "VCF Reso": 12, "VCF Env Amt": 22, "VCF Key Trk": 90,
        "Env A": 80, "Env D": 80, "Env S": 115, "Env R": 90,
        "Chorus": 2,
    },
    "Pad — Dark Drone": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 80,
        "VCF Freq": 45, "VCF Reso": 22, "VCF Env Amt": 10,
        "Env A": 110, "Env D": 120, "Env S": 115, "Env R": 120,
        "Chorus": 1,
    },

    # ─── BRASS (5) ───────────────────────────────────────────────────────
    "Brass — Synth": {
        "DCO Pulse": 1, "DCO PW": 70,
        "VCF Freq": 58, "VCF Reso": 18, "VCF Env Amt": 75,
        "Env A": 18, "Env D": 70, "Env S": 80, "Env R": 50,
    },
    "Brass — Soft": {
        "VCF Freq": 65, "VCF Reso": 12, "VCF Env Amt": 50,
        "Env A": 38, "Env D": 70, "Env S": 90, "Env R": 60,
        "Chorus": 1,
    },
    "Brass — Stack": {
        "DCO Sub": 50,
        "VCF Freq": 60, "VCF Reso": 18, "VCF Env Amt": 75,
        "Env A": 20, "Env D": 70, "Env S": 80, "Env R": 50,
        "Chorus": 2,
    },
    "Brass — Punchy": {
        "DCO Pulse": 1, "DCO PW": 72,
        "VCF Freq": 55, "VCF Reso": 25, "VCF Env Amt": 90,
        "Env A": 8, "Env D": 50, "Env S": 70, "Env R": 40,
    },
    "Brass — Mellow": {
        "VCF Freq": 70, "VCF Reso": 12, "VCF Env Amt": 42,
        "Env A": 50, "Env D": 80, "Env S": 100, "Env R": 70,
        "Chorus": 1,
    },

    # ─── LEADS (8) ───────────────────────────────────────────────────────
    "Lead — Saw": {
        "DCO LFO": 14, "LFO Delay": 40,
        "VCF Freq": 85, "VCF Reso": 22, "VCF Env Amt": 30,
        "Env A": 5, "Env D": 60, "Env S": 100, "Env R": 40,
        "Chorus": 1, "LFO Rate": 55,
    },
    "Lead — Square": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 64,
        "VCF Freq": 80, "VCF Reso": 18, "VCF Env Amt": 30,
        "Env A": 5, "Env D": 60, "Env S": 100, "Env R": 40,
        "Chorus": 1,
    },
    "Lead — Sync": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 92,
        "VCF Freq": 70, "VCF Reso": 42, "VCF Env Amt": 70,
        "Env A": 5, "Env D": 60, "Env S": 80, "Env R": 40,
    },
    "Lead — Acid": {
        "VCF Freq": 50, "VCF Reso": 110, "VCF Env Amt": 95,
        "Env A": 5, "Env D": 70, "Env S": 70, "Env R": 50,
    },
    "Lead — Bright": {
        "VCF Freq": 110, "VCF Reso": 10, "VCF Env Amt": 10,
        "Env A": 5, "Env D": 60, "Env S": 110, "Env R": 50,
        "Chorus": 2,
    },
    "Lead — PWM": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 64,
        "DCO PWM": 85, "DCO PWM Src": 1,
        "VCF Freq": 85, "VCF Reso": 20, "VCF Env Amt": 20,
        "Env A": 10, "Env D": 60, "Env S": 110, "Env R": 50,
        "Chorus": 2, "LFO Rate": 50,
    },
    "Lead — Mono": {
        "DCO Sub": 30,
        "VCF Freq": 78, "VCF Reso": 30, "VCF Env Amt": 40,
        "Env A": 5, "Env D": 60, "Env S": 90, "Env R": 40,
    },
    "Lead — Cosmic": {
        "VCF Freq": 75, "VCF Reso": 42, "VCF Env Amt": 22, "VCF LFO": 60,
        "Env A": 10, "Env D": 60, "Env S": 100, "Env R": 50,
        "Chorus": 3, "LFO Rate": 70,
    },

    # ─── BASS (8) ────────────────────────────────────────────────────────
    "Bass — Sub": {
        "DCO Saw": 0, "DCO Sub": 110,
        "DCO Range": 0,
        "VCF Freq": 45, "VCF Reso": 10, "VCF Env Amt": 10,
        "Env A": 0, "Env D": 70, "Env S": 110, "Env R": 20,
    },
    "Bass — Saw": {
        "DCO Sub": 40, "DCO Range": 0,
        "VCF Freq": 50, "VCF Reso": 22, "VCF Env Amt": 30,
        "Env A": 0, "Env D": 60, "Env S": 90, "Env R": 22,
    },
    "Bass — Pulse": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 70,
        "DCO Range": 0,
        "VCF Freq": 55, "VCF Reso": 18, "VCF Env Amt": 22,
        "Env A": 0, "Env D": 60, "Env S": 90, "Env R": 22,
    },
    "Bass — Acid": {
        "DCO Range": 0,
        "VCF Freq": 38, "VCF Reso": 110, "VCF Env Amt": 105,
        "Env A": 0, "Env D": 50, "Env S": 18, "Env R": 15,
    },
    "Bass — Hard": {
        "DCO Sub": 50, "DCO Range": 0,
        "VCF Freq": 45, "VCF Reso": 28, "VCF Env Amt": 80,
        "Env A": 0, "Env D": 40, "Env S": 70, "Env R": 15,
    },
    "Bass — Funky": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 72,
        "DCO Range": 0,
        "VCF Freq": 50, "VCF Reso": 32, "VCF Env Amt": 70,
        "Env A": 0, "Env D": 30, "Env S": 40, "Env R": 15,
    },
    "Bass — Stack": {
        "DCO Pulse": 1, "DCO PW": 70,
        "DCO Range": 0,
        "VCF Freq": 55, "VCF Reso": 22, "VCF Env Amt": 60, "VCF Key Trk": 80,
        "Env A": 0, "Env D": 40, "Env S": 70, "Env R": 18,
    },
    "Bass — Wobble": {
        "DCO Range": 0,
        "VCF Freq": 50, "VCF Reso": 50, "VCF Env Amt": 22, "VCF LFO": 65,
        "Env A": 0, "Env D": 60, "Env S": 100, "Env R": 22,
        "LFO Rate": 70,
    },

    # ─── KEYS / PLUCKS (6) ───────────────────────────────────────────────
    "Keys — Bright Pluck": {
        "VCF Freq": 60, "VCF Reso": 22, "VCF Env Amt": 80,
        "Env A": 0, "Env D": 40, "Env S": 12, "Env R": 30,
        "Chorus": 1,
    },
    "Keys — Soft Pluck": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 64,
        "VCF Freq": 65, "VCF Reso": 12, "VCF Env Amt": 50,
        "Env A": 0, "Env D": 50, "Env S": 22, "Env R": 40,
        "Chorus": 2,
    },
    "Keys — Bell": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 100,
        "VCF Freq": 90, "VCF Reso": 32, "VCF Env Amt": 40,
        "Env A": 0, "Env D": 70, "Env S": 12, "Env R": 50,
        "Chorus": 2,
    },
    "Keys — Mallet": {
        "VCF Freq": 55, "VCF Reso": 22, "VCF Env Amt": 60,
        "Env A": 0, "Env D": 38, "Env S": 0, "Env R": 22,
        "Chorus": 2,
    },
    "Keys — EP Stack": {
        "DCO Sub": 40,
        "VCF Freq": 65, "VCF Reso": 15, "VCF Env Amt": 50,
        "Env A": 0, "Env D": 70, "Env S": 40, "Env R": 50,
        "Chorus": 2,
    },
    "Keys — Funky Clav": {
        "DCO Saw": 0, "DCO Pulse": 1, "DCO PW": 92,
        "VCF Freq": 60, "VCF Reso": 32, "VCF Env Amt": 70,
        "Env A": 0, "Env D": 30, "Env S": 12, "Env R": 22,
    },

    # ─── FX / SPECIAL (6) ────────────────────────────────────────────────
    "FX — White Noise Pad": {
        "DCO Saw": 0, "DCO Noise": 110,
        "VCF Freq": 80, "VCF Reso": 22, "VCF Env Amt": 22,
        "Env A": 80, "Env D": 80, "Env S": 110, "Env R": 100,
        "Chorus": 2,
    },
    "FX — Sci-Fi Sweep": {
        "VCF Freq": 50, "VCF Reso": 80, "VCF Env Amt": 65, "VCF LFO": 100,
        "Env A": 110, "Env D": 120, "Env S": 120, "Env R": 120,
        "Chorus": 3, "LFO Rate": 18,
    },
    "FX — Drone": {
        "DCO Sub": 80,
        "VCF Freq": 70, "VCF Reso": 32, "VCF Env Amt": 8,
        "Env A": 120, "Env D": 127, "Env S": 127, "Env R": 127,
        "Chorus": 1,
    },
    "FX — Filter Pulse": {
        "VCF Freq": 60, "VCF Reso": 42, "VCF Env Amt": 12, "VCF LFO": 85,
        "VCA Mode": 1,
        "Env A": 0, "Env D": 10, "Env S": 100, "Env R": 10,
        "LFO Rate": 80,
    },
    "FX — Vibrato Lead": {
        "DCO LFO": 75, "LFO Delay": 30,
        "VCF Freq": 85, "VCF Reso": 18, "VCF Env Amt": 22,
        "Env A": 10, "Env D": 60, "Env S": 110, "Env R": 40,
        "Chorus": 1, "LFO Rate": 55,
    },
    "FX — Wobble Pad": {
        "DCO Sub": 40,
        "VCF Freq": 60, "VCF Reso": 32, "VCF Env Amt": 22, "VCF LFO": 80,
        "Env A": 80, "Env D": 80, "Env S": 110, "Env R": 90,
        "Chorus": 2, "LFO Rate": 30,
    },

    # ─── CLASSIC JUNO (7) ────────────────────────────────────────────────
    "Classic — Hoover": {
        "DCO Pulse": 1, "DCO PW": 70, "DCO Sub": 40,
        "DCO LFO": 22,
        "VCF Freq": 70, "VCF Reso": 42, "VCF Env Amt": 72,
        "Env A": 12, "Env D": 60, "Env S": 80, "Env R": 40,
        "Chorus": 2, "LFO Rate": 50,
    },
    "Classic — Atomic": {
        "VCF Freq": 40, "VCF Reso": 50, "VCF Env Amt": 105,
        "Env A": 120, "Env D": 120, "Env S": 120, "Env R": 120,
        "Chorus": 3, "LFO Rate": 12,
    },
    "Classic — Trance Pad": {
        "DCO Sub": 30,
        "VCF Freq": 55, "VCF Reso": 42, "VCF Env Amt": 80, "VCF LFO": 45,
        "Env A": 100, "Env D": 100, "Env S": 120, "Env R": 110,
        "Chorus": 3, "LFO Rate": 40,
    },
    "Classic — Polysynth": {
        "DCO Sub": 50,
        "VCF Freq": 75, "VCF Reso": 15, "VCF Env Amt": 22,
        "Env A": 10, "Env D": 70, "Env S": 110, "Env R": 60,
        "Chorus": 2,
    },
    "Classic — Eno Drone": {
        "VCF Freq": 50, "VCF Reso": 22, "VCF Env Amt": 10, "VCF LFO": 30,
        "Env A": 120, "Env D": 127, "Env S": 120, "Env R": 127,
        "Chorus": 1, "LFO Rate": 12,
    },
    "Classic — Vintage Lead": {
        "DCO LFO": 10, "LFO Delay": 50,
        "VCF Freq": 80, "VCF Reso": 22, "VCF Env Amt": 30,
        "Env A": 10, "Env D": 60, "Env S": 100, "Env R": 50,
        "Chorus": 1, "LFO Rate": 55,
    },
    "Classic — Juno Brass": {
        "DCO Pulse": 1, "DCO PW": 70,
        "VCF Freq": 60, "VCF Reso": 22, "VCF Env Amt": 80,
        "Env A": 22, "Env D": 70, "Env S": 80, "Env R": 50,
        "Chorus": 1,
    },
}

# ─────────────────────────────────────────────────────────────────────────
# Validation + emit
# ─────────────────────────────────────────────────────────────────────────

def xml_attr(s):
    """Conservatively escape characters that XML attribute values can't carry."""
    return (str(s).replace('&', '&amp;')
                  .replace('"', '&quot;')
                  .replace('<', '&lt;')
                  .replace('>', '&gt;'))


def validate():
    # All declared keys appear in DEFAULTS (and vice versa)
    if set(PARAM_INDEX.keys()) != set(DEFAULTS.keys()):
        diff = set(PARAM_INDEX.keys()) ^ set(DEFAULTS.keys())
        raise ValueError(f'PARAM_INDEX vs DEFAULTS mismatch: {diff}')

    # Every preset only references known parameters; every value is an int
    for name, overrides in PRESETS.items():
        for k, v in overrides.items():
            if k not in PARAM_INDEX:
                raise ValueError(f'Preset "{name}" references unknown param "{k}"')
            if not isinstance(v, int):
                raise ValueError(f'Preset "{name}" param "{k}" is not int: {v!r}')

    # Preset names must be unique (Python dict already enforces, but guard
    # against accidental case-only differences if a future edit slips in)
    lower = [n.lower() for n in PRESETS.keys()]
    if len(lower) != len(set(lower)):
        raise ValueError('Preset names collide case-insensitively')


def emit():
    out = ['<?xml version="1.0" encoding="utf-8"?>']
    out.append('<PresetDictionary>')
    for name, overrides in PRESETS.items():
        full = {**DEFAULTS, **overrides}
        out.append(f'  <Item Key="{xml_attr(name)}">')
        out.append('    <Preset Machine="Pedal Juno106">')
        out.append('      <Parameters>')
        for param_name, idx in PARAM_INDEX.items():
            value = full[param_name]
            out.append(
                f'        <Parameter Name="{xml_attr(param_name)}" '
                f'Group="1" Index="{idx}" Track="0" '
                f'Value="{value}" />'
            )
        out.append('      </Parameters>')
        out.append('      <Attributes />')
        out.append('      <Comment></Comment>')
        out.append('    </Preset>')
        out.append('  </Item>')
    out.append('</PresetDictionary>')
    return '\n'.join(out) + '\n'


if __name__ == '__main__':
    import os
    validate()
    here = os.path.dirname(os.path.abspath(__file__))
    out_path = os.path.join(here, 'Pedal Juno106_Presets.prs.xml')
    # UTF-8 with BOM per Build §3.1 — em-dashes in preset names need it
    with open(out_path, 'w', encoding='utf-8-sig') as f:
        f.write(emit())
    print(f'Wrote {len(PRESETS)} presets → {out_path}')
