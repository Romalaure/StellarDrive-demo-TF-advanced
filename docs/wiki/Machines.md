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

Enveloppe tout le vaisseau (et ce qui y est amarré) d'une bulle cyan qui encaisse les chocs et les forces de marée des trous noirs : **150 points** (un choc compte sa variation de vitesse en m/s). Consomme 2 000 W et 0,02 oxygène/s, se recharge de 0,5 point/s. À zéro il casse et redémarre **5 minutes** plus tard. La bulle rougit quand la charge baisse et flashe à chaque impact. Prise signal : charge en % (-1 éteint, négatif : secondes avant redémarrage).

## Matière noire

| Machine | Effet |
|---|---|
| Collecteur de matière noire | 0,004 unité/s pour 8 000 W, ×5 près d'un trou noir, ×3 dans Érèbe ; la verse dans la conduite de sa prise de fluide |
| Réservoir de matière noire | le réservoir du jeu blindé de météorite (prend tous les fluides) |
| Réacteur à matière noire | 40 000 W pour 0,002 unité/s de matière noire |

## Générateur de portail

Ouvre un portail violet **45 m devant lui** (dans la direction de sa face avant), fixe dans l'espace, de 18 m de rayon. Le vaisseau qui le traverse ressort à côté de la **cible choisie sur l'ordinateur de visée ou la carte stellaire** :
- une planète : à environ 20 km d'altitude, du côté d'où vous venez ;
- une balise (station, épave, vaisseau) : à 300 m.

Fonctionnement : un signal > 0 sur la **prise de commande** le lance ; il charge **10 s** à 20 000 W, brûle **5 matière noire** (prise de fluide) puis le portail reste ouvert **30 s**, et le générateur se recharge 60 s. Prise d'état : 0 prêt, 1 à 99 charge %, 100 ouvert, -1 pas de cible, -2 pas assez de matière noire, -3 pas de courant, -10 et moins : secondes de recharge.

Le modèle : un anneau dressé entre deux pylônes cerclés de bobines lumineuses et d'ailettes, des chevrons lumineux autour de l'anneau, une lentille d'émission dessous, des bancs de condensateurs et un pupitre de commande à l'arrière.

C'est le seul moyen raisonnable d'atteindre la [galaxie d'Érèbe](Galaxie-Erebe.md).

## Table de cartographie

Table holographique carrée de 1,4 m (onglet TF Avancé, rangée des ordinateurs) : piétement octogonal à ouïes, plateau de verre noir quadrillé, émetteur central, pupitre à clavier et deux petits écrans à l'avant. L'hologramme tient dans le cadre du plateau. Quand un joueur s'en approche, elle affiche :
- le **relief en 3D** du terrain sur 300 m autour du vaisseau (lignes de niveau sur une surface translucide) ;
- des repères : gisements et rochers (orange), coffres (jaune), bâtiments (violet), vaisseaux (blanc), votre vaisseau (vert) ;
- la **fiche de la planète** : biome, gravité, air, ressources, lieux, utilité, dangers (les fiches de ce wiki).

## Moteurs

Propulseur vectoriel, moteur lourd, moteur de précision, moteur économe, moteur à matière exotique (78 000 N pour 10 % des ergols).
