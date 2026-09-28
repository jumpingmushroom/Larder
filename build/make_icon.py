"""Thunderstore icon: 256x256 PNG. A dark plate with a golden border, three round food
"slots" (health red, stamina yellow, eitr blue — the game's own stat colours) in a row, and a
green up arrow over the middle slot standing for "one dish makes the combo better". Written
without PIL, which the build box lacks.
Run from the repo root: python3 build/make_icon.py"""
import struct
import zlib

S = 256
SS = 3                       # supersample
W = S * SS

px = bytearray(W * W * 4)

def blend(x, y, r, g, b, a):
    if x < 0 or y < 0 or x >= W or y >= W:
        return
    i = (y * W + x) * 4
    ia = 1.0 - a
    px[i] = int(r * a + px[i] * ia)
    px[i + 1] = int(g * a + px[i + 1] * ia)
    px[i + 2] = int(b * a + px[i + 2] * ia)
    px[i + 3] = int(min(255, 255 * a + px[i + 3] * ia))

def rounded_rect(x0, y0, x1, y1, rad, col, alpha=1.0):
    for y in range(int(y0), int(y1)):
        for x in range(int(x0), int(x1)):
            dx = max(x0 + rad - x, 0, x - (x1 - 1 - rad))
            dy = max(y0 + rad - y, 0, y - (y1 - 1 - rad))
            if dx * dx + dy * dy <= rad * rad:
                blend(x, y, *col, alpha)

PLATE = (0x1c, 0x1a, 0x17)
BORDER = (0xc8, 0xa0, 0x50)
HP = (0xe0, 0x60, 0x60)
ST = (0xe0, 0xc0, 0x50)
EITR = (0x70, 0x70, 0xe0)
ARROW = (0x5a, 0xc8, 0x5a)

border_w = 10 * SS
rounded_rect(0, 0, W, W, 34 * SS, BORDER)
rounded_rect(border_w, border_w, W - border_w, W - border_w, 26 * SS, PLATE)

# three round food slots in a row
r = 30 * SS
cy = 150 * SS
for i, col in enumerate((HP, ST, EITR)):
    cx = (64 + i * 64) * SS
    rounded_rect(cx - r, cy - r, cx + r, cy + r, r, col)

# green up arrow above the middle slot: "one dish makes it better"
ax = 128 * SS
for i in range(int(40 * SS)):
    half = (40 * SS - i) * 0.9
    rounded_rect(ax - half, 50 * SS + i, ax + half, 50 * SS + i + 1, 0, ARROW)
rounded_rect(ax - 10 * SS, 90 * SS, ax + 10 * SS, 112 * SS, 0, ARROW)

# downsample
out = bytearray()
for y in range(S):
    row = bytearray([0])
    for x in range(S):
        r = g = b = a = 0
        for sy in range(SS):
            for sx in range(SS):
                i = ((y * SS + sy) * W + (x * SS + sx)) * 4
                r += px[i]; g += px[i + 1]; b += px[i + 2]; a += px[i + 3]
        n = SS * SS
        row += bytes((r // n, g // n, b // n, a // n))
    out += row

def chunk(tag, data):
    c = tag + data
    return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xffffffff)

png = b"\x89PNG\r\n\x1a\n"
png += chunk(b"IHDR", struct.pack(">IIBBBBB", S, S, 8, 6, 0, 0, 0))
png += chunk(b"IDAT", zlib.compress(bytes(out), 9))
png += chunk(b"IEND", b"")
open("thunderstore/icon.png", "wb").write(png)
print("wrote thunderstore/icon.png", S, "x", S, len(png), "bytes")
