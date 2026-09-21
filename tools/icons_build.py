"""Draws Varda's map pin icons.

    python tools/icons_build.py

Run from the repo root. Writes assets/dungeon.png, which the mod loads at runtime, and
assets/variants/*.png for the picker icons that are drawn but not yet wired up - variants/ is
deliberately outside the csproj's copy glob, so nothing unused is shipped beside the DLL.

Needs Pillow and nothing else.


WHY THE NUMBERS IN HERE ARE WHAT THEY ARE

Vanilla's pins are serialised on the Minimap prefab inside a hashed UnityFS bundle. They are
in neither the assemblies nor the asset manifest, and Devkit cannot reach them because its
rip resolves names through ZNetScene and ObjectDB. So they were read out of the running game
with Varda's own DumpIcons switch, cut out of the 2048x2048 UI atlas by their logged rects,
and measured. Over the thirteen comparable pins - house, hammer, portal, fire, death, trader,
bogwitch camp, memorial, upgrade station, plain pin, bed, hildir, start temple:

    fill 34%    waist 8 px    edge 32 darker than core    median 199    p90 229    spread 100

Two of those are the ones a drawing gets wrong by default, and getting them wrong is what
"that does not look like this game" turns out to mean:

  - vanilla bodies read NEAR-WHITE, not grey. A first pass at median 176 looked washed out
    next to them, and the 176 came from averaging in pins that are not style references at
    all: the red event marker, the blue ping ring, the red player arrow, the translucent
    question marks.
  - vanilla shapes are FAT, and they are single masses with a detail punched out rather than
    assemblies of thin parts. The house is one pentagon with a door cut in it.

The third thing, which no measurement would have told us, is that every vanilla pin carries a
dark outline AND a soft dark shadow outside it. That is what lets the same sprite sit on pale
parchment and on unexplored black. A pin without it disappears into fog.
"""
import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 8                     # supersample while drawing the silhouette
BODY = (212, 208, 200)
LIGHT = (255, 254, 250)
DARK = (86, 82, 76)
OUTLINE = (126, 121, 112)
SHADOW = (18, 15, 12)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")


# --------------------------------------------------------------------- render

def mask(size, draw_shape):
    """Run draw_shape on a supersampled 'L' canvas and return an antialiased mask."""
    c = size * S
    img = Image.new("L", (c, c), 0)
    draw_shape(ImageDraw.Draw(img), c)
    return img.resize((size, size), Image.LANCZOS)


def render(m, bevel=1.9, shadow_radius=2.4, shadow_alpha=190, outline=1):
    """A mask becomes a pin: dark-edged, bevelled body over a soft outer shadow."""
    size = m.size[0]

    # A blurred copy shifted up-left minus the same shifted down-right is a surface-normal-ish
    # term: positive where the shape faces the light, negative away from it. Cheap, and at
    # this size indistinguishable from a real bevel.
    soft = m.filter(ImageFilter.GaussianBlur(bevel))
    hi = ImageChops.subtract(ImageChops.offset(soft, -2, -2), ImageChops.offset(soft, 2, 2))
    lo = ImageChops.subtract(ImageChops.offset(soft, 2, 2), ImageChops.offset(soft, -2, -2))

    body = Image.new("RGBA", (size, size), BODY + (255,))
    body.paste(Image.new("RGBA", (size, size), LIGHT + (255,)), (0, 0), hi)
    body.paste(Image.new("RGBA", (size, size), DARK + (255,)), (0, 0), lo)

    if outline > 0:
        inner = m
        for _ in range(outline):
            inner = inner.filter(ImageFilter.MinFilter(3))
        body.paste(Image.new("RGBA", (size, size), OUTLINE + (255,)), (0, 0),
                   ImageChops.subtract(m, inner))

    body.putalpha(m)

    # Grown before blurring so it reads as a shadow rather than as a blurred copy of the shape
    # peeking out from behind it.
    glow = m.filter(ImageFilter.MaxFilter(3)).filter(
        ImageFilter.GaussianBlur(shadow_radius)).point(lambda v: v * shadow_alpha // 255)

    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (size, size), SHADOW + (255,)), (0, 0), glow)
    out.alpha_composite(body)
    return out


# -------------------------------------------------------------------- dungeon

CX = .50
SPRING = .52          # where the arch curve meets the jambs
R_OUT, R_IN = .32, .205
BOTTOM = .93


def dungeon(d, c):
    """A stone arch with steps going down into the dark.

    From Robbin's reference, and it is the shape that finally worked after fourteen that did
    not. Every earlier attempt drew an opening and became a horseshoe, because punching a hole
    down to the bottom edge splits the mass into two legs.

    THE STEPS ARE WHAT FIX THAT. They are light, they sit in the bottom of the opening, and
    they join the jambs across it - so the silhouette stays continuous and the dark is a hole
    in the middle of a solid thing rather than a gap between two halves of one.

    The flanking pillars from the reference are left out on purpose. They are faithful at
    64 px and they thicken into a grey block at 32, which is where this is actually read.
    """
    # the arch ring, cut into a keystone and two voussoirs a side
    d.pieslice([c * (CX - R_OUT), c * (SPRING - R_OUT), c * (CX + R_OUT), c * (SPRING + R_OUT)],
               start=180, end=360, fill=255)
    for k in (1, 2, 3, 4):
        a = math.radians(180 + k * 36)
        d.line([(c * (CX + (R_IN - .01) * math.cos(a)), c * (SPRING + (R_IN - .01) * math.sin(a))),
                (c * (CX + (R_OUT + .01) * math.cos(a)), c * (SPRING + (R_OUT + .01) * math.sin(a)))],
               fill=0, width=int(c * .022))

    # the keystone, big and standing proud of the curve
    top, bot = SPRING - R_OUT - .075, SPRING - R_IN + .03
    d.polygon([(c * (CX - .125), c * top), (c * (CX + .125), c * top),
               (c * (CX + .085), c * bot), (c * (CX - .085), c * bot)], fill=255)
    d.line([(c * (CX - .105), c * (top + .045)), (c * (CX + .105), c * (top + .045))],
           fill=0, width=int(c * .020))

    # the jambs the arch stands on
    for sign in (-1, 1):
        lo, hi = sorted((CX + sign * R_IN, CX + sign * R_OUT))
        d.rectangle([c * lo, c * SPRING, c * hi, c * BOTTOM], fill=255)
        d.line([(c * lo, c * .70), (c * hi, c * .70)], fill=0, width=int(c * .020))

    # the dark, cut to the floor
    d.pieslice([c * (CX - R_IN), c * (SPRING - R_IN), c * (CX + R_IN), c * (SPRING + R_IN)],
               start=180, end=360, fill=0)
    d.rectangle([c * (CX - R_IN), c * SPRING, c * (CX + R_IN), c * BOTTOM], fill=0)

    # the steps, fanning wider than the opening as they come forward
    n, top = 4, .62
    depth = (BOTTOM - top) / n
    for i in range(n):
        y0 = top + i * depth
        half = R_IN * .55 + (R_IN * 1.35 - R_IN * .55) * (i / (n - 1))
        d.rectangle([c * (CX - half), c * y0, c * (CX + half), c * (y0 + depth * .74)], fill=255)


# ------------------------------------------------- picker icons, not yet wired

def anchor(d, c):
    """Boat landing. Nothing in vanilla is an anchor - the hammer pin gets mistaken for one."""
    cx = c / 2
    d.rounded_rectangle([cx - c * .075, c * .19, cx + c * .075, c * .82], radius=c * .05, fill=255)
    d.ellipse([cx - c * .12, c * .05, cx + c * .12, c * .29], outline=255, width=int(c * .090))
    d.rounded_rectangle([c * .22, c * .28, c * .78, c * .28 + c * .135], radius=c * .05, fill=255)
    d.arc([c * .15, c * .40, c * .85, c * .95], start=15, end=165, fill=255, width=int(c * .150))
    d.polygon([(c * .11, c * .60), (c * .27, c * .66), (c * .15, c * .78)], fill=255)
    d.polygon([(c * .89, c * .60), (c * .73, c * .66), (c * .85, c * .78)], fill=255)


def fish(d, c):
    """Fishing spot. One shape, no rod and no water lines, so it holds at map size."""
    d.polygon([(c * .06, c * .50), (c * .30, c * .27), (c * .62, c * .27),
               (c * .78, c * .50), (c * .62, c * .73), (c * .30, c * .73)], fill=255)
    d.polygon([(c * .76, c * .50), (c * .96, c * .28), (c * .96, c * .72)], fill=255)
    d.polygon([(c * .38, c * .28), (c * .52, c * .12), (c * .60, c * .30)], fill=255)
    d.ellipse([c * .16, c * .43, c * .26, c * .53], fill=0)


# 64 because vanilla's own pins top out there and this one carries joint lines and steps that
# want the room. The map draws them smaller; the source should not be the limit.
SIZE = 64

SHIPPED = [("dungeon", dungeon)]
PENDING = [("anchor", anchor), ("fish", fish)]


def main():
    for folder in (ASSETS, VARIANTS):
        if not os.path.isdir(folder):
            os.makedirs(folder)

    for name, fn in SHIPPED:
        path = os.path.join(ASSETS, name + ".png")
        render(mask(SIZE, fn)).save(path)
        print("wrote", os.path.relpath(path, ROOT))

    for name, fn in PENDING:
        path = os.path.join(VARIANTS, name + ".png")
        render(mask(SIZE, fn)).save(path)
        print("wrote", os.path.relpath(path, ROOT), "(not shipped until the picker is built)")


if __name__ == "__main__":
    main()
