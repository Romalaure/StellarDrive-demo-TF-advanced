# Pirates

*With the **Arsenal** mod only: pirates use its weapons.*

When your ship flies in space (in orbit or between planets), a group of pirates now and then shows up about 2 km away and attacks you. On average one attack every **12 minutes** spent in space (setting `PirateIntervalMinutes`). There is never more than one group at a time.

Each attack draws a difficulty level at random:

| Pirate | Difficulty | Hull | Weapons | Flying | Group | Chance |
|---|---|---|---|---|---|---|
| **Scout** (grey, yellow lights) | easy | 1,200 points | 1 machine gun, poor aim | very fast (95 m/s), fights at about 150 m | 2 or 3 | 35% |
| **Corsair** (red) | medium | 2,500 points | 2 machine guns, 1 missile launcher | fast (80 m/s), about 170 m | 1 or 2 | 35% |
| **Raider** (black, orange lights) | hard | 4,200 points | 2 machine guns, 1 laser cannon, 1 missile launcher | 70 m/s, about 210 m | alone | 20% |
| **Cruiser** (dark violet) | very hard | 7,000 points | 2 plasma cannons, 2 machine guns, 3 missile launchers, point defense | slow (50 m/s), about 260 m | alone | 10% |

They have a real ship shape: pointed nose with a ram under the glazed cockpit, canopy, dorsal ridge, swept wings with wingtip pods and lights, slanted fins, engine pods with glowing nozzles, riveted side armor, antenna. This fairing does not stop shots: they go through to the iron hull it covers.

**Bursts and reload**: their machine guns, lasers and cannons no longer fire non-stop but in bursts, followed by a reload time:

| Pirate | Burst | Reload | Aim | Missiles |
|---|---|---|---|---|
| Scout | 1.2 s | 4 s | ± 4° | none |
| Corsair | 1.5 s | 3.5 s | ± 2.5° | every 8 s |
| Raider | 2 s | 3 s | ± 1.5° | every 6 s |
| Cruiser | 2.5 s | 3 s | ± 1° | every 4 s |


## Radars

Three radars (tab **TF Avancé**, combat row) detect the pirates around the ship:

| Radar | Range | Power | Cost |
|---|---|---|---|
| **Short range radar** (green glow) | 1,000 km | 400 W | 10 copper, 6 glass, 6 iron |
| **Long range radar** (golden glow) | 5,000 km | 1,200 W | 20 copper, 10 glass, 8 titanium, 4 quartz |
| **Deep space radar** (violet glow) | 20,000 km | 3,200 W | 30 copper, 15 titanium, 8 quartz, 6 cryonite, 1 quantum core |

It needs power on its power sockets (the grid must be able to give all of it). Its **round screen** shows the contacts seen from above, the radar's front at the top: distance on a log scale (rings at 10, 100, 1,000 and 10,000 km), a sweep turns and lights the echoes as it passes. Beside it, the list of the nearest contacts: pirate ships (red), **bases** (orange) and **incoming raids** with their time before contact. Its signal socket gives the number of contacts (wire it to an alarm); off: -1 no power grid, -3 not enough power.

**Incoming raids**: a pirate group no longer appears all at once. It is spotted **25,000 km** away (setting `PirateRaidStartKm`) and rushes at its target at **250 km/s** (`PirateApproachKmPerSecond`) before jumping in beside it. A radar sees it as soon as it enters its range: about **80 s** of warning with the deep space radar, **20 s** with the long range radar, **4 s** with the short range radar. When a radar near you sees a raid coming, an **alert** shows at the top of the screen (setting `RadarAlarm`).

## Stealth generator

The **stealth generator** (6,000 W, setting `CloakWatts`) blurs the signature of the ship and everything docked to it on pirate sensors while it is powered (its rings turn, the crystal glows):
- raids find the ship **three times less often**, and a raid already on its way may **lose its track** (about one chance in five every 5 s);
- pirates only see it within **2.5 km** (bases within **1.2 km**); farther, they lose it;
- they must come closer to fire (**400 m** instead of 650), **aim far worse** and lock their missiles later (**800 m**, narrower cone);
- bases fire their missiles within 1.2 km instead of 2.5 km and launch their fighters within 1.5 km instead of 4 km.

Cost: 2 quantum cores, 10 cryonite, 8 quartz, 12 titanium, 15 copper. Its signal socket reads 1 while it hides the ship. Radars and generator are unlocked by the **Electronic warfare** tech (after Cartography).

## Safe zone

Around the starting planet, **442 Otrera b**, lies a **2,000 km** safe zone (setting `PirateSafeRadiusKm`, in km from its centre; 0 removes it). No pirate attacks there, and a fight that enters it is broken off: the pirates leave after 15 s.

## Pirate bases and Bastion Écarlate

In the Erebus galaxy, the pirates have their own planet, [Bastion Écarlate](Bastion.md) (the Scarlet Bastion), an artificial planet covered with fortresses, and three armed **bases**, shown on the star map (their beacons keep their French names):

| Base | Where | Weapons |
|---|---|---|
| Forteresse Écarlate (Scarlet Fortress) | Bastion orbit | 8 machine guns, 4 plasma cannons, 3 point defenses |
| Chantier de la Rouille (Rust Yard) | Bastion orbit | 7 machine guns, 1 laser cannon, 2 point defenses |
| Avant-poste du Croc (Fang Outpost) | Pyros orbit | 5 machine guns, 1 point defense |

A base has **20,000 hull points** and does not move. When a ship comes near:
- its guns that point at it fire in bursts (under 900 m);
- it launches salvos of 2 homing missiles every 5 s (under 2.5 km);
- it sends fighters out of its hangar (scouts or a corsair) every 2 min 30 (under 4 km), 3 at most at a time.

Destroyed, it explodes and leaves a big loot capsule (quantum cores, exotic matter, Nexoran alloy, titanium, uranium, artifacts); its wreck and chests stay, it does not come back. Around the Bastion (5,000 km), pirate attacks are **four times as frequent**.

## How they fight

The pirate pilot rushes at your ship then circles it at its fighting distance, nose pointed at you (the less skilled aim worse). Its machine guns and cannons fire in bursts when it is lined up (under 650 m); its missiles leave when you are in their cone (under 1,300 m). Near a planet, it keeps some altitude above you.

## Beating them

- Every Arsenal weapon hits them: bullets go through their iron hull, explosions count much more. A scout falls to 2 missiles, a corsair to 4, a raider to 7, a cruiser to about 11.
- A **force field** (see [Machines](Machines.md#force-field)) takes their shots instead of the hull: the rapid field or the nuclear field are made for combat.
- Arsenal's point defense shoots down their missiles.

Destroyed, a pirate explodes and leaves a **loot capsule** (a chest on a plate), richer with its difficulty: titanium, uranium and cryonite for the scout; plus Nexoran alloy for the corsair; quantum core and meteorite for the raider; the cruiser adds an artifact fragment.

A pirate left far behind (more than 9 km) for 10 minutes goes away.

Settings (`[TFAdvanced]`): `Pirates = false` to see no more of them, `PirateIntervalMinutes` for how often.
