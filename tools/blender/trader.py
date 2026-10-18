"""The merchant terminal (part 7465): a bevelled counter with a gold rim, glowing front strips, a
slanted screen in a rounded bezel, a scale and goods on top, a lit sign on a post behind.
Footprint 1.6 x 0.9 m, 2.3 m high; the customer stands at the front (-Y here, -Z in the game)."""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, box, cylinder, rounded_panel, export  # noqa: E402

BODY, TRIM, SCREEN, SIGN = "TF_NoPaint_TraderBody", "TF_NoPaint_TraderTrim", "TF_NoPaint_TraderScreen", "TF_NoPaint_TraderSign"

reset()

# The counter: a body on a recessed plinth, a thick top with a gold rim, a back shelf
box("Plinth", (0, 0.03, 0.06), (1.42, 0.62, 0.12), TRIM, bevel=0.02)
box("Body", (0, 0, 0.53), (1.5, 0.7, 0.82), BODY, bevel=0.035, segments=3)
box("Top", (0, -0.02, 0.97), (1.6, 0.8, 0.07), BODY, bevel=0.025, segments=3)
box("Rim", (0, -0.02, 0.935), (1.62, 0.82, 0.025), TRIM, bevel=0.01)
# Front: three recessed panels framed by glowing strips
for i, x in enumerate((-0.5, 0.0, 0.5)):
    box("Panel%d" % i, (x, -0.352, 0.53), (0.42, 0.02, 0.62), BODY, bevel=0.012)
for x in (-0.75, -0.25, 0.25, 0.75):
    box("Strip%.2f" % x, (x, -0.358, 0.53), (0.025, 0.012, 0.66), SCREEN)
box("Kick", (0, -0.36, 0.17), (1.46, 0.02, 0.06), TRIM, bevel=0.008)

# The screen: a rounded bezel tilted toward the customer, on a short neck
cylinder("Neck", (0.38, 0.16, 1.0), (0.38, 0.18, 1.14), 0.05, BODY, vertices=16)
rounded_panel("Bezel", (0.38, 0.14, 1.32), 0.62, 0.42, 0.05, 0.05, BODY, rotation=(-28, 0, 0))
rounded_panel("Screen", (0.38, 0.11, 1.33), 0.54, 0.34, 0.012, 0.035, SCREEN, rotation=(-28, 0, 0))

# The scale: a round foot, a post, a beam and two pans
cylinder("ScaleFoot", (-0.42, 0.0, 1.0), (-0.42, 0.0, 1.03), 0.09, TRIM, vertices=24, bevel=0.005)
cylinder("ScalePost", (-0.42, 0.0, 1.03), (-0.42, 0.0, 1.38), 0.014, TRIM, vertices=12)
box("ScaleBeam", (-0.42, 0.0, 1.38), (0.46, 0.022, 0.022), TRIM, bevel=0.006)
for side in (-1, 1):
    x = -0.42 + side * 0.21
    cylinder("Hanger%d" % side, (x, 0.0, 1.38), (x, 0.0, 1.24), 0.004, TRIM, vertices=6, cap=False)
    cylinder("Pan%d" % side, (x, 0.0, 1.22), (x, 0.0, 1.245), 0.085, TRIM, radius_b=0.1, vertices=24)

# Goods: a crate, a stack of gold ingots, a glowing crystal
box("Crate", (-0.05, 0.12, 1.09), (0.18, 0.18, 0.18), BODY, bevel=0.012, rotation=(0, 0, 18))
for i, (x, y, z) in enumerate(((0.05, -0.18, 1.025), (0.13, -0.18, 1.025), (0.09, -0.18, 1.065))):
    box("Ingot%d" % i, (x, y, z), (0.075, 0.13, 0.04), TRIM, bevel=0.01)
cylinder("Crystal", (-0.2, -0.2, 1.0), (-0.2, -0.2, 1.16), 0.035, SCREEN, radius_b=0.0, vertices=6)

# The sign: a post behind the counter and a rounded plate with a lit face toward the front
cylinder("SignPost", (0, 0.33, 1.0), (0, 0.33, 2.02), 0.032, BODY, vertices=16)
rounded_panel("SignPlate", (0, 0.33, 2.15), 0.95, 0.3, 0.06, 0.06, TRIM)
rounded_panel("SignFace", (0, 0.295, 2.15), 0.85, 0.2, 0.012, 0.04, SIGN)

export("trader.obj")
