"""The TF Construction parts (7341-7367), furniture (7440-7449, 8200-8211, 8240-8256), stairs (8220-8229) and the tractor beam (7486), each in its part's frame and box
(MaterialParts sizes): y out of the surface it stands on, the box from y = 0 up. Their pieces are
named C<id>_<look>: Wood, WoodDark, Steel, SteelDark, Chrome, Glass, Fabric, FabricDark, Leaf,
Pot, Soil, Rug, RugTrim, BookA, BookB, BookC, Sheet, Screen, ScreenLit, Lamp, Neon, White, Green, Red (the mod gives each look its material: real wood
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

# 7344 Wooden railing 1 m (1 x 1 x 0.08): two posts, a rounded handrail, two rails. The posts stand
# on the ends (half past them): two railings in a line or at a corner share one post
P = 7344
for x in (-0.5, 0.5):
    box(P, "Wood", (x, 0.47, 0), (0.08, 0.94, 0.08), bevel=0.008)
    box(P, "SteelDark", (x, 0.015, 0), (0.1, 0.03, 0.1), bevel=0.003)
box(P, "WoodDark", (0, 0.965, 0), (1.04, 0.07, 0.08), bevel=0.025, segments=2)
for y in (0.35, 0.65):
    box(P, "Wood", (0, y, 0), (0.92, 0.06, 0.04), bevel=0.006)

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
for x in (-0.5, 0.5):
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
        x = -w / 2 + w * i / (n - 1)
        box(P, "SteelDark", (x, 0.47, 0), (0.04, 0.94, 0.04), bevel=0.004)
        box(P, "SteelDark", (x, 0.01, 0), (0.08, 0.02, 0.06), bevel=0.003)
    cyl(P, "Steel", (-w / 2, 0.96, 0), (w / 2, 0.96, 0), 0.022, sides=16)
    cyl(P, "Steel", (-w / 2, 0.52, 0), (w / 2, 0.52, 0), 0.014, sides=12)
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

# ======================= More furniture (8200-8211) =======================

# 8200 Bed (1 x 0.55 x 2.1): a wooden frame, a mattress, a sheet, a folded blanket, a pillow, a headboard at +z
P = 8200
for x in (-0.46, 0.46):
    for z in (-1.0, 1.0):
        box(P, "WoodDark", (x, 0.08, z), (0.07, 0.16, 0.07), bevel=0.005)
for s in (-1, 1):
    box(P, "Wood", (s * 0.48, 0.2, 0), (0.04, 0.12, 2.06), bevel=0.005)
box(P, "Wood", (0, 0.2, -1.03), (1.0, 0.12, 0.04), bevel=0.005)
box(P, "Wood", (0, 0.5, 1.03), (1.0, 0.72, 0.05), bevel=0.01)
box(P, "WoodDark", (0, 0.86, 1.03), (1.02, 0.03, 0.07), bevel=0.006)
box(P, "Sheet", (0, 0.32, 0), (0.92, 0.18, 2.0), bevel=0.04, segments=3)
box(P, "FabricDark", (0, 0.42, -0.45), (0.96, 0.05, 1.0), bevel=0.02, segments=2)
box(P, "Fabric", (0, 0.45, -0.05), (0.97, 0.06, 0.22), bevel=0.025, segments=2)
box(P, "Sheet", (0, 0.46, 0.8), (0.62, 0.1, 0.32), bevel=0.045, segments=3)

# 8201 Bunk bed (1 x 1.8 x 2.1): a steel frame, two mattresses, a ladder at the front (-z) right
P = 8201
for x in (-0.47, 0.47):
    for z in (-1.0, 1.0):
        box(P, "SteelDark", (x, 0.9, z), (0.05, 1.8, 0.05), bevel=0.004)
for y in (0.25, 1.15):
    for s in (-1, 1):
        box(P, "Steel", (s * 0.47, y, 0), (0.04, 0.08, 1.96), bevel=0.003)
        box(P, "Steel", (0, y, s * 1.0), (0.9, 0.08, 0.04), bevel=0.003)
    box(P, "SteelDark", (0, y + 0.03, 0), (0.88, 0.02, 1.94))
    box(P, "Sheet", (0, y + 0.11, 0), (0.88, 0.14, 1.92), bevel=0.035, segments=3)
    box(P, "FabricDark", (0, y + 0.19, -0.4), (0.9, 0.03, 1.0), bevel=0.012)
    box(P, "Sheet", (0, y + 0.22, 0.75), (0.56, 0.08, 0.28), bevel=0.035, segments=3)
# The upper bunk's guard rail at +x and -x
for s in (-1, 1):
    cyl(P, "Steel", (s * 0.47, 1.55, -0.95), (s * 0.47, 1.55, 0.95), 0.018, sides=12)
for x in (0.18, 0.42):
    box(P, "SteelDark", (x, 0.95, -1.06), (0.035, 1.6, 0.035), bevel=0.003)
for k in range(5):
    cyl(P, "Chrome", (0.18, 0.35 + k * 0.28, -1.06), (0.42, 0.35 + k * 0.28, -1.06), 0.014, sides=10)

# 8202 Nightstand (0.5 x 0.55 x 0.42): a small cabinet, a drawer, a lamp
P = 8202
box(P, "Wood", (0, 0.24, 0), (0.5, 0.44, 0.42), bevel=0.008)
box(P, "WoodDark", (0, 0.46, 0), (0.52, 0.03, 0.44), bevel=0.006)
box(P, "WoodDark", (0, 0.33, -0.211), (0.44, 0.15, 0.01), bevel=0.003)
box(P, "Chrome", (0, 0.33, -0.222), (0.1, 0.014, 0.012), bevel=0.003)
for x in (-0.21, 0.21):
    for z in (-0.17, 0.17):
        box(P, "SteelDark", (x, 0.01, z), (0.04, 0.02, 0.04))
cyl(P, "Chrome", (0.1, 0.475, 0.06), (0.1, 0.49, 0.06), 0.06, sides=16)
cyl(P, "Chrome", (0.1, 0.49, 0.06), (0.1, 0.6, 0.06), 0.008, sides=8)
cyl(P, "Lamp", (0.1, 0.55, 0.06), (0.1, 0.6, 0.06), 0.03, sides=12)
cyl(P, "Sheet", (0.1, 0.54, 0.06), (0.1, 0.64, 0.06), 0.1, 0.065, sides=20, cap=False)

# 8203 Wardrobe (1.2 x 2.1 x 0.6): a tall cabinet, two doors with long handles, a plinth and a cornice
P = 8203
box(P, "WoodDark", (0, 0.04, 0), (1.16, 0.08, 0.56))
box(P, "Wood", (0, 1.05, 0.01), (1.2, 1.94, 0.58), bevel=0.008)
box(P, "WoodDark", (0, 2.07, 0), (1.24, 0.06, 0.62), bevel=0.008)
for s in (-1, 1):
    box(P, "Wood", (s * 0.296, 1.05, -0.285), (0.58, 1.86, 0.02), bevel=0.006)
    box(P, "Chrome", (s * 0.04, 1.1, -0.305), (0.016, 0.4, 0.016), bevel=0.004)
    for y in (0.25, 1.85):
        box(P, "WoodDark", (s * 0.296, y, -0.297), (0.5, 0.012, 0.006))

# 8204 Dresser (1.2 x 0.85 x 0.5): a chest of six drawers in two columns
P = 8204
box(P, "Wood", (0, 0.44, 0.01), (1.2, 0.8, 0.48), bevel=0.008)
box(P, "WoodDark", (0, 0.835, 0), (1.22, 0.03, 0.52), bevel=0.006)
for i in range(3):
    y = 0.18 + i * 0.25
    for s in (-1, 1):
        box(P, "WoodDark", (s * 0.295, y, -0.236), (0.56, 0.22, 0.012), bevel=0.004)
        box(P, "Chrome", (s * 0.295, y + 0.04, -0.25), (0.14, 0.014, 0.014), bevel=0.003)
for x in (-0.56, 0.56):
    for z in (-0.2, 0.2):
        cyl(P, "SteelDark", (x, 0.0, z), (x, 0.045, z), 0.02, sides=10)

# 8205 Kitchen counter (1.2 x 0.9 x 0.6): a cabinet, a steel worktop with a sink and a tap
P = 8205
box(P, "SteelDark", (0, 0.04, 0.02), (1.16, 0.08, 0.54))
box(P, "Wood", (0, 0.46, 0.01), (1.2, 0.76, 0.58), bevel=0.006)
for s in (-1, 1):
    box(P, "WoodDark", (s * 0.296, 0.46, -0.285), (0.58, 0.7, 0.018), bevel=0.004)
    box(P, "Chrome", (s * 0.05, 0.72, -0.3), (0.016, 0.14, 0.016), bevel=0.003)
box(P, "Steel", (0, 0.87, 0), (1.22, 0.04, 0.62), bevel=0.006)
box(P, "SteelDark", (0.25, 0.85, -0.02), (0.44, 0.03, 0.36))
box(P, "Chrome", (0.25, 0.89, -0.02), (0.46, 0.006, 0.38))
cyl(P, "Chrome", (0.25, 0.89, 0.22), (0.25, 1.06, 0.22), 0.016, sides=12)
cyl(P, "Chrome", (0.25, 1.06, 0.22), (0.25, 1.06, 0.05), 0.013, sides=12)
cyl(P, "Chrome", (0.33, 0.89, 0.22), (0.33, 0.95, 0.22), 0.012, sides=8)
# A chopping board and a pan on the left
box(P, "WoodDark", (-0.3, 0.9, 0.0), (0.36, 0.02, 0.26), bevel=0.004)
cyl(P, "SteelDark", (-0.3, 0.91, 0.0), (-0.3, 0.95, 0.0), 0.1, 0.11, sides=20)

# 8206 Fridge (0.7 x 1.9 x 0.7): a tall body, two doors, long handles
P = 8206
box(P, "Steel", (0, 0.95, 0.01), (0.7, 1.86, 0.66), bevel=0.02, segments=2)
box(P, "SteelDark", (0, 0.03, 0.01), (0.66, 0.06, 0.62))
box(P, "Chrome", (0, 1.25, -0.335), (0.68, 1.22, 0.03), bevel=0.012, segments=2)
box(P, "Chrome", (0, 0.36, -0.335), (0.68, 0.54, 0.03), bevel=0.012, segments=2)
box(P, "SteelDark", (0, 0.645, -0.34), (0.66, 0.01, 0.03))
for y0, y1 in ((0.95, 1.55), (0.45, 0.6)):
    cyl(P, "SteelDark", (0.27, y0, -0.375), (0.27, y1, -0.375), 0.014, sides=10)
box(P, "Screen", (-0.18, 1.55, -0.352), (0.14, 0.08, 0.004))

# 8207 TV unit (1.6 x 1.3 x 0.45): a low cabinet, a flat screen on a stand, a speaker bar
P = 8207
box(P, "WoodDark", (0, 0.24, 0), (1.6, 0.44, 0.45), bevel=0.008)
box(P, "Wood", (0, 0.465, 0), (1.62, 0.03, 0.47), bevel=0.006)
for x in (-0.54, 0.0, 0.54):
    box(P, "Wood", (x, 0.24, -0.226), (0.5, 0.36, 0.01), bevel=0.004)
box(P, "SteelDark", (0, 0.49, 0.05), (0.36, 0.02, 0.2), bevel=0.004)
box(P, "SteelDark", (0, 0.58, 0.08), (0.06, 0.18, 0.04))
box(P, "SteelDark", (0, 0.92, 0.07), (1.4, 0.8, 0.05), bevel=0.01)
box(P, "Screen", (0, 0.93, 0.043), (1.34, 0.74, 0.004))
box(P, "SteelDark", (0, 0.515, -0.12), (0.9, 0.07, 0.09), bevel=0.02, segments=2)

# 8208 Bench (1.6 x 0.46 x 0.45): a slatted wooden seat on two steel frames
P = 8208
for x in (-0.68, 0.68):
    box(P, "SteelDark", (x, 0.21, -0.18), (0.04, 0.42, 0.04), bevel=0.003)
    box(P, "SteelDark", (x, 0.21, 0.18), (0.04, 0.42, 0.04), bevel=0.003)
    box(P, "SteelDark", (x, 0.4, 0), (0.05, 0.03, 0.42), bevel=0.003)
    box(P, "SteelDark", (x, 0.02, 0), (0.06, 0.03, 0.44), bevel=0.003)
for i in range(4):
    z = -0.165 + i * 0.11
    box(P, "Wood", (0, 0.44, z), (1.6, 0.04, 0.095), bevel=0.006)

# 8209 Office chair (0.6 x 1.1 x 0.6): a five-star base on casters, a gas lift, a padded seat and back, arms
P = 8209
for i in range(5):
    a = math.radians(90 + 72 * i)
    tip = (0.28 * math.cos(a), 0.08, 0.28 * math.sin(a))
    cyl(P, "SteelDark", (0, 0.08, 0), (tip[0], 0.07, tip[2]), 0.022, sides=10)
    cyl(P, "SteelDark", (tip[0], 0.0, tip[2]), (tip[0], 0.055, tip[2]), 0.028, sides=12)
cyl(P, "Chrome", (0, 0.08, 0), (0, 0.42, 0), 0.025, sides=14)
cyl(P, "SteelDark", (0, 0.06, 0), (0, 0.2, 0), 0.04, sides=14)
box(P, "SteelDark", (0, 0.43, 0), (0.3, 0.03, 0.3))
box(P, "FabricDark", (0, 0.48, -0.02), (0.52, 0.08, 0.5), bevel=0.03, segments=3)
box(P, "SteelDark", (0, 0.62, 0.26), (0.06, 0.3, 0.04))
box(P, "FabricDark", (0, 0.86, 0.26), (0.46, 0.5, 0.07), bevel=0.03, segments=3)
for s in (-1, 1):
    box(P, "SteelDark", (s * 0.25, 0.58, 0.0), (0.03, 0.18, 0.03))
    box(P, "FabricDark", (s * 0.25, 0.68, -0.02), (0.06, 0.03, 0.26), bevel=0.012)

# 8210 Round table (1 x 0.75 x 1): a round wooden top on a pedestal
P = 8210
cyl(P, "Wood", (0, 0.71, 0), (0, 0.75, 0), 0.5, sides=40, bevel=0.006)
cyl(P, "WoodDark", (0, 0.69, 0), (0, 0.71, 0), 0.42, sides=32)
cyl(P, "SteelDark", (0, 0.03, 0), (0, 0.69, 0), 0.05, sides=16)
cyl(P, "SteelDark", (0, 0.0, 0), (0, 0.03, 0), 0.3, 0.28, sides=32)
cyl(P, "Pot", (0.12, 0.75, 0.1), (0.12, 0.85, 0.1), 0.04, 0.05, sides=14)
cyl(P, "Leaf", (0.12, 0.85, 0.1), (0.12, 0.92, 0.1), 0.05, 0.02, sides=10)

# 8211 Planter box (1.2 x 0.75 x 0.4): a long wooden box, soil, a row of leafy plants
P = 8211
box(P, "Wood", (0, 0.2, 0), (1.2, 0.4, 0.4), bevel=0.008)
for s in (-1, 1):
    box(P, "WoodDark", (0, 0.39, s * 0.19), (1.22, 0.04, 0.04), bevel=0.004)
    box(P, "WoodDark", (s * 0.59, 0.39, 0), (0.04, 0.04, 0.4), bevel=0.004)
box(P, "Soil", (0, 0.39, 0), (1.12, 0.03, 0.34))
for k in range(5):
    cx = -0.48 + k * 0.24
    for i in range(6):
        a = math.radians(i * 60 + k * 17)
        top = (cx + 0.08 * math.cos(a), 0.55 + random.uniform(0.0, 0.18), 0.08 * math.sin(a))
        rod(P, "Leaf", (cx, 0.4, 0), top, 0.006, sides=5)
        d = (math.cos(a), 0, math.sin(a))
        tip = (top[0] + d[0] * 0.12, top[1] + 0.03, top[2] + d[2] * 0.12)
        side = (-d[2] * 0.04, 0, d[0] * 0.04)
        mid = (top[0] + d[0] * 0.06, top[1] + 0.025, top[2] + d[2] * 0.06)
        solid(P, "Leaf", [top, (mid[0] + side[0], mid[1], mid[2] + side[2]), tip, (mid[0] - side[0], mid[1], mid[2] - side[2]),
                          (top[0], top[1] + 0.005, top[2]), (mid[0] + side[0], mid[1] + 0.005, mid[2] + side[2]), (tip[0], tip[1] + 0.005, tip[2]), (mid[0] - side[0], mid[1] + 0.005, mid[2] - side[2])])

# ======================= Stairs (8220-8229) =======================
# Straight flights climb toward +z: the foot at -z (in front of the player placing them), the top
# edge at +z, rise H over run L. Spirals turn counterclockwise seen from above.


def stringer(P, look, x, H, L, depth=0.16, t=0.04):
    """A sloped side board under the steps, from the foot to the top edge."""
    z0, z1 = -L / 2, L / 2
    pts = [(x - t / 2, 0.0, z0 + 0.12), (x - t / 2, H - depth, z1), (x - t / 2, H, z1), (x - t / 2, depth, z0), (x - t / 2, 0.0, z0)]
    solid(P, look, pts + [(p[0] + t, p[1], p[2]) for p in pts])


def flight(P, style, H, L):
    n = max(4, int(round(H / 0.2)))
    rise, run = H / n, L / n
    w = 1.0
    for i in range(n):
        y = (i + 1) * rise
        zc = -L / 2 + (i + 0.5) * run
        if style == "wood":
            box(P, "Wood", (0, y - 0.025, zc + 0.01), (w - 0.1, 0.05, run + 0.02), bevel=0.006)
            box(P, "WoodDark", (0, y - rise / 2 - 0.02, zc - run / 2 + 0.012), (w - 0.12, rise - 0.05, 0.02))
        elif style == "iron":
            box(P, "SteelDark", (0, y - 0.02, zc), (w - 0.1, 0.04, run - 0.01), bevel=0.003)
            for k in range(7):
                x = -0.39 + k * 0.13
                box(P, "Steel", (x, y - 0.002, zc), (0.012, 0.006, run - 0.04))
            box(P, "Steel", (0, y - 0.01, zc - run / 2 + 0.02), (w - 0.1, 0.02, 0.03))
        elif style == "glass":
            box(P, "Glass", (0, y - 0.025, zc), (w - 0.06, 0.04, run - 0.01))
            box(P, "Chrome", (0, y - 0.05, zc), (0.16, 0.02, run * 0.8), bevel=0.004)
            for x in (-0.4, 0.4):
                cyl(P, "Chrome", (x, y - 0.05, zc), (x, y - 0.002, zc), 0.012, sides=8)
        else:  # future: thick dark blocks, a light strip on each nosing, light under the tread
            box(P, "SteelDark", (0, y - 0.04, zc), (w - 0.04, 0.08, run), bevel=0.012, segments=2)
            box(P, "Neon", (0, y - 0.01, zc - run / 2 + 0.008), (w - 0.12, 0.012, 0.012))
            box(P, "Neon", (0, y - 0.081, zc), (w - 0.3, 0.004, run * 0.5))
    if style == "wood":
        for x in (-0.47, 0.47):
            stringer(P, "WoodDark", x, H, L)
    elif style == "iron":
        for x in (-0.47, 0.47):
            stringer(P, "Steel", x, H, L, depth=0.18, t=0.03)
    elif style == "glass":
        # One central chrome spine under the treads
        stringer(P, "Chrome", 0.0, H - 0.04, L, depth=0.1, t=0.08)
    else:
        for x in (-0.49, 0.49):
            stringer(P, "SteelDark", x, H, L, depth=0.2, t=0.02)
        # A light line along each side, just above the nosings
        z0, z1 = -L / 2, L / 2
        for x in (-0.5, 0.5):
            solid(P, "Neon", [(x - 0.004, 0.02, z0), (x - 0.004, H + 0.02, z1), (x - 0.004, H + 0.035, z1), (x - 0.004, 0.035, z0),
                              (x + 0.004, 0.02, z0), (x + 0.004, H + 0.02, z1), (x + 0.004, H + 0.035, z1), (x + 0.004, 0.035, z0)])


flight(8220, "wood", 1.0, 1.0)
flight(8221, "wood", 1.0, 2.0)
flight(8222, "iron", 1.0, 1.0)
flight(8223, "iron", 1.0, 2.0)
flight(8224, "glass", 1.0, 1.0)
flight(8225, "glass", 1.0, 2.0)
flight(8226, "future", 1.0, 1.0)
flight(8227, "future", 1.0, 2.0)


def spiral(P, style, H=2.0, turn=270.0, steps=9, r0=0.1, r1=0.97):
    rise = H / steps
    da = math.radians(turn / steps)
    pole = "SteelDark" if style == "iron" else "Chrome"
    cyl(P, pole, (0, 0.0, 0), (0, H, 0), 0.08, sides=20)
    cyl(P, "SteelDark", (0, 0.0, 0), (0, 0.03, 0), 0.2, sides=24)
    for i in range(steps):
        a0, a1 = i * da, (i + 1) * da
        y = (i + 1) * rise
        # A wedge tread from the pole out, its outer edge a little wider than the inner
        pts = []
        for (r, a) in ((r0, a0), (r1, a0), (r1, a1), (r0, a1)):
            pts.append((r * math.cos(a), 0, r * math.sin(a)))
        t = 0.05 if style == "iron" else 0.06
        look = "Steel" if style == "iron" else "Glass"
        solid(P, look, [(p[0], y - t, p[2]) for p in pts] + [(p[0], y, p[2]) for p in pts])
        am = (a0 + a1) / 2
        if style == "iron":
            # A railing post at each tread's outer edge, chequer bars on top
            cyl(P, "SteelDark", (0.93 * math.cos(am), y, 0.93 * math.sin(am)), (0.93 * math.cos(am), y + 0.9, 0.93 * math.sin(am)), 0.014, sides=8)
            for k in range(3):
                r = 0.35 + k * 0.2
                cyl(P, "SteelDark", (r * math.cos(a0 + 0.08), y + 0.002, r * math.sin(a0 + 0.08)), (r * math.cos(a1 - 0.08), y + 0.002, r * math.sin(a1 - 0.08)), 0.006, sides=5)
        else:
            # A lit edge on each tread's front and a ring of light on the pole
            solid(P, "Neon", [(r0 * math.cos(a0), y - 0.004, r0 * math.sin(a0)), (r1 * math.cos(a0), y - 0.004, r1 * math.sin(a0)),
                              (r1 * math.cos(a0 + 0.02), y - 0.004, r1 * math.sin(a0 + 0.02)), (r0 * math.cos(a0 + 0.02), y - 0.004, r0 * math.sin(a0 + 0.02)),
                              (r0 * math.cos(a0), y + 0.004, r0 * math.sin(a0)), (r1 * math.cos(a0), y + 0.004, r1 * math.sin(a0)),
                              (r1 * math.cos(a0 + 0.02), y + 0.004, r1 * math.sin(a0 + 0.02)), (r0 * math.cos(a0 + 0.02), y + 0.004, r0 * math.sin(a0 + 0.02))])
            cyl(P, "Neon", (0, y - 0.03, 0), (0, y - 0.01, 0), 0.085, sides=20)
    if style == "iron":
        # The handrail: a polyline over the post tops
        prev = None
        for i in range(steps + 1):
            a = i * da
            p = (0.93 * math.cos(a), i * rise + 0.9 + rise / 2, 0.93 * math.sin(a))
            if prev is not None:
                rod(P, "Steel", prev, p, 0.02, sides=10)
            prev = p


spiral(8228, "iron")
spiral(8229, "future")


# ======================= Futuristic furniture (8240-8256) =======================

def lathe(part, look, profile, sides=32):
    """A closed solid of revolution around y from a closed (r, y) profile, counterclockwise in (r, y)."""
    bm = bmesh.new()
    rings = []
    for (r, y) in profile:
        if r < 1e-6:
            center = bm.verts.new(B((0.0, y, 0.0)))
            rings.append([center] * sides)
        else:
            rings.append([bm.verts.new(B((r * math.cos(2 * math.pi * i / sides), y, r * math.sin(2 * math.pi * i / sides)))) for i in range(sides)])
    n = len(rings)
    for k in range(n):
        r0, r1 = rings[k], rings[(k + 1) % n]
        for i in range(sides):
            j = (i + 1) % sides
            face = []
            for v in (r0[i], r0[j], r1[j], r1[i]):
                if v not in face:
                    face.append(v)
            if len(face) >= 3:
                try:
                    bm.faces.new(face)
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    name = "C%d_%s_lathe" % (part, look)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    return _finish(obj, "C%d_%s" % (part, look))


def ball(part, look, center, r, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=B(center))
    obj = bpy.context.active_object
    obj.name = "C%d_%s_ball" % (part, look)
    return _finish(obj, "C%d_%s" % (part, look))


def ring(part, look, center, r, minor, sides=40):
    """A flat torus in the x-z plane."""
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=minor, major_segments=sides, minor_segments=8, location=B(center))
    obj = bpy.context.active_object
    obj.name = "C%d_%s_ring" % (part, look)
    return _finish(obj, "C%d_%s" % (part, look))


# 7486 Tractor beam (0.8 x 0.64 x 0.8): an armored square foot, a turning collar, a dish of coils, the emitter
P = 7486
box(P, "SteelDark", (0, 0.06, 0), (0.8, 0.12, 0.8), bevel=0.02, segments=2)
box(P, "Steel", (0, 0.124, 0), (0.7, 0.012, 0.7), bevel=0.004)
for x in (-0.33, 0.33):
    for z in (-0.33, 0.33):
        cyl(P, "Chrome", (x, 0.12, z), (x, 0.135, z), 0.022, sides=10)
lathe(P, "Steel", [(0.0, 0.13), (0.3, 0.13), (0.3, 0.2), (0.27, 0.225), (0.0, 0.225)])
ring(P, "Green", (0, 0.215, 0), 0.285, 0.012)
# The dish: a thick bowl, dark inside, a chrome lip
lathe(P, "SteelDark", [(0.0, 0.225), (0.17, 0.225), (0.4, 0.43), (0.4, 0.45), (0.37, 0.45), (0.15, 0.27), (0.0, 0.27)], sides=40)
ring(P, "Chrome", (0, 0.45, 0), 0.385, 0.018, sides=48)
for i in range(8):
    a = math.radians(i * 45 + 22.5)
    c, s = math.cos(a), math.sin(a)
    # A coil on the bowl pointing at the emitter, a lit tip
    cyl(P, "Steel", (0.31 * c, 0.37, 0.31 * s), (0.22 * c, 0.5, 0.22 * s), 0.028, 0.02, sides=12)
    for k in range(3):
        t = 0.25 + k * 0.25
        p = (0.31 * c + (0.22 - 0.31) * c * t, 0.37 + 0.13 * t, 0.31 * s + (0.22 - 0.31) * s * t)
        cyl(P, "Chrome", (p[0], p[1] - 0.006, p[2]), (p[0], p[1] + 0.006, p[2]), 0.034, sides=12)
    ball(P, "Green", (0.22 * c, 0.505, 0.22 * s), 0.022, segments=10, rings=6)
# The emitter: a mast with lit rings and a glowing lens
cyl(P, "Chrome", (0, 0.27, 0), (0, 0.56, 0), 0.045, 0.035, sides=16)
for y in (0.34, 0.42, 0.5):
    ring(P, "Green", (0, y, 0), 0.06, 0.012, sides=20)
ball(P, "Lamp", (0, 0.585, 0), 0.04, segments=14, rings=8)
cyl(P, "SteelDark", (0, 0.55, 0), (0, 0.575, 0), 0.055, sides=16)

# 8240 Lab bench (1.6 x 0.95 x 0.7): white cupboards, a dark top, a microscope, glowing flasks
P = 8240
box(P, "SteelDark", (0, 0.04, 0.02), (1.54, 0.08, 0.62))
box(P, "White", (0, 0.45, 0.01), (1.6, 0.76, 0.66), bevel=0.012, segments=2)
for x in (-0.6, -0.2, 0.2, 0.6):
    box(P, "White", (x, 0.45, -0.325), (0.38, 0.66, 0.012), bevel=0.004)
    box(P, "SteelDark", (x + 0.13, 0.62, -0.335), (0.012, 0.14, 0.012))
box(P, "Neon", (0, 0.82, -0.33), (1.56, 0.012, 0.006))
box(P, "SteelDark", (0, 0.855, 0), (1.62, 0.05, 0.7), bevel=0.008)
# The microscope
box(P, "SteelDark", (-0.45, 0.895, 0.05), (0.18, 0.03, 0.22), bevel=0.006)
cyl(P, "White", (-0.45, 0.9, 0.13), (-0.45, 1.12, 0.1), 0.025, sides=12)
cyl(P, "Chrome", (-0.45, 1.13, 0.08), (-0.45, 1.0, -0.02), 0.022, sides=14)
cyl(P, "SteelDark", (-0.45, 1.16, 0.1), (-0.45, 1.22, 0.14), 0.014, sides=10)
cyl(P, "Chrome", (-0.45, 0.98, 0.02), (-0.45, 0.985, 0.02), 0.06, sides=16)
# Flasks
for k, (x, z, h) in enumerate(((0.15, 0.05, 0.16), (0.3, -0.05, 0.2), (0.45, 0.1, 0.13))):
    lathe(P, "Glass", [(0.0, 0.88), (0.05, 0.88), (0.05, 0.88 + h * 0.5), (0.018, 0.88 + h * 0.8), (0.018, 0.88 + h), (0.0, 0.88 + h)], sides=16)
    cyl(P, "Green", (x, 0.885, z), (x, 0.88 + h * 0.4, z), 0.044, sides=14)
# (the lathes are made on the axis: move each flask in place)
flasks = sorted([o for o in bpy.data.objects if o.name.startswith("C8240_Glass_lathe")], key=lambda o: o.name)
for o, (x, z, h) in zip(flasks, ((0.15, 0.05, 0.16), (0.3, -0.05, 0.2), (0.45, 0.1, 0.13))):
    o.location = B((x, 0, z))
box(P, "ScreenLit", (-0.05, 0.9, 0.18), (0.26, 0.02, 0.18), rotation=(0, 0, 0))

# 8241 Specimen tank (0.8 x 2.1 x 0.8): a steel base and cap, a glass tube, the green liquid, a floating shape
P = 8241
lathe(P, "SteelDark", [(0.0, 0.0), (0.4, 0.0), (0.4, 0.18), (0.36, 0.25), (0.0, 0.25)])
ring(P, "Neon", (0, 0.2, 0), 0.385, 0.01)
cyl(P, "Glass", (0, 0.25, 0), (0, 1.88, 0), 0.33, sides=32)
cyl(P, "Green", (0, 0.27, 0), (0, 1.6, 0), 0.31, sides=32)
ball(P, "SteelDark", (0, 1.0, 0), 0.14, segments=14, rings=10)
lathe(P, "SteelDark", [(0.0, 1.88), (0.36, 1.88), (0.4, 1.95), (0.4, 2.06), (0.2, 2.1), (0.0, 2.1)])
ring(P, "Neon", (0, 1.93, 0), 0.385, 0.01)
for a in (45, 135, 225, 315):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    cyl(P, "Chrome", (0.35 * c, 0.25, 0.35 * s), (0.35 * c, 1.88, 0.35 * s), 0.014, sides=8)

# 8242 Analysis console (1.0 x 1.3 x 0.6): a white stand, a slanted desk, a lit screen and keys
P = 8242
box(P, "SteelDark", (0, 0.02, 0.05), (0.6, 0.04, 0.45), bevel=0.006)
box(P, "White", (0, 0.42, 0.08), (0.3, 0.8, 0.3), bevel=0.02, segments=2)
box(P, "White", (0, 0.86, -0.04), (1.0, 0.06, 0.5), bevel=0.012, rotation=(-18, 0, 0))
box(P, "Neon", (0, 0.88, -0.1), (0.7, 0.012, 0.22), rotation=(-18, 0, 0))
box(P, "SteelDark", (0, 1.08, 0.2), (0.9, 0.42, 0.04), bevel=0.01, rotation=(12, 0, 0))
box(P, "ScreenLit", (0, 1.08, 0.177), (0.84, 0.36, 0.005), rotation=(12, 0, 0))

# 8243 Server rack (0.7 x 2.0 x 0.9): a dark cabinet, a glass door, servers with lights
P = 8243
box(P, "SteelDark", (0, 1.0, 0.02), (0.7, 2.0, 0.86), bevel=0.01)
for k in range(9):
    y = 0.22 + k * 0.19
    box(P, "Steel", (0, y, -0.37), (0.6, 0.16, 0.06), bevel=0.004)
    for i in range(6):
        look = "Green" if (i + k) % 3 else ("Red" if k % 4 == 0 else "Neon")
        box(P, look, (-0.24 + i * 0.03, y + 0.04, -0.402), (0.012, 0.012, 0.004))
    box(P, "SteelDark", (0.15, y, -0.401), (0.22, 0.1, 0.004))
box(P, "Glass", (0, 1.0, -0.43), (0.66, 1.94, 0.012))
box(P, "Chrome", (0.3, 1.0, -0.445), (0.016, 0.5, 0.016))
box(P, "Neon", (0, 1.985, -0.4), (0.66, 0.012, 0.01))

# 8244 Cargo crate (1 x 1 x 1): white, reinforced corners, light stripes, a red label
P = 8244
box(P, "White", (0, 0.5, 0), (0.96, 0.96, 0.96), bevel=0.02, segments=2)
for x in (-0.47, 0.47):
    for z in (-0.47, 0.47):
        box(P, "SteelDark", (x, 0.5, z), (0.07, 1.0, 0.07), bevel=0.008)
for y in (0.03, 0.97):
    for s in (-1, 1):
        box(P, "SteelDark", (0, y, s * 0.47), (1.0, 0.06, 0.07), bevel=0.006)
        box(P, "SteelDark", (s * 0.47, y, 0), (0.07, 0.06, 1.0), bevel=0.006)
for s in (-1, 1):
    box(P, "Neon", (0, 0.72, s * 0.483), (0.8, 0.02, 0.006))
    box(P, "Neon", (s * 0.483, 0.72, 0), (0.006, 0.02, 0.8))
box(P, "Red", (-0.22, 0.42, -0.483), (0.26, 0.12, 0.006))

# 8245 Storage rack (1.6 x 2.0 x 0.6): steel uprights, four shelves, crates and bins
P = 8245
for x in (-0.77, 0.77):
    for z in (-0.27, 0.27):
        box(P, "Steel", (x, 1.0, z), (0.05, 2.0, 0.05), bevel=0.004)
random.seed(42)
for k, y in enumerate((0.08, 0.6, 1.12, 1.64)):
    box(P, "SteelDark", (0, y, 0), (1.58, 0.04, 0.58), bevel=0.004)
    box(P, "Neon", (0, y - 0.025, -0.29), (1.5, 0.01, 0.006))
    x = -0.72
    while x < 0.6:
        w = random.uniform(0.25, 0.42)
        h = random.uniform(0.2, 0.38)
        look = random.choice(("White", "White", "SteelDark", "Steel"))
        box(P, look, (x + w / 2, y + 0.02 + h / 2, random.uniform(-0.04, 0.04)), (w - 0.03, h, 0.46), bevel=0.01)
        x += w + 0.02

# 8246 Canisters on a pallet (1 x 1.2 x 1): a steel pallet, two white canisters with red bands
P = 8246
box(P, "SteelDark", (0, 0.06, 0), (1.0, 0.12, 1.0), bevel=0.01)
for x in (-0.3, 0.3):
    box(P, "Steel", (x, 0.02, 0), (0.08, 0.04, 1.0))
for (x, z) in ((-0.24, -0.1), (0.24, 0.12)):
    lathe(P, "White", [(0.0, 0.12), (0.22, 0.12), (0.24, 0.16), (0.24, 1.08), (0.2, 1.14), (0.0, 1.14)])
    o = bpy.context.active_object
    o.location = B((x, 0, z))
    for y in (0.35, 0.85):
        ring(P, "Red", (x, y, z), 0.243, 0.014, sides=32)
    cyl(P, "SteelDark", (x, 1.14, z), (x, 1.19, z), 0.05, sides=12)

# 8247 Sleep pod (1.1 x 1.2 x 2.2): a white shell, a mattress, a glass canopy, lit trim
P = 8247
box(P, "SteelDark", (0, 0.05, 0), (1.0, 0.1, 2.1), bevel=0.01)
box(P, "White", (0, 0.32, 0), (1.1, 0.44, 2.2), bevel=0.08, segments=3)
box(P, "Fabric", (0, 0.58, 0.05), (0.9, 0.12, 1.95), bevel=0.04, segments=2)
box(P, "Sheet", (0, 0.66, 0.78), (0.6, 0.1, 0.3), bevel=0.04, segments=2)
box(P, "FabricDark", (0, 0.65, -0.35), (0.92, 0.04, 1.0), bevel=0.015)
box(P, "White", (0, 0.85, 1.05), (1.1, 0.7, 0.1), bevel=0.04, segments=2)
# The canopy: a half tube of glass over the mattress
bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.52, depth=1.7, location=B((0, 0.6, -0.12)), rotation=(math.radians(90), 0, 0))
can = bpy.context.active_object
can.name = "C8247_Glass_canopy"
bpy.context.view_layer.update()
bm = bmesh.new()
bm.from_mesh(can.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if (can.matrix_world @ v.co).z < 0.6], context='VERTS')
bm.to_mesh(can.data)
bm.free()
_finish(can, "C8247_Glass")
for s in (-1, 1):
    box(P, "Neon", (s * 0.551, 0.4, 0), (0.006, 0.02, 2.0))
box(P, "ScreenLit", (0.3, 0.9, 0.995), (0.3, 0.16, 0.006))

# 8248 Crew lockers (0.9 x 2.0 x 0.5): two tall lockers, vented doors, a light line
P = 8248
box(P, "SteelDark", (0, 1.0, 0.01), (0.9, 2.0, 0.48), bevel=0.01)
for s in (-1, 1):
    box(P, "White", (s * 0.222, 1.02, -0.235), (0.42, 1.88, 0.02), bevel=0.006)
    for k in range(5):
        box(P, "SteelDark", (s * 0.222, 1.65 + k * 0.04, -0.247), (0.26, 0.012, 0.006))
    box(P, "Chrome", (s * 0.05, 1.05, -0.252), (0.016, 0.22, 0.016))
    box(P, "Neon", (s * 0.222, 1.4, -0.247), (0.08, 0.016, 0.004))
box(P, "Neon", (0, 0.06, -0.24), (0.86, 0.012, 0.006))

# 8249 Food synthesizer (0.8 x 1.8 x 0.6): a white unit, a menu screen, a lit hatch, a tray
P = 8249
box(P, "White", (0, 0.9, 0.01), (0.8, 1.8, 0.58), bevel=0.03, segments=2)
box(P, "SteelDark", (0, 0.95, -0.24), (0.56, 0.42, 0.14), bevel=0.01)
box(P, "Lamp", (0, 1.15, -0.2), (0.5, 0.012, 0.1))
box(P, "Chrome", (0, 0.76, -0.24), (0.5, 0.012, 0.16))
cyl(P, "White", (0, 0.77, -0.24), (0, 0.8, -0.24), 0.09, 0.11, sides=20)
box(P, "ScreenLit", (0, 1.45, -0.292), (0.5, 0.26, 0.006))
box(P, "Neon", (0, 1.7, -0.292), (0.6, 0.014, 0.006))
for k in range(4):
    box(P, "SteelDark", (0, 0.2 + k * 0.08, -0.292), (0.5, 0.02, 0.006))

# 8250 Galley island (1.6 x 0.95 x 0.8): a white body, a steel top, two lit induction plates
P = 8250
box(P, "SteelDark", (0, 0.04, 0), (1.5, 0.08, 0.7))
box(P, "White", (0, 0.47, 0), (1.6, 0.8, 0.8), bevel=0.02, segments=2)
box(P, "Neon", (0, 0.085, -0.37), (1.5, 0.012, 0.006))
box(P, "Steel", (0, 0.89, 0), (1.64, 0.04, 0.84), bevel=0.008)
for x in (-0.35, 0.35):
    box(P, "SteelDark", (x, 0.912, 0), (0.5, 0.005, 0.5))
    for r in (0.09, 0.15):
        ring(P, "Red", (x, 0.916, 0), r, 0.005, sides=32)
box(P, "SteelDark", (0, 0.47, -0.402), (1.5, 0.6, 0.006))
cyl(P, "SteelDark", (0.0, 0.915, 0.25), (0.0, 0.97, 0.25), 0.11, 0.12, sides=24)

# 8251 Command desk (1.8 x 1.25 x 0.8): two white sides, a dark top, three lit screens
P = 8251
for x in (-0.84, 0.84):
    box(P, "White", (x, 0.37, 0), (0.1, 0.74, 0.78), bevel=0.02, segments=2)
box(P, "SteelDark", (0, 0.76, 0), (1.8, 0.05, 0.8), bevel=0.012)
box(P, "Neon", (0, 0.733, -0.4), (1.7, 0.01, 0.006))
box(P, "White", (0, 0.45, 0.3), (1.6, 0.5, 0.03))
for (x, yaw) in ((-0.56, 20), (0.0, 0), (0.56, -20)):
    box(P, "SteelDark", (x, 0.8, 0.22), (0.12, 0.03, 0.1))
    box(P, "SteelDark", (x, 0.88, 0.25), (0.03, 0.16, 0.03))
    box(P, "SteelDark", (x, 1.05, 0.24), (0.52, 0.32, 0.03), bevel=0.006, rotation=(0, yaw, 0))
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    box(P, "ScreenLit", (x - 0.017 * s, 1.05, 0.24 - 0.017 * c), (0.48, 0.28, 0.004), rotation=(0, yaw, 0))
box(P, "SteelDark", (0, 0.792, -0.15), (0.5, 0.015, 0.16), bevel=0.003)
box(P, "Neon", (0, 0.801, -0.15), (0.46, 0.003, 0.12))

# 8252 Holo table (1.4 x 1.5 x 1.4): a white drum, a dark glass top ringed with light, a planet above
P = 8252
lathe(P, "White", [(0.0, 0.0), (0.5, 0.0), (0.55, 0.1), (0.42, 0.7), (0.7, 0.82), (0.7, 0.88), (0.0, 0.88)], sides=48)
cyl(P, "Glass", (0, 0.88, 0), (0, 0.9, 0), 0.66, sides=48)
ring(P, "Neon", (0, 0.88, 0), 0.68, 0.012, sides=48)
cyl(P, "Neon", (0, 0.9, 0), (0, 0.905, 0), 0.12, sides=24)
ball(P, "Neon", (0, 1.22, 0), 0.16, segments=20, rings=12)
ring(P, "Neon", (0, 1.22, 0), 0.27, 0.006, sides=48)
ball(P, "Neon", (0.38, 1.3, 0.1), 0.035, segments=10, rings=6)

# 8253 Lounge sofa (2.0 x 0.85 x 0.9): a white shell on a glow, dark cushions
P = 8253
box(P, "SteelDark", (0, 0.05, 0.02), (1.8, 0.1, 0.7), bevel=0.02)
box(P, "Neon", (0, 0.03, 0.02), (1.84, 0.012, 0.74))
box(P, "White", (0, 0.27, 0), (2.0, 0.34, 0.9), bevel=0.06, segments=3)
box(P, "White", (0, 0.6, 0.38), (2.0, 0.5, 0.14), bevel=0.06, segments=3)
for s in (-1, 1):
    box(P, "White", (s * 0.94, 0.55, 0), (0.12, 0.3, 0.9), bevel=0.05, segments=3)
for x in (-0.6, 0.0, 0.6):
    box(P, "FabricDark", (x, 0.47, -0.06), (0.58, 0.1, 0.66), bevel=0.04, segments=3)
    box(P, "FabricDark", (x, 0.66, 0.26), (0.56, 0.32, 0.1), bevel=0.04, segments=3)

# 8254 Mess table (2.0 x 0.75 x 1.6): a long white table and two benches
P = 8254
box(P, "White", (0, 0.73, 0), (2.0, 0.04, 0.8), bevel=0.012)
box(P, "Neon", (0, 0.708, 0), (1.9, 0.006, 0.7))
for x in (-0.8, 0.8):
    box(P, "SteelDark", (x, 0.36, 0), (0.08, 0.7, 0.5), bevel=0.01)
    box(P, "SteelDark", (x, 0.02, 0), (0.12, 0.04, 1.5), bevel=0.006)
for s in (-1, 1):
    box(P, "White", (0, 0.43, s * 0.62), (1.9, 0.06, 0.34), bevel=0.012)
    box(P, "FabricDark", (0, 0.47, s * 0.62), (1.8, 0.03, 0.3), bevel=0.01)
    for x in (-0.8, 0.8):
        box(P, "SteelDark", (x, 0.2, s * 0.62), (0.06, 0.4, 0.2))
for (x, z) in ((-0.5, -0.2), (0.5, -0.2), (-0.5, 0.2), (0.5, 0.2)):
    box(P, "Steel", (x, 0.756, z), (0.32, 0.012, 0.24), bevel=0.004)

# 8255 Vending machine (0.9 x 1.9 x 0.8): white body, a glass front full of boxes, a screen
P = 8255
box(P, "White", (0, 0.95, 0.02), (0.9, 1.9, 0.76), bevel=0.03, segments=2)
box(P, "SteelDark", (-0.1, 1.12, -0.3), (0.6, 1.3, 0.14))
for k in range(5):
    y = 0.58 + k * 0.25
    box(P, "Steel", (-0.1, y - 0.03, -0.3), (0.58, 0.012, 0.12))
    for i in range(5):
        box(P, ("BookA", "BookB", "BookC", "Red", "Green")[(i + k) % 5], (-0.34 + i * 0.12, y + 0.05, -0.3), (0.08, 0.14, 0.08))
box(P, "Glass", (-0.1, 1.12, -0.375), (0.62, 1.32, 0.012))
box(P, "ScreenLit", (0.32, 1.35, -0.362), (0.16, 0.22, 0.006))
for k in range(3):
    for i in range(3):
        box(P, "SteelDark", (0.28 + i * 0.04, 1.12 - k * 0.04, -0.362), (0.03, 0.03, 0.006))
box(P, "SteelDark", (-0.1, 0.3, -0.36), (0.5, 0.14, 0.04))
box(P, "Neon", (0.0, 1.88, -0.362), (0.84, 0.014, 0.006))

# 8256 Medical bed (1.0 x 1.4 x 2.1): a white pedestal, a mattress, an arm and a monitor at the head
P = 8256
box(P, "SteelDark", (0, 0.03, 0), (0.7, 0.06, 1.4), bevel=0.01)
box(P, "White", (0, 0.35, 0), (0.4, 0.6, 1.0), bevel=0.05, segments=2)
box(P, "White", (0, 0.68, 0), (1.0, 0.08, 2.1), bevel=0.03, segments=2)
box(P, "Sheet", (0, 0.77, 0), (0.92, 0.12, 2.0), bevel=0.04, segments=3)
box(P, "Sheet", (0, 0.86, 0.78), (0.56, 0.08, 0.28), bevel=0.035, segments=3)
box(P, "Neon", (0, 0.64, -1.051), (0.9, 0.014, 0.006))
for s in (-1, 1):
    box(P, "Neon", (s * 0.501, 0.68, 0), (0.006, 0.014, 2.0))
cyl(P, "Chrome", (0.42, 0.72, 1.0), (0.42, 1.25, 1.0), 0.02, sides=10)
cyl(P, "Chrome", (0.42, 1.25, 1.0), (0.3, 1.3, 0.8), 0.016, sides=10)
box(P, "SteelDark", (0.3, 1.3, 0.78), (0.36, 0.22, 0.03), bevel=0.006, rotation=(0, 20, 0))
box(P, "ScreenLit", (0.295, 1.3, 0.765), (0.32, 0.18, 0.004), rotation=(0, 20, 0))


export("construction.obj")
