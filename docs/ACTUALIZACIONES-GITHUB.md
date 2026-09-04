# Actualizaciones firmadas desde GitHub

Repositorio oficial:
<https://github.com/Informatica-Oxapampa/Generador-de-Anexos>

Versión actual del programa: **1.0.3**.

## Modelo de confianza

Cada Release válida publica:

- `update.json`, con versiones, URL, tamaño y SHA-256;
- `update.json.p7s`, firma CMS separada del contenido exacto del manifiesto;
- el instalador con firma Authenticode;
- el ZIP de plantillas, cuyo tamaño y hash están dentro del manifiesto firmado;
- `SHA256SUMS.txt` para comprobación manual.

La aplicación fija la huella SHA-256 del certificado institucional. Rechaza el
manifiesto si su firma no coincide y vuelve a comprobar tamaño, hash,
Authenticode y huella antes de elevar y ejecutar el Setup. Las plantillas se
extraen en una carpeta temporal y se validan antes de sustituir las activas.

Si `FirmantesPermitidosSha256` está vacío, el actualizador se deshabilita de
forma segura. Para habilitarlo se necesita un certificado real; nunca use una
huella inventada ni añada el PFX al repositorio.

## Configuración única de GitHub

Cree un entorno de Actions llamado `produccion`, protéjalo con revisores
obligatorios y limite qué ramas o etiquetas pueden desplegar. Cree allí estos
secretos:

- `WINDOWS_SIGNING_CERT_BASE64`: contenido Base64 del PFX;
- `WINDOWS_SIGNING_CERT_PASSWORD`: contraseña del PFX.

No deje el PFX como secreto general de trabajos de prueba: los flujos de
publicación son los únicos que deben recibirlo. La huella SHA-256 del mismo certificado debe aparecer en
`ConfiguracionActualizaciones.cs`. El flujo compara ambas antes de compilar.

Las acciones externas están fijadas por hash de commit. Los flujos solo crean
Releases en borrador y no publican automáticamente a los usuarios.

## Publicar una versión del programa

1. Cambie `Version`, `AssemblyVersion` y `FileVersion` en el proyecto y
   `MiVersion` en el `.iss`.
2. Actualice `CHANGELOG.md` y haga commit/push.
3. Cree y suba una etiqueta como `v1.0.3`.
4. Espere el flujo **Publicar versión**.
5. Descargue el borrador y pruébelo en un equipo limpio.
6. Solo entonces publique la Release como la más reciente.

El flujo ejecuta las pruebas, compila en Windows, firma el EXE y el Setup,
verifica ambas firmas, empaqueta plantillas, firma el manifiesto y calcula los
hashes. Cualquier fallo detiene la Release.

## Publicar solo plantillas

1. Edite los `.docx` y aumente `plantillas/version.txt`.
2. Cree una etiqueta como `plantillas-1.0.1`.
3. El flujo valida la estructura básica de ambos DOCX y `catalogos.json`.
4. Recupera el manifiesto vigente del programa y comprueba su firma.
5. Crea un ZIP y un manifiesto nuevos, firma el manifiesto y deja la Release
   en borrador.
6. Pruebe la actualización antes de publicar como `latest`.

El programa aplica límites adicionales: 25 MB por paquete, 15 MB por DOCX,
límite de entradas y de expansión ZIP, prohibición de macros/ActiveX/objetos
embebidos y relaciones externas salvo el dominio institucional permitido.

## Rotación del certificado

Antes de que venza el certificado actual:

1. añada la huella del certificado nuevo sin retirar todavía la anterior;
2. publique una versión puente firmada con el certificado anterior;
3. confirme que los equipos instalaron la versión puente;
4. cambie los secretos al certificado nuevo y publique con él;
5. retire la huella anterior en una versión posterior.

Quitar primero la huella antigua impediría que equipos desactualizados confíen
en la versión puente.

## Recuperación y retroceso

No se ofrece una versión inferior desde el actualizador: se conserva la versión
más alta confiable ya vista. Ante una Release defectuosa, publique una
corrección con número superior. Para plantillas, el usuario puede restaurar las
incluidas desde Configuración y luego instalar una versión corregida mayor.

Los commits normales de `main` nunca llegan a los equipos. Solo una Release
publicada como `latest`, con manifiesto y paquetes firmados, es visible.
