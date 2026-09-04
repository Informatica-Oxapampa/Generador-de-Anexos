# Generador de Anexos

Aplicación de escritorio para Windows que genera los **Términos de Referencia
(TDR)** y los **Anexos N.° 06 al N.° 09** utilizados en los procedimientos de
contratación de bienes y servicios menores.

Desarrollada por la **Oficina de Tecnología de la Información** de la
**Municipalidad Provincial de Oxapampa**.

---

## Qué hace

- Genera el TDR y los Anexos N.° 06 al 09 en Word a partir de un formulario
  guiado, con validación de los campos obligatorios.
- **Importa el Pedido de Servicio del SIGA** en PDF y autocompleta dirección
  solicitante, motivo, meta, clasificador de gasto, valor y unidad de medida.
- Sincroniza los datos comunes entre el TDR y los Anexos, para no escribirlos
  dos veces.
- Calcula el plan de pagos según los entregables definidos.
- Guarda registros para reutilizarlos, con autoguardado y copias de seguridad.
- Comprueba actualizaciones firmadas desde las versiones publicadas en este repositorio.

## Requisitos

- Windows 10 versión 2004 (compilación 19041) o posterior, o Windows 11.
- Arquitectura de 64 bits.
- Aproximadamente 350 MB de espacio libre.

Para **usar el programa instalado** no hace falta instalar .NET por separado:
la publicación es autocontenida. El equipo sí necesita Microsoft Visual C++
2015–2022 (x64); el instalador lo
incluye si el archivo oficial `VC_redist.x64.exe` fue agregado al compilar y,
si no, se detiene con una explicación antes de copiar el programa.

## Instalación

Descargue el instalador de la última versión desde la sección
[Releases](https://github.com/Informatica-Oxapampa/Generador-de-Anexos/releases)
y ejecútelo. El asistente le guiará en el proceso.

Para verificar la descarga, compare su resumen SHA-256 con el publicado en
`SHA256SUMS.txt` de esa misma versión:

```powershell
Get-FileHash .\GeneradorAnexos-1.0.3-Setup.exe -Algorithm SHA256
```

## Actualizaciones

Una vez configurada la huella del certificado institucional, el programa
comprueba si hay una versión más reciente y ofrece instalarla. También puede
buscarlas manualmente desde **Configuración › Actualizaciones**. Si la firma
falta o no coincide, la actualización se rechaza.

Las plantillas de Word se actualizan por un canal aparte y firmado, sin
necesidad de reinstalar el programa ni de permisos de administrador.

## Documentación

| Documento | Contenido |
|---|---|
| [Cómo compilar](docs/COMO-COMPILAR.md) | Compilar el proyecto desde el código fuente |
| [Cómo distribuir](docs/COMO-DISTRIBUIR.md) | Instalador, GitHub y firma |
| [Notas técnicas](docs/notas-tecnicas/) | PDF SIGA y temas |

## Estructura del proyecto

```text
src/          Código fuente (.NET 8 · WinUI 3)
  ├── GeneradorAnexos.Domain              Modelos y reglas de negocio
  ├── GeneradorAnexos.Application         Casos de uso e interfaces
  ├── GeneradorAnexos.Infrastructure.*    Word, PDF, base de datos, cifrado
  └── GeneradorAnexos.WinUI               Interfaz de usuario
tests/        Pruebas (dominio, pagos, sincronización y lector PDF)
plantillas/   Plantillas de Word en blanco, con marcadores
instalador/   Script de Inno Setup
docs/         Documentación
```

## Compilar

```bat
compilar.cmd
```

Para **compilar el código fuente** sí se requiere el SDK de .NET 8. Si no está
instalado, puede obtenerse desde una consola con:

```bat
winget install --id Microsoft.DotNet.SDK.8 --exact --source winget
```

Después cierre y vuelva a abrir la consola. El detalle está en
[docs/COMO-COMPILAR.md](docs/COMO-COMPILAR.md). `compilar.cmd` está en la
**raíz** del repositorio.

```bat
dotnet run --project tests\GeneradorAnexos.Domain.Tests
dotnet run --project tests\OrderPdfReader.Regression
```

## Dónde se guardan los datos

En el perfil de cada usuario de Windows, en
`%LOCALAPPDATA%\GeneradorAnexos`: registros guardados, copias de seguridad,
preferencias y el registro de diagnóstico. Las actualizaciones nunca los tocan.

Este repositorio **no contiene datos de personas, credenciales ni información
de la infraestructura municipal**. Si encuentra algo de eso, avise según
[SECURITY.md](SECURITY.md).

## Licencia

Pendiente de definir por la Municipalidad. Mientras tanto se reservan todos
los derechos: ver [LICENSE.md](LICENSE.md).

## Contacto

Oficina de Tecnología de la Información
Municipalidad Provincial de Oxapampa · Perú
