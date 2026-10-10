# StellarDrive Demo — TF Advanced

Mods avancés du groupe TF pour **StellarDrive Demo** : ordinateur de bord, moteurs, réseau électrique, structure., dans un seul DLL MelonLoader, indépendant du mod `StellarDriveDemoTF`. Les pièces sont dans l'onglet de construction **TF Avancé**.

Rapport technique (moteur, systèmes du jeu, faisabilité de chaque projet) : [docs/AUDIT.md](docs/AUDIT.md).

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

## Configuration

`UserData/MelonPreferences.cfg`, section `[TFAdvanced]` :

| Clé | Défaut | Rôle |
|---|---|---|
| `ComputerRefreshRate` | `4` | rafraîchissements des écrans par seconde (1-20) |
| `ComputerViewDistance` | `40` | au-delà (m), un écran ne se rafraîchit plus |
| `TrafficRange` | `3000` | portée de la liste de trafic (m) |
| `CollisionWarningSeconds` / `CollisionWarningDistance` | `12` / `30` | seuils d'alerte de rapprochement |
| `DescentWarningSpeed` / `DescentWarningAltitude` | `20` / `400` | seuils d'alerte de descente |
| `SpinWarningRate` | `60` | seuil d'alerte de rotation (°/s) |
| `SelfTest` | `false` | développement : construit les pièces au menu et écrit leur description dans le log |

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
