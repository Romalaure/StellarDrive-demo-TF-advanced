# Constructions

Onglet **TF Construction**. Le jeu a déjà des sols, murs et pentes de coque en bois, verre et fer ; ces pièces ajoutent les formes qui manquent. Ce sont des pièces posées : on les place sur n'importe quelle surface de la coque (sol, mur, plafond), elles se recyclent comme les autres.

**Assemblage** : visez une pièce déjà posée.
- Sur sa **face du dessus** (ou du dessous), la nouvelle pièce s'y pose (ou s'y accroche par en dessous).
- Sur un **côté**, elle se place contre lui en restant droite, sa base au niveau de celle de la pièce visée : planches côte à côte, cloisons en enfilade, vitre contre un poteau.

Les positions suivent une grille de 25 cm comptée depuis la pièce visée, et les pièces se touchent (planches et cloisons en continu). **Orientation** : pendant la pose, la touche **B** (réglage `BuildTiltKey`) fait passer la pièce de droite à basculée, sur le côté puis retournée : une cloison debout contre un mur de coque, un poteau couché au sol, une cloison de 2 m de long sur 1 m de haut. **Sur la coque**, les pièces s'alignent sur ses cases : une pièce de 1 m se centre sur une case, une de 2 m sur un bord de case, et les pièces fines (garde-corps, cloisons, vitres) se posent sur les bords de case comme les murs du jeu : deux garde-corps à angle droit se rejoignent au coin.

| Bois | Verre | Fer |
|---|---|---|
| Poteau 2 m | Vitre 1 × 1 | Caillebotis 1 × 1 |
| Plancher en lattes 1 × 1 | Baie vitrée 2 × 2 | Garde-corps 1 m et 2 m |
| Cloison en planches 1 × 2 | Dalle de verre 1 × 1 (on marche dessus) | Treillis métallique 2 m |
| Rambarde 1 m | Garde-corps vitré 1 m | Colonne rivetée 2,5 m |
| Treillis 1 × 1 | Verrière 1 × 1 | Gousset d'angle 1 × 1 |
| Étagère 1 m | Cloison vitrée 1 × 2 | Tôle perforée 1 × 1 |

Les anciennes **pièces de structure** (poutres, plaques 1 × 1 et 2 × 1, cadre, jonction) ont quitté le menu en 0.44 ; celles déjà posées restent.

Modèles retravaillés : vrai grain de bois, acier satiné, verre de la coque. La collision est la boîte de la pièce (le vide d'un treillis est visuel).

## Meubles

Quatrième rangée de l'onglet, tech **Charpenterie** ; ils se posent au sol (ou à côté d'une pièce posée) :

| Meuble | Taille | Coût |
|---|---|---|
| Canapé (3 places) | 2 × 0,9 m | 6 bois, 2 fer |
| Fauteuil | 0,9 × 0,9 m | 3 bois, 1 fer |
| Chaise | 0,45 × 0,5 m | 2 bois |
| Tabouret | 0,4 m | 1 bois, 1 fer |
| Table | 1,6 × 0,9 m | 4 bois, 2 fer |
| Table basse (plateau en verre) | 1,1 × 0,6 m | 2 verre, 2 fer, 1 bois |
| Bureau (caisson à tiroirs) | 1,4 × 0,7 m | 5 bois, 2 fer |
| Tapis | 2 × 3 m | 2 bois |
| Bibliothèque | 1 × 2 m de haut | 6 bois |
| Plante en pot | 1,2 m de haut | 2 bois, 1 verre |

Le tissu du canapé, du fauteuil et le tapis prennent la couleur du pistolet à peinture. **On s'assoit** sur le canapé (3 places), le fauteuil, la chaise, le tabouret, la table et la table basse : regarde-les et appuie sur **T** (place libre la plus proche) ; **T** ou une touche de déplacement pour se relever. Les autres joueurs te voient assis. Le bureau, le tapis, la bibliothèque et la plante sont décoratifs.

**Autres meubles** (rangée sous les meubles, Charpenterie) : lit, lits superposés, table de chevet avec lampe, armoire, commode, plan de travail de cuisine (évier, robinet), réfrigérateur, meuble TV, banc (3 places), chaise de bureau (on s'y assoit), table ronde, jardinière. Bois, fer, verre et cuivre seulement.

**Meubles futuristes** (rangée suivante, recherche *Modules de station*), pour équiper les salles d'un vaisseau :
- **science** : paillasse de labo, cuve à spécimen (liquide lumineux), console d'analyse ;
- **stockage** : baie de serveurs, caisse de fret, étagère de stockage, bonbonnes ;
- **dortoir** : capsule de sommeil, casiers d'équipage ;
- **cuisine** : synthétiseur de repas, îlot de cuisine ;
- **bureaux** : bureau de commandement (3 écrans), table holographique (planète qui tourne) ;
- **salon / mess** : canapé lounge (3 places), table de mess avec bancs (4 places), distributeur ;
- **infirmerie** : lit médical.

**Dormir** : le lit, les lits superposés, la capsule de sommeil et le lit médical : regarde-les et appuie sur **T** pour dormir et passer la nuit (comme l'attente du jeu). Pas pendant le vol.

## Escaliers

Rangée du haut de l'onglet. Ils se posent au sol et **montent vers l'arrière** (le pied du côté du joueur qui pose) ; tourne-les avant de poser. Leur collision est une rampe sous les marches : on y monte en marchant, comme sur les pentes de coque.

| Escalier | Taille | Recherche |
|---|---|---|
| Bois, bois doux | 1 m de haut sur 1 m / sur 2 m | Charpenterie |
| Industriel (caillebotis), industriel doux | 1 m sur 1 m / sur 2 m | Construction métallique |
| Verre (marches sur poutre chromée), verre doux | 1 m sur 1 m / sur 2 m | Verrerie architecturale |
| Futuriste (blocs sombres, nez de marche lumineux), futuriste doux | 1 m sur 1 m / sur 2 m | Modules de station |
| Colimaçon en acier, colimaçon futuriste | 2 m de haut, ¾ de tour, 2 × 2 m au sol | Construction métallique / Modules de station |

**À la suite** : vise un escalier droit déjà posé avec un autre escalier droit, il se pose au bout du premier, dans le même sens (on peut mélanger raide et doux) ; pour monter plus haut. Le colimaçon a besoin d'une trémie dans le plafond au-dessus.

## Panneaux et portes

Dernière rangée de l'onglet.
- **Panneau lumineux** (mur), **plaque de porte** (petite, à côté d'une porte), **enseigne holographique** (au sol, texte flottant à hauteur des yeux) : regarde-les et appuie sur **T** pour écrire le texte (40 caractères) et choisir sa couleur (7 couleurs). Pour nommer les pièces : Pont, Infirmerie, Soute… Tous les joueurs le voient, l'hôte le garde avec le monde. Recherche *Ordinateur de bord*.
- **Porte automatique** (un carré) et **grande porte automatique** (2 × 2) : elle s'ouvre seule quand quelqu'un approche et se referme derrière lui. Posée **en diagonale** (enlève le mur diagonal, pose la porte au sol à sa place, tourne-la pour l'autre diagonale) ; ou **droite** (dans l'alignement d'un mur normal) : choisis pendant la pose avec la touche **B** (l'aperçu montre le choix). Elle **ne retient pas l'air**. Recherche *Construction métallique*.
- **La porte du jeu en diagonale** : pose la porte du jeu comme un mur et tourne-la (touche de rotation) : sur un mur en diagonale, elle prend sa version diagonale, comme les murs du jeu. C'est la vraie porte : elle s'ouvre avec la touche d'interaction et ferme la pièce étanche.

## Coque renforcée

Onglet **TF Blindage**, recherche *Blindage renforcé*. Les pièces de coque du jeu en **fer** et en **verre** (sol, plaque, plaque pivotée, coins de sol et de mur, pente, coins de pente), en version renforcée : même modèle, même pose, mais une teinte un peu plus sombre quand on les pose (la peinture les recolore comme les autres). C'est de la vraie coque : elle ferme les pièces étanches comme celle du jeu.

- **3 fois plus solide** dans les collisions ;
- avec le mod **Arsenal** : les armes n'y font qu'**un tiers des dégâts** et n'y percent **aucun trou** ;
- **1,5 fois plus lourde** ;
- coût : le double de fer (ou de verre), plus 1 titane (fer renforcé) ou 1 fer (verre renforcé).

## Menu de construction

Au-dessus du menu de construction, une barre :
- **Rechercher une pièce** : tapez quelques lettres, toutes les pièces débloquées dont le nom les contient s'affichent (accents et majuscules ignorés) ; videz le champ pour revenir à l'onglet.
- **Mod** : choisissez un mod (le jeu de base, TF Advanced, TFA Water, Submarine Add-ons…) pour n'afficher que ses pièces, page par page (◀ ▶) ; la recherche filtre encore dans ce mod. « Tous les mods » revient aux onglets.
- **− / +** : rétrécit ou agrandit le menu (gardé d'une partie à l'autre, jamais plus grand que l'écran).
