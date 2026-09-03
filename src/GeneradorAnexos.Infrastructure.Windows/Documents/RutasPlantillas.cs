namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>
/// Resuelve dónde están las plantillas de Word.
/// </summary>
/// <remarks>
/// Hay dos orígenes posibles y el orden importa:
///
/// <list type="number">
///   <item><b>Carpeta preferida</b>, si está definida: las plantillas
///   actualizadas por el canal independiente, que viven en la carpeta de datos
///   del usuario.</item>
///   <item><b>Junto al ejecutable</b>: las que trajo el instalador.</item>
/// </list>
///
/// La resolución es por archivo, no por carpeta: si la carpeta actualizada solo
/// trae una de las dos plantillas, la otra se sigue tomando de la instalación.
/// </remarks>
public static class RutasPlantillas
{
    /// <summary>
    /// Carpeta con plantillas actualizadas. La fija la aplicación al arrancar;
    /// <c>null</c> significa usar únicamente las incluidas en la instalación.
    /// </summary>
    public static string? CarpetaPreferida { get; set; }

    public static string Anexos() => Resolver("plantilla_anexos.docx");

    public static string Tdr() => Resolver("plantilla_tdr.docx");

    /// <summary>True si ambas plantillas están presentes.</summary>
    public static bool Existen() => File.Exists(Anexos()) && File.Exists(Tdr());

    /// <summary>Carpeta de plantillas incluida con la instalación.</summary>
    public static string CarpetaIncluida => Path.Combine(AppContext.BaseDirectory, "plantillas");

    private static string Resolver(string nombre)
    {
        if (CarpetaPreferida is { Length: > 0 } carpeta)
        {
            var actualizada = Path.Combine(carpeta, nombre);
            if (File.Exists(actualizada))
            {
                return actualizada;
            }
        }

        return Path.Combine(CarpetaIncluida, nombre);
    }
}
