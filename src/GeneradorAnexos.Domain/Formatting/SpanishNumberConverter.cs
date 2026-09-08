#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace GeneradorAnexos.Domain.Formatting;

/// <summary>Conversión de números a la redacción documental española.</summary>
public static class SpanishNumberConverter
{
    public const long MaximumSupportedInteger = 999_999_999;

    private static readonly string[] Units =
    [
        string.Empty, "uno", "dos", "tres", "cuatro", "cinco",
        "seis", "siete", "ocho", "nueve",
    ];

    private static readonly Dictionary<int, string> Specials =
        new Dictionary<int, string>
        {
            [10] = "diez",
            [11] = "once",
            [12] = "doce",
            [13] = "trece",
            [14] = "catorce",
            [15] = "quince",
            [16] = "dieciséis",
            [17] = "diecisiete",
            [18] = "dieciocho",
            [19] = "diecinueve",
            [20] = "veinte",
            [21] = "veintiuno",
            [22] = "veintidós",
            [23] = "veintitrés",
            [24] = "veinticuatro",
            [25] = "veinticinco",
            [26] = "veintiséis",
            [27] = "veintisiete",
            [28] = "veintiocho",
            [29] = "veintinueve",
        };

    private static readonly string[] Tens =
    [
        string.Empty, string.Empty, "veinte", "treinta", "cuarenta",
        "cincuenta", "sesenta", "setenta", "ochenta", "noventa",
    ];

    private static readonly string[] Hundreds =
    [
        string.Empty, "ciento", "doscientos", "trescientos", "cuatrocientos",
        "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos",
    ];

    /// <summary>Convierte un entero de 0 a 999 999 999 a minúsculas.</summary>
    public static string IntegerToWords(long number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number, "No se admiten cantidades negativas.");
        }

        if (number > MaximumSupportedInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number,
                $"La cantidad supera el máximo soportado ({MaximumSupportedInteger}).");
        }

        if (number == 0)
        {
            return "cero";
        }

        var millions = (int)(number / 1_000_000);
        var thousands = (int)((number % 1_000_000) / 1_000);
        var units = (int)(number % 1_000);
        var parts = new List<string>(3);

        if (millions != 0)
        {
            parts.Add(millions == 1
                ? "un millón"
                : Apocopate(GroupToWords(millions)) + " millones");
        }

        if (thousands != 0)
        {
            parts.Add(thousands == 1
                ? "mil"
                : Apocopate(GroupToWords(thousands)) + " mil");
        }

        if (units != 0)
        {
            parts.Add(GroupToWords(units));
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Convierte un monto a la forma legal: por ejemplo,
    /// <c>MIL QUINIENTOS CON 00/100 SOLES</c>.
    /// </summary>
    public static string AmountToWords(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "No se admiten cantidades negativas.");
        }

        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var wholePart = decimal.Truncate(rounded);
        if (wholePart > MaximumSupportedInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount,
                $"La cantidad supera el máximo soportado ({MaximumSupportedInteger}).");
        }

        var cents = decimal.ToInt32((rounded - wholePart) * 100m);
        var words = IntegerToWords(decimal.ToInt64(wholePart));
        return $"{words} con {cents:00}/100 soles".ToUpperInvariant();
    }

    /// <summary>Devuelve <c>palabras (cifra)</c> o el texto original si no es entero.</summary>
    public static string NumberWithWords(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!BigInteger.TryParse(
                trimmed,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer))
        {
            return value;
        }

        if (integer < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "No se admiten cantidades negativas.");
        }

        if (integer > MaximumSupportedInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"La cantidad supera el máximo soportado ({MaximumSupportedInteger}).");
        }

        var number = (long)integer;
        return $"{IntegerToWords(number)} ({number})";
    }

    public static string NumberWithWords(long value) =>
        $"{IntegerToWords(value)} ({value})";

    public static string CalendarDaysPhrase(string? value) =>
        NumberWithWords(value) + " días calendario";

    public static string CalendarDaysPhrase(long value) =>
        NumberWithWords(value) + " días calendario";

    private static string GroupToWords(int number)
    {
        if (number == 0)
        {
            return string.Empty;
        }

        if (number == 100)
        {
            return "cien";
        }

        var parts = new List<string>(2);
        var hundred = number / 100;
        var remainder = number % 100;
        if (hundred != 0)
        {
            parts.Add(Hundreds[hundred]);
        }

        if (remainder is > 0 and < 10)
        {
            parts.Add(Units[remainder]);
        }
        else if (remainder is >= 10 and < 30)
        {
            parts.Add(Specials[remainder]);
        }
        else if (remainder >= 30)
        {
            var ten = remainder / 10;
            var unit = remainder % 10;
            parts.Add(unit == 0 ? Tens[ten] : $"{Tens[ten]} y {Units[unit]}");
        }

        return string.Join(" ", parts);
    }

    private static string Apocopate(string text)
    {
        const string TwentyOne = "veintiuno";
        if (text.EndsWith(TwentyOne, StringComparison.Ordinal))
        {
            return text[..^TwentyOne.Length] + "veintiún";
        }

        return text.EndsWith("uno", StringComparison.Ordinal)
            ? text[..^3] + "un"
            : text;
    }
}
