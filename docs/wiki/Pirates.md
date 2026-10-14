# Pirates

*Avec le mod **Arsenal** seulement : les pirates se servent de ses armes.*

Quand votre vaisseau vole dans l'espace (en orbite ou entre les planètes), un groupe de pirates surgit de temps en temps à environ 2 km et vous attaque. En moyenne une attaque toutes les **12 minutes** passées dans l'espace (réglage `PirateIntervalMinutes`). Il n'y a jamais plus d'un groupe à la fois.

| Pirate | Coque | Armes | Pilotage |
|---|---|---|---|
| **Corsaire pirate** | 2 500 points | 2 mitrailleuses lourdes, 1 lance-missiles | rapide (80 m/s), se bat vers 170 m |
| **Croiseur pirate** (plus rare) | 7 000 points | 2 canons à plasma, 2 mitrailleuses, 3 lance-missiles, 1 défense rapprochée (abat vos missiles) | plus lent (50 m/s), se bat vers 260 m |

## Comment ils se battent

Le pilote pirate fonce sur votre vaisseau puis tourne autour de lui à sa distance de combat, le nez pointé sur vous. Ses mitrailleuses et canons tirent dès qu'il est aligné (moins de 650 m) ; ses missiles partent quand vous êtes dans leur cône (moins de 1 300 m). Près d'une planète, il garde de l'altitude au-dessus de vous.

## Les vaincre

- Toutes les armes d'Arsenal les touchent : les balles percent leur coque de fer, les explosions comptent beaucoup plus. Un corsaire tombe en **4 missiles** environ.
- Un **champ de force** (voir [Machines](Machines.md#champ-de-force)) encaisse leurs tirs à la place de la coque : le champ réactif ou le champ nucléaire sont faits pour le combat.
- La défense rapprochée d'Arsenal abat leurs missiles.

Détruit, un pirate explose et laisse une **capsule de butin** (un coffre sur une plaque) : titane, uranium, alliage nexorien, cryonite ; le croiseur ajoute des noyaux quantiques, de la météorite et un fragment d'artefact.

Un pirate laissé loin derrière (plus de 9 km) pendant 10 minutes s'en va.

Réglages (`[TFAdvanced]`) : `Pirates = false` pour ne plus en voir, `PirateIntervalMinutes` pour leur fréquence.
