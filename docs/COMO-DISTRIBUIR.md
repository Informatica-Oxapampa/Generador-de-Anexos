# Cómo distribuir e instalar el programa en otras PC

Este documento explica cómo generar un `Setup.exe` con asistente de
instalación y entregarlo a otras personas.

---

## 1. Qué tipo de instalador corresponde a este proyecto

El proyecto está configurado como **aplicación de escritorio sin empaquetar y
autocontenida**:

```xml
<WindowsPackageType>None</WindowsPackageType>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<SelfContained>true</SelfContained>
```

Eso significa que al publicar se obtiene una carpeta con el `.exe` y **todas**
sus dependencias dentro: el runtime de .NET 8, el Windows App SDK, los recursos
de WinUI (`.pri`, `.xbf`), la carpeta `Assets` y la carpeta `plantillas`. El
equipo de destino no necesita instalar .NET ni el Windows App SDK.

Con ese punto de partida, las opciones son:

| Opción | Veredicto |
|---|---|
| **Inno Setup 6** | **Recomendada.** Gratuita, produce un solo `Setup.exe`, asistente clásico de Windows, permite elegir carpeta, accesos directos opcionales y aparece en Configuración › Aplicaciones. Ideal para una app autocontenida. |
| MSIX / paquete de la Store | Requiere firma digital obligatoria y cambiaría el modelo de despliegue del proyecto (`WindowsPackageType`), además de mover la ruta de datos. No conviene aquí. |
| WiX Toolset (MSI) | Potente y apto para despliegue por directiva de grupo, pero mucho más laborioso. Reserve esta vía si más adelante el área de sistemas necesita instalación masiva por GPO/Intune. |
| ClickOnce | Pensado para actualizaciones automáticas desde un servidor; no genera un instalador clásico y encaja mal con WinUI 3 sin empaquetar. |

**Conclusión: Inno Setup 6.** El script ya está escrito en
`instalador\GeneradorAnexos.iss`.

---

## 2. Preparación (una sola vez, solo en su equipo)

1. Descargue e instale **Inno Setup 6** desde <https://jrsoftware.org/isdl.php>.
   Es gratuito. Solo hace falta en el equipo donde usted genera el instalador,
   no en los equipos de los usuarios.
2. *(Recomendado)* Descargue **VC_redist.x64.exe** desde
   <https://aka.ms/vs/17/release/vc_redist.x64.exe> y colóquelo en
   `instalador\redist\`.
   El tiempo de ejecución autocontenido del Windows App SDK depende del
   redistribuible de Visual C++. Casi todos los equipos con Windows 10/11 ya lo
   tienen, pero incluirlo evita sorpresas: el instalador lo ejecutará en
   silencio **solo** en los equipos donde falte.

---

## 3. Generar el `Setup.exe`

Doble clic en:

```text
winui\crear-instalador.cmd
```

El script hace dos cosas seguidas:

1. Compila y publica la aplicación en `winui\publicado`.
2. Empaqueta esa carpeta con Inno Setup.

El resultado queda en:

```text
winui\instalador\salida\GeneradorAnexos-1.0.0-Setup.exe
```

Ese **único archivo** es el que se entrega a los usuarios.

> Si prefiere hacerlo por pasos: ejecute primero `compilar.cmd` y después
> abra `instalador\GeneradorAnexos.iss` con Inno Setup y pulse **Compile**
> (F9).

---

## 4. Qué hace el instalador en el equipo del usuario

- Asistente en español, con pantalla de bienvenida, información del programa,
  selección de carpeta, selección de accesos directos y pantalla final.
- Muestra nombre, versión y entidad responsable durante todo el proceso.
- Pregunta si desea instalar **para todos los usuarios** (requiere permisos de
  administrador) o **solo para el usuario actual** (no requiere permisos).
- Permite **elegir la carpeta de instalación**.
- Crea siempre el acceso directo en el **Menú Inicio**.
- Crea el acceso directo en el **Escritorio** solo si el usuario marca la
  casilla correspondiente.
- Copia el ejecutable, el runtime, los recursos de WinUI y la carpeta
  `plantillas`, respetando la estructura de subcarpetas.
- Instala el redistribuible de Visual C++ únicamente si falta (si usted lo
  incluyó en `instalador\redist\`).
- Registra el programa en **Configuración › Aplicaciones › Aplicaciones
  instaladas**, desde donde puede desinstalarse.
- Ofrece iniciar el programa al terminar.
- No abre ninguna ventana de consola, ni durante la instalación ni al ejecutar
  la aplicación: el proyecto se compila como `WinExe`.

Al desinstalar, el instalador pregunta si desea conservar o eliminar los
registros guardados, los respaldos y las preferencias del usuario. Por defecto
**se conservan**, de modo que una reinstalación o una actualización no pierde
datos.

---

## 5. Actualizar a una versión posterior

1. Cambie `AppVersion` en el `.iss` y la versión que muestra la aplicación.
2. Vuelva a ejecutar `crear-instalador.cmd`.
3. Entregue el nuevo `Setup.exe`.

El usuario lo ejecuta encima de la instalación existente y el programa se
actualiza en el mismo sitio. Esto funciona porque `AppId` es un identificador
fijo:

```ini
AppId={{7F2C6A18-4D9B-4C3E-9A61-3B8E5D2F71C4}
```

**No cambie nunca ese valor.** Si lo cambia, Windows tratará la nueva versión
como un programa distinto y quedarán dos entradas en Aplicaciones instaladas.

Los datos del usuario viven en `%LOCALAPPDATA%\GeneradorAnexos` y no se tocan
al actualizar.

---

## 6. Firma digital y SmartScreen

Este es el punto que más llama la atención al entregar el instalador y conviene
anticiparlo:

Al ejecutar un `.exe` descargado y sin firmar, Windows muestra la advertencia
azul **«Windows protegió su PC»** de SmartScreen. El usuario puede continuar con
*Más información › Ejecutar de todas formas*, pero da mala impresión en un
entorno institucional.

Para eliminarla hace falta un **certificado de firma de código** (Sectigo,
DigiCert, GlobalSign u otra autoridad; los de tipo OV/EV son de pago y anuales).
Con el certificado instalado, se firma así antes de distribuir:

```bat
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ^
  "instalador\salida\GeneradorAnexos-1.0.0-Setup.exe"
```

Conviene firmar también `publicado\GeneradorAnexos.exe` **antes** de generar el
instalador, para que el ejecutable instalado quede firmado igualmente.

Sin certificado, la alternativa práctica dentro de la municipalidad es
distribuir el instalador por la red interna o por una carpeta compartida en
lugar de por descarga desde Internet, y avisar a los usuarios de la advertencia.

---

## 7. Comprobación antes de entregar

Recomiendo probar en un equipo distinto al de desarrollo, idealmente uno donde
nunca se haya instalado Visual Studio ni .NET:

1. Ejecutar el `Setup.exe` y completar el asistente.
2. Comprobar que el acceso directo del Menú Inicio abre el programa.
3. Generar un TDR y un Anexo de prueba (verifica que la carpeta `plantillas`
   se instaló correctamente).
4. Guardar un registro y volver a abrir el programa (verifica la base de datos
   y el cifrado DPAPI en ese equipo).
5. Desinstalar desde Configuración › Aplicaciones y confirmar que la carpeta de
   instalación queda limpia.

---

## 8. Problemas frecuentes

| Síntoma | Causa | Solución |
|---|---|---|
| `#error No se encontro ..\publicado\GeneradorAnexos.exe` al compilar el `.iss` | No se publicó la aplicación | Ejecute `compilar.cmd` antes, o use `crear-instalador.cmd`, que lo hace solo |
| `NO SE ENCONTRO INNO SETUP 6` | Falta Inno Setup en su equipo | Instálelo desde jrsoftware.org |
| El programa instalado no abre y no muestra nada | Falta el redistribuible de Visual C++ | Incluya `VC_redist.x64.exe` en `instalador\redist\` y regenere el instalador |
| «Error al generar el documento» en el equipo destino | La carpeta `plantillas` no se copió | Verifique que existe `{carpeta de instalación}\plantillas` con los dos `.docx` |
| El instalador no arranca en un equipo | Windows de 32 bits o anterior a la versión 2004 | La aplicación requiere Windows 10 2004 (19041) de 64 bits o superior |
