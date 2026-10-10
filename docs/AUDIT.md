# Audit technique — StellarDrive Demo

Audit réalisé le 10/10/2026 sur l'installation locale, par décompilation (ilspycmd 9.1) des assemblies du jeu et de SDModKit. Le code décompilé n'est pas inclus dans ce dépôt.

## 1. Environnement

| Élément | Valeur vérifiée |
|---|---|
| Jeu | StellarDrive Demo (CuriousOwlGames), Steam appid 4689730, build 25548544 |
| Moteur | Unity 6000.3.24f1, scripting Mono (MonoBleedingEdge), x64, URP |
| Code du jeu | C#, ~40 assemblies modulaires (`Ships.dll`, `Players.dll`, `Planets.dll`, `WorldTracking.dll`, `UI.dll`…) |
| Réseau | FishNet (serveur autoritaire : l'hôte simule la physique et les pièces, les clients reçoivent les états) |
| Entrées | Unity Input System (`Keyboard.current`) |
| UI native | uGUI + TextMeshPro, couleurs dans l'asset `Core.Values.GameColorPalette` |
| Chargeur de mods | MelonLoader 0.7.3 Open-Beta (runtime net35/Mono), Harmony 2 |
| Outils communautaires installés | SDModKit (pièces personnalisées par clonage de prefab, meshes OBJ, prises, config par pièce), UnityExplorer 4.13.1, StellarModManager |
| Compatibilité | Le mod TF (même chaîne MelonLoader + SDModKit) tourne déjà sur ce build : chaîne d'outils validée |

Le jeu n'a **pas d'API de modding officielle**. Toute extension passe par Harmony (patch de méthodes) et par le clonage de `PartSettings` existants (SDModKit).

Touches natives relevées dans les assets d'entrée : WASD, Maj, Ctrl, Espace, Tab, Q, E, F, R, G, flèches, F1–F3, 1–5, Échap, Entrée. Déjà prises par des mods : Inser (SDModKit), F9 (TF, SDModKit), T, V (TF). → Ordinateur de bord : **F6** / **F7**.

## 2. Comment le jeu représente les vaisseaux

- **Vaisseau** = `ShipState` (réseau) suivi par `ShipsServerTracker` (hôte) et `ShipsClientTracker` (tous). Position/rotation/vitesse dans `ShipPhysicsState`, **relatives à un « espace »** (`PlanetSurfaceSpace`, `OrbitSpace`, `TrajectorySpace`, `WarpSpace`, `PlanetRingSpace`, `ShipRelativeSpace`). Conversion entre espaces : `IWorldTransposeClient`.
- **Coque** = grille voxel (`ShipGrid`). Chaque voxel a 4 emplacements (sol, mur avant, mur droit, intérieur). Seulement **4 formes** (`HullMeshSegmentType` : Flat, Corner, Slope, SlopeCorner), en métal, verre ou bois. Les maillages et collisions de coque sont générés par chunks à partir de ces 4 formes.
- **Pièces à état** (`StatefulPart`) : `PartSettings` + placement + `IStatefulPartState` (record immuable remplacé à chaque tick par des « updaters » côté hôte). Collisions boîte (`ShipStatefulColliders`).
- **Liaisons** : rotors et portes d'amarrage créent des vaisseaux enfants dans un arbre (`ShipsJointsTreeTracker`). L'enfant **perd son Rigidbody** et devient parenté cinématiquement à la racine (`ShipRigidbody.SetJoint`). Seule la racine a un corps physique.
- **Câbles** : signal, fluide, électricité (`CableType`). Groupes de connexion calculés par `ConnectionsTracker`.

## 3. Physique (hôte uniquement)

`WorldPhysicsServerUpdater.PrePhysics` → pour chaque vaisseau : signaux → préparation énergie → consommateurs/producteurs → transferts → `UpdateParts` → `ShipNewtonianPhysics.ComputePhysicsForces` → `Rigidbody.AddRelativeForce/AddRelativeTorque`.

- **Poussée** (`ShipThrust`) : somme de `-UpOrientation × force` pour chaque propulseur. **Appliquée au centre de masse, sans couple.** Constantes **en dur** : propulseur chimique 7 800 N × `ThrusterStrength`, propulseur à bois 3 000 N × niveau. `ThrusterSettings.thrustStrength` **n'est pas lu**.
- **Propulseur chimique** : aucune commande directe. Il pousse dès que ses prises 0 et 1 reçoivent oxygène + éthanol (débit max 0,05 u/s par fluide). On le commande avec des vannes pilotées par signal.
- **Rotation** (`ShipRotation`) : gyroscopes (8 000 × force, prises signal 0-2, alim. prise 3, jusqu'à 800 W), gyroscope interne du siège (2 000), roues de réaction. Couple appliqué en **accélération angulaire** (degrés/s), plus un amortissement fixe. Ce n'est pas un vrai modèle de couple.
- **Collisions** : `ShipCollisionDestruction` détruit des pièces selon une dureté × masse et génère des débris. Il existe donc un **système de dégâts natif**.
- Autres forces : traînée, gravité, portance, **magnétisme** (attraction des portes d'amarrage).

## 4. Énergie

- Réseau par groupe de câbles (`PowerServerTracker`, `PowerGroupServerState`) : `TotalPowerNeeded`, `TotalPowerProductionCapacity`, taux de satisfaction. Valeurs par tick, converties en W par le jeu (`watts = transfert / deltaTime`).
- Producteur unique : **générateur à bois** (4 000 W max). Consommateurs : gyroscope, électrolyseur (2 000 W × taux), distillateur (400 W × taux).
- **Aucun stockage** : la pièce « Battery Bank » (id 65) n'a **aucun code** qui la lit ou l'écrit. Elle est décorative dans la démo.
- Les clients reçoivent les watts par câble (`CablePowerState`). Le détail demande/capacité n'existe que chez l'hôte.

## 5. Interface

- UI native en uGUI/TextMeshPro avec la palette `GameColorPalette` (fond, lignes, texte, indisponible, invalide). On peut récupérer la palette et la police du jeu à l'exécution.
- Le curseur est géré par le jeu (`UI.Common.MenuTracker`). Un overlay non interactif n'interfère pas avec les commandes.

## 6. Multijoueur

Seul l'hôte simule. Toute mécanique qui ajoute des forces ou de l'énergie doit tourner **sur l'hôte**, et **tous les joueurs** doivent avoir le mod pour les pièces ajoutées (les ids inconnus sont ignorés au chargement). Un instrument en lecture seule comme l'ordinateur de bord fonctionne pour chaque joueur séparément.

## 7. Faisabilité des 7 projets

| Projet | Verdict | Méthode réaliste | Limites |
|---|---|---|---|
| **Advanced Ship Computer** | ✅ **Livré en v0.2.0** : 6 écrans à poser | Lecture côté client des états synchronisés + détail énergie chez l'hôte | Pas de donnée terrain côté client (altitude = au-dessus du rayon moyen), vitesse angulaire dérivée, radar = vaisseaux suivis seulement |
| **Advanced Propulsion Pack** | ✅ **Livré en v0.2.0** : 4 moteurs | Pièces clonées du propulseur chimique (SDModKit). Patch Harmony de `ShipThrust.CalculateShipThrustForce` et `ShipThrustersUpdater` pour une poussée et une consommation par modèle | La poussée native n'a **pas de couple**. Un Vector Thruster qui fait tourner le vaisseau demande d'ajouter r × F (couple réel) à nos pièces, ce qui changerait le comportement par rapport aux natives. Consommation = ergols (l'énergie électrique ne sert pas aux propulseurs dans le jeu). La thermique serait une mécanique propre au mod |
| **Landing Gear Plus** | ⚠️ Partiel | Pièce avec collider escamotable + suspension par raycasts appliquant des forces de ressort/amortisseur au Rigidbody racine (hôte) | Pas de corps physique séparé possible pour les jambes (les liaisons sont cinématiques). Interaction avec le système de destruction à éprouver |
| **Power Grid Overhaul** | ✅ Réalisable | Batterie = pièce clonée qui ajoute de la capacité au groupe en déficit et consomme le surplus (patch de `PowerGroupServerState`/`PowerServerTracker`). Disjoncteur = coupure de groupe pilotée par signal | Le générateur ne produit que ce qui est demandé : la « recharge » doit être modélisée comme une demande. Priorités applicables seulement entre consommateurs que le mod contrôle |
| **Docking & Modular Ships** | ✅ Existe déjà nativement | Le jeu a déjà des portes d'amarrage magnétiques et un arbre de liaisons qui agrège poussée et rotation. Il faut **étendre** ce système (aide à l'alignement, indicateurs), pas en créer un second | Transferts : signaux/fluides/énergie traversent déjà les liaisons natives via les prises. Les portes horizontales du mod TF montrent qu'on peut ajouter des variantes |
| **Space Station Builder** | ⚠️ Partiel | Une station = un vaisseau (pas de structure « fixe » native). Modules = pièces clonées + amarrage natif | Pas d'ancrage permanent. Un ancrage artificiel (Rigidbody cinématique) aurait des conséquences physiques et réseau à étudier. Panneaux solaires = mécanique propre au mod (le jeu n'a ni capteur de lumière ni production solaire) |
| **Advanced Construction** | ⚠️ Partiel | Poutres, cadres, plaques, jonctions = **pièces à état** clonées avec meshes OBJ et colliders boîte, posées en `PreciseOnAny` | **Nouvelles formes de coque voxel impossibles sans réécrire le générateur de maillage et de collisions** (4 formes en dur). Les coques courbes ou triangulaires seraient des pièces séparées, pas de la coque native : elles ne fermeraient pas les pièces étanches (`ShipRooms`) |

## 8. Ordre recommandé

1. Advanced Ship Computer (v0.2.0, à valider en jeu)
2. Advanced Propulsion Pack (v0.2.0, à valider en jeu)
3. Power Grid Overhaul (batterie + surveillance)
4. Advanced Construction (poutre, plaque, cadre en pièces à état)
5. Docking (extension du natif), Landing Gear, Space Station
