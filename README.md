# StellarDrive Demo — TF Advanced

Mods avancés du groupe TF pour **StellarDrive Demo**, dans un seul DLL MelonLoader, indépendant du mod `StellarDriveDemoTF`. Les pièces sont dans l'onglet de construction **TF Avancé**.

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

1. Choisir un id libre (7307-7310 pour les écrans, 7315+ pour les moteurs) et un donneur : `SignalDisplay` pour une pièce vierge, `Thruster` pour un moteur.
2. Écrans : appeler `RegisterSmall` (ou copier `ConfigureWallScreen`) dans `ShipComputer/DashboardParts.cs`. Le contenu de l'écran est choisi par `layout`.
3. Moteurs : ajouter une ligne à `EngineCatalog.All`. La poussée, la consommation, le couple et la montée en régime suivent automatiquement.
4. Compiler, activer `SelfTest` et vérifier la pièce dans `MelonLoader/Latest.log`.

## Tests

| Test | Résultat |
|---|---|
| Compilation Release | ✅ 0 erreur, 0 avertissement |
| Chargement par MelonLoader | ✅ 6 patchs appliqués, aucune erreur |
| Construction des 10 pièces par SDModKit (auto-test au menu) | ✅ ids, libellés, masses, tailles, prises (0 pour les écrans, 4 pour les moteurs), poussées attendues |
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
10. `MelonLoader/Latest.log` : aucune erreur `ship computer` ni `engine`.

## Feuille de route

Voir [docs/AUDIT.md](docs/AUDIT.md) §7–8 : Power Grid (batterie, disjoncteur), Advanced Construction (poutres, plaques, cadres), puis Docking, Landing Gear, Station.
