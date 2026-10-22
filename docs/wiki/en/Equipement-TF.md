# TF equipment

The objects of the **StellarDrive Demo TF** mod joined TF Advanced (v0.18), in the **TF Équipement** build tab:

| Object | Use |
|---|---|
| **20 lamps** (ceiling light, wall light, spotlight, light strip, recessed spot, industrial lamp, flashing beacon, rotating beacon, floor lamp, desk lamp, bulkhead light, ring chandelier, up and down wall light, track spots...): placed on any surface (hull, slope, ceiling, placed parts) | light the walls, floors and parts around them, stronger at night and indoors; take the color painted with the TF mod's paint gun |
| **Surveillance camera** | fix it anywhere; the **tablet** (**F9**) shows its picture, arrows switch cameras, Z changes the size |
| **Teleport capsule** | look at it and press **T**: pick another (named) capsule and you are teleported there. A pad with a turning emitter, pillars, a lit back wall and a console; its energy rings sweep up and down, faster with you inside, and flare when you leave |
| **Radio** | **T**: plays music (file or YouTube link) heard by every player around |
| **Tool rack** | **T**: take back a lost base tool (build tool, recycler, mining laser), the paint gun, the schematic tablet; tells you when you already have it |
| **Infinite generator, oxygen and ethanol tanks, resource chest** | endless, but **extremely expensive**: hundreds of iron, copper or glass, 8 quantum cores, 10 exotic matter, 6 artifacts, 40 Nexoran alloys and 30 meteorites each |

**Teleporter** (item **7419**, no recipe: give it to yourself with a give command): held in the hand, left click opens the list: **planets** (set down on dry land, day side), **stations** and **beacons** (set down beside the beacon).

**Cosmetic cabin**: T to change your look: colors of the suit, helmet, backpack and visor, a hat (cap, cowboy, top hat, beanie, crown), sunglasses (classic, aviator, visor band) and lens color, a jacket (vest, jacket, long coat) and its color. The booth's mannequin wears your outfit; the other players see you in it (setting `Outfit`).

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

## Photo mode and photo frame

- **Photo mode** (**F8** key, setting `PhotoKey`, or a left click with the **camera** in hand, taken from the tool rack): the view leaves the player and flies freely (mouse to look, WASD to move, Space/Ctrl up and down, Shift faster, Alt slower, wheel to zoom, R to tilt and T to level). The game's interface goes. **F** changes the filter (black and white, sepia, vivid, cinema, night vision), **H** hides the help, **P**, Enter or a click takes the photo, Esc or F8 comes back.
- Photos go to `UserData/TFAdvanced/photos` (any PNG or JPG put there works too).
- **Photo frame** (TF Equipment tab, 3 wood + 1 glass): look at it and press **T** to choose a photo. It is sent to the host, kept with the world and shown to every player. The picture faces out of the surface the frame is on: best on a wall.
