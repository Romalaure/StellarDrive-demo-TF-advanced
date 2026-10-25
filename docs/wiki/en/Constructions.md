# Building parts

**TF Construction** tab. The game already has hull floors, walls and slopes in wood, glass and iron; these parts add the missing shapes. They are placed parts: you put them on any hull surface (floor, wall, ceiling), and they are recycled like the others.

**Assembly**: aim at a part already placed.
- On its **top face** (or bottom face), the new part sits on it (or hangs under it).
- On a **side**, it goes against it while staying upright, its base level with the aimed part's base: planks side by side, walls in a row, a glass pane against a post.

Positions follow a 25 cm grid counted from the aimed part.

| Wood | Glass | Iron |
|---|---|---|
| Post 2 m | Glass pane 1 × 1 | Grating 1 × 1 |
| Slatted floor 1 × 1 | Picture window 2 × 2 | Railing 1 m and 2 m |
| Plank wall 1 × 2 | Glass floor tile 1 × 1 (walkable) | Steel truss 2 m |
| Railing 1 m | Glass railing 1 m | Riveted column 2.5 m |
| Lattice 1 × 1 | Skylight 1 × 1 | Corner gusset 1 × 1 |
| Shelf 1 m | Glass partition 1 × 2 | Perforated sheet 1 × 1 |

The old **structural parts** (beams, plates 1 × 1 and 2 × 1, frame, junction) left the menu in 0.44; the ones already placed stay.

Placed parts touch (planks and partitions in a continuous run). **Orientation**: while placing, the **B** key (setting `BuildTiltKey`) turns the part from upright to tipped forward, on its side and upside down: a partition standing against a hull wall, a post lying on the floor, a 2 m long, 1 m high partition. Reworked models: real wood grain, satin steel, the hull's glass. On the hull, parts line up with its cells: a 1 m part is centered on a cell, a 2 m part on a cell's edge, and thin parts (railings, partitions, windows) stand on the cells' edges like the game's walls: two railings at a right angle meet at the corner. The collision is the part's box (the gaps of a lattice are only visual).

## Furniture

The tab's fourth row, research **Carpentry**; placed on floors (or beside a placed part):

| Furniture | Size | Cost |
|---|---|---|
| Sofa (3 seats) | 2 × 0.9 m | 6 wood, 2 iron |
| Armchair | 0.9 × 0.9 m | 3 wood, 1 iron |
| Chair | 0.45 × 0.5 m | 2 wood |
| Stool | 0.4 m | 1 wood, 1 iron |
| Table | 1.6 × 0.9 m | 4 wood, 2 iron |
| Coffee table (glass top) | 1.1 × 0.6 m | 2 glass, 2 iron, 1 wood |
| Desk (drawer unit) | 1.4 × 0.7 m | 5 wood, 2 iron |
| Rug | 2 × 3 m | 2 wood |
| Bookcase | 1 × 2 m high | 6 wood |
| Potted plant | 1.2 m high | 2 wood, 1 glass |

The sofa's and armchair's fabric and the rug take the paint gun's color. **You can sit** on the sofa (3 seats), the armchair, the chair, the stool, the table and the coffee table: look at one and press **T** (the nearest free seat); **T** or a move key to get up. The other players see you seated. The desk, rug, bookcase and plant are decorative.

**More furniture** (the row under the furniture, Carpentry): bed, bunk bed, nightstand with a lamp, wardrobe, dresser, kitchen counter (sink, tap), fridge, TV unit, bench (3 seats), office chair (you can sit on it), round table, planter box. Wood, iron, glass and copper only.

**Futuristic furniture** (the next row, research *Station modules*), to fit out a ship's rooms:
- **science**: lab bench, specimen tank (glowing liquid), analysis console;
- **storage**: server rack, cargo crate, storage rack, canisters;
- **crew quarters**: sleep pod, crew lockers;
- **galley**: food synthesizer, galley island;
- **offices**: command desk (3 screens), holo table (a turning planet);
- **lounge / mess**: lounge sofa (3 seats), mess table with benches (4 seats), vending machine;
- **sickbay**: medical bed.

**Sleeping**: the bed, the bunk bed, the sleep pod and the medical bed: look at one and press **T** to sleep through the night (like the game's waiting). Not during flight.

## Stairs

Top row of the tab. They stand on floors and **climb toward the back** (the foot on the side of the player placing them); turn them before placing. Their collision is a ramp under the steps: you walk up them, like the hull's slopes.

| Stairs | Size | Research |
|---|---|---|
| Wood, gentle wood | 1 m high over 1 m / over 2 m | Carpentry |
| Industrial (grating), gentle industrial | 1 m over 1 m / over 2 m | Metal construction |
| Glass (steps on a chrome spine), gentle glass | 1 m over 1 m / over 2 m | Architectural glass |
| Futuristic (dark blocks, lit nosings), gentle futuristic | 1 m over 1 m / over 2 m | Station modules |
| Steel spiral, futuristic spiral | 2 m high, ¾ of a turn, 2 × 2 m footprint | Metal construction / Station modules |

**Chaining**: aim at a straight flight already placed with another straight flight, it goes on from the end of the first one, the same way (steep and gentle mix); to climb higher. The spiral needs a hole in the ceiling above it.

## Signs and doors

Last row of the tab.
- **Lit sign panel** (wall), **door plate** (small, beside a door), **holographic sign** (on the floor, text floating at eye height): look at one and press **T** to write its text (40 characters) and pick its color (7 colors). To name rooms: Bridge, Sickbay, Cargo… Every player sees it, the host keeps it with the world. Research *Ship computer*.
- **Automatic door** (one square) and **large automatic door** (2 × 2): it opens by itself when someone comes near and closes behind them. Set **diagonally** (remove the diagonal wall, set the door on the floor in its place, turn it for the other diagonal); or **straight** (in line with a normal wall): choose while placing it with the **B** key (the preview shows it). It **does not hold air**. Research *Metal construction*.
- **The game's door on diagonals**: place the game's door like a wall and turn it (rotate key): on a diagonal wall it takes its diagonal version, like the game's walls. It is the real door: it opens with the interact key and seals its room.

## Reinforced hull

**TF Blindage** tab, research *Reinforced plating*. The game's **iron** and **glass** hull pieces (floor, plate, pivoted plate, floor and wall corners, slope, slope corners), reinforced: same model, placed the same way, but a slightly darker shade when placed (paint recolors them like any hull). It is real hull: it seals airtight rooms like the game's.

- **3 times tougher** in collisions;
- with the **Arsenal** mod: weapons do **a third of the damage** and punch **no holes**;
- **1.5 times heavier**;
- cost: twice the iron (or glass), plus 1 titanium (reinforced iron) or 1 iron (reinforced glass).

## Build menu

Over the build menu, a bar:
- **Search a part**: type a few letters, every unlocked part whose name holds them shows (accents and case ignored); empty the field to go back to the tab.
- **Mod**: pick a mod (the base game, TF Advanced, TFA Water, Submarine Add-ons…) to show only its parts, page by page (◀ ▶); the search narrows them further. "Every mod" goes back to the tabs.
- **− / +**: makes the menu smaller or bigger (kept between sessions, never bigger than the screen).
