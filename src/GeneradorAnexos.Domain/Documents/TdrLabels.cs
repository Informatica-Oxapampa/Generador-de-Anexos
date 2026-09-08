using System.Globalization;

namespace GeneradorAnexos.Domain.Documents;

/// <summary>
/// Equivalente de las etiquetas y textos por defecto de
/// <c>core/generador_tdr.py</c>. Compartidos por la interfaz y el generador.
/// </summary>
public static class TdrLabels
{
    public static readonly string[] Ordinales =
    {
        "PRIMER", "SEGUNDO", "TERCER", "CUARTO", "QUINTO",
        "SEXTO", "SÉPTIMO", "OCTAVO", "NOVENO", "DÉCIMO",
    };

    public static readonly string[] OrdinalesMinuscula =
    {
        "primer", "segundo", "tercer", "cuarto", "quinto",
        "sexto", "séptimo", "octavo", "noveno", "décimo",
    };

    public const string DescripcionCartaDefecto =
        "El proveedor deberá presentar una carta, señalando la ejecución de las " +
        "actividades realizadas durante el plazo establecido.";

    public const string PlazoMarcador = "Plazo máximo del servicio";

    /// <summary>"PRIMER ENTREGABLE", "SEGUNDO ENTREGABLE", …</summary>
    public static string EtiquetaEntregable(int indice) =>
        indice < Ordinales.Length
            ? $"{Ordinales[indice]} ENTREGABLE"
            : $"ENTREGABLE N° {indice + 1}";

    /// <summary>"PRIMER PAGO", "SEGUNDO PAGO", …</summary>
    public static string EtiquetaPago(int indice) =>
        indice < Ordinales.Length
            ? $"{Ordinales[indice]} PAGO"
            : $"PAGO N° {indice + 1}";

    /// <summary>Condicion sugerida para el pago del entregable indicado.</summary>
    public static string CondicionPagoDefecto(int indice)
    {
        var ordinal = indice < OrdinalesMinuscula.Length
            ? OrdinalesMinuscula[indice]
            : $"N° {indice + 1}";

        return $"A la presentación del {ordinal} Entregable, previo informe de " +
               "conformidad de la Oficina de Abastecimiento.";
    }

    /// <summary>Extrae hasta tres dígitos de un plazo nuevo o guardado anteriormente.</summary>
    public static string ExtraerCantidadDias(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        var digitos = new string(texto.SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(char.IsDigit).Take(3).ToArray());
        return int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var dias) && dias > 0
            ? dias.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>Convierte el valor numérico de la interfaz en «30 días».</summary>
    public static string DiasConSufijo(string? valor)
    {
        var dias = ExtraerCantidadDias(valor);
        return dias.Length == 0 ? string.Empty : dias == "1" ? "1 día" : $"{dias} días";
    }

    /// <summary>Reparte 100 % entre <paramref name="cantidad"/> pagos (suma exacta).</summary>
    public static int[] DistribuirPorcentajes(int cantidad)
    {
        if (cantidad <= 0)
        {
            return System.Array.Empty<int>();
        }

        var baseValor = 100 / cantidad;
        var valores = new int[cantidad];
        for (var i = 0; i < cantidad; i++)
        {
            valores[i] = baseValor;
        }

        // El ultimo absorbe el resto, igual que el original.
        valores[cantidad - 1] += 100 - (baseValor * cantidad);
        return valores;
    }
}
