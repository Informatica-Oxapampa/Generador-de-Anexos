# Cómo distribuir e instalar el programa

La versión 1.0.3 se publica como aplicación WinUI 3 sin empaquetar,
autocontenida y x64. El equipo de destino no necesita instalar .NET ni Windows
App SDK por separado.

## Requisitos del equipo de compilación

- Windows 10/11 x64.
- SDK oficial de .NET 8.
- Inno Setup 6.
- SignTool del Windows SDK.
- Certificado institucional de firma de código vigente, exportado como PFX.
- Redistribuible oficial `VC_redist.x64.exe` en `instalador\redist\`.

El PFX y su contraseña no se guardan en el repositorio. Antes de compilar,
configure para esa consola:

```bat
set GENERADOR_ANEXOS_SIGN_PFX=C:\ruta-segura\codigo.pfx
set GENERADOR_ANEXOS_SIGN_PASSWORD=contraseña
```

Obtenga la huella SHA-256 del certificado y agréguela a
`FirmantesPermitidosSha256` en
`ConfiguracionActualizaciones.cs`. `validar-certificado-firma.ps1` comprueba
que el PFX sea vigente, permita firma de código, tenga clave privada y coincida
con esa huella. La lista vacía mantiene deshabilitadas las actualizaciones
automáticas.

## Crear el instalador local

Ejecute `crear-instalador.cmd`. El proceso:

1. valida el certificado y su huella fijada;
2. compila desde una carpeta `publicado` limpia;
3. firma y verifica `GeneradorAnexos.exe`;
4. genera el instalador con Inno Setup;
5. firma y verifica el instalador.

El resultado es:

```text
instalador\salida\GeneradorAnexos-1.0.3-Setup.exe
```

Nunca distribuya el archivo si el script termina con error. No existe una ruta
de publicación sin firma.

## Comportamiento del instalador

- Instala únicamente para todo el equipo en
  `C:\Program Files\Generador de Anexos` y solicita UAC.
- Requiere Windows 10 2004 (19041) o posterior y arquitectura x64.
- Crea el acceso del Menú Inicio y ofrece uno de Escritorio.
- Registra la aplicación en Configuración y Panel de control.
- Solo intenta cerrar `GeneradorAnexos.exe`, no otros procesos.
- Comprueba el runtime de Visual C++; si falta y el redistribuible no fue
  incluido, cancela la instalación para no dejar un programa inutilizable.
- Actualiza siempre el mismo producto porque conserva su `AppId`.
- El desinstalador elimina solo los archivos que instaló. Nunca borra datos del
  perfil del usuario ni aplica un borrado recursivo sobre `{app}`.

Los registros, respaldos, preferencias, borradores y plantillas descargadas se
guardan en `%LOCALAPPDATA%\GeneradorAnexos`.

## Lista de comprobación

Antes de publicar, pruebe el instalador en una máquina limpia:

1. Verifique en Propiedades que el Setup y el EXE tengan firma válida.
2. Instale, abra y genere un TDR y un Anexo.
3. Cree, cierre, reabra y actualice un registro.
4. Simule cambios sin guardar y confirme las tres opciones del diálogo.
5. Compruebe actualización y conservación de datos.
6. Desinstale y confirme que solo desaparece la carpeta del programa.

## Errores que detienen la publicación

| Error | Acción |
|---|---|
| Falta SDK de .NET 8 | Instale el SDK oficial y vuelva a ejecutar el script. |
| Falta VC++ Redistributable | Añada el instalador oficial a `instalador\redist`. |
| Falta SignTool o Inno Setup | Instale Windows SDK o Inno Setup 6. |
| Certificado ausente, vencido o no fijado | Corrija los secretos y la huella; no omita la comprobación. |
| Versión del EXE distinta de `MiVersion` | Iguale ambos valores antes de crear el Setup. |
