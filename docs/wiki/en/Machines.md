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
