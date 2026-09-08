using System;
using System.Globalization;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Versión con formato <c>MAYOR.MENOR.PARCHE</c> (Semantic Versioning).
/// </summary>
/// <remarks>
/// La comparación es numérica, campo por campo, nunca alfabética. Comparar
/// como texto daría resultados incorrectos en cuanto se llegue a dos cifras:
/// «2.10.0» es posterior a «2.9.0», pero como texto sería anterior.
///
/// Se aceptan la «v» inicial de las etiquetas de GitHub (<c>v1.0.0</c>) y una
/// cuarta cifra de compilación, que se ignora en la comparación.
/// </remarks>
public readonly struct VersionSemantica : IEquatable<VersionSemantica>, IComparable<VersionSemantica>
{
    public VersionSemantica(int mayor, int menor, int parche)
    {
        Mayor = mayor;
        Menor = menor;
        Parche = parche;
    }

    /// <summary>Cambio importante, posiblemente incompatible.</summary>
    public int Mayor { get; }

    /// <summary>Nuevas funcionalidades compatibles.</summary>
    public int Menor { get; }

    /// <summary>Correcciones.</summary>
    public int Parche { get; }

    public static bool operator >(VersionSemantica a, VersionSemantica b) => a.CompareTo(b) > 0;

    public static bool operator <(VersionSemantica a, VersionSemantica b) => a.CompareTo(b) < 0;

    public static bool operator >=(VersionSemantica a, VersionSemantica b) => a.CompareTo(b) >= 0;

    public static bool operator <=(VersionSemantica a, VersionSemantica b) => a.CompareTo(b) <= 0;

    public static bool operator ==(VersionSemantica a, VersionSemantica b) => a.Equals(b);

    public static bool operator !=(VersionSemantica a, VersionSemantica b) => !a.Equals(b);

    /// <summary>
    /// Interpreta un texto de versión. Devuelve <c>false</c> si no tiene al
    /// menos una cifra mayor y una menor reconocibles.
    /// </summary>
    public static bool TryParse(string? texto, out VersionSemantica version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpio = texto.Trim();

        if (limpio.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            limpio = limpio[1..];
        }

        var partes = limpio.Split('.');
        // Canal estable: no convertir una preversión ni un parche inválido
        // en una versión final. Se conserva el formato de ensamblado x.y.z.r.
        if (partes.Length is < 2 or > 4 || partes.Any(parte =>
                parte.Length == 0 || parte.Any(c => c is < '0' or > '9') ||
                !int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        if (!int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mayor)
            || !int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var menor))
        {
            return false;
        }

        var parche = 0;
        if (partes.Length > 2)
        {
            parche = int.Parse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture);
        }

        if (mayor < 0 || menor < 0 || parche < 0)
        {
            return false;
        }

        version = new VersionSemantica(mayor, menor, parche);
        return true;
    }

    public int CompareTo(VersionSemantica other)
    {
        if (Mayor != other.Mayor)
        {
            return Mayor.CompareTo(other.Mayor);
        }

        if (Menor != other.Menor)
        {
            return Menor.CompareTo(other.Menor);
        }

        return Parche.CompareTo(other.Parche);
    }

    public bool Equals(VersionSemantica other)
        => Mayor == other.Mayor && Menor == other.Menor && Parche == other.Parche;

    public override bool Equals(object? obj) => obj is VersionSemantica other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Mayor, Menor, Parche);

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Mayor}.{Menor}.{Parche}");
}
