# StellarDrive Demo — TF Advanced

![TF Advanced](docs/images/hero.jpg)

*[Version française](README.md)*

Advanced mods by the TF group for **StellarDrive Demo**, in a single MelonLoader DLL, independent of the `StellarDriveDemoTF` mod:

- **ship computer** screens showing live data from the game (flight, propulsion, power, diagnostics);
- **engines**: vector thruster, heavy, precision, efficient, exotic matter, nuclear and dark matter engines;
- **power grid**: battery, circuit breaker, quantum reactor, cryonite battery, solar panels;
- **building parts** in wood, glass and iron, beams, plates and frames;
- **space stations** to build: habitation, corridors, hub, stairwell, dome, airlock, docking, laboratory, infirmary, life support, observatory;
- **7 new planets** around Otrera and a **second galaxy, Erebus** (4 planets and an artificial pirate planet, a black hole, wrecks), reached through a **portal**;
- new ores (titanium, quartz, cryonite, amber, **meteorite**, **uranium**), **nuclear power** (centrifuge, reactor, engines) and **dark matter** (collector, reactor, engine, portal);
- **force fields** (six generators), cryonite condenser, quantum gyroscope, **cartography table**;
- a branch of the **research table**, 3 abandoned stations, 3 black holes, asteroid fields;
- **pirates** (with the Arsenal mod): random attacks, pirate bases, the Bastion Écarlate;
- **radars** (1,000 / 5,000 / 20,000 km) showing pirate ships, bases and incoming raids on a round screen, and a **stealth generator** that makes the ship hard to find and to hit (v0.17).

Three build tabs: **TF Avancé**, **TF Construction**, **TF Station**.

**In-game wiki**: open the **Guide** (G key), entry **TF Advanced Wiki** at the top of the list: every planet, its resources and use, machines, building parts, stations, research, with links between pages. The same pages are in [docs/wiki/en](docs/wiki/en/Home.md).

## Pictures

![Overview of the mod](docs/images/overview.jpg)

| | | |
|---|---|---|
| ![Trous noirs](docs/images/blackholes.jpg)<br>Black holes | ![Nouvelles planètes](docs/images/planet_jungle.jpg)<br>New planets | ![Galaxie d'Érèbe](docs/images/planet_erebos.jpg)<br>Erebus galaxy |
| ![Mondes exotiques](docs/images/planet_exotic.jpg)<br>Exotic worlds | ![Champ de force](docs/images/forcefield.jpg)<br>Force field | ![Portail](docs/images/portal.jpg)<br>Dimensional portal |
| ![Pirates](docs/images/pirates.jpg)<br>Pirates (Arsenal) | ![Modules de station](docs/images/station.jpg)<br>Station modules | ![Carte stellaire](docs/images/starmap.jpg)<br>Star map |
| ![Poste de pilotage](docs/images/cockpit.jpg)<br>Control seat | ![Objets et arbres](docs/images/items.jpg)<br>Items, ores, trees | ![Ordinateur de bord](docs/images/computer.jpg)<br>Ship computer |

## Reinforced hull, ship mining laser (v0.25)

- **Reinforced hull** (*TF Blindage* tab): the game's iron and glass floors, plates, corners and slopes, reinforced. Same model, a slightly darker shade, real airtight hull. 3 times tougher in collisions; with Arsenal, a third of the damage and no holes. 1.5 times heavier.
- **Ship mining laser**: the player's mining laser on a turret that bolts to the hull. Its beam (40 m) mines rocks, crystals and asteroid ores, 3 times faster than the tool, and puts what it mines into the chest linked to its item socket by an item line. 800 W, command and status sockets.

## Stations you can enter, pirate fortress chests (v0.24)

- **Stations and wrecks you can enter**: their entrance is now one of the game's docking doors, open: dock with your ship's docking door, or float in. Stations already in your worlds are rebuilt with their door when the world loads (a destroyed pirate base stays a wreck).
- **More realistic stations**: ceiling lights, lit strips in corridors, a console, a star map, a research table, shelves, supply crates besides the chests; outside, a truss mast, solar panels along the corridors, antennas and blinking beacons.
- **Chests in the Bastion's pirate fortress**: towers and domes hide the pirates' plunder (alloy, titanium, uranium, meteorite, quantum cores, gold).

## Paint gun, rack fixed, new capsule (v0.23)

- **The TF mod's paint gun**: without the TF mod, TF Advanced provides it. It also paints doors, machines and placed parts, with finishes, presets and camouflage; the colors you already laid (`tf-paint.json`) come back, and schematics copy the paint.
- **Tool rack**: gives the paint gun and the tablet, and tells you when you already have it (before, it silently gave nothing).
- **Teleport capsule remade**: a pad with a turning emitter, profiled pillars, a lit back wall, an emitter roof, a console; animated energy rings, a light column and rising sparks, brighter with someone inside and on departure.

## Ships turn without jerks (v0.22)

- **Jerky turns fixed**: with vertical sync on a fast screen (165 Hz) and the game around 60 fps, frames stayed on screen 2 then 3 refreshes (12 then 18 ms): a ship turning steadily moved by small then big steps. When the game can't keep up with the screen, every frame now waits the same number of refreshes (a steady 55 fps on 165 Hz). Every 45 s it checks whether the game got fast enough again. Never under 45 fps, nothing without vertical sync; setting `SteadyFrameRate`.

## Schematic tablet, third person, lighter portal, new radars (v0.21)

- **Schematic tablet and third person view** (from the TF mod): without the TF mod, TF Advanced provides them. Same tablet (item 7201), same schematics (`UserData/TF/schematics`), key **V** while piloting. The tablet comes from the tool rack. KeySeat seat keys are kept and copied by schematics.
- **Portal**: it draws 8,000 W instead of 20,000 (an infinite generator gives 20,000: it stalled as soon as another machine drew power) and burns 3 dark matter instead of 5. Short of power, it charges slower instead of stopping. Settings left at the old values move to the new ones.
- **Radars remade**: a sweeping bar antenna for short range, a dish on a tripod with a counterweight for long range, a lit phased array panel for deep space; braced mast, lit bearing, screen visor, status lights.

## Tougher pirates the farther you go, new portal (v0.20)

**Danger grows with distance**: near the home planet, only scouts, one or two at a time, and less often. One danger level more every 5 million km (setting `PirateDangerStepKm`): corsairs and hornets around TF Aridia, raiders and lancers around TF Verdance and TF Glacia, escorted cruisers and torpedo boats past TF Nexora, and dreadnoughts in the Erebos galaxy.

**Four new pirates**, armed with the Arsenal weapons they did not use yet:
- **Hornet** (level 2): yellow with black stripes, the fastest, looses 9 rockets in a row;
- **Lancer** (level 3): railgun and two ion cannons that jam your weapons, fires from afar;
- **Torpedo boat** (level 4): two torpedo launchers, a SAM battery and a point defense;
- **Dreadnought** (level 5, Erebos only): 14,000 hull points, 14 weapons, command tower.

Each has its own fairing. Pirate bases now launch hornets too.

**Dimensional portal reworked**: the generator has a ring in sections with glowing seams and two gimbal rings turning like a gyroscope around a floating crystal, faster and faster while it charges. The portal opens in steps: sixteen segments fly in and lock with a flash, then the horizon opens with two swirls turning against each other, ripples, sparks drawn in, a rune ring and a violet light. When it closes it collapses into a flash, even after the ship has gone through.

## Spanish, full translation (v0.19)

The mod now speaks **Spanish**: setting `Language = "es"` (or `auto` with the game in Spanish). Parts, machines, research, ship computer screens, radars, planets, cartography table and the **in-game wiki** (27 pages) are translated. The TF equipment (lamps, camera and tablet, capsule, radio, tool rack, infinite parts), French only since it came from the TF mod, now speaks French, English and Spanish.

**Lamps**: the `[TFAdvancedObjects]` settings were never loaded since v0.18. Every lamp threw an error each frame (thousands a minute, lower FPS), and the tablet, radio and use key could not read their settings. Fixed.

## TF equipment and stability (v0.18)

The TF mod's **lamps, camera and its tablet, teleport capsule, radio, tool rack and infinite parts** are now in TF Advanced (tab **TF Équipement**, same part numbers: what is built stays). Infinite parts are now **extremely expensive**. The TF mod 0.15 leaves them to it. Details: [wiki, TF equipment](docs/wiki/en/Equipement-TF.md).

**Shaking ship**: the quantum gyroscope (torque x5) made the ship oscillate at every physics step as soon as it turned, and after (the game's gyroscope controller asked it for 5 times too much). It now knows its strength: no more oscillation, and from 47-55 to 65-70 FPS while turning a big ship. Also: no more thousands of "NetworkManager not found" messages in the menu (TF Advanced and TF), the force field bubble and the cartography table no longer walk the whole ship or scene every 2-3 s.

## Language

The mod speaks **English**, **French** and **Spanish**. Setting `Language` (section `[TFAdvanced]` of `UserData/MelonPreferences.cfg`):

| Value | Language |
|---|---|
| `auto` (default) | the game's language; French or Spanish if the game is in French or Spanish, English otherwise |
| `en` | English |
| `fr` | French |
| `es` | Spanish |

Part names and descriptions are read at start-up: restart the game after changing it. The in-game wiki follows the setting at once. Names saved in a world (station beacons, planets) keep their original French names so that every player of a world sees the same names.

## Installation

**With StellarModManager**: download "StellarDrive Demo TF Advanced", then "Install in game".

**By hand**:
1. MelonLoader 0.7.3 and **SDModKit** installed in the game.
2. Download `StellarDrive-demo-TF-advanced.zip` from the [latest release](https://github.com/Romalaure/StellarDrive-demo-TF-advanced/releases/latest).
3. Copy `Mods/StellarDriveDemoTFAdvanced.dll` from the zip into `StellarDrive Demo/Mods/`.

In multiplayer, **every player** needs the mod: physics runs on the host, and the parts must exist for everyone. A world saved with these parts still loads without the mod: the game skips unknown parts.

Optional: the **Arsenal** mod (weapons) enables pirates and lets force fields stop weapons.

## Getting started

1. Go to **TF Verdance**: wood, amber, village chests. Research *Carpentry* and *Metal construction*.
2. **TF Altis** or **TF Petra** for titanium, Petra for the first meteorites (no air there).
3. **TF Glacia** for cryonite: cryonite battery, condenser, force field.
4. **TF Nexora** for quantum cores and alloy: quantum reactor.
5. Research *Dark matter*, then *Dimensional portal*, and open a portal to **Erebus**.

The [suggested route](docs/wiki/en/Itineraire.md) and the [planets](docs/wiki/en/Home.md) pages have the details.

## Settings

`UserData/MelonPreferences.cfg`, section `[TFAdvanced]`:

| Key | Default | Role |
|---|---|---|
| `Language` | `auto` | language of the mod's texts: `auto`, `en` or `fr` |
| `ComputerRefreshRate` | `4` | screen refreshes per second (1-20) |
| `ComputerViewDistance` | `40` | beyond this (m), a screen stops refreshing |
| `TrafficRange` | `3000` | range of the traffic list (m) |
| `NewPlanets` | `true` | the new planets |
| `Erebos` | `true` | the Erebus galaxy (needs `NewPlanets`) |
| `BlackHole` | `true` | the black holes |
| `AbandonedStations` | `true` | creates the abandoned stations in a world that does not have them yet |
| `ResearchUnlocks` | `true` | the mod's parts are unlocked at the research table (`false`: all available at once) |
| `EngineSeatControl` | `true` | nuclear and dark matter engines obey the pilot seat (`false`: they push as soon as they are fed) |
| `PirateRaidStartKm` / `PirateApproachKmPerSecond` | `25000` / `250` | where a pirate raid is spotted (km) and how fast it closes in (km/s) |
| `RadarAlarm` | `true` | on-screen alert when a radar near you sees a raid coming |
| `SteadyFrameRate` | `true` | vertical sync on and game slower than the screen: same number of refreshes for every frame, so turns don't jerk (never under 45 fps) |
| `RadarWatts` / `CloakWatts` (`[TFAdvancedPower]`) | `400` / `6000` | power of the short range radar (x3 long range, x8 deep space) and of the stealth generator |
| `Pirates` / `PirateIntervalMinutes` | `true` / `12` | pirates (with Arsenal) and mean time in space between two attacks (min) |
| `PirateSafeRadiusKm` | `2000` | pirate-free zone around 442 Otrera b (km, 0: none) |
| `TerrainChunkBuffer` | `1.5` | room for terrain chunks on the graphics card, times the game's; above 1, high mountains no longer leave holes in the ground |

Power values (reactors, batteries, force fields, portal…) are in section `[TFAdvancedPower]`. In multiplayer, world settings (planets, galaxy, black holes) must be the same for every player.

## Building

Requirements: .NET SDK 8, the game installed with MelonLoader and SDModKit.

```
dotnet build src/StellarDriveDemoTFAdvanced -c Release
```

The build copies the DLL into the game's `Mods` folder. `tools/package.ps1` builds the release zip.
