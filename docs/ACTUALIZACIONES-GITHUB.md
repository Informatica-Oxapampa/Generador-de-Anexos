# Actualizaciones desde GitHub — guía de trabajo

Repositorio oficial:
**<https://github.com/Informatica-Oxapampa/Generador-de-Anexos>**

---

## 1. Las dos cosas que hay que tener separadas

Este es el punto más importante de todo el sistema.

| | Desarrollo | Publicación |
|---|---|---|
| Herramienta | GitHub Desktop | Etiqueta + GitHub Actions |
| Qué produce | Commits en la rama | Una **Release** con instalador y manifiesto |
| Quién lo ve | Solo usted | **Todas las PC con el programa instalado** |
| Frecuencia | Todas las que quiera | Solo cuando la versión está lista |

**La aplicación instalada nunca mira la rama.** Consulta exclusivamente:

```text
https://github.com/Informatica-Oxapampa/Generador-de-Anexos/releases/latest/download/update.json
```

Esa dirección solo existe cuando usted publica una Release. Puede hacer
quince commits con GitHub Desktop, dejar código a medias, subir pruebas: **nada
de eso llega a los usuarios**. Solo la etiqueta desencadena una publicación.

En el código, esa separación está en un único archivo:
`Services/Actualizaciones/ConfiguracionActualizaciones.cs`. Ningún otro sitio
construye direcciones de GitHub.

---

## 2. Su flujo diario con GitHub Desktop (sin cambios)

Siga trabajando exactamente igual:

1. Modifica el proyecto en Visual Studio o VS Code.
2. GitHub Desktop muestra los archivos cambiados.
3. **Commit**.
4. **Push**.

Eso es todo. No se publica nada, no se dispara ningún flujo, ningún equipo
recibe nada.

---

## 3. Los dos canales de actualización

El sistema distingue **programa** y **plantillas**, y cada uno tiene su propia
versión y su propia etiqueta.

| | Programa | Plantillas |
|---|---|---|
| Qué contiene | Todo el aplicativo | Solo los `.docx` |
| Tamaño | ~240 MB | Unos pocos KB |
| Etiqueta | `v1.0.1` | `plantillas-1.0.1` |
| Versión declarada en | `GeneradorAnexos.WinUI.csproj` | `plantillas/version.txt` |
| Dónde se instala | Carpeta del programa | `%LOCALAPPDATA%\GeneradorAnexos\plantillas` |

Corregir una redacción del Anexo N.° 06 ya no obliga a que decenas de equipos
descarguen 240 MB ni a reinstalar nada.

Las plantillas actualizadas se guardan en la carpeta del usuario, no junto al
ejecutable. Así funciona aunque el programa esté instalado en Archivos de
programa y el usuario no sea administrador, y una reinstalación nunca pisa unas
plantillas más recientes.

---

## 4. Publicar una versión del PROGRAMA

Ejemplo: pasar de `1.0.0` a `1.0.1`.

### Paso 1 — Subir el número de versión

En `src/GeneradorAnexos.WinUI/GeneradorAnexos.WinUI.csproj`:

```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
```

En `instalador/GeneradorAnexos.iss`:

```ini
#define MiVersion "1.0.1"
```

Commit y push con GitHub Desktop, como siempre.

### Paso 2 — Crear la etiqueta

En GitHub Desktop: menú **Repository › Create tag…**, nombre `v1.0.1`, y
después **Push** para subirla. (También puede crearla desde la web de GitHub
al preparar la Release.)

### Paso 3 — Esperar a GitHub Actions

La etiqueta dispara el flujo `Publicar versión`, que:

1. Compila y publica la aplicación.
2. **Comprueba que el csproj declare `1.0.1`**; si no coincide con la etiqueta,
   se detiene con un error. Es a propósito: publicar un instalador que anuncia
   una versión distinta de la que instala rompe el actualizador.
3. Genera el instalador con Inno Setup.
4. Empaqueta las plantillas.
5. Calcula los SHA-256 y escribe `update.json`.
6. Crea la Release **en borrador** con todos los archivos adjuntos.

### Paso 4 — Revisar y publicar

Entre en la pestaña **Releases**. Verá el borrador con:

```text
GeneradorAnexos-1.0.1-Setup.exe
plantillas-1.0.0.zip
update.json
SHA256SUMS.txt
```

Escriba las notas de cambios, y cuando esté conforme pulse **Publish release**
con **Set as the latest release** marcado.

> El borrador es deliberado: hasta que usted no pulse publicar, ningún equipo ve
> nada. Puede probar el instalador descargándolo del borrador.

### Paso 5 — Comprobar

Abra en el navegador:

```text
https://github.com/Informatica-Oxapampa/Generador-de-Anexos/releases/latest/download/update.json
```

Debe mostrar el manifiesto de la versión nueva.

---

## 5. Publicar solo PLANTILLAS

Ejemplo: corregir un texto del Anexo y pasar las plantillas de `1.0.0` a
`1.0.1`, sin tocar el programa.

### Paso 1 — Modificar y numerar

1. Edite los `.docx` en la carpeta `plantillas/`.
2. Cambie `plantillas/version.txt` a `1.0.1`.
3. Commit y push con GitHub Desktop.

Puede hacer todos los commits intermedios que quiera: hasta la etiqueta no se
publica nada.

### Paso 2 — Etiquetar

Cree la etiqueta `plantillas-1.0.1` y súbala.

### Paso 3 — GitHub Actions

El flujo `Publicar plantillas`:

1. Comprueba que `version.txt` coincida con la etiqueta.
2. Empaqueta los `.docx` en `plantillas-1.0.1.zip`.
3. **Copia sin tocar el canal del programa** desde el manifiesto de la Release
   vigente. Esto es lo que hace que los equipos vean solo la actualización de
   plantillas y no vuelvan a descargar el instalador.
4. Crea la Release en borrador.

### Paso 4 — Publicar

Revise y pulse **Publish release** con **Set as the latest release** marcado.

En los equipos, la aplicación mostrará «Plantillas actualizadas disponibles»,
descargará unos pocos KB y las sustituirá. Sin reinstalar, sin tocar los datos
del usuario y sin reiniciar el programa.

---

## 6. El manifiesto `update.json`

Lo genera GitHub Actions; no hace falta escribirlo a mano. Su forma:

```json
{
  "manifiesto": 1,
  "publicado": "2026-09-15T14:00:00Z",
  "release": "v1.0.1",
  "app": {
    "version": "1.0.1",
    "fecha": "2026-09-15",
    "url": "https://github.com/Informatica-Oxapampa/Generador-de-Anexos/releases/download/v1.0.1/GeneradorAnexos-1.0.1-Setup.exe",
    "sha256": "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08",
    "tamano": 251658240,
    "obligatoria": false,
    "versionMinima": "",
    "notas": ["Corrige la negrita del Anexo N.° 06."]
  },
  "plantillas": {
    "version": "1.0.0",
    "fecha": "2026-09-01",
    "url": ".../plantillas-1.0.0.zip",
    "sha256": "...",
    "tamano": 412160,
    "archivos": ["plantilla_anexos.docx", "plantilla_tdr.docx"],
    "notas": []
  }
}
```

**Un solo manifiesto describe los dos canales.** Cada Release lo publica
completo, aunque solo cambie uno; el otro repite la versión vigente. Así la
aplicación resuelve ambas comprobaciones con una única petición y nunca se queda
con información desparejada.

Campos que puede editar a mano en el borrador antes de publicar:

- `notas` — los cambios que verá el usuario en el aviso de actualización.
- `obligatoria` — póngalo en `true` si la versión corrige algo grave y no quiere
  que nadie pueda posponerla. Desaparece el botón «Omitir esta versión».

---

## 7. Numeración

Formato `MAYOR.MENOR.PARCHE`, comparado numéricamente campo por campo (por eso
`1.10.0` se detecta correctamente como posterior a `1.9.0`).

| Cambio | Cuándo |
|---|---|
| PARCHE `1.0.0 → 1.0.1` | Correcciones |
| MENOR `1.0.1 → 1.1.0` | Funciones nuevas compatibles |
| MAYOR `1.1.0 → 2.0.0` | Cambio importante o incompatible |

> **Estado actual: v1.0.0.** No se incrementa nada hasta publicar oficialmente
> la primera versión.

Programa y plantillas se numeran por separado y no tienen por qué coincidir.

---

## 8. Probar antes de que lo vea todo el mundo

Tiene dos redes de seguridad, y conviene usar las dos:

**El borrador.** Los flujos crean la Release en borrador. Descárguese el
instalador desde ahí e instálelo en un equipo de prueba. Mientras siga en
borrador, `releases/latest` no lo sirve.

**La pre-release.** Si necesita que varias personas lo prueben, publique
marcando **Set as a pre-release**. Una pre-release tampoco se sirve en
`latest`. Cuando esté conforme, edítela, desmarque pre-release y marque
«Set as the latest release».

**Prueba del ciclo completo:** instale la versión anterior en un equipo,
publique la nueva, abra el programa y confirme que a los pocos segundos aparece
el aviso; acepte y compruebe que al terminar se reabre actualizado y que **los
registros guardados siguen ahí**.

**Prueba de que un archivo manipulado se rechaza:** cambie un carácter del
`sha256` en el `update.json` del borrador. La descarga debe completarse y
fallar después en la verificación, sin ejecutar nada.

---

## 9. Volver atrás

**Del programa.** En GitHub, marque la Release anterior como «latest» y
desmarque la defectuosa. Los equipos que aún no se actualizaron dejan de verla.
Los que ya la instalaron: descargue el instalador anterior y ejecútelo encima;
como el `AppId` es el mismo, Windows lo trata como la misma aplicación y **los
datos del usuario no se tocan**.

**De las plantillas.** Más simple todavía: en el propio programa,
**Configuración › Datos y diagnóstico › Restaurar plantillas incluidas**. Borra
las descargadas y vuelve a las que trajo el instalador, sin depender de la red.
Después, publique unas plantillas corregidas con número superior.

---

## 10. Repositorio público o privado

**Recomendación: público.** En un repositorio privado, cada descarga exige un
token, y ese token habría que incrustarlo en el ejecutable que se reparte a los
usuarios: cualquiera podría extraerlo. Es un antipatrón de seguridad.

Si el código no debe ser público, la solución correcta es separar: código en un
repositorio privado y un segundo repositorio público solo con las Releases.

---

## 11. Errores frecuentes

| Síntoma | Causa | Solución |
|---|---|---|
| Actions falla con «El csproj declara X pero la etiqueta es Y» | Se etiquetó sin subir la versión | Iguale csproj, `.iss` y etiqueta |
| Actions falla con «version.txt dice X pero la etiqueta es Y» | Etiqueta de plantillas sin actualizar `version.txt` | Iguálelos |
| Nadie recibe la actualización | La Release sigue en borrador o es pre-release | Publíquela y márquela como «latest» |
| «La dirección de descarga no es de confianza» | La `url` del manifiesto no apunta a github.com | Use la URL que genera Actions |
| Descarga bien y falla la verificación | El hash no corresponde al archivo subido | Regenere el manifiesto |
| El flujo de plantillas falla al buscar la Release del programa | Todavía no existe `v<versión del csproj>` | Publique primero el programa |
