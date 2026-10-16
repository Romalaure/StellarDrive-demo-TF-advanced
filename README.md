# StellarDrive Demo — TF Advanced

![TF Advanced](docs/images/hero.jpg)

*[English version](README.en.md)*

Mods avancés du groupe TF pour **StellarDrive Demo** : ordinateur de bord, moteurs, réseau électrique, constructions en bois, verre et fer, **stations spatiales** à construire, 7 nouvelles planètes autour d'Otrera et une **seconde galaxie, Érèbe** (4 planètes et une planète artificielle pirate, un trou noir, des épaves) atteinte par **portail**, nouveaux minerais (dont la **météorite** et l'**uranium**), l'**énergie nucléaire** (uranium, centrifugeuse, réacteur, moteurs) et une nouvelle énergie, la **matière noire**, pièces avancées (réacteur quantique, champ de force, condenseur, gyroscope quantique, moteur exotique, machines à matière noire, générateur de portail, table de cartographie), une branche de la **table de recherche**, 3 stations abandonnées, 3 trous noirs, champs d'astéroïdes, dans un seul DLL MelonLoader, indépendant du mod `StellarDriveDemoTF`. Trois onglets de construction : **TF Avancé**, **TF Construction**, **TF Station**.

**Wiki dans le jeu** : ouvrez le **Guide** (touche G), entrée **Wiki TF Avancé** en tête de liste : chaque planète, ses ressources et son utilité, machines, constructions, stations, recherche, avec des liens entre les pages. Les mêmes pages sont dans [docs/wiki](docs/wiki/Home.md) (le mod les embarque à la compilation). Résumé : [docs/PLANETES.md](docs/PLANETES.md).

Rapport technique (moteur, systèmes du jeu, faisabilité de chaque projet) : [docs/AUDIT.md](docs/AUDIT.md).

## En images

![Vue d'ensemble du mod](docs/images/overview.jpg)

| | | |
|---|---|---|
| ![Trous noirs](docs/images/blackholes.jpg)<br>Trous noirs | ![Nouvelles planètes](docs/images/planet_jungle.jpg)<br>Nouvelles planètes | ![Galaxie d'Érèbe](docs/images/planet_erebos.jpg)<br>Galaxie d'Érèbe |
| ![Mondes exotiques](docs/images/planet_exotic.jpg)<br>Mondes exotiques | ![Champ de force](docs/images/forcefield.jpg)<br>Champ de force | ![Portail](docs/images/portal.jpg)<br>Portail dimensionnel |
| ![Pirates](docs/images/pirates.jpg)<br>Pirates (Arsenal) | ![Modules de station](docs/images/station.jpg)<br>Modules de station | ![Carte stellaire](docs/images/starmap.jpg)<br>Carte stellaire |
| ![Poste de pilotage](docs/images/cockpit.jpg)<br>Poste de pilotage | ![Objets et arbres](docs/images/items.jpg)<br>Objets, minerais, arbres | ![Ordinateur de bord](docs/images/computer.jpg)<br>Ordinateur de bord |

## Installation

**Avec StellarModManager** : télécharger « StellarDrive Demo TF Advanced », puis « Installer dans le jeu ».

**À la main** :
1. MelonLoader 0.7.3 et **SDModKit** installés dans le jeu.
2. Télécharger `StellarDrive-demo-TF-advanced.zip` depuis la [dernière release](https://github.com/Romalaure/StellarDrive-demo-TF-advanced/releases/latest).
3. Copier `Mods/StellarDriveDemoTFAdvanced.dll` du zip dans `StellarDrive Demo/Mods/`.

En multijoueur, **tous les joueurs** doivent avoir le mod : la physique tourne chez l'hôte, et les pièces doivent exister chez chacun. Un monde sauvegardé avec ces pièces reste chargeable sans le mod : le jeu ignore les pièces inconnues.

## Ordinateur de bord (Advanced Ship Computer)

Il s'agit de vraies pièces à construire, sans touche ni prise. Chaque écran affiche le vaisseau sur lequel il est posé (avec ses parties amarrées ou sur rotor), à partir des données réelles du jeu. Une valeur que le jeu ne fournit pas est affichée comme indisponible, jamais inventée.

| Id | Pièce | Pose | Contenu |
|---|---|---|---|
| 7301 | Ordinateur de bord (mural) | mur, plafond, coque | les 4 sections |
| 7302 | Console de bord | sol, écran incliné | les 4 sections |
| 7303 | Écran de vol | partout | FLIGHT |
| 7304 | Écran de propulsion | partout | PROPULSION |
| 7305 | Écran d'énergie | partout | POWER |
| 7306 | Écran de diagnostic | partout | DIAGNOSTICS |

On les tourne pendant la pose (comme toute pièce) pour orienter l'écran. Le cadre accepte la peinture du mod TF, pas la vitre.

| Section | Donnée | Source | État |
|---|---|---|---|
| FLIGHT | vaisseau, pièces, masse (vaisseaux liés compris) | état du vaisseau | ✅ |
| | type d'espace, planète de référence | espaces du jeu | ✅ |
| | altitude **au-dessus du rayon moyen** de la planète | centre de la planète via `IWorldTransposeClient` | ✅ (pas l'altitude au sol) |
| | vitesse surface / orbitale | `ShipVelocityProvider` du jeu (le même calcul que l'hologramme du siège) | ✅ |
| | vitesse verticale | vitesse surface projetée sur la verticale locale | ✅ |
| | tangage / roulis dans le repère du siège pilote | rotation du vaisseau + verticale locale | ✅ |
| | vitesse angulaire | **dérivée** de deux orientations successives (le jeu ne la synchronise pas) | ⚠️ approximation |
| | trafic : vaisseaux suivis dans le même espace, distance, rapprochement | `ShipsClientTracker` | ✅ (centre à centre) |
| | distance au sol, astéroïdes | — | ❌ aucune donnée exploitable |
| PROPULSION | chaque propulseur (jeu et pack) : niveau, poussée (N), débit d'ergols, axe | états + constantes du jeu | ✅ |
| | poussée nette, accélération | somme des poussées / masse | ✅ |
| | gyroscopes, sièges : force, alimentation | états + prises | ✅ |
| | cœur stellaire : état | `WarpDriveState` | ✅ |
| POWER | générateurs : niveau, W, bois | état + 4 000 W du jeu | ✅ |
| | électrolyseur, distillateur (W), gyroscope (niveau) | états + formules du jeu | ✅ (le jeu ne publie pas les W du gyroscope) |
| | réseaux : W transmis (client) ; demande, capacité, satisfaction (hôte) | câbles / `PowerServerTracker` | ✅ |
| | stockage | absent de la démo (Battery Bank décorative) | ℹ️ signalé |
| DIAGNOSTICS | propulseur absent, prises d'ergols, même fluide sur les deux conduites, manque de bois, gyroscope non branché ou sous-alimenté, réseau sous-alimenté, descente rapide, rotation rapide, rapprochement | règles appliquées aux données ci-dessus | ✅ |

L'en-tête de chaque écran affiche le nombre d'alertes, même sur un écran sans section DIAGNOSTICS. Un vaisseau est lu une fois par rafraîchissement, quel que soit son nombre d'écrans. Un écran éloigné de la caméra ne se rafraîchit plus.

## Pack de propulsion (Advanced Propulsion Pack)

Ce sont des clones du **propulseur chimique** du jeu. Ils ont le même modèle, la même flamme, les mêmes prises d'ergols (oxygène + éthanol) et se commandent pareil, avec des vannes sur les conduites. Le jeu les alimente, les sauvegarde et les synchronise comme son propre propulseur. Les valeurs sont calibrées sur lui : 7 800 N, 0,05 u/s de chaque ergol, 20 kg.

| Id | Moteur | Poussée | Ergols (chacun, plein régime) | Masse | Particularité |
|---|---|---|---|---|---|
| 7311 | Propulseur vectoriel | 1 950 N | 0,0125 u/s | 6 kg | **poussée décentrée** : produit un couple réel r × F |
| 7312 | Moteur lourd | 23 400 N | 0,165 u/s | 75 kg | montée en régime 1,5 s, 10 % moins efficace |
| 7313 | Moteur de précision | 624 N | 0,004 u/s | 4 kg | poussée décentrée, réponse immédiate |
| 7314 | Moteur économe | 4 680 N | 0,0175 u/s | 28 kg | ~1,7× plus de poussée par unité, montée en régime 4 s |
| 7316 | Propulseur nucléaire | 93 600 N | 0,002 u/s de combustible nucléaire + 0,02 u/s d'oxygène | 110 kg | modèle détaillé (réacteur, turbopompes, tuyère refroidie), montée en régime 2 s |
| 7317 | Moteur nucléaire lourd | 234 000 N | 0,005 combustible nucléaire + 0,05 oxygène | 320 kg | quatre turbopompes, radiateurs, montée en régime 4 s |
| 7318 | Moteur à matière noire | 624 000 N | 0,002 matière noire + 0,03 oxygène | 260 kg | le plus puissant : chambre de confinement, tuyère magnétique en anneaux, montée en régime 3 s |

Les moteurs 7316-7318 ont une **prise de commande** (signal, prise 4, à l'arrière du socle) : un signal dessus (câble ou petit transmetteur enfiché ; le canal du transmetteur est entendu sur tout l'assemblage, nacelles sur rotor comprises) règle leur poussée (0-1). Alimentés seulement par une vanne ou une pompe (aucun réservoir sur leur conduite), ils suivent la vanne comme le propulseur chimique. Branchés en direct sur leurs réservoirs sans signal, ils obéissent au **siège de pilotage** : ils ne poussent que si un pilote demande de la poussée dans leur direction (réglage `EngineSeatControl`). Ils ont leur propre modèle 3D à la place de celui du propulseur chimique (sa flamme, teintée, et son son restent). Une de leurs prises d'ergols reçoit le combustible nucléaire ou la matière noire, l'autre l'oxygène.

Fonctionnement :
- **Poussée et consommation** : trois patchs Harmony côté hôte corrigent la poussée, la demande d'ergols et la consommation, uniquement pour ces ids. Les propulseurs du jeu ne sont pas modifiés.
- **Couple** : le jeu applique toute poussée au centre de masse. Pour le propulseur vectoriel et le moteur de précision, le mod ajoute le couple r × F autour du centre de masse de l'ensemble (vaisseaux amarrés compris). Ce couple est appliqué au Rigidbody du jeu : c'est une vraie force, pas une rotation imposée. Monté au centre de masse, il ne fait pas tourner.
- **Montée en régime** : elle ne ralentit que la hausse de poussée. Un moteur ne consomme jamais plus que ce que ses conduites lui fournissent.
- **Équilibrage** : section `[TFAdvancedEngines]` de `MelonPreferences.cfg` (`HeavyThrust`, `HeavyFuel`, …). En multijoueur, ce sont les valeurs de l'hôte qui comptent.
- **Non implémenté** : gestion thermique. Le jeu n'a pas de système thermique, et le cahier des charges demande d'abord des moteurs de base validés en jeu.

## Réseau électrique (Power Grid Overhaul)

Le jeu a un vrai réseau électrique : chaque groupe de câbles fait la somme des besoins et des capacités, puis partage la puissance. En revanche, il n'a aucun stockage (sa « Battery Bank » est décorative). Ces deux pièces s'insèrent dans ce calcul natif, côté hôte, sans réseau parallèle.

| Id | Pièce | Rôle |
|---|---|---|
| 7321 | Batterie | Modèle de la Battery Bank du jeu, rendu fonctionnel. Se décharge quand son réseau manque de puissance. Se recharge avec la réserve des générateurs, qui brûlent alors réellement plus de bois. Prise signal 2 : charge en %. |
| 7322 | Disjoncteur | Relie deux réseaux : prise 0 entrée (générateurs), prise 1 sortie (consommateurs). Fait passer la puissance manquante côté sortie, dans la limite de sa puissance nominale. Disjoncte en surcharge. Prise 3 : commande. Prise 2 : état (0 fermé, 1 ouvert, 2 disjoncté). |

- **Énergie conservée** : la batterie ne stocke que ce qu'un générateur a vraiment produit, au rendement réglé, et ne rend que ce qu'elle contient.
- **Commande du disjoncteur** : sans câble, il est fermé. Avec un câble, un signal > 0 le ferme et 0 l'ouvre. Pour le réarmer après un déclenchement : signal à 0 puis > 0. Sans câble, il se réarme seul après `BreakerAutoResetSeconds`.
- **Journal** : chaque déclenchement est écrit dans le log. L'hôte voit les déclenchements des 5 dernières minutes dans DIAGNOSTICS.
- **Persistance** : la charge et l'état sont sauvegardés avec le vaisseau et synchronisés à tous les joueurs. Le mod réutilise pour ça l'état de réservoir du jeu, sans fluide et sans masse ajoutée.
- **Affichage** : la section POWER des écrans affiche le stockage total, chaque batterie (%, charge ou décharge en W) et chaque disjoncteur (état, W transmis).
- **Pas encore fait** : priorités entre circuits et répartiteur multi-circuits. Les disjoncteurs posés en cascade donnent déjà un ordre de service fixe (ids croissants).

Réglages : section `[TFAdvancedPower]` (`BatteryCapacityKJ` 1 200, soit 5 min de générateur ; `BatteryChargeWatts` 2 000 ; `BatteryDischargeWatts` 4 000 ; `BatteryEfficiency` 0,9 ; `BreakerRatingWatts` 4 000 ; `BreakerTripSeconds` 0,5 ; `BreakerAutoResetSeconds` 10).

## Construction avancée (Advanced Construction, premier lot)

La coque du jeu est une grille de voxels limitée à 4 formes codées en dur (plat, coin, pente, coin de pente). On ne peut pas y ajouter de nouvelles formes sans réécrire son générateur de maillage et de collisions (voir l'audit). Ces pièces sont donc des **pièces posées**, sans état et sans coût de simulation. Elles se placent comme toute pièce, sur n'importe quelle surface.

| Id | Pièce | Taille | Masse |
|---|---|---|---|
| 7331-7333 | Poutre 1 / 2 / 4 m | caisson 12 cm, platines | 6 / 12 / 24 kg |
| 7334 | Poutre renforcée 2 m | profilé en I 20 cm | 20 kg |
| 7335 / 7336 | Plaque 1 × 1 / 2 × 1 | 4 cm, boulons | 10 / 20 kg |
| 7337 | Cadre 1 × 1 | barres de 8 cm | 6 kg |
| 7338 | Jonction | nœud 5 départs | 3 kg |

- Une poutre sort perpendiculairement à sa surface de pose : verticale au sol, horizontale sur un mur.
- La collision est la boîte de la pièce, comme pour toute pièce du jeu. Le vide du cadre est donc visuel seulement.
- Ces pièces ne ferment pas les pièces étanches du jeu (les salles pressurisées) : seule la coque le fait.
- Elles acceptent la peinture du mod TF.

## Nouvelles planètes

Guide joueur : [docs/PLANETES.md](docs/PLANETES.md).

Sept planètes rejoignent le système 442 Otrera, en orbite autour de la même étoile. Le jeu les génère, les éclaire, les sauvegarde et les simule comme sa propre planète (terrain, flore, objets, atmosphère). On les rejoint en vaisseau (carte stellaire et saut en distorsion).

| Planète | Biome | Gravité | Atmosphère | Flore | Points d'intérêt |
|---|---|---|---|---|---|
| **TF Aridia** | désertique. Régions : dunes de sable dans les basses terres, mesas rouges en altitude, lacs salés | 0,9 g | respirable, ciel ambré, sans nuages | savanes d'**acacias**, arbres morts, herbe sèche | épaves, cargo écrasé, ruines et village en grès |
| **TF Petra** | rocheuse : roche grise couverte de cratères | 0,45 g | **aucune** (oxygène indispensable) | aucune | épaves, cargo écrasé, ruines et village en basalte |
| **TF Altis** | montagneuse : reliefs jusqu'à 8 km. Régions : vallées vertes, **neige au-dessus de 1 400 m**, lacs glaciaires | 1,1 g | respirable, ciel bleu froid | forêts de pins denses, bosquets de **bouleaux** (verts et d'automne) | ruines et village en granit, épaves |
| **TF Glacia** | glaciaire : calottes de glace, montagnes enneigées jusqu'à 9 km, lacs gelés | 1 g | respirable, ciel bleu pâle, nuages | sapins givrés et arbres morts dans les vallées, pas d'herbe | ruines et village pris dans la glace, épaves, cargo écrasé |
| **TF Nexora** | futuriste : plaines sombres et lacs turquoise sous un ciel violet | 0,85 g | respirable | arbres cyan cultivés, herbe turquoise | **cité abandonnée**, épaves, cargo écrasé |
| **TF Sylvara** | jungle : collines humides, berges boueuses, rivières et lacs tièdes | 1,05 g | respirable, ciel brumeux | **kapokiers géants** (30 m, contreforts), arbres de canopée, palmiers ; sous-bois de **fougères**, buissons et **fleurs** rouges, jaunes, violettes et blanches | temples et villages moussus, épaves, ambre |
| **TF Verdance** | tempérée, proche de la planète de départ, grands lacs | 0,95 g | respirable | forêts de conifères et, dans d'autres régions, bois de **chênes** et de **bouleaux** (verts et d'automne), herbe dense | ruines et village moussus, épaves, cargo écrasé |

- **Épaves** : sites de crash assemblés à partir des pièces écrasées du jeu (coque, propulseurs, sièges), recyclables comme sur la planète de départ.
- **Ruines d'une ancienne civilisation** : porte monumentale (piliers, linteau, marches), piliers debout et brisés, blocs tombés, obélisques. Taillées dans la pierre du jeu et teintées selon la planète, elles ont leurs propres collisions et ne se minent pas.
- **Cité abandonnée** (TF Nexora) : quartiers d'une civilisation avancée disparue, moins nombreux mais bien plus denses (v0.8.0) : grand gratte-ciel, 3 à 6 tours, 2 à 3 dômes, pylônes d'antenne, pistes d'atterrissage hexagonales, arches au-dessus de routes brisées, lampadaires, places, voitures volantes écrasées, kiosques et barrières. Intérieurs meublés : bacs à plantes, écrans muraux, canapés, porte d'ascenseur dans les halls ; bancs et plantes dans les dômes. Les bandes lumineuses cyan brillent encore la nuit. Bâtiments avec collisions, au look des coques du jeu, non minables.
- **Villages abandonnés** : un temple à colonnes au toit effondré, des maisons sans toit (porte, fenêtres, murs écroulés) et une tour de guet crénelée.
- **Cargos écrasés** : une grande épave d'environ 30 m (cockpit enfoui, soute tordue, moteurs arrachés, ailes, plaques de coque) entourée de propulseurs et de sièges, recyclable.
- **Arbres** : chêne, acacia et bouleau (et leurs variantes d'automne) sont des copies du sapin du jeu avec un nouveau modèle (tronc et feuillage tirés de l'atlas de feuilles du jeu). On les abat et on les recycle comme les sapins. Chaque type de forêt a son propre bruit de densité : les essences poussent dans des régions différentes de la planète.
- **Bâtiments visitables** : les maisons, la grande halle, la tour de guet, le temple, le hall des gratte-ciel et les dômes de la cité sont creux. On y entre par leurs portes : murs avec fenêtres, poutres de toit en bois, tables, bancs, étagères, foyer, échelle de la tour, comptoir et terminaux de la cité.
- **Coffres** : chaque bâtiment visitable contient un coffre (une caisse de butin du jeu, en bois, glace, cristal, alliage ou or selon le lieu), rempli de ce que le lieu et la planète offrent : minerais locaux, matériaux, clés, lingots, et parfois les **objets rares** des pièces avancées. Un bâtiment ne reçoit son coffre qu'une fois (la liste est gardée avec la sauvegarde, `tfa_sites.txt`).
- **Gisements** des nouveaux minerais, à miner au laser comme les rochers du jeu.
- **Champs d'astéroïdes** : TF Petra (large ceinture sombre) et TF Glacia (anneau clair) ont un anneau d'astéroïdes comme la planète de départ, avec ses ressources à miner.
- Rochers, minerais (fer, cuivre) et cristaux de verre comme sur la planète de départ.

### Nouveaux objets

| Objet | Où le trouver |
|---|---|
| **Minerai de titane** | gisements de TF Petra, TF Altis, TF Glacia |
| **Cristal de quartz** | gisements de TF Aridia et TF Nexora |
| **Cryonite** (cristal lumineux) | gisements de TF Glacia |
| **Ambre fossile** | gisements de TF Verdance et TF Sylvara |
| **Fragment d'artefact ancien** (rare) | coffres des temples, villages et halles |
| **Noyau quantique** (rare) | coffres des gratte-ciel et dômes de TF Nexora, stations Kepler et Orion |
| **Alliage nexorien** | coffres de la cité de TF Nexora, stations |
| **Matière exotique** (rare) | coffres de la Sonde Hadès, épaves d'Érèbe |
| **Météorite** | gisements sombres aux veines orange : TF Petra, Pyros, Mycélia, Cristallis ; épaves |
| **Minerai d'uranium** | gisements verdâtres de TF Aridia, TF Petra, Pyros |
| **Combustible nucléaire** (fluide) | centrifugeuse d'enrichissement |
| **Matière noire** (fluide) | collecteur de matière noire ; réservoirs des épaves d'Érèbe |

Ce sont de vrais objets du jeu : ils s'empilent, pèsent, vont dans l'inventaire et les coffres, tombent au sol, se sauvegardent, avec leur modèle 3D texturé (matériaux du jeu) et leur icône.

### Pièces avancées (onglet **TF Avancé**, dernière rangée)

| Pièce | Effet | Coût |
|---|---|---|
| **Réacteur quantique** | 12 000 W en continu, **sans carburant et sans fin** (trois générateurs à bois) | 2 noyaux quantiques, 6 alliages nexoriens, 8 titane, 2 fragments d'artefact, 10 cuivre |
| **Batterie à cryonite** | 8 fois la capacité d'une batterie, charge et décharge 3 fois plus rapides | 8 cryonite, 4 titane, 8 cuivre, 4 aluminium |
| **Moteur à matière exotique** | 78 000 N (dix propulseurs chimiques) pour 10 % de leurs ergols | 3 matière exotique, 1 noyau quantique, 12 titane, 6 alliages nexoriens |
| **Générateur de champ de force** | protège **tout le vaisseau** (et les vaisseaux qui lui sont attachés) : absorbe 150 points de dégâts (chocs, forces de marée des trous noirs). Consomme 2 000 W et 0,02 oxygène/s. Se recharge de 0,5 point/s. Vide, il casse : **5 min** avant de redémarrer. Bulle translucide visible quand il est actif | 1 noyau quantique, 6 cryonite, 4 alliages nexoriens, 6 titane, 8 cuivre |
| **Générateur de champ renforcé** | champ de force doré : **450 points**, recharge 1,5 point/s. Consomme 6 000 W et 0,05 oxygène/s | 3 noyaux quantiques, 15 cryonite, 12 alliages nexoriens, 20 titane, 20 cuivre |
| **Générateur de champ à matière noire** | champ de force violet : **1 200 points**, recharge 4 points/s. Consomme 10 000 W (un réacteur quantique entier, pas partagé entre deux réseaux) et 0,02 **matière noire**/s (pas d'oxygène). Recherche « Matière noire » | 6 noyaux quantiques, 6 matière exotique, 25 météorite, 20 alliages nexoriens, 30 titane |
| **Condenseur à cryonite** | fabrique 0,05 oxygène/s dans la conduite de sa prise de fluide (vide ou déjà d'oxygène), pour 1 500 W | 6 cryonite, 3 titane, 6 cuivre, 4 verre |
| **Gyroscope quantique** | le couple de 5 gyroscopes, commandé comme le gyroscope du jeu | 1 noyau quantique, 4 alliages nexoriens, 4 titane, 4 cuivre |

**Prises** du champ de force et du condenseur : 0 signal (charge en %, -1 éteint, négatif pendant le redémarrage ; production du condenseur en millièmes d'unité/s), 1 et 2 puissance (côtés), 3 fluide (arrière, oxygène). Le réacteur alimente ses deux prises (puissance partagée si elles vont à deux réseaux).

**Design (v0.8.0)** : batterie, batterie à cryonite, réacteur, champ de force et condenseur ont un nouveau modèle : carter gris anguleux, bandes blanches sur les arêtes, grilles noires et **ouïes rouges lumineuses** (cyan pour la cryonite, violet pour le quantique). Les moteurs du mod reçoivent le même habillage autour de la tuyère, et leurs prises sont recalées sur le modèle.

L'ordinateur de bord affiche les réacteurs (puissance, « sans carburant »).

Comment ça marche : chaque planète est une copie de la planète de la démo (442 Otrera b), créée avant que le jeu ne charge ses planètes, puis remodelée : bruit du terrain, cratères, matériaux du sol, atmosphère, configuration de la flore et des points d'intérêt. Les autres planètes présentes dans les fichiers de la démo appartiennent au jeu complet : le mod ne les touche pas.

**Limites** :
- **Multijoueur** : chaque joueur doit avoir le mod et le même réglage `NewPlanets`. Les planètes du mod n'ont pas d'objet réseau FishNet (seule la scène du jeu peut en créer) : depuis la v0.8.0, les arbres coupés et rochers minés y sont écrits, marqués, dans l'état réseau de la planète de départ, que tous les clients reçoivent, puis relus par chaque client pour la bonne planète.
- **Sauvegardes** : ne désinstalle pas le mod (et ne coupe pas `NewPlanets`) pendant qu'un joueur ou un vaisseau se trouve sur l'une de ces planètes.
- L'éclairage précalculé et les distances d'affichage sont ceux de la planète de départ.

## Recherche

Les pièces du mod se débloquent à la **table de recherche** : une branche de 12 techs rattachées aux techs du jeu (construction métallique après Iron Panels, propulsion avancée après Chemical Propulsion…), payées en ressources. Le mod place ses techs dans les cases libres de l'arbre sans déplacer celles du jeu ni des autres mods. Détail : [wiki, Recherche](docs/wiki/Recherche.md). `ResearchUnlocks = false` : tout est disponible d'emblée.

## Constructions en bois, verre et fer (onglet TF Construction)

19 pièces posées qui complètent la coque du jeu : poteau, plancher en lattes, cloison en planches, rambarde, treillis et étagère en bois ; vitre, baie vitrée 2 × 2, dalle de verre, garde-corps vitré, verrière, cloison vitrée ; caillebotis, garde-corps 1 et 2 m, treillis métallique, colonne rivetée, gousset, tôle perforée. Le bois et le fer portent les textures de la coque du jeu (son tableau de textures, choisi par sommet comme la coque), le verre son verre.

**Pièces posées sur des pièces posées** : le jeu n'accroche une pièce posée qu'à la coque. Le mod ajoute le cas d'une pièce visée : sur sa face du dessus, la nouvelle pièce s'y pose ; sur un côté, elle se met contre lui en restant droite, base au même niveau (grille de 25 cm comptée depuis la pièce visée). Ça vaut pour toutes les pièces posées : constructions, meubles et machines dans un module, panneaux solaires sur un toit, modules entre eux.

## Stations spatiales (onglet TF Station)

| Module | Rôle |
|---|---|
| Module d'habitation | 4 × 6 m : couchettes, table, casiers, hublots, deux portes |
| Couloir pressurisé | 2,4 × 4 m entre deux modules |
| Hub de connexion | 4 × 4 m, une porte sur chaque face |
| Module de stockage | étagères et terminal d'inventaire (10 emplacements) |
| Module d'amarrage | sas à collier magnétique : un vaisseau équipé du même module s'y amarre |
| Support de panneaux solaires | jusqu'à 3 000 W face à l'étoile, rien la nuit ou à l'ombre d'une planète ; se pose sur le toit d'un module |
| Laboratoire | une vraie table de recherche dans la pièce, paillasses et flacons |
| Infirmerie de clonage | une vraie capsule de clonage (point de réapparition), deux lits médicaux |
| Module de support vital | usine à oxygène : 2 500 W, 0,1 oxygène/s dans la conduite de sa prise d'air |
| Observatoire | plafond vitré, grandes baies sur trois côtés, une vraie carte stellaire sur un pupitre bas |
| Sas de dépressurisation | deux portes étanches commandées par un levier, jamais ouvertes ensemble ; sans air quand la porte extérieure est ouverte |
| Couloirs d'angle, en T, en U (demi-tour), long, cylindrique | coude à 90° et T de 4,4 m (deux modules y tiennent côte à côte), demi-tour à 180° de 8,6 m, couloir vitré de 8 m, tube rond |
| Cage d'escalier | 4 × 8 m, deux étages : escalier à 31°, palier à 3 m, porte en bas et porte en haut |
| Dôme d'habitation | salle ronde de 6 m sous une coupole |
| Siège de commande à leviers | le poste de pilotage avancé du jeu avec six leviers d'accoudoir qui suivent ses six signaux |

- **Pose libre** : un module se pose seul dans l'espace (ou au sol), en visant le vide : il devient une nouvelle station (sur une dalle de coque cachée sous son plancher), au même coût.
- **Interactions** : interrupteur de lumière dans chaque module, couchette de réapparition dans l'habitation, lits médicaux dans l'infirmerie, consoles (wiki) dans le hub, le dôme, le support vital et le laboratoire.
- **Assemblage** : un module posé contre le mur d'un autre se met en face de la porte de ce mur la plus proche du viseur, à la hauteur de son étage, tourné pour qu'une de ses portes y fasse face.
- **Habitation utilisable** : la couchette du bas est le lit du jeu (dormir), les casiers sont un rangement de 10 emplacements, la console ouvre le wiki du mod (comme celle du hub).
- **Murs** : un objet du jeu se fixe sur le mur d'un module comme sur la coque ; les constructions du mod s'y adossent debout.
- **Peinture et matériau** : avec le pistolet du mod TF (v0.12.0 ou plus), extérieur et intérieur ont chacun leur couleur et leur matériau (métal, bois, métal sombre, blanc, verre) ; on peint le côté visé, ou les deux.
- **Machines du jeu** : le laboratoire, l'infirmerie et l'observatoire gardent la machine du jeu dont ils sont faits (table de recherche, capsule de clonage, carte stellaire), qui fonctionne comme d'habitude.
- **On entre dedans** : chaque module a ses propres collisions (sol, plafond, murs autour des portes), via les « colliders dynamiques » du jeu, et une boîte de placement réduite au plancher pour y poser meubles et machines.
- **Air** : un module est pressurisé dès qu'un module de la station est relié à une conduite d'oxygène ; l'oxygène respiré est pris dans la conduite.
- **Amarrage** : le collier est placé au point d'amarrage fixe du jeu (comme une porte d'amarrage) ; deux modules collier contre collier s'attirent et s'amarrent.

## Matière noire et portail

- **Collecteur de matière noire** : 0,004 unité/s pour 8 000 W ; ×5 près d'un trou noir, ×3 dans Érèbe. **Réservoir** de matière noire. **Réacteur à matière noire** : 150 000 W pour 0,002 unité/s. **Moteur à matière noire** : 624 000 N.
- **Générateur de portail** : un signal sur sa prise de commande ; 10 s de charge à 8 000 W (plus lente si le courant manque), 3 matière noire, puis un portail de 18 m de rayon s'ouvre 45 m devant lui, fixe dans l'espace, 30 s. Le vaisseau qui le traverse ressort près de la **cible de son ordinateur de visée ou de sa carte stellaire** (planète : ~20 km d'altitude ; balise : 300 m). Le saut passe par l'espace de distorsion du jeu.
- **Table de cartographie** : relief 3D du terrain sur 300 m (fonction d'altitude du jeu sur l'hôte), repères des gisements, coffres, bâtiments et vaisseaux, et la fiche de la planète.

## Énergie nucléaire

Le palier entre les artefacts et la matière noire (il reprend les valeurs qu'avait la matière noire ; la matière noire a été relevée pour rester la plus puissante).
- **Minerai d'uranium** sur TF Aridia, TF Petra et Pyros.
- **Centrifugeuse d'enrichissement** (clone du distillateur d'éthanol du jeu, nouveau modèle : trois rotors, trémie, pupitre) : minerai d'uranium dans sa trappe (main ou ligne d'objets), 6 000 W, 0,05 minerai/s, 0,2 unité de **combustible nucléaire** par minerai dans la conduite de sa prise 0.
- **Réacteur nucléaire** : 40 000 W pour 0,002 unité/s de combustible nucléaire. **Réservoir de combustible nucléaire**.
- **Propulseur nucléaire** (93 600 N) et **moteur nucléaire lourd** (234 000 N), voir le pack de propulsion.
- Tech de recherche **Énergie nucléaire** (après Technologie des artefacts) ; la tech Matière noire vient maintenant après elle.

## Pirates (avec le mod Arsenal)

Des vaisseaux pirates, armés des armes de vaisseau d'Arsenal, attaquent de temps en temps un vaisseau de joueur qui vole dans l'espace (en moyenne toutes les 12 min passées dans l'espace, un groupe à la fois, à ~2 km). Quatre niveaux tirés au hasard : **éclaireur** (facile, 1 200 points, 1 mitrailleuse, en groupe de 2-3), **corsaire** (2 500 points, 2 mitrailleuses et un lance-missiles), **pillard** (difficile, 4 200 points, laser en plus), **croiseur** (très difficile, 7 000 points, canons à plasma, lance-missiles, défense rapprochée). Leurs armes à tir direct tirent par rafales suivies d'un rechargement (de 1,2 s / 4 s pour l'éclaireur à 2,5 s / 3 s pour le croiseur), et les plus faciles visent moins bien. Chaque modèle a son carénage détaillé (nez, verrière, ailes en flèche, dérives, nacelles moteur), sans collision : les tirs percent la coque de fer dessous. Leur pilote (côté hôte) s'approche, tourne autour de sa cible à sa distance de combat, la vise et tire quand il est aligné ; il garde de l'altitude près d'une planète. Les armes d'Arsenal usent leur coque (un corsaire tombe en ~4 missiles) ; détruit, un pirate explose et laisse une capsule de butin. Le champ de force les encaisse comme toute arme d'Arsenal. Sans Arsenal, pas de pirates. **Zone sûre** de 2 000 km autour de 442 Otrera b (`PirateSafeRadiusKm`) : pas d'attaque, combat rompu. Dans Érèbe, la planète artificielle **Bastion Écarlate** (115, couverte de forteresses : tours, canons, bunkers, remparts aux feux rouges) et **trois bases pirates** armées (20 000 points, canons, salves de missiles autoguidés, chasseurs lancés depuis leur hangar), visibles sur la carte stellaire ; autour du Bastion les attaques sont 4 fois plus fréquentes. Détail : [wiki, Pirates](docs/wiki/Pirates.md).

## Radars et générateur d'invisibilité (v0.17)

Trois **radars** (1 000, 5 000 et 20 000 km, de 400 à 3 200 W) affichent sur un écran rond les pirates autour du vaisseau : leurs vaisseaux, leurs bases et les **raids en approche**, désormais repérés à 25 000 km et qui foncent à 250 km/s avant d'attaquer (le radar de l'espace profond donne environ 80 s d'avance, avec une alerte à l'écran). Le **générateur d'invisibilité** (6 000 W) rend le vaisseau difficile à trouver et à viser : raids trois fois plus rares et qui peuvent perdre sa trace, pirates qui ne le voient qu'à moins de 2,5 km, tirent de plus près et visent bien plus mal. Tech **Guerre électronique**. Les contacts sont calculés par l'hôte et envoyés à chaque joueur. Détail : [wiki, Pirates](docs/wiki/Pirates.md#radars).

**Optimisations (v0.17)** : plus de recherche de services ni de liste de caméras à chaque image (trous noirs, galaxies, portail, sièges, sas), gravité des trous noirs calculée une fois par pas physique et par espace, bulle de champ de force redessinée seulement quand elle est visible (et moins souvent de loin), table de cartographie et missiles des bases sans parcours de toute la scène, couple des moteurs vectoriels sauté pour les vaisseaux qui n'en ont pas, lectures d'Arsenal mises en cache (compatibles avec plusieurs versions d'Arsenal).

## Vaisseaux qui tournent sans à-coups (v0.22)

- **Rotations saccadées corrigées** : avec la synchro verticale sur un écran rapide (165 Hz) et le jeu vers 60 fps, les images restaient à l'écran 2 puis 3 rafraîchissements (12 puis 18 ms) : un vaisseau qui tourne régulièrement avançait par petits puis grands pas. Quand le jeu ne suit pas l'écran, chaque image attend maintenant le même nombre de rafraîchissements (55 fps réguliers sur 165 Hz). Toutes les 45 s il vérifie si le jeu est redevenu assez rapide. Jamais sous 45 fps, rien sans synchro verticale ; réglage `SteadyFrameRate`.

## Tablette schématique, 3e personne, portail moins gourmand, nouveaux radars (v0.21)

- **Tablette schématique et vue à la 3e personne** (venues du mod TF) : sans le mod TF, TF Advanced les fournit. Même tablette (objet 7201), mêmes schématiques (`UserData/TF/schematics`), touche **V** en pilotant. La tablette se prend au râtelier à outils. Les touches des sièges KeySeat sont gardées et recopiées par les schématiques.
- **Portail** : il demande 8 000 W au lieu de 20 000 (un générateur infini en donne 20 000 : il ne s'arrêtait plus de charger dès qu'une autre machine tirait du courant) et 3 matières noires au lieu de 5. Avec moins de courant, il charge plus lentement au lieu de s'arrêter. Vos réglages restés aux anciennes valeurs passent aux nouvelles.
- **Radars refaits** : antenne barre tournante pour la courte portée, parabole à trépied et contrepoids pour la longue portée, panneau à réseau phasé lumineux pour l'espace profond ; mât haubané, palier lumineux, visière d'écran, voyants.

## Pirates de plus en plus durs, nouveau portail (v0.20)

**Le danger monte avec la distance** : près de la planète de départ, seuls des éclaireurs, un ou deux à la fois, et plus rarement. Un niveau de danger de plus tous les 5 millions de km (réglage `PirateDangerStepKm`) : corsaires et frelons vers TF Aridia, pillards et lanciers vers TF Verdance et TF Glacia, croiseurs et torpilleurs escortés au-delà de TF Nexora, et les cuirassés dans la galaxie d'Érèbe.

**Quatre nouveaux pirates**, armés des armes d'Arsenal qu'ils n'utilisaient pas encore :
- **Frelon** (niveau 2) : jaune rayé de noir, le plus rapide, lâche 9 roquettes d'affilée ;
- **Lancier** (niveau 3) : canon électrique et deux canons ioniques qui brouillent vos armes, il tire de loin ;
- **Torpilleur** (niveau 4) : deux lance-torpilles, une batterie sol-air et une défense rapprochée ;
- **Cuirassé** (niveau 5, Érèbe seulement) : 14 000 points de coque, 14 armes, tour de commandement.

Chacun a son propre carénage. Les bases pirates sortent maintenant aussi des frelons.

**Portail dimensionnel retravaillé** : le générateur a un anneau en sections aux joints lumineux, deux anneaux de cardan qui tournent comme un gyroscope autour d'un cristal flottant, de plus en plus vite pendant la charge. Le portail s'ouvre en étapes : seize segments arrivent et se verrouillent dans un éclair, l'horizon s'ouvre avec deux spirales qui tournent en sens contraire, des ondes, des étincelles aspirées, un anneau de runes et une lumière violette. À la fermeture, il s'effondre dans un éclair, même après le passage du vaisseau.

## Español, traduction complète (v0.19)

Le mod parle maintenant **espagnol** : réglage `Language = "es"` (ou `auto` avec le jeu en espagnol). Pièces, machines, recherche, écrans de l'ordinateur de bord, radars, planètes, table de cartographie et **wiki dans le jeu** (27 pages) sont traduits. L'équipement TF (lampes, caméra et tablette, capsule, radio, râtelier, objets infinis), resté en français seulement depuis le mod TF, est maintenant en français, anglais et espagnol.

**Lampes** : les réglages `[TFAdvancedObjects]` n'étaient jamais chargés depuis la v0.18. Chaque lampe levait une erreur à chaque image (des milliers par minute, moins d'images/s), et la tablette, la radio et la touche d'utilisation ne lisaient pas leurs réglages. C'est corrigé.

## Équipement TF et stabilité (v0.18)

Les **luminaires, la caméra et sa tablette, la capsule de téléportation, la radio, le râtelier à outils et les objets infinis** du mod TF sont maintenant dans TF Advanced (onglet **TF Équipement**, mêmes numéros de pièces : ce qui est construit reste). Les objets infinis sont devenus **extrêmement chers**. Le mod TF 0.15 les lui cède. Détail : [wiki, Équipement TF](docs/wiki/Equipement-TF.md).

**Vaisseau qui tremble** : le gyroscope quantique (couple ×5) faisait osciller le vaisseau à chaque pas physique dès qu'il tournait, et après (le régulateur des gyroscopes du jeu lui demandait 5 fois trop). Il connaît maintenant sa force : plus d'oscillation, et de 47-55 à 65-70 images/s en virage sur un gros vaisseau. Aussi : plus de milliers de messages « NetworkManager not found » au menu (TF Advanced et TF), bulle de champ de force et table de cartographie qui ne parcourent plus tout le vaisseau ou toute la scène toutes les 2-3 s.

## Galaxie d'Érèbe

Une seconde étoile (copie de l'étoile du jeu, une géante orange) à 0,18 année-lumière, avec **Pyros** (volcanique), **Thalassa** (océanique), **Mycélia** (fongique, champignons géants) et **Cristallis** (cristalline, anneau), le trou noir **Œil d'Érèbe** et trois épaves avec de la matière noire. Le jeu n'attend qu'une étoile : l'éclairage, le jour et la nuit, la carte stellaire et le suivi d'étoile prennent l'étoile la plus proche ; les deux restent visibles. La carte stellaire n'affiche que le système où l'on se trouve. Réglage `Erebos`.

## Stations spatiales abandonnées

Trois stations abandonnées sont créées dans le monde quand l'hôte charge une partie (une seule fois par monde). Ce sont de vrais vaisseaux du jeu : on y entre, on les démonte, on récupère leurs pièces.

| Station | Emplacement |
|---|---|
| **Station Kepler (abandonnée)** | en orbite lointaine de TF Verdance |
| **Avant-poste Orion (abandonné)** | près de TF Aridia |
| **Sonde Hadès (trou noir)** | à environ 930 km du trou noir |

- **Localisation** : chaque station a une **balise** à son nom. Elle apparaît sur la carte stellaire comme destination de saut, comme les balises posées par les joueurs.
- **Targeting Computer** : 4 filtres en haut de la liste des cibles : Tout, Planètes, Stations, Balises.
- **Contenu** : modules reliés par des couloirs, sas d'entrée, hublots, coque percée par endroits, propulseurs morts, couchettes, réservoirs d'oxygène et d'éthanol, coffres et caisses de butin (dont des objets rares : noyaux quantiques, alliage, matière exotique dans la Sonde Hadès).
- **Peinture** : coque claire avec une bande de couleur par station (orange Kepler, bleu Orion, rouge sur coque sombre pour Hadès), intérieur clair. Les stations déjà créées dans un monde gardent leur ancien aspect ; une station dont la balise a disparu est recréée.
- **Abri** : à moins de 3 km d'une station, la gravité du trou noir ne s'applique pas. On peut donc s'amarrer à la Sonde Hadès.
- Réglage : `AbandonedStations` (section `[TFAdvanced]`).

## Trous noirs

Deux trous noirs :
- le **trou noir de Petra**, à environ 3 000 km de **TF Petra**, visible dans le ciel sans atmosphère de Petra ;
- le **trou noir de Glacia**, deux fois et demie plus massif, à environ 4 100 km de **TF Glacia**.

Ils suivent les relations de Schwarzschild dans les unités du jeu. Le jeu réduit les astres (1 g à 320 km de rayon), donc la vitesse de la lumière est réduite d'autant. Elle vaut ici 10 km/s : c'est la vitesse de libération à l'horizon.

| Grandeur | Petra | Glacia |
|---|---|---|
| Horizon des événements r_s | 10 km | 25 km |
| Ombre (capture des photons, 2,6 r_s) | 26 km | 65 km |
| Disque d'accrétion | 3 à 40 r_s | 3 à 40 r_s |
| Gravité GM/r² à 1 000 km | 0,5 m/s² | 1,25 m/s² |
| Gravité à l'horizon | 5 000 m/s² | 2 000 m/s² |
| Zone de marée | sous 50 km | sous 68 km |
| Arrêt de distorsion | 600 km | 1 500 km |
| Dilatation du temps | √(1 − r_s/r) | √(1 − r_s/r) |

Comme en vrai, les forces de marée croissent avec la masse moins vite que l'horizon (la limite varie comme la racine cubique de la masse). Le trou noir le plus massif est donc plus doux à son horizon.

Effets en jeu (sur l'hôte) :
- **Attraction** : une vraie accélération en 1/r² pour les vaisseaux et les joueurs, en orbite comme dans l'espace profond. Le jeu, lui, n'a pas de gravité hors du sol des planètes.
- **Forces de marée** (zone du tableau) : le vaisseau perd des pièces, de plus en plus vite en approchant.
- **Horizon** : un vaisseau qui le franchit est entièrement détruit, un joueur meurt.
- **Distorsion** : un saut s'arrête avant le trou noir (distance du tableau), comme devant une étoile.
- **Ordinateur de bord** : distance, attraction et dilatation du temps du trou noir le plus proche (FLIGHT), alertes de proximité et de marée (DIAGNOSTICS).

Aspect :
- une ombre noire ;
- un disque d'accrétion procédural (bleu-blanc au bord intérieur, rouge à l'extérieur) qui tourne ;
- un effet Doppler : le côté qui vient vers toi est plus brillant ;
- un anneau de photons, une image courbée du disque autour de l'ombre et un halo.

Une vraie déviation de la lumière (lentille gravitationnelle) demanderait un shader que le jeu n'inclut pas, et Unity ne compile pas de shader en cours de partie : l'image en reproduit donc l'apparence avec des surfaces.

Réglage : `BlackHole` (section `[TFAdvanced]`, les deux trous noirs), identique pour tous les joueurs.

## Configuration

`UserData/MelonPreferences.cfg`, section `[TFAdvanced]` :

| Clé | Défaut | Rôle |
|---|---|---|
| `Language` | `auto` | langue des textes du mod : `auto` (celle du jeu : français ou espagnol si le jeu est en français ou en espagnol, anglais sinon), `fr`, `en` ou `es` ; noms et descriptions des pièces au prochain démarrage, wiki tout de suite |
| `ComputerRefreshRate` | `4` | rafraîchissements des écrans par seconde (1-20) |
| `ComputerViewDistance` | `40` | au-delà (m), un écran ne se rafraîchit plus |
| `TrafficRange` | `3000` | portée de la liste de trafic (m) |
| `CollisionWarningSeconds` / `CollisionWarningDistance` | `12` / `30` | seuils d'alerte de rapprochement |
| `DescentWarningSpeed` / `DescentWarningAltitude` | `20` / `400` | seuils d'alerte de descente |
| `SpinWarningRate` | `60` | seuil d'alerte de rotation (°/s) |
| `NewPlanets` | `true` | les 6 nouvelles planètes |
| `BlackHole` | `true` | les trous noirs près de TF Petra et de TF Glacia |
| `AbandonedStations` | `true` | crée les 3 stations abandonnées dans un monde qui ne les a pas encore |
| `TerrainChunkBuffer` | `1.5` | place pour les chunks de terrain sur la carte graphique, fois celle du jeu ; au-dessus de 1, les hautes montagnes ne laissent plus de trous dans le sol (environ 38 Mo de mémoire vidéo par 0,1 et par planète proche) |
| `ReactorWatts` (section `[TFAdvancedPower]`) | `12000` | puissance d'un réacteur quantique (W) |
| `NuclearReactorWatts` / `NuclearReactorBurnPerSecond` (`[TFAdvancedPower]`) | `40000` / `0.002` | réacteur nucléaire |
| `CentrifugeWatts` / `CentrifugeOrePerSecond` / `CentrifugeFuelPerOre` (`[TFAdvancedPower]`) | `6000` / `0.05` / `0.2` | centrifugeuse d'enrichissement |
| `DarkReactorWatts` / `DarkReactorBurnPerSecond` (`[TFAdvancedPower]`) | `150000` / `0.002` | réacteur à matière noire (une ancienne valeur de 40 000 passe à 150 000) |
| `ShieldWatts` / `ShieldOxygenPerSecond` / `ShieldCapacity` / `ShieldRegenPerSecond` / `ShieldCooldownSeconds` (`[TFAdvancedPower]`) | `2000` / `0.02` / `150` / `0.5` / `300` | champ de force de base ; les autres modèles en sont des multiples (voir le wiki, Machines) |
| `EngineSeatControl` | `true` | moteurs nucléaires et à matière noire commandés par le siège de pilotage (`false` : ils poussent dès qu'ils sont alimentés) |
| `PirateSafeRadiusKm` | `2000` | zone sûre sans pirates autour de 442 Otrera b (km, 0 : aucune) |
| `Pirates` / `PirateIntervalMinutes` | `true` / `12` | pirates (avec Arsenal) et temps moyen dans l'espace entre deux attaques (min) |
| `PirateRaidStartKm` / `PirateApproachKmPerSecond` | `25000` / `250` | distance où un raid pirate est repéré et sa vitesse d'approche |
| `RadarAlarm` | `true` | alerte à l'écran quand un radar près de vous voit un raid arriver |
| `SteadyFrameRate` | `true` | synchro verticale activée et jeu plus lent que l'écran : même nombre de rafraîchissements pour chaque image, pour des rotations sans à-coups (jamais sous 45 fps) |
| `RadarWatts` / `CloakWatts` (`[TFAdvancedPower]`) | `400` / `6000` | courant du radar courte portée (×3 longue portée, ×8 espace profond) et du générateur d'invisibilité |
| `CondenserWatts` / `CondenserOxygenPerSecond` (`[TFAdvancedPower]`) | `1500` / `0.05` | condenseur à cryonite |
| `QuantumGyroFactor` (`[TFAdvancedPower]`) | `5` | couple du gyroscope quantique, en gyroscopes |
| `SelfTest` | `false` | développement : construit les pièces au menu, en écrit la description et des rendus PNG |
| `DevWorld` / `DevQuitSeconds` / `DevVisitPlanets` / `DevVisitPlanetId` / `DevVisitStations` / `DevVisitBlackHole` / `DevPlanetsNearby` | vides / `false` | développement : charge automatiquement une **copie** de monde, visite les planètes ou le trou noir, prend des captures dans `UserData/TFAdvanced/dev`, puis quitte |

## Compilation

Prérequis : .NET SDK 8, le jeu installé avec MelonLoader et SDModKit.

```powershell
dotnet build src/StellarDriveDemoTFAdvanced/StellarDriveDemoTFAdvanced.csproj -c Release
```

- Les références au jeu sont lues dans `G:\SteamLibrary\steamapps\common\StellarDrive Demo`. Pour un autre chemin, créer `Directory.Build.props.user` avec `<GamePath>`, ou passer `-p:GamePath=...`.
- Le DLL est copié automatiquement dans le dossier `Mods/` du jeu (`-p:DeployToGame=false` pour l'éviter).
- `tools/package.ps1` produit `dist/StellarDrive-demo-TF-advanced.zip` au format StellarModManager.

Aucun fichier du jeu n'est modifié et aucun DLL du jeu n'est versionné.

## Ajouter une pièce

1. Choisir un id libre (7307-7310 pour les écrans, 7315-7320 pour les moteurs, 7323-7330 pour l'énergie, 7339+ pour la structure) et un donneur : `SignalDisplay` pour une pièce vierge, `Thruster` pour un moteur.
   Structure : ajouter une entrée à `StructureParts.All` avec sa fonction de maillage.
2. Écrans : appeler `RegisterSmall` (ou copier `ConfigureWallScreen`) dans `ShipComputer/DashboardParts.cs`. Le contenu de l'écran est choisi par `layout`.
3. Moteurs : ajouter une ligne à `EngineCatalog.All`. La poussée, la consommation, le couple et la montée en régime suivent automatiquement.
4. Compiler, activer `SelfTest` et vérifier la pièce dans `MelonLoader/Latest.log`.

## Tests

| Test | Résultat |
|---|---|
| Compilation Release | ✅ 0 erreur, 0 avertissement |
| Chargement par MelonLoader | ✅ 6 patchs appliqués, aucune erreur |
| Construction des 20 pièces par SDModKit (auto-test au menu, v0.3.0) | ✅ ids, libellés, masses, tailles, états ; prises : écrans 0, moteurs 4 fluides, batterie 0-1 puissance + 2 signal, disjoncteur 0-1 puissance + 2-3 signal ; 8 patchs appliqués |
| Écran hors vaisseau | ✅ canevas créé, police du jeu (Anta) et palette du jeu |
| v0.3.1 : composant visuel de chaque pièce (vignettes, aperçu de pose, écran du build tool) | ✅ 20/20 (son absence sur la structure cassait le panneau du build tool en v0.3.0) |
| v0.3.1 : rendu des pièces | ✅ rendus PNG hors champ : matériau `Custom/PlanetObject` du jeu, panneaux dessinés de l'atlas LogicGates, comparés au coffre du jeu |
| v0.4.0 : planètes (monde chargé et visité automatiquement) | ✅ 4 planètes enregistrées par le jeu, rendues depuis l'espace et au sol. Biomes vérifiés sur captures : dunes orange d'Aridia, roche grise sans air de Petra, forêts de pins d'Altis, lacs et forêts de Verdance. Ruines (5 sites) et épaves (4 sites) chargées autour du joueur sur Aridia. Aucune exception. |
| v0.4.0 : trou noir | ✅ visible depuis Petra (ombre, disque incliné, halo), aucune exception ; ⏳ attraction, marée, horizon et arrêt de distorsion **à tester en vol** |
| v0.5.0 : écrans de l'ordinateur de bord (rendu hors champ, plus gros vaisseau du monde) | ✅ plus aucun texte superposé : colonne de valeurs alignée, FLIGHT compacté, taille minimale du texte relevée |
| v0.5.0 : stations abandonnées | ✅ 3 stations créées (1 077, 670 et 84 pièces de coque), balises vues par le client, joueur placé dans chacune |
| v0.5.0 : arbres, régions, villages, cargos | ✅ rendu des 5 arbres, des bâtiments et du cargo. En jeu, chaque planète visitée séparément : villages (temple, maisons, tour) et cargos chargés sur Aridia, Altis et Verdance, feuillus visibles dans les forêts de Verdance. Bug corrigé : le jeu numérote les configurations de flore à collisions parmi elles seules, la nouvelle forêt est donc placée avant l'herbe. ⚠️ Le téléport du test automatique déclenche un bug du jeu (`Space id … does not exist`), déjà connu et sans lien avec ces ajouts. ⏳ Régions du sol (mesas, neige) pas encore vues en capture |
| v0.6.0 : TF Glacia, TF Nexora, 2e trou noir | ✅ 6 planètes et 2 trous noirs créés, aucune erreur du mod. Rendu des bâtiments de la cité (gratte-ciel, dôme, pylône, piste, arche). En jeu : sol de glace et ruines de glace sur Glacia, cité chargée sur Nexora (2 gratte-ciel, 4 tours, 3 dômes, pylônes, piste, arche) avec bandes lumineuses visibles de nuit. ⏳ Les deux trous noirs ne sont pas encore vus en capture (la caméra de test était mal orientée). Attraction, marée et arrêt de distorsion du 2e trou noir **à tester en vol** |
| v0.7.0 : corrections | ✅ arbres coupés : ils disparaissent (serveur et client) ; physique de l'hôte : plus aucun blocage « Space id … does not exist » sur un tour des 7 planètes ; textes des écrans : un libellé long passe à la ligne au lieu d'être recouvert par sa valeur ; tampon de terrain agrandi (480 chunks) |
| v0.7.0 : carte stellaire | ✅ les 7 planètes nommées et colorées, trous noirs et stations marqués (rendu de l'hologramme du jeu) |
| v0.7.0 : objets, gisements, coffres | ✅ 8 objets ajoutés à la liste du jeu, modèles texturés ; gisements chargés ; 9 coffres posés dans un village de Verdance et de Sylvara, remplis (2 à 4 objets), stables au sol |
| v0.7.0 : pièces avancées | ✅ réacteur, batterie à cryonite et moteur exotique construits (auto-test : prises, état, 78 000 N) ; ⏳ production réelle du réacteur **à tester en jeu** |
| v0.7.0 : jungle, stations, astéroïdes | ✅ Sylvara : arbres et plantes rendus, 81 FPS ; stations recréées avec peinture et équipements ; anneaux de Petra et Glacia enregistrés et visibles depuis Petra ; ⏳ minage dans les nouveaux anneaux **à tester** |
| v0.7.0 : performance (tour des 7 planètes) | ✅ 50 à 110 FPS. Corrigé en route : des couches d'arbres séparées se chevauchaient et se renversaient en chaîne (3 FPS) ; chaque planète a maintenant une seule couche d'arbres mêlant les essences |
| v0.7.1 : monde bloqué au chargement | ✅ un vaisseau au bord extérieur d'un anneau d'astéroïdes faisait lire la densité de l'anneau hors de son tableau ; lecture bornée, largeurs d'anneau du jeu rétablies ; le monde se recharge |
| v0.8.0 : champ de force, réacteur, condenseur (vaisseau de test câblé lâché de 40 m) | ✅ réacteur 12 000 W sur ses deux prises ; le champ encaisse le crash (charge 100 → 82 %, 30 pièces sur 30 gardées), se recharge à 0,5/s, consomme 0,02 O₂/s ; le condenseur remplit un réservoir vide à 0,05/s. ⏳ Rupture et redémarrage après 5 min, bulle visible **à voir en jeu** |
| v0.8.0 : arbres en multijoueur | ✅ sur l'hôte, l'état d'une planète du mod est porté par celui de la planète de départ (aller-retour du marquage vérifié). ⏳ **Pas testé avec un vrai client** |
| v0.8.0 : cité de Nexora | ✅ 8 grands gratte-ciel, 14 tours, 8 dômes, 28 lampadaires, places, voitures, kiosques, coffres posés ; 81,8 FPS |
| v0.8.0 : Avant-poste Orion | ✅ collisions présentes (sondes dans Orion et Kepler). Le décor traversé et les coffres inutilisables venaient très probablement de la physique de l'hôte bloquée par le bug d'anneau (corrigé en v0.7.1) |
| v0.8.0 : onglet unique, filtres du Targeting Computer, carte stellaire, gyroscope quantique | ✅ compilés et chargés sans erreur. ⏳ Rendu de l'onglet fusionné, filtres et couple du gyroscope **à vérifier en jeu**. Les noms de la carte stellaire tournent maintenant seulement autour de l'axe de l'hologramme et ne sont plus dupliqués ; le bug « planètes dans tous les sens » n'a pas pu être reproduit |
| v0.9.0 : prises du moteur exotique | ✅ cause trouvée en jeu : le jeu replace les prises dans le modèle déjà agrandi (agrandies deux fois) ; le contenu du modèle est agrandi, plus le conteneur ; prises sur le socle (vérifié en photo) |
| v0.9.0 : portail | ✅ vaisseau de test : charge 10 s, 5 matière noire brûlée, portail ouvert (photo), vaisseau poussé à travers : en orbite de TF Verdance à 17,7 km, puis de Thalassa dans Érèbe |
| v0.9.0 : matière noire | ✅ collecteur 0,004/s, réacteur à matière noire en marche, réservoirs |
| v0.9.0 : amarrage | ✅ deux modules d'amarrage collier contre collier s'attirent et s'amarrent (états « docked » des deux côtés) ; ⏳ avec une porte d'amarrage du jeu **à vérifier** |
| v0.9.0 : modules, constructions, table | ✅ rendus en jeu (photos) : habitation, couloir, hub, stockage, panneaux solaires, 19 pièces bois/verre/fer, table avec relief et repères ; ⏳ air des modules, production solaire et ouverture du terminal de stockage **à vérifier en jeu** |
| v0.9.0 : Érèbe | ✅ étoile, 4 planètes, trou noir, 3 épaves créés ; Pyros, Thalassa, Mycélia visitées (éclairées par Érèbe le jour, nuit de l'autre côté) ; ⏳ Cristallis non visitée |
| v0.9.0 : synchronisation des planètes du mod | ✅ corrigé : la planète de départ réagissait elle-même aux objets marqués d'une planète du mod (erreur réseau « Index was outside the bounds ») |
| v0.9.0 : pièces posées sur des pièces posées | ✅ test en jeu par l'outil d'accrochage du jeu : couloir, habitation et stockage contre un hub (portes alignées, photo), cloison contre cloison, plancher sur plancher, vitre contre une cloison, panneaux solaires sur le toit du hub, coffre du jeu sur le sol du hub ; ⏳ geste du joueur avec l'outil de construction **à refaire en jeu** |
| v0.9.0 : nouveaux modules | ✅ laboratoire, infirmerie, support vital et observatoire construits et photographiés de l'intérieur (table de recherche, capsule de clonage et carte stellaire du jeu en place, hologramme de la carte actif) ; ⏳ ouvrir la recherche, choisir la capsule comme point de réapparition et l'oxygène du support vital **à vérifier en jeu** |
| v0.10.0 : accrochage des modules | ✅ test en jeu par l'outil d'accrochage du jeu : couloir à la porte du haut d'une cage d'escalier (à 3 m, centré sur la porte), coude contre un hub, écran du jeu fixé sur le mur d'un hub, plus les cas de la v0.9.0 |
| v0.10.1 : bulle du champ de force | ✅ en jeu (vaisseau de test câblé) : grille hexagonale, bord lumineux, bande de balayage, onde au choc (photo) ; le modèle du générateur ne change pas |
| v0.10.1 : console de bord | ✅ écran relevé au-dessus du pied (photo) |
| v0.10.1 : table de cartographie | ✅ les vaisseaux et coffres ne sont marqués que s'ils sont vraiment autour de la table (affichés, à moins de 300 m en hauteur comme à plat) : un vaisseau loin au-dessus ou en dessous n'apparaît plus en jaune |
| v0.15.1 : erreurs réseau « Size of -216 is invalid » | ✅ cause trouvée : le jeu découpe la synchronisation des vaisseaux (25 fois/s) en paquets d'environ 1 000 octets avec une estimation trop basse pour les espaces lointains ; avec les bases pirates en plus, un paquet débordait et tout ce qui suivait était mal lu (vaisseaux figés, dégâts perdus). Estimation corrigée : sur une copie du monde du joueur, avec les trois bases, 0 erreur (avant : 600 à 1 900 par minute) |
| v0.15.0 : zone sûre, bases, Bastion | ✅ en jeu : zone sûre calculée (cible à 2 332 km d'Otrera b : hors zone) ; les trois bases créées à leur place dans un monde existant (824, 988 et 311 plaques) ; base de test près d'une cible : 5 armes pointées tirent par rafales, elle lance un corsaire ; Bastion Écarlate visité : 7 grandes tours, 25 tours, 14 dômes, 32 canons, 21 bunkers, 43 remparts chargés, bassins rouges, pas d'air (vue de nuit). ⏳ Rupture de combat dans la zone sûre, salves de missiles des bases et vue de jour du Bastion **à voir en jeu** |
| v0.14.0 : pirates | ✅ en jeu : les quatre modèles photographiés (`DevPirateShowcase`) ; corsaire contre une plateforme (`DevPirateTest`) : tir par rafales (1,5 s puis 3,5 s de rechargement), ses missiles détruisent 13 pièces de la cible, les missiles Arsenal tirés sur lui usent sa coque (2 500 → 1 850 → 1 200 : le carénage ne bloque pas). ⏳ Éclaireur, pillard et croiseur en combat, tirage au hasard en vol **à voir en jeu** |
| v0.13.3 : commande des moteurs, cas réels | ✅ en jeu (moteur à matière noire, sans pilote) : alimenté par ses réservoirs avec une vanne ailleurs sur la ligne d'oxygène, il reste éteint (avant : toujours allumé) ; transmetteur câblé sur la prise de commande, canal émis par un autre transmetteur : pleine poussée ; vanne seule : pleine poussée ; siège : poussée seulement à la demande du pilote. ⏳ Transmetteur enfiché directement et relais de canal entre nacelles sur rotor **à voir en jeu** |
| v0.13.2 : moteurs commandés par vanne ou signal | ✅ en jeu (moteur à matière noire, sans pilote) : alimenté par une vanne ouverte par un signal, il monte à 1 en 2 s ; signal câblé sur sa prise de commande : idem ; prise visible sur le socle (photo) |
| v0.13.1 : moteurs au siège | ✅ en jeu (vaisseau de test, moteur à matière noire) : branché et alimenté, il reste éteint (0) ; un pilote au siège qui demande « monter » le fait passer à 1 en 2 s et le vaisseau décolle |
| v0.13.0 : champ qui ne s'allume pas | ✅ cause trouvée dans la sauvegarde du joueur : un champ à matière noire (10 000 W) sur un réseau d'un seul générateur à bois (4 000 W). En jeu (vaisseau de test, champ réglé à 20 000 W sur 6 000 W) : état -3 affiché, le champ cesse de demander du courant 5 s entre deux essais ; anneaux clignotants (code) |
| v0.13.0 : champs compact, réactif, nucléaire | ✅ en jeu : champ nucléaire monté, 0,01 combustible/s, explosion Arsenal de 1 400 : −9 % ; les six générateurs photographiés (le compact plus petit). Compact et réactif : valeurs non mesurées à part |
| v0.13.0 : pirates | ✅ en jeu (`DevPirateTest`, sur une planète) : le corsaire s'approche de 500 à 175 m, garde le nez sur sa cible (1°), tire et la détruit (27 → 7 pièces) ; il reste en l'air au-dessus d'elle ; 4 missiles Arsenal le détruisent (2 500 → 0), il explose et laisse une capsule de butin (coffre à 4 piles). Croiseur : 8 armes, se bat à 250 m, sa défense rapprochée abat les missiles. ⏳ Apparition automatique en vol dans l'espace et combat en multijoueur **à voir en jeu** |
| v0.12.0 : énergie nucléaire | ✅ en jeu (vaisseau de test câblé, `DevNuclearTest`) : la centrifugeuse chargée de 20 minerais d'uranium tourne à plein, 0,05 minerai/s, et remplit un réservoir vide de combustible nucléaire à 0,01 unité/s ; le réacteur nucléaire reste allumé (100) et brûle 0,002 unité/s ; menu de construction : nouvelle rangée (photo). ⏳ Gisements d'uranium sur les planètes et chargement de la trappe à la main ou par ligne d'objets **à voir en jeu** |
| v0.12.0 : moteurs nucléaires et à matière noire | ✅ en jeu : propulseur nucléaire à plein régime, 0,002 combustible + 0,02 oxygène/s, le vaisseau de test décolle et monte à 260 m/s ; moteur à matière noire, 0,002 matière noire + 0,03 oxygène/s, +430 m en 6 s en vol ; modèles des trois moteurs (photos). Moteur nucléaire lourd : modèle vérifié, poussée non mesurée à part |
| v0.11.1 : champs renforcé et à matière noire | ✅ en jeu (vaisseau de test lâché de 40 m, Arsenal 1.4.0) : **renforcé** 6 000 W, 0,05 O₂/s, crash et explosion de 1 400 encaissés (−15 % pour l'explosion), 30 pièces sur 30, bulle dorée (photo) ; **matière noire** 10 000 W, 0,02 matière noire/s, explosion −6 %, missile −5 %, 30 pièces sur 30, bulle violette (photo). Un réacteur quantique partagé entre deux réseaux (6 000 W chacun) ne suffit pas au champ à matière noire |
| v0.11.0 : champ de force contre Arsenal | ✅ en jeu (vaisseau de test sous champ, Arsenal 1.4.0) : explosion Arsenal de 1 400 absorbée, 30 pièces sur 30 gardées ; missile Arsenal tiré sur le vaisseau : il explose sur le champ, aucune pièce perdue. Souffle ramené à ÷ 20 ensuite |
| v0.11.0 : prises en une rangée | ✅ toutes les prises des machines du mod sur la face arrière, en ligne (photo) |
| v0.11.0 : réservoir de matière noire infini | ✅ construit (photo) ; mêmes mécanismes que le réservoir infini du mod TF |
| v0.10.3 : taille du champ de force | ✅ la bulle se mesurait elle-même (sa grille comptait dans la taille du vaisseau) et grossissait de 30 % toutes les 2 s ; elle est exclue du calcul. Vérifié en jeu : taille constante (9,4 × 5,2 × 9,5 m) sur 24 s |
| v0.10.2 : carte stellaire en double | ✅ cause trouvée : le jeu construit une deuxième carte, invisible, sur le corps physique du vaisseau (pour cliquer les planètes) ; le mod y ajoutait ses noms et repères, qui apparaissaient en double devant le vaisseau en vol puis tremblaient. Seule la carte affichée est décorée (vérifié en jeu : 24 repères, tous sur le vaisseau affiché) |
| v0.10.1 : interactions des modules | ✅ en jeu : 3 interrupteurs (pièces éteintes), 3 lits, 2 consoles, 2 points de réapparition trouvés ; la couchette du haut a fait de l'habitation le point de réapparition |
| v0.10.1 : module posé seul | ✅ en jeu : la requête de création envoyée pour un hub crée une nouvelle station (dalle + hub, photo) ; ⏳ le geste avec l'outil de construction **à faire en jeu** |
| v0.10.1 : levier du sas | ✅ dans le repère de la pièce (le jeu y lit le geste), contre le mur du fond ; ⏳ le manier **à faire en jeu** |
| v0.10.0 : escalier | ✅ rayons le long de l'escalier en jeu : surface continue du sol (0,15 m) au palier (3,15 m), 0,24 m de montée tous les 0,4 m ; 1,45 m pour y accéder depuis la porte du bas ; ⏳ monter à pied **à faire en jeu** |
| v0.10.0 : matériaux | ✅ dôme et coude peints par le code du mod TF : extérieur en bois, intérieur en blanc teinté (photos) |
| v0.10.0 : dôme, tube, siège à leviers | ✅ construits et photographiés (intérieurs du dôme et du tube) ; ⏳ leviers qui bougent en pilotant **à voir en jeu** |
| v0.10.0 : nouveaux modules | ✅ sas (levier, portes qui coulissent, photo), couloirs d'angle, en T, en U, long, cage d'escalier : construits et photographiés de l'intérieur ; ⏳ monter l'escalier et passer le sas **à faire en jeu** |
| v0.10.0 : peinture extérieur / intérieur | ✅ module peint par le code du mod TF : extérieur rouge, intérieur bleu (photos) ; ⏳ au pistolet, en visant **à faire en jeu** |
| v0.10.0 : habitation | ✅ lit, armoire et console en place (interaction du lit copiée du lit du jeu) ; ⏳ dormir, ranger et ouvrir le wiki depuis la console **à faire en jeu** |
| v0.10.0 : Upgrade Table | ✅ le mod Upgrade Table copiait le laboratoire (même état que la table de recherche) ; il copie de nouveau la table du jeu (vérifié dans l'arbre de la pièce) |
| v0.10.0 : écrans de bord | ✅ le texte « en attente du vaisseau » du modèle restait sous les stats : le canevas en double est retiré |
| v0.9.0 : wiki dans le Guide | ✅ entrée « Wiki TF Avancé » dans le Guide du jeu, 23 pages rendues (titres, listes, tableaux, liens), navigation Accueil → Pyros → Machines → Retour (photos) ; ⏳ clic de souris sur un lien **à refaire en jeu** |
| v0.9.0 : onglets de construction | ✅ deux rangées d'onglets centrées dans le cadre, libellés à taille lisible (mesuré en jeu) |
| v0.9.0 : collecteur et réacteur à matière noire | ✅ faces intérieures (entonnoir, anneaux) visibles : modèles en double face (photo) |
| v0.9.0 : recherche | ✅ 11 techs ajoutées, celles du jeu et des autres mods ne bougent plus ; ⏳ écran de la table de recherche **à voir en jeu** |
| Pose des pièces, écrans en vol, forces des moteurs, multijoueur | ⏳ **à faire en jeu** (procédure ci-dessous) |

### Procédure de test en jeu

1. Charger un monde. Ouvrir l'onglet **TF Avancé**. Poser un ordinateur mural et une console : l'écran affiche « en attente » pendant la pose, puis les données du vaisseau une fois posé.
2. Au sol : altitude faible et stable, vitesse surface ≈ 0, rotation ≈ 0 °/s.
3. Décoller : la vitesse surface doit suivre l'hologramme du siège, la vitesse verticale doit être positive en montée.
4. Poser un **moteur lourd** avec oxygène + éthanol et une vanne. L'écran PROPULSION doit afficher jusqu'à 23 400 N, atteints en 1,5 s.
5. Poser un **propulseur vectoriel** loin du centre de masse et l'allumer : le vaisseau doit pousser **et** tourner. Au centre de masse, il ne doit que pousser.
6. **Moteur économe** : comparer la baisse des réservoirs avec celle d'un propulseur chimique.
7. Débrancher une conduite d'ergols : alerte dans DIAGNOSTICS et pastille dans l'en-tête.
8. Sauvegarder, recharger : pièces, orientation et affichage conservés.
9. Multijoueur : un client voit les mêmes écrans, l'hôte voit en plus la demande et la capacité des réseaux.
10. **Batterie** : la brancher sur le réseau d'un générateur à bois. Avec un consommateur faible, le % monte et le générateur brûle plus vite. Générateur vide : la batterie prend le relais et le % baisse. Sauvegarder et recharger : le % est conservé.
11. **Disjoncteur** : générateur sur l'entrée, gyroscope ou électrolyseur sur la sortie (réseau séparé). La sortie est alimentée. Mettre une constante 0 sur la commande : le disjoncteur s'ouvre et le voyant devient gris. Brancher plus de 4 000 W en sortie : il disjoncte et le voyant devient rouge.
12. **Structure** : poser des poutres au sol et sur un mur, des plaques et un cadre. Marcher dessus, sauvegarder et recharger.
13. `MelonLoader/Latest.log` : aucune erreur `ship computer`, `engine` ni `power grid`.

## Feuille de route

Voir [docs/AUDIT.md](docs/AUDIT.md) §7–8 : priorités du réseau, compléments de structure (poutres en L/T/X, plaques triangulaires), puis Docking, Landing Gear, Station.
