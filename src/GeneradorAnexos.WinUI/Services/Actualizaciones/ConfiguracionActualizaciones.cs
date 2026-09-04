namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>
/// Único lugar del proyecto donde se define el repositorio oficial y las
/// direcciones del sistema de actualización.
/// </summary>
/// <remarks>
/// Ningún otro archivo construye direcciones de GitHub. Si algún día cambia el
/// repositorio, la organización o el nombre de los paquetes, se cambia aquí y
/// en ningún sitio más.
///
/// <b>Distinción importante entre las dos ramas del flujo de trabajo:</b>
/// <list type="bullet">
///   <item><b>Desarrollo.</b> Los commits que se suben con GitHub Desktop van a
///   la rama principal del repositorio. La aplicación instalada en los equipos
///   <i>nunca</i> los consulta.</item>
///   <item><b>Publicación.</b> Solo cuando se crea una <i>Release</i> con su
///   etiqueta y su manifiesto, los equipos ven una versión nueva.</item>
/// </list>
/// Por eso el manifiesto se lee de <c>releases/latest/download/…</c> y no del
/// contenido de la rama: cien commits no provocan ninguna actualización.
/// </remarks>
public static class ConfiguracionActualizaciones
{
    public const long TamanoMaximoManifiesto = 512 * 1024;
    public const long TamanoMaximoFirmaManifiesto = 128 * 1024;
    public const long TamanoMaximoInstalador = 700L * 1024 * 1024;
    public const long TamanoMaximoPlantillas = 25L * 1024 * 1024;
    public const int MaximoRedirecciones = 5;

    /// <summary>Organización propietaria del repositorio en GitHub.</summary>
    public const string Propietario = "Informatica-Oxapampa";

    /// <summary>Nombre del repositorio oficial.</summary>
    public const string Repositorio = "Generador-de-Anexos";

    /// <summary>Nombre del manifiesto adjunto a cada versión publicada.</summary>
    public const string NombreManifiesto = "update.json";

    /// <summary>Firma CMS separada del manifiesto.</summary>
    public const string NombreFirmaManifiesto = "update.json.p7s";

    /// <summary>Versión del formato de manifiesto que entiende esta aplicación.</summary>
    public const int FormatoManifiesto = 1;

    /// <summary>Página principal del repositorio.</summary>
    public static string UrlRepositorio
        => $"https://github.com/{Propietario}/{Repositorio}";

    /// <summary>
    /// Manifiesto de la última versión publicada. GitHub redirige
    /// <c>latest</c> a la Release más reciente marcada como tal, así que esta
    /// dirección no cambia nunca.
    /// </summary>
    public static string UrlManifiesto
        => $"{UrlRepositorio}/releases/latest/download/{NombreManifiesto}";

    public static string UrlFirmaManifiesto
        => $"{UrlRepositorio}/releases/latest/download/{NombreFirmaManifiesto}";

    /// <summary>Listado de versiones publicadas, para «Ver notas de la versión».</summary>
    public static string UrlVersiones => $"{UrlRepositorio}/releases";

    /// <summary>
    /// Dominios desde los que se acepta descargar. Un manifiesto manipulado no
    /// puede redirigir una descarga fuera de GitHub.
    /// </summary>
    public static readonly string[] DominiosPermitidos =
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    };

    /// <summary>
    /// Huellas SHA-256 de los certificados Authenticode autorizados para
    /// publicar el instalador. Deben completarse con el certificado
    /// institucional antes de habilitar una publicación automática.
    /// Mantener la lista vacía hace que el actualizador falle de forma segura.
    /// </summary>
    public static readonly string[] FirmantesPermitidosSha256 = Array.Empty<string>();

    public static bool FirmaInstitucionalConfigurada
        => FirmantesPermitidosSha256.Length > 0 &&
           FirmantesPermitidosSha256.All(EsHuellaSha256Valida);

    private static bool EsHuellaSha256Valida(string? huella)
    {
        if (string.IsNullOrWhiteSpace(huella))
        {
            return false;
        }

        var normalizada = huella.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (normalizada.Length != 64)
        {
            return false;
        }

        try
        {
            _ = Convert.FromHexString(normalizada);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
