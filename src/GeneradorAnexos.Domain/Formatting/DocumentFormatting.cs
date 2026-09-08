#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace GeneradorAnexos.Domain.Formatting;

/// <summary>Partes de fecha usadas como marcadores de las plantillas.</summary>
public readonly record struct DocumentDateParts(string Dia, string Mes, string Anio);

/// <summary>Formato y parseo puro para documentos institucionales.</summary>
public static class DocumentFormatting
{
    private static readonly string[] MonthNames =
    [
        string.Empty,
        "enero",
        "febrero",
        "marzo",
        "abril",
        "mayo",
        "junio",
        "julio",
        "agosto",
        "septiembre",
        "octubre",
        "noviembre",
        "diciembre",
    ];

    public static string FormatCurrency(decimal amount) =>
        "S/ " + FormatCurrencyWithoutSymbol(amount);

    public static string FormatCurrencyWithoutSymbol(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Replica el parseo tolerante del formulario histórico: elimina el símbolo,
    /// las comas y cualquier carácter que no sea un dígito o un punto.
    /// </summary>
    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = default;
        if (text is null)
        {
            return false;
        }

        var withoutSymbol = text
            .Replace("S/", string.Empty, StringComparison.Ordinal)
            .Replace("s/", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        var builder = new StringBuilder(withoutSymbol.Length);
        foreach (var character in withoutSymbol)
        {
            if (char.IsDigit(character) || character == '.')
            {
                builder.Append(character);
            }
        }

        var cleaned = builder.ToString();
        if (cleaned.Length == 0 || cleaned == ".")
        {
            return false;
        }

        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out amount);
    }

    /// <summary>Devuelve solo los dígitos y opcionalmente limita su longitud.</summary>
    public static string OnlyDigits(string? text, int? maximum = null)
    {
        if (maximum < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum), maximum, "La longitud máxima no puede ser negativa.");
        }

        var source = text ?? string.Empty;
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (!char.IsDigit(character))
            {
                continue;
            }

            builder.Append(character);
            if (maximum is > 0 && builder.Length == maximum.Value)
            {
                break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Aplica el formato <c>999 999 999</c> a un máximo de nueve dígitos.</summary>
    public static string FormatPhone(string? text)
    {
        var digits = OnlyDigits(text, 9);
        if (digits.Length <= 3)
        {
            return digits;
        }

        if (digits.Length <= 6)
        {
            return digits[..3] + " " + digits[3..];
        }

        return digits[..3] + " " + digits[3..6] + " " + digits[6..];
    }

    /// <summary>Obtiene día, mes en español y año para el contexto documental.</summary>
    public static DocumentDateParts GetDateParts(int day, int month, int year)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month), month, "Mes fuera de rango (1-12).");
        }

        return new DocumentDateParts(
            day.ToString(CultureInfo.InvariantCulture),
            MonthNames[month],
            year.ToString(CultureInfo.InvariantCulture));
    }

    public static DocumentDateParts GetDateParts(DateOnly date) =>
        GetDateParts(date.Day, date.Month, date.Year);

    /// <summary>Fecha peruana <c>dd/MM/aaaa</c>.</summary>
    public static string FormatPeruvianDate(int day, int month, int year) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{day:00}/{month:00}/{year:0000}");

    public static string FormatPeruvianDate(DateOnly date) =>
        FormatPeruvianDate(date.Day, date.Month, date.Year);
}
