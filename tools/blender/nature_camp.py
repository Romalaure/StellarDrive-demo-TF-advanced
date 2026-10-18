"""An abandoned camp (Kenney Nature Kit, CC0): two tents, a ring of stones round a dead fire,
logs as benches, a stack of firewood, a pot, stumps, mushrooms and a canoe pulled up. About
14 m across. The chest the mod adds stands inside the open tent (front -Y)."""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, export  # noqa: E402
from kenney import place  # noqa: E402

reset()
# The fire in the middle, benches round it
place("campfire_stones", (0, 0, 0), 3.2)
place("campfire_logs", (0, 0, 0.05), 2.4, 20, solid=False)
place("log_large", (0, -2.6, 0), 3.2, 90)
place("log_large", (2.4, 0.6, 0), 3.2, 15)
place("stump_round", (-2.3, -0.8, 0), 2.6)
# The tents: one open (the chest inside), one closed, a little apart
place("tent_detailedOpen", (-1.2, 4.2, 0), 4.2, 180)
place("tent_smallClosed", (3.6, 4.6, 0), 3.8, 200)
# Firewood, a pot, a canoe on its side of the camp
place("log_stackLarge", (-4.3, 1.4, 0), 3.4, 70)
place("pot_large", (1.4, -1.6, 0), 2.2)
place("canoe", (5.2, -2.4, 0), 4.0, 120)
place("canoe_paddle", (4.0, -3.4, 0.05), 3.0, 40, solid=False)
# Around: stumps, a fallen log, mushrooms, bushes
place("stump_oldTall", (-5.0, -3.2, 0), 2.8)
place("log", (-3.0, -5.2, 0), 3.4, 30)
place("mushroom_redGroup", (-4.4, -2.4, 0), 3.0, solid=False)
place("mushroom_tanGroup", (2.8, 2.7, 0), 3.0, solid=False)
place("plant_bushLarge", (-5.6, 3.6, 0), 3.4, solid=False)
place("plant_bushDetailed", (6.2, 1.2, 0), 3.2, solid=False)
place("grass_large", (0.6, 6.6, 0), 3.0, solid=False)
export("nature_camp.obj")
