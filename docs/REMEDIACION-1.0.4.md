# Informe de remediación — versión 1.0.2

La versión 1.0.2 corrige el fallo de compilación detectado en la versión 1.0.3.

## Correcciones aplicadas

- Eliminación de `MaxLength` en `AutoSuggestBox`, propiedad no admitida por el
  compilador XAML de WinUI 3.
- Conservación del límite de 120 caracteres del buscador mediante validación
  en el evento `TextChanged`.
- Uso de cultura invariable al formar el nombre de los respaldos.
- Semáforos de duración de proceso para evitar advertencias por recursos
  descartables y mantener la exclusión mutua.
- Instrucciones claras para instalar y comprobar el SDK de .NET 8.

El SDK 8.0.424 mostrado en el diagnóstico del usuario es compatible; no fue la
causa del fallo.
