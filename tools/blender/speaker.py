"""The radio speaker (part 7162): a rounded cabinet on a dark plinth, a woofer and a tweeter behind
lit rings, a small status light. Footprint 0.34 x 0.28 m, 0.52 m high; front toward -Y here
(-Z in the game)."""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box, cylinder, rounded_panel, export  # noqa: E402

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

export("speaker.obj")
