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
    /// <summary>Organización propietaria del repositorio en GitHub.</summary>
    public const string Propietario = "Informatica-Oxapampa";

    /// <summary>Nombre del repositorio oficial.</summary>
    public const string Repositorio = "Generador-de-Anexos";

    /// <summary>Nombre del manifiesto adjunto a cada versión publicada.</summary>
    public const string NombreManifiesto = "update.json";

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
}
