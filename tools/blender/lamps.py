"""The lamps (parts 7101-7126): every lamp's model in one file, its pieces in three looks named
L<id>_Frame (dark metal body), L<id>_Trim (brushed chrome) and L<id>_Diffuser (the lit glass).

Built in the part's own frame (the game's): y out of the surface the lamp is mounted on, x along
it, z across it; the helpers below turn that into Blender's axes (the loader maps Blender
(x, y, z) to the game's (x, z, y)). Each lamp keeps the footprint its part has in the game
(LampCatalog bounds). Light pieces are a few millimeters clear of the surfaces under them (no
flicker), the chrome is satin (thin mirror-bright pieces sparkled)."""
import math
import os
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box as _box, cylinder as _cylinder, export, material, _finish  # noqa: E402


def B(p):
    """A point of the part's frame in Blender's axes."""
    x, y, z = p
    return (x, z, y)


def box(lamp, look, center, size, bevel=0.0, rotation=(0, 0, 0)):
    sx, sy, sz = size
    rx, ry, rz = rotation
    return _box("L%d_%s_box" % (lamp, look), B(center), (sx, sz, sy), "L%d_%s" % (lamp, look), bevel=bevel, segments=2, rotation=(rx, rz, ry))


def cyl(lamp, look, a, b, r, rb=None, sides=32, bevel=0.0, cap=True):
    return _cylinder("L%d_%s_cyl" % (lamp, look), B(a), B(b), r, "L%d_%s" % (lamp, look), vertices=sides, radius_b=rb, bevel=bevel, segments=2, cap=cap)


def hull(lamp, look, points):
    """A solid from eight corners given in the part's frame (bottom four, then top four, each ring in order)."""
    bm = bmesh.new()
    v = [bm.verts.new(B(p)) for p in points]
    for face in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([v[i] for i in face])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    name = "L%d_%s_hull" % (lamp, look)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return _finish(obj, "L%d_%s" % (lamp, look), smooth=False)


def torus(lamp, look, center, major, minor, axis_y=True, sides=48, ring=12):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=sides, minor_segments=ring, location=B(center))
    obj = bpy.context.active_object
    obj.name = "L%d_%s_torus" % (lamp, look)
    # Blender's torus lies in its XY plane, around its Z: the part's y
    return _finish(obj, "L%d_%s" % (lamp, look))


def wedge(lamp, look, x0, x1, size, lip=0.0):
    """The corner fixtures' body: on the surface (y = 0) from z = -size/2 to size/2, against the wall at z = size/2, up to y = size."""
    z0, z1 = -size / 2 - lip, size / 2
    return hull(lamp, look, [(x0, 0, z0), (x1, 0, z0), (x1, 0, z1), (x0, 0, z1),
                             (x0, 0, z0), (x1, 0, z0), (x1, size + lip, z1), (x0, size + lip, z1)])


def slope_strip(lamp, look, length, size, inset, offset=0.004):
    """A thin lit strip on a wedge's sloped face, along x."""
    n = (0.0, 1 / math.sqrt(2), -1 / math.sqrt(2))
    half = length / 2
    s = size - inset
    a = (inset / 2, size - inset / 2)

    def on(x, t, h):
        # t from 0 (bottom edge) to 1 (top edge) of the slope, h along the normal
        y = a[0] + (a[1] - a[0]) * t
        z = -size / 2 + inset / 2 + (size - inset) * t
        return (x, y + n[1] * h, z + n[2] * h)
    pts = [on(-half, 0.15, offset), on(half, 0.15, offset), on(half, 0.85, offset), on(-half, 0.85, offset)]
    top = [on(-half, 0.15, offset + 0.006), on(half, 0.15, offset + 0.006), on(half, 0.85, offset + 0.006), on(-half, 0.85, offset + 0.006)]
    return hull(lamp, look, pts + top)


CORNER_N = (0.0, 1 / math.sqrt(2), -1 / math.sqrt(2))


def along(p, d, t):
    return (p[0] + d[0] * t, p[1] + d[1] * t, p[2] + d[2] * t)


reset()

# ---- 7101 Ceiling light: round 0.44 m, 7 cm: a bevelled base, chrome ring, opal dome
L = 7101
cyl(L, "Frame", (0, 0, 0), (0, 0.022, 0), 0.22, 0.214, sides=48, bevel=0.004)
for i in range(3):
    a = math.radians(30 + 120 * i)
    cyl(L, "Trim", (0.19 * math.cos(a), 0.022, 0.19 * math.sin(a)), (0.19 * math.cos(a), 0.027, 0.19 * math.sin(a)), 0.008, sides=10)
cyl(L, "Trim", (0, 0.022, 0), (0, 0.032, 0), 0.206, 0.2, sides=48, bevel=0.002)
cyl(L, "Diffuser", (0, 0.032, 0), (0, 0.05, 0), 0.19, 0.182, sides=48)
cyl(L, "Diffuser", (0, 0.05, 0), (0, 0.062, 0), 0.182, 0.15, sides=48)
cyl(L, "Diffuser", (0, 0.062, 0), (0, 0.07, 0), 0.15, 0.09, sides=48)

# ---- 7102 Wall light: a 0.2 m plate, frosted glass cube under a chrome cap on four posts
L = 7102
box(L, "Frame", (0, 0.01, 0), (0.2, 0.02, 0.2), bevel=0.006)
box(L, "Frame", (0, 0.03, 0), (0.13, 0.02, 0.13), bevel=0.004)
box(L, "Diffuser", (0, 0.07, 0), (0.11, 0.06, 0.11), bevel=0.006)
for y in (0.055, 0.085):
    box(L, "Trim", (0, y, 0), (0.118, 0.004, 0.118), bevel=0.001)
box(L, "Trim", (0, 0.105, 0), (0.138, 0.01, 0.138), bevel=0.003)
for x in (-0.062, 0.062):
    for z in (-0.062, 0.062):
        cyl(L, "Trim", (x, 0.04, z), (x, 0.1, z), 0.006, sides=10)

# ---- 7103 Floodlight: a yoke on a round base, a finned housing, chrome bezel and lens
L = 7103
cyl(L, "Frame", (0, 0, 0), (0, 0.02, 0), 0.12, 0.112, sides=40, bevel=0.003)
box(L, "Frame", (0, 0.03, 0), (0.18, 0.02, 0.06), bevel=0.004)
for s in (-1, 1):
    box(L, "Frame", (s * 0.108, 0.085, 0), (0.016, 0.13, 0.05), bevel=0.004)
    cyl(L, "Trim", (s * 0.124, 0.14, 0), (s * 0.112, 0.14, 0), 0.02, sides=16)
cyl(L, "Trim", (-0.1, 0.14, 0), (0.1, 0.14, 0), 0.012, sides=12)
cyl(L, "Frame", (0, 0.05, 0), (0, 0.27, 0), 0.075, 0.092, sides=40, bevel=0.003)
for y in (0.075, 0.1, 0.125, 0.15, 0.175):
    cyl(L, "Trim", (0, y, 0), (0, y + 0.007, 0), 0.084 + (y - 0.05) * 0.077, sides=40)
cyl(L, "Frame", (0, 0.04, 0), (0, 0.05, 0), 0.05, 0.075, sides=32)
cyl(L, "Trim", (0, 0.27, 0), (0, 0.29, 0), 0.1, 0.1, sides=40, bevel=0.003)
cyl(L, "Diffuser", (0, 0.283, 0), (0, 0.293, 0), 0.088, 0.085, sides=40)

# ---- 7104 Neon tube: a rail, a long glass tube held by three clips and two end caps
L = 7104
box(L, "Frame", (0, 0.01, 0), (1.0, 0.02, 0.06), bevel=0.005)
cyl(L, "Diffuser", (-0.47, 0.045, 0), (0.47, 0.045, 0), 0.022, sides=24, cap=False)
for s in (-1, 1):
    cyl(L, "Trim", (s * 0.5, 0.045, 0), (s * 0.465, 0.045, 0), 0.027, sides=24, bevel=0.002)
    box(L, "Frame", (s * 0.48, 0.03, 0), (0.03, 0.03, 0.05), bevel=0.003)
for x in (-0.3, 0.0, 0.3):
    cyl(L, "Trim", (x - 0.01, 0.045, 0), (x + 0.01, 0.045, 0), 0.026, sides=20)
    box(L, "Trim", (x, 0.026, 0), (0.016, 0.014, 0.04), bevel=0.002)


def corner(L, length, size):
    cap = 0.012
    wedge(L, "Frame", -length / 2 + cap, length / 2 - cap, size)
    wedge(L, "Trim", -length / 2, -length / 2 + cap, size, lip=0.005)
    wedge(L, "Trim", length / 2 - cap, length / 2, size, lip=0.005)
    slope_strip(L, "Diffuser", length - 2 * cap - 0.02, size, 0.0)


# ---- 7105 Corner lamp, 7106 Corner neon: a wedge in the angle, chrome end caps, a lit strip on its slope
corner(7105, 0.4, 0.14)
corner(7106, 1.0, 0.09)

# ---- 7107 Corner floodlight: a short wedge carrying a finned lamp aimed out of the angle
L = 7107
size = 0.16
wedge(L, "Frame", -0.1, 0.1, size)
face = (0, size / 2, 0)
lens = along(face, CORNER_N, 0.13)
cyl(L, "Frame", face, lens, 0.058, 0.07, sides=32)
for t in (0.035, 0.06, 0.085):
    p = along(face, CORNER_N, t)
    cyl(L, "Trim", p, along(p, CORNER_N, 0.006), 0.066 + t * 0.06, sides=32)
cyl(L, "Trim", lens, along(lens, CORNER_N, 0.015), 0.078, sides=32)
cyl(L, "Diffuser", along(lens, CORNER_N, 0.009), along(lens, CORNER_N, 0.019), 0.066, 0.063, sides=32)

# ---- 7108 Recessed spot: a flush chrome bezel, a dark reflector, the lens
L = 7108
cyl(L, "Trim", (0, 0, 0), (0, 0.01, 0), 0.085, 0.08, sides=40, bevel=0.002)
cyl(L, "Frame", (0, 0.004, 0), (0, 0.012, 0), 0.07, 0.066, sides=40)
cyl(L, "Diffuser", (0, 0.008, 0), (0, 0.015, 0), 0.056, 0.052, sides=40)

# ---- 7109 Industrial lantern: a glass globe in a chrome cage on a round base, under a cap
L = 7109
cyl(L, "Frame", (0, 0, 0), (0, 0.025, 0), 0.08, 0.074, sides=32, bevel=0.003)
cyl(L, "Diffuser", (0, 0.025, 0), (0, 0.15, 0), 0.05, 0.05, sides=32)
cyl(L, "Diffuser", (0, 0.15, 0), (0, 0.16, 0), 0.05, 0.035, sides=32)
for i in range(6):
    a = math.radians(60 * i)
    x, z = 0.058 * math.cos(a), 0.058 * math.sin(a)
    cyl(L, "Trim", (x, 0.025, z), (x, 0.165, z), 0.0045, sides=8)
for y in (0.07, 0.12):
    torus(L, "Trim", (0, y, 0), 0.058, 0.004, sides=32, ring=6)
cyl(L, "Frame", (0, 0.16, 0), (0, 0.19, 0), 0.072, 0.045, sides=32, bevel=0.003)

# ---- 7110 Light panel: a thin 0.6 m square, chrome border, the lit panel inset in it
L = 7110
box(L, "Frame", (0, 0.008, 0), (0.6, 0.016, 0.6), bevel=0.004)
for s in (-1, 1):
    box(L, "Trim", (s * 0.29, 0.02, 0), (0.02, 0.01, 0.6), bevel=0.002)
    box(L, "Trim", (0, 0.02, s * 0.29), (0.56, 0.01, 0.02), bevel=0.002)
box(L, "Diffuser", (0, 0.021, 0), (0.56, 0.006, 0.56))

# ---- 7113 Pendant lamp: a canopy, a thin cable, a socket and a wide shade with its glass under it
L = 7113
cyl(L, "Frame", (0, 0, 0), (0, 0.02, 0), 0.06, 0.05, sides=32, bevel=0.003)
cyl(L, "Trim", (0, 0.02, 0), (0, 0.42, 0), 0.004, sides=8)
cyl(L, "Trim", (0, 0.42, 0), (0, 0.455, 0), 0.018, 0.026, sides=24, bevel=0.002)
cyl(L, "Frame", (0, 0.45, 0), (0, 0.585, 0), 0.035, 0.165, sides=48, bevel=0.002)
torus(L, "Trim", (0, 0.585, 0), 0.165, 0.005, sides=48, ring=8)
cyl(L, "Diffuser", (0, 0.578, 0), (0, 0.592, 0), 0.155, 0.148, sides=48)

# ---- 7114 Flashing beacon: a small base, a ribbed red dome under a chrome cage
L = 7114
cyl(L, "Frame", (0, 0, 0), (0, 0.03, 0), 0.06, 0.054, sides=32, bevel=0.003)
cyl(L, "Trim", (0, 0.03, 0), (0, 0.036, 0), 0.05, sides=32)
cyl(L, "Diffuser", (0, 0.036, 0), (0, 0.075, 0), 0.045, 0.036, sides=32)
cyl(L, "Diffuser", (0, 0.075, 0), (0, 0.088, 0), 0.036, 0.012, sides=32)
for i in range(3):
    a = math.radians(120 * i)
    cyl(L, "Trim", (0.05 * math.cos(a), 0.034, 0.05 * math.sin(a)), (0.022 * math.cos(a), 0.09, 0.022 * math.sin(a)), 0.003, sides=6)

# ---- 7115 Rotating beacon: a base, a chrome ring, a tall amber dome, a cap
L = 7115
cyl(L, "Frame", (0, 0, 0), (0, 0.04, 0), 0.08, 0.074, sides=40, bevel=0.003)
cyl(L, "Trim", (0, 0.04, 0), (0, 0.05, 0), 0.068, sides=40)
cyl(L, "Diffuser", (0, 0.05, 0), (0, 0.15, 0), 0.06, 0.055, sides=40)
cyl(L, "Diffuser", (0, 0.15, 0), (0, 0.165, 0), 0.055, 0.03, sides=40)
cyl(L, "Frame", (0, 0.162, 0), (0, 0.17, 0), 0.03, 0.022, sides=24)

# ---- 7116 LED strip: a 2 m aluminium channel and its lit strip
L = 7116
box(L, "Trim", (0, 0.003, 0), (2.0, 0.006, 0.03), bevel=0.001)
for s in (-1, 1):
    box(L, "Trim", (0, 0.007, s * 0.0135), (2.0, 0.008, 0.003))
box(L, "Diffuser", (0, 0.009, 0), (1.98, 0.004, 0.02))

# ---- 7117 Floor lamp: a weighted base, a chrome stem with a joint, a shade with its glass
L = 7117
cyl(L, "Frame", (0, 0, 0), (0, 0.03, 0), 0.15, 0.14, sides=48, bevel=0.006)
cyl(L, "Trim", (0, 0.03, 0), (0, 0.045, 0), 0.04, 0.025, sides=24)
cyl(L, "Trim", (0, 0.045, 0), (0, 1.43, 0), 0.014, sides=16)
cyl(L, "Trim", (0, 0.8, 0), (0, 0.83, 0), 0.022, sides=16, bevel=0.003)
cyl(L, "Diffuser", (0, 1.44, 0), (0, 1.455, 0), 0.162, sides=48)
cyl(L, "Frame", (0, 1.43, 0), (0, 1.7, 0), 0.17, 0.12, sides=48, cap=False)
torus(L, "Trim", (0, 1.43, 0), 0.17, 0.005, sides=48, ring=8)

# ---- 7118 Desk lamp: a round base, two arms and a head aimed down and forward
L = 7118
cyl(L, "Frame", (0, 0, 0), (0, 0.025, 0), 0.1, 0.095, sides=40, bevel=0.005)
cyl(L, "Trim", (0, 0.025, 0.04), (0, 0.04, 0.04), 0.022, sides=20)
j1, j2 = (0, 0.04, 0.04), (0, 0.3, -0.05)
cyl(L, "Trim", j1, j2, 0.008, sides=12)
cyl(L, "Frame", (-0.02, 0.3, -0.05), (0.02, 0.3, -0.05), 0.018, sides=16)
head = (0, 0.42, 0.13)
cyl(L, "Trim", j2, head, 0.008, sides=12)
aim = (0.0, -0.75, 0.66)
cyl(L, "Frame", head, along(head, aim, 0.03), 0.02, 0.026, sides=24)
cyl(L, "Frame", along(head, aim, 0.03), along(head, aim, 0.1), 0.026, 0.065, sides=32, cap=False)
cyl(L, "Diffuser", along(head, aim, 0.085), along(head, aim, 0.092), 0.055, sides=32)

# ---- 7119 Bulkhead light: an oval chrome frame, a ribbed glass and a three-bar guard
L = 7119
bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1, depth=1)
oval = bpy.context.active_object
oval.name = "L7119_Frame_oval"
oval.scale = (0.13, 0.09, 0.03)
oval.location = B((0, 0.015, 0))
bpy.ops.object.transform_apply(location=True, rotation=False, scale=True)
_finish(oval, "L7119_Frame")
bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1, depth=1)
glass = bpy.context.active_object
glass.name = "L7119_Diffuser_oval"
glass.scale = (0.105, 0.068, 0.022)
glass.location = B((0, 0.036, 0))
bpy.ops.object.transform_apply(location=True, rotation=False, scale=True)
_finish(glass, "L7119_Diffuser")
for x in (-0.06, 0.0, 0.06):
    box(L, "Trim", (x, 0.05, 0), (0.008, 0.008, 0.15), bevel=0.002)
box(L, "Trim", (0, 0.052, 0), (0.22, 0.006, 0.008), bevel=0.002)

# ---- 7120 Ring chandelier: a canopy, three cables and a lit ring 0.6 m across in a chrome band
L = 7120
cyl(L, "Frame", (0, 0, 0), (0, 0.025, 0), 0.08, 0.07, sides=32, bevel=0.003)
for i in range(3):
    a = math.radians(90 + 120 * i)
    cyl(L, "Trim", (0.03 * math.cos(a), 0.02, 0.03 * math.sin(a)), (0.29 * math.cos(a), 0.5, 0.29 * math.sin(a)), 0.002, sides=6)
torus(L, "Frame", (0, 0.505, 0), 0.3, 0.018, sides=64, ring=12)
torus(L, "Diffuser", (0, 0.526, 0), 0.3, 0.012, sides=64, ring=10)

# ---- 7125 Up and down wall light: a chrome cylinder along the wall, lit at both ends
L = 7125
box(L, "Frame", (0, 0.008, 0), (0.07, 0.016, 0.12), bevel=0.004)
cyl(L, "Frame", (0, 0.016, 0), (0, 0.04, 0), 0.016, sides=16)
cyl(L, "Trim", (0, 0.065, -0.12), (0, 0.065, 0.12), 0.042, sides=40, bevel=0.003)
for s in (-1, 1):
    cyl(L, "Diffuser", (0, 0.065, s * 0.12), (0, 0.065, s * 0.128), 0.034, sides=32)
    cyl(L, "Frame", (0, 0.065, s * 0.105), (0, 0.065, s * 0.112), 0.044, sides=40)

# ---- 7126 Track spots: an 0.8 m rail carrying three heads aimed out of it
L = 7126
box(L, "Frame", (0, 0.015, 0), (0.8, 0.03, 0.04), bevel=0.004)
box(L, "Trim", (0, 0.031, 0), (0.78, 0.004, 0.012))
for x, tilt in ((-0.28, -0.25), (0.0, 0.0), (0.28, 0.25)):
    d = (math.sin(tilt), math.cos(tilt), 0.0)
    base = (x, 0.035, 0)
    cyl(L, "Trim", base, along(base, (0, 1, 0), 0.03), 0.006, sides=10)
    joint = along(base, (0, 1, 0), 0.03)
    cyl(L, "Frame", joint, along(joint, d, 0.09), 0.03, 0.036, sides=32, bevel=0.002)
    end = along(joint, d, 0.09)
    cyl(L, "Trim", end, along(end, d, 0.008), 0.038, sides=32)
    cyl(L, "Diffuser", along(end, d, 0.002), along(end, d, 0.01), 0.03, sides=32)

# ---- 7127 Light tile 1 x 1 (0.04): a satin frame, the lit panel and a thin grid of chrome
L = 7127
box(L, "Frame", (0, 0.01, 0), (1.0, 0.02, 1.0), bevel=0.004)
for s in (-1, 1):
    box(L, "Trim", (s * 0.485, 0.03, 0), (0.03, 0.02, 1.0), bevel=0.003)
    box(L, "Trim", (0, 0.03, s * 0.485), (0.94, 0.02, 0.03), bevel=0.003)
box(L, "Diffuser", (0, 0.03, 0), (0.94, 0.012, 0.94))
for k in (-1, 1):
    box(L, "Trim", (k * 0.157, 0.0385, 0), (0.008, 0.004, 0.94))
    box(L, "Trim", (0, 0.0385, k * 0.157), (0.94, 0.004, 0.008))

export("lamps.obj")
