# Política de seguridad

## Reportar una vulnerabilidad

Si detecta un problema de seguridad en este aplicativo, **no abra una incidencia
pública**. Escriba a la Oficina de Tecnología de la Información de la
Municipalidad Provincial de Oxapampa describiendo:

- Qué versión del programa está afectada.
- Cómo se reproduce el problema.
- Qué impacto cree que tiene.

Se le responderá para confirmar la recepción y se le informará cuando exista una
corrección publicada.

## Alcance

Este repositorio contiene únicamente el código fuente del aplicativo y las
plantillas documentales. **No contiene datos de ciudadanos, credenciales,
certificados ni información de la infraestructura municipal.**

Si encuentra en el historial de este repositorio algo que parezca una
credencial, un certificado o datos personales, avísenos de inmediato por el
mismo canal: es un error y hay que corregirlo con urgencia.

## Versiones que reciben correcciones

Solo la última versión publicada en la sección Releases. Las versiones
anteriores no reciben correcciones de seguridad.

## Verificación de las descargas

Cada versión publicada incluye `SHA256SUMS.txt` con el resumen del instalador.
Antes de instalar un archivo descargado, puede comprobarlo en PowerShell:

```powershell
Get-FileHash .\GeneradorAnexos-1.0.3-Setup.exe -Algorithm SHA256
```

El valor debe coincidir con el publicado. Además, Windows debe mostrar una
firma digital válida de la Municipalidad. Si cualquiera de las dos
comprobaciones falla, **no lo ejecute** y avise a la OTI.

## Confianza de las actualizaciones

- `update.json` se distribuye con una firma CMS separada
  (`update.json.p7s`).
- La aplicación acepta esa firma y la firma Authenticode del instalador solo
  si pertenecen a una huella SHA-256 fijada en el código.
- Si no se ha configurado una huella institucional real, la actualización
  automática permanece deshabilitada de forma segura.
- El certificado y su clave privada se guardan únicamente como secretos del
  entorno protegido `produccion`, con revisión obligatoria. Nunca deben
  añadirse al repositorio ni exponerse a trabajos de prueba.
