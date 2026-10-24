# Equipamiento TF

Los objetos del mod **StellarDrive Demo TF** se unieron a TF Advanced (v0.18), en la pestaña de construcción **TF Équipement**:

| Objeto | Uso |
|---|---|
| **21 lámparas** (losa luminosa 1 × 1 alineada con las casillas del casco, plafón, aplique, foco, regleta, foco empotrado, farol industrial, baliza intermitente, girofaro, lámpara de pie, lámpara de escritorio, ojo de buey luminoso, lámpara de anillo, aplique de doble haz, riel de focos...): se colocan en cualquier superficie (casco, pendiente, techo, piezas colocadas, hasta el borde de una pieza: rejilla de 5 cm) | iluminan las paredes, suelos y piezas a su alrededor, más fuerte de noche y en interiores; toman el color pintado con la pistola de pintura del mod TF |
| **Cámara de vigilancia** | se fija en cualquier sitio; la **tableta** (**F9**) muestra su imagen, las flechas cambian de cámara, Z cambia el tamaño |
| **Cápsula de teletransporte** | mírala y pulsa **T**: elige otra cápsula (con nombre) y te teletransportas allí. Base con emisor giratorio, pilares, pared trasera iluminada y consola; sus anillos de energía suben y bajan, más rápido contigo dentro, y se aceleran al partir |
| **Radio** | **T**: reproduce música (del juego o enlace de YouTube) que oyen todos los jugadores cercanos |
| **Soporte de herramientas** | **T**: recupera una herramienta básica perdida (construcción, reciclador, láser de minería), la pistola de pintura, la tableta esquemática; te avisa si ya la tienes |
| **Generador, tanques de oxígeno y de etanol, cofre de recursos infinitos** | sin fin, pero **extremadamente caros**: cientos de hierro, cobre o vidrio, 8 núcleos cuánticos, 10 de materia exótica, 6 artefactos, 40 aleaciones nexorianas y 30 meteoritos cada uno |

**Teletransportador** (objeto **7419**, sin receta: dátelo con un comando give): en la mano, clic izquierdo abre la lista: **planetas** (en tierra firme, lado de día), **estaciones** y **balizas** (junto a la baliza).

**Cabina cosmética**: T para cambiar tu aspecto: colores del traje, del casco, de la mochila y del visor, sombrero (gorra, cowboy, chistera, gorro, corona), gafas de sol (clásicas, aviador, banda) y color de los cristales, chaqueta (chaleco, cazadora, abrigo largo) y su color. El maniquí de la cabina lleva tu ropa; los demás jugadores te ven con ella (ajuste `Outfit`).

**Radio, silencio**: en la ventana de la radio, «Silenciada para mí» la apaga solo para ti. El anfitrión ve allí la lista de jugadores y puede bloquear a los que no pueden cambiar la música (`RadioLockedPlayers`).

Mismos números de pieza que antes: lo ya construido en tus mundos se conserva. Mantén el mod TF actualizado (0.15 o más): deja estos objetos a TF Advanced (las sillas, el armario y las puertas de atraque siguen en el mod TF). Con un mod TF más antiguo, es él quien los conserva, sin duplicados.

## Pistola de pintura, tableta esquemática, vista en tercera persona

Sin el mod TF, TF Advanced también ofrece (mismos objetos, mismos archivos: nada se pierde):
- la **pistola de pintura** del mod TF: pinta paredes y suelos, y también puertas, máquinas y todas las piezas colocadas, con acabado (brillo, metal, luminoso), preajustes y **camuflajes** (bosque, desierto, ártico...). Cógela del soporte, **clic izquierdo** pinta, **clic derecho** paleta, **rueda** cuentagotas. Los colores se guardan con el mundo (`tf-paint.json`);
- la **tableta esquemática**: cógela del soporte, selecciónala en tu barra de herramientas, **clic izquierdo** para abrir su ventana. **Copiar** guarda la nave más cercana (y lo acoplado); **Construir** la vuelve a crear gratis delante de ti. Los esquemas están en tu PC en `UserData/TF/schematics`, compartidos por todos tus mundos. La pintura de la nave se copia también, y las naves sujetas por un **rayo tractor** bloqueado se copian con la nave principal;
- la **vista en tercera persona** al pilotar: tecla **V** (`ThirdPersonKey`), rueda del ratón para la distancia (`ThirdPersonDistance`);
- las teclas de los asientos del mod **KeySeat** guardadas al instante y copiadas por los esquemas.

Si el mod TF está instalado, es él quien los ofrece, sin duplicados.

Ajustes: sección `[TFAdvancedObjects]` (tecla de la tableta `TabletKey`, tecla de uso `UseKey`, imágenes por segundo de la cámara, fuerza de iluminación de las lámparas `LampLightStrength`, volumen de la radio).

## Modo foto y marco de fotos

- **Modo foto** (tecla **F8**, ajuste `PhotoKey`, o clic izquierdo con la **cámara** en la mano, tomada del soporte de herramientas): la vista deja al jugador y vuela libre (ratón para mirar, WASD para moverse, Espacio/Ctrl para subir y bajar, Mayús más rápido, Alt más lento, rueda para el zoom, R para inclinar y T para nivelar). La interfaz del juego desaparece. **F** cambia el filtro (blanco y negro, sepia, vivos, cine, visión nocturna), **H** oculta la ayuda, **P**, Intro o un clic hace la foto, Esc o F8 vuelve.
- Las fotos van a `UserData/TFAdvanced/photos` (cualquier imagen PNG o JPG puesta allí también sirve).
- **Marco de fotos** (pestaña TF Equipamiento, 3 madera + 1 vidrio): míralo y pulsa **T** para elegir una foto. Se envía al anfitrión, se guarda con el mundo y todos los jugadores la ven. La imagen mira hacia fuera de la superficie donde está el marco: ideal en una pared.

## Pintura por zona (BuildZoop)

Con la pistola de pintura en la mano, **mantén la tecla zoop** (la del mod BuildZoop, **Mayús** por defecto, o Mayús sin ese mod): se fija una esquina donde apuntas, una caja naranja va de ella a donde apuntas ahora, con el número de piezas dentro. **Clic**: todo lo que hay en la caja se pinta de golpe (paredes, suelos, pendientes y piezas colocadas), con el color, el acabado y el camuflaje de la pistola, del lado apuntado primero (los dos con «ambos lados»).

**Bote de pintura**: casilla en la ventana de la pistola (clic derecho). Un **clic** pinta de golpe **todo el casco** de la nave apuntada (paredes, suelos, pendientes unidos a la pieza apuntada), sin tocar muebles, luces, máquinas ni otras piezas colocadas.

## Cámaras y modo foto: cielo y jugador

La imagen de las cámaras de vigilancia muestra ahora el **cielo de los planetas** (atmósfera, nubes) por defecto (ajuste `CameraPlanetSkyOn`, desactívalo si bajan las imágenes por segundo). En la imagen de una cámara y en modo foto, **tu propio personaje** se ve entero (cabeza incluida), con tu atuendo.
