"""A heap of boulders (Kenney Nature Kit, CC0): tall standing stones and big rocks, grass on
their tops, small stones and bushes round their feet. About 18 m across, 6 m high."""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import reset, export  # noqa: E402
from kenney import place, ring  # noqa: E402

reset()
place("stone_tallA", (0, 0, 0), 6.0)
place("stone_tallE", (3.6, 1.6, 0), 5.2, 70)
place("stone_tallH", (-3.4, 2.0, 0), 4.8, 200)
place("rock_largeA", (1.8, -3.8, 0), 5.0, 30)
place("rock_largeD", (-4.6, -2.6, 0), 4.6, 120)
place("stone_largeB", (6.4, -1.8, 0), 4.0, 15)
place("rock_tallC", (-1.2, 5.4, 0), 4.6, 300)
for i, (x, y) in enumerate(ring(10, 8.0, 10)):
    place(["stone_smallA", "stone_smallC", "rock_smallF", "stone_smallFlatB", "rock_smallTopB"][i % 5], (x, y, 0), 3.4, i * 37)
for (x, y) in ring(5, 6.0, 40):
    place("plant_bushSmall", (x, y, 0), 3.6, solid=False)
export("nature_boulders.obj")
