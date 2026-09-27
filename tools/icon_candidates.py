"""Draws three candidate Thunderstore icons for Varda, and a lineup to pick from.

    python tools/icon_candidates.py [out_dir]

Writes a.png, b.png, c.png (256x256) and lineup.png into out_dir, which defaults to
<temp>/varda-icons so nothing lands in the repo until a pick is made. Needs Pillow only.


THE STYLE BEING MATCHED

Stund's and Kvedja's icons, measured rather than eyeballed:

    background   vertical linear gradient, (170,168,165) at the top row to (126,125,123)
                 at the bottom, constant across each row, square corners
    body         one flat mass in (65,64,63), no outline and no drop shadow
    accent       exactly one element in (191,133,74)
    detail       (110,109,107) for the brighter marks, (92,91,89) for the quieter ones
    strokes      6 px (Stund's hour ticks), 10 px (its hour hand), 15-17 px (Kvedja's bars),
                 always with round ends
    margin       the body sits 34-39 px in from the sides: Stund's disc spans 39..217,
                 Kvedja's panel 34..222

Kvedja is drawn hard-edged and Stund antialiased; these follow Stund, the newer of the two.
"""
import math
import os
import sys
import tempfile
from PIL import Image, ImageDraw

N = 256
S = 4                     # supersample while drawing

BG_TOP = (170, 168, 165)
BG_BOTTOM = (126, 125, 123)
INK = (65, 64, 63)
ACCENT = (191, 133, 74)
LIGHT = (110, 109, 107)
DIM = (92, 91, 89)


class Layer:
    """One flat colour, drawn as a supersampled coverage mask."""

    def __init__(self, colour):
        self.colour = colour
        self.img = Image.new("L", (N * S, N * S), 0)
        self.d = ImageDraw.Draw(self.img)

    def poly(self, pts, fill=255):
        self.d.polygon([(x * S, y * S) for x, y in pts], fill=fill)

    def circle(self, cx, cy, r, fill=255):
        self.d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=fill)

    def rect(self, x0, y0, x1, y1, r=0, fill=255):
        self.d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r * S, fill=fill)

    def stroke(self, pts, w, fill=255):
        """A polyline with round caps and joints, the way every stroke in the set ends."""
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            self.d.line([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill, width=round(w * S))
        for x, y in pts:
            self.circle(x, y, w / 2, fill)


def background():
    img = Image.new("RGB", (N, N))
    for y in range(N):
        t = y / (N - 1)
        c = tuple(round(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3))
        img.paste(c, (0, y, N, y + 1))
    return img


def compose(layers):
    img = background()
    for layer in layers:
        mask = layer.img.resize((N, N), Image.LANCZOS)
        img.paste(layer.colour, (0, 0, N, N), mask)
    return img


def pebble(cx, cy, hw, hh, rot=0.0, n=2.6, flat=0.0, steps=240):
    """A rounded stone: a superellipse, rotated a few degrees so the stack is not machined.
    `flat` squashes the underside, which is how a stone that has been sat on looks."""
    r = math.radians(rot)
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        c, s = math.cos(t), math.sin(t)
        x = hw * math.copysign(abs(c) ** (2 / n), c)
        y = hh * math.copysign(abs(s) ** (2 / n), s)
        if y > 0:
            y *= 1 - flat
        pts.append((cx + x * math.cos(r) - y * math.sin(r), cy + x * math.sin(r) + y * math.cos(r)))
    return pts


def arch(cx, top, half, bottom, steps=90):
    """A doorway: a half circle on two straight jambs, closed along the floor."""
    cy = top + half
    pts = [(cx - half, bottom)]
    for i in range(steps + 1):
        a = math.pi + math.pi * i / steps
        pts.append((cx + half * math.cos(a), cy + half * math.sin(a)))
    pts.append((cx + half, bottom))
    return pts


# ------------------------------------------------------------------- designs

def design_a():
    """The cairn itself, a varda: a pile of stones somebody left where they had been, with
    the top stone lit as the mark. A pile three wide, not a balanced column, because a single
    column of flat stones reads as a spa's zen stack rather than a waymark."""
    ink = Layer(INK)
    cap = Layer(ACCENT)
    # Bottom row, middle row over the valleys, one stone, then the mark. The gaps are 7 px at
    # their narrowest, which is the least that still shows as a gap at 64.
    for cx, cy, hw, hh, rot in (
        (64, 198, 30, 24, -4), (127, 200, 26, 22, 3), (190, 197, 30, 25, -6),
        (95, 148, 31, 24, 5), (164, 149, 31, 23, -3),
        (130, 100, 32, 22, -4),
    ):
        ink.poly(pebble(cx, cy, hw, hh, rot=rot, flat=0.3))
    cap.poly(pebble(128, 58, 23, 17, rot=8, flat=0.15))
    return compose([ink, cap])


def design_b():
    """A pin whose head is a dungeon door: the doorway you walked into, lit from inside, in
    a ring of arch stones."""
    ink = Layer(INK)
    stones = Layer(DIM)
    door = Layer(ACCENT)
    step = Layer(LIGHT)
    head = arch(128, 38, 60, 150)[1:-1]            # the arch without its floor
    ink.poly(head + [(188, 152), (128, 222), (68, 152)])
    cx, cy = 128, 103
    for k in range(5):
        a0 = math.pi + math.pi * k / 5 + 0.07
        a1 = math.pi + math.pi * (k + 1) / 5 - 0.07
        outer = [(cx + 43 * math.cos(a0 + (a1 - a0) * j / 20), cy + 43 * math.sin(a0 + (a1 - a0) * j / 20)) for j in range(21)]
        inner = [(cx + 31 * math.cos(a0 + (a1 - a0) * j / 20), cy + 31 * math.sin(a0 + (a1 - a0) * j / 20)) for j in range(21)]
        stones.poly(outer + inner[::-1])
    door.poly(arch(128, 76, 27, 150))
    step.stroke([(100, 160), (156, 160)], 8)
    return compose([ink, stones, door, step])


def design_c():
    """A folded map, the way you walked across it dotted in, and a pin pushed in where you
    stopped."""
    ink = Layer(INK)
    trail = Layer(LIGHT)
    needle = Layer(LIGHT)
    pin = Layer(ACCENT)
    xs = [36, 97, 159, 220]
    tops = [72, 60, 72, 60]
    bottoms = [208, 196, 208, 196]
    gap = 3
    for i in range(3):
        def at(edge, x):
            return edge[i] + (edge[i + 1] - edge[i]) * (x - xs[i]) / (xs[i + 1] - xs[i])
        x0 = xs[i] + (gap if i else 0)
        x1 = xs[i + 1] - (gap if i < 2 else 0)
        ink.poly([(x0, at(tops, x0)), (x1, at(tops, x1)), (x1, at(bottoms, x1)), (x0, at(bottoms, x0))])
    for x, y in ((54, 186), (74, 176), (95, 170), (116, 168), (137, 170), (158, 171)):
        trail.circle(x, y, 7)
    # The head stays inside the right-hand panel: orange on the pale background loses most
    # of its contrast, and both reference icons only ever put the accent on the dark body.
    foot, head = (180, 168), (192, 112)
    needle.stroke([foot, head], 6)
    pin.circle(head[0], head[1], 21)
    return compose([ink, trail, needle, pin])


DESIGNS = {"a": design_a, "b": design_b, "c": design_c}


# -------------------------------------------------------------------- lineup

def lineup(icons, out):
    """Each icon at 256 on the top row and at 64, its real Thunderstore list size, below."""
    pad, label = 24, 18
    w = pad + len(icons) * (N + pad)
    h = pad + label + N + pad + 64 + pad
    sheet = Image.new("RGB", (w, h), (32, 34, 38))
    d = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(icons):
        x = pad + i * (N + pad)
        d.text((x, pad - 4), name, fill=(220, 220, 220))
        sheet.paste(img, (x, pad + label))
        sheet.paste(img.resize((64, 64), Image.LANCZOS), (x, pad + label + N + pad))
    sheet.save(out)


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(tempfile.gettempdir(), "varda-icons")
    os.makedirs(out_dir, exist_ok=True)
    made = []
    for key, build in DESIGNS.items():
        img = build()
        img.save(os.path.join(out_dir, key + ".png"))
        made.append((key, img))

    repos = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    refs = []
    for mod in ("stund", "kvedja"):
        path = os.path.join(repos, mod, "icon.png")
        if os.path.exists(path):
            refs.append((mod, Image.open(path).convert("RGB")))
    lineup(refs + made, os.path.join(out_dir, "lineup.png"))
    print("wrote", out_dir)


if __name__ == "__main__":
    main()
