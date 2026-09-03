# Cómo editar el texto que aparece en el instalador

La pantalla **«Información»** del asistente muestra el contenido de este
archivo:

```text
winui\instalador\informacion.rtf
```

Puede cambiarlo usted mismo cuando quiera. No hace falta tocar ningún código.

---

## Forma recomendada: WordPad

WordPad viene con Windows y guarda en el mismo formato RTF que usa el
instalador. Es la manera más simple y sin riesgo.

1. Abra la carpeta `winui\instalador`.
2. Clic derecho sobre `informacion.rtf` → **Abrir con** → **WordPad**.
   (Si no aparece: clic derecho → Abrir con → Elegir otra aplicación → WordPad.)
3. Edite el texto como en cualquier documento: escribir, borrar, poner negrita
   con **Ctrl+N**, cambiar tamaños, etc.
4. Guarde con **Ctrl+G**. Si WordPad pregunta el formato, elija
   **Formato de texto enriquecido (RTF)**.
5. Vuelva a generar el instalador con `crear-instalador.cmd`.

> Lo que ve en WordPad es prácticamente lo mismo que verá el usuario en el
> asistente, porque Inno Setup usa el mismo motor de texto enriquecido de
> Windows. Es la forma más fiable de comprobar cómo va a quedar.

---

## Qué conviene respetar

- **Sin colores.** El texto va todo en negro. La jerarquía se marca únicamente
  con negrita y con el tamaño de letra. Si en WordPad usa el botón de color de
  fuente, ese color sí aparecerá en el instalador.
- **Negrita, sí.** Se usa para el título, los encabezados de sección y la ruta
  de desinstalación.
- **Tamaños actuales:** título 15 pt, encabezados de sección 10,5 pt, texto
  normal 9,5 pt, líneas de la entidad 9 pt. No hace falta ser exacto; solo
  mantenga la coherencia.
- **No inserte imágenes ni tablas.** El cuadro del asistente es pequeño y con
  desplazamiento; las imágenes se ven mal y aumentan el tamaño del instalador.
- **Longitud.** El usuario ve unas 12 líneas a la vez y puede desplazarse. El
  texto actual ocupa aproximadamente dos pantallas, que es un buen límite.

---

## Si prefiere texto plano, sin formato

En la misma carpeta hay `informacion.txt` con el mismo contenido en texto
simple. Para usarlo, abra `GeneradorAnexos.iss` con el Bloc de notas y cambie
esta línea:

```ini
InfoBeforeFile=informacion.rtf
```

por:

```ini
InfoBeforeFile=informacion.txt
```

Se verá con una sola tipografía y sin negritas, pero es la opción más sencilla
de mantener.

---

## Si prefiere no mostrar esta pantalla

Comente la línea poniendo un punto y coma al principio:

```ini
;InfoBeforeFile=informacion.rtf
```

El asistente pasará directamente de la bienvenida a la selección de carpeta.

---

## Otros textos del asistente que puede cambiar

Todos están en `GeneradorAnexos.iss`, en la sección `[Setup]`:

| Línea | Qué controla |
|---|---|
| `AppName` | Nombre del programa en el asistente y en Aplicaciones instaladas |
| `AppVersion` | Versión mostrada |
| `AppPublisher` | Entidad responsable |
| `DefaultDirName` | Carpeta propuesta por defecto |
| `AppComments` | Descripción breve en las propiedades del programa |

Y el texto de la casilla del acceso directo está en la sección `[Tasks]`:

```ini
Description: "Crear un acceso directo en el &Escritorio"
```

El símbolo `&` marca la letra que funciona como atajo de teclado; puede moverlo
de sitio o quitarlo.

---

## Después de cualquier cambio

Vuelva a ejecutar:

```text
winui\crear-instalador.cmd
```

y entregue el nuevo `Setup.exe` que aparece en `instalador\salida`.
