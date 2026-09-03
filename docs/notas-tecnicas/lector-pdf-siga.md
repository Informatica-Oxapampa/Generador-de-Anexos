# Corrección del autocompletado de pedidos PDF

## Cómo usar esta versión

1. Descomprima el ZIP en una carpeta nueva.
2. Ejecute `compilar.cmd` en la raíz del repositorio. Si falta .NET 8, el archivo lo descargará e instalará automáticamente para su usuario.
3. Abra el programa recién generado en `publicado`. No use un ejecutable antiguo.
4. En TDR, seleccione **Cargar Pedido** y después **Procesar**.
5. Revise el resumen de campos completados y compruebe el TDR y Anexos antes de generar documentos.

Se entrega el proyecto C# completo y modularizado. No se modificó el programa Python, el diseño XAML, las plantillas, los temas, los datos guardados ni las funciones de generación. Solo se modificaron dos archivos de producción:

- `src/GeneradorAnexos.Infrastructure.Windows/Integrations/OrderPdfReader.cs`
- `src/GeneradorAnexos.WinUI/Views/PaginaTdr.xaml.cs`

Se añadieron pruebas independientes, esta explicación y la instalación automática del SDK de .NET 8. No se incluyeron los directorios `bin` y `obj` del ZIP original: contienen compilaciones antiguas y archivos regenerables. Las pruebas no forman parte del ejecutable publicado.

## Qué fallaba y qué cambió

El lector anterior buscaba números y etiquetas en líneas completas sin separar columnas. Esto permitía interpretar la versión `26.01.00.U1` como un clasificador o como un monto. Tampoco reconocía el rótulo “Dirección Solicitante”, la cabecera “PEDIDO DE SERVICIO Nº” ni “Unidad Medida”. El motivo se cortaba al tomar una sola línea.

El lector corregido utiliza las posiciones de las palabras con PdfPig. El monto, clasificador, unidad y meta se obtienen de sus columnas. El motivo se recoge entre secciones, incluso cuando su primera línea está por encima del rótulo. Conserva los ceros iniciales y sufijos del número de pedido. Si no existe un motivo identificable, la descripción completa del primer ítem sirve de respaldo, como prevé el formulario Python.

| Información del PDF | Campo de destino |
|---|---|
| Dirección Solicitante | Dirección solicitante / Área usuaria del TDR |
| Número de pedido | N° de pedido y su sincronización existente con Anexos |
| Motivo completo | Denominación; desde allí, objeto, cuadro y descripción de Anexos mediante la sincronización existente |
| META / MNEMONICO | Meta del TDR |
| Clasificador del primer ítem | Clasificador del TDR |
| Valor del primer ítem | Monto de Anexos |
| Unidad Medida del primer ítem | Unidad de medida del cuadro del TDR |

Los destinos editados manualmente mantienen la protección de la sincronización existente. Los campos que no se extraen no se vacían ni se rellenan con números de otras secciones: se conservan y se avisa para revisión manual.

## Verificación realizada

Se ejecutó el lector original de Python para obtener la referencia de los dos PDF incluidos en su proyecto. Después se compiló y ejecutó el lector C# corregido con .NET 8 y PdfPig 0.1.16, la versión presente en los archivos originales.

| Campo | pedido.pdf | pedido2.pdf |
|---|---|---|
| N° de pedido | 001211 | 001732 |
| Meta | 0054 | 0054 |
| Clasificador | 2.3.2.7.4.99 | 2.3.2.7.11.99 |
| Monto | 500.00 | 3000.00 |
| Unidad | SERVICIO | SERVICIO |
| Dirección solicitante | Coincide íntegramente con Python | Coincide íntegramente con Python |
| Motivo | Coincide íntegramente con Python | Coincide íntegramente con Python, con sus dos líneas |

Resultado: **57 comprobaciones correctas** en el programa de regresión, incluidos los dos PDF originales, motivos de tres líneas, números con ceros y sufijos, datos ausentes, formatos monetarios con coma y punto, varios ítems, primera página, PDF sin texto, documento ajeno, cancelación y el servicio real de sincronización.

También se compiló el código C# de la interfaz con las referencias y parciales XAML generadas incluidos en su ZIP, sin errores. Esta comprobación no reemplaza la recompilación completa de XAML ni la ejecución visual en Windows; no se ejecutó WinUI en este entorno ni se entrega un .exe nuevo ya validado.

## Límites conservados

- Solo la primera página y el primer ítem, igual que el lector Python; no suma ítems ni fusiona páginas.
- PDF con texto seleccionable; no procesa escaneos mediante OCR.
- La lectura por columnas está verificada con los dos diseños suministrados. Un diseño SIGA distinto debe probarse con su PDF concreto.
- Actividad operativa, fuente de financiamiento y otros datos que Python no importaba siguen siendo manuales.
- Los errores ortográficos presentes en el PDF se conservan; no se reescribe su contenido.

## Repetir las pruebas

Desde la raíz del repositorio, con el SDK de .NET 8:

```powershell
dotnet run --project tests/OrderPdfReader.Regression/OrderPdfReader.Regression.csproj
```

Las pruebas enlazan el archivo de producción, no una copia del algoritmo. Los PDF sintéticos se crean en una carpeta temporal propia y se eliminan al finalizar. `Fixtures/python_expected.json` contiene los resultados obtenidos ejecutando el lector Python original; `Fixtures/pedido.pdf` y `Fixtures/pedido2.pdf` son copias sin cambios de los adjuntos originales.

El registro de ejecución se incluye en `tests/OrderPdfReader.Regression/RESULTADOS.txt`.

## Corrección adicional: pagos de múltiples entregables

La tabla de forma de pago redistribuye automáticamente el 100 % cada vez que
el usuario agrega o elimina un entregable. La división utiliza enteros y el
último pago absorbe el residuo para garantizar una suma exacta: dos pagos son
`50/50`, tres son `33/33/34` y cuatro son `25/25/25/25`.

Las condiciones de pago escritas manualmente se conservan. Al cargar un
registro guardado también se conservan sus porcentajes; la redistribución se
aplica cuando el usuario cambia posteriormente la cantidad de entregables.
Los planes antiguos incompletos o cuya suma no sea 100 % se reparan al cargar.

Se añadieron **210 comprobaciones de porcentajes** a las 57 pruebas existentes:
casos exactos de 1, 2, 3, 4 y 6 pagos, además de validar desde 1 hasta 100 pagos
que la cantidad de filas sea correcta y que la suma siempre sea 100 %.
El resultado combinado es de **273 comprobaciones correctas**.
