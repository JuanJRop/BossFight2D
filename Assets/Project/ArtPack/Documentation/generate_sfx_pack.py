from pathlib import Path
import math
import random
import struct
import wave


SAMPLE_RATE = 44100
TAU = math.tau
RNG = random.Random(20260902)


def sample_count(seconds):
    return max(1, int(round(seconds * SAMPLE_RATE)))


def envelope(index, total, attack=0.01, release=0.08):
    time = index / SAMPLE_RATE
    duration = total / SAMPLE_RATE
    if attack > 0 and time < attack:
        return time / attack
    remaining = duration - time
    if release > 0 and remaining < release:
        return max(0.0, remaining / release)
    return 1.0


def silence(seconds):
    return [0.0] * sample_count(seconds)


def tone(seconds, start_frequency, end_frequency=None, volume=0.3,
         harmonics=(), attack=0.01, release=0.08):
    total = sample_count(seconds)
    end_frequency = start_frequency if end_frequency is None else end_frequency
    phase = 0.0
    result = []
    for index in range(total):
        progress = index / max(1, total - 1)
        frequency = start_frequency + (end_frequency - start_frequency) * progress
        phase += TAU * frequency / SAMPLE_RATE
        value = math.sin(phase)
        for ratio, gain in harmonics:
            value += math.sin(phase * ratio) * gain
        value *= volume * envelope(index, total, attack, release)
        result.append(value)
    return result


def noise(seconds, volume=0.2, smoothing=0.0, attack=0.005, release=0.08):
    total = sample_count(seconds)
    previous = 0.0
    result = []
    for index in range(total):
        raw = RNG.uniform(-1.0, 1.0)
        previous = previous * smoothing + raw * (1.0 - smoothing)
        result.append(previous * volume * envelope(index, total, attack, release))
    return result


def add_at(target, segment, offset_seconds):
    offset = int(round(offset_seconds * SAMPLE_RATE))
    if offset < 0:
        raise ValueError("offset_seconds must be positive")
    needed = offset + len(segment)
    if needed > len(target):
        target.extend([0.0] * (needed - len(target)))
    for index, value in enumerate(segment):
        target[offset + index] += value


def mix(*segments):
    result = []
    for segment in segments:
        add_at(result, segment, 0.0)
    return result


def concat(*segments):
    result = []
    for segment in segments:
        result.extend(segment)
    return result


def normalize(samples, peak=0.92):
    highest = max((abs(value) for value in samples), default=0.0)
    if highest == 0.0:
        return samples
    gain = peak / highest
    return [value * gain for value in samples]


def write_wav(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = bytearray()
    for value in normalize(samples):
        value = max(-1.0, min(1.0, value))
        pcm.extend(struct.pack("<h", int(value * 32767)))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(bytes(pcm))


def menu_confirm():
    return concat(
        tone(0.08, 560, 760, 0.28, ((2, 0.18),)),
        silence(0.025),
        tone(0.13, 760, 1060, 0.3, ((2, 0.16),)),
    )


def menu_cancel():
    return concat(
        tone(0.12, 520, 360, 0.27, ((2, 0.14),)),
        silence(0.025),
        tone(0.16, 360, 220, 0.3, ((2, 0.12),)),
    )


def player_shot():
    return mix(
        tone(0.13, 1800, 520, 0.36, ((2, 0.18), (3, 0.08)), 0.002, 0.06),
        noise(0.12, 0.28, 0.18, 0.001, 0.05),
    )


def player_damage():
    return mix(
        tone(0.26, 190, 72, 0.34, ((2, 0.22), (3, 0.12)), 0.002, 0.12),
        noise(0.22, 0.38, 0.35, 0.001, 0.12),
    )


def player_dash():
    return mix(
        tone(0.42, 170, 1350, 0.24, ((2, 0.1),), 0.015, 0.16),
        noise(0.36, 0.32, 0.82, 0.005, 0.18),
    )


def player_reload():
    click = mix(
        tone(0.045, 980, 620, 0.25, ((3, 0.18),), 0.001, 0.02),
        noise(0.04, 0.18, 0.25, 0.001, 0.02),
    )
    chamber = mix(
        tone(0.09, 620, 210, 0.25, ((2, 0.18),), 0.001, 0.04),
        noise(0.08, 0.22, 0.32, 0.001, 0.04),
    )
    return concat(click, silence(0.11), chamber, silence(0.07), click, silence(0.06), chamber)


def boss_emerge():
    result = mix(
        tone(0.95, 42, 98, 0.42, ((2, 0.25), (3, 0.12)), 0.12, 0.28),
        noise(0.9, 0.26, 0.94, 0.08, 0.3),
    )
    add_at(result, tone(0.18, 380, 720, 0.2, ((2, 0.12),)), 0.65)
    return result


def boss_telegraph():
    pulse = tone(0.16, 270, 430, 0.26, ((2, 0.15),), 0.005, 0.05)
    result = concat(pulse, silence(0.14), pulse, silence(0.11), pulse, silence(0.08))
    add_at(result, tone(0.72, 180, 820, 0.12, ((2, 0.08),), 0.1, 0.2), 0.0)
    return result


def boss_projectile():
    return mix(
        tone(0.22, 2100, 480, 0.3, ((2, 0.17), (3, 0.08)), 0.002, 0.1),
        noise(0.2, 0.2, 0.58, 0.001, 0.1),
    )


def rock_impact():
    result = mix(
        tone(0.34, 78, 34, 0.44, ((2, 0.2), (3, 0.1)), 0.001, 0.2),
        noise(0.3, 0.48, 0.2, 0.001, 0.2),
    )
    add_at(result, tone(0.08, 1200, 260, 0.18, ((2, 0.1),), 0.001, 0.04), 0.02)
    return result


def laser_warning():
    result = tone(1.15, 190, 920, 0.22, ((2, 0.12), (3, 0.05)), 0.04, 0.2)
    pulse = tone(0.09, 1200, 720, 0.2, ((2, 0.12),), 0.001, 0.03)
    for offset in (0.12, 0.39, 0.66, 0.93):
        add_at(result, pulse, offset)
    return result


def boss_phase_transition():
    result = mix(
        tone(2.7, 38, 29, 0.42, ((2, 0.26), (3, 0.13)), 0.15, 0.4),
        noise(2.5, 0.18, 0.96, 0.2, 0.5),
    )
    add_at(result, tone(1.6, 120, 680, 0.22, ((2, 0.15),), 0.12, 0.35), 0.35)
    add_at(result, tone(0.42, 420, 1040, 0.24, ((2, 0.12),), 0.01, 0.12), 1.7)
    return result


def boss_defeat():
    result = mix(
        tone(1.8, 220, 38, 0.38, ((2, 0.22), (3, 0.12)), 0.01, 0.48),
        noise(1.45, 0.32, 0.55, 0.02, 0.4),
    )
    add_at(result, tone(0.6, 880, 120, 0.2, ((2, 0.1),), 0.005, 0.25), 0.2)
    return result


def pickup_health():
    notes = (523, 659, 784, 1046)
    segments = []
    for frequency in notes:
        segments.extend((tone(0.11, frequency, frequency * 1.03, 0.22, ((2, 0.14),)), silence(0.025)))
    return concat(*segments)


def pickup_mana():
    result = []
    for frequency, offset in ((660, 0.0), (990, 0.08), (1320, 0.16), (1760, 0.24)):
        add_at(result, tone(0.33, frequency, frequency * 1.05, 0.18, ((2, 0.18), (3, 0.09)), 0.005, 0.16), offset)
    return result


def overdrive_activate():
    result = mix(
        tone(0.95, 180, 1450, 0.3, ((2, 0.18), (3, 0.08)), 0.02, 0.25),
        tone(0.95, 270, 2160, 0.18, ((2, 0.12),), 0.03, 0.22),
    )
    add_at(result, tone(0.55, 1046, 1568, 0.22, ((2, 0.13),), 0.005, 0.2), 0.58)
    return result


def victory_sting():
    chord = mix(
        tone(0.72, 523, 523, 0.2, ((2, 0.12),)),
        tone(0.72, 659, 659, 0.18, ((2, 0.1),)),
        tone(0.72, 784, 784, 0.16, ((2, 0.08),)),
    )
    return concat(chord, silence(0.06), tone(0.62, 784, 1046, 0.26, ((2, 0.13),), 0.01, 0.22))


def defeat_sting():
    return concat(
        tone(0.42, 392, 260, 0.24, ((2, 0.14),), 0.01, 0.18),
        silence(0.04),
        tone(0.75, 260, 98, 0.28, ((2, 0.16), (3, 0.08)), 0.01, 0.32),
    )


def arena_ambient_loop():
    duration = 12.0
    result = mix(
        tone(duration, 48, 48, 0.12, ((2, 0.3), (3, 0.15)), 0.8, 0.8),
        tone(duration, 73, 73, 0.05, ((2, 0.18),), 0.8, 0.8),
        noise(duration, 0.035, 0.985, 0.8, 0.8),
    )
    machinery_pulse = tone(0.55, 120, 82, 0.06, ((2, 0.1),), 0.06, 0.18)
    for offset in (0.8, 3.6, 6.4, 9.2):
        add_at(result, machinery_pulse, offset)
    warning_glint = tone(0.12, 860, 720, 0.035, ((2, 0.12),), 0.002, 0.05)
    for offset in (2.1, 5.0, 8.1, 10.7):
        add_at(result, warning_glint, offset)
    return result


def main():
    art_pack = Path(__file__).resolve().parents[1]
    sfx_root = art_pack / "Audio" / "SFX"
    music_root = art_pack / "Audio" / "Music"
    sounds = {
        "menu_confirm": menu_confirm,
        "menu_cancel": menu_cancel,
        "player_shot": player_shot,
        "player_damage": player_damage,
        "player_dash": player_dash,
        "player_reload": player_reload,
        "boss_emerge": boss_emerge,
        "boss_telegraph": boss_telegraph,
        "boss_projectile": boss_projectile,
        "rock_impact": rock_impact,
        "laser_warning": laser_warning,
        "boss_phase_transition": boss_phase_transition,
        "boss_defeat": boss_defeat,
        "pickup_health": pickup_health,
        "pickup_mana": pickup_mana,
        "overdrive_activate": overdrive_activate,
        "victory_sting": victory_sting,
        "defeat_sting": defeat_sting,
    }
    for name, builder in sounds.items():
        path = sfx_root / f"{name}.wav"
        write_wav(path, builder())
        print(f"created {path.name}")
    ambient_path = music_root / "arena_ambient_loop.wav"
    write_wav(ambient_path, arena_ambient_loop())
    print(f"created {ambient_path.name}")


if __name__ == "__main__":
    main()
