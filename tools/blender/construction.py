"""The TF Construction parts (7341-7367) and furniture (7440-7449), each in its part's frame and box
(MaterialParts sizes): y out of the surface it stands on, the box from y = 0 up. Their pieces are
named C<id>_<look>: Wood, WoodDark, Steel, SteelDark, Chrome, Glass, Fabric, FabricDark, Leaf,
Pot, Soil, Rug, RugTrim, BookA, BookB, BookC (the mod gives each look its material: real wood
grain, satin metal, the hull's glass...). Pieces stand a few millimeters clear of the faces under
them (no flicker). The helpers turn the part's frame into Blender's (game (x, y, z) = Blender (x, z, y))."""
import math
import os
import random
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box as _box, cylinder as _cylinder, export, _finish  # noqa: E402

random.seed(7)


def B(p):
    x, y, z = p
    return (x, z, y)


def box(part, look, center, size, bevel=0.0, rotation=(0, 0, 0), segments=1):
    sx, sy, sz = size
    rx, ry, rz = rotation
    return _box("C%d_%s_box" % (part, look), B(center), (sx, sz, sy), "C%d_%s" % (part, look), bevel=bevel, segments=segments, rotation=(rx, rz, ry))


def cyl(part, look, a, b, r, rb=None, sides=20, bevel=0.0, cap=True):
    return _cylinder("C%d_%s_cyl" % (part, look), B(a), B(b), r, "C%d_%s" % (part, look), vertices=sides, radius_b=rb, bevel=bevel, segments=1, cap=cap)


def solid(part, look, points):
    """A solid from corners in the part's frame: a bottom ring then a top ring of the same count."""
    n = len(points) // 2
    bm = bmesh.new()
    v = [bm.verts.new(B(p)) for p in points]
    bm.faces.new(v[:n])
    bm.faces.new(v[n:])
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((v[i], v[j], v[n + j], v[n + i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    name = "C%d_%s_solid" % (part, look)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return _finish(obj, "C%d_%s" % (part, look), smooth=False)


def rod(part, look, a, b, r, sides=12):
    return cyl(part, look, a, b, r, sides=sides)


reset()

# ======================= Wood =======================

# 7341 Wooden post 2 m (0.18 x 2 x 0.18): a squared post, steel shoe and bands, a cap
P = 7341
box(P, "Wood", (0, 1.0, 0), (0.14, 1.92, 0.14), bevel=0.008)
box(P, "SteelDark", (0, 0.02, 0), (0.18, 0.04, 0.18), bevel=0.004)
for y in (0.05, 1.93):
    box(P, "SteelDark", (0, y, 0), (0.152, 0.05, 0.152), bevel=0.003)
for x in (-0.06, 0.06):
    for z in (-0.06, 0.06):
        cyl(P, "Chrome", (x, 0.04, z), (x, 0.047, z), 0.009, sides=6)
box(P, "WoodDark", (0, 1.98, 0), (0.18, 0.04, 0.18), bevel=0.006)

# 7342 Slatted floor 1 x 1 (0.06): two battens, six bevelled slats, nail heads
P = 7342
for z in (-0.32, 0.32):
    box(P, "WoodDark", (0, 0.012, z), (0.98, 0.024, 0.08), bevel=0.003)
pitch = 1.0 / 6
for i in range(6):
    x = -0.5 + pitch * (i + 0.5)
    box(P, "Wood", (x, 0.042, 0), (pitch - 0.012, 0.034, 0.998), bevel=0.004)
    for z in (-0.32, 0.32):
        cyl(P, "SteelDark", (x, 0.059, z), (x, 0.061, z), 0.007, sides=6)

# 7343 Plank wall 1 x 2 (0.06): six upright planks on two battens and a brace behind
P = 7343
pitch = 1.0 / 6
for i in range(6):
    x = -0.5 + pitch * (i + 0.5)
    box(P, "Wood", (x, 1.0, -0.012), (pitch - 0.01, 1.99, 0.03), bevel=0.004)
for y in (0.3, 1.7):
    box(P, "WoodDark", (0, y, 0.016), (0.98, 0.12, 0.026), bevel=0.003)
box(P, "WoodDark", (0, 1.0, 0.016), (0.1, 1.5, 0.024), bevel=0.003, rotation=(0, 0, 33))
for i in range(6):
    x = -0.5 + pitch * (i + 0.5)
    for y in (0.27, 0.33, 1.67, 1.73):
        cyl(P, "SteelDark", (x, y, -0.028), (x, y, -0.03), 0.006, sides=6)

# 7344 Wooden railing 1 m (1 x 1 x 0.08): two posts, a rounded handrail, two rails
P = 7344
for x in (-0.45, 0.45):
    box(P, "Wood", (x, 0.47, 0), (0.08, 0.94, 0.08), bevel=0.008)
    box(P, "SteelDark", (x, 0.015, 0), (0.1, 0.03, 0.1), bevel=0.003)
box(P, "WoodDark", (0, 0.965, 0), (1.0, 0.07, 0.08), bevel=0.025, segments=2)
for y in (0.35, 0.65):
    box(P, "Wood", (0, y, 0), (0.82, 0.06, 0.04), bevel=0.006)

# 7345 Wooden lattice 1 x 1 (0.05): a frame, slats crossed both ways at two heights
P = 7345
for s in (-1, 1):
    box(P, "WoodDark", (s * 0.475, 0.025, 0), (0.05, 0.05, 1.0), bevel=0.004)
    box(P, "WoodDark", (0, 0.025, s * 0.475), (0.9, 0.05, 0.05), bevel=0.004)
# Each family runs along a diagonal, its slats side by side across it, cut to the 0.9 m opening
for k in range(-3, 4):
    d = k * 0.17
    length = 0.9 * math.sqrt(2) - 2 * abs(d) - 0.02
    if length < 0.08:
        continue
    o = d / math.sqrt(2)
    # (+45 degrees about y lays a slat along (-1, 0, 1): its neighbours step along (1, 0, 1))
    box(P, "Wood", (o, 0.017, o), (0.04, 0.016, length), rotation=(0, 45, 0))
    box(P, "Wood", (o, 0.034, -o), (0.04, 0.016, length), rotation=(0, -45, 0))

# 7346 Wooden shelf 1 m (1 x 0.9 x 0.32): sides, back, three shelves, a few things on them
P = 7346
for s in (-1, 1):
    box(P, "Wood", (s * 0.485, 0.45, 0), (0.03, 0.9, 0.32), bevel=0.004)
box(P, "WoodDark", (0, 0.45, 0.148), (0.94, 0.88, 0.02))
for y in (0.02, 0.32, 0.62, 0.885):
    box(P, "Wood", (0, y, -0.005), (0.94, 0.03, 0.3), bevel=0.003)
for i, (x, y, w, h) in enumerate(((-0.35, 0.335, 0.05, 0.22), (-0.29, 0.335, 0.04, 0.2), (-0.24, 0.335, 0.05, 0.24), (0.1, 0.635, 0.3, 0.06), (0.3, 0.035, 0.18, 0.12))):
    box(P, ("BookA", "BookB", "BookC")[i % 3], (x, y + h / 2, 0.02), (w, h, 0.2), bevel=0.003)
cyl(P, "Pot", (-0.2, 0.64, 0), (-0.2, 0.74, 0), 0.05, 0.06, sides=16)

# ======================= Glass =======================


def frame(P, w, h, depth=0.06, bar=0.05, look="Steel"):
    """A rectangular frame standing in its x-y plane, from y = 0 to h."""
    box(P, look, (0, bar / 2, 0), (w, bar, depth), bevel=0.004)
    box(P, look, (0, h - bar / 2, 0), (w, bar, depth), bevel=0.004)
    for s in (-1, 1):
        box(P, look, (s * (w / 2 - bar / 2), h / 2, 0), (bar, h - 2 * bar + 0.002, depth), bevel=0.004)


def window(P, w, h, columns=1, rows=1):
    frame(P, w, h)
    bar = 0.05
    box(P, "Glass", (0, h / 2, 0), (w - 2 * bar + 0.01, h - 2 * bar + 0.01, 0.012))
    for i in range(1, columns):
        x = -w / 2 + w * i / columns
        box(P, "Steel", (x, h / 2, 0), (0.04, h - 2 * bar, 0.05), bevel=0.003)
    for j in range(1, rows):
        y = h * j / rows
        box(P, "Steel", (0, y, 0), (w - 2 * bar, 0.04, 0.05), bevel=0.003)
    # Rubber gaskets around the glass, both faces
    for z in (-0.009, 0.009):
        box(P, "SteelDark", (0, bar + 0.006, z), (w - 2 * bar, 0.012, 0.006))
        box(P, "SteelDark", (0, h - bar - 0.006, z), (w - 2 * bar, 0.012, 0.006))


window(7351, 1.0, 1.0)
window(7352, 2.0, 2.0, columns=2, rows=2)
window(7356, 1.0, 2.0, rows=2)

# 7353 Glass floor tile 1 x 1 (0.08): a steel frame and a thick pane on rubber
P = 7353
for s in (-1, 1):
    box(P, "Steel", (s * 0.47, 0.04, 0), (0.06, 0.08, 1.0), bevel=0.004)
    box(P, "Steel", (0, 0.04, s * 0.47), (0.88, 0.08, 0.06), bevel=0.004)
box(P, "SteelDark", (0, 0.02, 0), (0.9, 0.04, 0.9))
box(P, "Glass", (0, 0.06, 0), (0.9, 0.034, 0.9))

# 7354 Glass railing 1 m (1 x 1 x 0.06): a pane in four clamps, a round chrome handrail on posts
P = 7354
box(P, "Glass", (0, 0.5, 0), (0.94, 0.82, 0.014))
for x in (-0.38, 0.38):
    for y in (0.14, 0.86):
        box(P, "Chrome", (x, y, 0), (0.05, 0.06, 0.034), bevel=0.006)
cyl(P, "Chrome", (-0.5, 0.96, 0), (0.5, 0.96, 0), 0.025, sides=20)
for x in (-0.47, 0.47):
    cyl(P, "Chrome", (x, 0.0, 0), (x, 0.95, 0), 0.016, sides=14)
    cyl(P, "SteelDark", (x, 0.0, 0), (x, 0.02, 0), 0.03, sides=16)

# 7355 Skylight 1 x 1 (0.4): a frame, four glass faces, steel ribs to the apex
P = 7355
for s in (-1, 1):
    box(P, "Steel", (s * 0.47, 0.03, 0), (0.06, 0.06, 1.0), bevel=0.004)
    box(P, "Steel", (0, 0.03, s * 0.47), (0.88, 0.06, 0.06), bevel=0.004)
apex = (0, 0.4, 0)
corners = [(-0.45, 0.06, -0.45), (0.45, 0.06, -0.45), (0.45, 0.06, 0.45), (-0.45, 0.06, 0.45)]
for i in range(4):
    a, b = corners[i], corners[(i + 1) % 4]
    solid(P, "Glass", [a, b, apex, (a[0] + 0.0, a[1] + 0.012, a[2]), (b[0], b[1] + 0.012, b[2]), (apex[0], apex[1] + 0.012, apex[2])])
for c in corners:
    rod(P, "Steel", c, apex, 0.015)
cyl(P, "SteelDark", (0, 0.39, 0), (0, 0.4, 0), 0.03, sides=14)

# ======================= Iron =======================

# 7361 Grating 1 x 1 (0.05): a frame, bearing bars on edge, cross rods above
P = 7361
for s in (-1, 1):
    box(P, "SteelDark", (s * 0.48, 0.025, 0), (0.04, 0.05, 1.0), bevel=0.003)
    box(P, "SteelDark", (0, 0.025, s * 0.48), (0.92, 0.05, 0.04), bevel=0.003)
for i in range(14):
    x = -0.42 + i * 0.84 / 13
    box(P, "Steel", (x, 0.022, 0), (0.008, 0.044, 0.92))
for j in range(8):
    z = -0.4 + j * 0.8 / 7
    cyl(P, "Steel", (-0.46, 0.046, z), (0.46, 0.046, z), 0.005, sides=8)


def steel_railing(P, w):
    n = max(2, int(round(w)) + 1)
    for i in range(n):
        x = -w / 2 + 0.03 + (w - 0.06) * i / (n - 1)
        box(P, "SteelDark", (x, 0.47, 0), (0.04, 0.94, 0.04), bevel=0.004)
        box(P, "SteelDark", (x, 0.01, 0), (0.08, 0.02, 0.06), bevel=0.003)
    cyl(P, "Steel", (-w / 2, 0.96, 0), (w / 2, 0.96, 0), 0.022, sides=16)
    cyl(P, "Steel", (-w / 2 + 0.03, 0.52, 0), (w / 2 - 0.03, 0.52, 0), 0.014, sides=12)
    box(P, "SteelDark", (0, 0.07, 0), (w - 0.06, 0.1, 0.008), bevel=0.002)


steel_railing(7362, 1.0)
steel_railing(7363, 2.0)

# 7364 Steel truss 2 m (0.36 x 2 x 0.32): three tube chords, zigzag diagonals, end plates
P = 7364
chords = [(-0.15, -0.12), (0.15, -0.12), (0.0, 0.13)]
for x, z in chords:
    rod(P, "Steel", (x, 0.02, z), (x, 1.98, z), 0.022, sides=14)
for k in range(10):
    y0, y1 = 0.04 + k * 0.192, 0.04 + (k + 1) * 0.192
    for i in range(3):
        a, b = chords[i], chords[(i + 1) % 3]
        if k % 2 == 0:
            rod(P, "Steel", (a[0], y0, a[1]), (b[0], y1, b[1]), 0.011, sides=8)
        else:
            rod(P, "Steel", (b[0], y0, b[1]), (a[0], y1, a[1]), 0.011, sides=8)
for y in (0.01, 1.99):
    solid(P, "SteelDark", [(-0.18, y - 0.01, -0.16), (0.18, y - 0.01, -0.16), (0.0, y - 0.01, 0.16),
                           (-0.18, y + 0.01, -0.16), (0.18, y + 0.01, -0.16), (0.0, y + 0.01, 0.16)])

# 7365 Riveted column 2.5 m (0.4 x 2.5 x 0.4): base, round shaft with riveted bands, capital
P = 7365
box(P, "SteelDark", (0, 0.03, 0), (0.4, 0.06, 0.4), bevel=0.006)
cyl(P, "Steel", (0, 0.06, 0), (0, 0.16, 0), 0.17, 0.13, sides=28)
cyl(P, "Steel", (0, 0.16, 0), (0, 2.34, 0), 0.11, 0.1, sides=28)
for y in (0.7, 1.4, 2.0):
    cyl(P, "SteelDark", (0, y, 0), (0, y + 0.06, 0), 0.118, 0.117, sides=28)
    for i in range(10):
        a = math.radians(36 * i)
        cyl(P, "Chrome", (0.118 * math.cos(a), y + 0.03, 0.118 * math.sin(a)), (0.124 * math.cos(a), y + 0.03, 0.124 * math.sin(a)), 0.008, sides=6)
cyl(P, "Steel", (0, 2.34, 0), (0, 2.44, 0), 0.1, 0.16, sides=28)
box(P, "SteelDark", (0, 2.47, 0), (0.4, 0.06, 0.4), bevel=0.006)

# 7366 Corner gusset 1 x 1 (0.04 thick): a triangular plate with flanges and bolts
P = 7366
t = 0.02
solid(P, "Steel", [(-t, 0.0, -0.5), (-t, 0.0, 0.5), (-t, 1.0, -0.5), (t, 0.0, -0.5), (t, 0.0, 0.5), (t, 1.0, -0.5)])
box(P, "SteelDark", (0, 0.015, 0), (0.04, 0.03, 1.0), bevel=0.003)
box(P, "SteelDark", (0, 0.5, -0.485), (0.04, 1.0, 0.03), bevel=0.003)
for y, z in ((0.15, 0.25), (0.15, -0.2), (0.55, -0.2)):
    for s in (-1, 1):
        cyl(P, "Chrome", (s * t, y, z), (s * (t + 0.006), y, z), 0.016, sides=6)

# 7367 Perforated sheet 1 x 1 (0.03): a folded frame and a plate pierced with round holes
P = 7367
for s in (-1, 1):
    box(P, "SteelDark", (s * 0.485, 0.015, 0), (0.03, 0.03, 1.0), bevel=0.003)
    box(P, "SteelDark", (0, 0.015, s * 0.485), (0.94, 0.03, 0.03), bevel=0.003)
box(P, "Steel", (0, 0.015, 0), (0.94, 0.012, 0.94))
for i in range(7):
    for j in range(7):
        x, z = -0.36 + i * 0.12, -0.36 + j * 0.12
        cyl(P, "SteelDark", (x, 0.008, z), (x, 0.022, z), 0.03, sides=12)

# ======================= Furniture =======================

# 7440 Sofa (2 x 0.85 x 0.9): dark legs, a frame, three seat and three back cushions, armrests
P = 7440
for x in (-0.92, 0.92):
    for z in (-0.38, 0.38):
        cyl(P, "WoodDark", (x, 0.0, z), (x, 0.08, z), 0.025, 0.02, sides=12)
box(P, "FabricDark", (0, 0.2, 0), (2.0, 0.24, 0.9), bevel=0.03, segments=2)
for i in range(3):
    x = -0.6 + i * 0.6
    box(P, "Fabric", (x, 0.38, -0.05), (0.58, 0.14, 0.72), bevel=0.05, segments=3)
    box(P, "Fabric", (x, 0.62, 0.33), (0.58, 0.42, 0.18), bevel=0.06, segments=3, rotation=(-8, 0, 0))
for s in (-1, 1):
    box(P, "FabricDark", (s * 0.93, 0.45, 0), (0.14, 0.36, 0.9), bevel=0.05, segments=3)

# 7441 Armchair (0.9 x 0.85 x 0.9)
P = 7441
for x in (-0.38, 0.38):
    for z in (-0.38, 0.38):
        cyl(P, "WoodDark", (x, 0.0, z), (x, 0.08, z), 0.025, 0.02, sides=12)
box(P, "FabricDark", (0, 0.2, 0), (0.9, 0.24, 0.9), bevel=0.03, segments=2)
box(P, "Fabric", (0, 0.38, -0.05), (0.62, 0.14, 0.72), bevel=0.05, segments=3)
box(P, "Fabric", (0, 0.62, 0.33), (0.62, 0.42, 0.18), bevel=0.06, segments=3, rotation=(-8, 0, 0))
for s in (-1, 1):
    box(P, "FabricDark", (s * 0.38, 0.45, 0), (0.14, 0.36, 0.9), bevel=0.05, segments=3)

# 7442 Chair (0.45 x 0.9 x 0.5): four legs, a seat, a slatted back
P = 7442
for x in (-0.19, 0.19):
    for z in (-0.2, 0.2):
        top = 0.88 if z > 0 else 0.44
        box(P, "Wood", (x, top / 2, z), (0.035, top, 0.035), bevel=0.004)
box(P, "Wood", (0, 0.455, 0), (0.45, 0.03, 0.46), bevel=0.006)
box(P, "Fabric", (0, 0.475, -0.01), (0.38, 0.02, 0.38), bevel=0.008, segments=2)
for y in (0.58, 0.7, 0.82):
    box(P, "Wood", (0, y, 0.205), (0.38, 0.06, 0.02), bevel=0.004)
for z in (-0.2, 0.2):
    box(P, "WoodDark", (0, 0.15, z), (0.34, 0.025, 0.02))

# 7443 Stool (0.4 x 0.7 x 0.4): three steel legs, a ring, a round wooden seat
P = 7443
for i in range(3):
    a = math.radians(90 + 120 * i)
    cyl(P, "SteelDark", (0.17 * math.cos(a), 0.0, 0.17 * math.sin(a)), (0.11 * math.cos(a), 0.67, 0.11 * math.sin(a)), 0.012, sides=10)
cyl(P, "Steel", (0, 0.25, 0), (0, 0.265, 0), 0.155, sides=24)
cyl(P, "Wood", (0, 0.66, 0), (0, 0.7, 0), 0.19, 0.185, sides=32, bevel=0.006)

# 7444 Table (1.6 x 0.76 x 0.9): a wooden top on four steel legs and a skirt
P = 7444
box(P, "Wood", (0, 0.74, 0), (1.6, 0.04, 0.9), bevel=0.008)
for x in (-0.72, 0.72):
    for z in (-0.37, 0.37):
        box(P, "SteelDark", (x, 0.36, z), (0.05, 0.72, 0.05), bevel=0.004)
for s in (-1, 1):
    box(P, "SteelDark", (0, 0.68, s * 0.37), (1.4, 0.07, 0.025))
    box(P, "SteelDark", (s * 0.72, 0.68, 0), (0.025, 0.07, 0.7))

# 7445 Coffee table (1.1 x 0.42 x 0.6): a glass top on a steel frame, a wooden shelf below
P = 7445
box(P, "Glass", (0, 0.41, 0), (1.1, 0.018, 0.6))
for x in (-0.5, 0.5):
    for z in (-0.25, 0.25):
        box(P, "SteelDark", (x, 0.2, z), (0.03, 0.4, 0.03), bevel=0.003)
for s in (-1, 1):
    box(P, "SteelDark", (0, 0.39, s * 0.25), (1.0, 0.02, 0.03))
    box(P, "SteelDark", (s * 0.5, 0.39, 0), (0.03, 0.02, 0.5))
box(P, "Wood", (0, 0.1, 0), (1.0, 0.025, 0.5), bevel=0.004)

# 7446 Desk (1.4 x 0.76 x 0.7): a top, a three-drawer unit, steel legs on the other side
P = 7446
box(P, "Wood", (0, 0.74, 0), (1.4, 0.04, 0.7), bevel=0.008)
box(P, "WoodDark", (0.45, 0.36, 0), (0.44, 0.72, 0.66), bevel=0.005)
for i in range(3):
    y = 0.12 + i * 0.22
    box(P, "Wood", (0.45, y + 0.1, -0.335), (0.4, 0.2, 0.02), bevel=0.004)
    box(P, "Chrome", (0.45, y + 0.15, -0.35), (0.12, 0.012, 0.012), bevel=0.003)
for z in (-0.3, 0.3):
    box(P, "SteelDark", (-0.65, 0.36, z), (0.04, 0.72, 0.04), bevel=0.003)
box(P, "SteelDark", (-0.65, 0.04, 0), (0.06, 0.03, 0.66))
box(P, "WoodDark", (-0.1, 0.5, 0.32), (1.0, 0.4, 0.02))

# 7447 Rug (2 x 0.012 x 3): a woven field, a border and fringes
P = 7447
box(P, "RugTrim", (0, 0.004, 0), (2.0, 0.008, 3.0))
box(P, "Rug", (0, 0.009, 0), (1.7, 0.006, 2.7))
for k in range(5):
    box(P, "RugTrim", (0, 0.0125, -1.0 + k * 0.5), (1.2, 0.002, 0.06))
for x in range(-9, 10):
    for z in (-1.51, 1.51):
        box(P, "RugTrim", (x * 0.1, 0.002, z), (0.02, 0.004, 0.04))

# 7448 Bookcase (1 x 2 x 0.35): a tall frame, five shelves full of books
P = 7448
for s in (-1, 1):
    box(P, "Wood", (s * 0.485, 1.0, 0), (0.03, 2.0, 0.35), bevel=0.004)
box(P, "WoodDark", (0, 1.0, 0.163), (0.94, 1.98, 0.02))
levels = [0.02, 0.42, 0.82, 1.22, 1.62, 1.985]
for y in levels:
    box(P, "Wood", (0, y, -0.005), (0.94, 0.03, 0.33), bevel=0.003)
for k in range(5):
    x = -0.45
    base = levels[k] + 0.015
    while x < 0.42:
        w = random.uniform(0.025, 0.055)
        h = random.uniform(0.22, 0.34)
        if random.random() < 0.12:
            x += w
            continue
        look = random.choice(("BookA", "BookB", "BookC"))
        box(P, look, (x + w / 2, base + h / 2, 0.02), (w - 0.004, h, random.uniform(0.2, 0.26)), bevel=0.002)
        x += w

# 7449 Potted plant (0.5 x 1.2 x 0.5): a tapered pot, soil, stems and broad leaves
P = 7449
cyl(P, "Pot", (0, 0.0, 0), (0, 0.4, 0), 0.17, 0.215, sides=32, bevel=0.006)
# The rim is a ring (a solid lip hid the soil), the soil heaped a little inside it, a few pebbles
bpy.ops.mesh.primitive_torus_add(major_radius=0.212, minor_radius=0.022, major_segments=40, minor_segments=10, location=B((0, 0.42, 0)))
rim = bpy.context.active_object
rim.name = "C7449_Pot_rim"
_finish(rim, "C7449_Pot")
cyl(P, "Soil", (0, 0.39, 0), (0, 0.425, 0), 0.205, 0.2, sides=32)
cyl(P, "Soil", (0, 0.425, 0), (0, 0.44, 0), 0.2, 0.12, sides=32)
for i in range(7):
    a = math.radians(i * 51 + 20)
    r = 0.08 + (i % 3) * 0.035
    cyl(P, "SteelDark", (r * math.cos(a), 0.43, r * math.sin(a)), (r * math.cos(a), 0.445, r * math.sin(a)), 0.014, 0.008, sides=6)
for i in range(9):
    a = math.radians(i * 40 + random.uniform(-10, 10))
    lean = random.uniform(0.12, 0.22)
    top = (lean * math.cos(a), 0.75 + random.uniform(0, 0.4), lean * math.sin(a))
    rod(P, "Leaf", (0, 0.42, 0), top, 0.008, sides=6)
    # A leaf: a flat diamond at the stem's tip, tilted outward
    d = (math.cos(a), 0, math.sin(a))
    tip = (top[0] + d[0] * 0.22, top[1] + 0.05, top[2] + d[2] * 0.22)
    side = (-d[2] * 0.07, 0, d[0] * 0.07)
    mid = (top[0] + d[0] * 0.1, top[1] + 0.04, top[2] + d[2] * 0.1)
    solid(P, "Leaf", [top, (mid[0] + side[0], mid[1], mid[2] + side[2]), tip, (mid[0] - side[0], mid[1], mid[2] - side[2]),
                      (top[0], top[1] + 0.006, top[2]), (mid[0] + side[0], mid[1] + 0.006, mid[2] + side[2]), (tip[0], tip[1] + 0.006, tip[2]), (mid[0] - side[0], mid[1] + 0.006, mid[2] - side[2])])

export("construction.obj")
