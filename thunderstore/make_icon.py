"""Draw the Thunderstore icon (256x256 PNG): a hooded green shroud with a glowing wisp. Pure Python, no PIL.

Usage: python make_icon.py <out.png>
"""
import math
import struct
import sys
import zlib

SIZE = 256
SS = 4  # supersampling per axis


def rgba(hex_color, a=255):
    h = hex_color.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


PLATE = rgba("#1b2420")
CLOAK = rgba("#2f6b3a")
CLOAK_LIGHT = rgba("#4c9a55")
CLOAK_EDGE = rgba("#d6dde8")
HOOD_SHADOW = rgba("#0d1210")
WISP = rgba("#e9fbff")
WISP_GLOW = rgba("#8fe3ff")


def rounded_rect(x, y, x0, y0, x1, y1, r):
    # Signed distance to a rounded rectangle (negative inside).
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r
    dx, dy = abs(x - cx) - hx, abs(y - cy) - hy
    return math.hypot(max(dx, 0), max(dy, 0)) + min(max(dx, dy), 0) - r


def circle(x, y, cx, cy, r):
    return math.hypot(x - cx, y - cy) - r


def segment(x, y, ax, ay, bx, by, r):
    px, py, vx, vy = x - ax, y - ay, bx - ax, by - ay
    t = max(0.0, min(1.0, (px * vx + py * vy) / (vx * vx + vy * vy)))
    return math.hypot(px - vx * t, py - vy * t) - r


def sample(x, y):
    """Colour at one sub-pixel; later layers paint over earlier ones."""
    color = (0, 0, 0, 0)
    if rounded_rect(x, y, 12, 12, 244, 244, 44) < 0:
        color = PLATE
    # Hooded shroud: a round hood over a robe that widens towards the bottom.
    half = 40 + max(0.0, y - 96) * 0.32
    robe = max(abs(x - 118) - half, 96 - y, y - 214)
    cloak = min(circle(x, y, 118, 92, 46), robe)
    if cloak < 6:
        color = CLOAK_EDGE
    if cloak < 0:
        color = CLOAK_LIGHT if x < 104 - (y - 96) * 0.15 else CLOAK
        if y > 140 and int((x - 118) / 22 + 100) % 2 == 0 and abs(x - 118) > 8:
            color = blend(color, rgba("#000000", 40))
    if circle(x, y, 118, 108, 25) < 0 and y > 90:
        color = HOOD_SHADOW
    # Wisp: a bright orb with a soft glow.
    d = circle(x, y, 196, 66, 0)
    if d < 40:
        color = blend(color, WISP_GLOW[:3] + (round(150 * (1 - d / 40) ** 2),))
    if d < 15:
        color = WISP
    return color


def blend(dst, src):
    a = src[3] / 255
    return tuple(round(dst[i] * (1 - a) + src[i] * a) for i in range(3)) + (dst[3],)


def render():
    rows = []
    for py in range(SIZE):
        row = bytearray([0])  # PNG filter type 0
        for px in range(SIZE):
            acc = [0, 0, 0, 0]
            for sy in range(SS):
                for sx in range(SS):
                    r, g, b, a = sample(px + (sx + 0.5) / SS, py + (sy + 0.5) / SS)
                    acc[0] += r * a
                    acc[1] += g * a
                    acc[2] += b * a
                    acc[3] += a
            n = SS * SS
            alpha = acc[3] / n
            if acc[3]:
                row += bytes((round(acc[0] / acc[3]), round(acc[1] / acc[3]), round(acc[2] / acc[3]), round(alpha)))
            else:
                row += bytes(4)
        rows.append(bytes(row))
    return b"".join(rows)


def write_png(path, raw):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)  # 8-bit RGBA
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


if __name__ == "__main__":
    write_png(sys.argv[1], render())
