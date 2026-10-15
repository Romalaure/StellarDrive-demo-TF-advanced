# Piratas

*Solo con el mod **Arsenal**: los piratas usan sus armas.*

Cuando tu nave vuela por el espacio (en órbita o entre planetas), de vez en cuando aparece un grupo de piratas a unos 2 km y te ataca. De media un ataque cada **12 minutos** pasados en el espacio (ajuste `PirateIntervalMinutes`). Nunca hay más de un grupo a la vez.

Cada ataque sortea un nivel de dificultad:

| Pirata | Dificultad | Casco | Armas | Vuelo | Grupo | Probabilidad |
|---|---|---|---|---|---|---|
| **Explorador** (gris, luces amarillas) | fácil | 1.200 puntos | 1 ametralladora, mala puntería | muy rápido (95 m/s), combate a unos 150 m | 2 o 3 | 35 % |
| **Corsario** (rojo) | media | 2.500 puntos | 2 ametralladoras, 1 lanzamisiles | rápido (80 m/s), unos 170 m | 1 o 2 | 35 % |
| **Saqueador** (negro, luces naranjas) | difícil | 4.200 puntos | 2 ametralladoras, 1 cañón láser, 1 lanzamisiles | 70 m/s, unos 210 m | solo | 20 % |
| **Crucero** (violeta oscuro) | muy difícil | 7.000 puntos | 2 cañones de plasma, 2 ametralladoras, 3 lanzamisiles, defensa puntual | lento (50 m/s), unos 260 m | solo | 10 % |

Tienen una verdadera forma de nave: morro puntiagudo con un espolón bajo la cabina acristalada, cúpula, cresta dorsal, alas en flecha con góndolas y luces en las puntas, derivas inclinadas, góndolas de motor con toberas brillantes, blindaje lateral remachado, antena. Este carenado no detiene los disparos: atraviesan hasta el casco de hierro que cubre.

**Ráfagas y recarga**: sus ametralladoras, láseres y cañones ya no disparan sin parar sino en ráfagas, seguidas de un tiempo de recarga:

| Pirata | Ráfaga | Recarga | Puntería | Misiles |
|---|---|---|---|---|
| Explorador | 1,2 s | 4 s | ± 4° | ninguno |
| Corsario | 1,5 s | 3,5 s | ± 2,5° | cada 8 s |
| Saqueador | 2 s | 3 s | ± 1,5° | cada 6 s |
| Crucero | 2,5 s | 3 s | ± 1° | cada 4 s |


## Radares

Tres radares (pestaña **TF Avancé**, fila de combate) detectan a los piratas alrededor de la nave:

| Radar | Alcance | Energía | Coste |
|---|---|---|---|
| **Radar de corto alcance** (brillo verde) | 1.000 km | 400 W | 10 de cobre, 6 de vidrio, 6 de hierro |
| **Radar de largo alcance** (brillo dorado) | 5.000 km | 1.200 W | 20 de cobre, 10 de vidrio, 8 de titanio, 4 de cuarzo |
| **Radar de espacio profundo** (brillo violeta) | 20.000 km | 3.200 W | 30 de cobre, 15 de titanio, 8 de cuarzo, 6 de criónita, 1 núcleo cuántico |

Necesita energía en sus tomas de energía (la red debe poder dársela toda). Su **pantalla redonda** muestra los contactos vistos desde arriba, el frente del radar arriba: distancia en escala logarítmica (anillos a 10, 100, 1.000 y 10.000 km), un barrido gira e ilumina los ecos al pasar. Al lado, la lista de los contactos más cercanos: naves piratas (rojo), **bases** (naranja) e **incursiones que llegan** con su tiempo antes del contacto. Su toma de señal da el número de contactos (conéctala a una alarma); apagado: -1 sin red eléctrica, -3 energía insuficiente.

**Incursiones que llegan**: un grupo pirata ya no aparece de golpe. Se detecta a **25.000 km** (ajuste `PirateRaidStartKm`) y se lanza hacia su objetivo a **250 km/s** (`PirateApproachKmPerSecond`) antes de saltar a su lado. Un radar lo ve en cuanto entra en su alcance: unos **80 s** de aviso con el radar de espacio profundo, **20 s** con el de largo alcance, **4 s** con el de corto alcance. Cuando un radar cerca de ti ve llegar una incursión, aparece una **alerta** en la parte superior de la pantalla (ajuste `RadarAlarm`).

## Generador de sigilo

El **generador de sigilo** (6.000 W, ajuste `CloakWatts`) difumina la firma de la nave y de todo lo acoplado a ella en los sensores piratas mientras recibe energía (sus anillos giran, el cristal brilla):
- las incursiones encuentran la nave **tres veces menos**, y una incursión ya en camino puede **perder su rastro** (alrededor de una probabilidad entre cinco cada 5 s);
- los piratas solo la ven a menos de **2,5 km** (las bases a menos de **1,2 km**); más lejos, la pierden;
- deben acercarse más para disparar (**400 m** en lugar de 650), **apuntan mucho peor** y fijan sus misiles más tarde (**800 m**, cono más estrecho);
- las bases disparan sus misiles a menos de 1,2 km en lugar de 2,5 km y lanzan sus cazas a menos de 1,5 km en lugar de 4 km.

Coste: 2 núcleos cuánticos, 10 de criónita, 8 de cuarzo, 12 de titanio, 15 de cobre. Su toma de señal da 1 mientras oculta la nave. Los radares y el generador se desbloquean con la tecnología **Guerra electrónica** (después de Cartografía).

## Zona segura

Alrededor del planeta de inicio, **442 Otrera b**, hay una zona segura de **2.000 km** (ajuste `PirateSafeRadiusKm`, en km desde su centro; 0 la elimina). Allí no hay ataques piratas, y un combate que entra en ella se interrumpe: los piratas se van tras 15 s.

## Bases piratas y Bastion Écarlate

En la galaxia de Érebo, los piratas tienen su propio planeta, [Bastion Écarlate](Bastion.md) (el Bastión Escarlata), un planeta artificial cubierto de fortalezas, y tres **bases** armadas, visibles en el mapa estelar (sus balizas conservan sus nombres en francés):

| Base | Dónde | Armas |
|---|---|---|
| Forteresse Écarlate (Fortaleza Escarlata) | órbita del Bastión | 8 ametralladoras, 4 cañones de plasma, 3 defensas puntuales |
| Chantier de la Rouille (Astillero del Óxido) | órbita del Bastión | 7 ametralladoras, 1 cañón láser, 2 defensas puntuales |
| Avant-poste du Croc (Puesto del Colmillo) | órbita de Pyros | 5 ametralladoras, 1 defensa puntual |

Una base tiene **20.000 puntos de casco** y no se mueve. Cuando una nave se acerca:
- sus cañones que apuntan hacia ella disparan en ráfagas (a menos de 900 m);
- lanza salvas de 2 misiles guiados cada 5 s (a menos de 2,5 km);
- saca cazas de su hangar (exploradores o un corsario) cada 2 min 30 (a menos de 4 km), 3 como máximo a la vez.

Destruida, explota y deja una gran cápsula de botín (núcleos cuánticos, materia exótica, aleación nexoriana, titanio, uranio, artefactos); sus restos y cofres se quedan, no vuelve. Alrededor del Bastión (5.000 km), los ataques piratas son **cuatro veces más frecuentes**.

## Cómo combaten

El piloto pirata se lanza hacia tu nave y luego la rodea a su distancia de combate, con el morro apuntándote (los menos hábiles apuntan peor). Sus ametralladoras y cañones disparan en ráfagas cuando está alineado (a menos de 650 m); sus misiles salen cuando estás en su cono (a menos de 1.300 m). Cerca de un planeta, se mantiene algo por encima de ti.

## Vencerlos

- Todas las armas de Arsenal les hacen daño: las balas atraviesan su casco de hierro, las explosiones cuentan mucho más. Un explorador cae con 2 misiles, un corsario con 4, un saqueador con 7, un crucero con unos 11.
- Un **campo de fuerza** (ver [Máquinas](Machines.md#campo-de-fuerza)) recibe sus disparos en lugar del casco: el campo rápido o el campo nuclear están hechos para el combate.
- La defensa puntual de Arsenal derriba sus misiles.

Destruido, un pirata explota y deja una **cápsula de botín** (un cofre sobre una plataforma), más rica según su dificultad: titanio, uranio y criónita para el explorador; además aleación nexoriana para el corsario; núcleo cuántico y meteorito para el saqueador; el crucero añade un fragmento de artefacto.

Un pirata dejado muy atrás (más de 9 km) durante 10 minutos se marcha.

Ajustes (`[TFAdvanced]`): `Pirates = false` para no verlos más, `PirateIntervalMinutes` para su frecuencia.
