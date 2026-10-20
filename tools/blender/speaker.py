"""The radio speaker (part 7162): a rounded cabinet on a dark plinth, a woofer and a tweeter behind
lit rings, a small status light. Footprint 0.34 x 0.28 m, 0.52 m high; front toward -Y here
(-Z in the game)."""
import math
import os
import sys

import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box, cylinder, rounded_panel, export, _finish  # noqa: E402

CASE, TRIM, METAL, CONE, GLOW = "TF_Speaker_Case", "TF_NoPaint_SpeakerTrim", "TF_NoPaint_SpeakerMetal", "TF_NoPaint_SpeakerCone", "TF_NoPaint_SpeakerGlow"

reset()

# Plinth and cabinet
box("Plinth", (0, 0, 0.02), (0.3, 0.24, 0.04), TRIM, bevel=0.012, segments=3)
box("Cabinet", (0, 0, 0.29), (0.34, 0.28, 0.5), CASE, bevel=0.045, segments=4)
# Dark front baffle, slightly proud
rounded_panel("Baffle", (0, -0.141, 0.29), 0.29, 0.45, 0.012, 0.035, TRIM)

# Woofer: metal surround, dark cone, dust cap, lit ring
wy = -0.148
cylinder("WooferRim", (0, wy, 0.21), (0, wy - 0.012, 0.21), 0.112, METAL, vertices=40, bevel=0.003)
cylinder("WooferCone", (0, wy - 0.006, 0.21), (0, wy + 0.03, 0.21), 0.098, CONE, radius_b=0.03, vertices=40, cap=False)
cylinder("WooferCap", (0, wy - 0.004, 0.21), (0, wy + 0.01, 0.21), 0.032, CONE, radius_b=0.02, vertices=24)
cylinder("WooferGlow", (0, wy - 0.013, 0.21), (0, wy - 0.015, 0.21), 0.118, GLOW, vertices=40, cap=False)

# Tweeter above
cylinder("TweeterRim", (0, wy, 0.42), (0, wy - 0.01, 0.42), 0.045, METAL, vertices=28, bevel=0.002)
cylinder("TweeterDome", (0, wy - 0.006, 0.42), (0, wy - 0.022, 0.42), 0.026, CONE, radius_b=0.006, vertices=24)

# Status light and a bass port slot
cylinder("Light", (0.1, wy, 0.495), (0.1, wy - 0.006, 0.495), 0.007, GLOW, vertices=12)
box("Port", (0, wy - 0.002, 0.075), (0.16, 0.006, 0.022), METAL, bevel=0.004)

# Side trims: thin metal bands
for side in (-1, 1):
    box("Band%d" % side, (side * 0.171, 0, 0.29), (0.004, 0.2, 0.36), METAL, bevel=0.0015)

# ---- Details: the woofer's rubber surround, screws, the tweeter's waveguide, a volume knob,
# a badge, the port's dark slot, a metal inlay on top, the binding posts at the back
bpy.ops.mesh.primitive_torus_add(major_radius=0.103, minor_radius=0.007, major_segments=36, minor_segments=6,
                                 location=(0, wy - 0.011, 0.21), rotation=(math.radians(90), 0, 0))
bpy.context.active_object.name = "WooferSurround"
_finish(bpy.context.active_object, CONE)
for k in range(8):
    a = math.radians(22.5 + 45 * k)
    x, z = 0.108 * math.cos(a), 0.21 + 0.108 * math.sin(a)
    cylinder("WooferScrew%d" % k, (x, wy - 0.011, z), (x, wy - 0.016, z), 0.0045, METAL, vertices=8)
cylinder("Waveguide", (0, wy + 0.002, 0.42), (0, wy - 0.006, 0.42), 0.062, METAL, radius_b=0.046, vertices=32, cap=False)
for k in range(4):
    a = math.radians(45 + 90 * k)
    x, z = 0.054 * math.cos(a), 0.42 + 0.054 * math.sin(a)
    cylinder("TweeterScrew%d" % k, (x, wy - 0.001, z), (x, wy - 0.005, z), 0.0035, METAL, vertices=8)
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * 0.125, 0.29 + sz * 0.205
        cylinder("BaffleScrew%d%d" % (sx, sz), (x, wy, z), (x, wy - 0.004, z), 0.0055, METAL, vertices=8)
cylinder("Knob", (0.11, wy, 0.45), (0.11, wy - 0.018, 0.45), 0.014, METAL, vertices=20, bevel=0.002)
cylinder("KnobFace", (0.11, wy - 0.018, 0.45), (0.11, wy - 0.019, 0.45), 0.011, TRIM, vertices=20)
box("KnobMark", (0.11, wy - 0.0195, 0.457), (0.002, 0.001, 0.007), GLOW)
for k in range(9):
    a = math.radians(225 - 270 * k / 8)
    x, z = 0.11 + 0.021 * math.cos(a), 0.45 + 0.021 * math.sin(a)
    cylinder("KnobTick%d" % k, (x, wy, z), (x, wy - 0.001, z), 0.0013, METAL, vertices=6)
rounded_panel("Badge", (-0.09, wy - 0.002, 0.495), 0.06, 0.014, 0.003, 0.005, METAL)
box("PortSlot", (0, wy - 0.0056, 0.075), (0.146, 0.001, 0.012), CONE)
box("TopInlay", (0, 0, 0.5405), (0.22, 0.16, 0.002), METAL, bevel=0.0008)
rounded_panel("Terminal", (0, 0.143, 0.14), 0.1, 0.08, 0.006, 0.01, TRIM)
for x in (-0.025, 0.025):
    cylinder("Post%.3f" % x, (x, 0.146, 0.14), (x, 0.166, 0.14), 0.007, METAL, vertices=12)
    cylinder("PostCap%.3f" % x, (x, 0.158, 0.14), (x, 0.168, 0.14), 0.01, TRIM, vertices=12)

export("speaker.obj")
