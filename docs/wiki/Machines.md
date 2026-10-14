# Machines

Onglet de construction **TF Avancé**. Les machines se débloquent à la [table de recherche](Recherche.md).

## Énergie

| Machine | Effet | Prises |
|---|---|---|
| Batterie | stocke l'énergie (1 200 kJ), se recharge sur le surplus des générateurs | 2 puissance, 1 signal (charge %) |
| Disjoncteur | relie deux réseaux, coupe en surcharge (4 000 W) | entrée, sortie, état, commande |
| Réacteur quantique | 12 000 W sans carburant | 2 puissance, 1 signal |
| Batterie à cryonite | 8 batteries, charge et décharge ×3 | comme la batterie |
| Condenseur à cryonite | 0,05 oxygène/s pour 1 500 W | 2 puissance, 1 fluide, 1 signal |
| Gyroscope quantique | le couple de 5 gyroscopes | comme le gyroscope |
| Support de panneaux solaires | jusqu'à 3 000 W face à l'étoile, rien la nuit ou à l'ombre d'une planète | 2 puissance, 1 signal (W) |

## Champ de force

Enveloppe tout le vaisseau (et ce qui y est amarré) d'une bulle cyan qui encaisse les chocs et les forces de marée des trous noirs : **150 points** (un choc compte sa variation de vitesse en m/s). Consomme 2 000 W et 0,02 oxygène/s, se recharge de 0,5 point/s. À zéro il casse et redémarre **5 minutes** plus tard. La bulle est une coque d'énergie : presque invisible de face, lumineuse sur les bords, couverte d'une grille hexagonale, traversée par une bande de balayage qui monte ; chaque impact lance une onde qui s'étend sur la bulle. Elle rougit quand la charge baisse et vacille quand elle est presque vide.

**Avec le mod Arsenal** : le champ encaisse aussi ses armes. Les balles et éclats qui touchent la coque, et le souffle des explosions sur les pièces, vident le champ au lieu d'abîmer le vaisseau (dégâts de plaque ÷ 4, puissance d'explosion ÷ 20 : la plus grosse explosion prend environ la moitié d'un champ plein). Tant qu'il tient, les explosions ne poussent plus le vaisseau. Les missiles et roquettes qui entrent dans la bulle explosent dessus, avec une onde à l'endroit de l'impact. Prise signal : charge en % (-1 éteint, négatif : secondes avant redémarrage).

### Modèles plus résistants

| Générateur | Points | Recharge | Consommation | Fluide | Recherche |
|---|---|---|---|---|---|
| Champ de force (cyan) | 150 | 0,5 /s | 2 000 W | 0,02 oxygène/s | Technologie des artefacts |
| **Champ renforcé** (doré) | 450 | 1,5 /s | 6 000 W | 0,05 oxygène/s | Technologie des artefacts |
| **Champ à matière noire** (violet) | 1 200 | 4 /s | 10 000 W | 0,02 matière noire/s | Matière noire |

Même fonctionnement et mêmes prises que le champ de base ; la bulle prend la couleur du générateur. Le champ à matière noire se branche sur un réservoir de matière noire (pas d'oxygène) ; il lui faut un réacteur quantique entier : branché sur deux réseaux, le réacteur n'en donne que la moitié à chacun. Plusieurs générateurs sur un vaisseau : le plus chargé encaisse.

## Matière noire

| Machine | Effet |
|---|---|
| Collecteur de matière noire | 0,004 unité/s pour 8 000 W, ×5 près d'un trou noir, ×3 dans Érèbe ; la verse dans la conduite de sa prise de fluide |
| Réservoir de matière noire | le réservoir du jeu blindé de météorite (prend tous les fluides) |
| Réservoir de matière noire infini | toujours plein de matière noire : ce qu'on y puise se recrée, son contenu ne pèse rien |
| Réacteur à matière noire | **150 000 W** pour 0,002 unité/s de matière noire (le plus puissant) |
| Moteur à matière noire | **624 000 N** (80 propulseurs chimiques) pour 0,002 unité/s de matière noire et 0,03 d'oxygène ; chambre de confinement et tuyère magnétique en anneaux |

## Énergie nucléaire

Entre l'énergie des artefacts et la matière noire. L'**uranium** se mine sur TF Aridia, TF Petra et Pyros (rochers verdâtres aux veines lumineuses).

| Machine | Effet |
|---|---|
| Centrifugeuse d'enrichissement | prend le minerai d'uranium dans sa trappe (à la main ou par une ligne d'objets) et le change en **combustible nucléaire** (fluide) dans la conduite de sa prise de fluide : 0,05 minerai/s, 0,2 unité de combustible par minerai, 6 000 W. Branchement du distillateur d'éthanol du jeu |
| Réservoir de combustible nucléaire | le réservoir du jeu blindé (prend tous les fluides) |
| Réacteur nucléaire | **40 000 W** pour 0,002 unité/s de combustible nucléaire ; cuve sous pression, barres de contrôle, boucles de refroidissement |
| Propulseur nucléaire | **93 600 N** (12 propulseurs chimiques) pour 0,002 unité/s de combustible et 0,02 d'oxygène |
| Moteur nucléaire lourd | **234 000 N** (30 propulseurs chimiques) pour 0,005 unité/s de combustible et 0,05 d'oxygène ; turbopompes et radiateurs |

Les moteurs nucléaires et à matière noire se branchent comme le propulseur chimique (vannes sur les conduites), mais une prise reçoit leur combustible et l'autre l'**oxygène**, dans n'importe quel ordre. Un minerai d'uranium fait tourner un réacteur nucléaire 100 s.

## Générateur de portail

Ouvre un portail violet **45 m devant lui** (dans la direction de sa face avant), fixe dans l'espace, de 18 m de rayon. Le vaisseau qui le traverse ressort à côté de la **cible choisie sur l'ordinateur de visée ou la carte stellaire** :
- une planète : à environ 20 km d'altitude, du côté d'où vous venez ;
- une balise (station, épave, vaisseau) : à 300 m.

Fonctionnement : un signal > 0 sur la **prise de commande** le lance ; il charge **10 s** à 20 000 W, brûle **5 matière noire** (prise de fluide) puis le portail reste ouvert **30 s**, et le générateur se recharge 60 s. Prise d'état : 0 prêt, 1 à 99 charge %, 100 ouvert, -1 pas de cible, -2 pas assez de matière noire, -3 pas de courant, -10 et moins : secondes de recharge.

Le modèle : un anneau dressé entre deux pylônes cerclés de bobines lumineuses et d'ailettes, des chevrons lumineux autour de l'anneau, une lentille d'émission dessous, des bancs de condensateurs et un pupitre de commande à l'arrière.

C'est le seul moyen raisonnable d'atteindre la [galaxie d'Érèbe](Galaxie-Erebe.md).

## Table de cartographie

Table holographique carrée de 1,4 m (onglet TF Avancé, rangée des ordinateurs) : piétement octogonal à ouïes, plateau de verre noir quadrillé, émetteur central, pupitre à clavier et deux petits écrans à l'avant, tournés vers le joueur. L'hologramme tient dans le cadre du plateau. Quand un joueur s'en approche, elle affiche :
- le **relief en 3D** du terrain sur 300 m autour du vaisseau (lignes de niveau sur une surface translucide) ;
- des repères : gisements et rochers (orange), coffres (jaune), bâtiments (violet), vaisseaux (blanc), votre vaisseau (vert) ;
- la **fiche de la planète** : biome, gravité, air, ressources, lieux, utilité, dangers (les fiches de ce wiki).

## Moteurs

Propulseur vectoriel, moteur lourd, moteur de précision, moteur économe, moteur à matière exotique (78 000 N pour 10 % des ergols).

## Carte stellaire

La carte stellaire du jeu montre le système où se trouve le vaisseau. Dans son menu (comme dans celui de l'ordinateur de visée), le bouton **Galaxie** passe de **442 Otrera** à **Érèbe** puis revient au système du vaisseau : la carte et la liste des cibles montrent les planètes de la galaxie choisie.

## Prises

Toutes les machines du mod ont leurs prises **en une seule rangée sur la face arrière** : tous les câbles partent dans le même axe. De gauche à droite (vu de derrière) : électricité, fluide, signal, électricité.
