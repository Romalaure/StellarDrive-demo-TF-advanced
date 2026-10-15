# Piratas

*Solo con el mod **Arsenal**: los piratas usan sus armas.*

Cuando tu nave vuela por el espacio (en órbita o entre planetas), de vez en cuando aparece un grupo de piratas a unos 2 km y te ataca. De media un ataque cada **12 minutos** pasados en el espacio (ajuste `PirateIntervalMinutes`). Nunca hay más de un grupo a la vez.

## Peligro: cuanto más lejos, más duros

Los piratas son más peligrosos cuanto más te alejas del planeta de inicio. El **peligro** depende de la distancia entre la nave atacada y 442 Otrera b: un nivel más cada **5 millones de km** (ajuste `PirateDangerStepKm`).

| Peligro | Dónde | Piratas | Ataques |
|---|---|---|---|
| 1 | alrededor del planeta de inicio, TF Sylvara, TF Petra (menos de 5 M km) | solo exploradores, 1 o 2 | 1,5 veces menos a menudo |
| 2 | TF Aridia (5 a 10 M km) | corsarios y avispones, a veces exploradores | 1,2 veces menos a menudo |
| 3 | TF Verdance, TF Glacia (10 a 15 M km) | saqueadores y lanceros, a veces corsarios y avispones | normal |
| 4 | TF Nexora, TF Altis y más allá (más de 15 M km) | cruceros y torpederos, a menudo escoltados, a veces saqueadores y lanceros | algo más a menudo |
| 5 | galaxia de Érebo | acorazados, cruceros y torpederos, escoltados | aún más a menudo (4 veces más alrededor del Bastión) |

Cada ataque sortea un pirata del nivel del peligro (el doble de a menudo) o del nivel justo inferior. Un crucero, un torpedero o un acorazado llega a menudo con una **escolta** de 1 o 2 cazas: exploradores o avispones, corsarios o avispones en Érebo.

| Pirata | Nivel | Casco | Armas | Vuelo | Grupo |
|---|---|---|---|---|---|
| **Explorador** (gris, luces amarillas) | 1 | 1.200 puntos | 1 ametralladora, mala puntería | muy rápido (95 m/s), combate a unos 150 m | 2 o 3 (1 o 2 cerca del planeta de inicio) |
| **Corsario** (rojo) | 2 | 2.500 puntos | 2 ametralladoras, 1 lanzamisiles | rápido (80 m/s), unos 170 m | 1 o 2 |
| **Avispón** (amarillo con rayas negras, mandíbulas, aguijón bajo la cola) | 2 | 1.800 puntos | 1 lanzacohetes (9 cohetes seguidos), 1 ametralladora | el más rápido (105 m/s), unos 140 m | 2 o 3 |
| **Saqueador** (negro, luces naranjas) | 3 | 4.200 puntos | 2 ametralladoras, 1 cañón láser, 1 lanzamisiles | 70 m/s, unos 210 m | solo |
| **Lancero** (azul acero, lanza con anillos bajo el morro) | 3 | 3.600 puntos | 1 cañón de riel, 2 cañones de iones cuyos impactos bloquean tus armas | 75 m/s, dispara de lejos (unos 520 m, hasta 900 m) | 1 o 2 |
| **Crucero** (violeta oscuro) | 4 | 7.000 puntos | 2 cañones de plasma, 2 ametralladoras, 3 lanzamisiles, defensa puntual | lento (50 m/s), unos 260 m | solo |
| **Torpedero** (caqui, dos tubos bajo el vientre, antena de radar) | 4 | 6.000 puntos | 2 lanzatorpedos, 2 ametralladoras, batería antiaérea, defensa puntual | 60 m/s, unos 320 m | solo |
| **Acorazado** (rojo sangre, torre de mando, espolón) | 5 | 14.000 puntos | 2 cañones de plasma, 1 cañón de riel, 2 cañones de iones, 2 ametralladoras, 2 lanzatorpedos, 2 lanzamisiles, batería antiaérea, 2 defensas puntuales | muy lento (40 m/s), unos 340 m, dispara hasta 800 m | solo |

Tienen una verdadera forma de nave: morro puntiagudo con un espolón bajo la cabina acristalada, cúpula, cresta dorsal, alas en flecha con góndolas y luces en las puntas, derivas inclinadas, góndolas de motor con toberas brillantes, blindaje lateral remachado, antena. Los nuevos tienen cada uno sus piezas: rayas, mandíbulas y aguijón del avispón, lanza con anillos y aletas sensoras del lancero, tubos ventrales y antena de radar del torpedero, torre de mando con ventanas iluminadas, costillas de blindaje y espolón del acorazado. Este carenado no detiene los disparos: atraviesan hasta el casco de hierro que cubre.

**Ráfagas y recarga**: sus ametralladoras, láseres y cañones ya no disparan sin parar sino en ráfagas, seguidas de un tiempo de recarga:

| Pirata | Ráfaga | Recarga | Puntería | Misiles |
|---|---|---|---|---|
| Explorador | 1,2 s | 4 s | ± 4° | ninguno |
| Corsario | 1,5 s | 3,5 s | ± 2,5° | cada 8 s |
| Saqueador | 2 s | 3 s | ± 1,5° | cada 6 s |
| Crucero | 2,5 s | 3 s | ± 1° | cada 4 s |
| Avispón | 1,4 s | 3,5 s | ± 3° | ninguno (cohetes durante la ráfaga) |
| Lancero | 2,5 s | 2,5 s | ± 1,2° | ninguno |
| Torpedero | 2 s | 3 s | ± 1° | torpedos cada 7 s |
| Acorazado | 3 s | 2,5 s | ± 0,7° | torpedos y misiles cada 5 s |


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
- saca cazas de su hangar (exploradores, avispones o un corsario) cada 2 min 30 (a menos de 4 km), 3 como máximo a la vez.

Destruida, explota y deja una gran cápsula de botín (núcleos cuánticos, materia exótica, aleación nexoriana, titanio, uranio, artefactos); sus restos y cofres se quedan, no vuelve. Alrededor del Bastión (5.000 km), los ataques piratas son **cuatro veces más frecuentes**.

## Cómo combaten

El piloto pirata se lanza hacia tu nave y luego la rodea a su distancia de combate, con el morro apuntándote (los menos hábiles apuntan peor). Sus ametralladoras y cañones disparan en ráfagas cuando está alineado (a menos de 650 m, 800 m el acorazado, 900 m el lancero); sus misiles salen cuando estás en su cono (a menos de 1.300 m). Cerca de un planeta, se mantiene algo por encima de ti.

## Vencerlos

- Todas las armas de Arsenal les hacen daño: las balas atraviesan su casco de hierro, las explosiones cuentan mucho más. Un explorador cae con 2 misiles, un avispón con 3, un corsario con 4, un lancero con 6, un saqueador con 7, un torpedero con 10, un acorazado con unos 22 (torpedero y acorazado tienen batería antiaérea y defensas puntuales que derriban parte de tus misiles), un crucero con unos 11.
- Un **campo de fuerza** (ver [Máquinas](Machines.md#campo-de-fuerza)) recibe sus disparos en lugar del casco: el campo rápido o el campo nuclear están hechos para el combate.
- La defensa puntual de Arsenal derriba sus misiles.

Destruido, un pirata explota y deja una **cápsula de botín** (un cofre sobre una plataforma), más rica según su dificultad: titanio, uranio y criónita para el explorador; además aleación nexoriana para el corsario; núcleo cuántico y meteorito para el saqueador; el crucero añade un fragmento de artefacto. El avispón deja algo más que el explorador, el lancero un núcleo cuántico, el torpedero dos núcleos y meteorito, el acorazado dos artefactos y materia exótica.

Un pirata dejado muy atrás (más de 9 km) durante 10 minutos se marcha.

Ajustes (`[TFAdvanced]`): `Pirates = false` para no verlos más, `PirateIntervalMinutes` para su frecuencia, `PirateDangerStepKm` para la distancia entre dos niveles de peligro.
