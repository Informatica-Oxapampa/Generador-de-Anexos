using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Contenido de <c>update.json</c>, el manifiesto adjunto a cada versión
/// publicada en GitHub Releases.
/// </summary>
/// <remarks>
/// Un solo manifiesto describe los <b>dos canales</b>: el programa y las
/// plantillas de Word. Cada Release publica el manifiesto completo, aunque solo
/// cambie uno de los dos; el otro repite la versión vigente. Así la aplicación
/// resuelve ambas comprobaciones con una única petición y no puede quedarse con
/// información desparejada.
/// </remarks>
public sealed class ManifiestoActualizacion
{
    /// <summary>Versión del formato del propio manifiesto.</summary>
    [JsonPropertyName("manifiesto")]
    public int Formato { get; set; } = 1;

    /// <summary>Fecha de publicación del manifiesto, en ISO 8601.</summary>
    [JsonPropertyName("publicado")]
    public string Publicado { get; set; } = string.Empty;

    /// <summary>Etiqueta de la Release que lo contiene, por ejemplo «v1.6.0».</summary>
    [JsonPropertyName("release")]
    public string Release { get; set; } = string.Empty;

    /// <summary>Canal del programa: el instalador completo.</summary>
    [JsonPropertyName("app")]
    public PaqueteActualizacion? App { get; set; }

    /// <summary>Canal de plantillas: solo los documentos de Word.</summary>
    [JsonPropertyName("plantillas")]
    public PaqueteActualizacion? Plantillas { get; set; }
}

/// <summary>Un paquete descargable dentro del manifiesto.</summary>
public sealed class PaqueteActualizacion
{
    /// <summary>Versión publicada, en formato MAYOR.MENOR.PARCHE.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Fecha de publicación (aaaa-mm-dd).</summary>
    [JsonPropertyName("fecha")]
    public string Fecha { get; set; } = string.Empty;

    /// <summary>Dirección HTTPS del archivo, siempre en dominios de GitHub.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-256 del archivo, en hexadecimal (64 caracteres).</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Tamaño exacto del archivo en bytes.</summary>
    [JsonPropertyName("tamano")]
    public long Tamano { get; set; }

    /// <summary>Si es obligatoria, no se ofrece «Omitir esta versión».</summary>
    [JsonPropertyName("obligatoria")]
    public bool Obligatoria { get; set; }

    /// <summary>
    /// Versión instalada mínima que puede saltar directamente a esta. Sirve
    /// para forzar una actualización intermedia cuando cambia el formato de
    /// los datos guardados.
    /// </summary>
    [JsonPropertyName("versionMinima")]
    public string VersionMinima { get; set; } = string.Empty;

    /// <summary>Archivos que contiene el paquete (informativo).</summary>
    [JsonPropertyName("archivos")]
    public List<string> Archivos { get; set; } = new();

    /// <summary>Cambios principales, una línea por elemento.</summary>
    [JsonPropertyName("notas")]
    public List<string> Notas { get; set; } = new();

    /// <summary>Fecha en formato legible, o el texto original si no se entiende.</summary>
    public string FechaLegible()
        => DateTime.TryParse(Fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
            : Fecha;

    /// <summary>Tamaño legible, por ejemplo «238,4 MB» o «412 KB».</summary>
    public string TamanoLegible()
    {
        if (Tamano <= 0)
        {
            return string.Empty;
        }

        var megas = Tamano / 1024d / 1024d;

        return megas < 1
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} KB", Tamano / 1024d)
            : string.Format(CultureInfo.CurrentCulture, "{0:N1} MB", megas);
    }

    /// <summary>
    /// True si el paquete está completo y es seguro de usar: versión
    /// reconocible, dirección de confianza y hash con la longitud correcta.
    /// </summary>
    public bool EsValido(out VersionSemantica version)
        => VersionSemantica.TryParse(Version, out version)
           && Sha256.Trim().Length == 64
           && EsDescargaPermitida(Url);

    /// <summary>Acepta solo HTTPS y solo dominios de GitHub.</summary>
    public static bool EsDescargaPermitida(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var direccion)
            || !string.Equals(direccion.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var dominio in ConfiguracionActualizaciones.DominiosPermitidos)
        {
            if (string.Equals(direccion.Host, dominio, StringComparison.OrdinalIgnoreCase)
                || direccion.Host.EndsWith("." + dominio, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
