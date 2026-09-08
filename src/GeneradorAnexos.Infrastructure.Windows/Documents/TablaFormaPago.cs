using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Payments;

namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>El mismo cuadro de pagos para TDR y Anexos.</summary>
internal static class TablaFormaPago
{
    internal static void Rellenar(Table tabla, PlanPagos plan)
    {
        var filas = tabla.Elements<TableRow>().ToList();
        if (filas.Count < 2 || plan.Cuotas.Count == 0)
            throw new DocumentoException("El cuadro de pagos necesita cabecera, fila modelo y cuotas.");
        var cabecera = filas[0];
        cabecera.TableRowProperties ??= new TableRowProperties();
        cabecera.TableRowProperties.RemoveAllChildren<TableHeader>();
        cabecera.TableRowProperties.AppendChild(new TableHeader());
        var modelo = filas[1];
        var total = filas.Count > 2 && DocxTemplateEngine.Normalizar(filas[^1].InnerText).Contains("TOTAL", StringComparison.Ordinal)
            ? filas[^1] : null;
        foreach (var fila in filas.Skip(2).Where(x => x != total)) fila.Remove();
        TableRow anterior = modelo;
        for (var i = 0; i < plan.Cuotas.Count; i++)
        {
            var fila = i == 0 ? modelo : DocxTemplateEngine.ClonarFila(anterior);
            fila.TableRowProperties?.RemoveAllChildren<TableRowHeight>();
            var celdas = fila.Elements<TableCell>().ToList();
            if (celdas.Count < 3) throw new DocumentoException("El cuadro de pagos necesita tres columnas.");
            if (plan.Modo == ConstructorPlanPagos.ModoMultiple)
            {
                DocxTemplateEngine.EscribirCelda(celdas[0], TdrLabels.EtiquetaPago(i), negrita: true);
                DocxTemplateEngine.EscribirCelda(celdas[1], plan.Cuotas[i].Condicion);
            }
            // En pago único se conserva la condición institucional de la plantilla TDR.
            DocxTemplateEngine.EscribirCelda(celdas[2], $"{plan.Cuotas[i].Porcentaje} %");
            anterior = fila;
        }
        if (total is null)
        {
            total = (TableRow)modelo.CloneNode(true);
            var celdas = total.Elements<TableCell>().ToList();
            DocxTemplateEngine.EscribirCelda(celdas[0], "TOTAL PORCENTAJE", negrita: true);
            celdas[0].TableCellProperties ??= new TableCellProperties();
            celdas[0].TableCellProperties!.GridSpan = new GridSpan { Val = 2 };
            if (int.TryParse(celdas[0].TableCellProperties!.TableCellWidth?.Width, out var w1) &&
                int.TryParse(celdas[1].TableCellProperties?.TableCellWidth?.Width, out var w2))
                celdas[0].TableCellProperties!.TableCellWidth = new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = (w1 + w2).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            celdas[1].Remove();
            tabla.Append(total);
        }
        total.TableRowProperties?.RemoveAllChildren<TableRowHeight>();
        DocxTemplateEngine.EscribirCelda(total.Elements<TableCell>().Last(), $"{plan.Cuotas.Sum(x => x.Porcentaje)} %", negrita: true);
    }

    internal static void AjustarAncho(Table tabla, TableCell destino)
    {
        var anchoTexto = destino.TableCellProperties?.TableCellWidth;
        if (anchoTexto?.Type?.Value != TableWidthUnitValues.Dxa || !int.TryParse(anchoTexto.Width?.Value, out var ancho))
            ancho = 8500;
        ancho = Math.Max(1200, ancho - 240); // Márgenes interiores de la celda contenedora.
        var columnas = new[] { ancho * 20 / 100, ancho * 60 / 100, 0 };
        columnas[2] = ancho - columnas[0] - columnas[1];
        tabla.TableProperties ??= new TableProperties();
        tabla.TableProperties.TableWidth = new TableWidth { Type = TableWidthUnitValues.Dxa, Width = ancho.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        tabla.TableProperties.TableIndentation = new TableIndentation { Type = TableWidthUnitValues.Dxa, Width = 0 };
        tabla.TableProperties.TableLayout = new TableLayout { Type = TableLayoutValues.Fixed };
        tabla.RemoveAllChildren<TableGrid>();
        tabla.InsertAfter(new TableGrid(columnas.Select(x => new GridColumn { Width = x.ToString(System.Globalization.CultureInfo.InvariantCulture) })), tabla.TableProperties);
        foreach (var fila in tabla.Elements<TableRow>())
        {
            var indice = 0;
            foreach (var celda in fila.Elements<TableCell>())
            {
                celda.TableCellProperties ??= new TableCellProperties();
                var span = celda.TableCellProperties.GridSpan?.Val?.Value ?? 1;
                celda.TableCellProperties.TableCellWidth = new TableCellWidth { Type = TableWidthUnitValues.Dxa,
                    Width = columnas.Skip(indice).Take(span).Sum().ToString(System.Globalization.CultureInfo.InvariantCulture) };
                indice += span;
            }
        }
    }
}
