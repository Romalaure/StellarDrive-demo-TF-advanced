"""Building scenes from Kenney's Nature Kit (CC0, www.kenney.nl), for the model scripts.

The pack's GLB files are read from tools/blender/assets/kenney_nature (see assets/README.md;
the pack itself is not in the repository). place() imports one model, scaled, turned and
recolored: each of the pack's materials becomes one of the scene's looks (PALETTE keys), and
solid pieces are copied again into the "TFA_Solid" group, the collider the mod makes (plants,
moss and flowers stay walk-through).
"""
import math
import os

import bpy

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "kenney_nature", "Models", "GLTF format")

# The pack's materials -> the look they take in the game (a material name the mod colors)
LOOKS = {
    "woodBark": "Bark", "woodDark": "Bark", "wood": "Wood", "woodInner": "WoodInner",
    "grass": "Leaves", "leafsGreen": "Leaves", "leafsDark": "Leaves", "leafsFall": "Autumn",
    "stone": "Stone", "stoneDark": "Stone", "dirt": "Dirt", "dirtDark": "Dirt",
    "colorRed": "Canvas", "colorRedDark": "Canvas", "_defaultMat": "Pale", "colorWhite": "Pale",
    "metal": "Metal", "colorBlack": "Dark", "colorYellow": "Accent", "colorBlue": "Water",
}
SOLID = "TFA_Solid"


def place(model, location, scale=1.0, turn=0.0, solid=True, look=None):
    """Imports the pack's model at location (meters), scaled, turned about Z (degrees)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ASSETS, model + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == 'MESH']
    for o in new:
        if o.parent is None:
            o.location = location
            o.rotation_mode = 'XYZ'
            o.rotation_euler = (0, 0, math.radians(turn))
            o.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    for o in meshes:
        # Bake the hierarchy's transform into the mesh, then drop the empties
        o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world.identity()
        for slot in o.material_slots:
            name = slot.material.name.split(".")[0] if slot.material else "_defaultMat"
            key = look or LOOKS.get(name, "Pale")
            m = bpy.data.materials.get(key) or bpy.data.materials.new(key)
            slot.material = m
        for p in o.data.polygons:
            p.use_smooth = False
        if solid:
            copy = o.copy()
            copy.data = o.data.copy()
            bpy.context.collection.objects.link(copy)
            copy.data.materials.clear()
            copy.data.materials.append(bpy.data.materials.get(SOLID) or bpy.data.materials.new(SOLID))
            for p in copy.data.polygons:
                p.material_index = 0
    for o in new:
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    return meshes


def ring(count, radius, start=0.0):
    """Points around a circle (x, y), for scattering."""
    return [(math.cos(math.radians(start + i * 360.0 / count)) * radius, math.sin(math.radians(start + i * 360.0 / count)) * radius)
            for i in range(count)]
