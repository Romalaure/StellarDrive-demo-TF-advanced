# Machines

**TF Avancé** build tab. Machines are unlocked at the [research table](Recherche.md).

## Power

| Machine | Effect | Sockets |
|---|---|---|
| Battery | stores energy (1,200 kJ), charges from the generators' surplus | 2 power, 1 signal (charge %) |
| Circuit breaker | links two grids, cuts on overload (4,000 W) | input, output, state, command |
| Quantum reactor | 12,000 W with no fuel | 2 power, 1 signal |
| Cryonite battery | 8 batteries, charge and discharge ×3 | like the battery |
| Cryonite condenser | 0.05 oxygen/s for 1,500 W | 2 power, 1 fluid, 1 signal |
| Quantum gyroscope | the torque of 5 gyroscopes | like the gyroscope |
| Solar panel mount | up to 3,000 W facing the star, nothing at night or in a planet's shadow | 2 power, 1 signal (W) |

## Ship mining laser

The player's mining laser, scaled up, on a turret that bolts to the hull (floor, wall, ceiling, the ship's underside). Its beam goes straight out of the surface it is placed on, up to **40 m**, and mines the rocks, crystals, asteroid ores and **trees** (for wood) it hits, **3 times faster** than the tool. What it mines does not go to your inventory: it goes into the **chest linked by an item line**.

1. Place the laser, the turret facing what you want to mine.
2. Plug power into one of its power sockets (**800 W** while it fires).
3. Link its **item socket** (the yellow hatch, in the middle of the row of sockets) with an **item line** to a chest that has one: the *Item Lines* mod's **item terminal** or supply crate, or a storage machine with an item port. With no chest linked it does not fire.
4. **Command** socket: with no cable it fires as soon as it has power; with a cable (lever, button, seat) it fires while the signal is above 0.

**Status** socket: 2 mining, 1 firing with nothing to mine, 0 off, -1 no power, -2 no chest, -3 chest full (it waits until emptied). Mining runs on the host, near a player (rocks only exist where someone is). Research *Ship mining*.

## Force field

Wraps the whole ship (and what is docked to it) in a cyan bubble that takes impacts and black hole tidal forces: **150 points** (an impact counts its change of speed in m/s). Draws 2,000 W and 0.02 oxygen/s, recharges 0.5 point/s. At zero it breaks and restarts **5 minutes** later. The bubble is an energy shell: almost invisible face-on, glowing at the edges, covered with a hexagonal grid, crossed by a rising scan band; each impact sends a ripple across the bubble. It turns red as the charge drops and flickers when almost empty.

**With the Arsenal mod**: the field also takes its weapons (and the [pirates'](Pirates.md)). Bullets and shrapnel hitting the hull, and blast on parts, drain the field instead of damaging the ship (plate damage ÷ 4, blast power ÷ 20: the biggest explosion takes about half a full field). While it holds, explosions no longer push the ship. Missiles and rockets entering the bubble explode on it, with a ripple where they hit. Signal socket: charge in % (-1 off, negative: seconds before restart).

### The six generators

| Generator | Points | Recharge | Power | Fluid | Restart | Research |
|---|---|---|---|---|---|---|
| **Compact field** (bluish white, small) | 60 | 0.3 /s | 600 W | 0.01 oxygen/s | 5 min | Artifact technology |
| Force field (cyan) | 150 | 0.5 /s | 2,000 W | 0.02 oxygen/s | 5 min | Artifact technology |
| **Rapid field** (orange) | 300 | 6 /s | 5,000 W | 0.03 oxygen/s | **1 min** | Artifact technology |
| **Reinforced field** (gold) | 450 | 1.5 /s | 6,000 W | 0.05 oxygen/s | 5 min | Artifact technology |
| **Nuclear field** (green) | 750 | 2.5 /s | 8,000 W | 0.01 nuclear fuel/s | 5 min | Nuclear power |
| **Dark matter field** (violet) | 1,200 | 4 /s | 10,000 W | 0.02 dark matter/s | 5 min | Dark matter |

Same behavior and sockets for all; the bubble takes the generator's color. Several generators on a ship: the most charged one takes the hits. They have their own row in the TF Avancé tab.

### It does not turn on?

The power grid must be able to give it **all** of its power (together with the grid's other machines). A wood generator gives 4,000 W, a quantum reactor 12,000 W (6,000 W per socket if it feeds two grids), a nuclear reactor 40,000 W. The generator shows what it lacks:
- **blinking red rings**, signal **-3**: not enough power. It stops asking for 5 s then retries, so it does not starve the rest of the ship;
- **blinking orange rings**, signal **-2**: no fluid (wrong fluid, empty line or flow too low);
- rings off, signal **-1**: no power socket plugged;
- signal **-10 and below**: it broke, it restarts in (−value − 10) seconds.

## Force field door

An emitter you set **on the floor, in the middle of an opening in the hull**, facing the opening. Look at it and press the use key (**T**): a window lets you choose the **field's width and height** (1 to 50 m each, for example 2 × 2 or 10 × 20; an orange outline shows the size while you set it) and switch it on or off.

- On, the field **holds the oxygen like a closed door**, but **you walk through it** (a ripple runs from where you cross it).
- It draws **25 W per m²** (setting `FieldDoorWattsPerSquareMeter`) on its two power sockets; the grid must be able to supply all of it. Without power it goes out and the air escapes.
- Rail lights: cyan up, blinking orange not enough power or starting, blinking red no grid, dark off.
- **Negative values**: a negative width sends the field to the other side; a negative height makes it go down from the emitter (set on a ceiling).
- **Solid**: the "Solid" setting in the window makes the field a wall, nobody gets through (the air stays in).
- **Color**: paint the door with the paint gun, the field takes its color.
- **Door manager** (same row): a console listing every door of the ship and of what is docked to it (how many, which ones are up) switching them on or off and making them solid or walk-through, one by one or all together. No cable.
- **Side**: the field spreads on both sides of the emitter, or only to its left or its right (seen facing the emitter, from the side without plugs): then set it against one side of the opening.
- **Thickness**: set in the window, from 5 cm to 3 m (20 cm by default). The field goes down to the floor. While it is up the rail has no collision: you walk over it without bumping, and it can be painted and taken down even when on.
- Take the opening's size: the field overlaps the hull around by half a meter, and settles into the hull's plane by itself.
- Research **Artifact technology**, force field row of the TF Advanced tab.

## Tractor beam

An emitter to bolt to the hull (artifacts row of the TF Advanced tab, research *Ship mining*): its beam goes straight out of the surface it is placed on.
- Switch it on: look at it and press **T**, or the **Y** key (`TractorKey`) from the pilot seat (every beam of the ship).
- Powered (3,000 W), it **locks onto the nearest ship in its beam** (a ship, a wreck, a pirate) up to 150 m, **pulls it in to 14 m** in front of it and holds it there, at your speed (its spin is damped). A heavy ship comes slowly. Switch it off to let go.
- Command socket: with a cable it only works while the signal is above 0. Status socket: 2 locked, 1 searching, 0 off, 3 no power.

## Landing gear

A retractable leg with a shock absorber (TF Advanced tab, propulsion row, Advanced propulsion research), fixed **under the hull**: it comes out toward the ground.

- **Deployed**, it carries the ship like a suspension: spring and damper soak up the landing (tested: a 5 m drop), the pad grips the ground without sliding, even on a slope.
- **Retracted**, it folds flat against the hull.
- It comes out and goes in with the **signal** on its command socket (the right one): wire it to a lever, a pilot seat, an advanced control or anything giving a signal (out above 0.5). Unwired, it stays out. The left socket gives its state (1 out).
- Fit at least three, spread apart. The stiffness adapts to the ship's mass and the number of legs.

## Nuclear power

Between artifact power and dark matter. **Uranium** is mined on TF Aridia, TF Petra and Pyros (greenish rocks with glowing veins).

| Machine | Effect |
|---|---|
| Enrichment centrifuge | takes uranium ore in its hatch (by hand or by an item line) and turns it into **nuclear fuel** (fluid) in the line on its fluid socket: 0.05 ore/s, 0.2 unit of fuel per ore, 6,000 W. Wired like the game's ethanol distiller |
| Nuclear fuel tank | the game's tank, armored (takes every fluid) |
| Nuclear reactor | **40,000 W** for 0.002 unit/s of nuclear fuel; pressure vessel, control rods, cooling loops |
| Nuclear thruster | **93,600 N** (12 Chemical Thrusters) for 0.002 unit/s of fuel and 0.02 of oxygen |
| Heavy nuclear engine | **234,000 N** (30 Chemical Thrusters) for 0.005 unit/s of fuel and 0.05 of oxygen; turbopumps and radiators |

Nuclear and dark matter engines are wired like the Chemical Thruster, but one socket takes their fuel and the other **oxygen**, in any order. Three ways to control them:
- **command socket** (signal socket at the back of their base): a signal on it sets their thrust, from 0 to 1. A cable, or a small transmitter plugged straight into the socket: a transmitter plugged there hears its channel across the whole assembly, rotor pods included (the game itself only links channels inside one ship);
- **valve or pump** on their line, like the Chemical Thruster: only if the line going to the engine has no tank (fuel only comes through the valve); they push when the valve lets fuel through;
- **plugged straight** into their tanks, with no signal: they obey the **pilot seat** and only push if a pilot sitting in a seat of the ship (or of a docked ship) asks for thrust in their direction (forward, back, up, down; the advanced seat also flies sideways).

Setting `EngineSeatControl = false`: plugged straight in, they push as soon as they are fed. One uranium ore runs a nuclear reactor for 100 s.

## Portal generator

Opens a violet portal **45 m in front of it** (in the direction of its front face), fixed in space, 18 m in radius. The ship that goes through comes out next to the **target picked on the targeting computer or the star map**:
- a planet: at about 20 km altitude, on the side you come from;
- a beacon (station, wreck, ship): 300 m away.

How it works: a signal > 0 on the **command socket** starts it; it charges for **10 s** at 8,000 W (less power: it charges slower), burns **3 dark matter** (fluid socket), then the portal stays open **30 s**, and the generator recharges for 60 s. State socket: 0 ready, 1 to 99 charge %, 100 open, -1 no target, -2 not enough dark matter, -3 no power, -10 and below: seconds of recharge.

The model: an upright ring in twelve armored sections with glowing seams and four focusing emitters, between two pylons circled with coils and fins. In the middle, two nested **gimbal rings** turn like a gyroscope around a **floating crystal**: they spin faster with the charge, the crystal throbs and everything turns violet, then flickers when the charge is nearly full. Below, an emitter lens; at the back, capacitor banks and a control desk.

**Portal opening**: sixteen frame segments fly in from all around and lock into a ring with a flash, then the horizon opens from the middle: two swirls turning against each other, ripples running out to the rim, sparks spiraling inward, a rune ring and a violet halo lighting what is near. When it closes (time up or ship through), the horizon collapses into a flash and the segments fly apart.

It is the only sensible way to reach the [Erebus galaxy](Galaxie-Erebe.md).

## Cartography table

A square 1.4 m holographic table (TF Avancé tab, computers row): vented octagonal base, black gridded glass top, central emitter, a keyboard desk and two small screens at the front, facing the player. The hologram fits inside the top's frame. When a player comes near, it shows:
- the **3D relief** of the ground 300 m around the ship (contour lines on a see-through surface);
- markers: deposits and rocks (orange), chests (yellow), buildings (violet), ships (white), your ship (green);
- the **planet's card**: biome, gravity, air, resources, places, use, dangers (the cards of this wiki).

## Engines

Vector thruster, heavy engine, precision engine, efficient engine, exotic matter engine (78,000 N for 10% of the propellant).

## Star map

The game's star map shows the system the ship is in. In its menu (as in the targeting computer's), the **Galaxy** button switches from **442 Otrera** to **Erebus** then back to the ship's system: the map and the target list show the planets of the chosen galaxy.

## Sockets

Every machine of the mod has its sockets **in a single row on the back face**: all cables leave along the same axis. From left to right (seen from behind): power, fluid, signal, power.
