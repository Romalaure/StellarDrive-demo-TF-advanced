"""A forest floor (Kenney Nature Kit, CC0): two fallen trunks, old stumps, rings of red and tan
mushrooms, ferns and bushes, mossy rocks. About 16 m across, to scatter in the woods."""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, export  # noqa: E402
from kenney import place, ring  # noqa: E402

reset()
place("log_large", (0, 0, 0), 5.5, 25)
place("log_large", (4.6, 3.0, 0), 4.5, 110)
place("stump_oldTall", (-3.8, 2.4, 0), 3.4)
place("stump_old", (3.0, -4.0, 0), 3.4, 60)
place("stump_roundDetailed", (-5.5, -3.5, 0), 3.0)
# A fairy ring of mushrooms, groups along the trunk
for i, (x, y) in enumerate(ring(9, 2.6)):
    place("mushroom_red" if i % 3 else "mushroom_redTall", (x - 1.0, y - 5.6, 0), 3.2, i * 40, solid=False)
place("mushroom_tanGroup", (1.8, 1.0, 0), 3.6, solid=False)
place("mushroom_redGroup", (-1.6, -1.2, 0), 3.2, 90, solid=False)
place("mushroom_tanTall", (5.8, 0.6, 0), 3.6, solid=False)
# Rocks with grass on top, ferns and bushes
place("rock_largeC", (-6.4, 1.0, 0), 3.0, 40)
place("rock_smallTopA", (6.6, -2.8, 0), 3.4, 10)
place("rock_smallB", (-1.0, 5.2, 0), 3.4)
for (x, y), model in zip(ring(7, 6.8, 15), ["plant_bushLarge", "plant_bushDetailed", "grass_leafsLarge", "plant_bush", "grass_large", "plant_bushLarge", "grass_leafsLarge"]):
    place(model, (x, y, 0), 3.4, x * 20, solid=False)
place("hanging_moss", (0.4, 0.2, 0.9), 2.6, 25, solid=False)
export("nature_clearing.obj")
