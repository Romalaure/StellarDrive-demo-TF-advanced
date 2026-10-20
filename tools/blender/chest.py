"""The loot chest (parts 7475-7479): a planked chest bound in iron, rivets, corner brackets, a lock
plate with a gem, hinges, side handles and feet; its barrel lid stands open behind, a glow rises
from the loot. Looks: wood, iron, the rarity's trim metal, the gem/loot glow, the dark inside.
Body 0.8 x 0.56 x 0.44 m; front toward -Y here (-Z in the game), origin at the floor's middle."""
import math
import os
import random
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box as _box, cylinder, export  # noqa: E402


def box(name, center, size, mat, bevel=0.0, segments=1, rotation=(0, 0, 0)):
    # One bevel segment: many small pieces, the chest must stay light (chests are everywhere)
    return _box(name, center, size, mat, bevel=bevel, segments=1, rotation=rotation)


WOOD, IRON, TRIM, GLOW, INSIDE = "TF_ChestWood", "TF_ChestIron", "TF_ChestTrim", "TF_ChestGlow", "TF_ChestInside"
W, D, H = 0.8, 0.56, 0.44
FOOT = 0.04

reset()

# ---- Body: planks on each side (small gaps between them), a floor, the dark inside
for i in range(4):
    z = FOOT + 0.02 + i * (H - 0.04) / 4 + (H - 0.04) / 8
    box("FrontPlank%d" % i, (0, -D / 2 + 0.018, z), (W - 0.06, 0.036, (H - 0.04) / 4 - 0.008), WOOD, bevel=0.006)
    box("BackPlank%d" % i, (0, D / 2 - 0.018, z), (W - 0.06, 0.036, (H - 0.04) / 4 - 0.008), WOOD, bevel=0.006)
    for s in (-1, 1):
        box("SidePlank%d_%d" % (i, s), (s * (W / 2 - 0.018), 0, z), (0.036, D - 0.06, (H - 0.04) / 4 - 0.008), WOOD, bevel=0.006)
box("Floor", (0, 0, FOOT + 0.02), (W - 0.04, D - 0.04, 0.04), WOOD, bevel=0.005)
box("Inside", (0, 0, FOOT + (H - 0.1) * 0.5 + 0.02), (W - 0.08, D - 0.08, H - 0.1), INSIDE)
# The loot inside, glowing: a heap under the rim
box("LootHeap", (0, 0, FOOT + H - 0.075), (W - 0.12, D - 0.12, 0.05), GLOW, bevel=0.02)
for i, (x, y) in enumerate(((-0.2, -0.05), (0.12, 0.08), (0.25, -0.1), (-0.05, 0.1))):
    cylinder("Coin%d" % i, (x, y, FOOT + H - 0.045), (x, y, FOOT + H - 0.02), 0.035, TRIM, vertices=16)

# ---- Iron: corner posts, two bands around, the rim, rivets
for sx in (-1, 1):
    for sy in (-1, 1):
        box("Corner%d%d" % (sx, sy), (sx * (W / 2 - 0.012), sy * (D / 2 - 0.012), FOOT + H / 2), (0.05, 0.05, H + 0.01), IRON, bevel=0.008)
        # Brackets folded over each corner, top and bottom
        for zz in (FOOT + 0.03, FOOT + H - 0.03):
            box("Bracket%d%d%.2f" % (sx, sy, zz), (sx * (W / 2 - 0.05), sy * (D / 2 + 0.002), zz), (0.1, 0.012, 0.07), IRON, bevel=0.004)
            box("BracketS%d%d%.2f" % (sx, sy, zz), (sx * (W / 2 + 0.002), sy * (D / 2 - 0.05), zz), (0.012, 0.1, 0.07), IRON, bevel=0.004)
for zz in (FOOT + H * 0.33, FOOT + H * 0.7):
    box("BandFront%.2f" % zz, (0, -D / 2 - 0.004, zz), (W + 0.012, 0.012, 0.04), IRON, bevel=0.003)
    box("BandBack%.2f" % zz, (0, D / 2 + 0.004, zz), (W + 0.012, 0.012, 0.04), IRON, bevel=0.003)
    for s in (-1, 1):
        box("BandSide%d%.2f" % (s, zz), (s * (W / 2 + 0.004), 0, zz), (0.012, D + 0.012, 0.04), IRON, bevel=0.003)
    for k in range(7):
        x = -W / 2 + 0.08 + k * (W - 0.16) / 6
        for y in (-D / 2 - 0.011, D / 2 + 0.011):
            cylinder("Rivet%.2f%.2f%.2f" % (x, y, zz), (x, y, zz), (x, y * 1.02, zz), 0.008, IRON, vertices=6)
# The rim in the rarity's metal
box("RimFront", (0, -D / 2 + 0.01, FOOT + H + 0.006), (W + 0.02, 0.03, 0.02), TRIM, bevel=0.004)
box("RimBack", (0, D / 2 - 0.01, FOOT + H + 0.006), (W + 0.02, 0.03, 0.02), TRIM, bevel=0.004)
for s in (-1, 1):
    box("RimSide%d" % s, (s * (W / 2 - 0.01), 0, FOOT + H + 0.006), (0.03, D + 0.02, 0.02), TRIM, bevel=0.004)

# ---- Feet, side handles, the lock with its gem
for sx in (-1, 1):
    for sy in (-1, 1):
        box("Foot%d%d" % (sx, sy), (sx * (W / 2 - 0.04), sy * (D / 2 - 0.04), FOOT / 2), (0.07, 0.07, FOOT), IRON, bevel=0.01)
    # A ring handle hanging from two studs
    x = sx * (W / 2 + 0.02)
    for y in (-0.07, 0.07):
        cylinder("HandleStud%d%.2f" % (sx, y), (sx * W / 2, y, FOOT + H * 0.62), (x, y, FOOT + H * 0.62), 0.012, IRON, vertices=6)
    segments = 10
    for k in range(segments):
        a0, a1 = math.pi * k / segments, math.pi * (k + 1) / segments
        p0 = (x + sx * 0.005, -0.07 * math.cos(a0), FOOT + H * 0.62 - 0.07 * math.sin(a0))
        p1 = (x + sx * 0.005, -0.07 * math.cos(a1), FOOT + H * 0.62 - 0.07 * math.sin(a1))
        cylinder("Handle%d_%d" % (sx, k), p0, p1, 0.009, IRON, vertices=6)
box("LockPlate", (0, -D / 2 - 0.012, FOOT + H - 0.07), (0.13, 0.016, 0.15), TRIM, bevel=0.006)
box("LockHasp", (0, -D / 2 - 0.022, FOOT + H - 0.02), (0.05, 0.012, 0.08), IRON, bevel=0.004)
cylinder("Keyhole", (0, -D / 2 - 0.02, FOOT + H - 0.105), (0, -D / 2 - 0.023, FOOT + H - 0.105), 0.012, INSIDE, vertices=12)
cylinder("Gem", (0, -D / 2 - 0.02, FOOT + H - 0.055), (0, -D / 2 - 0.036, FOOT + H - 0.055), 0.022, GLOW, radius_b=0.012, vertices=6)

# ---- The lid: a barrel top of staves, standing open behind (turned on the hinges at the back)
hinge_y, hinge_z = D / 2, FOOT + H + 0.01
lid_angle = math.radians(-112)
R, FLAT = D / 2, 0.7


def lid(x, y, z):
    """A point of the closed lid (hinge at the origin, its front at y = -D) turned open."""
    c, s = math.cos(lid_angle), math.sin(lid_angle)
    return (x, hinge_y + y * c - z * s, hinge_z + y * s + z * c)


def stave(name, x, length, mid, width, lift, thickness, mat):
    # On the closed lid's arc (center at y = -R), facing out, then opened with the lid
    y, z = -R + R * math.cos(mid), R * math.sin(mid) * FLAT + lift
    tilt = math.degrees(mid) - 90 + math.degrees(lid_angle)
    box(name, lid(x, y, z), (length, width, thickness), mat, bevel=0.004, rotation=(tilt, 0, 0))


staves = 7
for k in range(staves):
    a0, a1 = math.pi * k / staves, math.pi * (k + 1) / staves
    width = 2 * R * math.sin((a1 - a0) / 2) * 1.0
    stave("LidStave%d" % k, 0, W - 0.03, (a0 + a1) / 2, width - 0.006, 0, 0.032, WOOD)
    for x in (-W / 2 + 0.08, W / 2 - 0.08):
        stave("LidStrap%.2f_%d" % (x, k), x, 0.045, (a0 + a1) / 2, width + 0.004, 0.02, 0.012, IRON)
    stave("LidTrim%d" % k, 0, 0.06, (a0 + a1) / 2, width + 0.004, 0.02, 0.012, TRIM)
for s in (-1, 1):
    # The lid's ends: a flat plank under the arc, and the hinges
    box("LidEnd%d" % s, lid(s * (W / 2 - 0.016), -R, R * FLAT * 0.45), (0.03, D - 0.02, R * FLAT * 0.9), WOOD, bevel=0.005, rotation=(math.degrees(lid_angle), 0, 0))
    cylinder("Hinge%d" % s, (s * (W / 2 - 0.16), hinge_y + 0.012, hinge_z), (s * (W / 2 - 0.08), hinge_y + 0.012, hinge_z), 0.016, IRON, vertices=12)

# ---- Details: nail heads holding each plank, rivets on the lid's straps, more loot (coins, some
# standing on edge, gems, a goblet)
for i in range(4):
    z = FOOT + 0.02 + i * (H - 0.04) / 4 + (H - 0.04) / 8
    for s in (-1, 1):
        for y in (-D / 2, D / 2):
            x = s * (W / 2 - 0.15)
            cylinder("Nail%d%d%.2f" % (i, s, y), (x, y, z), (x, y * 1.015, z), 0.006, IRON, vertices=6)
        for y in (-D / 2 + 0.11, D / 2 - 0.11):
            cylinder("NailS%d%d%.2f" % (i, s, y), (s * W / 2, y, z), (s * W / 2 * 1.012, y, z), 0.006, IRON, vertices=6)
for k in range(staves):
    a0, a1 = math.pi * k / staves, math.pi * (k + 1) / staves
    for x in (-W / 2 + 0.08, W / 2 - 0.08):
        stave("LidRivet%.2f_%d" % (x, k), x, 0.014, (a0 + a1) / 2, 0.014, 0.03, 0.008, IRON)
random.seed(7)
top = FOOT + H - 0.05
for i in range(12):
    x, y = random.uniform(-0.3, 0.3), random.uniform(-0.17, 0.17)
    if i % 4 == 0:
        # On edge, leaning
        a = random.uniform(0, math.pi)
        cylinder("CoinEdge%d" % i, (x - 0.004 * math.cos(a), y - 0.004 * math.sin(a), top + 0.033), (x + 0.004 * math.cos(a), y + 0.004 * math.sin(a), top + 0.036), 0.03, TRIM, vertices=14)
    else:
        z = top + random.uniform(0.0, 0.02)
        cylinder("CoinMore%d" % i, (x, y, z), (x + random.uniform(-0.004, 0.004), y, z + 0.005), 0.028, TRIM, vertices=14)
for i, (x, y) in enumerate(((0.06, -0.12), (-0.27, 0.11), (0.3, 0.05))):
    # A cut gem: two pyramids
    cylinder("GemTop%d" % i, (x, y, top + 0.04), (x, y, top + 0.06), 0.025, GLOW, radius_b=0.012, vertices=6)
    cylinder("GemBottom%d" % i, (x, y, top + 0.04), (x, y, top + 0.012), 0.025, GLOW, radius_b=0.001, vertices=6)
gx, gy = -0.12, -0.08
cylinder("GobletFoot", (gx, gy, top + 0.01), (gx, gy, top + 0.018), 0.035, TRIM, vertices=16)
cylinder("GobletStem", (gx, gy, top + 0.018), (gx, gy, top + 0.075), 0.008, TRIM, vertices=10)
cylinder("GobletCup", (gx, gy, top + 0.075), (gx, gy, top + 0.13), 0.022, TRIM, radius_b=0.04, vertices=16, cap=False)
cylinder("GobletWine", (gx, gy, top + 0.12), (gx, gy, top + 0.121), 0.037, GLOW, vertices=16)

export("chest.obj")
