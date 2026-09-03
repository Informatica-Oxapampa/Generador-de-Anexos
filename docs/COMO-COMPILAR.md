# Cómo obtener el .exe

## Requisitos

- Windows 10 u 11 de 64 bits.
- Conexión a Internet durante la primera compilación.
- Aproximadamente 1 GB libre para el SDK y las dependencias.

No necesita instalar previamente .NET ni ejecutar el archivo como
administrador. Si el SDK de .NET 8 no está instalado, `compilar.cmd` descarga
automáticamente el instalador oficial de Microsoft y lo guarda en:

```text
%LOCALAPPDATA%\GeneradorAnexos\dotnet8
```

Esta instalación es privada para su usuario y se reutiliza en las siguientes
compilaciones. No modifica permanentemente el `PATH` de Windows.
El archivo `global.json` garantiza que el proyecto use la rama estable de
.NET 8, aunque el equipo tenga también versiones posteriores.

## Compilar

Descomprima el ZIP y **haga doble clic en `compilar.cmd`** (está en la raíz del repositorio).

El script busca .NET 8, lo instala automáticamente si falta, descarga las
dependencias y genera el programa. Al terminar abre sola la carpeta con el
resultado:

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
| `NO SE PUDO PREPARAR EL SDK` | No se pudo descargar o instalar .NET 8 | Revise Internet, proxy y antivirus; luego ejecute otra vez `compilar.cmd` |
| `NETSDK1045 ... net8.0-windows` | SDK demasiado antiguo | Instale .NET 8 SDK |
| `MSB4019 ... Microsoft.WindowsAppSDK` | Falta la carga de trabajo de escritorio | Ábra el instalador de Visual Studio → Modificar → «Desarrollo de escritorio de .NET» |

## Nota honesta

La descarga automática resuelve únicamente la ausencia del SDK. La compilación
completa de WinUI 3 debe terminar en Windows. Si después aparece otro error,
copie el mensaje completo para poder identificar exactamente la dependencia o
el archivo que falta.
