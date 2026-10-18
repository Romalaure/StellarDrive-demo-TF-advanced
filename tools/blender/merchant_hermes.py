"""The Comptoir Hermes, a merchant freighter (decor part 7466): everything outside its walkable
modules. Built in the game's station frame (meters, Y up, Z forward: G() turns that into
Blender's axes). The modules it dresses, outer faces of their voxels:
  bridge        x +-2.5, y -0.5..2.5, z 12.5..16.5 (glazed)
  fore corridor x +-1.5, y -0.5..2.5, z  9.5..12.5
  trading hall  x +-3.5, y -0.5..3.5, z  1.5..9.5  (door on +x at z 3.5..5.5, y -0.5..1.5)
  mid corridor  x +-1.5, y -0.5..2.5, z -1.5..1.5
  cargo hold    x +-3.5, y -0.5..2.5, z -10.5..-1.5
  engine room   x +-2.5, y -0.5..2.5, z -13.5..-10.5
Nothing here may reach inside those boxes or across the door. Groups named Solid_N are not
drawn: each is one convex collider of the big pieces (the small ones do not collide)."""
import math
import os
import random
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from common import reset, box, cylinder, export, _finish  # noqa: E402

HULL, PLATE, DARK, TRIM, GOLD = "Hull", "Plate", "Dark", "Trim", "Gold"
GLOW, ENGINE, RED, GREEN, BEACON = "Glow", "Engine", "NavRed", "NavGreen", "Beacon"
SOLAR, GLASS, HAZARD = "Solar", "Glass", "Hazard"
CARGO = ("Cargo0", "Cargo1", "Cargo2")

random.seed(131)
_solids = [0]


def G(x, y, z):
    """Game (x, y up, z forward) to Blender (x, y, z up)."""
    return (x, z, y)


def gbox(name, center, size, mat, bevel=0.0, segments=1):
    return box(name, G(*center), (size[0], size[2], size[1]), mat, bevel=bevel, segments=segments)


def gcyl(name, a, b, radius, mat, vertices=16, radius_b=None, bevel=0.0, cap=True):
    return cylinder(name, G(*a), G(*b), radius, mat, vertices=vertices, radius_b=radius_b, bevel=bevel, cap=cap)


def hull(name, points, mat, bevel=0.0):
    """The convex hull of game-frame points."""
    bm = bmesh.new()
    for p in points:
        bm.verts.new(G(*p))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    return _finish(obj, mat, bevel, 1, smooth=False)


def solid(points):
    """One convex collider (not drawn)."""
    hull("Solid%d" % _solids[0], points, "Solid_%d" % _solids[0])
    _solids[0] += 1


def solid_box(center, size):
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    solid([(cx + a * sx, cy + b * sy, cz + c * sz) for a in (-1, 1) for b in (-1, 1) for c in (-1, 1)])


def shell(name, a, b, r1, r2, mat, vertices=20, thickness=0.06):
    """An open bell (nozzle), with a wall thickness so it is seen from inside too."""
    obj = gcyl(name, a, b, r1, mat, vertices=vertices, radius_b=r2, cap=False)
    s = obj.modifiers.new("Solidify", 'SOLIDIFY')
    s.thickness = thickness
    return obj


def mirror(fn):
    for s in (-1, 1):
        fn(s)


reset()

# ---- Bow: a sharp chin under the bridge, a visor over its glass, cheek strakes

chin = [(-1.6, -0.6, 9.6), (1.6, -0.6, 9.6), (-1.6, -1.15, 9.6), (1.6, -1.15, 9.6),
        (-2.65, -0.6, 12.5), (2.65, -0.6, 12.5), (-2.4, -1.45, 13.0), (2.4, -1.45, 13.0),
        (-2.55, -0.6, 16.6), (2.55, -0.6, 16.6), (-1.2, -1.25, 17.6), (1.2, -1.25, 17.6),
        (-0.25, -0.75, 20.2), (0.25, -0.75, 20.2), (0.0, -1.0, 19.8)]
hull("Chin", chin, HULL, bevel=0.04)
solid(chin)
gbox("ChinKeel", (0, -1.2, 15.0), (0.18, 0.2, 5.6), TRIM, bevel=0.02)
visor = [(-2.65, 2.55, 12.4), (2.65, 2.55, 12.4), (-2.45, 3.0, 12.4), (2.45, 3.0, 12.4),
         (-2.55, 2.55, 17.1), (2.55, 2.55, 17.1), (-2.0, 2.72, 17.1), (2.0, 2.72, 17.1)]
hull("Visor", visor, PLATE, bevel=0.03)
solid(visor)
gbox("VisorLip", (0, 2.56, 17.12), (4.9, 0.05, 0.08), GOLD)


def cheek(s):
    hull("Cheek%d" % s, [(s * 2.55, -0.55, 12.7), (s * 2.85, -0.4, 12.9), (s * 2.55, 0.25, 12.7), (s * 2.75, 0.2, 12.9),
                          (s * 2.55, -0.55, 16.3), (s * 2.7, -0.45, 16.1), (s * 2.55, 0.1, 16.3), (s * 2.65, 0.05, 16.1)], PLATE, bevel=0.02)
    # Navigation lights: red to port (-x), green to starboard
    gbox("NavBridge%d" % s, (s * 2.68, 2.35, 16.25), (0.12, 0.12, 0.2), GREEN if s > 0 else RED, bevel=0.02)


mirror(cheek)

# ---- Spine: a fairing over the fore corridor, the hall's roof deck with its sensors

spine = [(-0.9, 2.55, 9.6), (0.9, 2.55, 9.6), (-0.6, 3.15, 9.8), (0.6, 3.15, 9.8),
         (-0.9, 2.55, 12.4), (0.9, 2.55, 12.4), (-0.4, 2.9, 12.4), (0.4, 2.9, 12.4)]
hull("Spine", spine, PLATE, bevel=0.03)
for x in (-3.56, 3.56):
    gbox("RoofEdge%.0f" % x, (x, 3.46, 5.5), (0.07, 0.1, 8.1), GOLD)
for z in (1.56, 9.44):
    gbox("RoofEdgeZ%.0f" % z, (0, 3.46, z), (7.2, 0.1, 0.07), GOLD)
# Sensor dome on a collar
gcyl("DomeCollar", (0, 3.5, 7.3), (0, 3.8, 7.3), 0.95, TRIM, vertices=24, bevel=0.02)
bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=0.8, location=G(0, 3.8, 7.3))
dome = bpy.context.active_object
dome.name = "Dome"
dome.scale = (1, 1, 0.75)
bpy.ops.object.transform_apply(scale=True)
_finish(dome, GLASS)
solid_box((0, 4.05, 7.3), (1.8, 1.0, 1.8))
# Dish on a short mast, tilted back
gcyl("DishMast", (-2.1, 3.5, 3.2), (-2.1, 4.4, 3.2), 0.09, TRIM, vertices=10)
shell("Dish", (-2.1, 4.35, 3.15), (-2.1, 4.75, 2.8), 0.12, 0.95, TRIM, vertices=24, thickness=0.04)
gcyl("DishFeed", (-2.1, 4.45, 3.05), (-2.1, 5.15, 2.5), 0.03, DARK, vertices=6)
# Antenna mast with a beacon
gcyl("Antenna", (2.3, 3.5, 8.6), (2.3, 6.2, 8.6), 0.05, TRIM, vertices=8)
for y in (4.6, 5.4):
    gbox("AntennaBar%.1f" % y, (2.3, y, 8.6), (0.9, 0.04, 0.04), TRIM)
gbox("AntennaBeacon", (2.3, 6.25, 8.6), (0.14, 0.14, 0.14), BEACON, bevel=0.03)
# Roof hatches and vents
for z in (2.6, 4.6):
    gbox("Hatch%.1f" % z, (1.2, 3.54, z), (1.1, 0.06, 1.1), PLATE, bevel=0.02)
    gbox("HatchRim%.1f" % z, (1.2, 3.51, z), (1.25, 0.03, 1.25), DARK)
for i in range(4):
    gbox("Vent%d" % i, (-1.0 + i * 0.35, 3.55, 5.9), (0.22, 0.08, 1.2), DARK, bevel=0.01)

# ---- The hall's flanks: a gold band under the windows, the trader's emblem, the dock frame

for s in (-1, 1):
    x = s * 3.56
    gbox("Band%d" % s, (x, -0.2, 5.5), (0.06, 0.16, 8.0) if s < 0 else (0.06, 0.16, 1.8), GLOW)
    if s > 0:
        gbox("BandAft", (x, -0.2, 2.35), (0.06, 0.16, 1.4), GLOW)
        gbox("BandFore", (x, -0.2, 7.6), (0.06, 0.16, 3.4), GLOW)
# Emblem (a gold disc with a lit ring) on the port side
gcyl("Emblem", (-3.55, 2.65, 5.5), (-3.68, 2.65, 5.5), 0.62, GOLD, vertices=28, bevel=0.015)
gcyl("EmblemRing", (-3.67, 2.65, 5.5), (-3.71, 2.65, 5.5), 0.45, GLOW, vertices=28)
gcyl("EmblemCore", (-3.7, 2.65, 5.5), (-3.74, 2.65, 5.5), 0.3, GOLD, vertices=20)
# Docking frame round the door (thin: a docking ship's own door frame meets it)
gbox("DockTop", (3.62, 1.65, 4.5), (0.12, 0.2, 2.5), TRIM, bevel=0.02)
gbox("DockSill", (3.62, -0.62, 4.5), (0.12, 0.16, 2.5), TRIM, bevel=0.02)
for z in (3.35, 5.65):
    gbox("DockPost%.2f" % z, (3.62, 0.5, z), (0.12, 2.5, 0.2), TRIM, bevel=0.02)
    for i in range(5):
        gbox("DockStripe%.2f_%d" % (z, i), (3.69, -0.5 + i * 0.5, z), (0.03, 0.22, 0.2), HAZARD)
    gbox("DockLight%.2f" % z, (3.7, 1.82, z), (0.1, 0.1, 0.1), GREEN, bevel=0.02)

# ---- Thruster quads at the hall's corners


def rcs(x, y, z, s):
    gbox("RcsBody%.0f%.0f" % (x, z), (x, y, z), (0.3, 0.4, 0.4), PLATE, bevel=0.03)
    for dy, dz, d in ((0.3, 0, (0, 1, 0)), (-0.3, 0, (0, -1, 0)), (0, 0.3, (0, 0, 1)), (0, -0.3, (0, 0, -1))):
        a = (x + s * 0.05, y + dy * 0.6, z + dz * 0.6)
        b = (a[0], a[1] + d[1] * 0.18, a[2] + d[2] * 0.18)
        gcyl("Rcs%.0f%.0f%.1f%.1f" % (x, z, dy, dz), a, b, 0.05, DARK, vertices=8, radius_b=0.08, cap=False)


for s in (-1, 1):
    for z in (1.9, 9.1):
        rcs(s * 3.72, 3.1, z, s)

# ---- Cargo hold: containers on the roof and in side racks, rails, a crane


def container(name, center, size, look, yaw=0.0):
    gbox(name, center, size, look, bevel=0.03)
    cx, cy, cz = center
    sx, sy, sz = size
    # Door end frames and corrugation ribs (one mesh each look)
    for e in (-1, 1):
        gbox(name + "End%d" % e, (cx, cy, cz + e * (sz / 2 - 0.04)), (sx + 0.04, sy + 0.04, 0.08), TRIM)
    for i in range(1, 4):
        gbox(name + "Rib%d" % i, (cx, cy, cz - sz / 2 + i * sz / 4), (sx + 0.03, sy - 0.12, 0.05), look)


for x in (-1.75, 1.75):
    gbox("RoofRail%.0f" % x, (x, 2.58, -6.0), (1.3, 0.08, 8.8), DARK)
    for i, z in enumerate((-9.0, -6.0, -3.0)):
        high = 2 if random.random() < 0.55 else 1
        for level in range(high):
            container("Roof%.0f_%d_%d" % (x, i, level), (x, 3.25 + level * 1.22, z), (1.3, 1.2, 2.7), CARGO[random.randrange(3)])
    solid_box((x, 3.25 + 0.61, -6.0), (1.35, 2.44, 8.4))


def rack(s):
    x = s * 4.4
    # A cage of beams from the hold's wall, three containers in it
    for z in (-10.3, -6.0, -1.7):
        gbox("RackRib%d%.0f" % (s, z), (s * 4.45, 0.95, z), (1.7, 0.12, 0.14), TRIM)
        gbox("RackPost%d%.0f" % (s, z), (s * 5.25, 0.95, z), (0.14, 2.9, 0.14), TRIM)
    gbox("RackTop%d" % s, (s * 4.45, 2.4, -6.0), (1.7, 0.1, 8.8), TRIM)
    gbox("RackFloor%d" % s, (s * 4.45, -0.45, -6.0), (1.7, 0.1, 8.8), DARK)
    for i, z in enumerate((-8.9, -6.0, -3.1)):
        container("Side%d_%d" % (s, i), (x, 0.85, z), (1.3, 2.4, 2.6), CARGO[(i + (s > 0)) % 3])
    solid_box((s * 4.45, 0.95, -6.0), (1.75, 2.95, 8.9))


mirror(rack)
# Cargo crane folded on the stern of the roof
gcyl("CraneBase", (0, 2.55, -10.0), (0, 3.0, -10.0), 0.45, PLATE, vertices=16, bevel=0.02)
gbox("CraneArm", (0, 3.25, -7.6), (0.3, 0.3, 5.0), HAZARD, bevel=0.03)
gbox("CraneHook", (0, 2.95, -5.2), (0.2, 0.4, 0.2), DARK)

# ---- Solar wings on a mast over the hold

gcyl("WingMast", (0, 2.55, -6.0), (0, 5.6, -6.0), 0.12, TRIM, vertices=10)
gbox("WingSpar", (0, 5.6, -6.0), (15.4, 0.12, 0.12), TRIM)


def wing(s):
    for i, (x0, x1) in enumerate(((2.1, 4.8), (5.0, 7.7))):
        gbox("Wing%d_%d" % (s, i), (s * (x0 + x1) / 2, 5.6, -6.0), (x1 - x0, 0.05, 2.4), SOLAR)
        gbox("WingFrame%d_%d" % (s, i), (s * (x0 + x1) / 2, 5.6, -6.0), (x1 - x0 + 0.06, 0.03, 2.48), TRIM)
    gbox("WingTip%d" % s, (s * 7.75, 5.6, -6.0), (0.1, 0.1, 0.1), GREEN if s > 0 else RED)


mirror(wing)

# ---- Engines: a mount on the stern, three main bells, two side nacelles on pylons

gbox("EngineMount", (0, 1.0, -13.85), (5.3, 3.3, 0.6), PLATE, bevel=0.06, segments=2)
for x in (-1.7, 0.0, 1.7):
    shell("Bell%.1f" % x, (x, 1.0, -14.1), (x, 1.0, -16.3), 0.5, 0.82, DARK, vertices=24)
    gcyl("BellRing%.1f" % x, (x, 1.0, -14.15), (x, 1.0, -14.4), 0.62, TRIM, vertices=24, bevel=0.02)
    gcyl("BellGlow%.1f" % x, (x, 1.0, -14.2), (x, 1.0, -14.3), 0.46, ENGINE, vertices=20)
solid_box((0, 1.0, -15.1), (5.3, 1.7, 2.4))


def nacelle(s):
    x = s * 6.1
    gcyl("Nacelle%d" % s, (x, 1.0, -13.4), (x, 1.0, -7.6), 0.75, HULL, vertices=20, bevel=0.03)
    gcyl("NacelleNose%d" % s, (x, 1.0, -7.6), (x, 1.0, -6.6), 0.75, HULL, vertices=20, radius_b=0.2)
    gcyl("NacelleBand%d" % s, (x, 1.0, -9.0), (x, 1.0, -9.3), 0.78, GOLD, vertices=20)
    shell("NacelleBell%d" % s, (x, 1.0, -13.4), (x, 1.0, -14.6), 0.5, 0.66, DARK, vertices=20)
    gcyl("NacelleGlow%d" % s, (x, 1.0, -13.45), (x, 1.0, -13.55), 0.45, ENGINE, vertices=16)
    # Pylon over the cargo rack
    hull("Pylon%d" % s, [(s * 5.2, 2.35, -12.0), (s * 5.2, 2.6, -12.0), (s * 5.2, 2.35, -9.0), (s * 5.2, 2.6, -9.0),
                         (x, 1.55, -12.3), (x, 1.85, -12.3), (x, 1.55, -8.6), (x, 1.85, -8.6)], PLATE, bevel=0.02)
    gbox("NacelleLight%d" % s, (x, 1.8, -7.9), (0.1, 0.1, 0.1), GREEN if s > 0 else RED)
    solid_box((x, 1.0, -10.1), (1.55, 1.55, 7.2))


mirror(nacelle)
# Radiators on the engine room, ribbed
for s in (-1, 1):
    hull("Radiator%d" % s, [(s * 0.6, 2.55, -13.3), (s * 0.6, 2.55, -10.7), (s * 0.7, 2.62, -13.3), (s * 0.7, 2.62, -10.7),
                            (s * 3.0, 4.0, -13.3), (s * 3.0, 4.0, -10.7), (s * 3.08, 4.06, -13.3), (s * 3.08, 4.06, -10.7)], TRIM)
    for i in range(5):
        z = -13.0 + i * 0.55
        gbox("RadRib%d%d" % (s, i), (s * 1.85, 3.36, z), (0.06, 0.06, 0.06), DARK)
gbox("SternBeacon", (0, 2.75, -13.2), (0.16, 0.16, 0.16), BEACON, bevel=0.03)

# ---- Belly: a keel plate under the hall and the hold, landing pads

belly = [(-2.9, -0.6, -13.0), (2.9, -0.6, -13.0), (-2.2, -0.95, -12.6), (2.2, -0.95, -12.6),
         (-3.3, -0.6, 9.4), (3.3, -0.6, 9.4), (-2.6, -0.95, 9.0), (2.6, -0.95, 9.0)]
hull("Belly", belly, PLATE, bevel=0.03)
solid(belly)
for x in (-2.2, 2.2):
    for z in (-11.0, 7.5):
        gcyl("Pad%.0f%.0f" % (x, z), (x, -0.95, z), (x, -1.35, z), 0.35, DARK, vertices=12, radius_b=0.45)
for z in (-8.0, -4.0, 0.0, 4.0):
    gbox("BellyLight%.0f" % z, (0, -0.97, z), (0.6, 0.04, 0.12), GLOW)

export("merchant_hermes.obj")
