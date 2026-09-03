using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Domain.Formatting;
using GeneradorAnexos.Domain.Payments;

namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>
/// Equivalente de <c>core/generador.py: generar_anexos</c>.
/// </summary>
/// <remarks>
/// Rellena la plantilla <c>plantilla_anexos.docx</c> y luego reescribe la celda
/// combinada que sigue al rótulo «FORMA DE PAGO» con el plan vigente: un texto
/// único en modo simple, o una línea por cuota en modo múltiple.
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
            // La plantilla no tiene el bloque de forma de pago: no es un error
            // fatal, el resto del documento ya quedó generado.
            return;
        }

        if (plan.Modo == ConstructorPlanPagos.ModoUnico)
        {
            DocxTemplateEngine.EscribirCelda(celda, ConstructorPlanPagos.TextoFormaPagoUnico);
            return;
        }

        var lineas = plan.Cuotas.Select(cuota =>
        {
            var importe = cuota.Monto is null
                ? string.Empty
                : $" — {DocumentFormatting.FormatCurrency(cuota.Monto.Value)}";

            return $"{Domain.Documents.TdrLabels.EtiquetaPago(cuota.Indice - 1)} " +
                   $"({cuota.Porcentaje} %){importe}: {cuota.Condicion}";
        });

        DocxTemplateEngine.EscribirCelda(celda, string.Join("\n", lineas));
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
