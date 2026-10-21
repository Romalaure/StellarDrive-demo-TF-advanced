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

## Laser de minage embarqué

Le laser de minage du joueur, en grand, sur une tourelle à fixer sur la coque (sol, mur, plafond, dessous du vaisseau). Son rayon part tout droit de la surface où on le pose, jusqu'à **40 m**, et mine les rochers, cristaux, minerais d'astéroïde et **arbres** (pour le bois) qu'il touche, **3 fois plus vite** que l'outil. Ce qu'il extrait ne va pas dans votre inventaire : il va dans le **coffre relié par une ligne d'objets**.

1. Posez le laser, la tourelle tournée vers ce que vous voulez miner.
2. Branchez du courant sur une de ses prises électriques (**800 W** pendant qu'il tire).
3. Reliez sa **prise d'objets** (la trappe jaune, au milieu de la rangée de prises) par une **ligne d'objets** à un coffre qui en a une : le **terminal d'objets** ou la caisse de ravitaillement du mod *Item Lines*, ou une machine de stockage avec un port d'objets. Sans coffre relié, il ne tire pas.
4. Prise de **commande** : sans câble, il tire dès qu'il a du courant ; avec un câble (levier, bouton, siège), il tire quand le signal est au-dessus de 0.

Prise d'**état** : 2 mine, 1 tire sans rien à miner, 0 éteint, -1 sans courant, -2 sans coffre, -3 coffre plein (il attend qu'on le vide). Le minage se fait chez l'hôte, près d'un joueur (les rochers n'existent que là où il y a quelqu'un). Recherche *Minage embarqué*.

## Champ de force

Enveloppe tout le vaisseau (et ce qui y est amarré) d'une bulle cyan qui encaisse les chocs et les forces de marée des trous noirs : **150 points** (un choc compte sa variation de vitesse en m/s). Consomme 2 000 W et 0,02 oxygène/s, se recharge de 0,5 point/s. À zéro il casse et redémarre **5 minutes** plus tard. La bulle est une coque d'énergie : presque invisible de face, lumineuse sur les bords, couverte d'une grille hexagonale, traversée par une bande de balayage qui monte ; chaque impact lance une onde qui s'étend sur la bulle. Elle rougit quand la charge baisse et vacille quand elle est presque vide.

**Avec le mod Arsenal** : le champ encaisse aussi ses armes (et celles des [pirates](Pirates.md)). Les balles et éclats qui touchent la coque, et le souffle des explosions sur les pièces, vident le champ au lieu d'abîmer le vaisseau (dégâts de plaque ÷ 4, puissance d'explosion ÷ 20 : la plus grosse explosion prend environ la moitié d'un champ plein). Tant qu'il tient, les explosions ne poussent plus le vaisseau. Les missiles et roquettes qui entrent dans la bulle explosent dessus, avec une onde à l'endroit de l'impact. Prise signal : charge en % (-1 éteint, négatif : secondes avant redémarrage).

### Les six générateurs

| Générateur | Points | Recharge | Courant | Fluide | Redémarrage | Recherche |
|---|---|---|---|---|---|---|
| **Champ compact** (blanc bleuté, petit) | 60 | 0,3 /s | 600 W | 0,01 oxygène/s | 5 min | Technologie des artefacts |
| Champ de force (cyan) | 150 | 0,5 /s | 2 000 W | 0,02 oxygène/s | 5 min | Technologie des artefacts |
| **Champ réactif** (orange) | 300 | 6 /s | 5 000 W | 0,03 oxygène/s | **1 min** | Technologie des artefacts |
| **Champ renforcé** (doré) | 450 | 1,5 /s | 6 000 W | 0,05 oxygène/s | 5 min | Technologie des artefacts |
| **Champ nucléaire** (vert) | 750 | 2,5 /s | 8 000 W | 0,01 combustible nucléaire/s | 5 min | Énergie nucléaire |
| **Champ à matière noire** (violet) | 1 200 | 4 /s | 10 000 W | 0,02 matière noire/s | 5 min | Matière noire |

Même fonctionnement et mêmes prises pour tous ; la bulle prend la couleur du générateur. Plusieurs générateurs sur un vaisseau : le plus chargé encaisse. Ils ont leur propre rangée dans l'onglet TF Avancé.

### Il ne s'allume pas ?

Le réseau électrique doit pouvoir lui donner **toute** sa puissance (avec les autres machines du réseau). Un générateur à bois donne 4 000 W, un réacteur quantique 12 000 W (6 000 W par prise s'il alimente deux réseaux), un réacteur nucléaire 40 000 W. Le générateur montre ce qui lui manque :
- **anneaux rouges clignotants**, signal **-3** : pas assez de courant. Il cesse d'en demander pendant 5 s puis réessaie, pour ne pas priver le reste du vaisseau ;
- **anneaux orange clignotants**, signal **-2** : pas de fluide (mauvais fluide, conduite vide ou débit trop faible) ;
- anneaux éteints, signal **-1** : aucune prise électrique branchée ;
- signal **-10 et moins** : il a cassé, il redémarre dans (−valeur − 10) secondes.

## Porte à champ de force

Un émetteur à poser **au sol, au milieu d'une ouverture de la coque**, face à l'ouverture. Regarde-le et appuie sur la touche d'utilisation (**T**) : une fenêtre permet de choisir la **largeur et la hauteur du champ** (de 1 à 30 m chacune, par exemple 2 × 2 ou 10 × 20 ; un contour orange montre la taille pendant le réglage) et de l'allumer ou l'éteindre.

- Allumé, le champ **retient l'oxygène comme une porte fermée**, mais **on passe à travers** (une onde part de l'endroit où tu le traverses).
- Il consomme **25 W par m²** (réglage `FieldDoorWattsPerSquareMeter`) sur ses deux prises électriques ; le réseau doit pouvoir les fournir en entier. Sans courant, il s'éteint et l'air s'échappe.
- Voyants du rail : cyan actif, orange clignotant pas assez de courant ou démarrage, rouge clignotant pas de réseau, éteint.
- **Valeurs négatives** : largeur négative = le champ part de l'autre côté ; hauteur négative = il descend depuis l'émetteur (posé au plafond).
- **Solide** : réglage « Solide » dans la fenêtre : le champ devient un mur, personne ne passe (l'air reste retenu).
- **Couleur** : peins la porte au pistolet à peinture, le champ prend sa couleur.
- **Gestionnaire de portes** (même rangée) : une console qui liste toutes les portes du vaisseau et de ce qui y est amarré (combien, lesquelles sont actives) les allume ou les coupe, et les rend solides ou traversables, une par une ou toutes ensemble. Pas de câble.
- **Côté** : le champ part des deux côtés de l'émetteur, ou seulement à sa gauche ou à sa droite (vu face à l'émetteur, du côté sans prises) : pose-le alors contre un bord de l'ouverture.
- Le champ est épais (20 cm) et descend jusqu'au sol. Quand il est actif, le rail n'a plus de collision : on passe dessus sans buter, et il se peint et se démonte même allumé.
- Prends la taille de l'ouverture : le champ déborde d'un demi-mètre sur la coque autour, et se cale de lui-même dans le plan de la coque.
- Recherche **Technologie des artefacts**, rangée des champs de force de l'onglet TF Avancé.

## Train d'atterrissage

Une jambe rétractable à amortisseur (onglet TF Avancé, rangée propulsion, recherche Propulsion avancée), à fixer **sous la coque** : elle sort vers le sol.

- **Sortie**, elle porte le vaisseau comme une suspension : ressort et amortisseur absorbent l'atterrissage (testé : chute de 5 m amortie), le patin tient au sol sans glisser, même en pente.
- **Rentrée**, elle se replie à plat contre la coque.
- Il sort et rentre selon le **signal** de sa prise de commande (celle de droite) : branche-la à un levier, un siège de pilotage, un contrôle avancé ou tout ce qui donne un signal (sorti au-dessus de 0,5). Sans câble, il reste sorti. La prise de gauche donne son état (1 sorti).
- Mets-en au moins trois, bien écartés. La raideur se règle seule selon la masse du vaisseau et le nombre de jambes.

## Énergie nucléaire

Entre l'énergie des artefacts et la matière noire. L'**uranium** se mine sur TF Aridia, TF Petra et Pyros (rochers verdâtres aux veines lumineuses).

| Machine | Effet |
|---|---|
| Centrifugeuse d'enrichissement | prend le minerai d'uranium dans sa trappe (à la main ou par une ligne d'objets) et le change en **combustible nucléaire** (fluide) dans la conduite de sa prise de fluide : 0,05 minerai/s, 0,2 unité de combustible par minerai, 6 000 W. Branchement du distillateur d'éthanol du jeu |
| Réservoir de combustible nucléaire | le réservoir du jeu blindé (prend tous les fluides) |
| Réacteur nucléaire | **40 000 W** pour 0,002 unité/s de combustible nucléaire ; cuve sous pression, barres de contrôle, boucles de refroidissement |
| Propulseur nucléaire | **93 600 N** (12 propulseurs chimiques) pour 0,002 unité/s de combustible et 0,02 d'oxygène |
| Moteur nucléaire lourd | **234 000 N** (30 propulseurs chimiques) pour 0,005 unité/s de combustible et 0,05 d'oxygène ; turbopompes et radiateurs |

Les moteurs nucléaires et à matière noire se branchent comme le propulseur chimique, mais une prise reçoit leur combustible et l'autre l'**oxygène**, dans n'importe quel ordre. Trois façons de les commander :
- **prise de commande** (prise signal à l'arrière de leur socle) : un signal dessus règle leur poussée, de 0 à 1. Câble, ou petit transmetteur enfiché directement dans la prise : un transmetteur branché là entend son canal sur tout l'assemblage, nacelles sur rotor comprises (le jeu, lui, ne relie les canaux qu'à l'intérieur d'un même vaisseau) ;
- **vanne ou pompe** sur leur conduite, comme le propulseur chimique : seulement si la conduite qui va au moteur n'a aucun réservoir (le carburant n'arrive que par la vanne) ; ils poussent quand la vanne laisse passer ;
- **branchés en direct** sur leurs réservoirs, sans signal : ils obéissent au **siège de pilotage** et ne poussent que si un pilote assis à un siège du vaisseau (ou d'un vaisseau amarré) demande de la poussée dans leur direction (avancer, reculer, monter, descendre ; le siège avancé pilote aussi les côtés).

Réglage `EngineSeatControl = false` : branchés en direct, ils poussent dès qu'ils sont alimentés. Un minerai d'uranium fait tourner un réacteur nucléaire 100 s.

## Générateur de portail

Ouvre un portail violet **45 m devant lui** (dans la direction de sa face avant), fixe dans l'espace, de 18 m de rayon. Le vaisseau qui le traverse ressort à côté de la **cible choisie sur l'ordinateur de visée ou la carte stellaire** :
- une planète : à environ 20 km d'altitude, du côté d'où vous venez ;
- une balise (station, épave, vaisseau) : à 300 m.

Fonctionnement : un signal > 0 sur la **prise de commande** le lance ; il charge **10 s** à 8 000 W (moins de courant : il charge plus lentement), brûle **3 matière noire** (prise de fluide) puis le portail reste ouvert **30 s**, et le générateur se recharge 60 s. Prise d'état : 0 prêt, 1 à 99 charge %, 100 ouvert, -1 pas de cible, -2 pas assez de matière noire, -3 pas de courant, -10 et moins : secondes de recharge.

Le modèle : un anneau dressé en douze sections blindées aux joints lumineux, avec quatre émetteurs de focalisation, entre deux pylônes cerclés de bobines et d'ailettes. Au centre, deux **anneaux de cardan** imbriqués comme un gyroscope tournent autour d'un **cristal flottant** : ils accélèrent avec la charge, le cristal pulse et tout vire au violet, puis ça clignote quand la charge est presque pleine. Dessous, une lentille d'émission ; à l'arrière, des bancs de condensateurs et un pupitre de commande.

**Ouverture du portail** : seize segments de cadre arrivent de tous côtés et se verrouillent en anneau dans un éclair, puis l'horizon s'ouvre depuis le centre : deux spirales qui tournent en sens contraire, des ondes qui courent vers le bord, des étincelles aspirées en spirale, un anneau de runes et un halo violet qui éclaire les alentours. À la fermeture (temps écoulé ou vaisseau passé), l'horizon s'effondre dans un éclair et les segments s'écartent.

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
