# Pirates

*Avec le mod **Arsenal** seulement : les pirates se servent de ses armes.*

Quand votre vaisseau vole dans l'espace (en orbite ou entre les planètes), un groupe de pirates arrive de temps en temps : repéré au loin (25 000 km), il fonce vers vous et surgit à environ 2 km pour attaquer (voir [Radars](#radars)). En moyenne une attaque toutes les **12 minutes** passées dans l'espace (réglage `PirateIntervalMinutes`). Il n'y a jamais plus d'un groupe à la fois.

Chaque attaque tire au hasard un niveau de difficulté :

| Pirate | Difficulté | Coque | Armes | Pilotage | Groupe | Chance |
|---|---|---|---|---|---|---|
| **Éclaireur** (gris, feux jaunes) | facile | 1 200 points | 1 mitrailleuse, tir imprécis | très rapide (95 m/s), se bat vers 150 m | 2 ou 3 | 35 % |
| **Corsaire** (rouge) | moyen | 2 500 points | 2 mitrailleuses, 1 lance-missiles | rapide (80 m/s), vers 170 m | 1 ou 2 | 35 % |
| **Pillard** (noir, feux orange) | difficile | 4 200 points | 2 mitrailleuses, 1 canon laser, 1 lance-missiles | 70 m/s, vers 210 m | seul | 20 % |
| **Croiseur** (violet sombre) | très difficile | 7 000 points | 2 canons à plasma, 2 mitrailleuses, 3 lance-missiles, défense rapprochée | lent (50 m/s), vers 260 m | seul | 10 % |

Ils ont une vraie silhouette de vaisseau : nez en pointe avec éperon sous le cockpit vitré, verrière, arête dorsale, ailes en flèche avec nacelles de bout d'aile et feux, dérives inclinées, nacelles moteur aux tuyères rougeoyantes, blindage latéral riveté, antenne. Ce carénage n'arrête pas les tirs : ils percent la coque de fer qu'il habille.

**Rafales et rechargement** : leurs mitrailleuses, lasers et canons ne tirent plus en continu mais par rafales, suivies d'un temps de rechargement :

| Pirate | Rafale | Rechargement | Visée | Missiles |
|---|---|---|---|---|
| Éclaireur | 1,2 s | 4 s | ± 4° | aucun |
| Corsaire | 1,5 s | 3,5 s | ± 2,5° | toutes les 8 s |
| Pillard | 2 s | 3 s | ± 1,5° | toutes les 6 s |
| Croiseur | 2,5 s | 3 s | ± 1° | toutes les 4 s |

## Radars

Trois radars (onglet **TF Avancé**, rangée combat) détectent les pirates autour du vaisseau :

| Radar | Portée | Courant | Coût |
|---|---|---|---|
| **Radar courte portée** (lueur verte) | 1 000 km | 400 W | 10 cuivre, 6 verre, 6 fer |
| **Radar longue portée** (lueur dorée) | 5 000 km | 1 200 W | 20 cuivre, 10 verre, 8 titane, 4 quartz |
| **Radar de l'espace profond** (lueur violette) | 20 000 km | 3 200 W | 30 cuivre, 15 titane, 8 quartz, 6 cryonite, 1 noyau quantique |

Il lui faut du courant sur ses prises électriques (le réseau doit pouvoir le fournir en entier). Son **écran rond** montre les contacts vus du dessus, l'avant du radar en haut : la distance est en échelle logarithmique (anneaux à 10, 100, 1 000 et 10 000 km), un balayage tourne et allume les échos à son passage. À côté, la liste des contacts les plus proches : vaisseaux pirates (rouge), **bases** (orange) et **raids en approche** avec leur temps avant contact. Sa prise signal donne le nombre de contacts (à brancher sur une alarme) ; éteint : -1 pas de réseau électrique, -3 pas assez de courant.

**Raids en approche** : un groupe de pirates ne surgit plus d'un coup. Il est repéré à **25 000 km** (réglage `PirateRaidStartKm`) et fonce vers sa cible à **250 km/s** (`PirateApproachKmPerSecond`) avant de sauter à côté d'elle. Un radar le voit dès qu'il entre dans sa portée : environ **80 s** d'avance avec le radar de l'espace profond, **20 s** avec le radar longue portée, **4 s** avec le radar courte portée. Quand un radar près de vous voit un raid arriver, une **alerte** s'affiche en haut de l'écran (réglage `RadarAlarm`).

## Générateur d'invisibilité

Le **générateur d'invisibilité** (6 000 W, réglage `CloakWatts`) brouille la signature du vaisseau et de tout ce qui y est amarré sur les capteurs pirates tant qu'il est alimenté (ses anneaux tournent, le cristal brille) :
- les raids trouvent le vaisseau **trois fois moins souvent**, et un raid déjà en route peut **perdre sa trace** (environ une chance sur cinq toutes les 5 s) ;
- les pirates ne le voient qu'à moins de **2,5 km** (les bases à moins de **1,2 km**) ; plus loin, ils le perdent ;
- ils doivent s'approcher pour tirer (**400 m** au lieu de 650), **visent bien plus mal** et verrouillent leurs missiles plus tard (**800 m**, cône plus étroit) ;
- les bases tirent leurs missiles à moins de 1,2 km au lieu de 2,5 km et lancent leurs chasseurs à moins de 1,5 km au lieu de 4 km.

Coût : 2 noyaux quantiques, 10 cryonite, 8 quartz, 12 titane, 15 cuivre. Sa prise signal vaut 1 quand il masque le vaisseau. Radars et générateur se débloquent avec la tech **Guerre électronique** (après Cartographie).

## Zone sûre

Autour de la planète de départ, **442 Otrera b**, s'étend une zone sûre de **2 000 km** (réglage `PirateSafeRadiusKm`, en km depuis son centre ; 0 pour l'enlever). Aucun pirate n'y attaque, et un combat qui y entre est rompu : les pirates s'en vont au bout de 15 s.

## Bases pirates et Bastion Écarlate

Dans la galaxie d'Érèbe, les pirates ont leur planète, le [Bastion Écarlate](Bastion.md), une planète artificielle couverte de forteresses, et trois **bases** armées, visibles sur la carte stellaire :

| Base | Où | Armement |
|---|---|---|
| Forteresse Écarlate | orbite du Bastion | 8 mitrailleuses, 4 canons à plasma, 3 défenses rapprochées |
| Chantier de la Rouille | orbite du Bastion | 7 mitrailleuses, 1 canon laser, 2 défenses rapprochées |
| Avant-poste du Croc | orbite de Pyros | 5 mitrailleuses, 1 défense rapprochée |

Une base a **20 000 points** de coque et ne bouge pas. Quand un vaisseau approche :
- ses canons qui pointent vers lui tirent par rafales (à moins de 900 m) ;
- elle lance des salves de 2 missiles autoguidés toutes les 5 s (à moins de 2,5 km) ;
- elle sort des chasseurs de son hangar (éclaireurs ou corsaire) toutes les 2 min 30 (à moins de 4 km), 3 au plus à la fois.

Détruite, elle explose et laisse une grosse capsule de butin (noyaux quantiques, matière exotique, alliage nexorien, titane, uranium, artefacts) ; son épave et ses coffres restent, elle ne revient pas. Autour du Bastion (5 000 km), les attaques de pirates sont **quatre fois plus fréquentes**.

## Comment ils se battent

Le pilote pirate fonce sur votre vaisseau puis tourne autour de lui à sa distance de combat, le nez pointé sur vous (les moins doués visent moins bien). Ses mitrailleuses et canons tirent par rafales quand il est aligné (moins de 650 m) ; ses missiles partent quand vous êtes dans leur cône (moins de 1 300 m). Près d'une planète, il garde de l'altitude au-dessus de vous.

## Les vaincre

- Toutes les armes d'Arsenal les touchent : les balles percent leur coque de fer, les explosions comptent beaucoup plus. Un éclaireur tombe en 2 missiles, un corsaire en 4, un pillard en 7, un croiseur en 11 environ.
- Un **champ de force** (voir [Machines](Machines.md#champ-de-force)) encaisse leurs tirs à la place de la coque : le champ réactif ou le champ nucléaire sont faits pour le combat.
- La défense rapprochée d'Arsenal abat leurs missiles.

Détruit, un pirate explose et laisse une **capsule de butin** (un coffre sur une plaque), plus riche selon sa difficulté : titane, uranium et cryonite pour l'éclaireur ; alliage nexorien en plus pour le corsaire ; noyau quantique et météorite pour le pillard ; le croiseur ajoute un fragment d'artefact.

Un pirate laissé loin derrière (plus de 9 km) pendant 10 minutes s'en va.

Réglages (`[TFAdvanced]`) : `Pirates = false` pour ne plus en voir, `PirateIntervalMinutes` pour leur fréquence.
