#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GeneradorAnexos.Domain.Validation;

/// <summary>Validadores puros equivalentes a los usados por la aplicación Python.</summary>
public static class FieldValidators
{
    public const int LongitudDni = 8;
    public const int LongitudRuc = 11;
    public const int LongitudCci = 20;
    public const int LongitudTelefono = 9;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClasificadorRegex = new(
        @"^[0-9]{1,2}(?:\.[0-9]{1,2}){5}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> PrefijosRuc = new(StringComparer.Ordinal)
    {
        "10", "15", "16", "17", "20",
    };

    public static bool IsValidDni(string? text) =>
        IsAllDigits(text) && text!.Length == LongitudDni;

    /// <summary>Valida longitud, prefijo peruano y dígito verificador módulo 11.</summary>
    public static bool IsValidRuc(string? text)
    {
        if (!IsAllDigits(text) || text!.Length != LongitudRuc)
        {
            return false;
        }

        return PrefijosRuc.Contains(text[..2]) && HasValidRucCheckDigit(text);
    }

    public static bool HasValidRucCheckDigit(string? ruc)
    {
        if (!IsAllDigits(ruc) || ruc!.Length != LongitudRuc)
        {
            return false;
        }

        return CalculateRucCheckDigit(ruc[..10]) == DigitValue(ruc[10]);
    }

    /// <summary>Construye un RUC de persona natural con prefijo 10.</summary>
    /// <returns>Cadena vacía cuando el DNI no tiene exactamente ocho dígitos.</returns>
    public static string Ruc10FromDni(string? dni)
    {
        if (!IsValidDni(dni))
        {
            return string.Empty;
        }

        var base10 = "10" + dni;
        return base10 + CalculateRucCheckDigit(base10).ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsValidCci(string? text) =>
        IsAllDigits(text) && text!.Length == LongitudCci;

    public static bool IsValidEmail(string? text) =>
        text is not null && text.Trim().Length <= 254 && EmailRegex.IsMatch(text.Trim());

    public static bool IsValidPhone(string? text)
    {
        if (text is null)
        {
            return false;
        }

        var digits = 0;
        foreach (var character in text)
        {
            if (character is >= '0' and <= '9')
            {
                digits++;
            }
            else if (character != ' ')
            {
                return false;
            }
        }

        return digits == LongitudTelefono;
    }

    public static bool IsValidClassifier(string? text) =>
        text is not null && ClasificadorRegex.IsMatch(text.Trim()) && text.Trim().StartsWith("2.", StringComparison.Ordinal);

    public static bool IsNonEmptyText(string? text) =>
        !string.IsNullOrWhiteSpace(text);

    /// <summary>Valida una secuencia de dígitos que representa un entero mayor que cero.</summary>
    public static bool IsPositiveInteger(string? text)
    {
        if (!IsAllDigits(text))
        {
            return false;
        }

        foreach (var character in text!)
        {
            if (character > '0')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var character in text)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int CalculateRucCheckDigit(string base10)
    {
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var index = 0; index < weights.Length; index++)
        {
            sum += DigitValue(base10[index]) * weights[index];
        }

        var control = 11 - (sum % 11);
        return control switch
        {
            10 => 0,
            11 => 1,
            _ => control,
        };
    }

    private static int DigitValue(char character) => character - '0';
}
