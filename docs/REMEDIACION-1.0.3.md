# Informe de remediación — versión 1.0.3

La versión 1.0.3 corrige el error de compilación `CS0246` de la versión 1.0.2.

## Causa raíz

`ServiciosApp.cs` utilizaba la interfaz `ISecurityEventSink`, declarada en
`GeneradorAnexos.Application.Abstractions.Security`, sin importar ese espacio
de nombres. El proyecto WinUI no podía generar su ensamblado local y el
compilador XAML dejaba de resolver todos los controles y convertidores propios.

## Corrección

- Se agregó la directiva `using` faltante.
- Se comprobó que el proyecto WinUI referencia al proyecto `Application` que
  contiene la interfaz.
- Se comprobó la existencia y visibilidad pública de los convertidores,
  `Icono`, `CampoTexto` y `FilaLista` mencionados por los mensajes en cascada.
