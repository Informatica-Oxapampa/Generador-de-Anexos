using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Domain.Payments;

namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>
/// Equivalente de <c>core/generador.py: generar_anexos</c>.
/// </summary>
/// <remarks>
/// Rellena la plantilla <c>plantilla_anexos.docx</c> y luego reescribe la celda
/// combinada que sigue al rótulo «FORMA DE PAGO» con el mismo cuadro de pagos
/// del TDR, calculado desde el plan vigente.
/// </remarks>
public sealed class AnnexDocumentGenerator
{
#pragma warning disable CA1822
    public Task GenerateAsync(
        IReadOnlyDictionary<string, string> contexto,
        string destino,
        PlanPagos plan,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocxTemplateEngine.Renderizar(
                RutasPlantillas.Anexos(), destino, contexto,
                documento => AplicarFormaPago(documento, plan));
        }, cancellationToken);
#pragma warning restore CA1822

    private static void AplicarFormaPago(WordprocessingDocument documento, PlanPagos plan)
    {
        var cuerpo = documento.MainDocumentPart?.Document?.Body
            ?? throw new DocumentoException("El documento generado no tiene cuerpo válido.");
        var celda = CeldaFormaPago(cuerpo);

        if (celda is null)
        {
            throw new DocumentoException(
                "La plantilla de anexos no contiene el bloque 'FORMA DE PAGO' esperado.");
        }

        if (plan.Cuotas.Count == 1)
        {
            foreach (var tablaAnterior in celda.Elements<Table>().ToList()) tablaAnterior.Remove();
            DocxTemplateEngine.EscribirCelda(celda, ConstructorPlanPagos.TextoPagoUnicoAnexos);
            celda.Ancestors<TableRow>().FirstOrDefault()?.TableRowProperties?.RemoveAllChildren<TableRowHeight>();
            return;
        }

        using var tdr = WordprocessingDocument.Open(RutasPlantillas.Tdr(), false);
        var cuerpoTdr = tdr.MainDocumentPart?.Document?.Body
            ?? throw new DocumentoException("La plantilla TDR no tiene cuerpo válido.");
        var modelo = DocxTemplateEngine.BuscarTabla(cuerpoTdr, "PAGO", "PORCENTAJE")
            ?? throw new DocumentoException("La plantilla TDR no contiene el cuadro de pagos esperado.");
        var tabla = (Table)modelo.CloneNode(true);
        TablaFormaPago.Rellenar(tabla, plan);
        TablaFormaPago.AjustarAncho(tabla, celda);
        // Los pagos múltiples conservan la introducción editable y el cuadro del TDR.
        if (celda.InnerText.Contains("Según los Términos", StringComparison.OrdinalIgnoreCase))
            DocxTemplateEngine.EscribirCelda(celda, "Pagos Periódicos");
        foreach (var anterior in celda.Elements<Table>().ToList()) anterior.Remove();
        if (!celda.Elements<Paragraph>().Any())
            celda.Append(new Paragraph(new Run(new Text("Pagos Periódicos"))));
        // Word exige un párrafo final después de una tabla anidada: sin texto ni espaciado.
        celda.Append(tabla, new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0", Line = "1", LineRule = LineSpacingRuleValues.Exact })));
        // La fila contenedora debe crecer con el número de pagos.
        celda.Ancestors<TableRow>().FirstOrDefault()?.TableRowProperties?.RemoveAllChildren<TableRowHeight>();
    }

    /// <summary>
    /// Devuelve la celda combinada situada bajo el rótulo «FORMA DE PAGO».
    /// </summary>
    private static TableCell? CeldaFormaPago(Body cuerpo)
    {
        foreach (var tabla in cuerpo.Descendants<Table>())
        {
            var filas = tabla.Elements<TableRow>().ToList();

            for (var i = 0; i < filas.Count; i++)
            {
                var textoFila = DocxTemplateEngine.Normalizar(filas[i].InnerText);
                if (!textoFila.Contains("FORMA DE PAGO", StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= filas.Count)
                {
                    throw new DocumentoException(
                        "La plantilla no contiene contenido después de 'FORMA DE PAGO'.");
                }

                var celdas = filas[i + 1].Elements<TableCell>().ToList();
                if (celdas.Count == 0)
                {
                    throw new DocumentoException(
                        "La fila de 'FORMA DE PAGO' no tiene la celda combinada esperada.");
                }

                return celdas[0];
            }
        }

        return null;
    }
}
