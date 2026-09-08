using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeneradorAnexos.WinUI.Services;

/// <summary>
/// Compone el nombre propuesto al guardar un documento generado.
/// </summary>
/// <remarks>
/// El formato es <c>TIPO_PEDIDO_REFERENCIA_AAAA-MM-DD.docx</c>, por ejemplo
/// <c>TDR_001211_OFICINA_DE_TECNOLOGIA_2026-09-03.docx</c>.
///
/// Antes el nombre era solo el tipo y la referencia, de modo que dos documentos
/// de la misma área o del mismo proveedor proponían exactamente el mismo
/// archivo: el segundo sobrescribía al primero, o el usuario tenía que
/// renombrarlo a mano en cada generación. El número de pedido y la fecha lo
/// resuelven y, de paso, hacen la carpeta de destino mucho más fácil de
/// ordenar y de buscar.
///
/// Las partes vacías se omiten en lugar de dejar separadores sueltos, así que
/// un formulario a medio llenar sigue proponiendo un nombre correcto.
/// </remarks>
public static class NombreDocumento
{
    /// <summary>Longitud máxima de la parte descriptiva.</summary>
    private const int MaximoReferencia = 40;

    public static string Componer(
        string tipo,
        string? numeroPedido,
        string? referencia,
        string referenciaPorDefecto)
    {
        var partes = new[]
        {
            tipo,
            Limpiar(numeroPedido, 20),
            Limpiar(referencia, MaximoReferencia) is { Length: > 0 } texto
                ? texto
                : Limpiar(referenciaPorDefecto, MaximoReferencia),
            DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        return string.Join("_", partes.Where(p => !string.IsNullOrEmpty(p))) + ".docx";
    }

    /// <summary>
    /// Deja solo caracteres válidos para un nombre de archivo en Windows y
    /// sustituye los espacios por guiones bajos.
    /// </summary>
    private static string Limpiar(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var invalidos = Path.GetInvalidFileNameChars();

        var limpio = new string(texto
            .Where(c => !invalidos.Contains(c))
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray())
            .Trim()
            .Replace(' ', '_');

        // Los separadores repetidos afean el nombre sin aportar nada.
        while (limpio.Contains("__", StringComparison.Ordinal))
        {
            limpio = limpio.Replace("__", "_", StringComparison.Ordinal);
        }

        return limpio.Length > maximo ? limpio[..maximo].TrimEnd('_') : limpio;
    }
}
