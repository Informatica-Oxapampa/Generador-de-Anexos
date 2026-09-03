# Pedidos de muestra

Esta carpeta contiene los valores esperados (`python_expected.json`), pero **no
los PDF de los pedidos**.

## Por qué no están publicados

Los Pedidos de Servicio del SIGA incluyen el campo «Entregar a Sr(a)» con el
nombre completo de una persona identificada. Publicarlos en un repositorio
público expondría datos personales, algo que la Ley N.° 29733 de Protección de
Datos Personales no permite.

## Cómo ejecutar la batería completa

1. Copie en esta carpeta los pedidos que quiera comprobar, con los nombres que
   figuran como claves en `python_expected.json`.
2. Ajuste los valores esperados de ese archivo a los de sus pedidos.
3. Ejecute la prueba:

   ```bat
   dotnet run --project tests\OrderPdfReader.Regression
   ```

Si los PDF no están, la prueba lo indica y ejecuta igualmente el resto de
comprobaciones, que usan documentos sintéticos generados durante la propia
ejecución con la misma biblioteca que usa el aplicativo.

## Recomendación

Use pedidos anonimizados: sustituya el nombre del campo «Entregar a Sr(a)» por
un valor ficticio antes de guardarlos aquí, aunque sea en un equipo local.
