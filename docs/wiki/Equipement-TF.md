# Équipement TF

Les objets du mod **StellarDrive Demo TF** ont rejoint TF Advanced (v0.18), dans l'onglet de construction **TF Équipement** :

| Objet | Rôle |
|---|---|
| **15 luminaires** (plafonnier, applique, projecteur, réglette, spot encastré, lampe industrielle, balise clignotante, gyrophare...) | éclairent les murs, sols et pièces autour d'eux, plus fort la nuit et à l'intérieur ; prennent la couleur peinte avec le pistolet du mod TF |
| **Caméra de surveillance** | à fixer n'importe où ; la **tablette** (touche **F9**) montre son image, flèches pour changer de caméra, Z pour la taille |
| **Capsule de téléportation** | regarde-la et appuie sur **T** : choisis une autre capsule (nommée) et tu y es téléporté. Socle à émetteur tournant, piliers, paroi arrière lumineuse, console ; ses anneaux d'énergie montent et descendent, plus vite quand tu es dedans, et s'emballent au départ |
| **Radio** | touche **T** : joue une musique (fichier ou lien YouTube) entendue par tous les joueurs autour |
| **Râtelier à outils** | touche **T** : reprendre un outil de base perdu (construction, recyclage, laser de minage), le pistolet à peinture, la tablette schématique ; te dit si tu l'as déjà |
| **Générateur, réservoirs d'oxygène et d'éthanol, coffre de ressources infinis** | sans fin, mais **extrêmement chers** : des centaines de fer, cuivre ou verre, 8 noyaux quantiques, 10 matières exotiques, 6 artefacts, 40 alliages nexoriens et 30 météorites chacun |

**Radio, muet** : dans la fenêtre de la radio, « Muet pour moi » la coupe pour toi seul. L'hôte y voit la liste des joueurs et peut bloquer ceux qui n'ont pas le droit de changer la musique (`RadioLockedPlayers`).

Mêmes numéros de pièces qu'avant : ce qui est déjà construit dans vos mondes reste. Gardez le mod TF à jour (0.15 ou plus) : il cède ces objets à TF Advanced (chaises, garde-robe et portes d'amarrage restent dans le mod TF). Avec un mod TF plus ancien, c'est lui qui les garde, sans doublon.

## Pistolet à peinture, tablette schématique, vue à la troisième personne

Sans le mod TF, TF Advanced fournit aussi (mêmes objets, mêmes fichiers : rien n'est perdu) :
- le **pistolet à peinture** du mod TF : il peint les murs et sols, mais aussi les portes, machines et toutes les pièces posées, avec une finition (brillance, métal, lumineux), des préréglages et des **camouflages** (forêt, désert, arctique...). Prends-le au râtelier, **clic gauche** peindre, **clic droit** palette, **molette** pipette. Les couleurs sont gardées avec le monde (`tf-paint.json`) ;
- la **tablette schématique** : prends-la au râtelier, sélectionne-la dans ta barre d'outils, **clic gauche** pour ouvrir la fenêtre. **Copier** enregistre le vaisseau le plus proche (et ce qui y est attaché) ; **Construire** le recrée gratuitement devant toi. Les schématiques sont sur ton PC dans `UserData/TF/schematics`, partagées entre tous tes mondes. La peinture du vaisseau est copiée avec ;
- la **vue à la troisième personne** en pilotant : touche **V** (`ThirdPersonKey`), molette pour la distance (`ThirdPersonDistance`) ;
- les touches des sièges du mod **KeySeat** enregistrées tout de suite et recopiées par les schématiques.

Si le mod TF est installé, c'est lui qui les fournit, sans doublon.

Réglages : section `[TFAdvancedObjects]` (touche de la tablette `TabletKey`, touche d'utilisation `UseKey`, images par seconde de la caméra, force de l'éclairage des lampes `LampLightStrength`, volume de la radio).
