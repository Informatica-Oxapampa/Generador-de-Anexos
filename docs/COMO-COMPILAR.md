# Cómo obtener el .exe

## Requisitos

- Windows 10 u 11 de 64 bits.
- Conexión a Internet durante la primera compilación.
- Aproximadamente 1 GB libre para el SDK y las dependencias.

Instale previamente el SDK oficial de .NET 8. No basta con tener instalado el
runtime. En Windows 10 u 11 puede instalarlo mediante Windows Package Manager:

```bat
winget install --id Microsoft.DotNet.SDK.8 --exact --source winget
```

Después cierre y vuelva a abrir la consola y compruebe la instalación:

```bat
dotnet --list-sdks
```

Debe aparecer una versión que empiece por `8.`. `compilar.cmd` no descarga ni
ejecuta herramientas automáticamente. El archivo `global.json` mantiene el
proyecto en la rama estable de .NET 8 aunque el equipo tenga otras versiones.

## Compilar

Descomprima el ZIP y **haga doble clic en `compilar.cmd`** (está en la raíz del repositorio).

El script busca .NET 8, restaura las dependencias declaradas y genera el
programa. Al terminar abre la carpeta con el resultado:

```
publicado\GeneradorAnexos.exe
```

Esa carpeta `publicado` es la que se copia al equipo del usuario final. Debe
copiarse **completa** (el .exe necesita los archivos que están junto a él,
incluida la subcarpeta `plantillas`).

## Consulta del nombre por DNI

El RUC se calcula **localmente** (algoritmo de SUNAT) y no requiere Internet.
La consulta automática del nombre está desactivada hasta integrar el servicio
oficial de RENIEC. El botón **Validar** deriva el RUC 10 a partir del DNI y
avisa de que el nombre se completará cuando exista ese convenio.

No se envía el DNI a ningún proveedor externo. No hay token de ApiPeru ni
variable de entorno que lo active en esta versión.

## Si prefiere usar Visual Studio

Visual Studio es opcional. Para compilar desde él:

1. Abra `GeneradorAnexos.WinUI.sln`.
2. En la barra superior elija **Release** y **x64**.
3. Clic derecho en el proyecto **GeneradorAnexos.WinUI** → **Publicar**.

## Si algo falla

El script deja el error en pantalla y no cierra la ventana. Copie **todo** el
texto y envíelo: con el mensaje exacto se corrige rápido.

Los tres tropiezos habituales:

| Mensaje | Causa | Solución |
|---|---|---|
| `NO SE PUDO PREPARAR EL SDK` | No está instalado el SDK de .NET 8 | Ejecute el comando `winget` indicado arriba, abra otra consola y vuelva a ejecutar el script |
| `NETSDK1045 ... net8.0-windows` | SDK demasiado antiguo | Instale .NET 8 SDK |
| `MSB4019 ... Microsoft.WindowsAppSDK` | Falta la carga de trabajo de escritorio | Ábra el instalador de Visual Studio → Modificar → «Desarrollo de escritorio de .NET» |

## Nota honesta

La compilación completa de WinUI 3 debe ejecutarse en Windows. El flujo de CI
también compila el proyecto completo en un agente Windows para detectar
regresiones antes de publicar.
