# Corrección del sistema de temas — cuadros blancos y fondo azul

## Las dos causas

### 1. Los cuadros blancos en modo oscuro

No era un problema de un control suelto: era **el orden de arranque combinado
con el uso de pinceles desde código**.

Las tablas de entregables, pagos y objeto del servicio se construyen desde C#
en el constructor de `PaginaTdr`. Ese constructor se ejecuta dentro del
`InitializeComponent()` de la ventana principal, es decir **antes** de que se
aplique el tema guardado. En ese instante el servicio de tema todavía no tenía
raíz sobre la que consultar, así que devolvía «claro» por defecto y todas las
celdas se pintaban con los pinceles del tema claro.

Y aunque el momento hubiera sido el correcto, el problema seguiría existiendo:
**un pincel obtenido desde código es una fotografía del tema de ese instante.**
No cambia al alternar entre claro y oscuro. Por eso las celdas se quedaban
blancas para siempre.

**Corrección.** El código ya no asigna pinceles: aplica **estilos**. Un estilo
cuyos setters usan `ThemeResource` se reevalúa solo cuando cambia el tema. Se
migraron todas las superficies construidas desde código:

- Celdas, cabeceras, editores y marcos de las tres tablas.
- Resalte rojo de celda inválida (antes pintado a mano; ahora es un estilo).
- Filas de requisitos incluidos por defecto.
- Nombre del archivo de pedido cargado.
- Editor de porcentaje de la tabla de pagos.
- Iconos creados desde código.
- Tarjeta de «registro activo» de la cabecera.

### 2. El tinte azul del fondo

Ese venía de mi propia paleta. Los grises que había definido (`#131820`,
`#1D242D`, `#2A323C`) llevan componente azul. Eran colores inventados, y usted
tiene razón en que no deberían existir.

**Corrección: la paleta ya no define colores.** Cada clave `Ga.*` es ahora un
**alias de un recurso de tema de WinUI**, que a su vez es el color real del tema
de Windows:

```xml
<StaticResource x:Key="Ga.Panel"      ResourceKey="CardBackgroundFillColorDefaultBrush" />
<StaticResource x:Key="Ga.Fondo"      ResourceKey="LayerFillColorDefaultBrush" />
<StaticResource x:Key="Ga.Ventana"    ResourceKey="SolidBackgroundFillColorBaseBrush" />
<StaticResource x:Key="Ga.TextoMedio" ResourceKey="TextFillColorSecondaryBrush" />
<StaticResource x:Key="Ga.Ok"         ResourceKey="SystemFillColorSuccessBrush" />
```

Es la misma técnica que usa WinUI en sus propios diccionarios. Consecuencias:

- Los grises son los neutros reales de Windows: **no queda tinte azul**.
- Los tres temas (claro, oscuro y **alto contraste**) salen correctos sin
  mantener tres tablas de colores en paralelo, que era justo lo que se
  desincronizaba.
- Los estados de puntero, foco y pulsación siguen siendo los del sistema,
  porque los estilos derivan de los predeterminados de WinUI.

**Única excepción documentada:** la franja dorada institucional
(`Ga.Dorado`). No es un color de interfaz sino identidad gráfica de la MPO,
igual que el escudo, así que no sigue al tema.

### 3. Cambio importante: el color de énfasis

Antes, la paleta **redefinía el color de acento de Windows** con un azul
institucional. Eso hacía que los botones principales, el indicador de
navegación y los selectores usaran ese azul en lugar del que el usuario tiene
configurado.

Lo he retirado, siguiendo su indicación de no imponer colores. Ahora la
aplicación usa **el color de énfasis configurado en Windows**, como cualquier
aplicación del sistema.

Si prefiere recuperar el azul institucional, se hace añadiendo estas líneas al
final de `Themes/Paleta.xaml`, fuera de los diccionarios de tema:

```xml
<Color x:Key="SystemAccentColor">#1B5E9C</Color>
<Color x:Key="SystemAccentColorDark1">#17527F</Color>
<Color x:Key="SystemAccentColorDark2">#123F63</Color>
<Color x:Key="SystemAccentColorDark3">#0E3049</Color>
<Color x:Key="SystemAccentColorLight1">#3C81C4</Color>
<Color x:Key="SystemAccentColorLight2">#5B9CD9</Color>
<Color x:Key="SystemAccentColorLight3">#8FBEE8</Color>
```

Dígame cuál de las dos prefiere y lo dejo fijado.

---

## Integración con la configuración visual de Windows

**Transparencia.** El material Mica ahora exige tres condiciones, no una:

1. Windows 11 (compilación 22000 o superior).
2. Que el equipo lo admita (`MicaController.IsSupported()`).
3. **Que el usuario tenga activados los efectos de transparencia**
   (`UISettings.AdvancedEffectsEnabled`).

La tercera es la que faltaba. Es la misma preferencia de Configuración ›
Personalización › Colores, y también se apaga sola con «Reducir animaciones y
efectos» de accesibilidad. Si el usuario la desactiva, la aplicación retira el
material y muestra la superficie opaca del sistema.

**En caliente.** La aplicación escucha dos eventos de Windows:

- `AdvancedEffectsEnabledChanged` → aplica o retira Mica sin reiniciar.
- `ColorValuesChanged` → repinta los colores de la barra de título.

El cambio de tema claro/oscuro no necesita nada de eso: WinUI lo propaga solo
mediante `ActualThemeChanged`, que es a lo que responden todos los
`ThemeResource`.

**Windows 10.** No recibe Mica —allí no existe— y usa la superficie opaca del
tema. La barra de título estándar se pinta en oscuro mediante DWM cuando
corresponde. Ningún efecto exclusivo de Windows 11 provoca errores en
Windows 10.

---

## Versión

La versión del programa está en un único sitio, `GeneradorAnexos.WinUI.csproj`
(ahora **1.0.1**). La aplicación la lee del ensamblado y el instalador comprueba
que coincida.

---

## Qué conviene comprobar al ejecutar

Recorra las cuatro secciones en cada modo, entrando además en al menos un
cuadro de diálogo (Guardar, Limpiar o Buscar actualizaciones):

1. **Oscuro** → tablas sin celdas blancas, cabeceras legibles, fondo gris
   neutro sin azul.
2. **Claro** → mismo recorrido.
3. **Usar tema de Windows** → cambie Windows de claro a oscuro con la
   aplicación abierta y confirme que cambia todo, incluidas las tablas.
4. Secuencia **Claro → Oscuro → Windows → Claro → Oscuro** desde Configuración,
   comprobando que nada se queda con el tema anterior.
5. Desactive **Efectos de transparencia** en Windows y confirme que la ventana
   pasa a fondo sólido; vuelva a activarlos y confirme que reaparece Mica.
