"""The loot chest (parts 7475-7479): a sci-fi cargo case. A rounded metal shell on a dark plinth,
recessed dark panels, corner bumpers and the lid's edge in the rarity's metal, a thin light band
around it and a lock pad in front, side handles; its lid stands open behind, a few small lit
crystals and ingots inside (no lit surface: the old glowing heap was blinding).
Looks: the shell, the dark frame, the rarity's trim metal, the small lights, the dark inside.
Body 0.8 x 0.56 x 0.44 m; front toward -Y here (-Z in the game), origin at the floor's middle."""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box as _box, cylinder, export  # noqa: E402


def box(name, center, size, mat, bevel=0.0, rotation=(0, 0, 0)):
    # One bevel segment: chests are everywhere, the model must stay light
    return _box(name, center, size, mat, bevel=bevel, segments=1, rotation=rotation)


SHELL, FRAME, TRIM, GLOW, INSIDE = "TF_ChestShell", "TF_ChestIron", "TF_ChestTrim", "TF_ChestGlow", "TF_ChestInside"
W, D, H = 0.8, 0.56, 0.44
BASE = 0.05
TOP = BASE + H

reset()

# ---- Plinth: a dark chamfered base with four pads
box("Plinth", (0, 0, BASE * 0.5), (W - 0.02, D - 0.02, BASE), FRAME, bevel=0.015)
for sx in (-1, 1):
    for sy in (-1, 1):
        box("Pad%d%d" % (sx, sy), (sx * (W / 2 - 0.07), sy * (D / 2 - 0.06), 0.006), (0.1, 0.08, 0.012), FRAME, bevel=0.003)

# ---- The shell: four walls around the dark inside (open top), rounded edges
WALL = 0.035
box("WallFront", (0, -D / 2 + WALL / 2, BASE + H / 2), (W, WALL, H), SHELL, bevel=0.012)
box("WallBack", (0, D / 2 - WALL / 2, BASE + H / 2), (W, WALL, H), SHELL, bevel=0.012)
for s in (-1, 1):
    box("WallSide%d" % s, (s * (W / 2 - WALL / 2), 0, BASE + H / 2), (WALL, D - 2 * WALL + 0.004, H), SHELL, bevel=0.012)

# Recessed panels: dark plates a few millimeters proud of each face (clear of it: no flicker)
box("PanelFront", (0, -D / 2 - 0.004, BASE + H * 0.45), (W - 0.2, 0.008, H * 0.5), FRAME, bevel=0.004)
box("PanelBack", (0, D / 2 + 0.004, BASE + H * 0.45), (W - 0.2, 0.008, H * 0.5), FRAME, bevel=0.004)
for s in (-1, 1):
    box("PanelSide%d" % s, (s * (W / 2 + 0.004), 0, BASE + H * 0.45), (0.008, D - 0.2, H * 0.5), FRAME, bevel=0.004)

# Corner bumpers in the rarity's metal, a band at the top of each
for sx in (-1, 1):
    for sy in (-1, 1):
        box("Bumper%d%d" % (sx, sy), (sx * (W / 2 - 0.01), sy * (D / 2 - 0.01), BASE + H / 2), (0.06, 0.06, H + 0.012), TRIM, bevel=0.014)
        box("BumperCap%d%d" % (sx, sy), (sx * (W / 2 - 0.01), sy * (D / 2 - 0.01), TOP + 0.004), (0.066, 0.066, 0.014), FRAME, bevel=0.004)

# A thin light band around the case, low, proud of the panels
BAND_Z = BASE + 0.07
box("BandFront", (0, -D / 2 - 0.006, BAND_Z), (W - 0.12, 0.004, 0.01), GLOW)
box("BandBack", (0, D / 2 + 0.006, BAND_Z), (W - 0.12, 0.004, 0.01), GLOW)
for s in (-1, 1):
    box("BandSide%d" % s, (s * (W / 2 + 0.006), 0, BAND_Z), (0.004, D - 0.12, 0.01), GLOW)

# Lock pad in front: a dark rounded plate, its trim frame and a small light
box("LockFrame", (0, -D / 2 - 0.012, TOP - 0.075), (0.15, 0.012, 0.09), TRIM, bevel=0.006)
box("LockPad", (0, -D / 2 - 0.02, TOP - 0.075), (0.11, 0.006, 0.06), FRAME, bevel=0.004)
box("LockLight", (0, -D / 2 - 0.024, TOP - 0.062), (0.06, 0.003, 0.008), GLOW)

# Side handles: two posts and a bar
for s in (-1, 1):
    for y in (-0.09, 0.09):
        box("HandlePost%d%.2f" % (s, y), (s * (W / 2 + 0.025), y, TOP - 0.12), (0.03, 0.02, 0.02), FRAME, bevel=0.004)
    cylinder("HandleBar%d" % s, (s * (W / 2 + 0.04), -0.11, TOP - 0.12), (s * (W / 2 + 0.04), 0.11, TOP - 0.12), 0.012, FRAME, vertices=10)

# The rim: a trim edge around the opening
box("RimFront", (0, -D / 2 + 0.012, TOP + 0.004), (W - 0.06, 0.024, 0.008), TRIM, bevel=0.002)
box("RimBack", (0, D / 2 - 0.012, TOP + 0.004), (W - 0.06, 0.024, 0.008), TRIM, bevel=0.002)
for s in (-1, 1):
    box("RimSide%d" % s, (s * (W / 2 - 0.012), 0, TOP + 0.004), (0.024, D - 0.06, 0.008), TRIM, bevel=0.002)

# ---- The loot: a few small lit crystals, ingots and coins on the dark floor
FLOOR = BASE + 0.01 + (H - 0.05) * 0.55
for i, (x, y, r) in enumerate(((-0.18, 0.02, 0.04), (0.06, -0.06, 0.03), (0.2, 0.08, 0.035))):
    cylinder("Crystal%d" % i, (x, y, FLOOR), (x, y, FLOOR + r * 2.6), r, GLOW, vertices=6, radius_b=0.004)
for i, (x, y) in enumerate(((-0.02, 0.09), (0.12, 0.12), (-0.1, -0.1))):
    box("Ingot%d" % i, (x, y, FLOOR + 0.02), (0.11, 0.05, 0.035), TRIM, bevel=0.008)
for i, (x, y) in enumerate(((0.24, -0.1), (-0.25, -0.12), (0.0, -0.13))):
    cylinder("Coin%d" % i, (x, y, FLOOR), (x, y, FLOOR + 0.012), 0.028, TRIM, vertices=12)
# The dark floor of the inside, up to where the loot lies (the walls' inner faces stay open)
box("Inside", (0, 0, (BASE + FLOOR) / 2), (W - 2 * WALL - 0.004, D - 2 * WALL - 0.004, FLOOR - BASE), INSIDE)

# ---- The lid, open behind: hinged on the back top edge, turned 105 degrees
ANGLE = math.radians(-105)
LID_T = 0.06


def lid(local):
    """A point of the closed lid (hinge at the origin, the lid toward -Y) once opened."""
    x, y, z = local
    return (x, D / 2 + y * math.cos(ANGLE) - z * math.sin(ANGLE), TOP + y * math.sin(ANGLE) + z * math.cos(ANGLE))


ROT = (math.degrees(ANGLE), 0, 0)
box("LidShell", lid((0, -D / 2, LID_T / 2)), (W, D, LID_T), SHELL, bevel=0.014, rotation=ROT)
box("LidPanel", lid((0, -D / 2, LID_T + 0.004)), (W - 0.2, D - 0.2, 0.008), FRAME, bevel=0.004, rotation=ROT)
box("LidEdgeFront", lid((0, -D + 0.012, LID_T / 2)), (W + 0.008, 0.03, LID_T + 0.008), TRIM, bevel=0.006, rotation=ROT)
for s in (-1, 1):
    box("LidEdgeSide%d" % s, lid((s * (W / 2 - 0.004), -D / 2, LID_T / 2)), (0.016, D - 0.04, LID_T + 0.004), TRIM, bevel=0.004, rotation=ROT)
box("LidLight", lid((0, -D / 2, -0.004)), (W - 0.3, 0.01, 0.004), GLOW, rotation=ROT)
for s in (-1, 1):
    cylinder("Hinge%d" % s, (s * 0.28 - 0.05, D / 2 + 0.01, TOP), (s * 0.28 + 0.05, D / 2 + 0.01, TOP), 0.018, FRAME, vertices=12)

export("chest.obj")
