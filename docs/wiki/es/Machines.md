# Máquinas

Pestaña de construcción **TF Avancé**. Las máquinas se desbloquean en la [mesa de investigación](Recherche.md).

## Energía

| Máquina | Efecto | Tomas |
|---|---|---|
| Batería | almacena energía (1.200 kJ), se carga con el excedente de los generadores | 2 de energía, 1 de señal (carga %) |
| Disyuntor | une dos redes, corta en caso de sobrecarga (4.000 W) | entrada, salida, estado, mando |
| Reactor cuántico | 12.000 W sin combustible | 2 de energía, 1 de señal |
| Batería de criónita | 8 baterías, carga y descarga ×3 | como la batería |
| Condensador de criónita | 0,05 de oxígeno/s por 1.500 W | 2 de energía, 1 de fluido, 1 de señal |
| Giroscopio cuántico | el par de 5 giroscopios | como el giroscopio |
| Soporte de paneles solares | hasta 3.000 W de cara a la estrella, nada de noche o a la sombra de un planeta | 2 de energía, 1 de señal (W) |

## Láser de minería de nave

El láser de minería del jugador, en grande, sobre una torreta que se fija al casco (suelo, pared, techo, bajo la nave). Su rayo sale recto de la superficie donde se coloca, hasta **40 m**, y mina las rocas, cristales y minerales de asteroide que toca, **3 veces más rápido** que la herramienta. Lo que extrae no va a tu inventario: va al **cofre unido por una línea de objetos**.

1. Coloca el láser, con la torreta mirando hacia lo que quieres minar.
2. Conecta energía a una de sus tomas eléctricas (**800 W** mientras dispara).
3. Une su **toma de objetos** (la trampilla amarilla, en medio de la fila de tomas) con una **línea de objetos** a un cofre que tenga una: el **terminal de objetos** o la caja de suministros del mod *Item Lines*, o una máquina de almacenamiento con puerto de objetos. Sin cofre unido no dispara.
4. Toma de **mando**: sin cable dispara en cuanto tiene energía; con cable (palanca, botón, asiento) dispara mientras la señal es mayor que 0.

Toma de **estado**: 2 mina, 1 dispara sin nada que minar, 0 apagado, -1 sin energía, -2 sin cofre, -3 cofre lleno (espera a que se vacíe). La minería se hace en el anfitrión, cerca de un jugador (las rocas solo existen donde hay alguien). Investigación *Minería embarcada*.

## Campo de fuerza

Envuelve toda la nave (y lo que está acoplado a ella) en una burbuja cian que recibe los impactos y las fuerzas de marea de los agujeros negros: **150 puntos** (un impacto cuenta su cambio de velocidad en m/s). Consume 2.000 W y 0,02 de oxígeno/s, se recarga 0,5 puntos/s. A cero se rompe y se reinicia **5 minutos** después. La burbuja es una capa de energía: casi invisible de frente, brillante en los bordes, cubierta por una malla hexagonal, recorrida por una banda de escaneo ascendente; cada impacto lanza una onda sobre la burbuja. Se vuelve roja a medida que baja la carga y parpadea cuando está casi vacía.

**Con el mod Arsenal**: el campo también recibe sus armas (y las de los [piratas](Pirates.md)). Las balas y la metralla que alcanzan el casco, y la onda expansiva sobre las piezas, vacían el campo en lugar de dañar la nave (daño a las placas ÷ 4, potencia de la explosión ÷ 20: la mayor explosión se lleva cerca de la mitad de un campo lleno). Mientras aguanta, las explosiones ya no empujan la nave. Los misiles y cohetes que entran en la burbuja explotan contra ella, con una onda donde golpean. Toma de señal: carga en % (-1 apagado, negativo: segundos antes del reinicio).

### Los seis generadores

| Generador | Puntos | Recarga | Energía | Fluido | Reinicio | Investigación |
|---|---|---|---|---|---|---|
| **Campo compacto** (blanco azulado, pequeño) | 60 | 0,3 /s | 600 W | 0,01 de oxígeno/s | 5 min | Tecnología de artefactos |
| Campo de fuerza (cian) | 150 | 0,5 /s | 2.000 W | 0,02 de oxígeno/s | 5 min | Tecnología de artefactos |
| **Campo rápido** (naranja) | 300 | 6 /s | 5.000 W | 0,03 de oxígeno/s | **1 min** | Tecnología de artefactos |
| **Campo reforzado** (dorado) | 450 | 1,5 /s | 6.000 W | 0,05 de oxígeno/s | 5 min | Tecnología de artefactos |
| **Campo nuclear** (verde) | 750 | 2,5 /s | 8.000 W | 0,01 de combustible nuclear/s | 5 min | Energía nuclear |
| **Campo de materia oscura** (violeta) | 1.200 | 4 /s | 10.000 W | 0,02 de materia oscura/s | 5 min | Materia oscura |

Mismo comportamiento y tomas para todos; la burbuja toma el color del generador. Varios generadores en una nave: el más cargado recibe los golpes. Tienen su propia fila en la pestaña TF Avancé.

### ¿No se enciende?

La red eléctrica debe poder darle **toda** su energía (junto con las demás máquinas de la red). Un generador de madera da 4.000 W, un reactor cuántico 12.000 W (6.000 W por toma si alimenta dos redes), un reactor nuclear 40.000 W. El generador muestra lo que le falta:
- **anillos rojos parpadeantes**, señal **-3**: energía insuficiente. Deja de pedir durante 5 s y vuelve a intentarlo, para no dejar sin energía al resto de la nave;
- **anillos naranjas parpadeantes**, señal **-2**: sin fluido (fluido equivocado, línea vacía o caudal demasiado bajo);
- anillos apagados, señal **-1**: ninguna toma de energía conectada;
- señal **-10 y menos**: se rompió, se reinicia en (−valor − 10) segundos.

## Energía nuclear

Entre la energía de artefactos y la materia oscura. El **uranio** se extrae en TF Aridia, TF Petra y Pyros (rocas verdosas con vetas brillantes).

| Máquina | Efecto |
|---|---|
| Centrifugadora de enriquecimiento | recibe mineral de uranio en su trampilla (a mano o por una línea de objetos) y lo convierte en **combustible nuclear** (fluido) en la línea de su toma de fluido: 0,05 mineral/s, 0,2 unidades de combustible por mineral, 6.000 W. Se conecta como el destilador de etanol del juego |
| Tanque de combustible nuclear | el tanque del juego, blindado (acepta todos los fluidos) |
| Reactor nuclear | **40.000 W** por 0,002 unidad/s de combustible nuclear; vasija a presión, barras de control, circuitos de refrigeración |
| Propulsor nuclear | **93.600 N** (12 Propulsores químicos) por 0,002 unidad/s de combustible y 0,02 de oxígeno |
| Motor nuclear pesado | **234.000 N** (30 Propulsores químicos) por 0,005 unidad/s de combustible y 0,05 de oxígeno; turbobombas y radiadores |

Los motores nucleares y de materia oscura se conectan como el Propulsor químico, pero una toma recibe su combustible y la otra **oxígeno**, en cualquier orden. Tres formas de controlarlos:
- **toma de mando** (toma de señal en la parte trasera de su base): una señal en ella fija su empuje, de 0 a 1. Un cable, o un pequeño transmisor conectado directamente a la toma: un transmisor conectado ahí oye su canal en todo el conjunto, góndolas de rotor incluidas (el juego solo une los canales dentro de una misma nave);
- **válvula o bomba** en su línea, como el Propulsor químico: solo si la línea que va al motor no tiene tanque (el combustible solo llega por la válvula); empujan cuando la válvula deja pasar combustible;
- **conectados directamente** a sus tanques, sin señal: obedecen al **asiento del piloto** y solo empujan si un piloto sentado en un asiento de la nave (o de una nave acoplada) pide empuje en su dirección (adelante, atrás, arriba, abajo; el asiento avanzado también se desplaza de lado).

Ajuste `EngineSeatControl = false`: conectados directamente, empujan en cuanto están alimentados. Un mineral de uranio hace funcionar un reactor nuclear durante 100 s.

## Generador de portal

Abre un portal violeta **45 m delante de él** (en la dirección de su cara frontal), fijo en el espacio, de 18 m de radio. La nave que lo atraviesa sale junto al **objetivo elegido en el ordenador de navegación o el mapa estelar**:
- un planeta: a unos 20 km de altitud, por el lado desde el que llegas;
- una baliza (estación, restos, nave): a 300 m.

Funcionamiento: una señal > 0 en la **toma de mando** lo activa; se carga durante **10 s** a 8.000 W (con menos energía se carga más despacio), quema **3 de materia oscura** (toma de fluido), luego el portal queda abierto **30 s**, y el generador se recarga durante 60 s. Toma de estado: 0 listo, 1 a 99 % de carga, 100 abierto, -1 sin objetivo, -2 materia oscura insuficiente, -3 sin energía, -10 y menos: segundos de recarga.

El modelo: un anillo vertical en doce secciones blindadas con juntas luminosas y cuatro emisores de enfoque, entre dos pilones rodeados de bobinas y aletas. En el centro, dos **anillos cardán** encajados giran como un giroscopio alrededor de un **cristal flotante**: aceleran con la carga, el cristal late y todo se vuelve violeta, y parpadea cuando la carga está casi llena. Debajo, una lente emisora; detrás, bancos de condensadores y un pupitre de mando.

**Apertura del portal**: dieciséis segmentos del marco llegan desde todos lados y se encajan en un anillo con un destello; luego el horizonte se abre desde el centro: dos espirales que giran en sentidos opuestos, ondas que corren hacia el borde, chispas aspiradas en espiral, un anillo de runas y un halo violeta que ilumina los alrededores. Al cerrarse (tiempo agotado o nave pasada), el horizonte se derrumba en un destello y los segmentos se separan.

Es la única forma razonable de llegar a la [galaxia de Érebo](Galaxie-Erebe.md).

## Mesa de cartografía

Una mesa holográfica cuadrada de 1,4 m (pestaña TF Avancé, fila de ordenadores): base octogonal con rejillas, tablero de cristal negro cuadriculado, emisor central, un pupitre con teclado y dos pequeñas pantallas delante, de cara al jugador. El holograma cabe dentro del marco del tablero. Cuando un jugador se acerca, muestra:
- el **relieve 3D** del terreno 300 m alrededor de la nave (curvas de nivel sobre una superficie transparente);
- marcadores: yacimientos y rocas (naranja), cofres (amarillo), edificios (violeta), naves (blanco), tu nave (verde);
- la **ficha del planeta**: bioma, gravedad, aire, recursos, lugares, utilidad, peligros (las fichas de esta wiki).

## Motores

Propulsor vectorial, motor pesado, motor de precisión, motor eficiente, motor de materia exótica (78.000 N por el 10 % del propergol).

## Mapa estelar

El mapa estelar del juego muestra el sistema en el que está la nave. En su menú (como en el del ordenador de navegación), el botón **Galaxia** pasa de **442 Otrera** a **Érebo** y luego vuelve al sistema de la nave: el mapa y la lista de objetivos muestran los planetas de la galaxia elegida.

## Tomas

Todas las máquinas del mod tienen sus tomas **en una sola fila en la cara trasera**: todos los cables salen en el mismo eje. De izquierda a derecha (visto desde detrás): energía, fluido, señal, energía.
