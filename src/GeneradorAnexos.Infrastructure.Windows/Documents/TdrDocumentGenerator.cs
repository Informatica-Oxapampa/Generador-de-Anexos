using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Domain.Documents;
using GeneradorAnexos.Domain.Models;
using GeneradorAnexos.Domain.Payments;

namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>
/// Equivalente de <c>core/generador_tdr.py: generar_tdr</c>.
/// </summary>
/// <remarks>
/// Rellena <c>plantilla_tdr.docx</c>, sustituye los tokens de viñetas
/// (requisitos adicionales, formación, experiencia y capacitaciones) y, en modo
/// múltiple, reconstruye las tablas de entregables y de forma de pago clonando
/// su fila modelo, igual que el original.
/// </remarks>
public sealed class TdrDocumentGenerator
{
    // Tokens con los que la plantilla marca las secciones de viñetas.
    private const string TokenRequisitos = "###REQ_ADICIONALES###";
    private const string TokenFormacion = "###FORMACION_ACADEMICA###";
    private const string TokenExperiencia = "###EXPERIENCIA_VINETAS###";
    private const string TokenCapacitaciones = "###CAPACITACIONES_VINETAS###";

#pragma warning disable CA1822
    public Task GenerateAsync(
        IReadOnlyDictionary<string, string> contexto,
        string destino,
        TdrPayload tdr,
        PlanPagos plan,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ctx = new Dictionary<string, string>(contexto)
            {
                ["REQUISITOS"] = TokenRequisitos,
                ["FORMACION_ACADEMICA"] = TokenFormacion,
                ["EXPERIENCIA"] = TokenExperiencia,
                ["CAPACITACIONES"] = TokenCapacitaciones,
            };

            DocxTemplateEngine.Renderizar(
                RutasPlantillas.Tdr(), destino, ctx,
                documento => PostProcesar(documento, tdr, plan));
        }, cancellationToken);
#pragma warning restore CA1822

    private static void PostProcesar(WordprocessingDocument documento, TdrPayload tdr, PlanPagos plan)
    {
        var cuerpo = documento.MainDocumentPart?.Document?.Body
            ?? throw new DocumentoException("El documento generado no tiene cuerpo válido.");

        DocxTemplateEngine.AplicarVinetas(cuerpo, TokenRequisitos, Limpiar(tdr.Requisitos));
        DocxTemplateEngine.AplicarVinetas(cuerpo, TokenFormacion, Limpiar(tdr.Formacion));
        DocxTemplateEngine.AplicarVinetas(cuerpo, TokenExperiencia, Limpiar(tdr.Experiencia));
        DocxTemplateEngine.AplicarVinetas(cuerpo, TokenCapacitaciones, Limpiar(tdr.Capacitaciones));

        if (plan.Modo != ConstructorPlanPagos.ModoMultiple)
        {
            return;
        }

        ReconstruirEntregables(cuerpo, plan);
        ReconstruirPagos(cuerpo, plan);
    }

    private static List<string> Limpiar(IEnumerable<string?>? valores) => valores?
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(v => v!.Trim())
        .ToList() ?? new List<string>();

    /// <summary>Reconstruye la tabla de entregables clonando su fila modelo.</summary>
    private static void ReconstruirEntregables(Body cuerpo, PlanPagos plan)
    {
        var tabla = DocxTemplateEngine.BuscarTabla(cuerpo, "ENTREGABLE", "PLAZO");
        if (tabla is null)
        {
            throw new DocumentoException(
                "La plantilla TDR no contiene la tabla de ENTREGABLE y PLAZO esperada.");
        }

        var filas = tabla.Elements<TableRow>().ToList();
        if (filas.Count < 2)
        {
            throw new DocumentoException("La tabla de entregables no contiene una fila modelo válida.");
        }

        var modelo = filas[1];

        // Elimina las filas de datos previas, conservando la cabecera y el modelo.
        foreach (var sobrante in filas.Skip(2).ToList())
        {
            sobrante.Remove();
        }

        TableRow anterior = modelo;
        for (var i = 0; i < plan.Cuotas.Count; i++)
        {
            var fila = i == 0 ? modelo : DocxTemplateEngine.ClonarFila(anterior);
            var cuota = plan.Cuotas[i];
            var celdas = fila.Elements<TableCell>().ToList();

            if (celdas.Count < 3)
            {
                throw new DocumentoException(
                    "La fila modelo de entregables debe contener al menos tres celdas.");
            }

            DocxTemplateEngine.EscribirCelda(celdas[0], TdrLabels.EtiquetaEntregable(i), negrita: true);
            DocxTemplateEngine.EscribirCelda(celdas[1], cuota.Descripcion);
            DocxTemplateEngine.EscribirCelda(celdas[2], cuota.Plazo);

            anterior = fila;
        }
    }

    /// <summary>Reconstruye la tabla de forma de pago y su fila de total.</summary>
    private static void ReconstruirPagos(Body cuerpo, PlanPagos plan)
    {
        var tabla = DocxTemplateEngine.BuscarTabla(cuerpo, "PAGO", "PORCENTAJE");
        if (tabla is null)
        {
            throw new DocumentoException(
                "La plantilla TDR no contiene la tabla de PAGO y PORCENTAJE esperada.");
        }

        var filas = tabla.Elements<TableRow>().ToList();
        if (filas.Count < 2)
        {
            throw new DocumentoException("La tabla de pagos no contiene una fila modelo válida.");
        }

        var modelo = filas[1];

        // Conserva la última fila si la plantilla trae un TOTAL fijo.
        var filaTotal = filas.Count > 2 &&
            DocxTemplateEngine.Normalizar(filas[^1].InnerText).Contains("TOTAL", StringComparison.Ordinal)
                ? filas[^1]
                : null;

        foreach (var sobrante in filas.Skip(2).Where(f => f != filaTotal).ToList())
        {
            sobrante.Remove();
        }

        TableRow anterior = modelo;
        for (var i = 0; i < plan.Cuotas.Count; i++)
        {
            var fila = i == 0 ? modelo : DocxTemplateEngine.ClonarFila(anterior);
            var cuota = plan.Cuotas[i];
            var celdas = fila.Elements<TableCell>().ToList();

            if (celdas.Count < 3)
            {
                throw new DocumentoException(
                    "La fila modelo de pagos debe contener al menos tres celdas.");
            }

            DocxTemplateEngine.EscribirCelda(celdas[0], TdrLabels.EtiquetaPago(i), negrita: true);
            DocxTemplateEngine.EscribirCelda(celdas[1], cuota.Condicion);
            DocxTemplateEngine.EscribirCelda(celdas[2], $"{cuota.Porcentaje} %");

            anterior = fila;
        }

        if (filaTotal is not null)
        {
            var celdas = filaTotal.Elements<TableCell>().ToList();
            if (celdas.Count == 0)
            {
                throw new DocumentoException(
                    "La fila TOTAL de pagos no contiene ninguna celda.");
            }

            DocxTemplateEngine.EscribirCelda(
                celdas[^1],
                $"{plan.Cuotas.Sum(c => c.Porcentaje)} %",
                negrita: true);
        }
    }
}
