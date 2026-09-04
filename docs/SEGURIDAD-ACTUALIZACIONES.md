# Seguridad del sistema de actualización

## Estado de la versión 1.0.3

El actualizador funciona con una política de **fallo seguro**. Mientras
`FirmantesPermitidosSha256` no contenga la huella SHA-256 real del certificado
institucional, la aplicación puede usarse con normalidad, pero no descargará ni
ejecutará actualizaciones automáticas.

No se debe colocar una huella de ejemplo ni rebajar esta comprobación. La clave
privada correspondiente nunca debe entrar al repositorio.

## Cadena de confianza

Una actualización del programa solo se inicia cuando se cumplen todas estas
condiciones:

1. `update.json` y `update.json.p7s` llegan por HTTPS desde un dominio permitido.
2. La firma CMS separada es criptográficamente válida, tiene un único firmante,
   permite firma de código, está vigente y coincide con una huella fijada en el
   ejecutable.
3. El manifiesto tiene formato, fecha, etiqueta, versiones, direcciones, tamaños
   y hashes válidos.
4. Cada redirección sigue usando HTTPS y permanece en la lista de dominios.
5. El archivo descargado coincide exactamente en tamaño y SHA-256.
6. Inmediatamente antes de ejecutarlo se repiten tamaño y hash.
7. Windows valida la firma Authenticode y el certificado firmante vuelve a
   coincidir con la huella institucional fijada.

Los paquetes de plantillas no se ejecutan. Su tamaño y SHA-256 están dentro del
manifiesto firmado y, antes de instalarlos, se limitan las entradas y la
expansión ZIP. Se rechazan macros, ActiveX, objetos embebidos, binarios y
relaciones externas distintas del enlace HTTPS institucional permitido.

## Amenazas y defensas

| Riesgo | Defensa aplicada |
|---|---|
| Descarga alterada o incompleta | Tamaño exacto y SHA-256 |
| Manifiesto sustituido en GitHub | Firma CMS y huella institucional fijada |
| Instalador sustituido junto con su hash | Authenticode y la misma huella fijada |
| Redirección a un servidor ajeno | HTTPS, lista de dominios y máximo de cinco saltos |
| Sustitución entre verificación y ejecución | Segunda verificación justo antes de iniciar |
| Retroceso a una versión antigua firmada | Se conserva la versión de aplicación más alta vista |
| Paquete ZIP hostil | Límites de tamaño, cantidad, relación de compresión y contenido |
| Escritura privilegiada permanente | No existe servicio actualizador; UAC solo eleva el instalador |
| Pérdida de datos durante una actualización | El instalador no borra `%LOCALAPPDATA%\GeneradorAnexos` |

## Publicación segura

La publicación requiere:

- un certificado PFX institucional vigente, con clave privada y uso de firma de
  código;
- su huella SHA-256 incorporada en `ConfiguracionActualizaciones.cs`;
- los secretos `WINDOWS_SIGNING_CERT_BASE64` y
  `WINDOWS_SIGNING_CERT_PASSWORD` configurados únicamente en el entorno
  protegido `produccion` de GitHub Actions, con aprobación obligatoria;
- pruebas y compilación correctas;
- firma y verificación de la aplicación, el instalador y el manifiesto.

Los flujos crean una Release en **borrador**. Antes de publicarla se deben
revisar sus archivos y los hashes. Consulte
[ACTUALIZACIONES-GITHUB.md](ACTUALIZACIONES-GITHUB.md) y
[COMO-DISTRIBUIR.md](COMO-DISTRIBUIR.md).

## Rotación y revocación

Para cambiar el certificado sin interrumpir a los equipos, publique primero una
versión que acepte temporalmente las huellas antigua y nueva, firmada con la
clave todavía confiable. Después publique con el certificado nuevo y retire la
huella anterior en una versión posterior.

Si se compromete una clave privada:

1. revoque el certificado con la entidad emisora;
2. retire los secretos de todos los entornos;
3. suspenda las Releases hasta distribuir por un canal administrado una versión
   que confíe en un certificado nuevo;
4. investigue y rote también tokens y credenciales relacionadas.

## Límites conocidos

- No hay rollback binario automático. Si una versión no inicia, se reinstala la
  anterior desde un canal institucional. Un proceso vigilante privilegiado
  permanente ampliaría innecesariamente la superficie de ataque.
- La protección DPAPI vincula registros y borradores a la cuenta de Windows que
  los creó. Otra cuenta no puede descifrarlos; esto es intencional.
- La firma no sustituye la protección de la cuenta de GitHub: deben mantenerse
  MFA, permisos mínimos, revisión de colaboradores y reglas de rama.
