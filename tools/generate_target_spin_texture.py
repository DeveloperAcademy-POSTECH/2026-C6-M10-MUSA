"""Rebuild the target's asymmetric spin-reference texture without third-party tools.

The single pale arrow and uneven colour bands expose direction and complete turns
on a cylindrical mesh. This is intentionally diagnostic art, not a monster skin.
"""

from pathlib import Path
import struct
import zlib


WIDTH = 1024
HEIGHT = 512
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/Lab/Art/Targets/TargetSpinGradient.png"
STOPS = (
    (0.00, (221, 112, 56)),
    (0.15, (181, 75, 57)),
    (0.39, (51, 111, 118)),
    (0.60, (27, 70, 85)),
    (0.82, (153, 88, 58)),
    (1.00, (221, 112, 56)),
)


def colour_at(u):
    for index in range(len(STOPS) - 1):
        start, a = STOPS[index]
        end, b = STOPS[index + 1]
        if u <= end:
            t = (u - start) / (end - start)
            t = t * t * (3.0 - 2.0 * t)
            return tuple(round(x + (y - x) * t) for x, y in zip(a, b))
    return STOPS[-1][1]


def pixel(x, y):
    u = x / (WIDTH - 1)
    v = y / (HEIGHT - 1)
    rgb = colour_at(u)

    # Unequal side markers keep the turning direction legible even when the
    # front-facing arrow is temporarily outside the visible half-cylinder.
    if 0.155 < u < 0.174:
        rgb = (255, 200, 103)
    elif 0.718 < u < 0.728:
        rgb = (19, 38, 51)
    elif 0.755 < u < 0.771 and v < 0.76:
        rgb = (250, 166, 92)

    # One unmistakable top-pointing marker. It is the only broad pale stripe.
    arrowhead = 0.071 < v < 0.265 and abs(u - 0.5) < ((v - 0.071) / 0.194) * 0.077
    shaft = 0.212 < v < 0.865 and abs(u - 0.5) < 0.033
    if arrowhead or shaft:
        rgb = (24, 48, 55) if abs(u - 0.5) > 0.027 and not arrowhead else (250, 235, 188)

    # Dark rings identify the top/bottom as the cylinder tilts; the reference
    # stripe remains clear between them.
    if v < 0.035 or v > 0.965:
        rgb = tuple(round(channel * 0.54) for channel in rgb)
    elif v < 0.055 or v > 0.945:
        rgb = tuple(round(channel * 0.78) for channel in rgb)
    return bytes(rgb)


def chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data) & 0xFFFFFFFF)


def main():
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    scanlines = bytearray()
    for y in range(HEIGHT):
        scanlines.append(0)  # PNG filter 0
        for x in range(WIDTH):
            scanlines.extend(pixel(x, y))
    image = b"\x89PNG\r\n\x1a\n"
    image += chunk(b"IHDR", struct.pack(">IIBBBBB", WIDTH, HEIGHT, 8, 2, 0, 0, 0))
    image += chunk(b"IDAT", zlib.compress(scanlines, 9))
    image += chunk(b"IEND", b"")
    OUTPUT.write_bytes(image)
    print(OUTPUT)


if __name__ == "__main__":
    main()
