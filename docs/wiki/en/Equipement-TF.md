# TF equipment

The objects of the **StellarDrive Demo TF** mod joined TF Advanced (v0.18), in the **TF Équipement** build tab:

| Object | Use |
|---|---|
| **15 lamps** (ceiling light, wall light, spotlight, light strip, recessed spot, industrial lamp, flashing beacon, rotating beacon...) | light the walls, floors and parts around them, stronger at night and indoors; take the color painted with the TF mod's paint gun |
| **Surveillance camera** | fix it anywhere; the **tablet** (**F9**) shows its picture, arrows switch cameras, Z changes the size |
| **Teleport capsule** | look at it and press **T**: pick another (named) capsule and you are teleported there. A pad with a turning emitter, pillars, a lit back wall and a console; its energy rings sweep up and down, faster with you inside, and flare when you leave |
| **Radio** | **T**: plays music (file or YouTube link) heard by every player around |
| **Tool rack** | **T**: take back a lost base tool (build tool, recycler, mining laser), the paint gun, the schematic tablet; tells you when you already have it |
| **Infinite generator, oxygen and ethanol tanks, resource chest** | endless, but **extremely expensive**: hundreds of iron, copper or glass, 8 quantum cores, 10 exotic matter, 6 artifacts, 40 Nexoran alloys and 30 meteorites each |

**Radio, mute**: in the radio's window, "Muted for me" silences it for you alone. The host sees the players there and can block those who may not change the music (`RadioLockedPlayers`).

Same part numbers as before: what is already built in your worlds stays. Keep the TF mod up to date (0.15 or later): it leaves these objects to TF Advanced (chairs, wardrobe and docking doors stay in the TF mod). With an older TF mod, it keeps them, without duplicates.

## Paint gun, schematic tablet, third person view

Without the TF mod, TF Advanced also provides (same items, same files: nothing is lost):
- the TF mod's **paint gun**: it paints walls and floors, and also doors, machines and every placed part, with a finish (gloss, metal, glow), presets and **camouflage** patterns (forest, desert, arctic...). Take it from the tool rack, **left click** paints, **right click** opens the palette, **mouse wheel** picks a color. Colors are saved with the world (`tf-paint.json`);
- the **schematic tablet**: take it from the tool rack, select it in your toolbar, **left click** to open its window. **Copy** saves the nearest ship (and what is attached to it); **Build** makes it again for free in front of you. Schematics are on your PC in `UserData/TF/schematics`, shared by all your worlds. The ship's paint is copied too;
- the **third person view** while piloting: key **V** (`ThirdPersonKey`), mouse wheel for the distance (`ThirdPersonDistance`);
- the **KeySeat** mod's seat keys saved at once and copied by schematics.

When the TF mod is installed, it provides them itself, with no double.

Settings: section `[TFAdvancedObjects]` (tablet key `TabletKey`, use key `UseKey`, camera frames per second, lamp lighting strength `LampLightStrength`, radio volume).
