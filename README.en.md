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
