"""Shared helpers for the mod's Blender model scripts (run headless by build.ps1).

Axes: build in Blender's own axes, Z up, the model's front toward -Y. The mod's loader turns
Blender (x, y, z) into Unity (x, z, y): Blender -Y is the model's -Z (its front) in the game.
Each material's name is the key the mod gives its look (color, metal, glow): keep one material
per look and as few faces as reads well (the game draws many parts).
"""
import math
import sys

import bpy
import bmesh


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    return m


def _finish(obj, mat, bevel=0.0, segments=2, smooth=True):
    if mat:
        obj.data.materials.clear()
        obj.data.materials.append(material(mat))
    if bevel > 0:
        b = obj.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = segments
        b.limit_method = 'ANGLE'
        b.angle_limit = math.radians(30)
        b.harden_normals = True
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(35)
    return obj


def box(name, center, size, mat, bevel=0.0, segments=2, rotation=(0, 0, 0)):
    """A box of the given size (x, y, z) around center, rotated in degrees, edges bevelled."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=tuple(math.radians(a) for a in rotation))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, mat, bevel, segments)


def cylinder(name, a, b, radius, mat, vertices=24, radius_b=None, bevel=0.0, segments=2, cap=True):
    """A cylinder (or cone, with radius_b) from point a to point b."""
    ax, ay, az = a
    bx, by, bz = b
    dx, dy, dz = bx - ax, by - ay, bz - az
    length = math.sqrt(dx * dx + dy * dy + dz * dz)
    rb = radius if radius_b is None else radius_b
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=rb, depth=length,
                                    end_fill_type='NGON' if cap else 'NOTHING',
                                    location=((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2))
    obj = bpy.context.active_object
    obj.name = name
    # Point the cone's Z along a -> b
    direction = (dx / length, dy / length, dz / length)
    from mathutils import Vector
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector(direction))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return _finish(obj, mat, bevel, segments)


def rounded_panel(name, center, width, height, depth, radius, mat, rotation=(0, 0, 0), corners=6):
    """A flat panel with rounded corners in its X-Z plane, `depth` thick along Y."""
    bm = bmesh.new()
    w, h = width / 2 - radius, height / 2 - radius
    points = []
    for cx, cz, start in ((w, h, 0), (-w, h, 90), (-w, -h, 180), (w, -h, 270)):
        for i in range(corners + 1):
            a = math.radians(start + 90 * i / corners)
            points.append((cx + radius * math.cos(a), cz + radius * math.sin(a)))
    front = [bm.verts.new((x, -depth / 2, z)) for x, z in points]
    back = [bm.verts.new((x, depth / 2, z)) for x, z in points]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = center
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _finish(obj, mat)


def export(default_name):
    """Writes the scene as OBJ (triangles, normals, one group per material) to the path given after --."""
    out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else default_name
    bpy.ops.export_scene.obj(filepath=out, use_selection=False, use_mesh_modifiers=True, use_materials=True,
                             use_triangles=True, use_normals=True, use_uvs=False, use_smooth_groups=False,
                             group_by_material=True, axis_forward='-Z', axis_up='Y')
    tris = 0
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            depsgraph = bpy.context.evaluated_depsgraph_get()
            mesh = obj.evaluated_get(depsgraph).to_mesh()
            tris += sum(len(p.vertices) - 2 for p in mesh.polygons)
    print("exported", out, tris, "triangles")
